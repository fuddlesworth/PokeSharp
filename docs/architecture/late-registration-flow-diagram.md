# Late System Registration Flow Diagram

## Problem: Current Flow (NPCBehaviorSystem Excluded)

```
┌─────────────────────────────────────────────────────────────────────┐
│ GameInitializer.Initialize()                                        │
├─────────────────────────────────────────────────────────────────────┤
│                                                                     │
│  1. Register 8 Systems                                              │
│     ├─ SpatialHashSystem                                            │
│     ├─ PoolCleanupSystem                                            │
│     ├─ InputSystem                                                  │
│     ├─ MovementSystem                                               │
│     ├─ CollisionSystem                                              │
│     ├─ AnimationSystem                                              │
│     ├─ CameraFollowSystem                                           │
│     ├─ TileAnimationSystem                                          │
│     └─ ZOrderRenderSystem                                           │
│        └─> Each sets _executionPlanBuilt = false                    │
│                                                                     │
│  2. _systemManager.Initialize(_world)                               │
│                                                                     │
│  3. RebuildExecutionPlan() [if ParallelSystemManager]               │
│     ├─ Analyzes 8 systems                                           │
│     ├─ Computes execution stages                                    │
│     └─> Sets _executionPlanBuilt = true                             │
│                                                                     │
│  ✅ Execution plan built with 8 systems                             │
│                                                                     │
└─────────────────────────────────────────────────────────────────────┘
                              │
                              ▼
┌─────────────────────────────────────────────────────────────────────┐
│ NPCBehaviorInitializer.Initialize() [LATER - SEPARATE FLOW]        │
├─────────────────────────────────────────────────────────────────────┤
│                                                                     │
│  1. Load behavior definitions                                       │
│  2. Compile behavior scripts                                        │
│  3. Create NPCBehaviorSystem                                        │
│  4. systemManager.RegisterSystem(npcBehaviorSystem)                 │
│     └─> Sets _executionPlanBuilt = false                            │
│                                                                     │
│  ❌ RebuildExecutionPlan() NOT CALLED!                              │
│                                                                     │
└─────────────────────────────────────────────────────────────────────┘
                              │
                              ▼
┌─────────────────────────────────────────────────────────────────────┐
│ Game Loop - Update()                                                │
├─────────────────────────────────────────────────────────────────────┤
│                                                                     │
│  Check: !_executionPlanBuilt ?                                      │
│  ├─> FALSE (plan was built, but invalidated by late registration)  │
│  │                                                                  │
│  Check: _executionStages == null ?                                  │
│  ├─> FALSE (stages exist, but for 8 systems, not 9)                │
│  │                                                                  │
│  Execute parallel stages:                                           │
│  ├─ Stage 1: [SpatialHashSystem, PoolCleanupSystem, InputSystem]   │
│  ├─ Stage 2: [MovementSystem, CollisionSystem]                     │
│  └─ Stage 3: [AnimationSystem, TileAnimationSystem, RenderSystem]  │
│                                                                     │
│  ❌ NPCBehaviorSystem NOT in any stage - RUNS SEQUENTIALLY!         │
│                                                                     │
└─────────────────────────────────────────────────────────────────────┘
```

**Result**: NPCBehaviorSystem excluded from parallel execution

---

## Solution: Lazy Rebuild (Option A)

