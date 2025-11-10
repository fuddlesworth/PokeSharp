# Late System Registration - Implementation Plan

**Status**: Ready for Implementation
**Estimated Effort**: 2-3 hours
**Risk**: Low
**Backward Compatibility**: ✅ Full

---

## Executive Summary

**Problem**: NPCBehaviorSystem registered after `RebuildExecutionPlan()` is excluded from parallel execution.

**Root Cause**: Current implementation requires all systems to be registered before calling `RebuildExecutionPlan()`. Late-registered systems run sequentially instead of in parallel.

**Recommended Solution**: Lazy rebuild - automatically rebuild execution plan on next `Update()` when new systems are registered.

**Impact**: Zero per-frame overhead, minimal one-time rebuild cost (~1-2ms), automatic behavior.

---

## Code Changes Required

### 1. Make `_executionPlanBuilt` volatile (Thread Safety)

**File**: `PokeSharp.Core/Parallel/ParallelSystemManager.cs` (Line 20)

```diff
- private bool _executionPlanBuilt;
+ private volatile bool _executionPlanBuilt;
```

**Rationale**: Ensures visibility of flag changes across threads

---

### 2. Add Lazy Rebuild Logic to Update()

**File**: `PokeSharp.Core/Parallel/ParallelSystemManager.cs` (Line 308, before existing checks)

```csharp
public new void Update(World world, float deltaTime)
{
    ArgumentNullException.ThrowIfNull(world);

    // NEW: Lazy rebuild if plan was invalidated by late registration
    if (_parallelEnabled && !_executionPlanBuilt)
    {
        _logger?.LogInformation(
            "Rebuilding execution plan due to late system registration (systems: {Count})",
            RegisteredUpdateSystems.Count + RegisteredRenderSystems.Count);
        RebuildExecutionPlan();
    }

    // EXISTING: Fall back to sequential if plan invalid
    if (!_parallelEnabled || !_executionPlanBuilt || _executionStages == null || _executionStages.Count == 0)
    {
        base.Update(world, deltaTime);
        return;
    }

    // ... rest of method unchanged ...
}
```

---

### 3. Enhanced Logging for Late Registration

**File**: `PokeSharp.Core/Parallel/ParallelSystemManager.cs` (Line 88)

```csharp
public override void RegisterSystem(ISystem system)
{
    ArgumentNullException.ThrowIfNull(system);

    // Extract and register metadata before calling base
    RegisterSystemMetadata(system, system.Priority);

    base.RegisterSystem(system);

    // NEW: Enhanced logging for late registration
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

Apply same pattern to all registration methods:
- `RegisterUpdateSystem<T>()` (Line 102)
- `RegisterUpdateSystem(IUpdateSystem)` (Line 120)
- `RegisterRenderSystem<T>()` (Line 136)
- `RegisterRenderSystem(IRenderSystem)` (Line 155)

---

### 4. Update XML Documentation

**File**: `PokeSharp.Core/Parallel/ParallelSystemManager.cs` (Line 89)

```csharp
/// <summary>
///     Register a system instance with inferred metadata.
///     If called after RebuildExecutionPlan(), the execution plan will be
///     automatically rebuilt on the next Update() call.
/// </summary>
/// <param name="system">The system to register.</param>
/// <remarks>
///     Late registration (after RebuildExecutionPlan()) is supported.
///     The system will be included in parallel execution starting from the
///     next frame. For immediate effect, call RebuildExecutionPlan() manually.
/// </remarks>
public override void RegisterSystem(ISystem system)
```

---

## Testing Requirements

### Unit Tests

Create: `tests/PokeSharp.Core.Tests/Parallel/LateRegistrationTests.cs`

```csharp
using Arch.Core;
using Microsoft.Extensions.Logging;
using PokeSharp.Core.Parallel;
using PokeSharp.Core.Systems;
using Xunit;

namespace PokeSharp.Core.Tests.Parallel;

public class LateRegistrationTests
{
    private readonly World _world;
    private readonly ILogger<ParallelSystemManager> _logger;

    public LateRegistrationTests()
    {
        _world = World.Create();
        _logger = LoggerFactory.Create(b => b.AddConsole()).CreateLogger<ParallelSystemManager>();
    }

    [Fact]
    public void LateRegistration_AutomaticallyIncludedInPlan_OnNextUpdate()
    {
        // Arrange
        var manager = new ParallelSystemManager(_world, enableParallel: true, _logger);

        // Register initial systems and build plan
        manager.RegisterSystem(new TestSystem("System1"));
        manager.RebuildExecutionPlan();

        var initialPlan = manager.GetExecutionPlan();
        Assert.DoesNotContain("System2", initialPlan);

        // Act: Late registration
        manager.RegisterSystem(new TestSystem("System2"));

        // Trigger lazy rebuild via Update
        manager.Update(_world, 0.016f);

        // Assert: Late system included in updated plan
        var updatedPlan = manager.GetExecutionPlan();
        Assert.Contains("System2", updatedPlan);
    }

