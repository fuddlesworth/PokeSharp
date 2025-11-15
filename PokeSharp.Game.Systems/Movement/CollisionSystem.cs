using Arch.Core.Extensions;
using Microsoft.Extensions.Logging;
using PokeSharp.Engine.Core.Systems;
using PokeSharp.Game.Components.Movement;
using PokeSharp.Game.Components.Rendering;
using PokeSharp.Game.Components.Tiles;
using PokeSharp.Game.Systems.Services;

namespace PokeSharp.Game.Systems;

/// <summary>
///     Service that provides tile-based collision detection for grid movement.
///     Uses spatial hash to query entities with Collision components.
///     This is a service, not a system - it doesn't run every frame.
/// </summary>
public class CollisionService : ICollisionService
{
    private readonly ILogger<CollisionService>? _logger;
    private readonly ISpatialQuery _spatialQuery;

    public CollisionService(ISpatialQuery spatialQuery, ILogger<CollisionService>? logger = null)
    {
        _spatialQuery = spatialQuery ?? throw new ArgumentNullException(nameof(spatialQuery));
        _logger = logger;
    }

    /// <summary>
    ///     Checks if a tile position is walkable (not blocked by collision).
    /// </summary>
    /// <param name="mapId">The map identifier.</param>
    /// <param name="tileX">The X coordinate in tile space.</param>
    /// <param name="tileY">The Y coordinate in tile space.</param>
    /// <param name="fromDirection">Direction moving FROM (player's movement direction).</param>
    /// <param name="entityElevation">The elevation of the entity checking collision (default: standard elevation).</param>
    /// <returns>True if the position is walkable from this direction, false if blocked.</returns>
    /// <remarks>
    /// <para>
    /// Pokemon Emerald elevation rules:
    /// - Entities can only collide with objects at the SAME elevation
    /// - Bridge at elevation 6 doesn't collide with water at elevation 0
    /// - Player at elevation 3 walks under overhead structures at elevation 9+
    /// </para>
    /// </remarks>
    public bool IsPositionWalkable(
        int mapId,
        int tileX,
        int tileY,
        Direction fromDirection = Direction.None,
        byte entityElevation = Elevation.Default
    )
    {
        // Get all entities at this position from spatial hash
        var entities = _spatialQuery.GetEntitiesAt(mapId, tileX, tileY);

        foreach (var entity in entities)
        {
            // Check elevation first - only collide with entities at same elevation
            if (entity.Has<Elevation>())
            {
                ref var elevation = ref entity.Get<Elevation>();
                if (elevation.Value != entityElevation)
                {
                    // Different elevation - no collision (e.g., walking under bridge)
                    continue;
                }
            }

            // Check if entity has Collision component
            if (entity.Has<Collision>())
            {
                ref var collision = ref entity.Get<Collision>();

                if (collision.IsSolid)
                {
                    // Check ledge logic if entity has TileLedge component
                    if (entity.Has<TileLedge>() && fromDirection != Direction.None)
                    {
                        ref var ledge = ref entity.Get<TileLedge>();

                        // Check if ledge blocks this specific direction
                        if (ledge.IsBlockedFrom(fromDirection))
                            return false; // Ledge blocks this direction (can't climb up)
                        // Ledge allows this direction (can jump down) - continue checking other entities
                        // Don't return true yet, there might be other blocking entities
                    }
                    else
                    {
                        // Solid collision without ledge - always blocks
                        return false;
                    }
                }
            }
        }

        // No blocking collisions found
        return true;
    }

    /// <summary>
    ///     Checks if a tile is a Pokemon-style ledge (has TileLedge component).
    /// </summary>
    /// <param name="mapId">The map identifier.</param>
    /// <param name="tileX">The X coordinate in tile space.</param>
    /// <param name="tileY">The Y coordinate in tile space.</param>
    /// <returns>True if the tile is a ledge, false otherwise.</returns>
    public bool IsLedge(int mapId, int tileX, int tileY)
    {
        var entities = _spatialQuery.GetEntitiesAt(mapId, tileX, tileY);

        foreach (var entity in entities)
            if (entity.Has<TileLedge>())
                return true;

        return false;
    }

    /// <summary>
    ///     Gets the allowed jump direction for a ledge tile.
    /// </summary>
    /// <param name="mapId">The map identifier.</param>
    /// <param name="tileX">The X coordinate in tile space.</param>
    /// <param name="tileY">The Y coordinate in tile space.</param>
    /// <returns>The direction you can jump across this ledge, or None if not a ledge.</returns>
    public Direction GetLedgeJumpDirection(int mapId, int tileX, int tileY)
    {
        var entities = _spatialQuery.GetEntitiesAt(mapId, tileX, tileY);

        foreach (var entity in entities)
            if (entity.Has<TileLedge>())
            {
                ref var ledge = ref entity.Get<TileLedge>();
                return ledge.JumpDirection;
            }

        return Direction.None;
    }
}
