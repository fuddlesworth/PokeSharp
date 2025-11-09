# QueryResultCache - Architecture Diagrams

Visual reference for the QueryResultCache system design.

---

## 1. System Architecture

```
┌─────────────────────────────────────────────────────────────────┐
│                    ParallelQueryExecutor                        │
│                                                                 │
│  ┌───────────────────────────────────────────────────────────┐ │
│  │              QueryResultCache (Optional)                  │ │
│  │                                                           │ │
│  │  ┌─────────────────────────────────────────────────┐    │ │
│  │  │   ConcurrentDictionary<Key, CachedResult>       │    │ │
│  │  │                                                 │    │ │
│  │  │   Key1 → CachedQueryResult                      │    │ │
│  │  │          ├─ Entity[] (ArrayPool)                │    │ │
│  │  │          ├─ Count: 1000                         │    │ │
│  │  │          ├─ Version: 42                         │    │ │
│  │  │          ├─ DependentTypes: [Position, Sprite]  │    │ │
│  │  │          └─ AccessTimestamp: 12345678           │    │ │
│  │  │                                                 │    │ │
│  │  │   Key2 → CachedQueryResult                      │    │ │
│  │  │          ├─ Entity[] (ArrayPool)                │    │ │
│  │  │          └─ ...                                 │    │ │
│  │  └─────────────────────────────────────────────────┘    │ │
│  │                                                           │ │
│  │  ┌─────────────────────────────────────────────────┐    │ │
│  │  │   InvalidationTracker                           │    │ │
│  │  │   ├─ GlobalVersion: 42 (Interlocked.Increment)  │    │ │
│  │  │   ├─ ModifiedComponents: HashSet<Type>          │    │ │
│  │  │   └─ InvalidationMode: Global/Component/Frame   │    │ │
│  │  └─────────────────────────────────────────────────┘    │ │
│  │                                                           │ │
│  │  ┌─────────────────────────────────────────────────┐    │ │
│  │  │   CacheStatistics                               │    │ │
│  │  │   ├─ HitCount: 8500                             │    │ │
│  │  │   ├─ MissCount: 1500                            │    │ │
│  │  │   ├─ HitRate: 85%                               │    │ │
│  │  │   ├─ MemoryBytes: 8,192,000                     │    │ │
│  │  │   └─ CachedQueryCount: 45                       │    │ │
│  │  └─────────────────────────────────────────────────┘    │ │
│  └───────────────────────────────────────────────────────────┘ │
│                                                                 │
│  ┌───────────────────────────────────────────────────────────┐ │
│  │        QueryCache (Existing - QueryDescription)           │ │
│  │        ConcurrentDictionary<string, QueryDescription>     │ │
│  └───────────────────────────────────────────────────────────┘ │
│                                                                 │
│  ┌───────────────────────────────────────────────────────────┐ │
│  │        Arch.Core.World (ECS)                              │ │
│  │        Entities & Components                              │ │
│  └───────────────────────────────────────────────────────────┘ │
└─────────────────────────────────────────────────────────────────┘
```

---

## 2. Cache Key Structure

```
┌────────────────────────────────────────────────────────────┐
│                     QueryCacheKey                          │
│                    (Immutable Struct)                      │
├────────────────────────────────────────────────────────────┤
│                                                            │
│  Component Type Hashes (sorted):                          │
│  ┌──────────────────────────────────────────────────┐    │
│  │  [12345678, 23456789, 34567890]                   │    │
│  │   Position   Velocity   Sprite                    │    │
│  └──────────────────────────────────────────────────┘    │
│                                                            │
│  Query Filter Flags:                                      │
│  ┌──────────────────────────────────────────────────┐    │
│  │  WithAll: true                                     │    │
│  │  WithNone: true   (e.g., !Dead)                   │    │
│  │  WithAny: false                                    │    │
│  └──────────────────────────────────────────────────┘    │
│                                                            │
│  Generic Arity:                                           │
│  ┌──────────────────────────────────────────────────┐    │
│  │  3  (ExecuteParallel<T1, T2, T3>)                 │    │
│  └──────────────────────────────────────────────────┘    │
│                                                            │
│  Pre-computed Hash Code:                                  │
│  ┌──────────────────────────────────────────────────┐    │
│  │  -1234567890                                       │    │
│  │  (computed once during construction)               │    │
│  └──────────────────────────────────────────────────┘    │
└────────────────────────────────────────────────────────────┘

Equality Check:
1. Compare hash codes (fast rejection)
2. Compare generic arity
3. Compare filter flags
4. Compare component type arrays (SequenceEqual)
```