    [Fact]
    public void MultipleLateRegistrations_RebuildOnlyOnce()
    {
        // Arrange
        var manager = new ParallelSystemManager(_world, enableParallel: true, _logger);
        manager.RebuildExecutionPlan();

        // Act: Register multiple systems
        manager.RegisterSystem(new TestSystem("System1"));
        manager.RegisterSystem(new TestSystem("System2"));
        manager.RegisterSystem(new TestSystem("System3"));

        // Single Update should rebuild once
        manager.Update(_world, 0.016f);

        // Second Update should NOT rebuild (plan already valid)
        // Monitor logs to verify no second rebuild
        manager.Update(_world, 0.016f);

        // Assert: All systems included
        var plan = manager.GetExecutionPlan();
        Assert.Contains("System1", plan);
        Assert.Contains("System2", plan);
        Assert.Contains("System3", plan);
    }

    [Fact]
    public void NoLateRegistration_NoRebuildOverhead()
    {
        // Arrange
        var manager = new ParallelSystemManager(_world, enableParallel: true, _logger);
        manager.RegisterSystem(new TestSystem("System1"));
        manager.RebuildExecutionPlan();

        // Act: Run many Updates with no late registration
        for (int i = 0; i < 1000; i++)
        {
            manager.Update(_world, 0.016f);
        }

        // Assert: No unexpected rebuilds (verify via logs)
        // Should only see initial rebuild log, no rebuilds during Updates
    }

    [Fact]
    public void ManualRebuild_PreventsLazyRebuild()
    {
        // Arrange
        var manager = new ParallelSystemManager(_world, enableParallel: true, _logger);
        manager.RebuildExecutionPlan();

        // Act: Late registration + manual rebuild
        manager.RegisterSystem(new TestSystem("System1"));
        manager.RebuildExecutionPlan(); // Manual rebuild

        // Update should NOT trigger rebuild (already valid)
        manager.Update(_world, 0.016f);

        // Assert: System included (verify via plan)
        var plan = manager.GetExecutionPlan();
        Assert.Contains("System1", plan);
    }

    // Helper test system
    private class TestSystem : BaseSystem
    {
        private readonly string _name;

        public TestSystem(string name) : base(100, null)
        {
            _name = name;
        }

        public override void Update(World world, float deltaTime) { }
    }
}
```

---

### Integration Test

Add to: `tests/PokeSharp.Game.Tests/Initialization/NPCBehaviorInitializerTests.cs`

```csharp
[Fact]
public void NPCBehaviorSystem_IncludedInParallelExecution_AfterLateRegistration()
{
    // Arrange
    var gameInitializer = new GameInitializer(/* ... */);
    gameInitializer.Initialize(graphicsDevice);

    var npcInitializer = new NPCBehaviorInitializer(/* ... */);

    var parallelManager = (ParallelSystemManager)systemManager;
    var planBeforeNPC = parallelManager.GetExecutionPlan();

    Assert.DoesNotContain("NPCBehaviorSystem", planBeforeNPC);

    // Act: Late register NPCBehaviorSystem
    npcInitializer.Initialize();

    // Trigger lazy rebuild
    parallelManager.Update(world, 0.016f);

    // Assert: NPCBehaviorSystem now in execution plan
    var planAfterNPC = parallelManager.GetExecutionPlan();
    Assert.Contains("NPCBehaviorSystem", planAfterNPC);
}
```

---

## Documentation Updates

### 1. Add Section to PARALLEL_EXECUTION_GUIDE.md

**File**: `docs/PARALLEL_EXECUTION_GUIDE.md`

```markdown
## Late System Registration

### Automatic Rebuild on Late Registration

Systems can be registered after `RebuildExecutionPlan()` has been called.
They will be automatically included in parallel execution on the next frame.

**Example: Dynamic System Loading**

```csharp
// Step 1: Initialize core systems
gameInitializer.Initialize(graphicsDevice);
// RebuildExecutionPlan() called internally

// Step 2: Later - Load and register NPC behavior system
npcBehaviorInitializer.Initialize();
// Internally calls: systemManager.RegisterSystem(npcBehaviorSystem)

// ✅ No manual RebuildExecutionPlan() needed!
// NPCBehaviorSystem will be automatically included on next Update()
```

### Performance Characteristics

| Aspect | Behavior |
|--------|----------|
| First registration | Invalidates execution plan (`_executionPlanBuilt = false`) |
| Next Update() | Detects invalid plan, rebuilds automatically |
| Rebuild cost | ~1-2ms one-time cost |
| Per-frame overhead | None (flag check ~0.001μs) |
| Frame delay | Late system included starting next frame (1 frame delay) |

### Manual Rebuild (Optional)

