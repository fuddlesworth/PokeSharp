# Parallel Execution Flow - Visual Diagram

## Current (Broken) Flow

```
┌─────────────────────────────────────────────────────────────┐
│                    GameInitializer                          │
└─────────────────────────────────────────────────────────────┘
                              │
                              ▼
┌─────────────────────────────────────────────────────────────┐
│  _systemManager.RegisterUpdateSystem(MovementSystem)        │
└─────────────────────────────────────────────────────────────┘
                              │
                              ▼
┌─────────────────────────────────────────────────────────────┐
│              SystemManager (Base Class)                     │
│  ┌──────────────────────────────────────────────┐          │
│  │  RegisterUpdateSystem(IUpdateSystem)         │          │
│  │  - Adds to _updateSystems list      ✓       │          │
│  │  - Adds to _systems list            ✓       │          │
│  │  - Initializes metrics              ✓       │          │
│  └──────────────────────────────────────────────┘          │
└─────────────────────────────────────────────────────────────┘
                              │
                              │ (NOT overridden in ParallelSystemManager!)
                              ▼
┌─────────────────────────────────────────────────────────────┐
│           ParallelSystemManager                             │
│  ┌──────────────────────────────────────────────┐          │
│  │  RegisterSystem(ISystem) override            │          │
│  │  - Extracts metadata                         │          │
│  │  - Calls _dependencyGraph.RegisterSystem()   │          │
│  │  - But this is NEVER called! ✗              │          │
│  └──────────────────────────────────────────────┘          │
│                                                              │
│  ┌──────────────────────────────────────────────┐          │
│  │  RebuildExecutionPlan()                      │          │
│  │  var systemTypes = Systems.Select(...)       │          │
│  │  ↓                                            │          │
│  │  _dependencyGraph.ComputeExecutionStages()   │          │
│  │  ↓                                            │          │
│  │  Filters to _systemMetadata.ContainsKey()    │          │
│  │  ↓                                            │          │
│  │  NO SYSTEMS FOUND! ✗                        │          │
│  │  Returns 0 stages ✗                         │          │
│  └──────────────────────────────────────────────┘          │
└─────────────────────────────────────────────────────────────┘
                              │
                              ▼
                    ❌ 0 EXECUTION STAGES
                    ❌ PARALLEL EXECUTION DISABLED
```

## Fixed Flow