---

## 3. Query Execution Flow (WITH Cache)

```
┌─────────────────────────────────────────────────────────────┐
│  ExecuteParallel<T>(query, action, useCache: true)          │
└────────────────────┬────────────────────────────────────────┘
                     │
                     ▼
        ┌────────────────────────┐
        │ Create QueryCacheKey   │
        │ from query + arity     │
        └────────┬───────────────┘
                 │
                 ▼
        ┌────────────────────────┐
        │ TryGet(key)            │
        │ from QueryResultCache  │
        └────┬───────────────────┘
             │
    ┌────────┴──────────┐
    ▼                   ▼
┌─────────┐       ┌──────────┐
│  Hit?   │       │  Miss?   │
└────┬────┘       └─────┬────┘
     │                  │
     ▼                  ▼
┌──────────────┐   ┌────────────────────────┐
│ Cache HIT    │   │    Cache MISS          │
└──────┬───────┘   └────────┬───────────────┘
       │                    │
       │                    ▼
       │           ┌────────────────────────┐
       │           │ Query World (Arch)     │
       │           │ CountEntities + Query  │
       │           └────────┬───────────────┘
       │                    │
       │                    ▼
       │           ┌────────────────────────┐
       │           │ Rent Entity[] from     │
       │           │ ArrayPool              │
       │           └────────┬───────────────┘
       │                    │
       │                    ▼
       │           ┌────────────────────────┐
       │           │ Collect Entities       │
       │           │ into array             │
       │           └────────┬───────────────┘
       │                    │
       │                    ▼
       │           ┌────────────────────────┐
       │           │ Store in Cache         │
       │           │ (Cache owns array)     │
       │           └────────┬───────────────┘
       │                    │
       └────────┬───────────┘
                │
                ▼
       ┌────────────────────────┐
       │ Parallel.For(entities) │
       │ Execute action on each │
       └────────┬───────────────┘
                │
                ▼
       ┌────────────────────────┐
       │ Record Statistics      │
       │ (hit/miss, time, etc.) │
       └────────────────────────┘

Cache HIT Path:   3ms  (skip archetype traversal)
Cache MISS Path:  5ms  (query + cache storage)
```

---

## 4. Invalidation Flow

### Global Invalidation

```
┌─────────────────────────────────────────────────────────┐
│                  Structural Change                      │
│    (Entity.Create/Destroy/Add/Remove Component)         │
└──────────────────────┬──────────────────────────────────┘
                       │
                       ▼
          ┌────────────────────────┐
          │  InvalidateAll()       │
          │  Interlocked.Increment │
          │  (GlobalVersion)       │
          └────────┬───────────────┘
                   │
                   ▼
    ┌──────────────────────────────┐
    │  GlobalVersion: 42 → 43      │
    └──────────────────────────────┘
                   │
                   ▼
    ┌──────────────────────────────────────────┐
    │  Next Cache Lookup:                      │
    │  result.Version (42) < GlobalVersion (43)│
    │  → Cache MISS                            │
    └──────────────────────────────────────────┘

ALL cached queries invalidated instantly.
```

### Component-Based Invalidation

```
┌─────────────────────────────────────────────────────────┐
│          Component Modified: Position                   │
└──────────────────────┬──────────────────────────────────┘
                       │
                       ▼
          ┌────────────────────────┐
          │ InvalidateCache        │
          │ <Position>()           │
          └────────┬───────────────┘
                   │
                   ▼
    ┌──────────────────────────────┐
    │ Add typeof(Position) to      │
    │ ModifiedComponents set       │
    │ Increment GlobalVersion      │
    └──────────┬───────────────────┘
                   │
                   ▼
    ┌──────────────────────────────────────────┐
    │  Cache Lookup Check:                     │
    │  If result.DependentTypes.Contains       │
    │     (typeof(Position))                    │
    │  → Invalidate this query                 │
    │  Else → Keep using cache                 │
    └──────────────────────────────────────────┘

ONLY queries with Position invalidated.
Queries with Velocity, Health, etc. still cached.
```

