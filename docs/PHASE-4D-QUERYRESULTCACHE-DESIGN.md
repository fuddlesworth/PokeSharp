# Phase 4D: QueryResultCache Design

**Status**: Design Phase
**Date**: 2025-11-09
**Author**: System Architecture Designer
**Related**: PHASE-4-ARCHITECTURE.md, ParallelQueryExecutor.cs, QueryCache.cs

---

## Executive Summary

This document specifies the design for **QueryResultCache**, a thread-safe, invalidation-aware cache for ECS query results in PokeSharp's ParallelQueryExecutor. The cache will store **Entity[] results** from queries to avoid redundant archetype traversals when the same query is executed multiple times within a frame or across frames with stable entity populations.

**Key Design Principles:**
- **Safety First**: Incorrect cache invalidation = subtle bugs
- **Opt-In**: Cache must be explicitly enabled per query
- **Thread-Safe**: Multiple systems query in parallel
- **Smart Invalidation**: Event-based tracking of structural changes
- **Memory Conscious**: LRU eviction with configurable limits

---

## 1. Architecture Overview

### 1.1 Cache Hierarchy

```
┌─────────────────────────────────────────────────────┐
│          ParallelQueryExecutor                      │
│  ┌───────────────────────────────────────────────┐  │
│  │  QueryResultCache (New)                       │  │
│  │  ├─ CachedQueryResult<T>                      │  │
│  │  │  ├─ Entity[] Results (ArrayPool)           │  │
│  │  │  ├─ int Count                              │  │
│  │  │  ├─ long Version                           │  │
│  │  │  └─ long AccessTimestamp                   │  │
│  │  │                                             │  │
│  │  ├─ InvalidationTracker                       │  │
│  │  │  ├─ long GlobalVersion (increments)        │  │
│  │  │  ├─ HashSet<Type> ModifiedComponents       │  │
│  │  │  └─ bool StructuralChange                  │  │
│  │  │                                             │  │
│  │  └─ CacheStatistics                           │  │
│  │     ├─ long HitCount                          │  │
│  │     ├─ long MissCount                         │  │
│  │     ├─ long InvalidationCount                 │  │
│  │     └─ long MemoryBytes                       │  │
│  └───────────────────────────────────────────────┘  │
│  ┌───────────────────────────────────────────────┐  │
│  │  QueryCache (Existing)                        │  │
│  │  └─ ConcurrentDictionary<string, QueryDesc>   │  │
│  └───────────────────────────────────────────────┘  │
└─────────────────────────────────────────────────────┘
```

### 1.2 Two-Tier Caching Strategy

1. **QueryCache (Existing)**: Caches `QueryDescription` objects (metadata)
2. **QueryResultCache (New)**: Caches `Entity[]` results (data)

---

## 2. Cache Key Strategy

### 2.1 Cache Key Design

**Challenge**: Uniquely identify queries that can use cached results.

**Solution**: Multi-factor key combining:
1. **Component Types** (sorted by TypeHandle for consistency)
2. **Query Filters** (None/Any/All modifiers)
3. **Method Signature** (generic parameter count)

```csharp
/// <summary>
/// Immutable cache key for query results.
/// Uses structural equality for dictionary lookups.
/// </summary>
public readonly struct QueryCacheKey : IEquatable<QueryCacheKey>
{
    // Component types involved in the query (sorted)
    private readonly int[] _componentTypeHashes;

    // Query filter flags (WithAll, WithNone, WithAny)
    private readonly QueryFilterFlags _filterFlags;

    // Generic method arity (1, 2, 3, 4 components)
    private readonly byte _genericArity;

    // Pre-computed hash code for fast dictionary lookup
    private readonly int _hashCode;

    public QueryCacheKey(QueryDescription query, int genericArity)
    {
        // Extract component types from query
        var types = ExtractComponentTypes(query);

        // Sort by TypeHandle for consistent hashing
        _componentTypeHashes = types
            .OrderBy(t => t.GetHashCode())
            .Select(t => t.GetHashCode())
            .ToArray();

        _filterFlags = ExtractFilterFlags(query);
        _genericArity = (byte)genericArity;

        // Compute hash once during construction
        _hashCode = ComputeHashCode();
    }

    private int ComputeHashCode()
    {
        var hash = new HashCode();
        hash.Add(_genericArity);
        hash.Add(_filterFlags);

        foreach (var typeHash in _componentTypeHashes)
            hash.Add(typeHash);

        return hash.ToHashCode();
    }

    public override int GetHashCode() => _hashCode;

    public bool Equals(QueryCacheKey other)
    {
        if (_hashCode != other._hashCode) return false;
        if (_genericArity != other._genericArity) return false;
        if (_filterFlags != other._filterFlags) return false;

        return _componentTypeHashes.SequenceEqual(other._componentTypeHashes);
    }
}
```

### 2.2 Cache Key Examples

