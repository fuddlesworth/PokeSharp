# PokeSharp Parallel Execution Guide

## Overview

PokeSharp's parallel execution system enables safe, efficient multi-threaded processing of ECS queries and systems. Built on top of Arch ECS's parallel query support, it automatically analyzes system dependencies to maximize parallelism while preventing data races.

## Table of Contents

1. [Core Concepts](#core-concepts)
2. [Getting Started](#getting-started)
3. [Parallel Query Execution](#parallel-query-execution)
4. [System Dependency Management](#system-dependency-management)
5. [Thread Safety Guidelines](#thread-safety-guidelines)
6. [Performance Best Practices](#performance-best-practices)
7. [Common Pitfalls](#common-pitfalls)
8. [Debugging Parallel Issues](#debugging-parallel-issues)

---

## Core Concepts

### What is Parallel Execution?

Parallel execution allows multiple CPU cores to process different parts of your game world simultaneously:

- **Parallel Queries**: Process entities in chunks across multiple threads
- **Parallel Systems**: Run independent systems simultaneously
- **Automatic Dependency Analysis**: Safely determines which systems can run in parallel

### Key Components

| Component | Purpose |
|-----------|---------|
| `ParallelQueryExecutor` | Executes ECS queries across multiple threads |
| `ParallelSystemManager` | Manages system execution with automatic parallelization |
| `SystemDependencyGraph` | Tracks component access patterns to prevent data races |
| `JobSystem` | High-level API for scheduling parallel work |
| `ThreadSafety` | Utilities for validating thread safety |

---

## Getting Started

### Basic Setup

```csharp
using PokeSharp.Core.Parallel;
using PokeSharp.Core.Systems;

// Create parallel system manager
var world = World.Create();
var systemManager = new ParallelSystemManager(world, enableParallel: true);

// Register systems with metadata
systemManager.RegisterSystemWithMetadata<MovementSystem>(
    SystemMetadataHelper.ForSystem<MovementSystem>()
        .Reads<Velocity>()
        .Writes<Position>()
        .WithPriority(100)
        .Build()
);

systemManager.RegisterSystemWithMetadata<RenderSystem>(
    SystemMetadataHelper.ForSystem<RenderSystem>()
        .Reads<Position, Sprite>()
        .WithPriority(200)
        .Build()
);

// Build execution plan
systemManager.RebuildExecutionPlan();

// Systems will now run in parallel where safe
systemManager.Update(world, deltaTime);
```

### Creating a Parallel System

```csharp
using PokeSharp.Core.Components;
using PokeSharp.Core.Systems;

public class MovementSystem : ParallelSystemBase
{
    public override int Priority => 100;

    // Declare component access patterns
    public override List<Type> GetReadComponents() => new() {
        typeof(Velocity),
        typeof(GridMovement)
    };

    public override List<Type> GetWriteComponents() => new() {
        typeof(Position)
    };

    public override void Update(World world, float deltaTime)
    {
        // Use parallel query execution
        var query = new QueryDescription()
            .WithAll<Position, Velocity>();

        ParallelQuery<Position, Velocity>(
            query,
            (entity, ref Position pos, ref Velocity vel) =>
            {
                pos.X += vel.X * deltaTime;
                pos.Y += vel.Y * deltaTime;
            }
        );
    }
}
```

---

## Parallel Query Execution

### Single Component Query

```csharp
var query = new QueryDescription().WithAll<Health>();

ParallelQuery<Health>(
    query,
    (entity, ref Health health) =>
    {
        health.Regenerate(deltaTime);
    }
);
```

### Multiple Components

```csharp
var query = new QueryDescription().WithAll<Position, Velocity, Acceleration>();

ParallelQuery<Position, Velocity, Acceleration>(
    query,
    (entity, ref Position pos, ref Velocity vel, ref Acceleration acc) =>
    {
        vel.X += acc.X * deltaTime;
        vel.Y += acc.Y * deltaTime;
        pos.X += vel.X * deltaTime;
        pos.Y += vel.Y * deltaTime;
    }
);
```

### Map-Reduce Pattern

Use for aggregating results from parallel queries:

```csharp
var query = new QueryDescription().WithAll<Enemy, Health>();

int aliveEnemies = ParallelQueryWithReduce<Health, int>(
    query,
    // Map: check if alive
    (entity, ref Health health) => health.Current > 0 ? 1 : 0,
    // Reduce: sum the counts
    (a, b) => a + b
);

Console.WriteLine($"Alive enemies: {aliveEnemies}");
```

### Performance Metrics

```csharp
var stats = ParallelExecutor.GetStats();

Console.WriteLine($"Total queries: {stats.TotalParallelQueries}");
Console.WriteLine($"Entities processed: {stats.TotalEntitiesProcessed}");
Console.WriteLine($"Average execution time: {stats.AverageExecutionTimeMs}ms");
Console.WriteLine($"Estimated speedup: {stats.EstimatedSpeedup}x");
```

---

## System Dependency Management

### Understanding Dependencies

Systems have **dependencies** based on which components they access:

- **Read Dependency**: System reads a component (immutable access)
- **Write Dependency**: System writes/modifies a component (mutable access)

### Dependency Rules

| Scenario | Can Run in Parallel? | Reason |
|----------|---------------------|--------|
| Both systems **read** the same component | ✅ Yes | Multiple readers are safe |
| One **reads**, one **writes** the same component | ❌ No | Read-write conflict |
| Both **write** the same component | ❌ No | Write-write conflict |
| No shared components | ✅ Yes | No data dependencies |

### Declaring Dependencies

#### Using Metadata Builder

```csharp
var metadata = SystemMetadataHelper.ForSystem<PhysicsSystem>()
    .Reads<Mass, Force>()           // Read-only access
    .Writes<Velocity, Acceleration>() // Mutable access
    .WithPriority(50)
    .AllowsParallel(true)
    .WithDescription("Physics simulation system")
    .Build();

systemManager.RegisterSystemWithMetadata<PhysicsSystem>(metadata);
```

#### Using ParallelSystemBase

```csharp
public class PhysicsSystem : ParallelSystemBase
{
    public override List<Type> GetReadComponents() => new() {
        typeof(Mass),
        typeof(Force)
    };

    public override List<Type> GetWriteComponents() => new() {
        typeof(Velocity),
        typeof(Acceleration)
    };

    public override bool AllowsParallelExecution => true;
}
```

### Visualizing Dependencies

```csharp
// Print dependency graph
var graph = systemManager.GetDependencyGraph();
Console.WriteLine(graph);

// Print execution plan
var plan = systemManager.GetExecutionPlan();
Console.WriteLine(plan);
```

Example output:

```
Parallel Execution Plan
=======================

Stage 1: (3 systems in parallel)
  - MovementSystem (Priority: 100)
  - AIBehaviorSystem (Priority: 100)
  - AnimationSystem (Priority: 100)

Stage 2: (2 systems in parallel)
  - CollisionSystem (Priority: 200)
  - SpatialHashSystem (Priority: 200)

Stage 3: (1 systems in parallel)
  - RenderSystem (Priority: 300)
```

### Disabling Parallel Execution

For systems with external side effects or global state:

```csharp
public class RenderSystem : ParallelSystemBase
{
    // This system accesses GPU resources - can't run in parallel
    public override bool AllowsParallelExecution => false;

    public override void Update(World world, float deltaTime)
    {
        // Rendering code...
    }
}
```

Or use the attribute:

```csharp
[RequiresExclusiveExecution("Accesses GPU resources")]
public class RenderSystem : ParallelSystemBase
{
    // ...
}
```

---

## Thread Safety Guidelines

### Thread-Safe Component Design

✅ **DO** - Use primitive types:

```csharp
public struct Position
{
    public float X;
    public float Y;
    // Primitives are thread-safe for parallel writes
}
```

✅ **DO** - Use nested structs:

```csharp
public struct Transform
{
    public Vector2 Position;   // Struct
    public Vector2 Scale;      // Struct
    public float Rotation;     // Primitive
}
```

❌ **DON'T** - Use reference types:

```csharp
public struct Enemy
{
    public string Name;        // ❌ String (reference type)
    public List<Item> Items;   // ❌ List (reference type)
}
```

❌ **DON'T** - Use arrays:

```csharp
public struct Inventory
{
    public Item[] Items;       // ❌ Array (not thread-safe)
}
```

### Analyzing Component Safety

```csharp
// Analyze a component type
var analysis = ThreadSafety.AnalyzeComponentType<MyComponent>();

Console.WriteLine(analysis.ToString());
// Output: MyComponent: SAFE for parallel writes (0 unsafe fields)

if (!analysis.IsThreadSafeWrite)
{
    foreach (var field in analysis.UnsafeFields)
    {
        Console.WriteLine($"  Unsafe field: {field}");
    }

    var recommendations = ThreadSafety.GetThreadSafetyRecommendations(typeof(MyComponent));
    foreach (var rec in recommendations)
    {
        Console.WriteLine(rec);
    }
}
```

### Making Components Thread-Safe

#### Before (Not Thread-Safe):

```csharp
public struct PathComponent
{
    public Vector2[] Waypoints;  // ❌ Array
    public int CurrentIndex;
}
```

#### After (Thread-Safe):

```csharp
public struct PathComponent
{
    public Vector2 CurrentWaypoint;
    public Vector2 NextWaypoint;
    public float Progress;
}

// Store path in separate entity or use ECS relationships
```

### Fixed-Size Buffers (Advanced)

For small arrays, use fixed-size buffers:

```csharp
public unsafe struct InventoryComponent
{
    public const int MaxItems = 8;
    public fixed int ItemIds[MaxItems];  // Fixed-size buffer
    public int ItemCount;
}
```

---

## Performance Best Practices

### 1. Batch Similar Operations

✅ **Good** - Process all entities of same type together:

```csharp
ParallelQuery<Position, Velocity>(query, (entity, ref pos, ref vel) => {
    pos.X += vel.X * deltaTime;
    pos.Y += vel.Y * deltaTime;
});
```

❌ **Bad** - Many small queries:

```csharp
foreach (var entity in someList)
{
    // Overhead of query setup for each entity
    ParallelQuery<Position>(singleEntityQuery, ...);
}
```

### 2. Minimize Component Access

Only declare components you actually need:

```csharp
// If you only need Position, don't include Sprite
public override List<Type> GetReadComponents() => new() {
    typeof(Position)  // ✅ Minimal dependencies
};
```

### 3. Use Appropriate Thread Count

```csharp
// Let the system decide (recommended)
var executor = new ParallelQueryExecutor(world);

// Or specify for specific hardware
var executor = new ParallelQueryExecutor(world, maxThreads: 4);
```

### 4. Profile Your Systems

```csharp
var metrics = systemManager.GetMetrics();

foreach (var (system, metric) in metrics)
{
    Console.WriteLine($"{system.GetType().Name}:");
    Console.WriteLine($"  Average: {metric.AverageUpdateMs:F2}ms");
    Console.WriteLine($"  Max: {metric.MaxUpdateMs:F2}ms");
    Console.WriteLine($"  Count: {metric.UpdateCount}");
}
```

### 5. Optimize Hot Paths

For systems that process many entities:

```csharp
public override void Update(World world, float deltaTime)
{
    // Pre-calculate values outside the parallel loop
    var deltaTimeSquared = deltaTime * deltaTime;
    var gravity = Physics.Gravity;

    ParallelQuery<Position, Velocity>(query, (entity, ref pos, ref vel) => {
        // Use pre-calculated values
        vel.Y += gravity * deltaTimeSquared;
        pos.Y += vel.Y * deltaTime;
    });
}
```

### Performance Expectations

| Entity Count | Sequential | Parallel (4 cores) | Speedup |
|--------------|-----------|-------------------|---------|
| 1,000 | 0.5ms | 0.3ms | 1.6x |
| 10,000 | 5ms | 1.8ms | 2.8x |
| 100,000 | 50ms | 15ms | 3.3x |

---

## Common Pitfalls

### 1. Race Conditions

❌ **Problem**: Multiple systems write the same component

```csharp
// System A writes Position
public override List<Type> GetWriteComponents() => new() { typeof(Position) };

// System B also writes Position - RACE CONDITION!
public override List<Type> GetWriteComponents() => new() { typeof(Position) };
```

✅ **Solution**: Properly declare dependencies or make systems sequential

```csharp
// System A: Priority 100
// System B: Priority 200 (runs after System A)
```

### 2. Shared Mutable State

❌ **Problem**: Accessing fields shared between threads

```csharp
public class BadSystem : ParallelSystemBase
{
    private int _counter; // ❌ Shared mutable state

    public override void Update(World world, float deltaTime)
    {
        ParallelQuery<Position>(query, (entity, ref pos) => {
            _counter++; // ❌ Race condition!
        });
    }
}
```

✅ **Solution**: Use Interlocked or avoid shared state

```csharp
public override void Update(World world, float deltaTime)
{
    int counter = 0;

    int result = ParallelQueryWithReduce<Position, int>(
        query,
        (entity, ref pos) => 1,  // Count each entity
        (a, b) => a + b           // Sum safely
    );

    _counter = result; // ✅ Update after parallel execution
}
```

### 3. Non-Deterministic Execution

Parallel execution can introduce non-determinism. If you need deterministic results:

```csharp
// Disable parallel execution for this system
public override bool AllowsParallelExecution => false;
```

### 4. External Side Effects

❌ **Problem**: Logging, file I/O, or GPU operations in parallel queries

```csharp
ParallelQuery<Position>(query, (entity, ref pos) => {
    Console.WriteLine($"Position: {pos}"); // ❌ Console I/O in parallel
    SaveToFile(pos);                        // ❌ File I/O in parallel
});
```

✅ **Solution**: Collect data first, then process sequentially

```csharp
var positions = new List<Position>();

// Collect in parallel
ParallelQuery<Position>(query, (entity, ref pos) => {
    lock (positions) {
        positions.Add(pos);
    }
});

// Process sequentially
foreach (var pos in positions)
{
    Console.WriteLine($"Position: {pos}");
}
```

---

## Debugging Parallel Issues

### Enable Logging

```csharp
var logger = LoggerFactory.Create(builder => {
    builder.AddConsole().SetMinimumLevel(LogLevel.Debug);
}).CreateLogger<ParallelSystemManager>();

var systemManager = new ParallelSystemManager(world, true, logger);
```

### Validate System Dependencies

```csharp
var validation = ThreadSafety.ValidateSystemThreadSafety(metadata);

if (!validation.IsThreadSafe)
{
    Console.WriteLine(validation.GetReport());
}
```

### Test Sequential vs Parallel

```csharp
// Test with parallel execution
var systemManager = new ParallelSystemManager(world, enableParallel: true);
// ... run test ...

// Test with sequential execution
var systemManager = new ParallelSystemManager(world, enableParallel: false);
// ... run same test and compare results ...
```

### Detect Race Conditions

Use tools like:
- **ThreadSanitizer** (Clang/GCC)
- **.NET Thread Safety Analyzer** (Roslyn analyzer)
- **Intel Inspector**

### Common Error Messages

| Error | Cause | Solution |
|-------|-------|----------|
| "System X is already registered" | Duplicate registration | Check registration code |
| "ParallelExecutor not initialized" | Missing Initialize() call | Ensure base.Initialize(world) is called |
| "Execution plan not built" | Missing RebuildExecutionPlan() | Call before first Update() |
| "Data race detected" | Concurrent writes to same component | Fix component access declarations |

---

## Advanced Topics

### Custom Job Scheduling

```csharp
var jobSystem = new JobSystem(workerThreads: 8);

// Schedule independent jobs
var job1 = jobSystem.Schedule(() => ProcessPhysics());
var job2 = jobSystem.Schedule(() => ProcessAI());
var job3 = jobSystem.Schedule(() => ProcessAnimation());

// Wait for all to complete
JobHandle.Combine(job1, job2, job3).Complete();
```

### Job Dependencies

```csharp
// Job B depends on Job A
var jobA = jobSystem.Schedule(() => UpdatePositions());
var jobB = jobSystem.ScheduleWithDependency(() => UpdateCollisions(), jobA);

jobB.Complete();
```

### Batch Processing

```csharp
var entities = GetAllEnemies();

var job = jobSystem.ScheduleBatch(entities, enemy => {
    enemy.UpdateAI(deltaTime);
});

job.Complete();
```

---

## Summary

### Key Takeaways

1. ✅ **Use `ParallelSystemBase`** for systems that process many entities
2. ✅ **Declare component access** accurately (Reads/Writes)
3. ✅ **Keep components thread-safe** (use structs, avoid arrays/classes)
4. ✅ **Profile and measure** performance gains
5. ✅ **Test both parallel and sequential** execution

### When to Use Parallel Execution

| Use Case | Parallel? | Reason |
|----------|-----------|--------|
| Updating 10,000+ entities | ✅ Yes | Large workload benefits from parallelism |
| Physics simulation | ✅ Yes | Independent calculations per entity |
| AI pathfinding | ✅ Yes | Each agent is independent |
| Rendering | ❌ No | GPU access requires sequential execution |
| Audio mixing | ❌ No | Requires specific ordering |
| Small entity counts (<100) | ❌ No | Overhead exceeds benefits |

### Further Reading

- [Arch ECS Documentation](https://github.com/genaray/Arch)
- [Entity Component System (ECS) Patterns](https://github.com/SanderMertens/ecs-faq)
- [C# Parallel Programming Guide](https://docs.microsoft.com/en-us/dotnet/standard/parallel-programming/)

---

**Questions or Issues?** Check the [PokeSharp GitHub Issues](https://github.com/your-repo/issues)
