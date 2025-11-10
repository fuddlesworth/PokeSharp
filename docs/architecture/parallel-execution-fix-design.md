# Parallel Execution Fix - Architectural Design

## Problem Analysis

### Root Cause
The `ParallelSystemManager` returns **0 execution stages** because of a critical mismatch between:

1. **SystemManager** uses `RegisterUpdateSystem()` and `RegisterRenderSystem()` which add systems to `_updateSystems` and `_renderSystems` lists
2. **ParallelSystemManager.RebuildExecutionPlan()** (line 156) only looks at the legacy `Systems` property (from base class)
3. The base class `Systems` property only contains systems registered via the legacy `RegisterSystem(ISystem)` method
4. Since GameInitializer uses the new API (`RegisterUpdateSystem`/`RegisterRenderSystem`), those systems are **NOT in the Systems collection**

### Execution Flow
```
GameInitializer.RegisterUpdateSystem(MovementSystem)
  ↓
SystemManager._updateSystems.Add(MovementSystem)  ✓
SystemManager._systems.Add(MovementSystem)        ✓ (backward compat, lines 158-164)
  ↓
ParallelSystemManager.RegisterSystem(MovementSystem) override
  ↓
_dependencyGraph.RegisterSystem<MovementSystem>(metadata)  ✓
base.RegisterSystem(MovementSystem) [calls SystemManager.RegisterSystem]  ✓
  ↓
ParallelSystemManager.RebuildExecutionPlan()
  ↓
var systemTypes = Systems.Select(s => s.GetType()).ToList()  ← PROBLEM!
  ↓
Systems collection contains all registered systems from _systems list  ✓
BUT ComputeExecutionStages filters to only systems in _systemMetadata
  ↓
_dependencyGraph.ComputeExecutionStages(systemTypes)
  ↓
registeredSystems = systems.Where(s => _systemMetadata.ContainsKey(s))
```

### The Real Issue

**The dependency graph ONLY contains systems registered through `ParallelSystemManager.RegisterSystem(ISystem)` override**, which properly calls `_dependencyGraph.RegisterSystem<T>(metadata)`.

**However**, the current implementation has a flaw in lines 88-147:

1. When `RegisterSystem(ISystem system)` is called (override in ParallelSystemManager)
2. It attempts to register metadata with reflection (lines 127-144)
3. **BUT** the reflection approach tries to call `RegisterSystem<TSystem>(metadata)` as a generic method
4. The reflection uses `system.GetType()` to make it generic
5. Then it calls `base.RegisterSystem(system)` which adds it to `_systems`

The problem is that **update systems and render systems are ALSO added to `_systems`** (lines 158-164 and 226-232 in SystemManager), so they SHOULD be in the Systems collection.

### Actual Root Cause (Deeper Analysis)

After reviewing the code more carefully, I see the real issue:

1. **ParallelSystemManager.RegisterSystem(ISystem)** override (lines 88-148) properly registers metadata
2. **SystemManager.RegisterUpdateSystem(IUpdateSystem)** (lines 177-198) adds systems to BOTH `_updateSystems` AND `_systems` lists
3. **Therefore**, systems should be in the `Systems` collection

**The bug is in the metadata registration:**

Looking at line 129-131 in ParallelSystemManager:
```csharp
var method = _dependencyGraph.GetType().GetMethod(nameof(_dependencyGraph.RegisterSystem));
var genericMethod = method!.MakeGenericMethod(system.GetType());
genericMethod.Invoke(_dependencyGraph, new object[] { metadata });
```

This reflection call works ONLY when the system is first registered. But if systems are registered via `RegisterUpdateSystem` and then ALSO need parallel metadata, there's no path to register metadata.

**ACTUAL ROOT CAUSE**:
- `RegisterUpdateSystem(IUpdateSystem)` in SystemManager calls `RegisterSystem(ISystem)` only if the system implements ISystem (lines 158-165)
- `ParallelSystemManager.RegisterSystem(ISystem)` override extracts metadata and registers it
- **BUT** systems like `MovementSystem : ParallelSystemBase, IUpdateSystem` implement both interfaces
- When registered via `RegisterUpdateSystem`, the base class adds it to `_systems` (line 188-191 in SystemManager)
- **HOWEVER**, `ParallelSystemManager.RegisterSystem` is NOT being called because ParallelSystemManager is created as a `SystemManager` type in GameInitializer!