```csharp
// Query: Position + Velocity
// Key: { types: [Position, Velocity], filter: WithAll, arity: 2 }
ExecuteParallel<Position, Velocity>(query, action);

// Query: Position + Velocity - Dead
// Key: { types: [Position, Velocity, Dead], filter: WithAll|WithNone, arity: 2 }
ExecuteParallel<Position, Velocity>(queryWithNone, action);

// Different queries = different keys:
// ExecuteParallel<Position>(...)        => Key1
// ExecuteParallel<Position, Velocity>() => Key2 (different!)
```

---

## 3. Cache Storage Design

### 3.1 Cached Result Structure

```csharp
/// <summary>
/// Cached query result with version tracking and pooled storage.
/// </summary>
internal sealed class CachedQueryResult : IDisposable
{
    // Pooled entity array (returned to ArrayPool when invalidated)
    public Entity[] Entities { get; private set; }

    // Actual entity count (array may be larger due to pooling)
    public int Count { get; private set; }

    // Global version when this result was cached
    public long Version { get; private set; }

    // Last access timestamp (for LRU eviction)
    public long AccessTimestamp { get; set; }

    // Component types this query depends on (for partial invalidation)
    public Type[] DependentTypes { get; private set; }

    // Thread-safety: only one thread can read/update at a time
    private readonly ReaderWriterLockSlim _lock = new();

    public CachedQueryResult(Entity[] entities, int count, long version, Type[] types)
    {
        Entities = entities;
        Count = count;
        Version = version;
        DependentTypes = types;
        AccessTimestamp = Stopwatch.GetTimestamp();
    }

    /// <summary>
    /// Copy entities to output array.
    /// Thread-safe read operation.
    /// </summary>
    public void CopyTo(Entity[] destination)
    {
        _lock.EnterReadLock();
        try
        {
            Array.Copy(Entities, 0, destination, 0, Count);
            AccessTimestamp = Stopwatch.GetTimestamp();
        }
        finally
        {
            _lock.ExitReadLock();
        }
    }

    /// <summary>
    /// Check if this cached result is still valid.
    /// </summary>
    public bool IsValid(long currentVersion)
    {
        _lock.EnterReadLock();
        try
        {
            return Version >= currentVersion;
        }
        finally
        {
            _lock.ExitReadLock();
        }
    }

    public void Dispose()
    {
        _lock.EnterWriteLock();
        try
        {
            if (Entities != null)
            {
                ArrayPool<Entity>.Shared.Return(Entities);
                Entities = null!;
            }
        }
        finally
        {
            _lock.ExitWriteLock();
        }

        _lock.Dispose();
    }
}
```

### 3.2 Main Cache Container

```csharp
/// <summary>
/// Thread-safe cache for query results with automatic invalidation.
/// Uses ConcurrentDictionary for parallel access.
/// </summary>
public sealed class QueryResultCache : IDisposable
{
    // Cache storage: Key -> CachedResult
    private readonly ConcurrentDictionary<QueryCacheKey, CachedQueryResult> _cache = new();

    // Global version counter (increments on structural changes)
    private long _globalVersion = 0;

    // Configuration
    private readonly QueryResultCacheConfig _config;

    // Statistics tracking
    private readonly CacheStatistics _stats = new();

    // LRU eviction tracking
    private readonly SemaphoreSlim _evictionLock = new(1, 1);

    public QueryResultCache(QueryResultCacheConfig config)
    {
        _config = config ?? throw new ArgumentNullException(nameof(config));
    }

    // ... (methods in next section)
}
```

### 3.3 Memory Management

**Storage Strategy**:
- Use `ArrayPool<Entity>.Shared` for cached Entity arrays
- Return arrays to pool when cache entries are evicted
- Track total memory usage in bytes

**LRU Eviction**:
```csharp
private void EnforceMemoryLimit()
{
    if (_cache.Count < _config.MaxCachedQueries) return;

    _evictionLock.Wait();
    try
    {
        // Find oldest entries by AccessTimestamp
        var toEvict = _cache
            .OrderBy(kvp => kvp.Value.AccessTimestamp)
            .Take(_cache.Count - _config.MaxCachedQueries + 1)
            .Select(kvp => kvp.Key)
            .ToList();

        foreach (var key in toEvict)
        {
            if (_cache.TryRemove(key, out var result))
            {
                result.Dispose(); // Returns array to pool
                _stats.EvictionCount++;
            }
        }
    }
    finally
    {
        _evictionLock.Release();
    }
}
```

---

## 4. Invalidation Strategy

### 4.1 Invalidation Events

**Arch ECS v2.1.0** does not provide built-in structural change events, so we must track:

1. **Entity Creation** (`World.Create(...)`)
2. **Entity Destruction** (`World.Destroy(...)`)
3. **Component Addition** (`Entity.Add<T>()`)
4. **Component Removal** (`Entity.Remove<T>()`)

