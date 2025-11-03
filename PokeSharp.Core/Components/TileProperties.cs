namespace PokeSharp.Core.Components;

/// <summary>
/// Component that stores custom properties for tiles in a map.
/// Properties are fully data-driven from Tiled editor - no hardcoded types!
/// Supports per-tile properties from Tiled custom properties.
/// </summary>
/// <remarks>
/// Properties are stored as key-value pairs where keys come from Tiled editor.
/// This allows mods and data files to define new tile behaviors without code changes.
/// 
/// Example Tiled properties:
/// - "passable": true/false
/// - "encounter_rate": 10
/// - "terrain_type": "grass"
/// - "script": "triggers/heal_tile.csx"
/// - "sound": "footstep_sand"
/// - Any custom property name!
/// </remarks>
public struct TileProperties
{
    /// <summary>
    /// Gets or sets the dictionary mapping tile ID to its properties.
    /// Key: Tile GID (global tile ID from Tiled)
    /// Value: Properties dictionary (string key → object value)
    /// </summary>
    public Dictionary<int, Dictionary<string, object>> TilePropertyMap { get; set; }

    /// <summary>
    /// Initializes a new instance of the TileProperties struct.
    /// </summary>
    public TileProperties()
    {
        TilePropertyMap = new Dictionary<int, Dictionary<string, object>>();
    }

    /// <summary>
    /// Gets properties for a specific tile GID.
    /// </summary>
    /// <param name="tileGid">The global tile ID.</param>
    /// <returns>Properties dictionary for the tile, or empty if none.</returns>
    public readonly Dictionary<string, object> GetPropertiesForTile(int tileGid)
    {
        return TilePropertyMap.TryGetValue(tileGid, out var props) 
            ? props 
            : new Dictionary<string, object>();
    }

    /// <summary>
    /// Checks if a tile has a specific property.
    /// </summary>
    /// <param name="tileGid">The global tile ID.</param>
    /// <param name="propertyName">The property name to check.</param>
    /// <returns>True if the property exists; otherwise, false.</returns>
    public readonly bool HasProperty(int tileGid, string propertyName)
    {
        return TilePropertyMap.TryGetValue(tileGid, out var props) && 
               props.ContainsKey(propertyName);
    }

    /// <summary>
    /// Gets a typed property value for a tile.
    /// </summary>
    /// <typeparam name="T">The expected type of the property value.</typeparam>
    /// <param name="tileGid">The global tile ID.</param>
    /// <param name="propertyName">The property name.</param>
    /// <param name="defaultValue">Default value if property doesn't exist.</param>
    /// <returns>The property value, or default if not found.</returns>
    public readonly T GetProperty<T>(int tileGid, string propertyName, T defaultValue = default!)
    {
        if (!TilePropertyMap.TryGetValue(tileGid, out var props))
        {
            return defaultValue;
        }

        if (!props.TryGetValue(propertyName, out var value))
        {
            return defaultValue;
        }

        // Handle type conversion from JSON types
        try
        {
            return value is T typedValue ? typedValue : (T)Convert.ChangeType(value, typeof(T));
        }
        catch
        {
            return defaultValue;
        }
    }

    /// <summary>
    /// Tries to get a typed property value for a tile.
    /// </summary>
    /// <typeparam name="T">The expected type of the property value.</typeparam>
    /// <param name="tileGid">The global tile ID.</param>
    /// <param name="propertyName">The property name.</param>
    /// <param name="value">The property value if found.</param>
    /// <returns>True if the property was found and converted; otherwise, false.</returns>
    public readonly bool TryGetProperty<T>(int tileGid, string propertyName, out T value)
    {
        value = default!;

        if (!TilePropertyMap.TryGetValue(tileGid, out var props))
        {
            return false;
        }

        if (!props.TryGetValue(propertyName, out var objValue))
        {
            return false;
        }

        try
        {
            value = objValue is T typedValue ? typedValue : (T)Convert.ChangeType(objValue, typeof(T));
            return true;
        }
        catch
        {
            return false;
        }
    }

    /// <summary>
    /// Sets a property for a specific tile (runtime modification).
    /// </summary>
    /// <param name="tileGid">The global tile ID.</param>
    /// <param name="propertyName">The property name.</param>
    /// <param name="value">The property value.</param>
    public void SetProperty(int tileGid, string propertyName, object value)
    {
        if (!TilePropertyMap.ContainsKey(tileGid))
        {
            TilePropertyMap[tileGid] = new Dictionary<string, object>();
        }

        TilePropertyMap[tileGid][propertyName] = value;
    }

    /// <summary>
    /// Gets the number of tiles that have properties.
    /// </summary>
    public readonly int TileCount => TilePropertyMap.Count;

    /// <summary>
    /// Gets all tile GIDs that have properties.
    /// </summary>
    public readonly IEnumerable<int> TileGids => TilePropertyMap.Keys;
}


