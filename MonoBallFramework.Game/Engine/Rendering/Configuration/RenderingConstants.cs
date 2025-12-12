namespace MonoBallFramework.Game.Engine.Rendering.Configuration;

/// <summary>
///     Centralized constants for rendering configuration.
///     Contains all hardcoded values used throughout the rendering pipeline.
///     Based on AudioConstants pattern.
/// </summary>
public static class RenderingConstants
{
    // ===== Sprite Batching Configuration =====

    /// <summary>Default maximum sprites per batch.</summary>
    public const int DefaultMaxSpriteBatchSize = 2048;

    /// <summary>Minimum sprites per batch.</summary>
    public const int MinSpriteBatchSize = 128;

    /// <summary>Maximum sprites per batch.</summary>
    public const int MaxSpriteBatchSize = 16384;

    // ===== Layer Configuration =====

    /// <summary>Default number of rendering layers.</summary>
    public const int DefaultLayerCount = 8;

    /// <summary>Default maximum render targets.</summary>
    public const int DefaultMaxRenderTargets = 4;

    /// <summary>
    ///     Layer index after which sprites should be rendered.
    ///     Sprites are rendered between object layers and overhead layers.
    /// </summary>
    /// <remarks>
    ///     Layer ordering:
    ///     - Layer 0: Ground
    ///     - Layer 1: Objects
    ///     - [Sprites rendered here]
    ///     - Layer 2+: Overhead (trees, roofs, etc.)
    /// </remarks>
    public const int SpriteRenderAfterLayer = 1;

    // ===== Rendering Defaults =====

    /// <summary>Default tile size in pixels.</summary>
    public const int DefaultTileSize = 16;

    /// <summary>Default camera zoom level.</summary>
    public const float DefaultCameraZoom = 1.0f;

    /// <summary>
    ///     Maximum Y coordinate for render distance normalization in z-ordering.
    ///     Used to calculate depth values for proper sprite layering.
    /// </summary>
    /// <remarks>
    ///     This value should be larger than any expected Y coordinate in your game world.
    ///     Sprites with Y coordinates beyond this will be clamped for rendering purposes.
    /// </remarks>
    public const float MaxRenderDistance = 10000f;

    // ===== Performance Configuration =====

    /// <summary>
    ///     Frame interval for performance logging (in frames).
    ///     Performance statistics are logged every N frames to avoid log spam.
    /// </summary>
    /// <remarks>
    ///     300 frames at 60fps = 5 seconds between log entries.
    ///     Adjust this value to log more or less frequently.
    /// </remarks>
    public const int PerformanceLogInterval = 300;

    // ===== Asset Configuration =====

    /// <summary>
    ///     Default assets root directory name.
    ///     This is the base directory where game assets are stored.
    /// </summary>
    /// <remarks>
    ///     This default can be overridden in AssetManager constructor.
    ///     All asset paths are resolved relative to this root directory.
    /// </remarks>
    public const string DefaultAssetRoot = "Assets";
}