```
┌─────────────────────────────────────────────────────────────┐
│                    GameInitializer                          │
└─────────────────────────────────────────────────────────────┘
                              │
                              ▼
┌─────────────────────────────────────────────────────────────┐
│  _systemManager.RegisterUpdateSystem(MovementSystem)        │
└─────────────────────────────────────────────────────────────┘
                              │
                              ▼
┌─────────────────────────────────────────────────────────────┐
│           ParallelSystemManager (NEW OVERRIDE)              │
│  ┌──────────────────────────────────────────────┐          │
│  │  RegisterUpdateSystem(IUpdateSystem) NEW!    │          │
│  │  ├─ Extract IParallelSystemMetadata  ✓      │          │
│  │  ├─ Create SystemMetadata object     ✓      │          │
│  │  ├─ Register with _dependencyGraph   ✓      │          │
│  │  ├─ Call base.RegisterUpdateSystem() ✓      │          │
│  │  └─ Invalidate execution plan        ✓      │          │
│  └──────────────────────────────────────────────┘          │
└─────────────────────────────────────────────────────────────┘
                              │
                              ▼
┌─────────────────────────────────────────────────────────────┐
│              SystemManager (Base Class)                     │
│  ┌──────────────────────────────────────────────┐          │
│  │  RegisterUpdateSystem(IUpdateSystem)         │          │
│  │  - Adds to _updateSystems list      ✓       │          │
│  │  - Adds to _systems list            ✓       │          │
│  │  - Initializes metrics              ✓       │          │
│  └──────────────────────────────────────────────┘          │
│                                                              │
│  ┌──────────────────────────────────────────────┐          │
│  │  NEW: Protected Properties                   │          │
│  │  - UpdateSystems (IReadOnlyList)    ✓       │          │
│  │  - RenderSystems (IReadOnlyList)    ✓       │          │
│  │  - TrackSystemPerformance (protected) ✓     │          │
│  └──────────────────────────────────────────────┘          │
└─────────────────────────────────────────────────────────────┘
                              │
                              ▼
┌─────────────────────────────────────────────────────────────┐
│           ParallelSystemManager                             │
│  ┌──────────────────────────────────────────────┐          │
│  │  RebuildExecutionPlan() UPDATED!             │          │
│  │  ├─ Get UpdateSystems from base     ✓       │          │
│  │  ├─ Get RenderSystems from base     ✓       │          │
│  │  ├─ Get legacy Systems              ✓       │          │
│  │  ├─ Merge and deduplicate           ✓       │          │
│  │  ├─ ComputeExecutionStages()        ✓       │          │
│  │  └─ ALL SYSTEMS INCLUDED!           ✓       │          │
│  └──────────────────────────────────────────────┘          │
│                                                              │
│  ┌──────────────────────────────────────────────┐          │
│  │  _dependencyGraph                            │          │
│  │  ├─ MovementSystem metadata         ✓       │          │
│  │  ├─ AnimationSystem metadata        ✓       │          │
│  │  ├─ CameraFollowSystem metadata     ✓       │          │
│  │  └─ ZOrderRenderSystem metadata     ✓       │          │
│  └──────────────────────────────────────────────┘          │
└─────────────────────────────────────────────────────────────┘
                              │
                              ▼
                    ✅ MULTIPLE EXECUTION STAGES
                    ✅ PARALLEL EXECUTION ENABLED
```

## Execution Stage Building Process

### Before Fix
```
RebuildExecutionPlan()
    │
    ├─ var systemTypes = Systems.Select(s => s.GetType())
    │  └─ Systems contains: [MovementSystem, AnimationSystem, ...]  ✓
    │
    ├─ _dependencyGraph.ComputeExecutionStages(systemTypes)
    │  │
    │  └─ registeredSystems = systemTypes.Where(s => _systemMetadata.ContainsKey(s))
    │     └─ _systemMetadata is EMPTY! ✗
    │        (Because RegisterSystem override was never called)
    │
    └─ Result: [] (empty list)
       └─ 0 stages ✗
```

### After Fix
```
RebuildExecutionPlan()
    │
    ├─ var systemTypes = []
    │  ├─ Add UpdateSystems.Select(s => s.GetType())  ✓
    │  ├─ Add RenderSystems.Select(s => s.GetType())  ✓
    │  └─ Add legacy Systems (not IUpdate/IRender)     ✓
    │
    ├─ systemTypes.Distinct() (remove duplicates)      ✓
    │
    ├─ _dependencyGraph.ComputeExecutionStages(systemTypes)
    │  │
    │  └─ registeredSystems = systemTypes.Where(s => _systemMetadata.ContainsKey(s))
    │     └─ _systemMetadata contains ALL systems! ✓
    │        (Because RegisterUpdateSystem/RegisterRenderSystem captured metadata)
    │
    └─ Result: [[InputSystem], [MovementSystem, AnimationSystem], [RenderSystem]]
       └─ 3 stages with parallel execution! ✓
```

## Data Flow per System Registration

### MovementSystem Example