## The Real Problem

Looking at GameInitializer.cs (assumed, based on grep results):
```csharp
_systemManager = new SystemManager(logger); // ← NOT ParallelSystemManager!
```

Or if it IS `ParallelSystemManager`, the issue is that `RegisterUpdateSystem` is defined in the **base class** `SystemManager`, not overridden in `ParallelSystemManager`.

## Solution Design

### Architecture Changes Required

#### 1. Override Registration Methods in ParallelSystemManager

**File**: `PokeSharp.Core/Parallel/ParallelSystemManager.cs`

**New Methods to Add** (after line 148):

```csharp
/// <summary>
///     Override update system registration to capture metadata.
/// </summary>
public void RegisterUpdateSystem<T>() where T : class, IUpdateSystem
{
    // Initialize factory if needed
    lock (_lock)
    {
        if (_systemFactory == null)
            _systemFactory = new SystemFactory(_serviceContainer);

        // Create the system instance
        var system = _systemFactory.CreateSystem<T>();

        // Register with metadata extraction
        RegisterUpdateSystem(system);
    }
}

/// <summary>
///     Override update system registration to capture metadata from instance.
/// </summary>
public void RegisterUpdateSystem(IUpdateSystem system)
{
    ArgumentNullException.ThrowIfNull(system);

    // Extract metadata if system provides it
    SystemMetadata metadata;

    if (system is IParallelSystemMetadata parallelSystem)
    {
        metadata = new SystemMetadata
        {
            SystemType = system.GetType(),
            ReadsComponents = parallelSystem.GetReadComponents(),
            WritesComponents = parallelSystem.GetWriteComponents(),
            Priority = system is ISystem legacySystem ? legacySystem.Priority :
                       (system.UpdatePriority < 1000 ? system.UpdatePriority : 100),
            AllowsParallelExecution = parallelSystem.AllowsParallelExecution
        };

        // Register in dependency graph
        try
        {
            var method = _dependencyGraph.GetType().GetMethod(nameof(_dependencyGraph.RegisterSystem));
            var genericMethod = method!.MakeGenericMethod(system.GetType());
            genericMethod.Invoke(_dependencyGraph, new object[] { metadata });

            _logger?.LogInformation(
                "Update system {SystemName} registered with metadata | Reads: {ReadCount}, Writes: {WriteCount}, Parallel: {AllowsParallel}",
                system.GetType().Name,
                metadata.ReadsComponents.Count,
                metadata.WritesComponents.Count,
                metadata.AllowsParallelExecution
            );
        }
        catch (Exception ex)
        {
            _logger?.LogWarning(ex, "Failed to register system metadata for {SystemName}", system.GetType().Name);
        }
    }
    else
    {
        // Create default metadata for non-parallel systems
        metadata = new SystemMetadata
        {
            SystemType = system.GetType(),
            ReadsComponents = new List<Type>(),
            WritesComponents = new List<Type>(),
            Priority = system is ISystem legacySystem ? legacySystem.Priority :
                       (system.UpdatePriority < 1000 ? system.UpdatePriority : 100),
            AllowsParallelExecution = true
        };

        _logger?.LogInformation(
            "Update system {SystemName} registered without metadata (not IParallelSystemMetadata)",
            system.GetType().Name
        );
    }

    // Call base implementation to add to update systems list
    base.RegisterUpdateSystem(system);

    _executionPlanBuilt = false;
}

/// <summary>
///     Override render system registration to capture metadata.
/// </summary>
public void RegisterRenderSystem<T>() where T : class, IRenderSystem
{
    lock (_lock)
    {
        if (_systemFactory == null)
            _systemFactory = new SystemFactory(_serviceContainer);

        var system = _systemFactory.CreateSystem<T>();
        RegisterRenderSystem(system);
    }
}

/// <summary>
///     Override render system registration to capture metadata from instance.
/// </summary>
public void RegisterRenderSystem(IRenderSystem system)
{
    ArgumentNullException.ThrowIfNull(system);

    // Extract metadata if system provides it
    if (system is IParallelSystemMetadata parallelSystem)
    {
        var metadata = new SystemMetadata
        {
            SystemType = system.GetType(),
            ReadsComponents = parallelSystem.GetReadComponents(),
            WritesComponents = parallelSystem.GetWriteComponents(),
            Priority = system is ISystem legacySystem ? legacySystem.Priority :
                       system.RenderOrder,
            AllowsParallelExecution = parallelSystem.AllowsParallelExecution
        };

        // Register in dependency graph
        try
        {
            var method = _dependencyGraph.GetType().GetMethod(nameof(_dependencyGraph.RegisterSystem));
            var genericMethod = method!.MakeGenericMethod(system.GetType());
            genericMethod.Invoke(_dependencyGraph, new object[] { metadata });

            _logger?.LogInformation(
                "Render system {SystemName} registered with metadata | Reads: {ReadCount}, Writes: {WriteCount}, Parallel: {AllowsParallel}",
                system.GetType().Name,
                metadata.ReadsComponents.Count,
                metadata.WritesComponents.Count,
                metadata.AllowsParallelExecution
            );
        }
        catch (Exception ex)
        {
            _logger?.LogWarning(ex, "Failed to register render system metadata for {SystemName}", system.GetType().Name);
        }
    }
    else
    {
        _logger?.LogInformation(
            "Render system {SystemName} registered without metadata (not IParallelSystemMetadata)",
            system.GetType().Name
        );
    }

    // Call base implementation
    base.RegisterRenderSystem(system);

    _executionPlanBuilt = false;
}
```

