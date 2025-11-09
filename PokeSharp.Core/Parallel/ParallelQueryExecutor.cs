using System.Buffers;
using System.Collections.Concurrent;
using System.Diagnostics;
using System.Threading.Tasks;
using Arch.Core;
using Arch.Core.Extensions;

namespace PokeSharp.Core.Parallel;

// Custom delegates that support ref parameters
public delegate void EntityAction<T>(Entity entity, ref T component) where T : struct;
public delegate void EntityAction<T1, T2>(Entity entity, ref T1 c1, ref T2 c2) where T1 : struct where T2 : struct;
public delegate void EntityAction<T1, T2, T3>(Entity entity, ref T1 c1, ref T2 c2, ref T3 c3) where T1 : struct where T2 : struct where T3 : struct;
public delegate void EntityAction<T1, T2, T3, T4>(Entity entity, ref T1 c1, ref T2 c2, ref T3 c3, ref T4 c4) where T1 : struct where T2 : struct where T3 : struct where T4 : struct;
public delegate TResult EntityFunc<T, TResult>(Entity entity, ref T component) where T : struct;

/// <summary>
///     Executes ECS queries in parallel across multiple threads.
///     Simple wrapper around Parallel.ForEach for ECS queries.
/// </summary>
public class ParallelQueryExecutor
{
    private readonly World _world;
    private readonly int _maxDegreeOfParallelism;
    private readonly ParallelExecutionStats _stats;
    private readonly object _statsLock = new();

    /// <summary>
    ///     Creates a new parallel query executor.
    /// </summary>
    /// <param name="world">The ECS world to execute queries on.</param>
    /// <param name="maxThreads">Maximum threads to use (null = ProcessorCount).</param>
    public ParallelQueryExecutor(World world, int? maxThreads = null)
    {
        _world = world ?? throw new ArgumentNullException(nameof(world));
        _maxDegreeOfParallelism = maxThreads ?? Environment.ProcessorCount;
        _stats = new ParallelExecutionStats();
    }

    /// <summary>
    ///     Execute query in parallel chunks with a single component.
    ///     Uses ArrayPool for zero-allocation entity collection.
    /// </summary>
    public void ExecuteParallel<T>(
        in QueryDescription query,
        EntityAction<T> action
    ) where T : struct
    {
        ArgumentNullException.ThrowIfNull(action);

        var stopwatch = Stopwatch.StartNew();

        // Phase 4: Use ArrayPool to eliminate List<Entity> allocations
        // Get entity count first
        var entityCount = _world.CountEntities(in query);
        if (entityCount == 0)
        {
            stopwatch.Stop();
            RecordExecution(0, stopwatch.Elapsed.TotalMilliseconds);
            return;
        }

        // Rent array from pool (zero allocation)
        var entityArray = ArrayPool<Entity>.Shared.Rent(entityCount);
        try
        {
            var index = 0;
            _world.Query(in query, (Entity entity) => entityArray[index++] = entity);

            // Process in parallel (array is safe because we only access [0..entityCount))
            var options = new ParallelOptions { MaxDegreeOfParallelism = _maxDegreeOfParallelism };
            var count = entityCount; // Capture for lambda
            System.Threading.Tasks.Parallel.For(0, count, options, i =>
            {
                ref var component = ref entityArray[i].Get<T>();
                action(entityArray[i], ref component);
            });

            stopwatch.Stop();
            RecordExecution(entityCount, stopwatch.Elapsed.TotalMilliseconds);
        }
        finally
        {
            // Always return array to pool
            ArrayPool<Entity>.Shared.Return(entityArray);
        }
    }

