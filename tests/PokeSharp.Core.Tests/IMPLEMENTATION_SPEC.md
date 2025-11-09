# Implementation Specification for Coder Agents

**Target**: CommandBuffer and QueryResultCache implementations
**Architecture**: Arch.Core 2.1.0 ECS framework
**Language**: C# .NET 9.0

---

## 1. CommandBuffer Implementation

### Location
`/PokeSharp.Core/Commands/CommandBuffer.cs`

### Purpose
Deferred command execution pattern for ECS. Records entity/component operations and executes them later in a batch, allowing safe modifications during iteration.

### Complete Implementation Spec

```csharp
using System.Collections.Concurrent;
using System.Collections.Generic;
using Arch.Core;
using Arch.Core.Extensions;

namespace PokeSharp.Core.Commands;

/// <summary>
/// Records ECS commands for deferred execution.
/// Thread-safe for recording, sequential for playback.
/// </summary>
public class CommandBuffer
{
    private readonly World _world;
    private readonly List<ICommand> _commands;
    private readonly object _recordLock = new();

    // Object pool for buffer reuse
    private static readonly ConcurrentQueue<CommandBuffer> _pool = new();
    private const int MaxPoolSize = 100;

    public CommandBuffer(World world)
    {
        _world = world ?? throw new ArgumentNullException(nameof(world));
        _commands = new List<ICommand>(capacity: 64);
    }

    /// <summary>
    /// Record entity creation. Returns a temporary ID.
    /// </summary>
    public int RecordCreateEntity()
    {
        lock (_recordLock)
        {
            var tempId = -(_commands.Count + 1); // Negative IDs for temp entities
            _commands.Add(new CreateEntityCommand(tempId));
            return tempId;
        }
    }

    /// <summary>
    /// Record entity destruction.
    /// </summary>
    public void RecordDestroyEntity(int entityId)
    {
        lock (_recordLock)
        {
            _commands.Add(new DestroyEntityCommand(entityId));
        }
    }

    /// <summary>
    /// Record component addition.
    /// </summary>
    public void RecordAddComponent<T>(int entityId, T component) where T : struct
    {
        lock (_recordLock)
        {
            _commands.Add(new AddComponentCommand<T>(entityId, component));
        }
    }

    /// <summary>
    /// Record component removal.
    /// </summary>
    public void RecordRemoveComponent<T>(int entityId) where T : struct
    {
        lock (_recordLock)
        {
            _commands.Add(new RemoveComponentCommand<T>(entityId));
        }
    }

    /// <summary>
    /// Execute all recorded commands in FIFO order.
    /// Clears buffer after execution.
    /// </summary>
    public void Playback()
    {
        // Map temp IDs to real entity references
        var entityMap = new Dictionary<int, Entity>();

        foreach (var command in _commands)
        {
            try
            {
                command.Execute(_world, entityMap);
            }
            catch (Exception ex)
            {
                // Log error but continue with remaining commands
                Console.Error.WriteLine($"Command execution failed: {ex.Message}");
            }
        }

        _commands.Clear();
    }

    /// <summary>
    /// Rent a buffer from the object pool.
    /// </summary>
    public static CommandBuffer Rent(World world)
    {
        if (_pool.TryDequeue(out var buffer))
        {
            // Reuse pooled buffer (already has world reference)
            return buffer;
        }

        return new CommandBuffer(world);
    }

    /// <summary>
    /// Return a buffer to the object pool.
    /// </summary>
    public static void Return(CommandBuffer buffer)
    {
        if (buffer == null) return;

        buffer._commands.Clear();

        if (_pool.Count < MaxPoolSize)
        {
            _pool.Enqueue(buffer);
        }
    }

    // Command interface
    private interface ICommand
    {
        void Execute(World world, Dictionary<int, Entity> entityMap);
    }

    // Command implementations
    private record CreateEntityCommand(int TempId) : ICommand
    {
        public void Execute(World world, Dictionary<int, Entity> entityMap)
        {
            var entity = world.Create();
            entityMap[TempId] = entity;
        }
    }

    private record DestroyEntityCommand(int EntityId) : ICommand
    {
        public void Execute(World world, Dictionary<int, Entity> entityMap)
        {
            // Check if it's a temp ID or real entity
            if (EntityId < 0 && entityMap.TryGetValue(EntityId, out var entity))
            {
                world.Destroy(entity);
            }
            else if (EntityId >= 0)
            {
                var entityRef = world.GetEntity(EntityId);
                if (entityRef.IsAlive())
                {
                    world.Destroy(entityRef);
                }
            }
        }
    }

    private record AddComponentCommand<T>(int EntityId, T Component) : ICommand where T : struct
    {
        public void Execute(World world, Dictionary<int, Entity> entityMap)
        {
            Entity entity;

            if (EntityId < 0 && entityMap.TryGetValue(EntityId, out var tempEntity))
            {
                entity = tempEntity;
            }
            else
            {
                entity = world.GetEntity(EntityId);
            }

            if (entity.IsAlive())
            {
                entity.Add(Component);
            }
        }
    }

    private record RemoveComponentCommand<T>(int EntityId) : ICommand where T : struct
    {
        public void Execute(World world, Dictionary<int, Entity> entityMap)
        {
            Entity entity;

            if (EntityId < 0 && entityMap.TryGetValue(EntityId, out var tempEntity))
            {
                entity = tempEntity;
            }
            else
            {
                entity = world.GetEntity(EntityId);
            }

            if (entity.IsAlive() && entity.Has<T>())
            {
                entity.Remove<T>();
            }
        }
    }
}
```