#### 2. Fix RebuildExecutionPlan to Include All Systems

**File**: `PokeSharp.Core/Parallel/ParallelSystemManager.cs`

**Replace method** (lines 150-197):

```csharp
/// <summary>
///     Rebuild execution plan based on current registered systems.
///     Must be called after all systems are registered and before Update.
/// </summary>
public void RebuildExecutionPlan()
{
    // Collect ALL system types from both update and render lists
    var allSystems = new List<Type>();

    // Get update systems (which includes legacy systems implementing ISystem)
    lock (_lock)
    {
        // Use reflection to access base class private fields
        var baseType = typeof(SystemManager);

        // Get _updateSystems field
        var updateSystemsField = baseType.GetField("_updateSystems",
            System.Reflection.BindingFlags.NonPublic |
            System.Reflection.BindingFlags.Instance);

        if (updateSystemsField != null)
        {
            var updateSystems = (List<IUpdateSystem>)updateSystemsField.GetValue(this)!;
            allSystems.AddRange(updateSystems.Select(s => s.GetType()));
        }

        // Get _renderSystems field
        var renderSystemsField = baseType.GetField("_renderSystems",
            System.Reflection.BindingFlags.NonPublic |
            System.Reflection.BindingFlags.Instance);

        if (renderSystemsField != null)
        {
            var renderSystems = (List<IRenderSystem>)renderSystemsField.GetValue(this)!;
            allSystems.AddRange(renderSystems.Select(s => s.GetType()));
        }

        // Also include legacy systems that don't implement IUpdateSystem/IRenderSystem
        var legacySystems = Systems.Where(s => s is not IUpdateSystem && s is not IRenderSystem);
        allSystems.AddRange(legacySystems.Select(s => s.GetType()));
    }

    // Remove duplicates (systems might be in multiple lists)
    var systemTypes = allSystems.Distinct().ToList();

    _logger?.LogInformation("Building execution plan for {SystemCount} systems", systemTypes.Count);
    foreach (var type in systemTypes)
    {
        var metadata = _dependencyGraph.GetMetadata(type);
        if (metadata != null)
        {
            _logger?.LogDebug("  System registered: {SystemName} (Priority: {Priority}, Reads: {ReadCount}, Writes: {WriteCount})",
                type.Name, metadata.Priority, metadata.ReadsComponents.Count, metadata.WritesComponents.Count);
        }
        else
        {
            _logger?.LogWarning("  System {SystemName} has NO METADATA - will not be included in parallel execution", type.Name);
        }
    }

    var stages = _dependencyGraph.ComputeExecutionStages(systemTypes);

    _logger?.LogInformation("ComputeExecutionStages returned {StageCount} stages", stages.Count);
    for (int i = 0; i < stages.Count; i++)
    {
        _logger?.LogDebug("  Stage {Index}: {SystemCount} systems - {Systems}",
            i + 1, stages[i].Count, string.Join(", ", stages[i].Select(t => t.Name)));
    }

    // Map types back to instances from ALL system lists
    _executionStages = stages.Select(stage =>
    {
        var stageInstances = new List<ISystem>();

        foreach (var systemType in stage)
        {
            // Try to find in update systems
            var updateField = typeof(SystemManager).GetField("_updateSystems",
                System.Reflection.BindingFlags.NonPublic |
                System.Reflection.BindingFlags.Instance);

            if (updateField != null)
            {
                var updateSystems = (List<IUpdateSystem>)updateField.GetValue(this)!;
                var updateSystem = updateSystems.FirstOrDefault(s => s.GetType() == systemType);
                if (updateSystem != null && updateSystem is ISystem legacyUpdateSystem)
                {
                    stageInstances.Add(legacyUpdateSystem);
                    continue;
                }
            }

            // Try to find in render systems
            var renderField = typeof(SystemManager).GetField("_renderSystems",
                System.Reflection.BindingFlags.NonPublic |
                System.Reflection.BindingFlags.Instance);

            if (renderField != null)
            {
                var renderSystems = (List<IRenderSystem>)renderField.GetValue(this)!;
                var renderSystem = renderSystems.FirstOrDefault(s => s.GetType() == systemType);
                if (renderSystem != null && renderSystem is ISystem legacyRenderSystem)
                {
                    stageInstances.Add(legacyRenderSystem);
                    continue;
                }
            }

            // Try legacy systems list
            var legacySystem = Systems.FirstOrDefault(s => s.GetType() == systemType);
            if (legacySystem != null)
            {
                stageInstances.Add(legacySystem);
            }
        }

        return stageInstances;
    }).ToList();

    _executionPlanBuilt = true;

    if (_executionStages.Count == 0 || _executionStages.All(s => s.Count == 0))
    {
        _logger?.LogWarning("Execution plan is empty, parallel execution will be disabled");
        _executionPlanBuilt = false;
        return;
    }

    _logger?.LogInformation(
        "Parallel execution plan built: {StageCount} stages, {MaxParallelism} max parallel systems",
        _executionStages.Count,
        _executionStages.Max(s => s.Count)
    );

    LogExecutionPlan();
}
```