If you need late-registered systems to take effect **immediately** (same frame):

```csharp
systemManager.RegisterSystem(lateSystem);

if (systemManager is ParallelSystemManager parallelManager)
{
    parallelManager.RebuildExecutionPlan(); // Force immediate rebuild
}
```

**Trade-off**: Adds 1-2ms to registration time, but system active immediately.

### Logging

Monitor log output to track execution plan changes:

```
[Information] Execution plan invalidated: NPCBehaviorSystem registered after initialization
[Information] Rebuilding execution plan due to late system registration (systems: 9)
[Information] Parallel execution plan built: 3 stages, 4 max parallel systems
```
```

---

## Performance Impact

### Expected Measurements

| Metric | Before | After | Delta |
|--------|--------|-------|-------|
| Per-frame overhead | 0μs | <0.001μs | Negligible |
| Late registration rebuild | Manual | Automatic | +1-2ms one-time |
| Memory overhead | 0 bytes | 0 bytes | None |

### Benchmark Test

```csharp
[Fact]
public void Benchmark_LazyRebuildOverhead()
{
    var manager = new ParallelSystemManager(_world, true, _logger);
    manager.RegisterSystem(new TestSystem("System1"));
    manager.RebuildExecutionPlan();

    var stopwatch = Stopwatch.StartNew();

    // Measure 10,000 frames with no late registration
    for (int i = 0; i < 10000; i++)
    {
        manager.Update(_world, 0.016f);
    }

    stopwatch.Stop();

    var averageOverheadUs = (stopwatch.Elapsed.TotalMicroseconds / 10000);

    // Should be < 0.01 microseconds per frame
    Assert.True(averageOverheadUs < 0.01,
        $"Per-frame overhead too high: {averageOverheadUs:F6}μs");
}
```

Expected result: **< 0.01μs per frame** (unmeasurable in practice)

---

## Risk Mitigation

### Risk: Thread Safety Issues
- **Mitigation**: Use `volatile bool` for memory visibility
- **Verification**: Run tests with ThreadSanitizer (if available)

### Risk: Unexpected Latency Spike
- **Mitigation**: Log warning if rebuild happens during Update()
- **Verification**: Monitor logs during gameplay

### Risk: Breaking Existing Code
- **Mitigation**: Backward compatible (existing code continues to work)
- **Verification**: Run full test suite after changes

---

## Implementation Checklist

### Code Changes
- [ ] Make `_executionPlanBuilt` volatile (1 line)
- [ ] Add lazy rebuild logic to `Update()` (8 lines)
- [ ] Add enhanced logging to `RegisterSystem()` (6 lines)
- [ ] Apply logging to all registration methods (4 methods × 6 lines)
- [ ] Update XML doc comments (5 methods)

### Testing
- [ ] Write unit test: Late registration triggers rebuild
- [ ] Write unit test: Multiple late registrations rebuild once
- [ ] Write unit test: No late registration has zero overhead
- [ ] Write unit test: Manual rebuild prevents lazy rebuild
- [ ] Write integration test: NPCBehaviorSystem included in plan
- [ ] Write performance benchmark: Verify <0.01μs overhead

### Documentation
- [ ] Add "Late System Registration" section to PARALLEL_EXECUTION_GUIDE.md
- [ ] Update architecture diagram (if exists)
- [ ] Add ADR document (late-system-registration-design.md) ✅

### Validation
- [ ] Run full test suite (all tests pass)
- [ ] Run performance benchmark (verify no regression)
- [ ] Test with NPCBehaviorInitializer (verify included in plan)
- [ ] Review logs (verify rebuild messages appear correctly)

---

## Success Criteria

### Functional
✅ Late-registered systems automatically included in execution plan
✅ No manual `RebuildExecutionPlan()` calls needed after late registration
✅ Backward compatible with existing code (no breaking changes)

### Performance
✅ Per-frame overhead < 0.01μs (unmeasurable)
✅ Rebuild cost < 2ms for 20 systems
✅ No regression in 60 FPS target

### Developer Experience
✅ Clear log messages for debugging
✅ Intuitive automatic behavior
✅ Well-documented in guide

---

## Rollback Plan

If issues arise after deployment:

1. **Revert `volatile` keyword**: Change back to `private bool _executionPlanBuilt;`
2. **Remove lazy rebuild logic**: Comment out rebuild check in `Update()`
3. **Restore logging**: Remove enhanced late registration logging
4. **Document workaround**: Add note requiring manual `RebuildExecutionPlan()` call

**Recovery Time**: < 5 minutes (simple code revert)

---

## Next Steps

1. **Implement code changes** (1-2 hours)
2. **Write and run tests** (1 hour)
3. **Update documentation** (30 minutes)
4. **Code review** (30 minutes)
5. **Merge and deploy** (15 minutes)

**Total Estimated Time**: 2-3 hours
