using System.Diagnostics;
using Arch.Core;
using Microsoft.Extensions.Logging;
using PokeSharp.Core.Components.Movement;
using PokeSharp.Core.Components.Rendering;

namespace PokeSharp.Core.Pooling;

/// <summary>
///     Example demonstrating component pooling usage and performance benefits.
/// </summary>
public class ComponentPoolingExample
{
    /// <summary>
    ///     Run comprehensive component pooling demonstration.
    /// </summary>
    public static void RunDemo(ILogger? logger = null)
    {
        logger?.LogInformation("=== Component Pooling Demonstration ===");

        // Initialize pool manager
        var poolManager = new ComponentPoolManager(logger, enableStatistics: true);

        logger?.LogInformation("Running performance comparison...");

        // Test 1: Position pooling
        var withoutPoolingTime = BenchmarkWithoutPooling();
        var withPoolingTime = BenchmarkWithPooling(poolManager);

        logger?.LogInformation(
            "Position operations - Without pooling: {WithoutMs}ms, With pooling: {WithMs}ms, Speedup: {Speedup:F2}x",
            withoutPoolingTime,
            withPoolingTime,
            withoutPoolingTime / (double)withPoolingTime
        );

        // Test 2: Animation pooling
        var animWithoutPoolingTime = BenchmarkAnimationWithoutPooling();
        var animWithPoolingTime = BenchmarkAnimationWithPooling(poolManager);

        logger?.LogInformation(
            "Animation operations - Without pooling: {WithoutMs}ms, With pooling: {WithMs}ms, Speedup: {Speedup:F2}x",
            animWithoutPoolingTime,
            animWithPoolingTime,
            animWithoutPoolingTime / (double)animWithPoolingTime
        );

        // Generate statistics report
        var report = poolManager.GenerateReport();
        logger?.LogInformation("Component Pool Statistics:\n{Report}", report);
    }

    private static long BenchmarkWithoutPooling()
    {
        var sw = Stopwatch.StartNew();

        for (int i = 0; i < 10000; i++)
        {
            var pos = new Position(i % 100, i / 100);
            pos.PixelX += 1.0f;
            pos.PixelY += 1.0f;

            var _ = pos.X + pos.Y; // Simulate usage
        }

        sw.Stop();
        return sw.ElapsedMilliseconds;
    }

    private static long BenchmarkWithPooling(ComponentPoolManager poolManager)
    {
        var sw = Stopwatch.StartNew();

        for (int i = 0; i < 10000; i++)
        {
            var pos = poolManager.RentPosition();
            pos.X = i % 100;
            pos.Y = i / 100;
            pos.PixelX += 1.0f;
            pos.PixelY += 1.0f;

            var _ = pos.X + pos.Y; // Simulate usage

            poolManager.ReturnPosition(pos);
        }

        sw.Stop();
        return sw.ElapsedMilliseconds;
    }

    private static long BenchmarkAnimationWithoutPooling()
    {
        var sw = Stopwatch.StartNew();

        for (int i = 0; i < 5000; i++)
        {
            var anim = new Animation("walk_down");
            anim.CurrentFrame = i % 4;
            anim.FrameTimer = 0.1f;
            anim.IsPlaying = true;

            var _ = anim.CurrentFrame; // Simulate usage
        }

        sw.Stop();
        return sw.ElapsedMilliseconds;
    }

    private static long BenchmarkAnimationWithPooling(ComponentPoolManager poolManager)
    {
        var sw = Stopwatch.StartNew();

        for (int i = 0; i < 5000; i++)
        {
            var anim = poolManager.RentAnimation();
            anim.CurrentAnimation = "walk_down";
            anim.CurrentFrame = i % 4;
            anim.FrameTimer = 0.1f;
            anim.IsPlaying = true;

            var _ = anim.CurrentFrame; // Simulate usage

            poolManager.ReturnAnimation(anim);
        }

        sw.Stop();
        return sw.ElapsedMilliseconds;
    }

    /// <summary>
    ///     Example of using component pooling in a system.
    /// </summary>
    public static void SystemIntegrationExample(World world, ComponentPoolManager poolManager)
    {
        // Query entities with movement
        var query = new QueryDescription().WithAll<Position, GridMovement>();

        world.Query(
            in query,
            (Entity entity, ref Position pos, ref GridMovement movement) =>
            {
                if (!movement.IsMoving)
                    return;

                // Rent temporary position for interpolation calculation
                var interpolatedPos = poolManager.RentPosition();

                try
                {
                    // Calculate interpolated position
                    float t = movement.MovementProgress;
                    interpolatedPos.PixelX =
                        movement.StartPosition.X + (movement.TargetPosition.X - movement.StartPosition.X) * t;
                    interpolatedPos.PixelY =
                        movement.StartPosition.Y + (movement.TargetPosition.Y - movement.StartPosition.Y) * t;

                    // Update entity position
                    pos.PixelX = interpolatedPos.PixelX;
                    pos.PixelY = interpolatedPos.PixelY;
                }
                finally
                {
                    // Always return to pool
                    poolManager.ReturnPosition(interpolatedPos);
                }
            }
        );
    }
}
