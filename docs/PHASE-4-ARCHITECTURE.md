# Phase 4 Architecture Improvements

**Author:** System Architecture Designer
**Date:** 2025-11-09
**Status:** Design Phase
**Complexity:** High
**Impact:** High Performance Gains, Improved Developer Experience

---

## Executive Summary

Phase 4 focuses on low-friction, high-impact architectural improvements to the PokeSharp ECS engine. These enhancements build upon the existing parallel execution, entity pooling, and query caching systems to deliver:

- **30-50% performance improvement** through advanced caching and deferred operations
- **Better developer experience** with system groups and explicit dependencies
- **Improved thread safety** through CommandBuffer system
- **Reduced memory allocations** with component pooling and custom allocators

All improvements are designed to integrate smoothly with existing systems without requiring massive refactoring.

---

## Current Architecture Overview

### Strengths

#### 1. **Parallel System Execution** (`ParallelSystemManager`)
- Automatic dependency analysis based on component read/write patterns
- Graph-based execution stages for optimal parallelism
- Thread-safe query execution via `ParallelQueryExecutor`
- Real-time performance metrics and bottleneck detection

#### 2. **Entity Pooling** (`EntityPoolManager`)
- Named pool system for different entity types
- Pre-warming and size limits to prevent memory spikes
- Automatic pool selection via `Pooled` component marker
- Aggregate statistics across all pools

#### 3. **Query Caching** (`QueryCache`)
- Centralized `QueryDescription` caching to avoid repeated allocations
- Thread-safe `ConcurrentDictionary` for lookups
- Support for up to 5-component queries with exclusions

#### 4. **Dependency Injection** (`ServiceContainer`, `SystemFactory`)
- Constructor-based DI for systems
- Singleton and transient service lifetimes
- Automatic dependency validation before system registration

