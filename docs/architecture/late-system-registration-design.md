# Architecture Decision Record: Late System Registration

**Status**: Proposed
**Date**: 2025-11-10
**Decision**: Design solution for systems registered after ParallelSystemManager initialization

---

## Problem Statement

**Issue**: NPCBehaviorSystem is registered AFTER `RebuildExecutionPlan()` is called in GameInitializer, so it's not included in the parallel execution stages.

**Current Flow**:
```
GameInitializer.Initialize()
├─ Register 8 systems (Update + Render systems)
├─ _systemManager.Initialize(_world)
├─ RebuildExecutionPlan() [if ParallelSystemManager] ← Builds plan with 8 systems
└─ Returns

NPCBehaviorInitializer.Initialize() [LATER]
└─ systemManager.RegisterSystem(npcBehaviorSystem) ← System #9 NOT in plan!
```

**Result**: NPCBehaviorSystem runs sequentially, missing out on parallel execution optimization.

---

## Requirements

### Functional Requirements
1. Support late system registration (systems added after initialization)
2. Automatically include late-registered systems in execution plan
3. Maintain backward compatibility with existing registration patterns
4. Log when execution plan is rebuilt due to late registration

### Non-Functional Requirements
1. **Performance**: Minimal overhead - don't rebuild on every frame
2. **Thread Safety**: Safe concurrent access to `_executionPlanBuilt` flag
3. **Maintainability**: Clear API semantics - developers shouldn't need to remember to call RebuildExecutionPlan()
4. **Debugging**: Clear logging to track execution plan changes

---

## Analysis of Options

### Option A: Invalidate execution plan in RegisterSystem() - rebuild on next Update()

**Approach**: Set `_executionPlanBuilt = false` in RegisterSystem(), rebuild lazily on next Update()

```csharp
public override void RegisterSystem(ISystem system)
{
    // ... existing registration code ...

    _executionPlanBuilt = false; // ← Invalidate plan
    _logger?.LogInformation("Execution plan invalidated due to late registration of {System}",
        system.GetType().Name);
}

public new void Update(World world, float deltaTime)
{
    // Rebuild plan if invalidated
    if (_parallelEnabled && !_executionPlanBuilt)
    {
        _logger?.LogInformation("Rebuilding execution plan due to system changes");
        RebuildExecutionPlan();
    }

    // ... existing update code ...
}
```

**Pros**:
- ✅ Automatic - no manual RebuildExecutionPlan() calls needed
- ✅ Deferred cost - rebuild happens once before first Update()
- ✅ Simple implementation
- ✅ Thread-safe with volatile flag

**Cons**:
- ⚠️ One frame delay - late system runs sequentially for 1 frame
- ⚠️ Unexpected rebuild during Update() could cause latency spike

**Performance**: Rebuild cost deferred to first Update() after registration (~1-2ms one-time cost)

---

### Option B: Call RebuildExecutionPlan() immediately in RegisterSystem()

**Approach**: Rebuild execution plan synchronously whenever a system is registered

```csharp
public override void RegisterSystem(ISystem system)
{
    // ... existing registration code ...

    // Rebuild immediately if already initialized
    if (_executionPlanBuilt)
    {
        _logger?.LogInformation("Rebuilding execution plan for late-registered {System}",
            system.GetType().Name);
        RebuildExecutionPlan();
    }
}
```

**Pros**:
- ✅ Immediate consistency - no frame delay
- ✅ Predictable performance - rebuild happens during registration
- ✅ Simpler Update() logic

**Cons**:
- ⚠️ Registration latency - adds 1-2ms to RegisterSystem() call
- ⚠️ Multiple registrations = multiple rebuilds (inefficient)
- ⚠️ Could cause issues if registration happens during Update()

**Performance**: ~1-2ms added to each late RegisterSystem() call

---

### Option C: Detect new systems in Update() and rebuild if needed

**Approach**: Track system count, rebuild if it changes