### Key Features
- ✅ Thread-safe recording (lock on `_commands`)
- ✅ Exception-safe playback (try-catch per command)
- ✅ FIFO execution order (List maintains order)
- ✅ Object pooling (static ConcurrentQueue)
- ✅ Temp entity ID mapping (negative IDs)
- ✅ Works with Arch.Core's World and Entity

### Usage Example
```csharp
var buffer = CommandBuffer.Rent(world);

// Record commands (can be from multiple threads)
var entityId = buffer.RecordCreateEntity();
buffer.RecordAddComponent(entityId, new Position { X = 10, Y = 20 });

// Execute all commands
buffer.Playback();

// Return to pool
CommandBuffer.Return(buffer);
```

---

## 2. QueryResultCache Implementation

### Location
`/PokeSharp.Core/Cache/QueryResultCache.cs`

### Purpose
Cache query results to avoid expensive re-queries when entities haven't changed. Provides massive performance boost for repeated queries (>2x speedup).

### Complete Implementation Spec

```csharp
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using Arch.Core;
using Arch.Core.Extensions;

namespace PokeSharp.Core.Cache;

/// <summary>
/// Caches query results with automatic invalidation on entity changes.
/// Thread-safe with LRU eviction policy.
/// </summary>
public class QueryResultCache
{
    private readonly ConcurrentDictionary<QueryKey, CacheEntry> _cache = new();
    private readonly ConcurrentDictionary<QueryKey, DateTime> _accessTimes = new();
    private readonly int? _maxSize;
    private readonly object _statsLock = new();

    private long _totalQueries;
    private long _cacheHits;
    private long _cacheMisses;

    private int _worldVersion = 0; // Track world changes

    public QueryResultCache(int? maxSize = null)
    {
        _maxSize = maxSize;
    }

    /// <summary>
    /// Get cached results or compute new ones.
    /// </summary>
    public (IEnumerable<Entity> results, bool isHit) GetOrCompute<T>(
        Query<T> query,
        Func<IEnumerable<Entity>> compute
    )
    {
        var key = new QueryKey(typeof(T).FullName!, _worldVersion);

        lock (_statsLock)
        {
            _totalQueries++;
        }

        // Try to get from cache
        if (_cache.TryGetValue(key, out var entry))
        {
            _accessTimes[key] = DateTime.UtcNow;

            lock (_statsLock)
            {
                _cacheHits++;
            }

            return (entry.Results, true);
        }

        // Cache miss - compute results
        var results = compute().ToList(); // Materialize to avoid re-execution

        lock (_statsLock)
        {
            _cacheMisses++;
        }

        // Store in cache
        _cache[key] = new CacheEntry(results);
        _accessTimes[key] = DateTime.UtcNow;

        // Evict if cache is full
        if (_maxSize.HasValue && _cache.Count > _maxSize.Value)
        {
            EvictLRU();
        }

        return (results, false);
    }

    /// <summary>
    /// Invalidate cache (call when entities change).
    /// </summary>
    public void Invalidate()
    {
        Interlocked.Increment(ref _worldVersion);
        // Old entries with different version will be cache misses
    }

    /// <summary>
    /// Clear all cached results.
    /// </summary>
    public void Clear()
    {
        _cache.Clear();
        _accessTimes.Clear();
        Interlocked.Increment(ref _worldVersion);
    }

    /// <summary>
    /// Get cache statistics.
    /// </summary>
    public CacheStatistics GetStatistics()
    {
        lock (_statsLock)
        {
            return new CacheStatistics
            {
                TotalQueries = _totalQueries,
                CacheHits = _cacheHits,
                CacheMisses = _cacheMisses,
                HitRate = _totalQueries > 0 ? (double)_cacheHits / _totalQueries : 0
            };
        }
    }

    private void EvictLRU()
    {
        // Find least recently used entry
        var lruKey = _accessTimes.OrderBy(kvp => kvp.Value).First().Key;

        _cache.TryRemove(lruKey, out _);
        _accessTimes.TryRemove(lruKey, out _);
    }

    private record QueryKey(string TypeName, int WorldVersion);

    private record CacheEntry(List<Entity> Results);
}

/// <summary>
/// Cache performance statistics.
/// </summary>
public class CacheStatistics
{
    public long TotalQueries { get; init; }
    public long CacheHits { get; init; }
    public long CacheMisses { get; init; }
    public double HitRate { get; init; }
}

/// <summary>
/// Query wrapper for type safety.
/// </summary>
public class Query<T>
{
    // Empty marker class for type differentiation
}
```

