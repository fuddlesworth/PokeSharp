using Arch.Core;
using PokeSharp.Core.Parallel;
using static PokeSharp.Core.Parallel.ParallelQueryExecutor;

namespace PokeSharp.Core.Systems;

/// <summary>
///     Base class for systems that support parallel query execution.
///     Provides easy access to parallel execution capabilities.
/// </summary>
public abstract class ParallelSystemBase : SystemBase, IParallelSystemMetadata
{
    private ParallelQueryExecutor? _parallelExecutor;

    /// <summary>
    ///     Gets the parallel query executor for this system.
    /// </summary>
    protected ParallelQueryExecutor ParallelExecutor
    {
        get
        {
            if (_parallelExecutor == null)
            {
                throw new InvalidOperationException(
                    $"ParallelExecutor not initialized. Ensure {nameof(InitializeParallelExecutor)} is called."
                );
            }
            return _parallelExecutor;
        }
    }

    /// <summary>
    ///     Initialize the parallel executor with the world.
    ///     Called automatically during system initialization.
    /// </summary>
    protected void InitializeParallelExecutor(World world, int? maxThreads = null)
    {
        _parallelExecutor = new ParallelQueryExecutor(world, maxThreads);
    }

    /// <inheritdoc />
    public override void Initialize(World world)
    {
        base.Initialize(world);
        InitializeParallelExecutor(world);
    }

    /// <summary>
    ///     Override to specify which components this system reads (for parallel execution analysis).
    /// </summary>
    public virtual List<Type> GetReadComponents() => new();

    /// <summary>
    ///     Override to specify which components this system writes (for parallel execution analysis).
    /// </summary>
    public virtual List<Type> GetWriteComponents() => new();

    /// <summary>
    ///     Whether this system allows parallel execution with other systems.
    ///     Override and return false for systems with external side effects or shared state.
    /// </summary>
    public virtual bool AllowsParallelExecution => true;

    /// <summary>
    ///     Execute query in parallel with a single component.
    /// </summary>
    protected void ParallelQuery<T>(
        in QueryDescription query,
        EntityAction<T> action
    ) where T : struct
    {
        ParallelExecutor.ExecuteParallel(query, action);
    }

    /// <summary>
    ///     Execute query in parallel with two components.
    /// </summary>
    protected void ParallelQuery<T1, T2>(
        in QueryDescription query,
        EntityAction<T1, T2> action
    ) where T1 : struct where T2 : struct
    {
        ParallelExecutor.ExecuteParallel(query, action);
    }

    /// <summary>
    ///     Execute query in parallel with three components.
    /// </summary>
    protected void ParallelQuery<T1, T2, T3>(
        in QueryDescription query,
        EntityAction<T1, T2, T3> action
    ) where T1 : struct where T2 : struct where T3 : struct
    {
        ParallelExecutor.ExecuteParallel(query, action);
    }

    /// <summary>
    ///     Execute query in parallel with four components.
    /// </summary>
    protected void ParallelQuery<T1, T2, T3, T4>(
        in QueryDescription query,
        EntityAction<T1, T2, T3, T4> action
    ) where T1 : struct where T2 : struct where T3 : struct where T4 : struct
    {
        ParallelExecutor.ExecuteParallel(query, action);
    }

    /// <summary>
    ///     Execute query with map-reduce for aggregation.
    /// </summary>
    protected TResult ParallelQueryWithReduce<T, TResult>(
        in QueryDescription query,
        EntityFunc<T, TResult> map,
        Func<TResult, TResult, TResult> reduce
    ) where T : struct
    {
        return ParallelExecutor.ExecuteParallelWithReduce(query, map, reduce);
    }

    /// <summary>
    ///     Get parallel execution statistics for this system.
    /// </summary>
    protected ParallelExecutionStats GetParallelStats()
    {
        return ParallelExecutor.GetStats();
    }
}

/// <summary>
///     Helper methods for creating system metadata.
/// </summary>
public static class SystemMetadataHelper
{
    /// <summary>
    ///     Create metadata from a ParallelSystemBase instance.
    /// </summary>
    public static SystemMetadata CreateMetadata(IParallelSystemMetadata system, int priority)
    {
        return new SystemMetadata
        {
            SystemType = system.GetType(),
            ReadsComponents = system.GetReadComponents(),
            WritesComponents = system.GetWriteComponents(),
            Priority = priority,
            AllowsParallelExecution = system.AllowsParallelExecution
        };
    }

    /// <summary>
    ///     Create metadata builder for fluent API.
    /// </summary>
    public static SystemMetadataBuilder ForSystem<TSystem>() where TSystem : ISystem
    {
        return new SystemMetadataBuilder(typeof(TSystem));
    }
}

/// <summary>
///     Fluent builder for system metadata.
/// </summary>
public class SystemMetadataBuilder
{
    private readonly SystemMetadata _metadata;

    internal SystemMetadataBuilder(Type systemType)
    {
        _metadata = new SystemMetadata
        {
            SystemType = systemType
        };
    }

    /// <summary>
    ///     Add a component that the system reads.
    /// </summary>
    public SystemMetadataBuilder Reads<TComponent>() where TComponent : struct
    {
        _metadata.ReadsComponents.Add(typeof(TComponent));
        return this;
    }

    /// <summary>
    ///     Add multiple components that the system reads.
    /// </summary>
    public SystemMetadataBuilder Reads(params Type[] componentTypes)
    {
        _metadata.ReadsComponents.AddRange(componentTypes);
        return this;
    }

    /// <summary>
    ///     Add a component that the system writes.
    /// </summary>
    public SystemMetadataBuilder Writes<TComponent>() where TComponent : struct
    {
        _metadata.WritesComponents.Add(typeof(TComponent));
        return this;
    }

    /// <summary>
    ///     Add multiple components that the system writes.
    /// </summary>
    public SystemMetadataBuilder Writes(params Type[] componentTypes)
    {
        _metadata.WritesComponents.AddRange(componentTypes);
        return this;
    }

    /// <summary>
    ///     Set the system priority.
    /// </summary>
    public SystemMetadataBuilder WithPriority(int priority)
    {
        _metadata.Priority = priority;
        return this;
    }

    /// <summary>
    ///     Set whether the system allows parallel execution.
    /// </summary>
    public SystemMetadataBuilder AllowsParallel(bool allows = true)
    {
        _metadata.AllowsParallelExecution = allows;
        return this;
    }

    /// <summary>
    ///     Disallow parallel execution (shorthand for AllowsParallel(false)).
    /// </summary>
    public SystemMetadataBuilder RequiresExclusive()
    {
        _metadata.AllowsParallelExecution = false;
        return this;
    }

    /// <summary>
    ///     Set a description for the system.
    /// </summary>
    public SystemMetadataBuilder WithDescription(string description)
    {
        _metadata.Description = description;
        return this;
    }

    /// <summary>
    ///     Build the metadata.
    /// </summary>
    public SystemMetadata Build()
    {
        return _metadata;
    }
}