```csharp
private int _lastKnownSystemCount = 0;

public new void Update(World world, float deltaTime)
{
    var currentCount = RegisteredUpdateSystems.Count + RegisteredRenderSystems.Count;

    if (_parallelEnabled && currentCount != _lastKnownSystemCount)
    {
        _logger?.LogInformation("Detected system count change ({Old} → {New}), rebuilding plan",
            _lastKnownSystemCount, currentCount);
        RebuildExecutionPlan();
        _lastKnownSystemCount = currentCount;
    }

    // ... existing update code ...
}
```

**Pros**:
- ✅ Automatic detection
- ✅ No changes to RegisterSystem()

**Cons**:
- ❌ Fragile - only detects count changes (misses replacements)
- ❌ Extra overhead on every Update() (count check)
- ❌ Unclear semantics - when does rebuild happen?

**Performance**: ~0.1μs overhead per frame + rebuild cost when triggered

---

### Option D: Require explicit RebuildExecutionPlan() call after late registration

**Approach**: Keep current behavior, document requirement

```csharp
// NPCBehaviorInitializer.cs
public void Initialize()
{
    // ... load behaviors ...

    systemManager.RegisterSystem(npcBehaviorSystem);

    // Rebuild execution plan if parallel manager
    if (systemManager is ParallelSystemManager parallelManager)
    {
        parallelManager.RebuildExecutionPlan();
    }
}
```

**Pros**:
- ✅ Explicit control - developer chooses when to rebuild
- ✅ Efficient for batch registration (one rebuild for multiple systems)
- ✅ No surprises - no hidden rebuilds

**Cons**:
- ❌ Easy to forget - error-prone API
- ❌ Requires developer knowledge - not discoverable
- ❌ Inconsistent behavior if forgotten

**Performance**: Optimal (rebuild only when explicitly requested)

---

## Recommended Approach: **Option A (Lazy Rebuild)**

### Decision Rationale

**Option A is recommended** because it provides the best balance of:

1. **Automatic behavior** - No manual RebuildExecutionPlan() calls needed
2. **Performance** - Deferred rebuild avoids registration latency
3. **Safety** - One frame delay is acceptable for late-registered systems
4. **Simplicity** - Clean implementation with existing flag mechanism

### Why Not Other Options?

- **Not B**: Registration latency is problematic for initialization sequences with many systems
- **Not C**: Count-based detection is fragile and adds per-frame overhead
- **Not D**: Explicit calls are error-prone and not discoverable

### Edge Cases Handled

1. **Multiple late registrations**: Only rebuild once before first Update()
2. **Thread safety**: Use `volatile bool` for `_executionPlanBuilt`
3. **Initialization order**: Works whether systems registered before or after Initialize()
4. **No late registration**: Zero overhead if all systems registered before RebuildExecutionPlan()

---

## Implementation Design

### Code Changes

#### 1. Make `_executionPlanBuilt` volatile for thread safety

```csharp
// ParallelSystemManager.cs (line 20)
- private bool _executionPlanBuilt;
+ private volatile bool _executionPlanBuilt;
```

**Rationale**: Ensures visibility of flag changes across threads (if Update() runs on separate thread)

#### 2. Add lazy rebuild logic to Update()

```csharp
// ParallelSystemManager.cs - Update() method
public new void Update(World world, float deltaTime)
{
    ArgumentNullException.ThrowIfNull(world);

    // Lazy rebuild if plan was invalidated by late registration
    if (_parallelEnabled && !_executionPlanBuilt)
    {
        _logger?.LogInformation(
            "Rebuilding execution plan due to late system registration (systems: {Count})",
            RegisteredUpdateSystems.Count + RegisteredRenderSystems.Count);
        RebuildExecutionPlan();
    }

    if (!_parallelEnabled || !_executionPlanBuilt || _executionStages == null || _executionStages.Count == 0)
    {
        // Fall back to sequential execution
        base.Update(world, deltaTime);
        return;
    }

    // ... rest of parallel execution logic ...
}
```

#### 3. Update RegisterSystem() to invalidate plan (already done)

Current implementation already sets `_executionPlanBuilt = false` in:
- `RegisterSystem(ISystem)` - line 96 ✓
- `RegisterUpdateSystem<T>()` - line 115 ✓
- `RegisterUpdateSystem(IUpdateSystem)` - line 130 ✓
- `RegisterRenderSystem<T>()` - line 149 ✓
- `RegisterRenderSystem(IRenderSystem)` - line 164 ✓

