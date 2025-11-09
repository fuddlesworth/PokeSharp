using Arch.Core;
using Arch.Core.Extensions;

namespace PokeSharp.Core.Pooling;

/// <summary>
///     Central manager for all entity pools in the game.
///     Provides unified access to multiple specialized pools and global statistics.
/// </summary>
/// <remarks>
///     Register this as a singleton service in DI container.
///     Configure pools during game initialization for optimal performance.
/// </remarks>
public class EntityPoolManager
{
    private readonly World _world;
    private readonly Dictionary<string, EntityPool> _pools;
    private readonly EntityPool _defaultPool;
    private readonly object _lock = new();

    /// <summary>
    ///     Creates a new entity pool manager for the specified world.
    /// </summary>
    /// <param name="world">ECS world to create entities in</param>
    public EntityPoolManager(World world)
    {
        ArgumentNullException.ThrowIfNull(world, nameof(world));

        _world = world;
        _pools = new Dictionary<string, EntityPool>();

        // Create default pool
        _defaultPool = new EntityPool(_world, "default", 100, 1000);
        _pools["default"] = _defaultPool;
    }

    /// <summary>
    ///     Register a named pool with custom configuration.
    /// </summary>
    /// <param name="poolName">Unique pool name</param>
    /// <param name="initialSize">Initial pool size</param>
    /// <param name="maxSize">Maximum pool size</param>
    /// <param name="warmup">Whether to pre-warm the pool</param>
    public void RegisterPool(
        string poolName,
        int initialSize = 100,
        int maxSize = 1000,
        bool warmup = true
    )
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(poolName, nameof(poolName));

        lock (_lock)
        {
            if (_pools.ContainsKey(poolName))
                throw new ArgumentException($"Pool '{poolName}' already registered");

            var pool = new EntityPool(_world, poolName, initialSize, maxSize);

            if (warmup)
                pool.Warmup(initialSize);

            _pools[poolName] = pool;
        }
    }

    /// <summary>
    ///     Register a pool using a configuration object.
    /// </summary>
    public void RegisterPool(PoolConfiguration config)
    {
        ArgumentNullException.ThrowIfNull(config, nameof(config));

        RegisterPool(config.Name, config.InitialSize, config.MaxSize, config.Warmup);
    }

    /// <summary>
    ///     Get an existing pool by name, or throw if not found.
    /// </summary>
    /// <param name="poolName">Name of the pool</param>
    /// <returns>The requested entity pool</returns>
    /// <exception cref="KeyNotFoundException">Thrown if pool doesn't exist</exception>
    public EntityPool GetPool(string poolName)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(poolName, nameof(poolName));

        lock (_lock)
        {
            if (!_pools.TryGetValue(poolName, out var pool))
                throw new KeyNotFoundException($"Pool '{poolName}' not found");

            return pool;
        }
    }

    /// <summary>
    ///     Try to get a pool by name.
    /// </summary>
    public bool TryGetPool(string poolName, out EntityPool? pool)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(poolName, nameof(poolName));

        lock (_lock)
        {
            return _pools.TryGetValue(poolName, out pool);
        }
    }

    /// <summary>
    ///     Get the default pool (always available).
    /// </summary>
    public EntityPool DefaultPool => _defaultPool;

    /// <summary>
    ///     Acquire entity from a named pool.
    /// </summary>
    /// <param name="poolName">Pool name (default: "default")</param>
    /// <returns>Entity from the pool</returns>
    public Entity Acquire(string poolName = "default")
    {
        var pool = GetPool(poolName);
        return pool.Acquire();
    }

    /// <summary>
    ///     Release entity back to its pool.
    ///     Automatically determines correct pool from entity's Pooled component.
    /// </summary>
    /// <param name="entity">Entity to release</param>
    /// <param name="poolName">Optional explicit pool name (falls back to entity's pool marker)</param>
    public void Release(Entity entity, string? poolName = null)
    {
        // If pool name not specified, try to get it from entity
        if (string.IsNullOrWhiteSpace(poolName))
        {
            if (entity.Has<Components.Pooled>())
            {
                poolName = entity.Get<Components.Pooled>().PoolName;
            }
            else
            {
                poolName = "default";
            }
        }

        var pool = GetPool(poolName);
        pool.Release(entity);
    }

    /// <summary>
    ///     Get aggregate statistics for all pools.
    /// </summary>
    public AggregatePoolStatistics GetStatistics()
    {
        lock (_lock)
        {
            var perPoolStats = new Dictionary<string, PoolStatistics>();
            var totalAvailable = 0;
            var totalActive = 0;
            var totalCreated = 0;

            foreach (var (name, pool) in _pools)
            {
                var stats = pool.GetStatistics();
                perPoolStats[name] = stats;
                totalAvailable += stats.AvailableCount;
                totalActive += stats.ActiveCount;
                totalCreated += stats.TotalCreated;
            }

            return new AggregatePoolStatistics
            {
                TotalPools = _pools.Count,
                TotalAvailable = totalAvailable,
                TotalActive = totalActive,
                TotalCreated = totalCreated,
                PerPoolStats = perPoolStats,
            };
        }
    }

    /// <summary>
    ///     Clear all pools (destroys all pooled entities).
    /// </summary>
    public void ClearAll()
    {
        lock (_lock)
        {
            foreach (var pool in _pools.Values)
                pool.Clear();
        }
    }

    /// <summary>
    ///     Get names of all registered pools.
    /// </summary>
    public IEnumerable<string> GetPoolNames()
    {
        lock (_lock)
        {
            return _pools.Keys.ToList();
        }
    }

    /// <summary>
    ///     Check if a pool with the given name exists.
    /// </summary>
    public bool HasPool(string poolName)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(poolName, nameof(poolName));

        lock (_lock)
        {
            return _pools.ContainsKey(poolName);
        }
    }
}

/// <summary>
///     Aggregate statistics across all pools in the manager.
/// </summary>
public struct AggregatePoolStatistics
{
    /// <summary>
    ///     Total number of registered pools.
    /// </summary>
    public int TotalPools;

    /// <summary>
    ///     Total entities available across all pools.
    /// </summary>
    public int TotalAvailable;

    /// <summary>
    ///     Total entities currently active (acquired) across all pools.
    /// </summary>
    public int TotalActive;

    /// <summary>
    ///     Total entities ever created across all pools.
    /// </summary>
    public int TotalCreated;

    /// <summary>
    ///     Per-pool statistics breakdown.
    /// </summary>
    public Dictionary<string, PoolStatistics> PerPoolStats;

    /// <summary>
    ///     Overall reuse rate across all pools.
    /// </summary>
    public float OverallReuseRate =>
        TotalCreated > 0 ? 1.0f - ((float)TotalCreated / (TotalActive + TotalAvailable)) : 0f;
}
