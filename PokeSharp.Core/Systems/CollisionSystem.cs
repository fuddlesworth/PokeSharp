using Arch.Core;
using Arch.Core.Extensions;
using PokeSharp.Core.Components;

namespace PokeSharp.Core.Systems;

/// <summary>
/// System that provides tile-based collision detection for grid movement.
/// Checks TileCollider components to determine if positions are walkable.
/// </summary>
public class CollisionSystem : BaseSystem
{
    /// <inheritdoc/>
    public override int Priority => SystemPriority.Collision;

    /// <inheritdoc/>
    public override void Update(World world, float deltaTime)
    {
        // Collision system doesn't require per-frame updates
        // It provides on-demand collision checking via IsPositionWalkable
        EnsureInitialized();
    }

    /// <summary>
    /// Checks if a tile position is walkable (not blocked by collision).
    /// </summary>
    /// <param name="world">The game world to query.</param>
    /// <param name="tileX">The X coordinate in tile space.</param>
    /// <param name="tileY">The Y coordinate in tile space.</param>
    /// <returns>True if the position is walkable, false if blocked or no collision data exists.</returns>
    public static bool IsPositionWalkable(World world, int tileX, int tileY)
    {
        return IsPositionWalkable(world, tileX, tileY, Direction.None);
    }

    /// <summary>
    /// Checks if a tile position is walkable from a specific direction.
    /// Uses tile-type collision from TileProperties (data-driven from Tiled tileset).
    /// Supports Pokemon-style directional blocking (ledges) via tile properties.
    /// </summary>
    /// <param name="world">The game world to query.</param>
    /// <param name="tileX">The X coordinate in tile space.</param>
    /// <param name="tileY">The Y coordinate in tile space.</param>
    /// <param name="fromDirection">Direction moving FROM (player's movement direction).</param>
    /// <returns>True if the position is walkable from this direction, false if blocked.</returns>
    public static bool IsPositionWalkable(
        World world,
        int tileX,
        int tileY,
        Direction fromDirection
    )
    {
        if (world == null)
        {
            return false;
        }

        // Query for entities with TileMap and TileProperties components
        var query = new QueryDescription().WithAll<TileMap, TileProperties>();

        bool isWalkable = true; // Default to walkable if no collision data found

        world.Query(
            in query,
            (Entity entity, ref TileMap tileMap, ref TileProperties tileProps) =>
            {
                // 1. Bounds checking - out of bounds is always solid
                if (tileX < 0 || tileY < 0 || tileX >= tileMap.Width || tileY >= tileMap.Height)
                {
                    isWalkable = false;
                    return;
                }

                // 2. Get tile at position
                int tileGid = tileMap.GroundLayer[tileY, tileX];
                if (tileGid == 0)
                {
                    return; // Empty tile (ID 0) is walkable
                }

                // 3. Check tile-type solid property (from tileset)
                if (tileProps.GetProperty<bool>(tileGid, "solid", defaultValue: false))
                {
                    isWalkable = false;
                    return;
                }

                // 4. Check tile-type ledges (from tileset properties)
                if (fromDirection != Direction.None && tileProps.HasProperty(tileGid, "ledge"))
                {
                    string ledgeDirection = tileProps.GetProperty<string>(
                        tileGid,
                        "ledge_direction",
                        defaultValue: ""
                    );

                    // Pokemon ledge logic: block opposite direction
                    // ledge_direction="down" means you can jump DOWN, but can't climb UP
                    bool blockedByLedge = (ledgeDirection, fromDirection) switch
                    {
                        ("down", Direction.Up) => true, // Can't climb up
                        ("up", Direction.Down) => true, // Can't climb down (rare)
                        ("left", Direction.Right) => true, // Can't go right
                        ("right", Direction.Left) => true, // Can't go left
                        _ => false,
                    };

                    if (blockedByLedge)
                    {
                        isWalkable = false;
                    }
                }
            }
        );

        return isWalkable;
    }

    /// <summary>
    /// Checks if a tile is a Pokemon-style ledge (has ledge property).
    /// </summary>
    /// <param name="world">The game world to query.</param>
    /// <param name="tileX">The X coordinate in tile space.</param>
    /// <param name="tileY">The Y coordinate in tile space.</param>
    /// <returns>True if the tile is a ledge, false otherwise.</returns>
    public static bool IsLedge(World world, int tileX, int tileY)
    {
        if (world == null)
        {
            return false;
        }

        var query = new QueryDescription().WithAll<TileMap, TileProperties>();
        bool ledgeFound = false;

        world.Query(
            in query,
            (ref TileMap tileMap, ref TileProperties tileProps) =>
            {
                if (tileX < 0 || tileY < 0 || tileX >= tileMap.Width || tileY >= tileMap.Height)
                {
                    return;
                }

                int tileGid = tileMap.GroundLayer[tileY, tileX];
                if (tileGid > 0 && tileProps.HasProperty(tileGid, "ledge"))
                {
                    ledgeFound = true;
                }
            }
        );

        return ledgeFound;
    }

    /// <summary>
    /// Gets the allowed jump direction for a ledge tile.
    /// </summary>
    /// <param name="world">The game world to query.</param>
    /// <param name="tileX">The X coordinate in tile space.</param>
    /// <param name="tileY">The Y coordinate in tile space.</param>
    /// <returns>The direction you can jump across this ledge, or None if not a ledge.</returns>
    public static Direction GetLedgeJumpDirection(World world, int tileX, int tileY)
    {
        if (world == null)
        {
            return Direction.None;
        }

        var query = new QueryDescription().WithAll<TileMap, TileProperties>();
        Direction jumpDirection = Direction.None;

        world.Query(
            in query,
            (ref TileMap tileMap, ref TileProperties tileProps) =>
            {
                if (tileX < 0 || tileY < 0 || tileX >= tileMap.Width || tileY >= tileMap.Height)
                {
                    return;
                }

                int tileGid = tileMap.GroundLayer[tileY, tileX];
                if (tileGid > 0 && tileProps.HasProperty(tileGid, "ledge"))
                {
                    string ledgeDirection = tileProps.GetProperty<string>(
                        tileGid,
                        "ledge_direction",
                        defaultValue: "down"
                    );

                    // Pokemon ledge logic: jump direction is same as ledge direction
                    jumpDirection = ledgeDirection switch
                    {
                        "down" => Direction.Down,
                        "up" => Direction.Up,
                        "left" => Direction.Left,
                        "right" => Direction.Right,
                        _ => Direction.Down,
                    };
                }
            }
        );

        return jumpDirection;
    }
}
