# Phase 4D: QueryResultCache - Implementation Guide

**Quick-start guide for implementing the QueryResultCache system**

---

## Implementation Checklist

### Week 1: Core Implementation

#### Day 1: Cache Key & Result Storage
- [ ] Create `QueryCacheKey` struct in `/PokeSharp.Core/Parallel/QueryCacheKey.cs`
  - Implement component type extraction
  - Implement hash code computation
  - Implement structural equality (IEquatable<T>)
  - Unit tests for hash collisions

- [ ] Create `CachedQueryResult` class in `/PokeSharp.Core/Parallel/CachedQueryResult.cs`
  - Entity array storage with ArrayPool
  - Version tracking
  - ReaderWriterLockSlim for thread-safety
  - Dispose pattern for cleanup

**Time Estimate:** 4-6 hours

#### Day 2: Cache Container
- [ ] Create `QueryResultCache` class in `/PokeSharp.Core/Parallel/QueryResultCache.cs`
  - ConcurrentDictionary storage
  - TryGet method with version checking
  - Store method with ownership transfer
  - InvalidateAll method with Interlocked.Increment

- [ ] Create `QueryResultCacheConfig` in same file
  - All configuration properties
  - Default values
  - Validation logic

**Time Estimate:** 4-6 hours

#### Day 3: Invalidation System
- [ ] Implement Global invalidation
  - Version counter increments
  - IsValid checks

- [ ] Implement Component-Based invalidation
  - ModifiedComponents tracking
  - Selective invalidation logic

- [ ] Implement Per-Frame invalidation
  - BeginFrame method
  - Frame boundary handling

**Time Estimate:** 4-6 hours

#### Day 4-5: ParallelQueryExecutor Integration
- [ ] Add optional `useCache` parameter to ExecuteParallel methods
  - ExecuteParallel<T>
  - ExecuteParallel<T1, T2>
  - ExecuteParallel<T1, T2, T3>
  - ExecuteParallel<T1, T2, T3, T4>
  - ExecuteParallelWithReduce<T, TResult>

- [ ] Implement cache lookup logic
  - Create QueryCacheKey
  - Try cache first
  - Fall back to original query

- [ ] Implement cache storage after query
  - Store only if count >= MinEntitiesToCache
  - Transfer array ownership to cache

**Time Estimate:** 6-8 hours

### Week 2: Testing & Optimization

#### Day 6: Unit Tests
- [ ] Test QueryCacheKey equality and hashing
  - Same components → same key
  - Different order → same key (sorted)
  - Different components → different key
  - Hash collision handling

- [ ] Test CachedQueryResult thread-safety
  - Concurrent reads
  - Exclusive writes
  - Dispose during read attempt

- [ ] Test QueryResultCache operations
  - Store and retrieve
  - Invalidation clears cache
  - Version tracking works

**Time Estimate:** 4-6 hours

#### Day 7: Integration Tests
- [ ] Test with ParallelQueryExecutor
  - Cache hit returns same entities
  - Cache miss populates cache
  - useCache=false bypasses cache

- [ ] Test invalidation modes
  - Global invalidates everything
  - Component-Based selective
  - Per-Frame boundary clearing

- [ ] Test edge cases
  - Empty queries
  - Single entity queries
  - Very large queries (10k+ entities)

**Time Estimate:** 4-6 hours

#### Day 8: Performance Benchmarks
- [ ] Benchmark cache hit vs miss
  - Measure archetype traversal savings
  - Compare with/without cache

- [ ] Benchmark memory overhead
  - Track cache size growth
  - Verify LRU eviction

- [ ] Benchmark thread contention
  - Parallel access patterns
  - Lock wait times

**Time Estimate:** 4-6 hours

#### Day 9-10: Polish & Documentation
- [ ] Add XML documentation comments
- [ ] Add usage examples to code
- [ ] Update ParallelQueryExecutor docs
- [ ] Add statistics tracking
- [ ] Memory profiling validation

**Time Estimate:** 6-8 hours

---

## Implementation Order

### Phase 1: Minimal Viable Cache (2-3 days)
**Goal:** Get basic caching working with Global invalidation

