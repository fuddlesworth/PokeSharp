using Arch.Core;
using Arch.Core.Extensions;
using PokeSharp.Core.Components;

namespace PokeSharp.Core.Systems;

/// <summary>
/// System that provides helper methods for querying tile properties from maps.
/// All properties are data-driven from Tiled editor - no hardcoded tile types!
/// </summary>
/// <remarks>
/// This system doesn't update each frame - it's a query helper.
/// Other systems (EncounterSystem, FootstepSystem, etc.) use this to query tile data.
///
/// Example properties from Tiled:
/// - "passable": true/false
/// - "encounter_rate": 10 (0-255)
/// - "terrain_type": "grass", "water", "sand"
/// - "script": "triggers/heal_tile.csx"
/// - "sound": "footstep_sand"
///
/// Mods can add unlimited custom properties without code changes!
/// </remarks>
public class TilePropertySystem : BaseSystem
{
    /// <inheritdoc/>
    public override int Priority => SystemPriority.TileProperties;

    /// <inheritdoc/>
    public override void Update(World world, float deltaTime)
    {
        // This system doesn't update - it's a query helper
        // Other systems call GetTilePropertyAt() during their updates
    }

    /// <summary>
    /// Gets the tile GID at a specific grid position in the map.
    /// </summary>
    /// <param name="world">The ECS world.</param>
    /// <param name="gridX">Grid X coordinate.</param>
    /// <param name="gridY">Grid Y coordinate.</param>
    /// <param name="layer">Which layer to query ("ground", "objects", "overhead").</param>
    /// <returns>Tile GID at that position, or 0 if empty/out of bounds.</returns>
    public static int GetTileGidAt(World world, int gridX, int gridY, string layer = "ground")
    {
        var mapQuery = new QueryDescription().WithAll<TileMap>();

        int tileGid = 0;

        world.Query(
            in mapQuery,
            (ref TileMap map) =>
            {
                // Check bounds
                if (gridX < 0 || gridX >= map.Width || gridY < 0 || gridY >= map.Height)
                {
                    return;
                }

                // Get tile from appropriate layer
                tileGid = layer.ToLowerInvariant() switch
                {
                    "ground" => map.GroundLayer[gridY, gridX],
                    "objects" => map.ObjectLayer[gridY, gridX],
                    "overhead" => map.OverheadLayer[gridY, gridX],
                    _ => map.GroundLayer[gridY, gridX],
                };
            }
        );

        return tileGid;
    }

    /// <summary>
    /// Gets custom properties for the tile at a specific grid position (data-driven).
    /// </summary>
    /// <param name="world">The ECS world.</param>
    /// <param name="gridX">Grid X coordinate.</param>
    /// <param name="gridY">Grid Y coordinate.</param>
    /// <param name="layer">Which layer to query ("ground", "objects", "overhead").</param>
    /// <returns>Properties dictionary for the tile, or empty if no properties.</returns>
    public static Dictionary<string, object> GetTilePropertiesAt(
        World world,
        int gridX,
        int gridY,
        string layer = "ground"
    )
    {
        // Get the tile GID at this position
        int tileGid = GetTileGidAt(world, gridX, gridY, layer);

        if (tileGid == 0)
        {
            return new Dictionary<string, object>();
        }

        // Query TileProperties component
        var propsQuery = new QueryDescription().WithAll<TileProperties>();
        var result = new Dictionary<string, object>();

        world.Query(
            in propsQuery,
            (ref TileProperties props) =>
            {
                result = props.GetPropertiesForTile(tileGid);
            }
        );

        return result;
    }

    /// <summary>
    /// Checks if a tile at a specific position has a given property (data-driven).
    /// </summary>
    /// <param name="world">The ECS world.</param>
    /// <param name="gridX">Grid X coordinate.</param>
    /// <param name="gridY">Grid Y coordinate.</param>
    /// <param name="propertyName">The property name to check (e.g., "passable", "encounter_rate").</param>
    /// <param name="layer">Which layer to query.</param>
    /// <returns>True if the tile has the property; otherwise, false.</returns>
    public static bool TileHasProperty(
        World world,
        int gridX,
        int gridY,
        string propertyName,
        string layer = "ground"
    )
    {
        int tileGid = GetTileGidAt(world, gridX, gridY, layer);

        if (tileGid == 0)
        {
            return false;
        }

        var propsQuery = new QueryDescription().WithAll<TileProperties>();
        bool hasProperty = false;

        world.Query(
            in propsQuery,
            (ref TileProperties props) =>
            {
                hasProperty = props.HasProperty(tileGid, propertyName);
            }
        );

        return hasProperty;
    }

    /// <summary>
    /// Gets a typed property value for the tile at a specific position (data-driven).
    /// </summary>
    /// <typeparam name="T">The expected type of the property value.</typeparam>
    /// <param name="world">The ECS world.</param>
    /// <param name="gridX">Grid X coordinate.</param>
    /// <param name="gridY">Grid Y coordinate.</param>
    /// <param name="propertyName">The property name (e.g., "encounter_rate", "terrain_type").</param>
    /// <param name="defaultValue">Default value if property doesn't exist.</param>
    /// <param name="layer">Which layer to query.</param>
    /// <returns>The property value, or default if not found.</returns>
    public static T GetTileProperty<T>(
        World world,
        int gridX,
        int gridY,
        string propertyName,
        T defaultValue = default!,
        string layer = "ground"
    )
    {
        int tileGid = GetTileGidAt(world, gridX, gridY, layer);

        if (tileGid == 0)
        {
            return defaultValue;
        }

        var propsQuery = new QueryDescription().WithAll<TileProperties>();
        T result = defaultValue;

        world.Query(
            in propsQuery,
            (ref TileProperties props) =>
            {
                result = props.GetProperty(tileGid, propertyName, defaultValue);
            }
        );

        return result;
    }

    /// <summary>
    /// Tries to get a typed property value for the tile at a specific position.
    /// </summary>
    /// <typeparam name="T">The expected type of the property value.</typeparam>
    /// <param name="world">The ECS world.</param>
    /// <param name="gridX">Grid X coordinate.</param>
    /// <param name="gridY">Grid Y coordinate.</param>
    /// <param name="propertyName">The property name.</param>
    /// <param name="value">The property value if found.</param>
    /// <param name="layer">Which layer to query.</param>
    /// <returns>True if the property was found; otherwise, false.</returns>
    public static bool TryGetTileProperty<T>(
        World world,
        int gridX,
        int gridY,
        string propertyName,
        out T value,
        string layer = "ground"
    )
    {
        value = default!;
        int tileGid = GetTileGidAt(world, gridX, gridY, layer);

        if (tileGid == 0)
        {
            return false;
        }

        var propsQuery = new QueryDescription().WithAll<TileProperties>();
        bool found = false;
        T localValue = default!;

        world.Query(
            in propsQuery,
            (ref TileProperties props) =>
            {
                found = props.TryGetProperty(tileGid, propertyName, out localValue);
            }
        );

        value = localValue;
        return found;
    }
}