### Per-Frame Invalidation

```
┌─────────────────────────────────────────────────────────┐
│                   Frame Boundary                        │
│              (Update loop starts)                       │
└──────────────────────┬──────────────────────────────────┘
                       │
                       ▼
          ┌────────────────────────┐
          │  BeginFrame()          │
          │  Increment             │
          │  GlobalVersion         │
          └────────┬───────────────┘
                   │
                   ▼
    ┌──────────────────────────────┐
    │  ALL cache entries invalid   │
    │  for this frame              │
    └──────────────────────────────┘
                   │
                   ▼
    ┌──────────────────────────────────────────┐
    │  Systems execute during frame:           │
    │  - First query: Cache MISS (populate)    │
    │  - Subsequent queries: Cache HIT         │
    └──────────────────────────────────────────┘
                   │
                   ▼
    ┌──────────────────────────────────────────┐
    │  Next Frame: Cache cleared again         │
    └──────────────────────────────────────────┘

Cache valid ONLY within single frame.
Assumes no structural changes during frame.
```

---

## 5. Memory Management (LRU Eviction)

```
┌─────────────────────────────────────────────────────────┐
│           Cache Storage (100 query limit)               │
├─────────────────────────────────────────────────────────┤
│                                                         │
│  Query 1:  AccessTimestamp: 1000  (Oldest)             │
│  Query 2:  AccessTimestamp: 1005                        │
│  Query 3:  AccessTimestamp: 1010                        │
│  ...                                                    │
│  Query 99: AccessTimestamp: 1500                        │
│  Query 100: AccessTimestamp: 1505  (Newest)            │
└─────────────────────────────────────────────────────────┘
                       │
                       ▼  (101st query added)
          ┌────────────────────────┐
          │  EnforceMemoryLimit()  │
          │  Triggered             │
          └────────┬───────────────┘
                   │
                   ▼
    ┌──────────────────────────────────────────┐
    │  1. Sort by AccessTimestamp (ascending)  │
    │  2. Take oldest entries to evict         │
    │     (Count - MaxQueries + 1)             │
    │  3. TryRemove from cache                 │
    │  4. Dispose (return array to pool)       │
    └──────────┬───────────────────────────────┘
                   │
                   ▼
    ┌──────────────────────────────────────────┐
    │  Cache Storage (back to 100 queries)     │
    │  Query 1 EVICTED                         │
    │  Query 2-100: Kept                        │
    │  Query 101: Added                         │
    └──────────────────────────────────────────┘

Evicted arrays returned to ArrayPool immediately.
```

---

## 6. Thread Safety Model

```
┌─────────────────────────────────────────────────────────┐
│                 Thread Safety Layers                    │
├─────────────────────────────────────────────────────────┤
│                                                         │
│  Layer 1: ConcurrentDictionary (Cache Container)       │
│  ┌───────────────────────────────────────────────┐    │
│  │  Lock-Free Reads: TryGetValue (multiple OK)   │    │
│  │  Synchronized Writes: AddOrUpdate, TryRemove   │    │
│  │  ✅ Multiple threads can lookup simultaneously │    │
│  └───────────────────────────────────────────────┘    │
│                                                         │
│  Layer 2: CachedQueryResult (Per-Entry)                │
│  ┌───────────────────────────────────────────────┐    │
│  │  ReaderWriterLockSlim:                         │    │
│  │  - Read Lock: CopyTo() [multiple readers]      │    │
│  │  - Write Lock: Dispose() [exclusive]           │    │
│  │  ✅ Allows parallel reads, exclusive writes    │    │
│  └───────────────────────────────────────────────┘    │
│                                                         │
│  Layer 3: Version Counter (Invalidation)               │
│  ┌───────────────────────────────────────────────┐    │
│  │  Interlocked.Increment(ref _globalVersion)     │    │
│  │  ✅ Atomic, thread-safe increment              │    │
│  └───────────────────────────────────────────────┘    │
└─────────────────────────────────────────────────────────┘

Scenario: 3 Threads Query Simultaneously
┌──────────┐  ┌──────────┐  ┌──────────┐
│ Thread 1 │  │ Thread 2 │  │ Thread 3 │
└────┬─────┘  └────┬─────┘  └────┬─────┘
     │             │             │
     ▼             ▼             ▼
┌─────────────────────────────────────┐
│  ConcurrentDictionary.TryGetValue   │
│  (All 3 threads read simultaneously)│
└─────────┬───────────────────────────┘
          │
    ┌─────┼─────┐
    ▼     ▼     ▼
  Hit   Hit   Miss
    │     │     │
    │     │     └──► Query World
    │     │           │
    ▼     ▼           ▼
  Process entities  Store in cache
                     (lock-free)

✅ No blocking, maximum parallelism
```