```csharp
// Step 1: Implement QueryCacheKey
public readonly struct QueryCacheKey { ... }

// Step 2: Implement CachedQueryResult
internal sealed class CachedQueryResult { ... }

// Step 3: Implement QueryResultCache (Global only)
public sealed class QueryResultCache
{
    public bool TryGet(...) { ... }
    public void Store(...) { ... }
    public void InvalidateAll() { ... }
}

// Step 4: Add to ParallelQueryExecutor
public void ExecuteParallel<T>(..., bool useCache = false)
{
    if (useCache && _cache != null)
    {
        if (_cache.TryGet(...)) { /* use cache */ }
    }
    // ... original logic
}
```

### Phase 2: Advanced Invalidation (1-2 days)
**Goal:** Add Component-Based and Per-Frame modes

```csharp
// Add to QueryResultCache:
public void InvalidateComponentType<T>() { ... }
public void InvalidateFrame() { ... }
```

### Phase 3: Memory Management (1 day)
**Goal:** Add LRU eviction and limits

```csharp
// Add to QueryResultCache:
private void EnforceMemoryLimit() { ... }
```

### Phase 4: Statistics & Polish (1 day)
**Goal:** Add monitoring and finalize API

```csharp
// Add to QueryResultCache:
public CacheStatistics GetStatistics() { ... }
```

---

## File Structure

```
PokeSharp.Core/Parallel/
├── ParallelQueryExecutor.cs (MODIFY)
│   └── Add useCache parameter to all ExecuteParallel methods
│
├── QueryCacheKey.cs (NEW)
│   ├── struct QueryCacheKey : IEquatable<QueryCacheKey>
│   └── enum QueryFilterFlags
│
├── CachedQueryResult.cs (NEW)
│   └── sealed class CachedQueryResult : IDisposable
│
├── QueryResultCache.cs (NEW)
│   ├── sealed class QueryResultCache : IDisposable
│   ├── sealed class QueryResultCacheConfig
│   ├── sealed class CacheStatistics
│   └── enum InvalidationMode
│
└── ParallelExecutionStats.cs (MODIFY)
    └── Add cache statistics properties
```

---

## Code Templates

### Template 1: QueryCacheKey

```csharp
using System;
using System.Linq;
using Arch.Core;

namespace PokeSharp.Core.Parallel;

/// <summary>
/// Immutable cache key for query results.
/// </summary>
public readonly struct QueryCacheKey : IEquatable<QueryCacheKey>
{
    private readonly int[] _componentTypeHashes;
    private readonly QueryFilterFlags _filterFlags;
    private readonly byte _genericArity;
    private readonly int _hashCode;

    public QueryCacheKey(QueryDescription query, int genericArity)
    {
        // TODO: Extract component types from query
        // TODO: Sort by hash for consistency
        // TODO: Compute hash code
    }

    public override int GetHashCode() => _hashCode;

    public bool Equals(QueryCacheKey other)
    {
        // TODO: Implement structural equality
        return false;
    }

    public override bool Equals(object? obj) =>
        obj is QueryCacheKey other && Equals(other);
}

[Flags]
internal enum QueryFilterFlags : byte
{
    None = 0,
    WithAll = 1 << 0,
    WithNone = 1 << 1,
    WithAny = 1 << 2
}
```

### Template 2: CachedQueryResult

```csharp
using System;
using System.Buffers;
using System.Diagnostics;
using System.Threading;
using Arch.Core;

namespace PokeSharp.Core.Parallel;

/// <summary>
/// Cached query result with version tracking.
/// </summary>
internal sealed class CachedQueryResult : IDisposable
{
    public Entity[] Entities { get; private set; }
    public int Count { get; private set; }
    public long Version { get; private set; }
    public Type[] DependentTypes { get; private set; }
    public long AccessTimestamp { get; set; }

    private readonly ReaderWriterLockSlim _lock = new();

    public CachedQueryResult(Entity[] entities, int count, long version, Type[] types)
    {
        Entities = entities;
        Count = count;
        Version = version;
        DependentTypes = types;
        AccessTimestamp = Stopwatch.GetTimestamp();
    }

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

### Template 3: QueryResultCache

```csharp
using System;
using System.Collections.Concurrent;
using System.Threading;

namespace PokeSharp.Core.Parallel;

/// <summary>
/// Thread-safe cache for query results.
/// </summary>
public sealed class QueryResultCache : IDisposable
{
    private readonly ConcurrentDictionary<QueryCacheKey, CachedQueryResult> _cache = new();
    private long _globalVersion = 0;
    private readonly QueryResultCacheConfig _config;
    private readonly CacheStatistics _stats = new();