#### 3. Better Alternative: Expose Protected Access

**Much cleaner approach** - Modify SystemManager to expose system lists:

**File**: `PokeSharp.Core/Systems/SystemManager.cs`

**Add new protected properties** (after line 59):

```csharp
/// <summary>
///     Gets all registered update systems (for derived classes).
/// </summary>
protected IReadOnlyList<IUpdateSystem> UpdateSystems
{
    get
    {
        lock (_lock)
        {
            return _updateSystems.AsReadOnly();
        }
    }
}

/// <summary>
///     Gets all registered render systems (for derived classes).
/// </summary>
protected IReadOnlyList<IRenderSystem> RenderSystems
{
    get
    {
        lock (_lock)
        {
            return _renderSystems.AsReadOnly();
        }
    }
}
```

**Then simplify RebuildExecutionPlan** in ParallelSystemManager:

```csharp
public void RebuildExecutionPlan()
{
    // Collect ALL system types
    var systemTypes = new List<Type>();

    // Add update systems
    systemTypes.AddRange(UpdateSystems.Select(s => s.GetType()));

    // Add render systems
    systemTypes.AddRange(RenderSystems.Select(s => s.GetType()));

    // Add legacy systems (not implementing IUpdateSystem/IRenderSystem)
    systemTypes.AddRange(Systems
        .Where(s => s is not IUpdateSystem && s is not IRenderSystem)
        .Select(s => s.GetType()));

    // Remove duplicates
    systemTypes = systemTypes.Distinct().ToList();

    _logger?.LogInformation("Building execution plan for {SystemCount} systems", systemTypes.Count);
    foreach (var type in systemTypes)
    {
        _logger?.LogDebug("  System type: {SystemName}", type.Name);
    }

    var stages = _dependencyGraph.ComputeExecutionStages(systemTypes);

    _logger?.LogInformation("ComputeExecutionStages returned {StageCount} stages", stages.Count);

    // Map types back to instances
    _executionStages = stages.Select(stage =>
    {
        var instances = new List<ISystem>();

        foreach (var type in stage)
        {
            // Try update systems
            var updateSystem = UpdateSystems.FirstOrDefault(s => s.GetType() == type);
            if (updateSystem is ISystem legacyUpdate)
            {
                instances.Add(legacyUpdate);
                continue;
            }

            // Try render systems
            var renderSystem = RenderSystems.FirstOrDefault(s => s.GetType() == type);
            if (renderSystem is ISystem legacyRender)
            {
                instances.Add(legacyRender);
                continue;
            }

            // Try legacy systems
            var legacySystem = Systems.FirstOrDefault(s => s.GetType() == type);
            if (legacySystem != null)
            {
                instances.Add(legacySystem);
            }
        }

        return instances;
    }).ToList();

    _executionPlanBuilt = true;

    if (_executionStages.Count == 0 || _executionStages.All(s => s.Count == 0))
    {
        _logger?.LogWarning("Execution plan is empty, parallel execution will be disabled");
        _executionPlanBuilt = false;
        return;
    }

    _logger?.LogInformation(
        "Parallel execution plan built: {StageCount} stages, {MaxParallelism} max parallel systems",
        _executionStages.Count,
        _executionStages.Max(s => s.Count)
    );

    LogExecutionPlan();
}
```