---

## 7. Statistics Tracking Flow

```
┌─────────────────────────────────────────────────────────┐
│               Query Execution with Stats                │
└────────────────────┬────────────────────────────────────┘
                     │
                     ▼
        ┌────────────────────────┐
        │ ExecuteParallel called │
        └────────┬───────────────┘
                 │
        ┌────────┴─────────┐
        ▼                  ▼
    ┌────────┐        ┌─────────┐
    │ Cache  │        │  Cache  │
    │  HIT   │        │  MISS   │
    └───┬────┘        └────┬────┘
        │                  │
        ▼                  ▼
┌────────────────┐  ┌────────────────┐
│ stats.HitCount++│  │ stats.MissCount++│
└────────┬────────┘  └────────┬───────┘
         │                    │
         └────────┬───────────┘
                  │
                  ▼
         ┌────────────────────┐
         │ Update Statistics: │
         │ - Total queries    │
         │ - Hit rate         │
         │ - Memory bytes     │
         │ - Execution time   │
         └────────┬───────────┘
                  │
                  ▼
         ┌────────────────────┐
         │ GetStatistics()    │
         │ Returns snapshot   │
         └────────────────────┘

Statistics Output:
┌──────────────────────────────────┐
│ Cache Hit Rate: 85.7%            │
│ Total Queries: 10,000            │
│ Cache Hits: 8,570                │
│ Cache Misses: 1,430              │
│ Cached Queries: 45               │
│ Memory Used: 8.2 MB              │
│ Invalidations: 12                │
│ Evictions: 3                     │
└──────────────────────────────────┘
```

---

## 8. Configuration Decision Tree

```
                Start
                  │
                  ▼
        ┌─────────────────────┐
        │ Need result caching?│
        └─────┬─────────┬─────┘
              │ No      │ Yes
              ▼         ▼
        Leave disabled  │
                        │
                        ▼
        ┌───────────────────────────┐
        │ Entity population stable? │
        └───────┬───────────┬───────┘
                │ No        │ Yes
                ▼           ▼
        ┌───────────┐  ┌──────────────┐
        │ Don't use │  │ How often    │
        │ cache     │  │ entities     │
        └───────────┘  │ change?      │
                       └───┬──────────┘
                           │
            ┌──────────────┼──────────────┐
            ▼              ▼              ▼
      ┌──────────┐  ┌────────────┐  ┌────────────┐
      │ Rarely   │  │ Per Frame  │  │ Frequently │
      │ (<5/sec) │  │            │  │ (>20/sec)  │
      └────┬─────┘  └─────┬──────┘  └─────┬──────┘
           │              │               │
           ▼              ▼               ▼
   ┌────────────────┐ ┌──────────┐ ┌─────────────┐
   │ ComponentBased │ │ PerFrame │ │   Global    │
   │ Invalidation   │ │ Inval.   │ │ Invalidation│
   └────────────────┘ └──────────┘ └─────────────┘

Configuration Examples:

1. Stable Render Loop (5000 entities, no spawns):
   ✅ PerFrame + Cache

2. Dynamic Combat (entities spawn/die frequently):
   ❌ Don't cache OR Global invalidation

3. Strategic Game (few changes per turn):
   ✅ ComponentBased + Cache

4. Debug/Testing:
   ✅ Global invalidation (safest)
```