**Solution**: Wrap World operations with invalidation tracking.

### 4.2 Invalidation Modes

```csharp
public enum InvalidationMode
{
    /// <summary>
    /// Invalidate ALL cached queries on any structural change.
    /// Safest but least performant.
    /// </summary>
    Global = 0,

    /// <summary>
    /// Invalidate only queries that depend on modified component types.
    /// Example: Adding Velocity only invalidates queries with Velocity.
    /// </summary>
    ComponentBased = 1,

    /// <summary>
    /// Cache is valid for the entire frame, invalidated at frame start.
    /// Assumes no structural changes during frame.
    /// </summary>
    PerFrame = 2,

    /// <summary>
    /// Manual invalidation only (for advanced users).
    /// WARNING: Incorrect usage can cause stale data bugs!
    /// </summary>
    Manual = 3
}
```

### 4.3 Global Invalidation (Default)

**Approach**: Increment global version counter on any structural change.

```csharp
public void InvalidateAll()
{
    Interlocked.Increment(ref _globalVersion);
    _stats.InvalidationCount++;
}

// Integration with World operations:
public class TrackedWorld
{
    private readonly World _world;
    private readonly QueryResultCache _cache;

    public Entity Create<T1>(T1 component) where T1 : struct
    {
        var entity = _world.Create(component);
        _cache.InvalidateAll(); // Structural change
        return entity;
    }

    public void Destroy(Entity entity)
    {
        _world.Destroy(entity);
        _cache.InvalidateAll(); // Structural change
    }
}
```

### 4.4 Component-Based Invalidation (Advanced)

**Approach**: Track which component types were modified and invalidate only affected queries.

```csharp
private readonly ConcurrentBag<Type> _modifiedComponentTypes = new();

public void InvalidateComponentType<T>() where T : struct
{
    _modifiedComponentTypes.Add(typeof(T));
    Interlocked.Increment(ref _globalVersion);
    _stats.InvalidationCount++;
}

public bool IsQueryValid(CachedQueryResult result)
{
    if (result.Version < _globalVersion)
    {
        // Check if any modified types overlap with query dependencies
        var hasOverlap = result.DependentTypes
            .Any(t => _modifiedComponentTypes.Contains(t));

        return !hasOverlap;
    }

    return true;
}

// Clear modified types at frame end
public void ClearFrameState()
{
    _modifiedComponentTypes.Clear();
}
```

### 4.5 Per-Frame Invalidation (Fastest)

**Approach**: Cache valid for entire frame, cleared at frame start.

```csharp
public void InvalidateFrame()
{
    // Simple version increment, no component tracking needed
    Interlocked.Increment(ref _globalVersion);
    _stats.InvalidationCount++;
}

// Called by game loop at frame start:
public void Update(GameTime gameTime)
{
    _cache.InvalidateFrame(); // Clear last frame's cache

    // Systems execute with fresh cache...
}
```

### 4.6 Invalidation Trade-offs

| Mode | Safety | Performance | Use Case |
|------|--------|-------------|----------|
| **Global** | ✅ Safe | ⚠️ Conservative | Default, handles all cases |
| **ComponentBased** | ✅ Safe | ✅ Optimal | Many queries, few changes |
| **PerFrame** | ⚠️ Assumes no mid-frame changes | ✅✅ Fastest | Stable entities during frame |
| **Manual** | ❌ User must track | ✅✅ Fastest | Advanced users only |

---

## 5. ParallelQueryExecutor Integration

### 5.1 Enhanced ExecuteParallel API

