using Arch.Core;
using Microsoft.Extensions.Logging;
using PokeSharp.Engine.Common.Logging;
using PokeSharp.Engine.Core.Systems;
using PokeSharp.Engine.Systems.Management;
using PokeSharp.Game.Components.Tiles;
using EcsQueries = PokeSharp.Engine.Systems.Queries.Queries;

namespace PokeSharp.Game.Systems;

/// <summary>
///     System that updates animated tile frames based on time.
///     Handles Pokemon-style tile animations (water ripples, grass swaying, flowers).
///     Priority: 850 (after Animation:800, before Render:1000).
///     OPTIMIZED: Uses precalculated source rectangles for zero runtime overhead.
/// </summary>
public class TileAnimationSystem(ILogger<TileAnimationSystem>? logger = null)
    : SystemBase,
        IUpdateSystem
{
    private readonly ILogger<TileAnimationSystem>? _logger = logger;
    private int _animatedTileCount = -1; // Track for logging on first update

    /// <summary>
    /// Gets the priority for execution order. Lower values execute first.
    /// Tile animation executes at priority 850, after animation (800) and camera follow (825).
    /// </summary>
    public override int Priority => SystemPriority.TileAnimation;

    /// <inheritdoc />
    public override void Update(World world, float deltaTime)
    {
        EnsureInitialized();

        if (!Enabled)
            return;

        // CRITICAL OPTIMIZATION: Use sequential query instead of ParallelQuery
        // For 100-200 tiles, parallel overhead (task scheduling, thread sync) is MORE EXPENSIVE
        // than just iterating sequentially. ParallelQuery only helps with 500+ entities.
        //
        // Performance comparison (116 tiles):
        // - ParallelQuery: 10-20ms peaks (thread overhead)
        // - Sequential Query: <1ms consistent (no overhead)
        world.Query(
            in EcsQueries.AnimatedTiles,
            (Entity entity, ref AnimatedTile animTile, ref TileSprite sprite) =>
            {
                UpdateTileAnimation(ref animTile, ref sprite, deltaTime);
            }
        );

        // Log animated tile count on first update (sequential count for logging)
        if (_animatedTileCount < 0)
        {
            var tileCount = 0;
            var precalcCount = 0;
            var nullRectCount = 0;

            world.Query(
                in EcsQueries.AnimatedTiles,
                (Entity entity, ref AnimatedTile tile) =>
                {
                    tileCount++;
                    if (tile.FrameSourceRects != null && tile.FrameSourceRects.Length > 0)
                        precalcCount++;
                    else
                        nullRectCount++;
                }
            );

            if (tileCount > 0)
            {
                _animatedTileCount = tileCount;
                _logger?.LogAnimatedTilesProcessed(_animatedTileCount);
                _logger?.LogWarning(
                    "PERFORMANCE CHECK: {PrecalcCount}/{TotalCount} tiles have precalculated rects. "
                        + "{NullCount} tiles missing precalc (OLD MAP DATA - RELOAD REQUIRED!)",
                    precalcCount,
                    tileCount,
                    nullRectCount
                );
            }
        }
    }

    /// <summary>
    ///     Updates a single animated tile's frame timer and advances frames when needed.
    ///     Updates the TileSprite component's SourceRect to display the new frame.
    ///     OPTIMIZED: Uses precalculated source rectangles - zero dictionary lookups or calculations.
    /// </summary>
    /// <param name="animTile">The animated tile data.</param>
    /// <param name="sprite">The tile sprite to update.</param>
    /// <param name="deltaTime">Time elapsed since last frame in seconds.</param>
    private static void UpdateTileAnimation(
        ref AnimatedTile animTile,
        ref TileSprite sprite,
        float deltaTime
    )
    {
        // Validate animation data
        if (
            animTile.FrameTileIds == null
            || animTile.FrameTileIds.Length == 0
            || animTile.FrameDurations == null
            || animTile.FrameDurations.Length == 0
            || animTile.FrameSourceRects == null
            || animTile.FrameSourceRects.Length == 0
        )
            return;

        // Update frame timer
        animTile.FrameTimer += deltaTime;

        // Get current frame duration
        var currentIndex = animTile.CurrentFrameIndex;
        if (currentIndex < 0 || currentIndex >= animTile.FrameDurations.Length)
        {
            currentIndex = 0;
            animTile.CurrentFrameIndex = 0;
        }

        var currentDuration = animTile.FrameDurations[currentIndex];

        // Check if we need to advance to next frame
        if (animTile.FrameTimer >= currentDuration)
        {
            // Advance to next frame
            animTile.CurrentFrameIndex = (currentIndex + 1) % animTile.FrameTileIds.Length;
            animTile.FrameTimer = 0f;

            // PERFORMANCE CRITICAL: Direct array access to precalculated source rectangle
            // No calculations, no dictionary lookups, no lock contention - just a simple array index
            sprite.SourceRect = animTile.FrameSourceRects[animTile.CurrentFrameIndex];
        }
    }
}
