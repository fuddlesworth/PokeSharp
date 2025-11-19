using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using PokeSharp.Engine.Debug.Console.Configuration;
using System;
using static PokeSharp.Engine.Debug.Console.Configuration.ConsoleColors;

namespace PokeSharp.Engine.Debug.Console.UI.Renderers;

/// <summary>
/// Handles output area rendering for the Quake console including section folding and scroll indicators.
/// Extracted from QuakeConsole to follow Single Responsibility Principle.
/// </summary>
public class ConsoleOutputRenderer
{
    private readonly SpriteBatch _spriteBatch;
    private readonly Texture2D _pixel;
    private float _screenWidth;
    private float _consoleHeight;

    public ConsoleOutputRenderer(
        SpriteBatch spriteBatch,
        Texture2D pixel,
        float screenWidth,
        float consoleHeight)
    {
        _spriteBatch = spriteBatch;
        _pixel = pixel;
        _screenWidth = screenWidth;
        _consoleHeight = consoleHeight;
    }

    /// <summary>
    /// Updates the dimensions when window or console size is resized.
    /// </summary>
    public void UpdateSize(float screenWidth, float consoleHeight)
    {
        _screenWidth = screenWidth;
        _consoleHeight = consoleHeight;
    }

    /// <summary>
    /// Draws fold/unfold boxes for section headers on the right side.
    /// </summary>
    public void DrawSectionFoldBoxes(int outputY, int lineHeight, ConsoleOutput output, int hoverLineIndex = -1)
    {
        var visibleHeaders = output.GetVisibleSectionHeaders();

        const int boxSize = ConsoleConstants.Rendering.SectionFoldBoxSize;
        const int scrollBarWidth = ConsoleConstants.Rendering.ScrollBarWidth;
        const int scrollBarMargin = ConsoleConstants.Rendering.ScrollBarMargin;
        const int boxSpacing = ConsoleConstants.Rendering.SectionFoldBoxSpacing;

        foreach (var (section, visualLineIndex) in visibleHeaders)
        {
            int lineY = outputY + (visualLineIndex * lineHeight);
            int centerY = lineY + (lineHeight / 2);

            // Position box to the left of scrollbar when scrollbar is visible
            bool hasScrollbar = output.GetEffectiveLineCount() > output.VisibleLines;
            int boxX = hasScrollbar
                ? (int)_screenWidth - scrollBarMargin - scrollBarWidth - boxSpacing - boxSize
                : (int)_screenWidth - ConsoleConstants.Rendering.SectionFoldBoxMargin - boxSize;
            int boxY = centerY - boxSize / 2;

            // Check if this section is hovered
            bool isHovered = (visualLineIndex == hoverLineIndex);

            // Draw box border (use hover color if hovered)
            Color borderColor = isHovered ? SectionFold_HoverOutline : section.HeaderColor;
            DrawRectangle(boxX, boxY, boxSize, boxSize, borderColor);

            // Draw background inside (use hover background if hovered)
            Color backgroundColor = isHovered ? SectionFold_Hover : SectionFold_Background;
            DrawRectangle(boxX + 1, boxY + 1, boxSize - 2, boxSize - 2, backgroundColor);

            // Draw symbol (+ for collapsed, - for expanded) (use hover color if hovered)
            Color symbolColor = isHovered ? SectionFold_HoverOutline : section.HeaderColor;
            int centerX = boxX + boxSize / 2;
            int centerBoxY = boxY + boxSize / 2;

            // Horizontal line (for both + and -)
            DrawRectangle(boxX + 2, centerBoxY, boxSize - 4, 1, symbolColor);

            // Vertical line (only for +)
            if (section.IsFolded)
            {
                DrawRectangle(centerX, boxY + 2, 1, boxSize - 4, symbolColor);
            }
        }
    }

