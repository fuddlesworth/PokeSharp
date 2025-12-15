namespace MonoBallFramework.Game.Engine.Rendering.Popups;

/// <summary>
///     Configuration options for PopupRegistry defaults.
/// </summary>
public sealed class PopupRegistryOptions
{
    /// <summary>
    ///     Configuration section name in appsettings.json.
    /// </summary>
    public const string SectionName = "PopupRegistry";

    /// <summary>
    ///     Gets or sets the default background ID.
    /// </summary>
    public string DefaultBackgroundId { get; set; } = "base:popup:background/wood";

    /// <summary>
    ///     Gets or sets the default outline ID.
    /// </summary>
    public string DefaultOutlineId { get; set; } = "base:popup:outline/wood_outline";

    /// <summary>
    ///     Gets or sets the default theme name.
    /// </summary>
    public string DefaultTheme { get; set; } = "wood";
}