```
┌─────────────────────────────────────────────────────────────────────┐
│ GameInitializer.Initialize()                                        │
├─────────────────────────────────────────────────────────────────────┤
│                                                                     │
│  1. Register 8 Systems                                              │
│     └─> Each sets _executionPlanBuilt = false                       │
│                                                                     │
│  2. _systemManager.Initialize(_world)                               │
│                                                                     │
│  3. RebuildExecutionPlan() [if ParallelSystemManager]               │
│     └─> Sets _executionPlanBuilt = true ✅                          │
│                                                                     │
│  State: _executionPlanBuilt = true, 8 systems in plan              │
│                                                                     │
└─────────────────────────────────────────────────────────────────────┘
                              │
                              ▼
┌─────────────────────────────────────────────────────────────────────┐
│ NPCBehaviorInitializer.Initialize() [LATER]                        │
├─────────────────────────────────────────────────────────────────────┤
│                                                                     │
│  1. Load behavior definitions                                       │
│  2. Compile behavior scripts                                        │
│  3. Create NPCBehaviorSystem                                        │
│  4. systemManager.RegisterSystem(npcBehaviorSystem)                 │
│     ├─> bool wasBuilt = _executionPlanBuilt  (true)                │
│     ├─> _executionPlanBuilt = false  ⚠️ INVALIDATE PLAN            │
│     └─> Log: "Execution plan invalidated: NPCBehaviorSystem..."    │
│                                                                     │
│  State: _executionPlanBuilt = false, 9 systems registered          │
│                                                                     │
└─────────────────────────────────────────────────────────────────────┘
                              │
                              ▼
┌─────────────────────────────────────────────────────────────────────┐
│ Game Loop - Update() [NEXT FRAME]                                  │
├─────────────────────────────────────────────────────────────────────┤
│                                                                     │
│  NEW: Lazy Rebuild Check                                            │
│  ┌─────────────────────────────────────────────────────────┐       │
│  │ if (_parallelEnabled && !_executionPlanBuilt)          │       │
│  │ {                                                       │       │
│  │     Log: "Rebuilding execution plan due to late..."    │       │
│  │     RebuildExecutionPlan();  ← AUTOMATIC REBUILD       │       │
│  │ }                                                       │       │
│  └─────────────────────────────────────────────────────────┘       │
│                                                                     │
│  RebuildExecutionPlan():                                            │
│  ├─ Collects all 9 systems (Update + Render + Legacy)              │
│  ├─ Computes new execution stages                                  │
│  └─> Sets _executionPlanBuilt = true ✅                             │
│                                                                     │
│  Execute NEW parallel stages:                                       │
│  ├─ Stage 1: [SpatialHashSystem, PoolCleanupSystem, InputSystem]   │
│  ├─ Stage 2: [MovementSystem, CollisionSystem, NPCBehaviorSystem]  │
│  └─ Stage 3: [AnimationSystem, TileAnimationSystem, RenderSystem]  │
│                                                                     │
│  ✅ NPCBehaviorSystem NOW INCLUDED IN PARALLEL EXECUTION!           │
│                                                                     │
│  State: _executionPlanBuilt = true, 9 systems in plan              │
│                                                                     │
└─────────────────────────────────────────────────────────────────────┘
                              │
                              ▼
┌─────────────────────────────────────────────────────────────────────┐
│ Subsequent Updates (Frames 2, 3, 4, ...)                           │
├─────────────────────────────────────────────────────────────────────┤
│                                                                     │
│  NEW: Lazy Rebuild Check                                            │
│  ┌─────────────────────────────────────────────────────────┐       │
│  │ if (_parallelEnabled && !_executionPlanBuilt)          │       │
│  │ {                                                       │       │
│  │     // NOT EXECUTED - _executionPlanBuilt is true ✅   │       │
│  │ }                                                       │       │
│  └─────────────────────────────────────────────────────────┘       │
│                                                                     │
│  ⚡ Skip rebuild check in ~1-2 CPU cycles                           │
│  ⚡ Per-frame overhead: <0.001μs (unmeasurable)                     │
│                                                                     │
│  Execute parallel stages (same as before):                          │
│  ├─ Stage 1: [3 systems in parallel]                               │
│  ├─ Stage 2: [3 systems in parallel] ← NPCBehaviorSystem here      │
│  └─ Stage 3: [3 systems in parallel]                               │
│                                                                     │
└─────────────────────────────────────────────────────────────────────┘
```

**Result**: NPCBehaviorSystem automatically included in parallel execution with 1 frame delay

---

## Performance Timeline

```
Timeline of Events:
═══════════════════════════════════════════════════════════════════════

Frame 0: Initialization
├─ Register 8 systems
├─ RebuildExecutionPlan() [~1-2ms]
└─ _executionPlanBuilt = true

Time: T+0ms
════════════════════════════════════════════════════════════════════════

Frame 1-N: Normal Operation (Before Late Registration)
├─ Update() checks _executionPlanBuilt == true ✅
├─ Skips rebuild (1-2 CPU cycles, <0.001μs)
└─ Executes parallel stages

Time: T+16ms per frame (60 FPS)
Overhead: <0.001μs per frame
════════════════════════════════════════════════════════════════════════

Event: Late Registration (Between frames)
├─ NPCBehaviorInitializer.Initialize()
├─ RegisterSystem(npcBehaviorSystem)
└─ _executionPlanBuilt = false ⚠️ INVALIDATED

Time: T+Xms (during initialization, not frame budget)
════════════════════════════════════════════════════════════════════════

Frame N+1: First Update After Late Registration
├─ Update() checks _executionPlanBuilt == false ⚠️
├─ Triggers RebuildExecutionPlan() [~1-2ms ONE-TIME COST]
├─ _executionPlanBuilt = true ✅
└─ Executes NEW parallel stages (9 systems)

Time: T+16ms + 1-2ms rebuild = 18ms
Impact: Still under 60 FPS budget (16.67ms × 1.2 = 20ms)
════════════════════════════════════════════════════════════════════════

Frame N+2, N+3, ... : Normal Operation (After Rebuild)
├─ Update() checks _executionPlanBuilt == true ✅
├─ Skips rebuild (1-2 CPU cycles, <0.001μs)
└─ Executes parallel stages (9 systems)

Time: T+16ms per frame (60 FPS)
Overhead: <0.001μs per frame
════════════════════════════════════════════════════════════════════════
```

