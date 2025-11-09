# PokeSharp Parallel System Architecture

## System Overview

This document provides a visual and technical overview of the parallel execution system architecture.

---

## Component Diagram

```
┌──────────────────────────────────────────────────────────────────────────┐
│                         PokeSharp ECS World                              │
│  ┌────────────────────────────────────────────────────────────────────┐ │
│  │                        Arch.Core.World                             │ │
│  │  - Entity Storage                                                  │ │
│  │  - Component Archetypes                                            │ │
│  │  - Query Engine                                                    │ │
│  └────────────────────────────────────────────────────────────────────┘ │
└────────────────────────────┬─────────────────────────────────────────────┘
                             │
                             │ Manages
                             ▼
┌──────────────────────────────────────────────────────────────────────────┐
│                      ParallelSystemManager                               │
│  ┌────────────────────────────────────────────────────────────────────┐ │
│  │  RESPONSIBILITIES:                                                 │ │
│  │  1. Register systems with metadata                                 │ │
│  │  2. Build dependency graph                                         │ │
│  │  3. Compute execution stages                                       │ │
│  │  4. Execute stages with parallelism                                │ │
│  │  5. Track performance metrics                                      │ │
│  └────────────────────────────────────────────────────────────────────┘ │
│                                                                            │
│  DEPENDENCIES:                                                             │
│  ┌───────────────────┐  ┌────────────────────┐  ┌──────────────────┐   │
│  │ Dependency Graph  │  │ Parallel Executor  │  │ ThreadSafety     │   │
│  └───────────────────┘  └────────────────────┘  └──────────────────┘   │
└──────────────────────────────────────────────────────────────────────────┘
                             │
                             │ Executes
                             ▼
┌──────────────────────────────────────────────────────────────────────────┐
│                        Execution Stages                                  │
│  ┌────────────────────────────────────────────────────────────────────┐ │
│  │ Stage 1: (Parallel)         Stage 2: (Parallel)   Stage 3: (Seq)  │ │
│  │ ┌──────────┐ ┌──────────┐   ┌──────────┐         ┌──────────┐    │ │
│  │ │System A  │ │System B  │   │System C  │         │System D  │    │ │
│  │ │Movement  │ │AI        │   │Collision │         │Render    │    │ │
│  │ └──────────┘ └──────────┘   └──────────┘         └──────────┘    │ │
│  │      ↓            ↓               ↓                    ↓           │ │
│  │  [Thread 1]  [Thread 2]      [Thread 1]           [Main]          │ │
│  └────────────────────────────────────────────────────────────────────┘ │
└──────────────────────────────────────────────────────────────────────────┘
```

---

## Data Flow Diagram

```
┌─────────────────────────────────────────────────────────────────────┐
│                    SYSTEM REGISTRATION PHASE                         │
└─────────────────────────────────────────────────────────────────────┘
     │
     │ 1. Developer Registers System with Metadata
     │    systemManager.RegisterSystemWithMetadata<MovementSystem>(...)
     │
     ▼
┌─────────────────────────────────────────────────────────────────────┐
│  SystemMetadata {                                                    │
│    SystemType: MovementSystem                                        │
│    ReadsComponents: [Velocity]                                       │
│    WritesComponents: [Position]                                      │
│    Priority: 100                                                     │
│    AllowsParallelExecution: true                                     │
│  }                                                                    │
└─────────────────────────────────────────────────────────────────────┘
     │
     │ 2. Add to Dependency Graph
     │
     ▼
┌─────────────────────────────────────────────────────────────────────┐
│  SystemDependencyGraph {                                             │
│    MovementSystem -> Reads: [Velocity], Writes: [Position]          │
│    AISystem       -> Reads: [Position], Writes: [Velocity]          │
│    RenderSystem   -> Reads: [Position], Writes: []                  │
│  }                                                                    │
└─────────────────────────────────────────────────────────────────────┘
     │
     │ 3. Compute Execution Stages (after all systems registered)
     │    systemManager.RebuildExecutionPlan()
     │
     ▼
┌─────────────────────────────────────────────────────────────────────┐
│  Execution Plan:                                                     │
│  Stage 1: [MovementSystem, RenderSystem]  ← Can run in parallel     │
│  Stage 2: [AISystem]                      ← Depends on Stage 1      │
└─────────────────────────────────────────────────────────────────────┘

┌─────────────────────────────────────────────────────────────────────┐
│                    EXECUTION PHASE (Per Frame)                       │
└─────────────────────────────────────────────────────────────────────┘
     │
     │ systemManager.Update(world, deltaTime)
     │
     ▼
┌─────────────────────────────────────────────────────────────────────┐
│  FOR EACH STAGE:                                                     │
│                                                                       │
│  IF (stage.systems.Count > 1):                                       │
│    ┌─────────────────────────────────────────────────────────┐     │
│    │  Parallel.ForEach(stage.systems, system => {            │     │
│    │    system.Update(world, deltaTime);                     │     │
│    │  });                                                     │     │
│    └─────────────────────────────────────────────────────────┘     │
│  ELSE:                                                               │
│    ┌─────────────────────────────────────────────────────────┐     │
│    │  stage.systems[0].Update(world, deltaTime);             │     │
│    └─────────────────────────────────────────────────────────┘     │
│                                                                       │
│  WAIT for stage completion before starting next stage                │
└─────────────────────────────────────────────────────────────────────┘
```

