namespace MonoBallFramework.Game.Infrastructure.Configuration;

/// <summary>
///     Configuration for gameplay settings (camera, input, pools).
/// </summary>
public class GameplayConfig
{
    /// <summary>
    ///     Default camera zoom level (fallback only).
    ///     Note: The actual zoom is calculated automatically by Camera.UpdateViewportForResize()
    ///     to match GBA native resolution (240x160 = 15x10 tiles visible).
    ///     This value is overridden on first window resize.
    /// </summary>
    public float DefaultZoom { get; set; } = 1.0f;

    /// <summary>
    ///     Camera zoom transition speed (0.0 to 1.0).
    /// </summary>
    public float ZoomTransitionSpeed { get; set; } = 0.1f;

    /// <summary>
    ///     Default tile size in pixels (used when map info is unavailable).
    /// </summary>
    public int DefaultTileSize { get; set; } = 16;

    /// <summary>
    ///     Input buffering configuration.
    /// </summary>
    public InputBufferConfig InputBuffer { get; set; } = new();

    /// <summary>
    ///     Creates a default gameplay configuration.
    /// </summary>
    public static GameplayConfig CreateDefault()
    {
        return new GameplayConfig();
    }

    /// <summary>
    ///     Creates a development configuration.
    /// </summary>
    public static GameplayConfig CreateDevelopment()
    {
        return new GameplayConfig();
    }

    /// <summary>
    ///     Creates a production configuration optimized for performance.
    /// </summary>
    public static GameplayConfig CreateProduction()
    {
        return new GameplayConfig();
    }
}

/// <summary>
///     Configuration for input buffering.
/// </summary>
public class InputBufferConfig
{
    /// <summary>
    ///     Maximum number of buffered inputs.
    /// </summary>
    public int MaxBufferedInputs { get; set; } = 5;

    /// <summary>
    ///     Input timeout in seconds before buffer is cleared.
    /// </summary>
    public float TimeoutSeconds { get; set; } = 0.2f;
}
