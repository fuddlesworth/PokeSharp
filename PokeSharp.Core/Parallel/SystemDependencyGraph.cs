using System.Text;
using PokeSharp.Core.Systems;

namespace PokeSharp.Core.Parallel;

/// <summary>
///     Tracks dependencies between systems to enable safe parallel execution.
///     Systems that don't share component access can run in parallel.
/// </summary>
public class SystemDependencyGraph
{
    private readonly Dictionary<Type, SystemMetadata> _systemMetadata = new();
    private readonly Dictionary<Type, HashSet<Type>> _readDependencies = new();
    private readonly Dictionary<Type, HashSet<Type>> _writeDependencies = new();

    /// <summary>
    ///     Register a system with its component access patterns.
    /// </summary>
    public void RegisterSystem<TSystem>(SystemMetadata metadata) where TSystem : ISystem
    {
        ArgumentNullException.ThrowIfNull(metadata);

        var systemType = typeof(TSystem);

        if (_systemMetadata.ContainsKey(systemType))
        {
            throw new InvalidOperationException(
                $"System {systemType.Name} is already registered in dependency graph."
            );
        }

        metadata.SystemType = systemType;
        _systemMetadata[systemType] = metadata;

        // Build dependency lookups for fast access
        _readDependencies[systemType] = new HashSet<Type>(metadata.ReadsComponents);
        _writeDependencies[systemType] = new HashSet<Type>(metadata.WritesComponents);
    }

    /// <summary>
    ///     Check if two systems can run in parallel (no data conflicts).
    /// </summary>
    public bool CanRunInParallel(Type system1, Type system2)
    {
        if (!_systemMetadata.ContainsKey(system1) || !_systemMetadata.ContainsKey(system2))
        {
            return false; // Unknown systems cannot run in parallel
        }

        var metadata1 = _systemMetadata[system1];
        var metadata2 = _systemMetadata[system2];

        // If either system disallows parallel execution, they can't run in parallel
        if (!metadata1.AllowsParallelExecution || !metadata2.AllowsParallelExecution)
        {
            return false;
        }

        // Check for write-write conflicts (both write same component)
        var writeConflict = _writeDependencies[system1]
            .Intersect(_writeDependencies[system2])
            .Any();

        if (writeConflict)
        {
            return false;
        }

        // Check for read-write conflicts (one reads what the other writes)
        var readWriteConflict =
            _writeDependencies[system1].Intersect(_readDependencies[system2]).Any() ||
            _writeDependencies[system2].Intersect(_readDependencies[system1]).Any();

        return !readWriteConflict;
    }

    /// <summary>
    ///     Get all systems that can run in parallel with the given system.
    /// </summary>
    public List<Type> GetParallelizableSystems(Type system)
    {
        if (!_systemMetadata.ContainsKey(system))
        {
            return new List<Type>();
        }

        var parallelizable = new List<Type>();

        foreach (var otherSystem in _systemMetadata.Keys)
        {
            if (otherSystem == system)
                continue;

            if (CanRunInParallel(system, otherSystem))
            {
                parallelizable.Add(otherSystem);
            }
        }

        return parallelizable;
    }

    /// <summary>
    ///     Compute execution stages where systems in each stage can run in parallel.
    ///     Uses graph coloring algorithm for optimal parallelism.
    /// </summary>
    public List<List<Type>> ComputeExecutionStages(List<Type> systems)
    {
        ArgumentNullException.ThrowIfNull(systems);

        // Filter to only registered systems
        var registeredSystems = systems.Where(s => _systemMetadata.ContainsKey(s)).ToList();

        if (registeredSystems.Count == 0)
        {
            return new List<List<Type>>();
        }

        // Sort by priority first
        registeredSystems.Sort((a, b) =>
        {
            var priorityA = _systemMetadata[a].Priority;
            var priorityB = _systemMetadata[b].Priority;
            return priorityA.CompareTo(priorityB);
        });

        var stages = new List<List<Type>>();
        var assigned = new HashSet<Type>();

        // Greedy stage assignment
        while (assigned.Count < registeredSystems.Count)
        {
            var currentStage = new List<Type>();

            foreach (var system in registeredSystems)
            {
                if (assigned.Contains(system))
                    continue;

                // Check if this system can run in parallel with all systems in current stage
                var canAddToStage = currentStage.All(s => CanRunInParallel(system, s));

                if (canAddToStage)
                {
                    currentStage.Add(system);
                    assigned.Add(system);
                }
            }

            if (currentStage.Count > 0)
            {
                stages.Add(currentStage);
            }
            else
            {
                // Safety check: if we can't add any system, break to prevent infinite loop
                break;
            }
        }

        return stages;
    }