---

## Dependency Analysis Algorithm

```
┌─────────────────────────────────────────────────────────────────────┐
│  FUNCTION: CanRunInParallel(System A, System B)                      │
└─────────────────────────────────────────────────────────────────────┘

INPUT:
  SystemA.ReadsComponents = [Velocity]
  SystemA.WritesComponents = [Position]
  SystemB.ReadsComponents = [Position]
  SystemB.WritesComponents = [Velocity]

CHECKS:

1. Write-Write Conflict?
   ┌────────────────────────────────────────────┐
   │ Intersect(A.Writes, B.Writes)              │
   │ = Intersect([Position], [Velocity])        │
   │ = ∅ (empty)                                │
   │ ✅ NO CONFLICT                             │
   └────────────────────────────────────────────┘

2. Read-Write Conflict?
   ┌────────────────────────────────────────────┐
   │ Intersect(A.Writes, B.Reads) OR            │
   │ Intersect(B.Writes, A.Reads)               │
   │ = Intersect([Position], [Position]) OR     │
   │   Intersect([Velocity], [Velocity])        │
   │ = [Position] OR [Velocity]                 │
   │ ❌ CONFLICT FOUND                          │
   └────────────────────────────────────────────┘

RESULT: Cannot run in parallel (System B depends on System A)

┌─────────────────────────────────────────────────────────────────────┐
│  EXECUTION PLAN:                                                     │
│  Stage 1: [System A]                                                 │
│  Stage 2: [System B]  ← Waits for Stage 1                           │
└─────────────────────────────────────────────────────────────────────┘
```

---

## Thread Safety Validation Flow

```
┌─────────────────────────────────────────────────────────────────────┐
│  COMPONENT ANALYSIS                                                  │
└─────────────────────────────────────────────────────────────────────┘

public struct Position {
    public float X;  // ← Primitive type
    public float Y;  // ← Primitive type
}

ANALYSIS:
  ✅ All fields are primitives
  ✅ No reference types
  ✅ No arrays
  ✅ No collections

RESULT: ✅ Thread-safe for parallel writes

────────────────────────────────────────────────────────────────────────

public struct Enemy {
    public string Name;      // ← Reference type (class)
    public List<Item> Items; // ← Reference type (class)
}

ANALYSIS:
  ❌ Field 'Name' is a reference type (string)
  ❌ Field 'Items' is a reference type (List<Item>)

RESULT: ❌ NOT thread-safe for parallel writes

RECOMMENDATION:
  1. Remove 'Name' field or make it readonly
  2. Replace 'Items' with separate ItemComponent entities
  3. Use fixed-size buffers instead of collections

────────────────────────────────────────────────────────────────────────

SYSTEM VALIDATION:

public class MySystem : ParallelSystemBase {
    public override List<Type> GetWriteComponents() => new() {
        typeof(Enemy)  // ← Not thread-safe!
    };
}

VALIDATION RESULT:
  ❌ System writes non-thread-safe component 'Enemy'
  ❌ Cannot safely run in parallel

ACTION:
  1. Set AllowsParallelExecution = false
  2. OR fix Enemy component to be thread-safe
```

---

## Performance Profiling Architecture