```csharp
/// <summary>
/// Execute query with optional result caching.
/// </summary>
public void ExecuteParallel<T>(
    in QueryDescription query,
    EntityAction<T> action,
    bool useCache = false  // NEW: opt-in caching
) where T : struct
{
    ArgumentNullException.ThrowIfNull(action);

    if (!useCache || _resultCache == null)
    {
        // Original non-cached path
        ExecuteParallelUncached(query, action);
        return;
    }

    // Try to get cached results
    var key = new QueryCacheKey(query, genericArity: 1);

    if (_resultCache.TryGet(key, out var cached, out var entityArray, out var count))
    {
        // CACHE HIT: Use cached entities
        ExecuteParallelCached<T>(entityArray, count, action);
        return;
    }

    // CACHE MISS: Execute query and cache results
    ExecuteParallelAndCache<T>(key, query, action);
}

private void ExecuteParallelCached<T>(Entity[] entities, int count, EntityAction<T> action)
    where T : struct
{
    var stopwatch = Stopwatch.StartNew();

    var options = new ParallelOptions { MaxDegreeOfParallelism = _maxDegreeOfParallelism };
    System.Threading.Tasks.Parallel.For(0, count, options, i =>
    {
        ref var component = ref entities[i].Get<T>();
        action(entities[i], ref component);
    });

    stopwatch.Stop();
    RecordExecution(count, stopwatch.Elapsed.TotalMilliseconds, cacheHit: true);
}

private void ExecuteParallelAndCache<T>(
    QueryCacheKey key,
    in QueryDescription query,
    EntityAction<T> action) where T : struct
{
    var stopwatch = Stopwatch.StartNew();

    // Execute query normally
    var entityCount = _world.CountEntities(in query);
    if (entityCount == 0)
    {
        stopwatch.Stop();
        RecordExecution(0, stopwatch.Elapsed.TotalMilliseconds, cacheHit: false);
        return;
    }

    // Rent array and collect entities
    var entityArray = ArrayPool<Entity>.Shared.Rent(entityCount);
    var index = 0;
    _world.Query(in query, (Entity entity) => entityArray[index++] = entity);

    // Process in parallel
    var options = new ParallelOptions { MaxDegreeOfParallelism = _maxDegreeOfParallelism };
    System.Threading.Tasks.Parallel.For(0, entityCount, options, i =>
    {
        ref var component = ref entityArray[i].Get<T>();
        action(entityArray[i], ref component);
    });

    stopwatch.Stop();

    // Cache the results (QueryResultCache now owns the array)
    var dependentTypes = new[] { typeof(T) };
    _resultCache.Store(key, entityArray, entityCount, dependentTypes);

    RecordExecution(entityCount, stopwatch.Elapsed.TotalMilliseconds, cacheHit: false);
}
```

### 5.2 Cache Lifecycle Management

```csharp
public class ParallelQueryExecutor
{
    private readonly QueryResultCache? _resultCache;

    public ParallelQueryExecutor(
        World world,
        int? maxThreads = null,
        QueryResultCacheConfig? cacheConfig = null)
    {
        _world = world;
        _maxDegreeOfParallelism = maxThreads ?? Environment.ProcessorCount;
        _stats = new ParallelExecutionStats();

        // Cache is opt-in via config
        if (cacheConfig?.Enabled == true)
        {
            _resultCache = new QueryResultCache(cacheConfig);
        }
    }

    /// <summary>
    /// Invalidate cached query results.
    /// Call this when entities/components are added/removed.
    /// </summary>
    public void InvalidateCache()
    {
        _resultCache?.InvalidateAll();
    }

    /// <summary>
    /// Invalidate cache for specific component type.
    /// </summary>
    public void InvalidateCache<T>() where T : struct
    {
        _resultCache?.InvalidateComponentType<T>();
    }

    /// <summary>
    /// Clear cache at frame boundaries (for PerFrame mode).
    /// </summary>
    public void BeginFrame()
    {
        _resultCache?.InvalidateFrame();
    }
}
```

---

## 6. Configuration & Statistics

### 6.1 Configuration

```csharp
/// <summary>
/// Configuration for QueryResultCache behavior.
/// </summary>
public sealed class QueryResultCacheConfig
{
    /// <summary>
    /// Enable result caching. Default: false (opt-in).
    /// </summary>
    public bool Enabled { get; set; } = false;

    /// <summary>
    /// Maximum number of cached query results.
    /// Default: 100 queries.
    /// </summary>
    public int MaxCachedQueries { get; set; } = 100;

    /// <summary>
    /// Maximum memory for cached entities (approximate).
    /// Default: 10 MB.
    /// </summary>
    public long MaxMemoryBytes { get; set; } = 10 * 1024 * 1024;

    /// <summary>
    /// Invalidation strategy.
    /// Default: Global (safest).
    /// </summary>
    public InvalidationMode InvalidationMode { get; set; } = InvalidationMode.Global;

    /// <summary>
    /// Minimum entity count to cache.
    /// Queries with fewer entities are not cached.
    /// Default: 10 entities.
    /// </summary>
    public int MinEntitiesToCache { get; set; } = 10;

    /// <summary>
    /// Enable detailed statistics tracking.
    /// Default: true.
    /// </summary>
    public bool EnableStatistics { get; set; } = true;
}
```

### 6.2 Statistics

```csharp
/// <summary>
/// Performance statistics for QueryResultCache.
/// </summary>
public sealed class CacheStatistics
{
    public long HitCount { get; set; }
    public long MissCount { get; set; }
    public long InvalidationCount { get; set; }
    public long EvictionCount { get; set; }

    /// <summary>
    /// Cache hit rate (0.0 to 1.0).
    /// </summary>
    public double HitRate =>
        HitCount + MissCount > 0
            ? (double)HitCount / (HitCount + MissCount)
            : 0.0;

    /// <summary>
    /// Total memory used by cached results (bytes).
    /// </summary>
    public long MemoryBytes { get; set; }

    /// <summary>
    /// Number of cached queries currently stored.
    /// </summary>
    public int CachedQueryCount { get; set; }

    /// <summary>
    /// Average entities per cached query.
    /// </summary>
    public double AvgEntitiesPerQuery =>
        CachedQueryCount > 0
            ? (double)MemoryBytes / (CachedQueryCount * IntPtr.Size)
            : 0.0;

    public void Reset()
    {
        HitCount = 0;
        MissCount = 0;
        InvalidationCount = 0;
        EvictionCount = 0;
    }

    public CacheStatistics Clone()
    {
        return new CacheStatistics
        {
            HitCount = HitCount,
            MissCount = MissCount,
            InvalidationCount = InvalidationCount,
            EvictionCount = EvictionCount,
            MemoryBytes = MemoryBytes,
            CachedQueryCount = CachedQueryCount
        };
    }
}
```

