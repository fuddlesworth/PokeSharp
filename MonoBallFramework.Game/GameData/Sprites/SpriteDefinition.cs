using System.Text.Json.Serialization;

namespace MonoBallFramework.Game.GameData.Sprites;

/// <summary>
///     Definition for a sprite asset. Data file lives in Assets/Definitions/Sprites/.
///     Follows the same pattern as popup outline definitions.
///
///     ID Format: base:sprite:{type}/{category}/{name}
///     Example: base:sprite:npcs/elite_four/drake
/// </summary>
public class SpriteDefinition
{
    /// <summary>
    ///     Unique identifier in format: base:sprite:{type}/{category}/{name}
    /// </summary>
    [JsonPropertyName("Id")]
    public string Id { get; set; } = string.Empty;

    /// <summary>
    ///     Human-readable display name.
    /// </summary>
    [JsonPropertyName("DisplayName")]
    public string DisplayName { get; set; } = string.Empty;

    /// <summary>
    ///     Asset type - always "Sprite" for sprite definitions.
    /// </summary>
    [JsonPropertyName("Type")]
    public string Type { get; set; } = "Sprite";

    /// <summary>
    ///     Relative path to texture from Assets root.
    ///     Example: "Graphics/Sprites/npcs/elite_four/drake.png"
    /// </summary>
    [JsonPropertyName("TexturePath")]
    public string TexturePath { get; set; } = string.Empty;

    [JsonPropertyName("FrameWidth")]
    public int FrameWidth { get; set; }

    [JsonPropertyName("FrameHeight")]
    public int FrameHeight { get; set; }

    [JsonPropertyName("FrameCount")]
    public int FrameCount { get; set; }

    [JsonPropertyName("Frames")]
    public List<SpriteFrameDefinition> Frames { get; set; } = new();

    [JsonPropertyName("Animations")]
    public List<SpriteAnimationDefinition> Animations { get; set; } = new();

    /// <summary>
    ///     Extracts the sprite type from the ID (e.g., "npcs" from "base:sprite:npcs/elite_four/drake")
    /// </summary>
    public string GetSpriteType()
    {
        // Format: base:sprite:{type}/{category}/{name}
        var parts = Id.Split(':');
        if (parts.Length >= 3)
        {
            var path = parts[2];
            var segments = path.Split('/');
            return segments.Length > 0 ? segments[0] : string.Empty;
        }
        return string.Empty;
    }

    /// <summary>
    ///     Extracts the category from the ID (e.g., "elite_four" from "base:sprite:npcs/elite_four/drake")
    /// </summary>
    public string GetCategory()
    {
        var parts = Id.Split(':');
        if (parts.Length >= 3)
        {
            var path = parts[2];
            var segments = path.Split('/');
            return segments.Length > 1 ? segments[1] : string.Empty;
        }
        return string.Empty;
    }

    /// <summary>
    ///     Extracts the sprite name from the ID (e.g., "drake" from "base:sprite:npcs/elite_four/drake")
    /// </summary>
    public string GetSpriteName()
    {
        var parts = Id.Split(':');
        if (parts.Length >= 3)
        {
            var path = parts[2];
            var segments = path.Split('/');
            return segments.Length > 2 ? segments[2] : segments.Length > 0 ? segments[^1] : string.Empty;
        }
        return string.Empty;
    }
}

/// <summary>
///     Definition for a single sprite frame.
/// </summary>
public class SpriteFrameDefinition
{
    [JsonPropertyName("Index")]
    public int Index { get; set; }

    [JsonPropertyName("X")]
    public int X { get; set; }

    [JsonPropertyName("Y")]
    public int Y { get; set; }

    [JsonPropertyName("Width")]
    public int Width { get; set; }

    [JsonPropertyName("Height")]
    public int Height { get; set; }
}

/// <summary>
///     Definition for a sprite animation.
/// </summary>
public class SpriteAnimationDefinition
{
    [JsonPropertyName("Name")]
    public string Name { get; set; } = string.Empty;

    [JsonPropertyName("Loop")]
    public bool Loop { get; set; }

    [JsonPropertyName("FrameIndices")]
    public int[] FrameIndices { get; set; } = Array.Empty<int>();

    [JsonPropertyName("FrameDurations")]
    public float[] FrameDurations { get; set; } = Array.Empty<float>();

    [JsonPropertyName("FlipHorizontal")]
    public bool FlipHorizontal { get; set; }
}