```
┌─────────────────────────────────────────────────────────────────────┐
│  METRICS COLLECTION                                                  │
└─────────────────────────────────────────────────────────────────────┘

FOR EACH SYSTEM UPDATE:

  ┌─────────────────────────────────────────────────────────┐
  │  START:                                                 │
  │    var stopwatch = Stopwatch.StartNew();                │
  │                                                          │
  │  EXECUTE:                                                │
  │    system.Update(world, deltaTime);                     │
  │                                                          │
  │  END:                                                    │
  │    stopwatch.Stop();                                    │
  │                                                          │
  │  RECORD:                                                 │
  │    metrics.UpdateCount++;                               │
  │    metrics.TotalTimeMs += stopwatch.Elapsed.Ms;         │
  │    metrics.LastUpdateMs = stopwatch.Elapsed.Ms;         │
  │    metrics.MaxUpdateMs = Max(current, previous);        │
  └─────────────────────────────────────────────────────────┘

AGGREGATED METRICS:
  ┌──────────────────────────────────────────────┐
  │ System: MovementSystem                       │
  │ Updates: 1000                                │
  │ Total Time: 150ms                            │
  │ Average: 0.15ms                              │
  │ Max: 0.8ms                                   │
  │ % of Frame Budget: 0.9%                      │
  └──────────────────────────────────────────────┘

PARALLEL EXECUTION STATS:
  ┌──────────────────────────────────────────────┐
  │ Total Parallel Queries: 500                  │
  │ Total Entities Processed: 150,000            │
  │ Average Entities/Query: 300                  │
  │ Average Execution Time: 0.25ms               │
  │ Threads Used: 8                              │
  │ Estimated Speedup: 5.6x                      │
  └──────────────────────────────────────────────┘
```

---

## Job System Architecture

```
┌─────────────────────────────────────────────────────────────────────┐
│  JOB SYSTEM (Advanced Usage)                                         │
└─────────────────────────────────────────────────────────────────────┘

var jobSystem = new JobSystem(workerThreads: 8);

PATTERN 1: Independent Jobs
  ┌────────────────────────────────────────────┐
  │ var job1 = jobSystem.Schedule(() => {      │
  │     ProcessPhysics();                      │
  │ });                                        │
  │                                            │
  │ var job2 = jobSystem.Schedule(() => {      │
  │     ProcessAI();                           │
  │ });                                        │
  │                                            │
  │ JobHandle.Combine(job1, job2).Complete();  │
  └────────────────────────────────────────────┘

  Timeline:
  Thread 1: [===Physics===]
  Thread 2: [====AI=====]
            └──────┬──────┘
              Wait for both

PATTERN 2: Dependent Jobs
  ┌────────────────────────────────────────────┐
  │ var jobA = jobSystem.Schedule(() => {      │
  │     UpdatePositions();                     │
  │ });                                        │
  │                                            │
  │ var jobB = jobSystem.ScheduleWithDependency│
  │     (() => UpdateCollisions(), jobA);      │
  │                                            │
  │ jobB.Complete();                           │
  └────────────────────────────────────────────┘

  Timeline:
  Thread 1: [==Positions==][==Collisions==]
                          ↑
                      Waits for jobA

PATTERN 3: Batch Processing
  ┌────────────────────────────────────────────┐
  │ var entities = GetAllEnemies();            │
  │                                            │
  │ var job = jobSystem.ScheduleBatch(         │
  │     entities,                              │
  │     enemy => enemy.UpdateAI(deltaTime)     │
  │ );                                         │
  │                                            │
  │ job.Complete();                            │
  └────────────────────────────────────────────┘

  Timeline:
  Thread 1: [Enemy 0-99]
  Thread 2: [Enemy 100-199]
  Thread 3: [Enemy 200-299]
  Thread 4: [Enemy 300-399]
            └─────┬──────┘
             Parallel execution
```

---

## Memory Layout & Access Patterns

```
┌─────────────────────────────────────────────────────────────────────┐
│  ECS MEMORY LAYOUT (Arch)                                            │
└─────────────────────────────────────────────────────────────────────┘

ARCHETYPE: [Position, Velocity]
┌──────────────────────────────────────────────────────────────────────┐
│  Entity Array:     [E1, E2, E3, E4, E5, ..., En]                    │
│  Position Array:   [P1, P2, P3, P4, P5, ..., Pn]                    │
│  Velocity Array:   [V1, V2, V3, V4, V5, ..., Vn]                    │
└──────────────────────────────────────────────────────────────────────┘

SEQUENTIAL ACCESS:
  ┌────────────────────────────────────────────┐
  │ foreach (entity in query) {                │
  │   Process(entity.Position, entity.Velocity)│
  │ }                                          │
  └────────────────────────────────────────────┘

  Cache-friendly: ✅
  Iterations: n
  CPU Usage: 1 core

PARALLEL ACCESS (Chunked):
  ┌────────────────────────────────────────────┐
  │ Chunk 1: [E1-E250]   → Thread 1           │
  │ Chunk 2: [E251-E500] → Thread 2           │
  │ Chunk 3: [E501-E750] → Thread 3           │
  │ Chunk 4: [E751-E1000]→ Thread 4           │
  └────────────────────────────────────────────┘

  Cache-friendly: ✅ (each thread has contiguous data)
  Iterations: n/4 per thread
  CPU Usage: 4 cores
  Speedup: ~3.2x (80% efficiency)

THREAD-SAFE WRITE PATTERN:
  ┌────────────────────────────────────────────┐
  │ Each chunk is independent:                 │
  │                                            │
  │ Thread 1 writes: P1-P250                   │
  │ Thread 2 writes: P251-P500                 │
  │ Thread 3 writes: P501-P750                 │
  │ Thread 4 writes: P751-P1000                │
  │                                            │
  │ No overlapping writes → Thread-safe! ✅    │
  └────────────────────────────────────────────┘
```