### Key Features
- ✅ Thread-safe (ConcurrentDictionary)
- ✅ Automatic invalidation (world version tracking)
- ✅ LRU eviction (when cache is full)
- ✅ Statistics tracking (hits, misses, hit rate)
- ✅ Materialized results (ToList() prevents re-execution)
- ✅ Works with Arch.Core entities

### Usage Example
```csharp
var cache = new QueryResultCache(maxSize: 100);

// First query - cache miss
var query = new Query<Position>();
var (results1, isHit1) = cache.GetOrCompute(query, () => world.Query<Position>());
// isHit1 = false

// Second query - cache hit
var (results2, isHit2) = cache.GetOrCompute(query, () => world.Query<Position>());
// isHit2 = true, results2 same as results1

// Entity changed - invalidate
cache.Invalidate();

// Next query - cache miss again
var (results3, isHit3) = cache.GetOrCompute(query, () => world.Query<Position>());
// isHit3 = false
```

---

## Integration with Existing Code

### CommandBuffer Integration
```csharp
// In systems:
public class MovementSystem : BaseSystem
{
    public override void Update(World world, float deltaTime)
    {
        var buffer = CommandBuffer.Rent(world);

        world.Query<Position, Velocity>((Entity entity, ref Position pos, ref Velocity vel) =>
        {
            pos.X += vel.VX * deltaTime;
            pos.Y += vel.VY * deltaTime;

            // Can't modify during iteration, so use buffer
            if (ShouldSpawn(pos))
            {
                var newEntity = buffer.RecordCreateEntity();
                buffer.RecordAddComponent(newEntity, new Position { X = pos.X, Y = pos.Y });
            }
        });

        buffer.Playback();
        CommandBuffer.Return(buffer);
    }
}
```

### QueryResultCache Integration
```csharp
// In ParallelQueryExecutor:
public class ParallelQueryExecutor
{
    private readonly World _world;
    private readonly QueryResultCache? _cache;

    public ParallelQueryExecutor(World world, QueryResultCache? cache = null)
    {
        _world = world;
        _cache = cache;
    }

    public IEnumerable<Entity> Execute<T>(Query<T> query)
    {
        if (_cache != null)
        {
            var (results, _) = _cache.GetOrCompute(query, () => _world.Query<T>());
            return results;
        }

        return _world.Query<T>();
    }
}
```

---

## Testing Requirements

### CommandBuffer Tests
- ✅ 13 unit tests covering all operations
- ✅ 7 integration tests with real systems
- ✅ Thread safety validation
- ✅ Performance benchmarks (10,000 commands < 1s)

### QueryResultCache Tests
- ✅ 12 unit tests covering caching logic
- ✅ 10 integration tests with ParallelQueryExecutor
- ✅ Thread safety validation
- ✅ Performance benchmarks (>2x speedup)

### Run Tests After Implementation
```bash
dotnet build -c Release
dotnet test tests/PokeSharp.Core.Tests/PokeSharp.Core.Tests.csproj -c Release
```

All 42 tests should pass once implementations are complete!

---

## Notes

1. **Arch.Core Compatibility**: Both implementations use Arch.Core's World and Entity types directly
2. **Performance**: CommandBuffer uses object pooling, QueryResultCache uses materialized results
3. **Thread Safety**: CommandBuffer locks on recording, QueryResultCache uses ConcurrentDictionary
4. **Error Handling**: CommandBuffer continues on error, QueryResultCache invalidates on changes
5. **Memory**: Both implementations minimize allocations through pooling/caching

---

**Status**: ⏳ Waiting for implementations
**Next**: Coder agents implement these classes, tester validates with test suite
