using Microsoft.Xna.Framework;
using PokeSharp.Engine.UI.Debug.Components.Base;
using PokeSharp.Engine.UI.Debug.Core;
using PokeSharp.Engine.UI.Debug.Layout;

namespace PokeSharp.Engine.UI.Debug.Components.Controls;

/// <summary>
/// A reusable component for displaying helpful hints and keyboard shortcuts.
/// Typically rendered at the bottom of input areas or panels.
/// </summary>
public class HintBar : UIComponent
{
    private string _text = string.Empty;

    // Visual properties
    public Color TextColor { get; set; } = UITheme.Dark.Success; // Dim green for helpful hints
    public Color BackgroundColor { get; set; } = Color.Transparent;
    public float FontSize { get; set; } = 1.0f; // Scale factor
    public float Padding { get; set; } = UITheme.Dark.PaddingSmall;

    public HintBar(string id)
    {
        Id = id;
    }

    /// <summary>
    /// Sets the hint text to display.
    /// </summary>
    public void SetText(string text)
    {
        _text = text ?? string.Empty;
    }

    /// <summary>
    /// Gets the current hint text.
    /// </summary>
    public string Text => _text;

    /// <summary>
    /// Calculates the desired height for this hint bar.
    /// </summary>
    public float GetDesiredHeight(UIRenderer? renderer = null)
    {
        if (string.IsNullOrEmpty(_text))
            return 0;

        float lineHeight = 20f; // Default

        if (renderer != null)
        {
            lineHeight = renderer.GetLineHeight();
        }
        else
        {
            try
            {
                if (Renderer != null)
                    lineHeight = Renderer.GetLineHeight();
            }
            catch
            {
                // No context available, use default
            }
        }

        return lineHeight + Padding * 2;
    }

    protected override void OnRender(UIContext context)
    {
        if (string.IsNullOrEmpty(_text))
            return;

        var renderer = Renderer;
        var resolvedRect = Rect;

        // Draw background if specified
        if (BackgroundColor.A > 0)
        {
            renderer.DrawRectangle(resolvedRect, BackgroundColor);
        }

        // Draw hint text
        var textPos = new Vector2(
            resolvedRect.X + Padding,
            resolvedRect.Y + Padding
        );

        renderer.DrawText(_text, textPos, TextColor);
    }

    protected override bool IsInteractive() => false;
}