    public QueryResultCache(QueryResultCacheConfig config)
    {
        _config = config ?? throw new ArgumentNullException(nameof(config));
    }

    public bool TryGet(
        QueryCacheKey key,
        out CachedQueryResult? result,
        out Entity[] entities,
        out int count)
    {
        // TODO: Implement cache lookup with version checking
        entities = null!;
        count = 0;
        result = null;
        return false;
    }

    public void Store(
        QueryCacheKey key,
        Entity[] entities,
        int count,
        Type[] dependentTypes)
    {
        // TODO: Implement cache storage with ownership transfer
    }

    public void InvalidateAll()
    {
        Interlocked.Increment(ref _globalVersion);
        _stats.InvalidationCount++;
    }

    public void Dispose()
    {
        foreach (var result in _cache.Values)
        {
            result.Dispose();
        }
        _cache.Clear();
    }
}
```

### Template 4: ParallelQueryExecutor Integration

```csharp
// Add to ParallelQueryExecutor class:

private readonly QueryResultCache? _resultCache;

public ParallelQueryExecutor(
    World world,
    int? maxThreads = null,
    QueryResultCacheConfig? cacheConfig = null)
{
    _world = world;
    _maxDegreeOfParallelism = maxThreads ?? Environment.ProcessorCount;
    _stats = new ParallelExecutionStats();

    // Cache is opt-in
    if (cacheConfig?.Enabled == true)
    {
        _resultCache = new QueryResultCache(cacheConfig);
    }
}

public void ExecuteParallel<T>(
    in QueryDescription query,
    EntityAction<T> action,
    bool useCache = false) where T : struct
{
    ArgumentNullException.ThrowIfNull(action);

    if (!useCache || _resultCache == null)
    {
        // Original uncached path
        ExecuteParallelUncached(query, action);
        return;
    }

    // Try cache first
    var key = new QueryCacheKey(query, genericArity: 1);
    if (_resultCache.TryGet(key, out var cached, out var entityArray, out var count))
    {
        // Cache hit
        ExecuteParallelCached<T>(entityArray, count, action);
        return;
    }

    // Cache miss: query and store
    ExecuteParallelAndCache<T>(key, query, action);
}

private void ExecuteParallelUncached<T>(
    in QueryDescription query,
    EntityAction<T> action) where T : struct
{
    // ... existing implementation ...
}
```

---

## Testing Strategy

### Unit Test Template

```csharp
using Xunit;
using Arch.Core;
using PokeSharp.Core.Parallel;

namespace PokeSharp.Tests.Parallel;

public class QueryResultCacheTests
{
    [Fact]
    public void CacheHit_ReturnsSameEntities()
    {
        // Arrange
        var world = World.Create();
        var config = new QueryResultCacheConfig { Enabled = true };
        var executor = new ParallelQueryExecutor(world, cacheConfig: config);

        // Create test entities
        for (int i = 0; i < 100; i++)
        {
            world.Create(new Position { X = i, Y = i });
        }

        var query = new QueryDescription().WithAll<Position>();

        // Act
        var results1 = new List<Entity>();
        executor.ExecuteParallel<Position>(
            query,
            (e, ref p) => results1.Add(e),
            useCache: true
        );

        var results2 = new List<Entity>();
        executor.ExecuteParallel<Position>(
            query,
            (e, ref p) => results2.Add(e),
            useCache: true
        );

        // Assert
        Assert.Equal(results1, results2);

        var stats = executor.GetCacheStatistics();
        Assert.Equal(1, stats.HitCount);
        Assert.Equal(1, stats.MissCount);
    }

    [Fact]
    public void Invalidation_ClearsCacheCorrectly()
    {
        // TODO: Implement invalidation test
    }
}
```

---

## Performance Validation

### Benchmark Template

```csharp
using BenchmarkDotNet.Attributes;
using Arch.Core;
using PokeSharp.Core.Parallel;

[MemoryDiagnoser]
public class QueryResultCacheBenchmark
{
    private World _world = null!;
    private ParallelQueryExecutor _executorCached = null!;
    private ParallelQueryExecutor _executorUncached = null!;
    private QueryDescription _query;