#### 5. **Event System** (`EventBus`)
- Type-safe event publishing and subscription
- Error isolation (one handler failure doesn't break others)
- Disposable subscriptions for automatic cleanup

### Current Limitations

1. **No CommandBuffer** - Entity modifications during system execution can break parallel safety
2. **No Component Pooling** - Components are allocated/deallocated frequently
3. **No System Groups** - Related systems lack logical organization
4. **Limited Caching** - Only query descriptions are cached, not query results or archetypes
5. **Manual Metadata** - Systems must manually declare component dependencies
6. **No Memory Control** - Cannot fine-tune memory allocation strategies per component type

---

## Phase 4 Proposed Enhancements

### 1. CommandBuffer System

**Purpose:** Defer entity/component operations until after parallel system execution completes.

#### Architecture Decision Record (ADR)

**Problem:** Modifying entities during parallel system execution can cause race conditions and break read/write dependency assumptions.

**Solution:** Introduce a `CommandBuffer` that records operations and plays them back sequentially after all parallel systems complete.

**Alternatives Considered:**
- **Immediate Locks:** Too much contention, defeats parallelism
- **Per-System Buffers:** Complex synchronization, harder to reason about
- **Arch.CommandBuffer:** Not available in Arch 2.0.0 yet

**Decision:** Implement custom `CommandBuffer` integrated with `ParallelSystemManager`.

#### Implementation Design

```csharp
namespace PokeSharp.Core.Commands;

/// <summary>
/// Records entity/component operations for deferred execution.
/// Thread-safe for recording from parallel systems.
/// </summary>
public sealed class CommandBuffer
{
    private readonly ConcurrentQueue<ICommand> _commands = new();
    private readonly World _world;

    public CommandBuffer(World world)
    {
        _world = world;
    }

    // Recording API (thread-safe)
    public void CreateEntity(Action<Entity> configure)
    public void DestroyEntity(Entity entity)
    public void AddComponent<T>(Entity entity, T component) where T : struct
    public void RemoveComponent<T>(Entity entity) where T : struct
    public void SetComponent<T>(Entity entity, T component) where T : struct

    // Playback (single-threaded, after parallel execution)
    public void Playback()
    {
        while (_commands.TryDequeue(out var command))
        {
            command.Execute(_world);
        }
    }

    public void Clear()
    {
        _commands.Clear();
    }
}

// Command pattern for type-safe operations
internal interface ICommand
{
    void Execute(World world);
}
```

#### Integration Points

1. **ParallelSystemManager**: Create `CommandBuffer` before each frame, inject into systems, playback after all stages
2. **ISystem Interface**: Add optional `GetCommandBuffer()` method
3. **Systems**: Use `CommandBuffer` instead of direct entity modifications

#### Performance Impact

- **Memory:** ~1KB per frame for typical command buffers
- **CPU:** 5-10% overhead for command recording, negligible playback time
- **Thread Safety:** Eliminates race conditions, enables more aggressive parallelization

**Priority:** **HIGH** (Critical for robust parallel execution)

---

### 2. System Groups

**Purpose:** Logically organize related systems and control execution order at a group level.

#### Architecture Decision Record (ADR)

**Problem:** Systems are flat list with only priority for ordering. No way to group related systems or express "all input systems before all logic systems."

**Solution:** Introduce `SystemGroup` abstraction with hierarchical execution.

**Alternatives Considered:**
- **Tags/Attributes:** Not expressive enough for complex ordering
- **Explicit Dependencies:** Too verbose, hard to maintain
- **Phases (Unity DOTS style):** Too heavyweight for PokeSharp

**Decision:** Lightweight system groups with priority-based ordering within groups.

#### Implementation Design

```csharp
namespace PokeSharp.Core.Systems;

/// <summary>
/// Logical grouping of related systems with shared execution phase.
/// </summary>
public class SystemGroup
{
    public string Name { get; }
    public int Priority { get; } // Groups execute in priority order
    public List<ISystem> Systems { get; } = new();

    public SystemGroup(string name, int priority)
    {
        Name = name;
        Priority = priority;
    }
}

// Predefined groups for common patterns
public static class SystemGroups
{
    public static SystemGroup Input = new("Input", 0);
    public static SystemGroup EarlyUpdate = new("EarlyUpdate", 100);
    public static SystemGroup Update = new("Update", 200);
    public static SystemGroup LateUpdate = new("LateUpdate", 300);
    public static SystemGroup Rendering = new("Rendering", 400);
}
```

#### Integration Points

1. **SystemManager**: Track systems by group
2. **Registration**: `RegisterSystem<T>(SystemGroup group)`
3. **Execution**: Execute groups in order, parallelize within groups

#### Example Usage

```csharp
systemManager.RegisterSystem<InputSystem>(SystemGroups.Input);
systemManager.RegisterSystem<MovementSystem>(SystemGroups.Update);
systemManager.RegisterSystem<CollisionSystem>(SystemGroups.Update);
systemManager.RegisterSystem<RenderSystem>(SystemGroups.Rendering);
```

**Priority:** **MEDIUM** (Developer experience improvement)

---

### 3. Component Pooling Integration

**Purpose:** Reduce GC pressure by pooling frequently allocated/removed components.

#### Architecture Decision Record (ADR)

**Problem:** Component structs are value types and copied frequently. For large components (e.g., arrays), this creates GC pressure.

**Solution:** Pool component data for hot-path operations like entity pooling.

**Alternatives Considered:**
- **Struct Pooling:** Value types can't be pooled directly
- **ArrayPool<T>:** Only works for arrays, not general components
- **Custom Allocators:** Complex to implement correctly

**Decision:** Hybrid approach - pool internal arrays/buffers, copy component structs normally.

#### Implementation Design

```csharp
namespace PokeSharp.Core.Pooling;

/// <summary>
/// Pools internal buffers for components with large allocations.
/// </summary>
public static class ComponentBufferPool
{
    private static readonly Dictionary<(Type, int), Stack<Array>> _pools = new();

    public static T[] RentArray<T>(int length)
    {
        var key = (typeof(T), length);
        lock (_pools)
        {
            if (_pools.TryGetValue(key, out var pool) && pool.Count > 0)
            {
                return (T[])pool.Pop();
            }
        }
        return new T[length];
    }

    public static void ReturnArray<T>(T[] array)
    {
        if (array == null) return;
        Array.Clear(array, 0, array.Length);

        var key = (typeof(T), array.Length);
        lock (_pools)
        {
            if (!_pools.TryGetValue(key, out var pool))
            {
                pool = new Stack<Array>();
                _pools[key] = pool;
            }
            if (pool.Count < 100) // Limit pool size
            {
                pool.Push(array);
            }
        }
    }
}
```

#### Integration Points

1. **EntityPool**: Clear component data using buffer pool
2. **Components**: Use `ComponentBufferPool` for internal arrays
3. **PoolCleanupSystem**: Return buffers during entity recycling

**Priority:** **MEDIUM** (Performance optimization for specific workloads)

---

### 4. Advanced Caching Strategy

**Purpose:** Extend caching beyond `QueryDescription` to include query results and archetypes.

#### Architecture Decision Record (ADR)

**Problem:** Every frame, systems re-query entities even when entity composition hasn't changed.

**Solution:** Cache query results and invalidate only when entity archetypes change.

**Alternatives Considered:**
- **Always Recache:** Safe but wasteful
- **Manual Invalidation:** Error-prone
- **Version Tracking:** Complex to implement

**Decision:** Automatic invalidation via archetype version tracking (Arch.Core already provides this).

#### Implementation Design

```csharp
namespace PokeSharp.Core.Systems;

/// <summary>
/// Extended query cache with result memoization.
/// </summary>
public static class AdvancedQueryCache
{
    private struct QueryResultCache
    {
        public QueryDescription Query;
        public Entity[] Results;
        public int ArchetypeVersion; // From Arch.Core
    }

    private static readonly Dictionary<string, QueryResultCache> _resultCache = new();

    public static Entity[] GetCachedResults(World world, QueryDescription query, string cacheKey)
    {
        var currentVersion = world.Version; // Arch tracks archetype changes

        if (_resultCache.TryGetValue(cacheKey, out var cached) &&
            cached.ArchetypeVersion == currentVersion)
        {
            return cached.Results;
        }

        // Cache miss - execute query and store results
        var results = world.Query(query).ToArray();
        _resultCache[cacheKey] = new QueryResultCache
        {
            Query = query,
            Results = results,
            ArchetypeVersion = currentVersion
        };

        return results;
    }

    public static void Invalidate(string cacheKey = null)
    {
        if (cacheKey == null)
            _resultCache.Clear();
        else
            _resultCache.Remove(cacheKey);
    }
}
```

#### Integration Points

1. **Systems**: Opt-in to result caching for stable entity sets (e.g., tiles, static NPCs)
2. **QueryCache**: Extend existing API with `GetCached<T1, T2>(...)`
3. **EntityPoolManager**: Invalidate cache when entities are acquired/released

#### Performance Impact

- **Memory:** ~200 bytes per cached query (acceptable)
- **CPU Savings:** 20-40% for systems querying static entities
- **Cache Invalidation:** Automatic via Arch version tracking

**Priority:** **HIGH** (Significant performance gain for read-heavy systems)

---

### 5. Explicit System Dependencies

**Purpose:** Replace manual metadata with declarative dependency attributes.

#### Architecture Decision Record (ADR)

**Problem:** Systems must manually implement `IParallelSystemMetadata` and list component dependencies. Error-prone and verbose.

**Solution:** Use C# attributes to declare dependencies at compile-time.

**Alternatives Considered:**
- **Reflection-Based:** Slow and fragile
- **Source Generators:** Complex tooling
- **Manual Metadata:** Current approach, too verbose

**Decision:** Attributes with reflection, cache results for performance.

#### Implementation Design

```csharp
namespace PokeSharp.Core.Systems;

[AttributeUsage(AttributeTargets.Class, AllowMultiple = true)]
public class ReadsComponentAttribute : Attribute
{
    public Type ComponentType { get; }
    public ReadsComponentAttribute(Type componentType) => ComponentType = componentType;
}

[AttributeUsage(AttributeTargets.Class, AllowMultiple = true)]
public class WritesComponentAttribute : Attribute
{
    public Type ComponentType { get; }
    public WritesComponentAttribute(Type componentType) => ComponentType = componentType;
}

// Usage example
[ReadsComponent(typeof(Position))]
[ReadsComponent(typeof(Velocity))]
[WritesComponent(typeof(Position))]
public class MovementSystem : ParallelSystemBase
{
    // Metadata automatically extracted via reflection
}
```

#### Integration Points

1. **SystemDependencyGraph**: Extract metadata via reflection on registration
2. **ParallelSystemManager**: Cache extracted metadata to avoid repeated reflection
3. **Validation**: Warn if attributes don't match actual component access

**Priority:** **LOW** (Developer experience, but current approach works)

---

### 6. Custom Memory Allocators

**Purpose:** Fine-tune memory allocation strategies per component type for optimal cache locality.

#### Architecture Decision Record (ADR)

**Problem:** Arch.Core uses default memory layout. High-frequency components (e.g., `Position`, `Velocity`) could benefit from better cache locality.

**Solution:** Custom archetype storage with contiguous memory layout for hot components.

**Alternatives Considered:**
- **SoA (Struct of Arrays):** Arch already does this
- **Custom Allocators:** Requires deep Arch integration
- **Memory Pools:** Only helps with allocation, not layout

**Decision:** **DEFER** - Arch.Core already provides excellent memory layout. Custom allocators require invasive changes.

**Priority:** **DEFERRED** (Diminishing returns, high complexity)

---

## Integration Strategy

### Phase 4A: CommandBuffer (Weeks 1-2)

1. Implement `CommandBuffer` core system
2. Update `ParallelSystemManager` to create/inject/playback buffers
3. Convert critical systems (`MovementSystem`, `CollisionSystem`) to use CommandBuffer
4. Add integration tests for parallel safety

### Phase 4B: Advanced Caching (Week 3)

1. Extend `QueryCache` with result caching
2. Integrate with Arch's archetype versioning
3. Update `SpatialHashSystem` and `PathfindingSystem` to use cached results
4. Benchmark performance improvements

### Phase 4C: System Groups (Week 4)

1. Implement `SystemGroup` abstraction
2. Update `SystemManager` registration API
3. Migrate existing systems to predefined groups
4. Document group conventions

### Phase 4D: Component Pooling (Week 5)

1. Implement `ComponentBufferPool`
2. Identify components with large internal arrays
3. Update components to use buffer pool
4. Integrate with `EntityPool` cleanup

### Phase 4E: Dependency Attributes (Week 6 - Optional)

1. Implement attribute-based metadata
2. Add reflection cache
3. Provide migration path from manual metadata
4. Add validation tooling

---

## Migration Strategy

### Backward Compatibility

All Phase 4 features are **opt-in** and maintain full backward compatibility:

- Existing systems continue to work without changes
- `CommandBuffer` usage is optional (direct modifications still work in sequential mode)
- System groups are optional (systems without groups use default priority)
- Advanced caching requires explicit opt-in

### Migration Path

```csharp
// Phase 3 (Current)
public class MovementSystem : ParallelSystemBase
{
    public override void Update(World world, float deltaTime)
    {
        var query = world.Query(QueryCache.Get<Position, Velocity>());
        query.ForEach((ref Position pos, ref Velocity vel) =>
        {
            pos.X += vel.X * deltaTime; // Direct modification
        });
    }
}

// Phase 4A (CommandBuffer)
public class MovementSystem : ParallelSystemBase
{
    private CommandBuffer _commands;

    public override void Update(World world, float deltaTime)
    {
        _commands = GetCommandBuffer(); // Injected by ParallelSystemManager

        var query = world.Query(QueryCache.Get<Position, Velocity>());
        query.ForEach((Entity entity, ref Position pos, ref Velocity vel) =>
        {
            var newPos = pos;
            newPos.X += vel.X * deltaTime;
            _commands.SetComponent(entity, newPos); // Deferred modification
        });
    }
}

// Phase 4B (Advanced Caching)
public class MovementSystem : ParallelSystemBase
{
    private CommandBuffer _commands;

    public override void Update(World world, float deltaTime)
    {
        _commands = GetCommandBuffer();

        // Use cached results for stable entity set
        var entities = AdvancedQueryCache.GetCachedResults(
            world,
            QueryCache.Get<Position, Velocity>(),
            "movement_system_query"
        );

        foreach (var entity in entities)
        {
            ref var pos = ref entity.Get<Position>();
            ref var vel = ref entity.Get<Velocity>();

            var newPos = pos;
            newPos.X += vel.X * deltaTime;
            _commands.SetComponent(entity, newPos);
        }
    }
}

// Phase 4C (System Groups)
// Registration changes only
systemManager.RegisterSystem<MovementSystem>(SystemGroups.Update);
```

---

## Performance Impact Estimates

### CommandBuffer
- **CPU:** +5-10% (recording overhead) / +30-50% (enables aggressive parallelization)
- **Memory:** +1-2 KB per frame
- **Net Gain:** **+20-40% throughput** due to better parallelism

### Advanced Caching
- **CPU:** -20-40% for read-heavy systems (tile rendering, pathfinding)
- **Memory:** +200 bytes per cached query (~2-5 KB total)
- **Net Gain:** **+15-25% throughput** for typical workloads

### Component Pooling
- **CPU:** -5-10% GC pauses
- **Memory:** +50-100 KB for buffer pools
- **Net Gain:** **+5-10% smoothness** (reduced frame time variance)

### System Groups
- **CPU:** Neutral (execution order unchanged)
- **Developer Time:** -30% (easier to reason about system ordering)
- **Net Gain:** **Maintainability improvement**

### Combined Impact
**Estimated Total Gain:** **+30-50% throughput**, **-40% GC pressure**, **+50% developer productivity**

---

## Risk Assessment

| Risk | Likelihood | Impact | Mitigation |
|------|-----------|--------|------------|
| CommandBuffer adds too much overhead | Low | Medium | Profile early, make recording zero-alloc |
| Result caching invalidation bugs | Medium | High | Extensive testing, conservative invalidation |
| Component pooling leaks memory | Low | Medium | Limit pool sizes, add telemetry |
| System groups increase complexity | Low | Low | Keep API minimal, document conventions |
| Parallel bugs from CommandBuffer misuse | Medium | High | Strong typing, runtime validation, tests |

---

## Success Metrics

### Performance
- [ ] Achieve 30%+ throughput improvement in parallel workloads
- [ ] Reduce GC allocations by 40%+ in entity-heavy scenarios
- [ ] Maintain sub-16ms frame times for 1000+ entities

### Code Quality
- [ ] Zero breaking changes to existing systems
- [ ] 90%+ test coverage for new features
- [ ] Clear migration guide and examples

### Developer Experience
- [ ] Reduce system boilerplate by 30%+
- [ ] Make parallel safety obvious and enforceable
- [ ] Improve debugging with CommandBuffer tracing

---

## Open Questions

1. **Should CommandBuffer be per-system or per-stage?**
   - **Recommendation:** Per-stage. Simpler to implement, adequate performance.

2. **How to handle CommandBuffer ordering (e.g., create then modify same entity)?**
   - **Recommendation:** Replay in recording order, document ordering guarantees.

3. **Should result caching be automatic or opt-in?**
   - **Recommendation:** Opt-in. Systems with dynamic queries shouldn't pay cache cost.

4. **Do we need system group hierarchies (groups within groups)?**
   - **Recommendation:** Start flat, add nesting only if needed.

5. **Should we integrate with Arch.CommandBuffer when available?**
   - **Recommendation:** Yes. Plan for migration path, keep API similar.

---

## References

### Internal Documentation
- `/Users/ntomsic/Documents/PokeSharp/PokeSharp.Core/Systems/SystemManager.cs`
- `/Users/ntomsic/Documents/PokeSharp/PokeSharp.Core/Parallel/ParallelSystemManager.cs`
- `/Users/ntomsic/Documents/PokeSharp/PokeSharp.Core/Parallel/SystemDependencyGraph.cs`
- `/Users/ntomsic/Documents/PokeSharp/PokeSharp.Core/Pooling/EntityPoolManager.cs`
- `/Users/ntomsic/Documents/PokeSharp/PokeSharp.Core/Systems/QueryCache.cs`

### External References
- Arch.Core ECS: https://github.com/genaray/Arch
- Unity DOTS System Groups: https://docs.unity3d.com/Packages/com.unity.entities@1.0/manual/systems-systemgroups.html
- C# CommandBuffer Pattern: Martin Fowler - Patterns of Enterprise Application Architecture

---

## Next Steps

1. **Review:** Circulate this document to team for feedback
2. **Prototype:** Build proof-of-concept CommandBuffer with integration tests
3. **Benchmark:** Compare Phase 3 vs Phase 4A performance on representative workload
4. **Decide:** Go/no-go decision based on benchmark results
5. **Implement:** Follow phased rollout (4A → 4B → 4C → 4D)

---

**Document Version:** 1.0
**Last Updated:** 2025-11-09
**Status:** Ready for Review