    /// <summary>
    ///     Execute query in parallel with two components.
    ///     Uses ArrayPool for zero-allocation entity collection.
    /// </summary>
    public void ExecuteParallel<T1, T2>(
        in QueryDescription query,
        EntityAction<T1, T2> action
    ) where T1 : struct where T2 : struct
    {
        ArgumentNullException.ThrowIfNull(action);

        var stopwatch = Stopwatch.StartNew();

        var entityCount = _world.CountEntities(in query);
        if (entityCount == 0)
        {
            stopwatch.Stop();
            RecordExecution(0, stopwatch.Elapsed.TotalMilliseconds);
            return;
        }

        var entityArray = ArrayPool<Entity>.Shared.Rent(entityCount);
        try
        {
            var index = 0;
            _world.Query(in query, (Entity entity) => entityArray[index++] = entity);

            var options = new ParallelOptions { MaxDegreeOfParallelism = _maxDegreeOfParallelism };
            var count = entityCount;
            System.Threading.Tasks.Parallel.For(0, count, options, i =>
            {
                ref var c1 = ref entityArray[i].Get<T1>();
                ref var c2 = ref entityArray[i].Get<T2>();
                action(entityArray[i], ref c1, ref c2);
            });

            stopwatch.Stop();
            RecordExecution(entityCount, stopwatch.Elapsed.TotalMilliseconds);
        }
        finally
        {
            ArrayPool<Entity>.Shared.Return(entityArray);
        }
    }

    /// <summary>
    ///     Execute query in parallel with three components.
    ///     Uses ArrayPool for zero-allocation entity collection.
    /// </summary>
    public void ExecuteParallel<T1, T2, T3>(
        in QueryDescription query,
        EntityAction<T1, T2, T3> action
    ) where T1 : struct where T2 : struct where T3 : struct
    {
        ArgumentNullException.ThrowIfNull(action);

        var stopwatch = Stopwatch.StartNew();

        var entityCount = _world.CountEntities(in query);
        if (entityCount == 0)
        {
            stopwatch.Stop();
            RecordExecution(0, stopwatch.Elapsed.TotalMilliseconds);
            return;
        }

        var entityArray = ArrayPool<Entity>.Shared.Rent(entityCount);
        try
        {
            var index = 0;
            _world.Query(in query, (Entity entity) => entityArray[index++] = entity);

            var options = new ParallelOptions { MaxDegreeOfParallelism = _maxDegreeOfParallelism };
            var count = entityCount;
            System.Threading.Tasks.Parallel.For(0, count, options, i =>
            {
                ref var c1 = ref entityArray[i].Get<T1>();
                ref var c2 = ref entityArray[i].Get<T2>();
                ref var c3 = ref entityArray[i].Get<T3>();
                action(entityArray[i], ref c1, ref c2, ref c3);
            });

            stopwatch.Stop();
            RecordExecution(entityCount, stopwatch.Elapsed.TotalMilliseconds);
        }
        finally
        {
            ArrayPool<Entity>.Shared.Return(entityArray);
        }
    }

    /// <summary>
    ///     Execute query in parallel with four components.
    ///     Uses ArrayPool for zero-allocation entity collection.
    /// </summary>
    public void ExecuteParallel<T1, T2, T3, T4>(
        in QueryDescription query,
        EntityAction<T1, T2, T3, T4> action
    ) where T1 : struct where T2 : struct where T3 : struct where T4 : struct
    {
        ArgumentNullException.ThrowIfNull(action);

        var stopwatch = Stopwatch.StartNew();

        var entityCount = _world.CountEntities(in query);
        if (entityCount == 0)
        {
            stopwatch.Stop();
            RecordExecution(0, stopwatch.Elapsed.TotalMilliseconds);
            return;
        }

        var entityArray = ArrayPool<Entity>.Shared.Rent(entityCount);
        try
        {
            var index = 0;
            _world.Query(in query, (Entity entity) => entityArray[index++] = entity);

            var options = new ParallelOptions { MaxDegreeOfParallelism = _maxDegreeOfParallelism };
            var count = entityCount;
            System.Threading.Tasks.Parallel.For(0, count, options, i =>
            {
                ref var c1 = ref entityArray[i].Get<T1>();
                ref var c2 = ref entityArray[i].Get<T2>();
                ref var c3 = ref entityArray[i].Get<T3>();
                ref var c4 = ref entityArray[i].Get<T4>();
                action(entityArray[i], ref c1, ref c2, ref c3, ref c4);
            });

            stopwatch.Stop();
            RecordExecution(entityCount, stopwatch.Elapsed.TotalMilliseconds);
        }
        finally
        {
            ArrayPool<Entity>.Shared.Return(entityArray);
        }
    }