---

## 7. Performance Analysis

### 7.1 When Caching is Beneficial

**Cache is Worth It When**:
1. ✅ **Same query executed multiple times** (e.g., every frame)
2. ✅ **Large entity counts** (1000+ entities)
3. ✅ **Stable entity populations** (few adds/removes)
4. ✅ **Expensive archetype traversals** (complex queries with WithNone/WithAny)

**Cache is NOT Worth It When**:
1. ❌ **Query executed once** (no reuse benefit)
2. ❌ **Small entity counts** (<10 entities)
3. ❌ **Frequent structural changes** (constant invalidation)
4. ❌ **Simple queries** (single component, fast archetype lookup)

### 7.2 Performance Scenarios

#### Scenario 1: Render System (High Benefit)

```csharp
// Query: Position + Sprite (executed every frame, 5000+ entities)
var query = QueryCache.Get<Position, Sprite>();

// Frame 1: Cache miss, populate cache
executor.ExecuteParallel(query, RenderEntity, useCache: true);
// Time: 5ms (query archetype + parallel processing)

// Frame 2-60: Cache hit
executor.ExecuteParallel(query, RenderEntity, useCache: true);
// Time: 2ms (skip archetype, use cached entities)

// Benefit: 3ms saved per frame = 180ms/second = 36% speedup
```

#### Scenario 2: Physics System (Moderate Benefit)

```csharp
// Query: Position + Velocity + Collision (1000 entities, stable)
var query = QueryCache.Get<Position, Velocity, Collision>();

// No entities added/removed during gameplay
executor.ExecuteParallel(query, UpdatePhysics, useCache: true);

// Benefit: 1-2ms saved per frame (moderate entity count)
```

#### Scenario 3: Dynamic Spawner (No Benefit)

```csharp
// Query: Enemy + Health (entities constantly spawning/dying)
var query = QueryCache.Get<Enemy, Health>();

// Every frame: entities added/removed -> cache invalidated
executor.ExecuteParallel(query, UpdateEnemies, useCache: true);

// Benefit: 0ms (cache always invalidated, overhead of cache logic)
// RECOMMENDATION: Don't use cache here!
```

### 7.3 Memory vs Speed Trade-off

| Entity Count | Array Size | Memory Cost | Speedup | Recommendation |
|--------------|------------|-------------|---------|----------------|
| 10 | 80 bytes | Negligible | 0% | ❌ Don't cache |
| 100 | 800 bytes | ~1 KB | 10-20% | ⚠️ Marginal |
| 1,000 | 8,000 bytes | ~8 KB | 30-40% | ✅ Cache |
| 10,000 | 80,000 bytes | ~78 KB | 40-50% | ✅ Cache |
| 100,000 | 800,000 bytes | ~780 KB | 50-60% | ✅ Cache (if stable) |

**Rule of Thumb**: Cache if entity count > 100 AND query executed > 5 times.

### 7.4 Benchmark Expectations

```csharp
// Benchmark: Query 10,000 entities with cache enabled
// Assumptions:
// - Cache hit rate: 90%
// - Archetype traversal: 2ms
// - Parallel processing: 3ms

// Without cache:
// Time per query: 2ms (archetype) + 3ms (parallel) = 5ms

// With cache (90% hit rate):
// Cache hit:  0ms (archetype) + 3ms (parallel) = 3ms
// Cache miss: 2ms (archetype) + 3ms (parallel) = 5ms
// Average: (0.9 * 3ms) + (0.1 * 5ms) = 3.2ms

// Speedup: (5ms - 3.2ms) / 5ms = 36% faster
```

---

## 8. API Specification

### 8.1 Core Cache Operations

