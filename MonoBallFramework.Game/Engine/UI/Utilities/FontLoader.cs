using FontStashSharp;
using Microsoft.Extensions.Logging;
using MonoBallFramework.Game.Engine.Content;

namespace MonoBallFramework.Game.Engine.UI.Utilities;

/// <summary>
/// Provides font loading and path resolution using the content provider system.
/// Supports mod-overridable fonts.
/// </summary>
public sealed class FontLoader
{
    private readonly IContentProvider _contentProvider;
    private readonly ILogger<FontLoader> _logger;

    /// <summary>
    /// Default game font filename.
    /// </summary>
    public const string GameFont = "pokemon.ttf";

    /// <summary>
    /// Debug/developer font filename.
    /// </summary>
    public const string DebugFont = "0xProtoNerdFontMono-Regular.ttf";

    /// <summary>
    /// Initializes a new instance of the <see cref="FontLoader"/> class.
    /// </summary>
    public FontLoader(
        IContentProvider contentProvider,
        ILogger<FontLoader> logger)
    {
        _contentProvider = contentProvider ?? throw new ArgumentNullException(nameof(contentProvider));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    /// <summary>
    /// Resolves the full path to a font file.
    /// </summary>
    /// <param name="fontFileName">The font filename (e.g., "pokemon.ttf").</param>
    /// <returns>The resolved path, or null if not found.</returns>
    public string? ResolveFontPath(string fontFileName)
    {
        string? path = _contentProvider.ResolveContentPath("Fonts", fontFileName);

        if (path == null)
        {
            _logger.LogWarning("Font not found: {Font}", fontFileName);
        }
        else
        {
            _logger.LogDebug("Resolved font {Font} to {Path}", fontFileName, path);
        }

        return path;
    }

    /// <summary>
    /// Gets the path to the main game font.
    /// </summary>
    /// <exception cref="FileNotFoundException">Thrown if font not found.</exception>
    public string GetGameFontPath() => ResolveFontPath(GameFont)
        ?? throw new FileNotFoundException($"Game font not found: {GameFont}");

    /// <summary>
    /// Gets the path to the debug font.
    /// </summary>
    /// <exception cref="FileNotFoundException">Thrown if font not found.</exception>
    public string GetDebugFontPath() => ResolveFontPath(DebugFont)
        ?? throw new FileNotFoundException($"Debug font not found: {DebugFont}");

    /// <summary>
    /// Checks if a font exists.
    /// </summary>
    public bool FontExists(string fontFileName) =>
        _contentProvider.ContentExists("Fonts", fontFileName);

    /// <summary>
    /// Loads a font and returns a FontSystem ready for use.
    /// </summary>
    /// <param name="fontFileName">The font filename to load.</param>
    /// <returns>A FontSystem loaded with the specified font.</returns>
    /// <exception cref="FileNotFoundException">Thrown if font not found.</exception>
    public FontSystem LoadFont(string fontFileName)
    {
        string? fontPath = ResolveFontPath(fontFileName);

        if (fontPath == null)
        {
            throw new FileNotFoundException($"Font not found: {fontFileName}");
        }

        var fontSystem = new FontSystem();
        fontSystem.AddFont(File.ReadAllBytes(fontPath));

        _logger.LogDebug("Loaded font {Font} from {Path}", fontFileName, fontPath);
        return fontSystem;
    }

    /// <summary>
    /// Loads the debug font.
    /// </summary>
    /// <returns>A FontSystem loaded with the debug font.</returns>
    public FontSystem LoadDebugFont() => LoadFont(DebugFont);

    /// <summary>
    /// Loads the game font.
    /// </summary>
    /// <returns>A FontSystem loaded with the game font.</returns>
    public FontSystem LoadGameFont() => LoadFont(GameFont);
}