    /// <summary>
    /// Draws a scroll indicator showing current position.
    /// </summary>
    public void DrawScrollIndicator(int yPos, int lineHeight, ConsoleOutput output)
    {
        const int scrollBarWidth = ConsoleConstants.Rendering.ScrollBarWidth;
        int scrollBarX = (int)_screenWidth - ConsoleConstants.Rendering.ScrollBarMargin;
        int scrollBarY = yPos + ConsoleConstants.Rendering.ScrollBarVerticalPadding;
        int scrollBarHeight = (int)_consoleHeight - ConsoleConstants.Rendering.ScrollBarBottomOffset;

        // Draw scrollbar background
        DrawRectangle(scrollBarX, scrollBarY, scrollBarWidth, scrollBarHeight, Scroll_Track);

        // Calculate thumb position and size (using filtered line count)
        int effectiveLineCount = output.GetEffectiveLineCount();
        float contentRatio = Math.Min(1.0f, (float)output.VisibleLines / effectiveLineCount);
        int thumbHeight = Math.Max(20, (int)(scrollBarHeight * contentRatio));

        float scrollProgress = effectiveLineCount <= output.VisibleLines
            ? 0
            : (float)output.ScrollOffset / (effectiveLineCount - output.VisibleLines);

        int thumbY = scrollBarY + (int)((scrollBarHeight - thumbHeight) * scrollProgress);

        // Draw thumb
        Color thumbColor = output.IsAtTop || output.IsAtBottom
            ? Scroll_Thumb_Active
            : Scroll_Thumb;

        DrawRectangle(scrollBarX, thumbY, scrollBarWidth, thumbHeight, thumbColor);

        // Draw triangle indicators for more content (centered on scrollbar)
        const int triangleWidth = ConsoleConstants.Rendering.ScrollTriangleWidth;
        const int triangleHeight = ConsoleConstants.Rendering.ScrollTriangleHeight;
        const int triangleGap = ConsoleConstants.Rendering.ScrollTriangleGap;
        int triangleCenterX = scrollBarX + (scrollBarWidth / 2);
        var indicatorColor = Scroll_Indicator;

        if (!output.IsAtTop)
        {
            // Draw up triangle above the scrollbar
            int upTriangleY = scrollBarY - triangleHeight - triangleGap;
            DrawUpTriangle(triangleCenterX, upTriangleY, triangleWidth, triangleHeight, indicatorColor);
        }

        if (!output.IsAtBottom)
        {
            // Draw down triangle below the scrollbar
            int downTriangleY = scrollBarY + scrollBarHeight + triangleGap;
            DrawDownTriangle(triangleCenterX, downTriangleY, triangleWidth, triangleHeight, indicatorColor);
        }
    }

    /// <summary>
    /// Draws an upward-pointing triangle (for scroll indicators).
    /// </summary>
    private void DrawUpTriangle(int centerX, int topY, int width, int height, Color color)
    {
        // Draw triangle row by row from top to bottom
        for (int row = 0; row < height; row++)
        {
            float progress = (float)row / height;
            int rowWidth = (int)(width * progress);
            int x = centerX - rowWidth / 2;
            DrawRectangle(x, topY + row, Math.Max(1, rowWidth), 1, color);
        }
    }

    /// <summary>
    /// Draws a downward-pointing triangle (for scroll indicators).
    /// </summary>
    private void DrawDownTriangle(int centerX, int topY, int width, int height, Color color)
    {
        // Draw triangle row by row from top (wide) to bottom (point)
        for (int row = 0; row < height; row++)
        {
            float progress = 1.0f - ((float)row / height);
            int rowWidth = (int)(width * progress);
            int x = centerX - rowWidth / 2;
            DrawRectangle(x, topY + row, Math.Max(1, rowWidth), 1, color);
        }
    }

    /// <summary>
    /// Draws a right-pointing triangle (for collapsed sections).
    /// </summary>
    public void DrawRightTriangle(int leftX, int centerY, int width, int height, Color color)
    {
        // Draw triangle column by column from left (point) to right (wide)
        for (int col = 0; col < width; col++)
        {
            float progress = (float)col / width;
            int colHeight = (int)(height * progress);
            int y = centerY - colHeight / 2;
            DrawRectangle(leftX + col, y, 1, Math.Max(1, colHeight), color);
        }
    }

    /// <summary>
    /// Draws a filled rectangle.
    /// </summary>
    private void DrawRectangle(int x, int y, int width, int height, Color color)
    {
        _spriteBatch.Draw(_pixel, new Rectangle(x, y, width, height), color);
    }
}