```csharp
public sealed class QueryResultCache
{
    /// <summary>
    /// Try to get cached query results.
    /// </summary>
    public bool TryGet(
        QueryCacheKey key,
        out CachedQueryResult? result,
        out Entity[] entities,
        out int count)
    {
        if (_cache.TryGetValue(key, out result))
        {
            if (result.IsValid(_globalVersion))
            {
                entities = ArrayPool<Entity>.Shared.Rent(result.Count);
                result.CopyTo(entities);
                count = result.Count;

                _stats.HitCount++;
                return true;
            }

            // Cached result is stale, remove it
            _cache.TryRemove(key, out _);
            result.Dispose();
        }

        _stats.MissCount++;
        entities = null!;
        count = 0;
        result = null;
        return false;
    }

    /// <summary>
    /// Store query results in cache.
    /// Takes ownership of entities array.
    /// </summary>
    public void Store(
        QueryCacheKey key,
        Entity[] entities,
        int count,
        Type[] dependentTypes)
    {
        if (count < _config.MinEntitiesToCache)
        {
            // Don't cache small queries
            ArrayPool<Entity>.Shared.Return(entities);
            return;
        }

        var cached = new CachedQueryResult(
            entities,
            count,
            _globalVersion,
            dependentTypes
        );

        _cache.AddOrUpdate(key, cached, (k, old) =>
        {
            old.Dispose(); // Return old array to pool
            return cached;
        });

        _stats.CachedQueryCount = _cache.Count;
        _stats.MemoryBytes += count * IntPtr.Size;

        EnforceMemoryLimit();
    }

    /// <summary>
    /// Invalidate all cached results.
    /// </summary>
    public void InvalidateAll()
    {
        Interlocked.Increment(ref _globalVersion);
        _stats.InvalidationCount++;
    }

    /// <summary>
    /// Invalidate queries that depend on a specific component type.
    /// </summary>
    public void InvalidateComponentType<T>() where T : struct
    {
        var targetType = typeof(T);

        foreach (var kvp in _cache)
        {
            if (kvp.Value.DependentTypes.Contains(targetType))
            {
                if (_cache.TryRemove(kvp.Key, out var result))
                {
                    result.Dispose();
                    _stats.InvalidationCount++;
                }
            }
        }
    }

    /// <summary>
    /// Clear all cached results and reset state.
    /// </summary>
    public void Clear()
    {
        foreach (var result in _cache.Values)
        {
            result.Dispose();
        }

        _cache.Clear();
        _stats.CachedQueryCount = 0;
        _stats.MemoryBytes = 0;
    }

    /// <summary>
    /// Get cache statistics.
    /// </summary>
    public CacheStatistics GetStatistics()
    {
        _stats.CachedQueryCount = _cache.Count;
        return _stats.Clone();
    }
}
```

### 8.2 Usage Examples

#### Example 1: Basic Caching

```csharp
// Setup
var config = new QueryResultCacheConfig
{
    Enabled = true,
    InvalidationMode = InvalidationMode.Global
};

var executor = new ParallelQueryExecutor(world, cacheConfig: config);

// Usage
var query = QueryCache.Get<Position, Velocity>();

// First call: cache miss
executor.ExecuteParallel(query, UpdateMovement, useCache: true);

// Second call: cache hit (if no structural changes)
executor.ExecuteParallel(query, UpdateMovement, useCache: true);
```

#### Example 2: Per-Frame Caching

```csharp
// Game loop
public void Update(GameTime gameTime)
{
    // Invalidate cache at frame start
    _executor.BeginFrame();

    // These queries will cache results for the frame
    _movementSystem.Update(gameTime); // uses cache
    _renderSystem.Update(gameTime);   // uses cache
    _physicsSystem.Update(gameTime);  // uses cache

    // Next frame: cache invalidated automatically
}
```

#### Example 3: Component-Based Invalidation

```csharp
var config = new QueryResultCacheConfig
{
    Enabled = true,
    InvalidationMode = InvalidationMode.ComponentBased
};

var executor = new ParallelQueryExecutor(world, cacheConfig: config);

// Add enemy entity
var enemy = world.Create<Position, Health>();
executor.InvalidateCache<Position>(); // Only invalidates Position queries
executor.InvalidateCache<Health>();   // Only invalidates Health queries

// Render query not affected (uses Sprite, not Position/Health)
executor.ExecuteParallel(renderQuery, Render, useCache: true); // Still uses cache!
```

#### Example 4: Manual Cache Control

```csharp
// Disable auto-invalidation for maximum control
var config = new QueryResultCacheConfig
{
    Enabled = true,
    InvalidationMode = InvalidationMode.Manual
};

// You must manually invalidate when needed
executor.ExecuteParallel(query, Process, useCache: true);

// After spawning enemies:
SpawnEnemies();
executor.InvalidateCache(); // YOU must call this!

executor.ExecuteParallel(query, Process, useCache: true);
```

---

## 9. Testing Strategy

### 9.1 Unit Tests