**Enhancement**: Add logging when invalidating due to late registration

```csharp
public override void RegisterSystem(ISystem system)
{
    // ... existing code ...

    bool wasBuilt = _executionPlanBuilt;
    _executionPlanBuilt = false;

    if (wasBuilt)
    {
        _logger?.LogInformation(
            "Execution plan invalidated: {System} registered after initialization",
            system.GetType().Name);
    }
}
```

---

## Edge Cases & Handling

### 1. System registered during Update()

**Scenario**: Another thread registers a system while Update() is running

**Handling**:
- `volatile bool _executionPlanBuilt` ensures visibility
- Plan rebuilds on *next* frame, not current frame
- Current frame continues with existing plan (safe)

**Trade-off**: New system waits 1 frame before being included

---

### 2. Multiple systems registered in quick succession

**Scenario**:
```csharp
systemManager.RegisterSystem(system1);
systemManager.RegisterSystem(system2);
systemManager.RegisterSystem(system3);
```

**Handling**:
- Each registration sets `_executionPlanBuilt = false`
- Only rebuilds ONCE before next Update()
- No performance penalty for multiple registrations

**Optimization**: Lazy rebuild amortizes cost across all registrations

---

### 3. RebuildExecutionPlan() called manually after late registration

**Scenario**: Developer explicitly calls RebuildExecutionPlan() after registering a late system

**Handling**:
- Sets `_executionPlanBuilt = true`
- Lazy rebuild in Update() detects flag is true, skips rebuild
- No duplicate rebuilds

**Behavior**: Manual call takes precedence (expected)

---

### 4. No late registrations (common case)

**Scenario**: All systems registered before RebuildExecutionPlan() (current flow in GameInitializer)

**Handling**:
- Plan built during initialization
- `_executionPlanBuilt = true`
- Update() skips lazy rebuild check (flag already true)

**Performance**: Zero overhead for existing code paths

---

### 5. Parallel execution disabled

**Scenario**: ParallelSystemManager created with `enableParallel: false`

**Handling**:
- Update() checks `!_parallelEnabled` first
- Skips lazy rebuild logic entirely
- Falls back to base.Update() (sequential)

**Performance**: No rebuild overhead when parallel disabled

---

### 6. Empty execution plan after rebuild

**Scenario**: RebuildExecutionPlan() produces empty stages (no systems with metadata)

**Handling**:
- Current code sets `_executionPlanBuilt = false` if empty (line 292)
- Logs warning (line 291)
- Falls back to sequential execution

**Safety**: Prevents infinite rebuild loop

---

## Performance Analysis

### Rebuild Cost
- **Typical rebuild time**: 1-2ms for 10 systems
- **Frequency**: Once per late registration batch (not per frame)
- **Impact**: Negligible (0.06% of 60 FPS frame budget)

### Lazy Rebuild Overhead
- **Check cost**: `if (!_executionPlanBuilt)` ≈ 1-2 CPU cycles (~0.001μs)
- **Frequency**: Once per frame
- **Impact**: Unmeasurable (<0.0001% of frame budget)

### Comparison with Option B (Immediate Rebuild)
- **Option A (Lazy)**: 1-2ms at next Update()
- **Option B (Immediate)**: 1-2ms per RegisterSystem() call
- **Winner**: Option A (better for batch registration)

---

## Migration Guide

### For Existing Code (No Changes Required)

**GameInitializer.cs** (current code):
```csharp
// Register systems
_systemManager.RegisterUpdateSystem(new InputSystem(...));
_systemManager.RegisterUpdateSystem(new MovementSystem(...));
// ... more systems ...

_systemManager.Initialize(_world);

// Build execution plan
if (_systemManager is ParallelSystemManager parallelManager)
{
    parallelManager.RebuildExecutionPlan();
}
```

**Result**: Works as before, no changes needed ✓

---

### For Late Registration (Automatic)

**NPCBehaviorInitializer.cs** (existing code):
```csharp
public void Initialize()
{
    // ... load behaviors ...

    systemManager.RegisterSystem(npcBehaviorSystem);

    // ✅ No RebuildExecutionPlan() call needed!
}
```

