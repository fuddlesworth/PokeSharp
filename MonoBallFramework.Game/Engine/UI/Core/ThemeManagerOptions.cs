namespace MonoBallFramework.Game.Engine.UI.Core;

/// <summary>
///     Configuration options for ThemeManager defaults.
/// </summary>
public sealed class ThemeManagerOptions
{
    /// <summary>
    ///     Configuration section name in appsettings.json.
    /// </summary>
    public const string SectionName = "ThemeManager";

    /// <summary>
    ///     Gets or sets the default theme name.
    ///     Available themes: onedark, monokai, dracula, gruvbox, nord, solarized, solarized-light, pokeball, pokeball-light
    /// </summary>
    public string DefaultTheme { get; set; } = "pokeball";
}