---

## State Machine Diagram

```
┌─────────────────────────────────────────────────────────────────────┐
│                      State Machine: _executionPlanBuilt             │
└─────────────────────────────────────────────────────────────────────┘

                    ┌──────────────────────┐
                    │                      │
          START ──> │  UNBUILT (false)     │
                    │                      │
                    └──────────────────────┘
                             │
                             │ RebuildExecutionPlan()
                             │
                             ▼
                    ┌──────────────────────┐
                    │                      │
                    │   BUILT (true)       │ <──┐ Manual RebuildExecutionPlan()
                    │                      │    │
                    └──────────────────────┘    │
                             │                  │
                             │ RegisterSystem() │
                             │ (Late)           │
                             ▼                  │
                    ┌──────────────────────┐    │
                    │                      │    │
                    │  INVALIDATED (false) │ ───┤
                    │  (Needs Rebuild)     │    │
                    └──────────────────────┘    │
                             │                  │
                             │ Update() detects │
                             │ flag = false     │
                             │                  │
                             │ Auto calls       │
                             │ RebuildExecutionPlan()
                             │                  │
                             └──────────────────┘


State Transitions:

1. START → UNBUILT
   - Initial state after construction

2. UNBUILT → BUILT
   - Trigger: RebuildExecutionPlan() called
   - Action: Compute execution stages, set flag = true

3. BUILT → INVALIDATED
   - Trigger: RegisterSystem() called (late registration)
   - Action: Set flag = false, log invalidation

4. INVALIDATED → BUILT
   - Trigger: Update() detects flag = false
   - Action: Auto-call RebuildExecutionPlan(), set flag = true

5. INVALIDATED → BUILT (Manual)
   - Trigger: Developer explicitly calls RebuildExecutionPlan()
   - Action: Compute execution stages, set flag = true

6. BUILT → BUILT (No-op)
   - Trigger: Update() with no late registration
   - Action: Skip rebuild check (fast path)
```

---

## Comparison: Option A vs Option B vs Option D

