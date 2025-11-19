using FontStashSharp;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;

namespace PokeSharp.Engine.Debug.Console.UI;

/// <summary>
/// Handles font rendering for the console using FontStashSharp.
/// Requires a TrueType font to be available on the system.
/// </summary>
public class ConsoleFontRenderer : IDisposable
{
    private readonly Texture2D _pixel;
    private readonly SpriteBatch _spriteBatch;
    private DynamicSpriteFont _font = null!;
    private int _fontSize = 16;
    private const int DefaultFontSize = 16;
    private const int MinFontSize = 8;
    private const int MaxFontSize = 32;
    private bool _disposed;

    public ConsoleFontRenderer(GraphicsDevice graphicsDevice, SpriteBatch spriteBatch)
    {
        _spriteBatch = spriteBatch;

        // Create a 1x1 white pixel for drawing UI elements
        _pixel = new Texture2D(graphicsDevice, 1, 1);
        _pixel.SetData(new[] { Color.White });

        // Load font (required - no fallback)
            LoadFont();

        if (_font == null)
        {
            throw new InvalidOperationException(
                "Failed to load console font. No suitable TrueType font found on system. " +
                "Checked paths: Monaco, Menlo, Consolas, Courier, Liberation Mono, DejaVu Sans Mono.");
        }
    }

    /// <summary>
    /// Loads the default font from system.
    /// </summary>
    private void LoadFont()
    {
        // Try to use system fonts
        var fontSystem = new FontSystem();

        // Try to load common monospace fonts
        string[] fontPaths = new[]
        {
            // macOS
            "/System/Library/Fonts/Monaco.ttf",
            "/System/Library/Fonts/Menlo.ttc",
            "/System/Library/Fonts/Courier New.ttf",

            // Windows
            "C:\\Windows\\Fonts\\consola.ttf",
            "C:\\Windows\\Fonts\\cour.ttf",

            // Linux
            "/usr/share/fonts/truetype/liberation/LiberationMono-Regular.ttf",
            "/usr/share/fonts/truetype/dejavu/DejaVuSansMono.ttf"
        };

        foreach (var path in fontPaths)
        {
            if (System.IO.File.Exists(path))
            {
                fontSystem.AddFont(System.IO.File.ReadAllBytes(path));
                var font = fontSystem.GetFont(_fontSize);
                if (font != null)
                {
                    _font = font;
                return;
                }
            }
        }
    }

    /// <summary>
    /// Gets the current font size.
    /// </summary>
    public int GetFontSize() => _fontSize;

    /// <summary>
    /// Sets the font size and reloads the font.
    /// </summary>
    /// <param name="size">The new font size (clamped to valid range).</param>
    public void SetFontSize(int size)
    {
        _fontSize = Math.Clamp(size, MinFontSize, MaxFontSize);
        LoadFont();
    }

    /// <summary>
    /// Increases the font size by the specified amount.
    /// </summary>
    /// <param name="increment">The amount to increase (default: 2).</param>
    /// <returns>The new font size.</returns>
    public int IncreaseFontSize(int increment = 2)
    {
        SetFontSize(_fontSize + increment);
        return _fontSize;
    }

    /// <summary>
    /// Decreases the font size by the specified amount.
    /// </summary>
    /// <param name="decrement">The amount to decrease (default: 2).</param>
    /// <returns>The new font size.</returns>
    public int DecreaseFontSize(int decrement = 2)
    {
        SetFontSize(_fontSize - decrement);
        return _fontSize;
    }

    /// <summary>
    /// Resets the font size to the default.
    /// </summary>
    /// <returns>The default font size.</returns>
    public int ResetFontSize()
    {
        SetFontSize(DefaultFontSize);
        return _fontSize;
    }

    /// <summary>
    /// Gets the line height for the current font.
    /// This uses FontStashSharp's LineHeight which includes descenders.
    /// </summary>
    public int GetLineHeight()
    {
        // FontStashSharp's LineHeight includes proper spacing for descenders
        return _font.LineHeight;
    }

    /// <summary>
    /// Gets the baseline offset (distance from top of line to text baseline).
    /// </summary>
    public int GetBaseline()
        {
        // Measure height of capital letter to get baseline position
            return (int)_font.MeasureString("A").Y;
    }

    /// <summary>
    /// Draws text at the specified position with color.
    /// </summary>
    public void DrawString(string text, int x, int y, Color color)
    {
            _spriteBatch.DrawString(_font, text, new Vector2(x, y), color);
    }

    /// <summary>
    /// Measures the size of the given text.
    /// </summary>
    public Vector2 MeasureString(string text)
        {
            return _font.MeasureString(text);
    }

    /// <summary>
    /// Gets whether the font was successfully loaded.
    /// </summary>
    public bool IsFontLoaded => _font != null;

    /// <summary>
    /// Disposes of the font renderer resources.
    /// </summary>
    public void Dispose()
    {
        Dispose(disposing: true);
        GC.SuppressFinalize(this);
    }

    /// <summary>
    /// Disposes of the font renderer resources.
    /// </summary>
    /// <param name="disposing">True if called from Dispose(), false if called from finalizer.</param>
    protected virtual void Dispose(bool disposing)
    {
        if (_disposed)
            return;

        if (disposing)
        {
            // Dispose managed resources
            _pixel?.Dispose();
            // Note: _font and _spriteBatch are not disposed here as they may be shared resources
            // _spriteBatch is passed in via constructor, so caller owns it
        }

        _disposed = true;
    }
}