    [GlobalSetup]
    public void Setup()
    {
        _world = World.Create();

        // Create 10,000 test entities
        for (int i = 0; i < 10_000; i++)
        {
            _world.Create(
                new Position { X = i, Y = i },
                new Velocity { X = 1, Y = 1 }
            );
        }

        _query = new QueryDescription().WithAll<Position, Velocity>();

        // Uncached executor
        _executorUncached = new ParallelQueryExecutor(_world);

        // Cached executor
        var config = new QueryResultCacheConfig
        {
            Enabled = true,
            InvalidationMode = InvalidationMode.PerFrame
        };
        _executorCached = new ParallelQueryExecutor(_world, cacheConfig: config);
    }

    [Benchmark(Baseline = true)]
    public void WithoutCache()
    {
        _executorUncached.ExecuteParallel<Position, Velocity>(
            _query,
            (e, ref p, ref v) => { p.X += v.X; p.Y += v.Y; },
            useCache: false
        );
    }

    [Benchmark]
    public void WithCache()
    {
        _executorCached.ExecuteParallel<Position, Velocity>(
            _query,
            (e, ref p, ref v) => { p.X += v.X; p.Y += v.Y; },
            useCache: true
        );
    }
}
```

**Expected Results:**
```
|      Method |     Mean |   Error |  StdDev | Ratio | Allocated |
|------------ |---------:|--------:|--------:|------:|----------:|
| WithoutCache| 5.000 ms | 0.05 ms | 0.04 ms |  1.00 |     80 B  |
|   WithCache | 3.200 ms | 0.03 ms | 0.02 ms |  0.64 |     40 B  |
```

---

## Common Pitfalls & Solutions

### Pitfall 1: Forgetting to Invalidate
**Problem:** Cache not cleared after entity spawn/destroy
**Solution:** Call `InvalidateCache()` after structural changes
```csharp
world.Create(new Enemy());
executor.InvalidateCache(); // Don't forget!
```

### Pitfall 2: Over-Caching Small Queries
**Problem:** Caching overhead > query cost for small entity counts
**Solution:** Use `MinEntitiesToCache` config
```csharp
var config = new QueryResultCacheConfig
{
    MinEntitiesToCache = 10 // Skip queries with <10 entities
};
```

### Pitfall 3: Memory Leaks from Non-Disposal
**Problem:** Cached arrays not returned to pool
**Solution:** Always call Dispose or use proper lifecycle management
```csharp
using var cache = new QueryResultCache(config);
// ... use cache ...
// Dispose called automatically
```

### Pitfall 4: Hash Collisions
**Problem:** Different queries mapped to same key
**Solution:** Full structural equality check in Equals()
```csharp
public bool Equals(QueryCacheKey other)
{
    // MUST check more than just hash!
    if (_hashCode != other._hashCode) return false;
    // ... full equality check ...
}
```

---

## Integration Checklist

Before merging to main:

- [ ] All unit tests pass (>90% coverage)
- [ ] Performance benchmarks show expected gains
- [ ] Memory profiler confirms no leaks
- [ ] Thread-safety validated under load
- [ ] Documentation complete (XML comments + guides)
- [ ] Code review completed
- [ ] Production testing in dev environment
- [ ] Statistics tracking working correctly
- [ ] Configuration defaults validated
- [ ] Error handling comprehensive

---

## Success Metrics

After implementation, validate:

1. **Performance Gain**: 30-50% speedup for stable queries
2. **Memory Overhead**: <10 MB for typical workloads
3. **Hit Rate**: >80% for per-frame caching
4. **Thread Safety**: No race conditions under stress test
5. **Correctness**: Zero stale data bugs in production

---

## Questions & Support

**Design Documents:**
- Full Design: `/docs/PHASE-4D-QUERYRESULTCACHE-DESIGN.md`
- Quick Reference: `/docs/PHASE-4D-ARCHITECTURE-SUMMARY.md`
- Diagrams: `/docs/PHASE-4D-DIAGRAMS.md`

**When Stuck:**
1. Review design documents for architecture details
2. Check diagrams for data flow visualization
3. Look at code templates for implementation patterns
4. Run unit tests incrementally to validate each component

---

**Document Version:** 1.0
**Last Updated:** 2025-11-09
**Estimated Implementation Time:** 1-2 weeks
**Target Performance Gain:** 30-50%