```
┌─────────────────────────────────────────────────────────────┐
│  RegisterUpdateSystem<MovementSystem>()                     │
└─────────────────────────────────────────────────────────────┘
                              │
                              ▼
┌─────────────────────────────────────────────────────────────┐
│  1. Create instance via SystemFactory                       │
│     var system = _systemFactory.CreateSystem<MovementSystem>()
│     ✓ Dependencies injected (SpatialHashSystem, logger)     │
└─────────────────────────────────────────────────────────────┘
                              │
                              ▼
┌─────────────────────────────────────────────────────────────┐
│  2. RegisterUpdateSystem(MovementSystem instance)           │
└─────────────────────────────────────────────────────────────┘
                              │
                              ▼
┌─────────────────────────────────────────────────────────────┐
│  3. Extract Metadata (MovementSystem : IParallelSystemMetadata)
│     metadata = {                                             │
│       SystemType: typeof(MovementSystem),                   │
│       ReadsComponents: [Position, GridMovement, Animation,  │
│                         MovementRequest, MapInfo],          │
│       WritesComponents: [Position, GridMovement,            │
│                          Animation, MovementRequest],       │
│       Priority: 100,                                        │
│       AllowsParallelExecution: true                         │
│     }                                                        │
└─────────────────────────────────────────────────────────────┘
                              │
                              ▼
┌─────────────────────────────────────────────────────────────┐
│  4. Register with DependencyGraph                           │
│     _dependencyGraph.RegisterSystem<MovementSystem>(metadata)
│     ✓ Stored in _systemMetadata[MovementSystem]            │
│     ✓ Built _readDependencies lookup                        │
│     ✓ Built _writeDependencies lookup                       │
└─────────────────────────────────────────────────────────────┘
                              │
                              ▼
┌─────────────────────────────────────────────────────────────┐
│  5. Call base.RegisterUpdateSystem(system)                  │
│     ✓ Added to _updateSystems                              │
│     ✓ Added to _systems (backward compat)                  │
│     ✓ Metrics initialized                                   │
└─────────────────────────────────────────────────────────────┘
                              │
                              ▼
┌─────────────────────────────────────────────────────────────┐
│  6. Invalidate execution plan                               │
│     _executionPlanBuilt = false                             │
│     (Will be rebuilt before first Update)                   │
└─────────────────────────────────────────────────────────────┘
```

## Dependency Analysis Flow

```
┌─────────────────────────────────────────────────────────────┐
│  ComputeExecutionStages([MovementSystem, AnimationSystem,   │
│                          CameraFollowSystem, RenderSystem]) │
└─────────────────────────────────────────────────────────────┘
                              │
                              ▼
┌─────────────────────────────────────────────────────────────┐
│  1. Filter to registered systems                            │
│     (all 4 systems have metadata ✓)                        │
└─────────────────────────────────────────────────────────────┘
                              │
                              ▼
┌─────────────────────────────────────────────────────────────┐
│  2. Sort by priority                                        │
│     [MovementSystem(100), AnimationSystem(800),             │
│      CameraFollowSystem(825), RenderSystem(1000)]           │
└─────────────────────────────────────────────────────────────┘
                              │
                              ▼
┌─────────────────────────────────────────────────────────────┐
│  3. Greedy stage assignment                                 │
│                                                              │
│  Stage 1:                                                   │
│    - MovementSystem (no conflicts)                          │
│                                                              │
│  Stage 2:                                                   │
│    - AnimationSystem                                        │
│      Conflict check vs MovementSystem:                      │
│      Writes: Animation                                      │
│      MovementSystem reads: Animation ✗                     │
│      Cannot run parallel with MovementSystem                │
│                                                              │
│  Stage 3:                                                   │
│    - CameraFollowSystem                                     │
│      Conflict check vs previous stages: ✓                  │
│      Can run parallel with others in this stage            │
│                                                              │
│  Stage 4:                                                   │
│    - RenderSystem                                           │
│      Reads all data, writes nothing                         │
│      Must run after all updates                             │
└─────────────────────────────────────────────────────────────┘
                              │
                              ▼
┌─────────────────────────────────────────────────────────────┐
│  Result: 4 stages                                           │
│    Stage 1: [MovementSystem]                                │
│    Stage 2: [AnimationSystem]                               │
│    Stage 3: [CameraFollowSystem]                            │
│    Stage 4: [RenderSystem]                                  │
└─────────────────────────────────────────────────────────────┘
```

## Parallel Execution at Runtime

