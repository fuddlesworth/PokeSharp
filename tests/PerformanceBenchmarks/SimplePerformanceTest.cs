using System.Diagnostics;
using Arch.Core;
using Arch.Core.Extensions;
using PokeSharp.Core.Components.Movement;
using PokeSharp.Core.Pooling;

namespace PokeSharp.Benchmarks;

/// <summary>
/// Simple performance tests that don't require parallel execution
/// </summary>
public static class SimplePerformanceTest
{
    private const int EntityCount = 10000;
    private const int LargeEntityCount = 50000;

    public static void RunAllTests()
    {
        Console.WriteLine("=== PokeSharp Performance Benchmarks ===\n");

        TestEntityPooling();
        TestQueryPerformance();
        TestSystemExecution();
        TestGCBehavior();

        Console.WriteLine("\n=== All Tests Complete ===");
    }

    private static void TestEntityPooling()
    {
        Console.WriteLine("1. Entity Pooling Performance:");

        var world = World.Create();
        var poolManager = new EntityPoolManager(world);
        poolManager.RegisterPool("test", 1000, 20000, warmup: true);

        // Test without pooling
        var sw = Stopwatch.StartNew();
        for (int i = 0; i < EntityCount; i++)
        {
            var entity = world.Create(
                new Position { X = i, Y = i },
                new Velocity { VelocityX = 1.0f, VelocityY = 1.0f }
            );
            world.Destroy(entity);
        }
        sw.Stop();
        var withoutPoolingMs = sw.ElapsedMilliseconds;
        var withoutPoolingUsPerEntity = sw.Elapsed.TotalMicroseconds / EntityCount;

        // Clear world
        world.Dispose();
        world = World.Create();
        poolManager = new EntityPoolManager(world);
        poolManager.RegisterPool("test", 1000, 20000, warmup: true);

        // Test with pooling
        sw.Restart();
        for (int i = 0; i < EntityCount; i++)
        {
            var entity = poolManager.Acquire("test");
            poolManager.Release(entity);
        }
        sw.Stop();
        var withPoolingMs = sw.ElapsedMilliseconds;
        var withPoolingUsPerEntity = sw.Elapsed.TotalMicroseconds / EntityCount;

        Console.WriteLine($"   Without pooling: {withoutPoolingMs}ms ({withoutPoolingUsPerEntity:F2}μs per entity)");
        Console.WriteLine($"   With pooling:    {withPoolingMs}ms ({withPoolingUsPerEntity:F2}μs per entity)");
        Console.WriteLine($"   Improvement:     {((double)withoutPoolingMs / withPoolingMs):F2}x faster\n");

        world.Dispose();
    }

    private static void TestQueryPerformance()
    {
        Console.WriteLine("2. Query Performance (50K entities):");

        var world = World.Create();
        var queryDesc = new QueryDescription().WithAll<Position, Velocity>();

        // Create test entities
        for (int i = 0; i < LargeEntityCount; i++)
        {
            world.Create(
                new Position { X = i, Y = i },
                new Velocity { VelocityX = 1.0f, VelocityY = 1.0f }
            );
        }

        // Sequential query
        var sw = Stopwatch.StartNew();
        world.Query(in queryDesc, (ref Position pos, ref Velocity vel) =>
        {
            pos.X += (int)vel.VelocityX;
            pos.Y += (int)vel.VelocityY;
        });
        sw.Stop();
        var sequentialMs = sw.ElapsedMilliseconds;

        Console.WriteLine($"   Sequential:      {sequentialMs}ms");
        Console.WriteLine($"   Entities/sec:    {LargeEntityCount * 1000.0 / sequentialMs:F0}\n");

        world.Dispose();
    }

    private static void TestSystemExecution()
    {
        Console.WriteLine("3. System Execution Performance:");

        var world = World.Create();
        var movementQuery = new QueryDescription().WithAll<Position, Velocity>();
        var renderQuery = new QueryDescription().WithAll<Position>();
        const float deltaTime = 0.016f; // 60 FPS

        // Create diverse entity set
        for (int i = 0; i < LargeEntityCount; i++)
        {
            if (i % 2 == 0)
            {
                world.Create(
                    new Position { X = i, Y = i },
                    new Velocity { VelocityX = 1.0f, VelocityY = 1.0f }
                );
            }
            else
            {
                world.Create(
                    new Position { X = i, Y = i }
                );
            }
        }

        // Single system
        var sw = Stopwatch.StartNew();
        world.Query(in movementQuery, (ref Position pos, ref Velocity vel) =>
        {
            pos.X += (int)(vel.VelocityX * deltaTime);
            pos.Y += (int)(vel.VelocityY * deltaTime);
        });
        sw.Stop();
        var singleSystemMs = sw.ElapsedMilliseconds;

        // Sequential systems
        sw.Restart();
        world.Query(in movementQuery, (ref Position pos, ref Velocity vel) =>
        {
            pos.X += (int)(vel.VelocityX * deltaTime);
            pos.Y += (int)(vel.VelocityY * deltaTime);
        });
        world.Query(in renderQuery, (ref Position pos) =>
        {
            if (pos.X < 0) pos.X = 0;
            if (pos.Y < 0) pos.Y = 0;
        });
        sw.Stop();
        var sequentialSystemsMs = sw.ElapsedMilliseconds;

        // Expected frame time at 60 FPS
        const float targetFrameTimeMs = 1000f / 60f;

        Console.WriteLine($"   Single system:   {singleSystemMs}ms");
        Console.WriteLine($"   Sequential (2):  {sequentialSystemsMs}ms");
        Console.WriteLine($"   Target (60 FPS): {targetFrameTimeMs:F2}ms");
        Console.WriteLine($"   Frame budget:    {(sequentialSystemsMs < targetFrameTimeMs ? "WITHIN" : "EXCEEDS")} budget\n");

        world.Dispose();
    }

    private static void TestGCBehavior()
    {
        Console.WriteLine("4. Garbage Collection Statistics:");

        var gen0Before = GC.CollectionCount(0);
        var gen1Before = GC.CollectionCount(1);
        var gen2Before = GC.CollectionCount(2);
        var memoryBefore = GC.GetTotalMemory(false);

        // Run some intensive operations
        var world = World.Create();
        var queryDesc = new QueryDescription().WithAll<Position, Velocity>();

        for (int i = 0; i < 100000; i++)
        {
            world.Create(
                new Position { X = i, Y = i },
                new Velocity { VelocityX = 1.0f, VelocityY = 1.0f }
            );
        }

        // Perform multiple queries
        for (int j = 0; j < 10; j++)
        {
            world.Query(in queryDesc, (ref Position pos, ref Velocity vel) =>
            {
                pos.X += (int)vel.VelocityX;
                pos.Y += (int)vel.VelocityY;
            });
        }

        var gen0After = GC.CollectionCount(0);
        var gen1After = GC.CollectionCount(1);
        var gen2After = GC.CollectionCount(2);
        var memoryAfter = GC.GetTotalMemory(false);

        Console.WriteLine($"   Gen0 Collections: {gen0After - gen0Before}");
        Console.WriteLine($"   Gen1 Collections: {gen1After - gen1Before}");
        Console.WriteLine($"   Gen2 Collections: {gen2After - gen2Before}");
        Console.WriteLine($"   Memory Before:    {memoryBefore / 1024.0 / 1024.0:F2}MB");
        Console.WriteLine($"   Memory After:     {memoryAfter / 1024.0 / 1024.0:F2}MB");
        Console.WriteLine($"   Memory Delta:     {(memoryAfter - memoryBefore) / 1024.0 / 1024.0:F2}MB");

        world.Dispose();
    }
}