#### 4. Add New Method to Track System Metrics

**File**: `PokeSharp.Core/Parallel/ParallelSystemManager.cs`

**Update the Update method** (lines 202-256) to track per-system metrics:

```csharp
/// <summary>
///     Update all systems with parallel execution where safe.
/// </summary>
public new void Update(World world, float deltaTime)
{
    ArgumentNullException.ThrowIfNull(world);

    if (!_parallelEnabled || !_executionPlanBuilt || _executionStages == null || _executionStages.Count == 0)
    {
        // Fall back to sequential execution
        base.Update(world, deltaTime);
        return;
    }

    // Execute each stage
    foreach (var stage in _executionStages)
    {
        if (stage.Count == 1)
        {
            // Single system in stage, run sequentially
            var system = stage[0];
            if (system.Enabled)
            {
                var stopwatch = Stopwatch.StartNew();
                try
                {
                    system.Update(world, deltaTime);
                }
                catch (Exception ex)
                {
                    _logger?.LogError(ex, "System {SystemName} threw exception during update", system.GetType().Name);
                }
                stopwatch.Stop();

                // Track metrics using protected method from base class
                TrackSystemPerformance(system.GetType().Name, stopwatch.Elapsed.TotalMilliseconds);
            }
        }
        else
        {
            // Multiple systems in stage, run in parallel
            System.Threading.Tasks.Parallel.ForEach(
                stage.Where(s => s.Enabled),
                new ParallelOptions { MaxDegreeOfParallelism = Environment.ProcessorCount },
                system =>
                {
                    var stopwatch = Stopwatch.StartNew();
                    try
                    {
                        system.Update(world, deltaTime);
                    }
                    catch (Exception ex)
                    {
                        _logger?.LogError(ex, "System {SystemName} threw exception during parallel update", system.GetType().Name);
                    }
                    stopwatch.Stop();

                    // Track metrics using protected method from base class
                    TrackSystemPerformance(system.GetType().Name, stopwatch.Elapsed.TotalMilliseconds);
                }
            );
        }
    }
}
```

**But wait** - `TrackSystemPerformance` is private in the base class. We need to either:
1. Make it protected in SystemManager
2. Use reflection (bad)
3. Duplicate the logic (bad)
4. Access metrics dictionary directly

**Best solution**: Make TrackSystemPerformance protected in SystemManager:

**File**: `PokeSharp.Core/Systems/SystemManager.cs`

Change line 424 from:
```csharp
private void TrackSystemPerformance(string systemName, double elapsedMs)
```

To:
```csharp
protected void TrackSystemPerformance(string systemName, double elapsedMs)
```

## Implementation Checklist

### Phase 1: SystemManager Changes (Base Class)
- [ ] Add `protected IReadOnlyList<IUpdateSystem> UpdateSystems` property
- [ ] Add `protected IReadOnlyList<IRenderSystem> RenderSystems` property
- [ ] Change `TrackSystemPerformance` from private to protected
- [ ] Add tests for new protected properties

### Phase 2: ParallelSystemManager Changes
- [ ] Add `RegisterUpdateSystem<T>()` override method
- [ ] Add `RegisterUpdateSystem(IUpdateSystem)` override method
- [ ] Add `RegisterRenderSystem<T>()` override method
- [ ] Add `RegisterRenderSystem(IRenderSystem)` override method
- [ ] Rewrite `RebuildExecutionPlan()` to use new protected properties
- [ ] Update `Update()` method to use protected `TrackSystemPerformance`
- [ ] Add comprehensive logging for metadata registration