    /// <summary>
    ///     Get metadata for a specific system.
    /// </summary>
    public SystemMetadata? GetMetadata(Type systemType)
    {
        return _systemMetadata.TryGetValue(systemType, out var metadata) ? metadata : null;
    }

    /// <summary>
    ///     Get all registered system types.
    /// </summary>
    public IReadOnlyCollection<Type> GetRegisteredSystems()
    {
        return _systemMetadata.Keys;
    }

    /// <summary>
    ///     Visualize dependency graph as a text representation (for debugging).
    /// </summary>
    public string GenerateDependencyGraph()
    {
        var sb = new StringBuilder();
        sb.AppendLine("System Dependency Graph");
        sb.AppendLine("======================");
        sb.AppendLine();

        foreach (var (systemType, metadata) in _systemMetadata.OrderBy(kvp => kvp.Value.Priority))
        {
            sb.AppendLine($"System: {systemType.Name}");
            sb.AppendLine($"  Priority: {metadata.Priority}");
            sb.AppendLine($"  Parallel Allowed: {metadata.AllowsParallelExecution}");

            if (metadata.ReadsComponents.Count > 0)
            {
                sb.AppendLine($"  Reads: {string.Join(", ", metadata.ReadsComponents.Select(t => t.Name))}");
            }

            if (metadata.WritesComponents.Count > 0)
            {
                sb.AppendLine($"  Writes: {string.Join(", ", metadata.WritesComponents.Select(t => t.Name))}");
            }

            var parallelWith = GetParallelizableSystems(systemType);
            if (parallelWith.Count > 0)
            {
                sb.AppendLine($"  Can run in parallel with: {string.Join(", ", parallelWith.Select(t => t.Name))}");
            }

            sb.AppendLine();
        }

        return sb.ToString();
    }

    /// <summary>
    ///     Visualize execution stages.
    /// </summary>
    public string GenerateExecutionPlan(List<List<Type>> stages)
    {
        var sb = new StringBuilder();
        sb.AppendLine("Parallel Execution Plan");
        sb.AppendLine("=======================");
        sb.AppendLine();

        for (int i = 0; i < stages.Count; i++)
        {
            var stage = stages[i];
            sb.AppendLine($"Stage {i + 1}: ({stage.Count} systems in parallel)");

            foreach (var systemType in stage)
            {
                var metadata = _systemMetadata[systemType];
                sb.AppendLine($"  - {systemType.Name} (Priority: {metadata.Priority})");
            }

            sb.AppendLine();
        }

        return sb.ToString();
    }

    /// <summary>
    ///     Clear all registered systems.
    /// </summary>
    public void Clear()
    {
        _systemMetadata.Clear();
        _readDependencies.Clear();
        _writeDependencies.Clear();
    }
}

/// <summary>
///     Metadata describing a system's component access patterns.
/// </summary>
public class SystemMetadata
{
    /// <summary>
    ///     The system type.
    /// </summary>
    public Type SystemType { get; set; } = null!;

    /// <summary>
    ///     Components this system reads (immutable access).
    /// </summary>
    public List<Type> ReadsComponents { get; set; } = new();

    /// <summary>
    ///     Components this system writes (mutable access).
    /// </summary>
    public List<Type> WritesComponents { get; set; } = new();

    /// <summary>
    ///     Execution priority (lower values execute first).
    /// </summary>
    public int Priority { get; set; } = 100;

    /// <summary>
    ///     Whether this system allows parallel execution.
    ///     Set to false for systems with external side effects or shared state.
    /// </summary>
    public bool AllowsParallelExecution { get; set; } = true;

    /// <summary>
    ///     Optional human-readable description.
    /// </summary>
    public string Description { get; set; } = string.Empty;
}