```csharp
[Fact]
public void CacheHit_ReturnsSameEntities()
{
    // Arrange
    var config = new QueryResultCacheConfig { Enabled = true };
    var executor = new ParallelQueryExecutor(world, cacheConfig: config);
    var query = QueryCache.Get<Position>();

    var entities = CreateTestEntities(100);

    // Act
    var results1 = new List<Entity>();
    executor.ExecuteParallel<Position>(query, (e, ref p) => results1.Add(e), useCache: true);

    var results2 = new List<Entity>();
    executor.ExecuteParallel<Position>(query, (e, ref p) => results2.Add(e), useCache: true);

    // Assert
    Assert.Equal(results1, results2); // Same entities

    var stats = executor.GetCacheStatistics();
    Assert.Equal(1, stats.HitCount);
    Assert.Equal(1, stats.MissCount);
}

[Fact]
public void Invalidation_ClearsCacheCorrectly()
{
    // Arrange
    var executor = CreateExecutor();
    var query = QueryCache.Get<Position>();

    // Prime cache
    executor.ExecuteParallel<Position>(query, NoOp, useCache: true);

    // Act: Add entity (structural change)
    world.Create(new Position());
    executor.InvalidateCache();

    // Assert: Next query is cache miss
    executor.ExecuteParallel<Position>(query, NoOp, useCache: true);

    var stats = executor.GetCacheStatistics();
    Assert.Equal(0, stats.HitCount); // Both were misses
    Assert.Equal(2, stats.MissCount);
}

[Fact]
public void ComponentBasedInvalidation_OnlyAffectsRelevantQueries()
{
    // Arrange
    var config = new QueryResultCacheConfig
    {
        Enabled = true,
        InvalidationMode = InvalidationMode.ComponentBased
    };
    var executor = new ParallelQueryExecutor(world, cacheConfig: config);

    var posQuery = QueryCache.Get<Position>();
    var velQuery = QueryCache.Get<Velocity>();

    // Prime both caches
    executor.ExecuteParallel<Position>(posQuery, NoOp, useCache: true);
    executor.ExecuteParallel<Velocity>(velQuery, NoOp, useCache: true);

    // Act: Invalidate only Position
    executor.InvalidateCache<Position>();

    // Assert
    executor.ExecuteParallel<Position>(posQuery, NoOp, useCache: true);
    executor.ExecuteParallel<Velocity>(velQuery, NoOp, useCache: true);

    var stats = executor.GetCacheStatistics();
    Assert.Equal(1, stats.HitCount);  // Velocity cache hit
    Assert.Equal(3, stats.MissCount); // 2 initial + 1 Position miss
}

[Fact]
public void LRUEviction_RemovesOldestEntries()
{
    // Arrange
    var config = new QueryResultCacheConfig
    {
        Enabled = true,
        MaxCachedQueries = 2
    };
    var executor = new ParallelQueryExecutor(world, cacheConfig: config);

    var query1 = CreateQuery<Position>();
    var query2 = CreateQuery<Velocity>();
    var query3 = CreateQuery<Health>();

    // Act: Cache 3 queries (limit is 2)
    executor.ExecuteParallel(query1, NoOp, useCache: true);
    executor.ExecuteParallel(query2, NoOp, useCache: true);
    executor.ExecuteParallel(query3, NoOp, useCache: true); // Evicts query1

    // Assert
    executor.ExecuteParallel(query1, NoOp, useCache: true); // Cache miss
    executor.ExecuteParallel(query2, NoOp, useCache: true); // Cache hit

    var stats = executor.GetCacheStatistics();
    Assert.Equal(1, stats.HitCount);
    Assert.Equal(1, stats.EvictionCount);
}
```

### 9.2 Performance Tests

```csharp
[Fact]
public void Benchmark_CacheSpeedup()
{
    // Arrange
    var entityCount = 10_000;
    CreateTestEntities(entityCount);

    var executor = new ParallelQueryExecutor(world);
    var query = QueryCache.Get<Position, Velocity>();

    // Benchmark: Without cache
    var sw = Stopwatch.StartNew();
    for (int i = 0; i < 100; i++)
    {
        executor.ExecuteParallel(query, UpdateMovement, useCache: false);
    }
    sw.Stop();
    var timeWithoutCache = sw.ElapsedMilliseconds;

    // Benchmark: With cache
    var executorCached = new ParallelQueryExecutor(
        world,
        cacheConfig: new() { Enabled = true, InvalidationMode = InvalidationMode.PerFrame }
    );

    sw.Restart();
    for (int i = 0; i < 100; i++)
    {
        executorCached.ExecuteParallel(query, UpdateMovement, useCache: true);
    }
    sw.Stop();
    var timeWithCache = sw.ElapsedMilliseconds;

    // Assert: Cache should be at least 20% faster
    var speedup = (double)(timeWithoutCache - timeWithCache) / timeWithoutCache;
    Assert.True(speedup > 0.2, $"Speedup was only {speedup:P2}");

    _output.WriteLine($"Without cache: {timeWithoutCache}ms");
    _output.WriteLine($"With cache: {timeWithCache}ms");
    _output.WriteLine($"Speedup: {speedup:P2}");
}
```

### 9.3 Thread-Safety Tests