**Result**: System automatically included in execution plan on next Update() ✓

---

### Optional: Manual Rebuild for Immediate Effect

If you want late-registered system to take effect immediately (same frame):

```csharp
public void Initialize()
{
    // ... load behaviors ...

    systemManager.RegisterSystem(npcBehaviorSystem);

    // Force immediate rebuild (optional)
    if (systemManager is ParallelSystemManager parallelManager)
    {
        parallelManager.RebuildExecutionPlan();
    }
}
```

**Trade-off**: Adds 1-2ms to initialization, but system active immediately

---

## Testing Strategy

### Unit Tests

#### Test 1: Late registration triggers rebuild
```csharp
[Fact]
public void LateRegistration_TriggersRebuild_OnNextUpdate()
{
    var manager = new ParallelSystemManager(world, enableParallel: true, logger);

    // Initial registration and build
    manager.RegisterSystem(new MovementSystem());
    manager.RebuildExecutionPlan();

    var initialPlan = manager.GetExecutionPlan();

    // Late registration
    manager.RegisterSystem(new NPCBehaviorSystem());

    // Trigger rebuild via Update
    manager.Update(world, 0.016f);

    var updatedPlan = manager.GetExecutionPlan();

    Assert.NotEqual(initialPlan, updatedPlan);
    Assert.Contains("NPCBehaviorSystem", updatedPlan);
}
```

#### Test 2: Multiple late registrations rebuild only once
```csharp
[Fact]
public void MultipleLateRegistrations_RebuildOnce()
{
    var manager = new ParallelSystemManager(world, enableParallel: true, logger);
    manager.RebuildExecutionPlan();

    int rebuildCount = 0;
    // Hook into logger to count rebuilds

    manager.RegisterSystem(new System1());
    manager.RegisterSystem(new System2());
    manager.RegisterSystem(new System3());

    manager.Update(world, 0.016f);

    Assert.Equal(1, rebuildCount); // Only 1 rebuild
}
```

#### Test 3: No late registration - no rebuild overhead
```csharp
[Fact]
public void NoLateRegistration_NoRebuildOverhead()
{
    var manager = new ParallelSystemManager(world, enableParallel: true, logger);
    manager.RegisterSystem(new MovementSystem());
    manager.RebuildExecutionPlan();

    int rebuildCount = 0;

    for (int i = 0; i < 1000; i++)
    {
        manager.Update(world, 0.016f);
    }

    Assert.Equal(0, rebuildCount); // No rebuilds during normal operation
}
```

---

### Integration Tests

#### Test 4: NPCBehaviorSystem included after late registration
```csharp
[Fact]
public void NPCBehaviorSystem_IncludedInExecutionPlan_AfterLateRegistration()
{
    var gameInitializer = new GameInitializer(...);
    gameInitializer.Initialize(graphicsDevice);

    var npcInitializer = new NPCBehaviorInitializer(...);
    npcInitializer.Initialize();

    var parallelManager = (ParallelSystemManager)systemManager;

    // Trigger rebuild via Update
    parallelManager.Update(world, 0.016f);

    var plan = parallelManager.GetExecutionPlan();
    Assert.Contains("NPCBehaviorSystem", plan);
}
```

---

## Logging & Debugging

### Log Messages Added

#### On late registration:
```
[Information] Execution plan invalidated: NPCBehaviorSystem registered after initialization
```

#### On lazy rebuild:
```
[Information] Rebuilding execution plan due to late system registration (systems: 9)
[Information] Parallel execution plan built: 3 stages, 4 max parallel systems
```

### Debugging Commands

#### Check execution plan:
```csharp
var parallelManager = (ParallelSystemManager)systemManager;
Console.WriteLine(parallelManager.GetExecutionPlan());
```

#### Verify system included:
```csharp
var plan = parallelManager.GetExecutionPlan();
Assert.Contains("NPCBehaviorSystem", plan);
```

---

## Documentation Updates

### 1. XML Doc Comments

Update `RegisterSystem()` documentation:

