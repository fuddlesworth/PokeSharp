# QueryResultCache - Architecture Summary

**Quick Reference for Implementation**

---

## Core Design Decisions

### 1. Cache Key Strategy
**Decision**: Multi-factor immutable struct with pre-computed hash
- Component type hashes (sorted for consistency)
- Query filter flags (WithAll/WithNone/WithAny)
- Generic method arity (1-4 components)
- Structural equality for correctness

**Rationale**: Fast dictionary lookup (O(1)) while ensuring correctness.

---

### 2. Invalidation Strategy
**Decision**: Multiple modes with Global as default

| Mode | Safety | Speed | Use Case |
|------|--------|-------|----------|
| **Global** (default) | ✅ Safest | ⚠️ Conservative | General purpose |
| **ComponentBased** | ✅ Safe | ✅ Fast | Selective invalidation |
| **PerFrame** | ⚠️ Assumes stable | ✅✅ Fastest | Stable entities |
| **Manual** | ❌ User risk | ✅✅ Fastest | Advanced users |

**Rationale**: Safety first, optimize later. Global mode prevents all stale data bugs.

---

### 3. Cache Storage
**Decision**: `ConcurrentDictionary<QueryCacheKey, CachedQueryResult>`

**CachedQueryResult contains**:
- `Entity[]` from ArrayPool (zero-allocation)
- `int Count` (actual entities, array may be larger)
- `long Version` (for invalidation tracking)
- `Type[] DependentTypes` (for component-based invalidation)
- `ReaderWriterLockSlim` (thread-safe access)

**Rationale**: Thread-safe, memory-efficient, integrates with existing ArrayPool usage.

---

### 4. Memory Management
**Decision**: LRU eviction with configurable limits

**Limits**:
- `MaxCachedQueries` (default: 100 queries)
- `MaxMemoryBytes` (default: 10 MB)
- `MinEntitiesToCache` (default: 10 entities)

**Rationale**: Prevent unbounded memory growth while caching large queries.

---

### 5. Thread Safety
**Decision**: Lock-free reads, synchronized writes

**Approach**:
- `ConcurrentDictionary` for main cache (lock-free reads)
- `ReaderWriterLockSlim` per cached result (multiple readers OK)
- `Interlocked` operations for version counter

**Rationale**: Maximize parallel query throughput while ensuring correctness.

---

### 6. API Design
**Decision**: Opt-in caching via `useCache` parameter

```csharp
// Opt-in per query call
executor.ExecuteParallel(query, action, useCache: true);

// Configure at executor level
var config = new QueryResultCacheConfig { Enabled = true };
var executor = new ParallelQueryExecutor(world, cacheConfig: config);
```

**Rationale**: Explicit control, no breaking changes to existing code.

---

## Performance Characteristics

### When to Cache
✅ **CACHE**:
- Query executed >5 times
- Entity count >100
- Stable entity population
- Complex queries (WithNone/WithAny)

❌ **DON'T CACHE**:
- Single-use queries
- Small entity counts (<10)
- Frequent structural changes
- Simple single-component queries

### Expected Speedup
| Entity Count | Speedup | Memory Cost |
|--------------|---------|-------------|
| 10 | 0% | ~80 bytes |
| 100 | 10-20% | ~800 bytes |
| 1,000 | 30-40% | ~8 KB |
| 10,000 | 40-50% | ~78 KB |

---

## Critical Safety Mechanisms

### 1. Version Tracking
Every cache entry has a version number. Global version increments on structural changes.
```csharp
public void InvalidateAll()
{
    Interlocked.Increment(ref _globalVersion);
}

public bool IsValid(long currentVersion)
{
    return Version >= currentVersion;
}
```

### 2. Component-Based Invalidation
Track which components were modified, only invalidate affected queries.
```csharp
// Only invalidate queries that use Position
executor.InvalidateCache<Position>();

// Queries with Velocity, Health, etc. still use cache
```

### 3. Automatic Eviction
When memory limit reached, evict least recently used entries.
```csharp
var toEvict = _cache
    .OrderBy(kvp => kvp.Value.AccessTimestamp)
    .Take(exceededCount)
    .Select(kvp => kvp.Key);
```

---

## Integration Points

### 1. ParallelQueryExecutor
Add optional caching to existing methods:
```csharp
public void ExecuteParallel<T>(
    in QueryDescription query,
    EntityAction<T> action,
    bool useCache = false)  // NEW
{
    if (useCache && _resultCache != null)
    {
        // Try cache first
        if (_resultCache.TryGet(key, out var cached))
        {
            ExecuteParallelCached(cached);
            return;
        }
    }

    // Original uncached path
    ExecuteParallelUncached(query, action);
}
```

### 2. Game Loop
Invalidate at frame boundaries:
```csharp
public void Update(GameTime gameTime)
{
    // Clear per-frame cache
    _executor.BeginFrame();

    // Systems execute with fresh cache
    _systemManager.Update(gameTime);
}
```

### 3. Entity Operations
Hook into structural changes:
```csharp
public class TrackedWorld
{
    public Entity Create<T>(T component) where T : struct
    {
        var entity = _world.Create(component);
        _executor.InvalidateCache(); // Structural change
        return entity;
    }
}
```

---

## Implementation Checklist