### Phase 3: Testing
- [ ] Unit test: Verify update systems are registered in dependency graph
- [ ] Unit test: Verify render systems are registered in dependency graph
- [ ] Unit test: Verify RebuildExecutionPlan includes all systems
- [ ] Unit test: Verify execution stages are built correctly
- [ ] Integration test: Full parallel execution with real systems
- [ ] Performance test: Measure per-system frame times

### Phase 4: Documentation
- [ ] Update XML documentation for new methods
- [ ] Update parallel execution guide
- [ ] Add architecture decision record (ADR)

## Testing Strategy

### Unit Tests Required

1. **Metadata Registration Tests**
   - Test that `RegisterUpdateSystem` captures IParallelSystemMetadata
   - Test that `RegisterRenderSystem` captures IParallelSystemMetadata
   - Test that systems without metadata get default metadata
   - Test reflection registration path

2. **Execution Plan Tests**
   - Test that RebuildExecutionPlan includes update systems
   - Test that RebuildExecutionPlan includes render systems
   - Test that RebuildExecutionPlan includes legacy systems
   - Test that duplicate systems are handled correctly
   - Test stage count is > 0 with valid systems

3. **Parallel Execution Tests**
   - Test that systems execute in correct stages
   - Test that metrics are tracked per system
   - Test that exceptions are caught and logged
   - Test fallback to sequential execution when plan fails

### Integration Tests Required

1. **Full System Registration Flow**
   ```csharp
   var manager = new ParallelSystemManager(world, true, logger);
   manager.RegisterUpdateSystem<MovementSystem>();
   manager.RegisterRenderSystem<ZOrderRenderSystem>();
   manager.RebuildExecutionPlan();

   // Verify stages > 0
   // Verify systems in dependency graph
   ```

2. **Parallel Execution Validation**
   ```csharp
   manager.Update(world, deltaTime);

   // Verify all systems executed
   // Verify metrics collected
   // Verify execution plan was used
   ```

## Performance Considerations

### Expected Improvements

1. **Stage Building**: O(n²) where n = number of systems
   - Current: ~10 systems = 100 operations
   - Acceptable for system registration (one-time cost)

2. **Per-Frame Execution**: O(n) where n = systems in stage
   - Parallel stages execute concurrently
   - Expected 2-4x speedup for systems with no dependencies

3. **Memory Usage**:
   - Additional overhead: ~1KB per system for metadata
   - Total: ~10KB for typical game (10 systems)

### Monitoring

Add logging for:
- Number of stages built
- Systems per stage
- Execution time per stage
- Total parallel vs sequential comparison

## Migration Guide

### For Existing Code

No breaking changes! The new implementation:
1. ✅ Maintains backward compatibility with `RegisterSystem(ISystem)`
2. ✅ Works with existing `RegisterUpdateSystem`/`RegisterRenderSystem` API
3. ✅ Only requires changing `SystemManager` to `ParallelSystemManager` in GameInitializer

### For New Systems

Systems should:
1. Extend `ParallelSystemBase` instead of `SystemBase`
2. Implement `GetReadComponents()` and `GetWriteComponents()`
3. Be registered normally via `RegisterUpdateSystem` or `RegisterRenderSystem`

Example:
```csharp
public class MySystem : ParallelSystemBase, IUpdateSystem
{
    public int UpdatePriority => 100;

    public override List<Type> GetReadComponents() => new()
    {
        typeof(Position),
        typeof(Velocity)
    };

    public override List<Type> GetWriteComponents() => new()
    {
        typeof(Position)
    };

    public override void Update(World world, float deltaTime)
    {
        // Implementation
    }
}
```

## Summary

This design fixes the parallel execution issue by:

1. **Overriding registration methods** to capture metadata from all systems
2. **Exposing protected properties** for update/render system lists
3. **Rebuilding execution plan** to include ALL registered systems (update + render + legacy)
4. **Tracking metrics** properly for parallel execution
5. **Maintaining backward compatibility** with existing code

The fix ensures that systems registered via the modern API (`RegisterUpdateSystem`/`RegisterRenderSystem`) are properly included in the parallel execution plan, while maintaining full compatibility with legacy code.