    /// <summary>
    ///     Execute with result aggregation (map-reduce pattern).
    ///     Uses ArrayPool for zero-allocation entity collection.
    /// </summary>
    public TResult ExecuteParallelWithReduce<T, TResult>(
        in QueryDescription query,
        EntityFunc<T, TResult> map,
        Func<TResult, TResult, TResult> reduce
    ) where T : struct
    {
        ArgumentNullException.ThrowIfNull(map);
        ArgumentNullException.ThrowIfNull(reduce);

        var stopwatch = Stopwatch.StartNew();
        var results = new ConcurrentBag<TResult>();

        var entityCount = _world.CountEntities(in query);
        if (entityCount == 0)
        {
            stopwatch.Stop();
            RecordExecution(0, stopwatch.Elapsed.TotalMilliseconds);
            return default!;
        }

        var entityArray = ArrayPool<Entity>.Shared.Rent(entityCount);
        try
        {
            var index = 0;
            _world.Query(in query, (Entity entity) => entityArray[index++] = entity);

            var options = new ParallelOptions { MaxDegreeOfParallelism = _maxDegreeOfParallelism };
            var count = entityCount;
            System.Threading.Tasks.Parallel.For(0, count, options, i =>
            {
                ref var component = ref entityArray[i].Get<T>();
                var result = map(entityArray[i], ref component);
                results.Add(result);
            });

            stopwatch.Stop();
            RecordExecution(entityCount, stopwatch.Elapsed.TotalMilliseconds);

            // Reduce results
            return results.Aggregate(reduce);
        }
        finally
        {
            ArrayPool<Entity>.Shared.Return(entityArray);
        }
    }

    /// <summary>
    ///     Get performance metrics for parallel execution.
    /// </summary>
    public ParallelExecutionStats GetStats()
    {
        lock (_statsLock)
        {
            return _stats.Clone();
        }
    }

    /// <summary>
    ///     Reset performance statistics.
    /// </summary>
    public void ResetStats()
    {
        lock (_statsLock)
        {
            _stats.Reset();
        }
    }

    private void RecordExecution(int entityCount, double milliseconds)
    {
        lock (_statsLock)
        {
            _stats.TotalParallelQueries++;
            _stats.TotalEntitiesProcessed += entityCount;
            _stats.TotalExecutionTimeMs += milliseconds;
            _stats.AverageThreadsUsed = _maxDegreeOfParallelism;
        }
    }
}

/// <summary>
///     Performance statistics for parallel query execution.
/// </summary>
public class ParallelExecutionStats
{
    public long TotalParallelQueries { get; set; }
    public long TotalEntitiesProcessed { get; set; }
    public double TotalExecutionTimeMs { get; set; }
    public int AverageThreadsUsed { get; set; }

    /// <summary>
    ///     Average entities processed per query.
    /// </summary>
    public double AverageEntitiesPerQuery =>
        TotalParallelQueries > 0 ? (double)TotalEntitiesProcessed / TotalParallelQueries : 0;

    /// <summary>
    ///     Average execution time per query in milliseconds.
    /// </summary>
    public double AverageExecutionTimeMs =>
        TotalParallelQueries > 0 ? TotalExecutionTimeMs / TotalParallelQueries : 0;

    /// <summary>
    ///     Estimated speedup vs sequential (theoretical based on thread count).
    /// </summary>
    public double EstimatedSpeedup => AverageThreadsUsed * 0.7; // Amdahl's law approximation

    public ParallelExecutionStats Clone()
    {
        return new ParallelExecutionStats
        {
            TotalParallelQueries = TotalParallelQueries,
            TotalEntitiesProcessed = TotalEntitiesProcessed,
            TotalExecutionTimeMs = TotalExecutionTimeMs,
            AverageThreadsUsed = AverageThreadsUsed
        };
    }

    public void Reset()
    {
        TotalParallelQueries = 0;
        TotalEntitiesProcessed = 0;
        TotalExecutionTimeMs = 0;
        AverageThreadsUsed = 0;
    }
}
