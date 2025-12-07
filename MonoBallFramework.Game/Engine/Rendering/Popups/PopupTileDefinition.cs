using System.Text.Json.Serialization;

namespace MonoBallFramework.Game.Engine.Rendering.Popups;

/// <summary>
///     Defines a single tile in a popup tile sheet.
/// </summary>
public class PopupTileDefinition
{
    /// <summary>
    ///     Tile index (0-based).
    /// </summary>
    [JsonPropertyName("Index")]
    public int Index { get; init; }

    /// <summary>
    ///     X position in the tile sheet (in pixels).
    /// </summary>
    [JsonPropertyName("X")]
    public int X { get; init; }

    /// <summary>
    ///     Y position in the tile sheet (in pixels).
    /// </summary>
    [JsonPropertyName("Y")]
    public int Y { get; init; }

    /// <summary>
    ///     Width of this tile (in pixels).
    /// </summary>
    [JsonPropertyName("Width")]
    public int Width { get; init; }

    /// <summary>
    ///     Height of this tile (in pixels).
    /// </summary>
    [JsonPropertyName("Height")]
    public int Height { get; init; }
}