```
┌─────────────────────────────────────────────────────────────────────┐
│ Option A: Lazy Rebuild (RECOMMENDED)                               │
├─────────────────────────────────────────────────────────────────────┤
│                                                                     │
│  RegisterSystem(npcBehaviorSystem)                                  │
│  ├─> Set _executionPlanBuilt = false                                │
│  └─> Log: "Plan invalidated..."                                     │
│                                                                     │
│  Next Update():                                                     │
│  ├─> Check: !_executionPlanBuilt ? YES                              │
│  ├─> RebuildExecutionPlan() [~1-2ms]                                │
│  └─> Execute with 9 systems                                         │
│                                                                     │
│  Pros: ✅ Automatic, ✅ Deferred cost, ✅ Batch-friendly             │
│  Cons: ⚠️ 1 frame delay                                             │
│                                                                     │
└─────────────────────────────────────────────────────────────────────┘

┌─────────────────────────────────────────────────────────────────────┐
│ Option B: Immediate Rebuild                                        │
├─────────────────────────────────────────────────────────────────────┤
│                                                                     │
│  RegisterSystem(npcBehaviorSystem)                                  │
│  ├─> Check: _executionPlanBuilt == true ? YES                       │
│  ├─> RebuildExecutionPlan() [~1-2ms] ← IMMEDIATE                    │
│  └─> Log: "Plan rebuilt..."                                         │
│                                                                     │
│  Next Update():                                                     │
│  ├─> Check: !_executionPlanBuilt ? NO                               │
│  └─> Execute with 9 systems                                         │
│                                                                     │
│  Pros: ✅ Immediate consistency, ✅ No frame delay                   │
│  Cons: ❌ Registration latency, ❌ Multiple rebuilds for batch       │
│                                                                     │
└─────────────────────────────────────────────────────────────────────┘

┌─────────────────────────────────────────────────────────────────────┐
│ Option D: Manual Rebuild (Current)                                 │
├─────────────────────────────────────────────────────────────────────┤
│                                                                     │
│  RegisterSystem(npcBehaviorSystem)                                  │
│  └─> Set _executionPlanBuilt = false                                │
│                                                                     │
│  Developer must call:                                               │
│  ├─> parallelManager.RebuildExecutionPlan()                         │
│  └─> (Easy to forget! ❌)                                           │
│                                                                     │
│  Next Update():                                                     │
│  ├─> Check: !_executionPlanBuilt ? IF FORGOTTEN, RUNS SEQUENTIAL   │
│  └─> Execute (with or without new system, depending on above)      │
│                                                                     │
│  Pros: ✅ Explicit control, ✅ Efficient batch registration          │
│  Cons: ❌ Error-prone, ❌ Not discoverable, ❌ Inconsistent          │
│                                                                     │
└─────────────────────────────────────────────────────────────────────┘

Performance Comparison (10 late registrations):

Option A (Lazy):
  ├─ RegisterSystem: 10 × <0.1ms = <1ms total
  ├─ First Update: 1 × 2ms rebuild = 2ms
  └─ Total: ~3ms

Option B (Immediate):
  ├─ RegisterSystem: 10 × 2ms = 20ms total
  ├─ First Update: 0ms (already built)
  └─ Total: ~20ms

Option D (Manual):
  ├─ RegisterSystem: 10 × <0.1ms = <1ms total
  ├─ Manual rebuild: 1 × 2ms = 2ms
  └─ Total: ~3ms (IF REMEMBERED)

Winner: Option A (same performance as D, but automatic!)
```

---

## Edge Cases Handled

### Edge Case 1: Multiple Late Registrations

```
Frame N: Late Registration Batch
├─ RegisterSystem(system1) → _executionPlanBuilt = false
├─ RegisterSystem(system2) → _executionPlanBuilt = false (already false, no-op)
├─ RegisterSystem(system3) → _executionPlanBuilt = false (already false, no-op)
└─ RegisterSystem(system4) → _executionPlanBuilt = false (already false, no-op)

Frame N+1: First Update
├─ Check: !_executionPlanBuilt ? TRUE
├─ RebuildExecutionPlan() [ONE REBUILD for all 4 systems]
└─ Execute with 12 systems (8 original + 4 late)

Result: ✅ Efficient batch rebuild
```

### Edge Case 2: Registration During Update()

```
Thread A (Update):                Thread B (Registration):
├─ Check: !_executionPlanBuilt
│  └─> false (plan valid)        ├─ RegisterSystem(system)
│                                 └─> _executionPlanBuilt = false
├─ Execute with current plan
│  (continues with old plan)
└─ Frame complete

Next Frame:
├─ Check: !_executionPlanBuilt
│  └─> true (detected change)
├─ RebuildExecutionPlan()
└─ Execute with new plan

Result: ✅ Safe - new system included in next frame
```

### Edge Case 3: Manual Rebuild After Late Registration

```
RegisterSystem(npcBehaviorSystem)
└─> _executionPlanBuilt = false

Developer calls:
RebuildExecutionPlan()
└─> _executionPlanBuilt = true

Next Update():
├─ Check: !_executionPlanBuilt ? FALSE (flag is true)
└─> Skip lazy rebuild (manual rebuild already happened)

Result: ✅ Manual rebuild takes precedence, no duplicate rebuild
```

---

## Summary

**Recommended Solution**: Option A (Lazy Rebuild)

**Key Benefits**:
- ✅ Automatic - no developer action required
- ✅ Efficient - one rebuild for multiple late registrations
- ✅ Safe - volatile flag ensures thread visibility
- ✅ Fast - <0.001μs per-frame overhead in normal operation
- ✅ Compatible - existing code continues to work

**Implementation Effort**: 2-3 hours
**Risk Level**: Low
**Backward Compatibility**: Full

**Next Steps**: See `late-registration-implementation-plan.md` for detailed checklist
