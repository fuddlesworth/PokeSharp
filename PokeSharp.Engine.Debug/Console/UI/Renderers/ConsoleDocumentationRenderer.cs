using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using PokeSharp.Engine.Debug.Console.Configuration;
using System.Linq;
using static PokeSharp.Engine.Debug.Console.Configuration.ConsoleColors;

namespace PokeSharp.Engine.Debug.Console.UI.Renderers;

/// <summary>
/// Handles rendering of documentation popup.
/// Separated from QuakeConsole to follow Single Responsibility Principle.
/// </summary>
public class ConsoleDocumentationRenderer
{
    private readonly ConsoleFontRenderer _fontRenderer;
    private readonly SpriteBatch _spriteBatch;
    private readonly Texture2D _pixel;
    private float _screenWidth;
    private float _screenHeight;

    public ConsoleDocumentationRenderer(ConsoleFontRenderer fontRenderer, SpriteBatch spriteBatch, Texture2D pixel,
                                       float screenWidth, float screenHeight)
    {
        _fontRenderer = fontRenderer;
        _spriteBatch = spriteBatch;
        _pixel = pixel;
        _screenWidth = screenWidth;
        _screenHeight = screenHeight;
    }

    /// <summary>
    /// Updates the screen dimensions when window is resized.
    /// </summary>
    public void UpdateScreenSize(float screenWidth, float screenHeight)
    {
        _screenWidth = screenWidth;
        _screenHeight = screenHeight;
    }

    /// <summary>
    /// Draws the documentation popup in the center of the screen.
    /// </summary>
    public void DrawDocumentationPopup(string documentationText, int lineHeight)
    {
        if (string.IsNullOrEmpty(documentationText))
            return;

        var lines = documentationText.Split('\n');
        int maxLineLength = lines.Max(l => l.Length);

        // Calculate popup dimensions
        int popupWidth = CalculatePopupWidth(maxLineLength);
        int popupHeight = CalculatePopupHeight(lines.Length, lineHeight);

        // Center the popup
        int popupX = ((int)_screenWidth - popupWidth) / 2;
        int popupY = ((int)_screenHeight - popupHeight) / 2;

        // Draw overlay
        DrawOverlay();

        // Draw popup
        DrawPopupBackground(popupX, popupY, popupWidth, popupHeight);
        DrawPopupBorder(popupX, popupY, popupWidth, popupHeight);
        DrawDocumentationText(lines, popupX, popupY, popupWidth, popupHeight, lineHeight);
        DrawCloseInstruction(popupX, popupY, popupWidth, popupHeight, lineHeight);
    }

    private int CalculatePopupWidth(int maxLineLength)
    {
        int calculatedWidth = maxLineLength * ConsoleConstants.Rendering.LineNumberCharWidth + 40;
        int maxWidth = (int)(_screenWidth * ConsoleConstants.Rendering.DocumentationPopupWidthRatio / 100f);
        return Math.Min(maxWidth, calculatedWidth);
    }

    private int CalculatePopupHeight(int lineCount, int lineHeight)
    {
        int calculatedHeight = lineCount * lineHeight + 20;
        int maxHeight = (int)(_screenHeight * ConsoleConstants.Rendering.DocumentationPopupHeightRatio / 100f);
        return Math.Min(maxHeight, calculatedHeight);
    }

    private void DrawOverlay()
    {
        DrawRectangle(0, 0, (int)_screenWidth, (int)_screenHeight,
                     Background_Overlay);
    }

    private void DrawPopupBackground(int x, int y, int width, int height)
    {
        DrawRectangle(x, y, width, height, Background_Secondary);
    }

    private void DrawPopupBorder(int x, int y, int width, int height)
    {
        int borderWidth = ConsoleConstants.Rendering.DocumentationPopupBorderWidth;
        var borderColor = Border_Default;

        DrawRectangle(x, y, width, borderWidth, borderColor); // Top
        DrawRectangle(x, y + height - borderWidth, width, borderWidth, borderColor); // Bottom
        DrawRectangle(x, y, borderWidth, height, borderColor); // Left
        DrawRectangle(x + width - borderWidth, y, borderWidth, height, borderColor); // Right
    }

    private void DrawDocumentationText(string[] lines, int popupX, int popupY, int popupWidth, int popupHeight, int lineHeight)
    {
        int textY = popupY + ConsoleConstants.Rendering.DocumentationPopupPadding;
        int textX = popupX + ConsoleConstants.Rendering.DocumentationPopupPadding;

        foreach (var line in lines)
        {
            if (textY + lineHeight > popupY + popupHeight - ConsoleConstants.Rendering.DocumentationPopupPadding)
                break; // Don't draw beyond popup bounds

            Color lineColor = GetDocumentationLineColor(line);
            _fontRenderer.DrawString(line, textX, textY, lineColor);
            textY += lineHeight;
        }
    }

    private Color GetDocumentationLineColor(string line)
    {
        if (line.Contains("═══") || line.Contains("╔") || line.Contains("╚"))
            return Border_Default;

        if (line.TrimStart().StartsWith("Summary:") ||
            line.TrimStart().StartsWith("Parameters:") ||
            line.TrimStart().StartsWith("Returns:"))
            return Documentation_Label;

        if (line.Contains("(method)") || line.Contains("(property)") || line.Contains("(field)"))
            return Documentation_TypeInfo;

        return Text_Primary;
    }

    private void DrawCloseInstruction(int popupX, int popupY, int popupWidth, int popupHeight, int lineHeight)
    {
        var instructionText = "Press Escape or F1 to close";
        var instructionSize = _fontRenderer.MeasureString(instructionText);
        int instructionX = popupX + (popupWidth - (int)instructionSize.X) / 2;
        int instructionY = popupY + popupHeight - lineHeight - 5;
        _fontRenderer.DrawString(instructionText, instructionX, instructionY, Documentation_Instruction);
    }

    private void DrawRectangle(int x, int y, int width, int height, Color color)
    {
        _spriteBatch.Draw(_pixel, new Rectangle(x, y, width, height), color);
    }
}