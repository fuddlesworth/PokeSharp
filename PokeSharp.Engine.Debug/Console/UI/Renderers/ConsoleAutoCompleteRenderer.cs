using Microsoft.CodeAnalysis.Completion;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;

namespace PokeSharp.Engine.Debug.Console.UI.Renderers;

/// <summary>
/// Handles rendering of auto-complete suggestions panel.
/// Separated from QuakeConsole to follow Single Responsibility Principle.
/// </summary>
public class ConsoleAutoCompleteRenderer
{
    private readonly ConsoleFontRenderer _fontRenderer;
    private readonly SpriteBatch _spriteBatch;
    private readonly Texture2D _pixel;

    public ConsoleAutoCompleteRenderer(
        ConsoleFontRenderer fontRenderer,
        SpriteBatch spriteBatch,
        Texture2D pixel
    )
    {
        _fontRenderer = fontRenderer;
        _spriteBatch = spriteBatch;
        _pixel = pixel;
    }

    /// <summary>
    /// Draws loading indicator for auto-complete.
    /// </summary>
    public void DrawLoading(int bottomY, int lineHeight, float animationValue)
    {
        int panelHeight = lineHeight + Rendering.Padding * 2;
        int y = bottomY - panelHeight;

        // Draw background
        DrawRectangle(
            Rendering.Padding + Rendering.SuggestionPanelMargin,
            y,
            Rendering.LoadingPanelWidth,
            panelHeight,
            Background_Elevated
        );

        // Draw loading text with animated dots
        var loadingText = "Loading suggestions";
        var dotCount =
            (int)(animationValue / ConsoleConstants.Animation.LoadingDotsAnimationDivisor)
            % ConsoleConstants.Animation.LoadingDotsMaxCount;
        var dots = new string('.', dotCount);
        _fontRenderer.DrawString(
            loadingText + dots,
            Rendering.Padding + Rendering.SuggestionVerticalPadding,
            y + Rendering.Padding,
            AutoComplete_Loading
        );
    }

    /// <summary>
    /// Draws auto-complete suggestions above the input area with scrolling support.
    /// </summary>
    public int DrawSuggestions(
        int bottomY,
        int lineHeight,
        List<CompletionItem> filteredSuggestions,
        int selectedIndex,
        int scrollOffset,
        int horizontalScroll,
        int hoverIndex,
        out int calculatedVisibleCount
    )
    {
        calculatedVisibleCount = 0;

        // Boundary check
        if (filteredSuggestions.Count == 0 || lineHeight <= 0)
            return -1;

        // Calculate maximum available space
        int maxAvailableHeight = bottomY - Rendering.Padding * 2;
        int maxVisibleSuggestions = Math.Max(
            1,
            (maxAvailableHeight - Rendering.Padding * 2) / lineHeight
        );
        maxVisibleSuggestions = Math.Min(
            maxVisibleSuggestions,
            ConsoleConstants.Limits.MaxAutoCompleteSuggestions
        );

        int visibleCount = Math.Min(maxVisibleSuggestions, filteredSuggestions.Count);
        calculatedVisibleCount = visibleCount;

        // Add extra space for scroll indicators at top and bottom (symmetric)
        int scrollIndicatorSpace = lineHeight; // One line height for each indicator
        int contentHeight = visibleCount * lineHeight;
        int panelHeight = contentHeight + scrollIndicatorSpace * 2;
        int y = bottomY - panelHeight;

        // Ensure scroll offset is valid
        int maxScroll = Math.Max(0, filteredSuggestions.Count - visibleCount);
        scrollOffset = Math.Clamp(scrollOffset, 0, maxScroll);

        // Boundary check for dimensions
        if (panelHeight <= 0 || Rendering.SuggestionPanelWidth <= 0)
            return -1;

        // Draw background
        DrawRectangle(
            Rendering.Padding + Rendering.SuggestionPanelMargin,
            y,
            Rendering.SuggestionPanelWidth,
            panelHeight,
            Background_Elevated
        );

        // Calculate content area (offset by top scroll indicator space - symmetric with bottom)
        int contentStartY = y + scrollIndicatorSpace;

        // Draw visible suggestions in the content area
        for (int i = 0; i < visibleCount; i++)
        {
            int itemIndex = i + scrollOffset;
            if (itemIndex >= filteredSuggestions.Count)
                break;

            var item = filteredSuggestions[itemIndex];
            bool isHovered = (i == hoverIndex);
            DrawSuggestionItem(
                item,
                i,
                itemIndex == selectedIndex,
                isHovered,
                contentStartY,
                lineHeight,
                horizontalScroll
            );
        }

        // Draw scroll indicators in their dedicated space
        DrawScrollIndicators(
            y,
            panelHeight,
            visibleCount,
            lineHeight,
            filteredSuggestions.Count,
            scrollOffset,
            scrollIndicatorSpace
        );

        return scrollOffset; // Return adjusted scroll offset
    }