```csharp
[Fact]
public async Task ConcurrentAccess_NoDataCorruption()
{
    // Arrange
    var executor = CreateExecutor();
    var query = QueryCache.Get<Position>();
    CreateTestEntities(1000);

    // Act: 10 threads query simultaneously
    var tasks = Enumerable.Range(0, 10)
        .Select(_ => Task.Run(() =>
        {
            for (int i = 0; i < 100; i++)
            {
                var results = new List<Entity>();
                executor.ExecuteParallel<Position>(
                    query,
                    (e, ref p) => results.Add(e),
                    useCache: true
                );
            }
        }))
        .ToArray();

    await Task.WhenAll(tasks);

    // Assert: No exceptions, consistent statistics
    var stats = executor.GetCacheStatistics();
    Assert.True(stats.HitCount + stats.MissCount == 1000);
}
```

---

## 10. Migration Path

### Phase 1: Implementation (Week 1)
1. Implement `QueryCacheKey` with hash computation
2. Implement `CachedQueryResult` with ArrayPool management
3. Implement `QueryResultCache` with Global invalidation
4. Add optional `useCache` parameter to `ExecuteParallel` methods

### Phase 2: Integration (Week 1)
5. Add cache statistics to `ParallelExecutionStats`
6. Integrate with `ParallelSystemManager`
7. Add configuration to `GameConfig`
8. Write comprehensive unit tests

### Phase 3: Optimization (Week 2)
9. Implement Component-Based invalidation
10. Implement Per-Frame invalidation
11. Add LRU eviction with memory limits
12. Performance benchmarking

### Phase 4: Documentation (Week 2)
13. API documentation
14. Usage examples
15. Performance guide (when to cache)
16. Migration guide for existing systems

---

## 11. Risks & Mitigation

### Risk 1: Stale Cache Data
**Symptom**: Entities rendered at wrong positions, components not updated.
**Cause**: Forgot to invalidate cache after structural change.
**Mitigation**:
- Default to `Global` invalidation mode (conservative)
- Add runtime validation in debug builds
- Log warnings when cache hit rate is suspiciously high (>99%)

### Risk 2: Memory Overhead
**Symptom**: OutOfMemoryException with large entity counts.
**Cause**: Caching too many large queries.
**Mitigation**:
- Enforce `MaxMemoryBytes` limit
- LRU eviction when limit reached
- Don't cache queries with < 10 entities
- Return arrays to ArrayPool immediately when evicted

### Risk 3: Thread Contention
**Symptom**: Slowdown with many parallel systems.
**Cause**: Lock contention in `CachedQueryResult`.
**Mitigation**:
- Use `ReaderWriterLockSlim` (multiple readers OK)
- Use `ConcurrentDictionary` for main cache
- Minimize lock duration (only during Copy)

### Risk 4: False Cache Hits
**Symptom**: Wrong entities returned for a query.
**Cause**: Hash collision in `QueryCacheKey`.
**Mitigation**:
- Use full structural equality in `Equals()`
- Don't rely solely on hash code
- Add debug assertions comparing entity counts

---

## 12. Future Enhancements

### 12.1 Archetype-Aware Caching
Cache at the archetype level instead of query level for better granularity.

### 12.2 Async Invalidation
Use background thread to rebuild cache asynchronously after invalidation.

### 12.3 Partial Updates
Instead of invalidating entire cache, update only affected entities.

### 12.4 Predictive Pre-caching
Analyze query patterns and pre-cache frequently used queries.

### 12.5 Cache Warming
Allow systems to hint which queries will be used next frame for pre-population.

---

## 13. Summary

**QueryResultCache** provides a **thread-safe, opt-in caching layer** for ECS query results in PokeSharp's `ParallelQueryExecutor`. The design prioritizes:

1. **Safety**: Conservative invalidation by default
2. **Performance**: 30-50% speedup for stable, large queries
3. **Configurability**: Multiple invalidation modes for different use cases
4. **Observability**: Detailed statistics for hit rate, memory usage

**Key Features**:
- ✅ Thread-safe concurrent access
- ✅ Multiple invalidation strategies (Global, Component-Based, Per-Frame)
- ✅ LRU eviction with memory limits
- ✅ ArrayPool integration for zero-allocation storage
- ✅ Comprehensive statistics tracking
- ✅ Opt-in per query

**When to Use**:
- ✅ Queries executed frequently (every frame)
- ✅ Large entity counts (1000+)
- ✅ Stable entity populations
- ❌ Don't use for dynamic spawning/destruction
- ❌ Don't use for small queries (<10 entities)

**Next Steps**:
1. Review design document
2. Implement `QueryCacheKey` and `CachedQueryResult`
3. Integrate with `ParallelQueryExecutor`
4. Write tests for correctness and performance
5. Benchmark against realistic workloads

---

**Document Version**: 1.0
**Last Updated**: 2025-11-09
**Status**: Ready for Implementation
