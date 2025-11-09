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
    /// </summary>
    public void ExecuteParallel<T>(
        in QueryDescription query,
        EntityAction<T> action
    ) where T : struct
    {
        ArgumentNullException.ThrowIfNull(action);

        var stopwatch = Stopwatch.StartNew();
        var entities = new List<Entity>();

        // Collect entities first (Arch queries can't be modified during iteration)
        _world.Query(in query, (Entity entity) => entities.Add(entity));

        // Process in parallel
        var options = new ParallelOptions { MaxDegreeOfParallelism = _maxDegreeOfParallelism };
        System.Threading.Tasks.Parallel.ForEach(entities, options, entity =>
        {
            ref var component = ref entity.Get<T>();
            action(entity, ref component);
        });

        stopwatch.Stop();
        RecordExecution(entities.Count, stopwatch.Elapsed.TotalMilliseconds);
    }

    /// <summary>
    ///     Execute query in parallel with two components.
    /// </summary>
    public void ExecuteParallel<T1, T2>(
        in QueryDescription query,
        EntityAction<T1, T2> action
    ) where T1 : struct where T2 : struct
    {
        ArgumentNullException.ThrowIfNull(action);

        var stopwatch = Stopwatch.StartNew();
        var entities = new List<Entity>();

        _world.Query(in query, (Entity entity) => entities.Add(entity));

        var options = new ParallelOptions { MaxDegreeOfParallelism = _maxDegreeOfParallelism };
        System.Threading.Tasks.Parallel.ForEach(entities, options, entity =>
        {
            ref var c1 = ref entity.Get<T1>();
            ref var c2 = ref entity.Get<T2>();
            action(entity, ref c1, ref c2);
        });

        stopwatch.Stop();
        RecordExecution(entities.Count, stopwatch.Elapsed.TotalMilliseconds);
    }

    /// <summary>
    ///     Execute query in parallel with three components.
    /// </summary>
    public void ExecuteParallel<T1, T2, T3>(
        in QueryDescription query,
        EntityAction<T1, T2, T3> action
    ) where T1 : struct where T2 : struct where T3 : struct
    {
        ArgumentNullException.ThrowIfNull(action);

        var stopwatch = Stopwatch.StartNew();
        var entities = new List<Entity>();

        _world.Query(in query, (Entity entity) => entities.Add(entity));

        var options = new ParallelOptions { MaxDegreeOfParallelism = _maxDegreeOfParallelism };
        System.Threading.Tasks.Parallel.ForEach(entities, options, entity =>
        {
            ref var c1 = ref entity.Get<T1>();
            ref var c2 = ref entity.Get<T2>();
            ref var c3 = ref entity.Get<T3>();
            action(entity, ref c1, ref c2, ref c3);
        });

        stopwatch.Stop();
        RecordExecution(entities.Count, stopwatch.Elapsed.TotalMilliseconds);
    }

    /// <summary>
    ///     Execute query in parallel with four components.
    /// </summary>
    public void ExecuteParallel<T1, T2, T3, T4>(
        in QueryDescription query,
        EntityAction<T1, T2, T3, T4> action
    ) where T1 : struct where T2 : struct where T3 : struct where T4 : struct
    {
        ArgumentNullException.ThrowIfNull(action);

        var stopwatch = Stopwatch.StartNew();
        var entities = new List<Entity>();

        _world.Query(in query, (Entity entity) => entities.Add(entity));

        var options = new ParallelOptions { MaxDegreeOfParallelism = _maxDegreeOfParallelism };
        System.Threading.Tasks.Parallel.ForEach(entities, options, entity =>
        {
            ref var c1 = ref entity.Get<T1>();
            ref var c2 = ref entity.Get<T2>();
            ref var c3 = ref entity.Get<T3>();
            ref var c4 = ref entity.Get<T4>();
            action(entity, ref c1, ref c2, ref c3, ref c4);
        });

        stopwatch.Stop();
        RecordExecution(entities.Count, stopwatch.Elapsed.TotalMilliseconds);
    }

    /// <summary>
    ///     Execute with result aggregation (map-reduce pattern).
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
        var entities = new List<Entity>();

        _world.Query(in query, (Entity entity) => entities.Add(entity));

        var options = new ParallelOptions { MaxDegreeOfParallelism = _maxDegreeOfParallelism };
        System.Threading.Tasks.Parallel.ForEach(entities, options, entity =>
        {
            ref var component = ref entity.Get<T>();
            var result = map(entity, ref component);
            results.Add(result);
        });

        stopwatch.Stop();
        RecordExecution(entities.Count, stopwatch.Elapsed.TotalMilliseconds);

        // Reduce results
        return results.Aggregate(reduce);
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