---

## 9. Integration with Game Loop

```
┌─────────────────────────────────────────────────────────┐
│                    Game Loop                            │
├─────────────────────────────────────────────────────────┤
│                                                         │
│  Frame N:                                               │
│  ┌───────────────────────────────────────────────┐    │
│  │ 1. BeginFrame()                               │    │
│  │    → Invalidate cache (PerFrame mode)         │    │
│  └───────────────┬───────────────────────────────┘    │
│                  │                                      │
│  ┌───────────────▼───────────────────────────────┐    │
│  │ 2. Input Processing                           │    │
│  │    → May create/destroy entities              │    │
│  │    → Call InvalidateCache() if needed         │    │
│  └───────────────┬───────────────────────────────┘    │
│                  │                                      │
│  ┌───────────────▼───────────────────────────────┐    │
│  │ 3. System Updates (ParallelSystemManager)     │    │
│  │                                                │    │
│  │    MovementSystem:                             │    │
│  │    ├─ Query Position+Velocity [CACHE MISS]    │    │
│  │    └─ Cache populated                          │    │
│  │                                                │    │
│  │    PhysicsSystem:                              │    │
│  │    ├─ Query Position+Velocity [CACHE HIT] ✅  │    │
│  │    └─ Reuses cached entities                   │    │
│  │                                                │    │
│  │    RenderSystem:                               │    │
│  │    ├─ Query Position+Sprite [CACHE MISS]      │    │
│  │    └─ Cache populated                          │    │
│  │                                                │    │
│  │    AISystem:                                   │    │
│  │    ├─ Query Position+AI [CACHE MISS]          │    │
│  │    └─ Cache populated                          │    │
│  └───────────────┬───────────────────────────────┘    │
│                  │                                      │
│  ┌───────────────▼───────────────────────────────┐    │
│  │ 4. Render                                      │    │
│  └───────────────┬───────────────────────────────┘    │
│                  │                                      │
│  ┌───────────────▼───────────────────────────────┐    │
│  │ 5. EndFrame()                                  │    │
│  │    → Statistics collection                     │    │
│  │    → Cache health check                        │    │
│  └────────────────────────────────────────────────┘    │
│                                                         │
│  Frame N+1:                                             │
│  (Repeat with fresh cache)                              │
└─────────────────────────────────────────────────────────┘
```

---

## 10. Performance Comparison

```
Scenario: 10,000 entities, Position+Velocity query, 60 FPS

WITHOUT CACHE:
┌────────────────────────────────────────┐
│ Frame 1-60 (each frame):               │
│ ├─ Archetype traversal: 2ms            │
│ ├─ Parallel processing: 3ms            │
│ └─ Total: 5ms per frame                │
│                                        │
│ 60 frames × 5ms = 300ms/second        │
└────────────────────────────────────────┘

WITH CACHE (PerFrame, stable entities):
┌────────────────────────────────────────┐
│ Frame 1 (cache miss):                  │
│ ├─ Archetype traversal: 2ms            │
│ ├─ Parallel processing: 3ms            │
│ └─ Cache storage: 0.1ms                │
│ └─ Total: 5.1ms                        │
│                                        │
│ Frames 2-60 (cache hit):               │
│ ├─ Archetype traversal: 0ms ✅         │
│ ├─ Parallel processing: 3ms            │
│ └─ Total: 3ms per frame                │
│                                        │
│ (1 × 5ms) + (59 × 3ms) = 182ms/second │
└────────────────────────────────────────┘

SPEEDUP: (300ms - 182ms) / 300ms = 39.3%

Bar Chart:
Without Cache: ████████████████████  300ms
With Cache:    ████████████          182ms
               ↑
               39% faster
```

---

**Document Version**: 1.0
**Last Updated**: 2025-11-09
**Related**: PHASE-4D-QUERYRESULTCACHE-DESIGN.md
