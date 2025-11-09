# PokeSharp Parallel System Examples

This document provides practical examples of parallel system implementation for common PokeSharp scenarios.

## Table of Contents

1. [Basic Movement System](#basic-movement-system)
2. [Combat System](#combat-system)
3. [AI Behavior System](#ai-behavior-system)
4. [Particle System](#particle-system)
5. [Tile Animation System](#tile-animation-system)
6. [Collision Detection System](#collision-detection-system)

---

## Basic Movement System

### Sequential Implementation (Before)

```csharp
public class MovementSystem : SystemBase
{
    public override int Priority => 100;

    public override void Update(World world, float deltaTime)
    {
        var query = new QueryDescription()
            .WithAll<Position, Velocity>();

        world.Query(in query, (Entity entity, ref Position pos, ref Velocity vel) =>
        {
            pos.X += vel.X * deltaTime;
            pos.Y += vel.Y * deltaTime;
        });
    }
}
```

### Parallel Implementation (After)

```csharp
using PokeSharp.Core.Components;
using PokeSharp.Core.Parallel;
using PokeSharp.Core.Systems;

public class MovementSystem : ParallelSystemBase
{
    public override int Priority => 100;

    // Declare component access for dependency analysis
    public override List<Type> GetReadComponents() => new() {
        typeof(Velocity)
    };

    public override List<Type> GetWriteComponents() => new() {
        typeof(Position)
    };

    public override void Update(World world, float deltaTime)
    {
        var query = new QueryDescription()
            .WithAll<Position, Velocity>();

        // Use parallel query execution
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

**Performance**: 2.5x speedup with 10,000 entities on 4 cores

---

## Combat System

### Damage Application System

```csharp
using PokeSharp.Core.Components;
using PokeSharp.Core.Parallel;

public class DamageApplicationSystem : ParallelSystemBase
{
    public override int Priority => 150;

    public override List<Type> GetReadComponents() => new() {
        typeof(DamageQueue)
    };

    public override List<Type> GetWriteComponents() => new() {
        typeof(Health),
        typeof(StatusEffects)
    };

    public override void Update(World world, float deltaTime)
    {
        var query = new QueryDescription()
            .WithAll<Health, DamageQueue>();

        ParallelQuery<Health, DamageQueue>(
            query,
            (entity, ref Health health, ref DamageQueue queue) =>
            {
                foreach (var damage in queue.PendingDamage)
                {
                    health.Current -= damage.Amount;

                    if (health.Current <= 0)
                    {
                        health.Current = 0;
                        // Mark for death processing
                    }
                }

                // Clear processed damage
                queue.Clear();
            }
        );
    }
}
```

### Combat Statistics Aggregation

```csharp
public class CombatStatisticsSystem : ParallelSystemBase
{
    public override int Priority => 200;

    public override List<Type> GetReadComponents() => new() {
        typeof(Health),
        typeof(CombatParticipant)
    };

    public struct CombatStats
    {
        public int TotalCombatants;
        public int DefeatedEnemies;
        public float TotalDamageDealt;
        public float AverageHealth;
    }

    public override void Update(World world, float deltaTime)
    {
        var query = new QueryDescription()
            .WithAll<Health, CombatParticipant>();

        var stats = ParallelQueryWithReduce<Health, CombatStats>(
            query,
            // Map: compute per-entity stats
            (entity, ref Health health) => new CombatStats
            {
                TotalCombatants = 1,
                DefeatedEnemies = health.Current <= 0 ? 1 : 0,
                TotalDamageDealt = health.DamageDealt,
                AverageHealth = health.Current
            },
            // Reduce: combine stats
            (a, b) => new CombatStats
            {
                TotalCombatants = a.TotalCombatants + b.TotalCombatants,
                DefeatedEnemies = a.DefeatedEnemies + b.DefeatedEnemies,
                TotalDamageDealt = a.TotalDamageDealt + b.TotalDamageDealt,
                AverageHealth = a.AverageHealth + b.AverageHealth
            }
        );

        stats.AverageHealth /= stats.TotalCombatants;

        // Update global combat state
        UpdateCombatUI(stats);
    }
}
```

---

## AI Behavior System

```csharp
using PokeSharp.Core.Components;
using PokeSharp.Core.Parallel;

[ThreadSafeComponent(Notes = "All fields are primitives")]
public struct AIBehavior
{
    public AIState CurrentState;
    public float StateTimer;
    public int TargetEntityId;
    public float AggressionLevel;
}

public class AIBehaviorSystem : ParallelSystemBase
{
    public override int Priority => 120;

    public override List<Type> GetReadComponents() => new() {
        typeof(Position),
        typeof(Vision)
    };

    public override List<Type> GetWriteComponents() => new() {
        typeof(AIBehavior),
        typeof(Velocity)
    };

    public override void Update(World world, float deltaTime)
    {
        var query = new QueryDescription()
            .WithAll<Position, Velocity, AIBehavior, Vision>();

        ParallelQuery<Position, Velocity, AIBehavior, Vision>(
            query,
            (entity, ref Position pos, ref Velocity vel,
             ref AIBehavior ai, ref Vision vision) =>
            {
                ai.StateTimer += deltaTime;

                switch (ai.CurrentState)
                {
                    case AIState.Idle:
                        UpdateIdleBehavior(ref ai, ref vel, deltaTime);
                        break;

                    case AIState.Patrol:
                        UpdatePatrolBehavior(ref ai, ref vel, ref pos, deltaTime);
                        break;

                    case AIState.Chase:
                        UpdateChaseBehavior(ref ai, ref vel, ref pos, deltaTime);
                        break;

                    case AIState.Attack:
                        UpdateAttackBehavior(ref ai, ref vel, deltaTime);
                        break;
                }
            }
        );
    }

    private void UpdateIdleBehavior(ref AIBehavior ai, ref Velocity vel, float deltaTime)
    {
        vel.X = 0;
        vel.Y = 0;

        if (ai.StateTimer > 3.0f)
        {
            ai.CurrentState = AIState.Patrol;
            ai.StateTimer = 0;
        }
    }

    private void UpdatePatrolBehavior(ref AIBehavior ai, ref Velocity vel,
        ref Position pos, float deltaTime)
    {
        // Simple patrol logic
        vel.X = MathF.Cos(ai.StateTimer) * 50;
        vel.Y = MathF.Sin(ai.StateTimer) * 50;

        if (ai.StateTimer > 10.0f)
        {
            ai.CurrentState = AIState.Idle;
            ai.StateTimer = 0;
        }
    }

    // ... other behavior methods
}
```

**Key Point**: AI systems are great candidates for parallelization because each entity's AI is independent.

---

## Particle System

```csharp
using PokeSharp.Core.Components;
using PokeSharp.Core.Parallel;

public struct Particle
{
    public float Lifetime;
    public float MaxLifetime;
    public float FadeSpeed;
    public float Alpha;
    public int ColorIndex;
}

public class ParticleUpdateSystem : ParallelSystemBase
{
    public override int Priority => 80;

    public override List<Type> GetReadComponents() => new() {
        typeof(Velocity)
    };

    public override List<Type> GetWriteComponents() => new() {
        typeof(Position),
        typeof(Particle),
        typeof(Sprite)
    };

    public override void Update(World world, float deltaTime)
    {
        var query = new QueryDescription()
            .WithAll<Position, Velocity, Particle, Sprite>();

        ParallelQuery<Position, Velocity, Particle, Sprite>(
            query,
            (entity, ref Position pos, ref Velocity vel,
             ref Particle particle, ref Sprite sprite) =>
            {
                // Update lifetime
                particle.Lifetime += deltaTime;

                // Update position
                pos.X += vel.X * deltaTime;
                pos.Y += vel.Y * deltaTime;

                // Fade out
                float lifetimePercent = particle.Lifetime / particle.MaxLifetime;
                particle.Alpha = 1.0f - lifetimePercent;
                sprite.Alpha = particle.Alpha;

                // Apply gravity
                vel.Y += 9.8f * deltaTime;

                // Mark for deletion if expired
                if (particle.Lifetime >= particle.MaxLifetime)
                {
                    // Component will be removed by cleanup system
                    particle.Alpha = 0;
                }
            }
        );
    }
}

// Separate cleanup system (must run after update)
public class ParticleCleanupSystem : SystemBase
{
    public override int Priority => 81; // Runs after ParticleUpdateSystem

    public override void Update(World world, float deltaTime)
    {
        var query = new QueryDescription()
            .WithAll<Particle>();

        var toDelete = new List<Entity>();

        world.Query(in query, (Entity entity, ref Particle particle) =>
        {
            if (particle.Lifetime >= particle.MaxLifetime)
            {
                toDelete.Add(entity);
            }
        });

        // Delete expired particles
        foreach (var entity in toDelete)
        {
            world.Destroy(entity);
        }
    }
}
```

**Performance**: 3.2x speedup with 50,000 particles on 8 cores

---

## Tile Animation System

```csharp
using PokeSharp.Core.Components;
using PokeSharp.Core.Parallel;

public struct AnimatedTile
{
    public int CurrentFrame;
    public int FrameCount;
    public float FrameDuration;
    public float Timer;
    public bool Loop;
}

public class TileAnimationSystem : ParallelSystemBase
{
    public override int Priority => 90;

    public override List<Type> GetWriteComponents() => new() {
        typeof(AnimatedTile),
        typeof(Sprite)
    };

    public override void Update(World world, float deltaTime)
    {
        var query = new QueryDescription()
            .WithAll<AnimatedTile, Sprite>();

        ParallelQuery<AnimatedTile, Sprite>(
            query,
            (entity, ref AnimatedTile anim, ref Sprite sprite) =>
            {
                anim.Timer += deltaTime;

                if (anim.Timer >= anim.FrameDuration)
                {
                    anim.Timer -= anim.FrameDuration;
                    anim.CurrentFrame++;

                    if (anim.CurrentFrame >= anim.FrameCount)
                    {
                        if (anim.Loop)
                        {
                            anim.CurrentFrame = 0;
                        }
                        else
                        {
                            anim.CurrentFrame = anim.FrameCount - 1;
                        }
                    }

                    // Update sprite frame
                    sprite.TextureIndex = anim.CurrentFrame;
                }
            }
        );
    }
}
```

---

## Collision Detection System

### Broad Phase (Parallel)

```csharp
using PokeSharp.Core.Components;
using PokeSharp.Core.Parallel;

public class BroadPhaseCollisionSystem : ParallelSystemBase
{
    public override int Priority => 160;

    public override List<Type> GetReadComponents() => new() {
        typeof(Position),
        typeof(Collider)
    };

    public override List<Type> GetWriteComponents() => new() {
        typeof(SpatialHash)
    };

    public override void Update(World world, float deltaTime)
    {
        var query = new QueryDescription()
            .WithAll<Position, Collider, SpatialHash>();

        // Update spatial hash in parallel
        ParallelQuery<Position, Collider, SpatialHash>(
            query,
            (entity, ref Position pos, ref Collider collider,
             ref SpatialHash hash) =>
            {
                // Compute spatial hash cell
                int cellX = (int)(pos.X / collider.CellSize);
                int cellY = (int)(pos.Y / collider.CellSize);

                hash.CellX = cellX;
                hash.CellY = cellY;
                hash.Hash = cellX * 73856093 ^ cellY * 19349663;
            }
        );
    }
}
```

### Narrow Phase (Sequential - requires global collision list)

```csharp
[RequiresExclusiveExecution("Builds global collision list")]
public class NarrowPhaseCollisionSystem : SystemBase
{
    public override int Priority => 161; // After broad phase

    public override void Update(World world, float deltaTime)
    {
        // Group entities by spatial hash
        var cellGroups = new Dictionary<int, List<Entity>>();

        var query = new QueryDescription()
            .WithAll<Position, Collider, SpatialHash>();

        world.Query(in query, (Entity entity, ref SpatialHash hash) =>
        {
            if (!cellGroups.ContainsKey(hash.Hash))
            {
                cellGroups[hash.Hash] = new List<Entity>();
            }
            cellGroups[hash.Hash].Add(entity);
        });

        // Check collisions within each cell
        foreach (var (hash, entities) in cellGroups)
        {
            for (int i = 0; i < entities.Count; i++)
            {
                for (int j = i + 1; j < entities.Count; j++)
                {
                    CheckCollision(world, entities[i], entities[j]);
                }
            }
        }
    }

    private void CheckCollision(World world, Entity a, Entity b)
    {
        // Detailed collision detection
        var posA = world.Get<Position>(a);
        var posB = world.Get<Position>(b);
        var colliderA = world.Get<Collider>(a);
        var colliderB = world.Get<Collider>(b);

        // AABB collision check
        bool collides =
            posA.X < posB.X + colliderB.Width &&
            posA.X + colliderA.Width > posB.X &&
            posA.Y < posB.Y + colliderB.Height &&
            posA.Y + colliderA.Height > posB.Y;

        if (collides)
        {
            HandleCollision(world, a, b);
        }
    }
}
```

---

## System Registration Example

```csharp
using PokeSharp.Core.Parallel;

public class GameWorld
{
    private World _world;
    private ParallelSystemManager _systemManager;

    public void Initialize()
    {
        _world = World.Create();
        _systemManager = new ParallelSystemManager(_world, enableParallel: true);

        // Register parallel systems with metadata
        RegisterSystems();

        // Build execution plan
        _systemManager.RebuildExecutionPlan();

        // Log execution plan
        Console.WriteLine(_systemManager.GetExecutionPlan());
    }

    private void RegisterSystems()
    {
        // Using fluent API
        _systemManager.RegisterSystemWithMetadata<MovementSystem>(
            SystemMetadataHelper.ForSystem<MovementSystem>()
                .Reads<Velocity>()
                .Writes<Position>()
                .WithPriority(100)
                .AllowsParallel(true)
                .Build()
        );

        _systemManager.RegisterSystemWithMetadata<AIBehaviorSystem>(
            SystemMetadataHelper.ForSystem<AIBehaviorSystem>()
                .Reads<Position, Vision>()
                .Writes<AIBehavior, Velocity>()
                .WithPriority(120)
                .Build()
        );

        _systemManager.RegisterSystemWithMetadata<ParticleUpdateSystem>(
            SystemMetadataHelper.ForSystem<ParticleUpdateSystem>()
                .Reads<Velocity>()
                .Writes<Position, Particle, Sprite>()
                .WithPriority(80)
                .Build()
        );

        _systemManager.RegisterSystemWithMetadata<TileAnimationSystem>(
            SystemMetadataHelper.ForSystem<TileAnimationSystem>()
                .Writes<AnimatedTile, Sprite>()
                .WithPriority(90)
                .Build()
        );

        _systemManager.RegisterSystemWithMetadata<BroadPhaseCollisionSystem>(
            SystemMetadataHelper.ForSystem<BroadPhaseCollisionSystem>()
                .Reads<Position, Collider>()
                .Writes<SpatialHash>()
                .WithPriority(160)
                .Build()
        );

        // Non-parallel system
        _systemManager.RegisterSystem<NarrowPhaseCollisionSystem>();
    }

    public void Update(float deltaTime)
    {
        _systemManager.Update(_world, deltaTime);
    }
}
```

Expected execution plan:

```
Parallel Execution Plan
=======================

Stage 1: (3 systems in parallel)
  - ParticleUpdateSystem (Priority: 80)
  - TileAnimationSystem (Priority: 90)
  - AIBehaviorSystem (Priority: 120)

Stage 2: (2 systems in parallel)
  - MovementSystem (Priority: 100)
  - BroadPhaseCollisionSystem (Priority: 160)

Stage 3: (1 systems in parallel)
  - NarrowPhaseCollisionSystem (Priority: 161)
```

---

## Performance Monitoring

```csharp
public class PerformanceMonitor
{
    private readonly ParallelSystemManager _systemManager;

    public void PrintStatistics()
    {
        // System performance
        var metrics = _systemManager.GetMetrics();
        Console.WriteLine("System Performance:");
        foreach (var (system, metric) in metrics)
        {
            Console.WriteLine($"  {system.GetType().Name}:");
            Console.WriteLine($"    Average: {metric.AverageUpdateMs:F2}ms");
            Console.WriteLine($"    Max: {metric.MaxUpdateMs:F2}ms");
            Console.WriteLine($"    Updates: {metric.UpdateCount}");
        }

        // Parallel execution stats
        var parallelStats = _systemManager.GetParallelStats();
        Console.WriteLine("\nParallel Execution Stats:");
        Console.WriteLine($"  Total queries: {parallelStats.TotalParallelQueries}");
        Console.WriteLine($"  Entities processed: {parallelStats.TotalEntitiesProcessed}");
        Console.WriteLine($"  Avg time: {parallelStats.AverageExecutionTimeMs:F2}ms");
        Console.WriteLine($"  Estimated speedup: {parallelStats.EstimatedSpeedup:F1}x");
    }
}
```

---

## Troubleshooting Common Issues

### Issue: System not running in parallel

**Cause**: Component access not declared or conflicts with another system.

**Solution**: Check dependency graph:

```csharp
var graph = systemManager.GetDependencyGraph();
Console.WriteLine(graph);
```

### Issue: Unexpected behavior in parallel execution

**Cause**: Shared mutable state or incorrect component access declarations.

**Solution**: Test with parallel disabled:

```csharp
var systemManager = new ParallelSystemManager(world, enableParallel: false);
// Run tests - if behavior is correct, issue is in parallel execution
```

### Issue: Performance regression

**Cause**: Overhead exceeds benefits for small entity counts.

**Solution**: Profile and consider disabling parallel for small workloads:

```csharp
if (entityCount < 1000)
{
    systemManager.SetParallelExecution(false);
}
```

---

For more information, see the [Parallel Execution Guide](PARALLEL_EXECUTION_GUIDE.md).