### Phase 1: Core (Week 1)
- [ ] Implement `QueryCacheKey` with hash function
- [ ] Implement `CachedQueryResult` with ArrayPool
- [ ] Implement `QueryResultCache` with Global invalidation
- [ ] Add `useCache` parameter to ExecuteParallel methods
- [ ] Write unit tests for correctness

### Phase 2: Integration (Week 1)
- [ ] Add cache statistics to ParallelExecutionStats
- [ ] Integrate with ParallelSystemManager
- [ ] Add configuration to GameConfig
- [ ] Write thread-safety tests

### Phase 3: Optimization (Week 2)
- [ ] Implement Component-Based invalidation
- [ ] Implement Per-Frame invalidation
- [ ] Add LRU eviction with memory limits
- [ ] Performance benchmarking

### Phase 4: Documentation (Week 2)
- [ ] API documentation (XML comments)
- [ ] Usage examples (code samples)
- [ ] Performance guide (when to cache)
- [ ] Migration guide

---

## Example Usage

### Basic Caching
```csharp
var config = new QueryResultCacheConfig
{
    Enabled = true,
    InvalidationMode = InvalidationMode.Global
};

var executor = new ParallelQueryExecutor(world, cacheConfig: config);

var query = QueryCache.Get<Position, Velocity>();

// First call: cache miss
executor.ExecuteParallel(query, UpdateMovement, useCache: true);

// Second call: cache hit (if no structural changes)
executor.ExecuteParallel(query, UpdateMovement, useCache: true);
```

### Per-Frame Caching
```csharp
public void Update(GameTime gameTime)
{
    _executor.BeginFrame(); // Invalidate last frame's cache

    // These queries cache for the frame
    _movementSystem.Update(gameTime);
    _renderSystem.Update(gameTime);
    _physicsSystem.Update(gameTime);
}
```

### Component-Based Invalidation
```csharp
var config = new QueryResultCacheConfig
{
    InvalidationMode = InvalidationMode.ComponentBased
};

// Add enemy
world.Create<Position, Health>();
executor.InvalidateCache<Position>(); // Only affects Position queries
executor.InvalidateCache<Health>();   // Only affects Health queries

// Render query unaffected (uses Sprite, not Position/Health)
executor.ExecuteParallel(renderQuery, Render, useCache: true);
```

---

## Risk Mitigation

### Stale Data
**Risk**: Cache not invalidated after structural change
**Mitigation**: Default to Global mode, add debug validation

### Memory Overhead
**Risk**: OutOfMemoryException with large caches
**Mitigation**: Enforce limits, LRU eviction, don't cache small queries

### Thread Contention
**Risk**: Slowdown from lock contention
**Mitigation**: ReaderWriterLockSlim, ConcurrentDictionary, minimize lock duration

### Hash Collisions
**Risk**: Wrong entities returned due to hash collision
**Mitigation**: Full structural equality in Equals(), debug assertions

---

## Testing Strategy

### Unit Tests
- ✅ Cache hit returns same entities
- ✅ Invalidation clears cache correctly
- ✅ Component-based invalidation selective
- ✅ LRU eviction removes oldest entries
- ✅ Thread-safe concurrent access

### Performance Tests
- ✅ Benchmark cache speedup (>20%)
- ✅ Memory usage within limits
- ✅ Hit rate tracking accuracy
- ✅ Eviction performance

### Integration Tests
- ✅ Works with ParallelSystemManager
- ✅ Per-frame invalidation in game loop
- ✅ Statistics collection accuracy
- ✅ No breaking changes to existing code

---

## Statistics & Monitoring

```csharp
public sealed class CacheStatistics
{
    public long HitCount { get; set; }
    public long MissCount { get; set; }
    public long InvalidationCount { get; set; }
    public long EvictionCount { get; set; }
    public double HitRate { get; } // 0.0 to 1.0
    public long MemoryBytes { get; set; }
    public int CachedQueryCount { get; set; }
}

// Usage
var stats = executor.GetCacheStatistics();
Console.WriteLine($"Hit rate: {stats.HitRate:P2}");
Console.WriteLine($"Memory: {stats.MemoryBytes / 1024} KB");
```

---

## Configuration

```csharp
public sealed class QueryResultCacheConfig
{
    // Enable caching (opt-in)
    public bool Enabled { get; set; } = false;

    // Max cached queries
    public int MaxCachedQueries { get; set; } = 100;

    // Max memory usage
    public long MaxMemoryBytes { get; set; } = 10 * 1024 * 1024; // 10 MB

    // Invalidation strategy
    public InvalidationMode InvalidationMode { get; set; } = InvalidationMode.Global;

    // Min entities to cache (skip small queries)
    public int MinEntitiesToCache { get; set; } = 10;

    // Statistics tracking
    public bool EnableStatistics { get; set; } = true;
}
```

---

## Next Steps

1. **Review** this summary and full design document
2. **Implement** QueryCacheKey and CachedQueryResult
3. **Integrate** with ParallelQueryExecutor
4. **Test** correctness and performance
5. **Benchmark** against realistic workloads
6. **Document** API usage and best practices

---

**Document Version**: 1.0
**Last Updated**: 2025-11-09
**Full Design**: See PHASE-4D-QUERYRESULTCACHE-DESIGN.md