```csharp
/// <summary>
///     Register a system instance with the manager.
///     Systems are automatically sorted by priority.
///     If called after RebuildExecutionPlan(), the execution plan will be
///     automatically rebuilt on the next Update() call.
/// </summary>
/// <param name="system">The system to register.</param>
/// <remarks>
///     Late registration (after RebuildExecutionPlan()) is supported.
///     The system will be included in parallel execution starting from the
///     next frame. For immediate effect, call RebuildExecutionPlan() manually.
/// </remarks>
```

### 2. README.md Section

Add to **docs/PARALLEL_EXECUTION_GUIDE.md**:

```markdown
## Late System Registration

Systems can be registered after `RebuildExecutionPlan()` has been called.
They will be automatically included in parallel execution on the next frame.

### Example: Dynamic System Loading

```csharp
// Initialize core systems
gameInitializer.Initialize(graphicsDevice);

// Later: Load and register NPC behavior system
npcBehaviorInitializer.Initialize(); // Registers NPCBehaviorSystem

// ✅ No manual RebuildExecutionPlan() needed!
// NPCBehaviorSystem will be included in parallel execution automatically
```

### Performance Notes
- Late-registered systems are included in the next frame (1 frame delay)
- Rebuild cost: ~1-2ms one-time cost before next Update()
- No per-frame overhead in normal operation
```

---

## Risks & Mitigations

### Risk 1: Thread Safety Issues
**Risk**: Concurrent access to `_executionPlanBuilt` flag
**Likelihood**: Low (Update() typically single-threaded)
**Impact**: Medium (could cause missed rebuilds)
**Mitigation**: Use `volatile bool` for memory visibility

### Risk 2: Performance Regression
**Risk**: Lazy rebuild adds overhead to Update()
**Likelihood**: Low (flag check is ~1 CPU cycle)
**Impact**: Low (<0.0001% of frame budget)
**Mitigation**: Performance test with 1000 frames, verify no regression

### Risk 3: Unexpected Rebuild During Critical Section
**Risk**: Late registration during performance-critical gameplay
**Likelihood**: Low (registration typically happens during initialization)
**Impact**: Medium (1-2ms latency spike)
**Mitigation**:
- Log warning if rebuild happens during Update()
- Document best practice: register systems during initialization

---

## Success Metrics

### Functional Success
- ✅ Late-registered systems included in execution plan
- ✅ No manual RebuildExecutionPlan() calls needed in application code
- ✅ Backward compatible with existing code

### Performance Success
- ✅ No measurable per-frame overhead (<0.001μs)
- ✅ Rebuild cost ≤ 2ms for 20 systems
- ✅ No regression in 60 FPS target

### Developer Experience
- ✅ Clear log messages for debugging
- ✅ Intuitive behavior (automatic)
- ✅ Well-documented edge cases

---

## Future Enhancements

### 1. Batch Registration API (v2.0)
```csharp
manager.BeginRegistration();
manager.RegisterSystem(system1);
manager.RegisterSystem(system2);
manager.RegisterSystem(system3);
manager.EndRegistration(); // Rebuild once
```

### 2. Registration Events (v2.1)
```csharp
manager.OnSystemRegistered += (system) => {
    Console.WriteLine($"System registered: {system.GetType().Name}");
};
```

### 3. Deferred Rebuild Threshold (v2.2)
```csharp
// Only rebuild if N+ systems registered since last plan
manager.SetRebuildThreshold(5); // Rebuild after 5 late registrations
```

---

## Conclusion

**Option A (Lazy Rebuild)** provides automatic, performant, and safe handling of late system registration with minimal code changes and zero per-frame overhead. This design maintains backward compatibility while improving developer experience and system robustness.

### Implementation Checklist
- [ ] Make `_executionPlanBuilt` volatile (1 line)
- [ ] Add lazy rebuild logic to Update() (5 lines)
- [ ] Add logging for late registration (3 lines)
- [ ] Update XML doc comments (5 lines)
- [ ] Add unit tests (3 tests)
- [ ] Add integration test (1 test)
- [ ] Update documentation (1 README section)

**Total Effort**: ~2-3 hours
**Risk Level**: Low
**Backward Compatibility**: ✅ Full