```
┌─────────────────────────────────────────────────────────────┐
│  ParallelSystemManager.Update(world, deltaTime)             │
└─────────────────────────────────────────────────────────────┘
                              │
                              ▼
┌─────────────────────────────────────────────────────────────┐
│  Check: _executionPlanBuilt? ✓                             │
│  Check: _parallelEnabled? ✓                                │
│  Check: _executionStages.Count > 0? ✓                      │
└─────────────────────────────────────────────────────────────┘
                              │
                              ▼
┌─────────────────────────────────────────────────────────────┐
│  foreach stage in _executionStages:                         │
│                                                              │
│  ┌────────────────────────────────────────────────┐        │
│  │  Stage 1: [MovementSystem]                     │        │
│  │    Single system → Sequential execution        │        │
│  │    ├─ Start stopwatch                          │        │
│  │    ├─ system.Update(world, deltaTime)          │        │
│  │    ├─ Stop stopwatch                           │        │
│  │    └─ TrackSystemPerformance()                 │        │
│  └────────────────────────────────────────────────┘        │
│                                                              │
│  ┌────────────────────────────────────────────────┐        │
│  │  Stage 2: [AnimationSystem, OtherSystem]       │        │
│  │    Multiple systems → Parallel execution       │        │
│  │                                                 │        │
│  │    Thread 1:                 Thread 2:         │        │
│  │    ┌──────────────┐         ┌──────────────┐  │        │
│  │    │ Animation    │         │ OtherSystem  │  │        │
│  │    │ System       │         │              │  │        │
│  │    │ .Update()    │         │ .Update()    │  │        │
│  │    └──────────────┘         └──────────────┘  │        │
│  │           │                         │          │        │
│  │           └─────────┬───────────────┘          │        │
│  │                     ▼                          │        │
│  │            TrackSystemPerformance()            │        │
│  └────────────────────────────────────────────────┘        │
│                                                              │
│  ┌────────────────────────────────────────────────┐        │
│  │  Stage 3: [CameraFollowSystem]                 │        │
│  │    Single system → Sequential execution        │        │
│  └────────────────────────────────────────────────┘        │
│                                                              │
│  ┌────────────────────────────────────────────────┐        │
│  │  Stage 4: [RenderSystem]                       │        │
│  │    Single system → Sequential execution        │        │
│  └────────────────────────────────────────────────┘        │
└─────────────────────────────────────────────────────────────┘
                              │
                              ▼
                    ✅ ALL SYSTEMS EXECUTED
                    ✅ METRICS TRACKED PER SYSTEM
                    ✅ PARALLEL WHERE SAFE
```

## Key Changes Summary

| Component | Change | Reason |
|-----------|--------|--------|
| SystemManager | Add protected `UpdateSystems` property | Allow ParallelSystemManager to access update systems |
| SystemManager | Add protected `RenderSystems` property | Allow ParallelSystemManager to access render systems |
| SystemManager | Make `TrackSystemPerformance` protected | Allow ParallelSystemManager to track metrics |
| ParallelSystemManager | Override `RegisterUpdateSystem<T>()` | Capture metadata when systems are registered |
| ParallelSystemManager | Override `RegisterUpdateSystem(IUpdateSystem)` | Extract IParallelSystemMetadata from instances |
| ParallelSystemManager | Override `RegisterRenderSystem<T>()` | Capture metadata for render systems |
| ParallelSystemManager | Override `RegisterRenderSystem(IRenderSystem)` | Extract metadata from render system instances |
| ParallelSystemManager | Rewrite `RebuildExecutionPlan()` | Include ALL systems (update + render + legacy) |
| ParallelSystemManager | Update `Update()` method | Use protected TrackSystemPerformance |

## Benefits

1. ✅ **Zero Breaking Changes** - Existing code continues to work
2. ✅ **Automatic Metadata Capture** - No manual registration needed
3. ✅ **Comprehensive Coverage** - All system types included
4. ✅ **Performance Tracking** - Per-system metrics collected
5. ✅ **Safe Parallelism** - Dependency analysis prevents conflicts
6. ✅ **Maintainable** - Clean separation of concerns