---

## Complete System Flow Example

```
┌─────────────────────────────────────────────────────────────────────┐
│  GAME LOOP EXECUTION (60 FPS)                                        │
└─────────────────────────────────────────────────────────────────────┘

Frame N:
  ├─ Input Processing (sequential)
  │    └─ 0.5ms
  │
  ├─ System Updates (parallel)
  │    │
  │    ├─ Stage 1 (Parallel) ─────────────────┐
  │    │   ├─ MovementSystem   [Thread 1] 1.2ms│
  │    │   ├─ AIBehaviorSystem [Thread 2] 1.5ms├─ MAX: 1.5ms
  │    │   └─ AnimationSystem  [Thread 3] 0.8ms│
  │    │                                       └─
  │    │
  │    ├─ Stage 2 (Parallel) ─────────────────┐
  │    │   ├─ PhysicsSystem    [Thread 1] 2.0ms│
  │    │   └─ ParticleSystem   [Thread 2] 1.8ms├─ MAX: 2.0ms
  │    │                                       └─
  │    │
  │    └─ Stage 3 (Sequential)
  │         └─ RenderSystem     [Main]    5.0ms
  │
  └─ Total: 0.5 + 1.5 + 2.0 + 5.0 = 9.0ms

Performance:
  Frame Time: 9.0ms
  FPS: 111 (target: 60 fps / 16.67ms)
  CPU Usage: ~60% (4 cores utilized)
  Margin: 7.67ms (46% under budget)

WITHOUT PARALLELIZATION:
  Frame Time: 0.5 + 1.2 + 1.5 + 0.8 + 2.0 + 1.8 + 5.0 = 12.8ms
  FPS: 78
  Speedup: 1.42x (12.8ms / 9.0ms)
```

---

## File Dependencies

```
ParallelSystemManager.cs
  ├─ depends on → SystemDependencyGraph.cs
  ├─ depends on → ParallelQueryExecutor.cs
  ├─ depends on → SystemBase.cs (existing)
  └─ extends    → SystemManager.cs (existing)

ParallelSystemBase.cs
  ├─ depends on → ParallelQueryExecutor.cs
  ├─ extends    → SystemBase.cs (existing)
  └─ implements → IParallelSystemMetadata

SystemDependencyGraph.cs
  ├─ uses       → SystemMetadata
  └─ depends on → ThreadSafety.cs

ThreadSafety.cs
  └─ uses       → SystemMetadata

JobSystem.cs
  └─ standalone (no dependencies)

All classes depend on:
  - Arch.Core (World, Entity, QueryDescription)
  - System.Threading.Tasks
```

---

## Integration Points

### With Existing Systems

```
BEFORE (Sequential):
  SystemManager
    └─ Update() calls each system.Update() sequentially

AFTER (Parallel):
  ParallelSystemManager (extends SystemManager)
    └─ Update() calls systems in parallel stages

MIGRATION:
  // Change this:
  var systemManager = new SystemManager();

  // To this:
  var systemManager = new ParallelSystemManager(world, enableParallel: true);
```

### With Arch ECS

```
Arch ECS provides:
  - World.Query() → Sequential iteration
  - World.ParallelQuery() → Parallel iteration

Our wrapper:
  - ParallelQueryExecutor wraps Arch's parallel queries
  - Adds performance tracking
  - Simplifies API
  - Provides map-reduce support
```

---

## Summary

This architecture provides:

✅ **Automatic parallelization** based on dependency analysis
✅ **Thread-safe by design** with compile-time declarations
✅ **Performance profiling** built into the system
✅ **Flexible execution** with fallback to sequential
✅ **Developer-friendly API** with clear patterns
✅ **Production-ready** with comprehensive testing

**Key Innovation**: Stage-based execution with automatic dependency resolution eliminates the need for manual thread management while guaranteeing thread safety.