    private void DrawSuggestionItem(
        CompletionItem item,
        int displayIndex,
        bool isSelected,
        bool isHovered,
        int contentStartY,
        int lineHeight,
        int horizontalScroll
    )
    {
        // Priority: Selected > Hovered > Unselected
        var color =
            isSelected ? AutoComplete_Selected
            : isHovered ? AutoComplete_Hover
            : AutoComplete_Unselected;

        // Build display text
        var displayText = item.DisplayText;
        if (!string.IsNullOrEmpty(item.DisplayTextSuffix))
            displayText += item.DisplayTextSuffix;

        if (string.IsNullOrWhiteSpace(displayText))
            return;

        // Strip newlines
        displayText = displayText.Replace("\n", " ↵ ");

        // contentStartY is already offset for the top indicator space, just add item offset
        int textY = contentStartY + (displayIndex * lineHeight);
        int textX = Rendering.Padding + Rendering.SuggestionVerticalPadding;

        // Calculate max text width
        int maxTextWidth = CalculateMaxTextWidth(item);

        // Check if text needs truncation
        bool needsTruncation = _fontRenderer.MeasureString(displayText).X > maxTextWidth;

        // Apply horizontal scrolling for selected item, but only if text needs truncation
        string renderedText = ApplyHorizontalScroll(
            displayText,
            isSelected && needsTruncation,
            horizontalScroll
        );

        // Truncate if needed (only mark as scrolled if this item is selected and needs truncation)
        string finalText = TruncateText(
            renderedText,
            maxTextWidth,
            isSelected && needsTruncation && horizontalScroll > 0
        );

        _fontRenderer.DrawString(finalText, textX, textY, color);

        // Draw inline description if available
        DrawInlineDescription(item, textY);
    }

    private int CalculateMaxTextWidth(CompletionItem item)
    {
        int maxTextWidth;
        if (!string.IsNullOrEmpty(item.InlineDescription))
        {
            var descriptionText = item.InlineDescription.Replace("\n", " ↵ ");
            var descSize = _fontRenderer.MeasureString(descriptionText);

            maxTextWidth =
                Rendering.SuggestionPanelWidth
                - Rendering.SuggestionVerticalPadding
                - (int)descSize.X
                - Rendering.SuggestionDescriptionGap
                - Rendering.SuggestionRightPadding;
        }
        else
        {
            maxTextWidth =
                Rendering.SuggestionPanelWidth - Rendering.SuggestionVerticalPadding * 2 - 20;
        }

        return Math.Max(maxTextWidth, Rendering.SuggestionMinTextWidth);
    }

    private string ApplyHorizontalScroll(string text, bool isSelected, int horizontalScroll)
    {
        if (!isSelected || horizontalScroll <= 0)
            return text;

        // Apply scroll up to the actual text length
        int scrollChars = Math.Min(horizontalScroll, text.Length);
        if (scrollChars > 0 && text.Length > scrollChars)
            return text.Substring(scrollChars);

        return text;
    }

    private string TruncateText(string text, int maxWidth, bool isScrolled)
    {
        var textSize = _fontRenderer.MeasureString(text);

        if (textSize.X <= maxWidth)
        {
            return isScrolled ? "... " + text : text;
        }

        // Truncate character by character
        string ellipsis = isScrolled ? "... ... " : " ...";
        float ellipsisWidth = _fontRenderer.MeasureString(ellipsis).X;
        float availableWidth = maxWidth - ellipsisWidth;

        int charCount = text.Length;
        while (charCount > 1)
        {
            string truncated = text.Substring(0, charCount);
            float width = _fontRenderer.MeasureString(truncated).X;
            if (width <= availableWidth)
            {
                return (isScrolled ? "... " : "") + truncated + " ...";
            }
            charCount--;
        }

        return (isScrolled ? "... " : "") + text.Substring(0, 1) + " ...";
    }

    private void DrawInlineDescription(CompletionItem item, int textY)
    {
        if (string.IsNullOrEmpty(item.InlineDescription))
            return;

        var description = item.InlineDescription.Replace("\n", " ↵ ");
        var descSize = _fontRenderer.MeasureString(description);

        int panelRightEdge =
            Rendering.Padding + Rendering.SuggestionPanelMargin + Rendering.SuggestionPanelWidth;
        int descX = panelRightEdge - (int)descSize.X - Rendering.SuggestionRightPadding;

        _fontRenderer.DrawString(description, descX, textY, AutoComplete_Description);
    }

    private void DrawScrollIndicators(
        int panelY,
        int panelHeight,
        int visibleCount,
        int lineHeight,
        int totalCount,
        int scrollOffset,
        int scrollIndicatorSpace
    )
    {
        int panelRightEdge =
            Rendering.Padding + Rendering.SuggestionPanelMargin + Rendering.SuggestionPanelWidth;

        // Up indicator - in dedicated top space
        if (scrollOffset > 0)
        {
            int hiddenAbove = scrollOffset;
            string upIndicator = $"^ {hiddenAbove} more";
            var textSize = _fontRenderer.MeasureString(upIndicator);
            int indicatorX =
                panelRightEdge - (int)textSize.X - Rendering.SuggestionScrollIndicatorRightMargin;
            int indicatorY = panelY + (scrollIndicatorSpace / 2) - (lineHeight / 2); // Centered in top space
            _fontRenderer.DrawString(upIndicator, indicatorX, indicatorY, Info_Dim);
        }

        // Down indicator - in dedicated bottom space
        int hiddenBelow = totalCount - (scrollOffset + visibleCount);
        if (hiddenBelow > 0)
        {
            string downIndicator = $"v {hiddenBelow} more";
            var textSize = _fontRenderer.MeasureString(downIndicator);
            int indicatorX =
                panelRightEdge - (int)textSize.X - Rendering.SuggestionScrollIndicatorRightMargin;
            int indicatorY =
                panelY
                + panelHeight
                - scrollIndicatorSpace
                + (scrollIndicatorSpace / 2)
                - (lineHeight / 2); // Centered in bottom space
            _fontRenderer.DrawString(downIndicator, indicatorX, indicatorY, Info_Dim);
        }
    }

    private void DrawRectangle(int x, int y, int width, int height, Color color)
    {
        _spriteBatch.Draw(_pixel, new Rectangle(x, y, width, height), color);
    }
}
