using PokeSharp.Game.Components.Movement;
using PokeSharp.Game.Components.Rendering;

namespace PokeSharp.Game.Systems.Services;

/// <summary>
/// Service for tile-based collision detection.
/// Provides collision queries without per-frame updates.
/// </summary>
/// <remarks>
/// This is a service, not a system. It doesn't run every frame;
/// instead, it provides on-demand collision checking when other
/// systems need to validate movement or check tile properties.
/// </remarks>
public interface ICollisionService
{
    /// <summary>
    /// Checks if a tile position is walkable (not blocked by collision).
    /// </summary>
    /// <param name="mapId">The map identifier.</param>
    /// <param name="tileX">The X coordinate in tile space.</param>
    /// <param name="tileY">The Y coordinate in tile space.</param>
    /// <param name="fromDirection">Optional direction moving FROM (for ledge checking).</param>
    /// <param name="entityElevation">The elevation of the entity checking collision (default: standard elevation).</param>
    /// <returns>True if the position is walkable from this direction, false if blocked.</returns>
    bool IsPositionWalkable(
        int mapId,
        int tileX,
        int tileY,
        Direction fromDirection = Direction.None,
        byte entityElevation = Elevation.Default
    );

    /// <summary>
    /// Checks if a tile is a Pokemon-style ledge (has TileLedge component).
    /// </summary>
    /// <param name="mapId">The map identifier.</param>
    /// <param name="tileX">The X coordinate in tile space.</param>
    /// <param name="tileY">The Y coordinate in tile space.</param>
    /// <returns>True if the tile is a ledge, false otherwise.</returns>
    bool IsLedge(int mapId, int tileX, int tileY);

    /// <summary>
    /// Gets the allowed jump direction for a ledge tile.
    /// </summary>
    /// <param name="mapId">The map identifier.</param>
    /// <param name="tileX">The X coordinate in tile space.</param>
    /// <param name="tileY">The Y coordinate in tile space.</param>
    /// <returns>The direction you can jump across this ledge, or None if not a ledge.</returns>
    Direction GetLedgeJumpDirection(int mapId, int tileX, int tileY);
}
