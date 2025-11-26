using Microsoft.Xna.Framework;
using PokeSharp.Engine.UI.Debug.Components.Base;
using PokeSharp.Engine.UI.Debug.Core;
using PokeSharp.Engine.UI.Debug.Input;
using PokeSharp.Engine.UI.Debug.Layout;
using System.Text;

namespace PokeSharp.Engine.UI.Debug.Components.Controls;

/// <summary>
/// Represents a line of text with metadata.
/// </summary>
public record TextBufferLine(string Text, Color Color, string Category = "General", object? Tag = null);

/// <summary>
/// Multi-line scrollable text buffer with colored lines, filtering, and search.
/// Perfect for console output, logs, or any multi-line text display.
/// </summary>
public class TextBuffer : UIComponent, ITextDisplay
{
    private readonly List<TextBufferLine> _lines = new();
    private readonly List<TextBufferLine> _filteredLines = new();
    private int _scrollOffset = 0;
    private int _maxLines = 10000;
    private bool _autoScroll = true;

    // Filtering
    private HashSet<string> _enabledCategories = new();
    private string? _searchFilter = null;
    private bool _isDirty = true;

    // Search
    private readonly List<int> _searchMatches = new(); // Line indices that match search
    private int _currentSearchMatchIndex = -1; // Current match being viewed
    private Color _searchHighlightColor = UITheme.Dark.ReverseSearchMatchHighlight; // Yellow highlight

    // Selection
    private bool _hasSelection = false;
    private int _selectionStartLine = 0;
    private int _selectionEndLine = 0;

    // Scrollbar tracking
    private bool _isDraggingScrollbar = false;
    private int _scrollbarDragStartY = 0;
    private int _scrollbarDragStartOffset = 0;
    private Point _lastMousePosition = Point.Zero;

    // Visual properties
    public Color BackgroundColor { get; set; } = UITheme.Dark.BackgroundPrimary;
    public Color ScrollbarTrackColor { get; set; } = UITheme.Dark.ScrollbarTrack;
    public Color ScrollbarThumbColor { get; set; } = UITheme.Dark.ScrollbarThumb;
    public Color ScrollbarThumbHoverColor { get; set; } = UITheme.Dark.ScrollbarThumbHover;
    public Color SelectionColor { get; set; } = UITheme.Dark.InputSelection;
    public int ScrollbarWidth { get; set; } = 10;
    public int ScrollbarPadding { get; set; } = 4; // Gap between content and scrollbar
    public int LineHeight { get; set; } = 20;
    public int LinePadding { get; set; } = 5;

    // Properties
    public bool AutoScroll
    {
        get => _autoScroll;
        set => _autoScroll = value;
    }

    public int MaxLines
    {
        get => _maxLines;
        set => _maxLines = Math.Max(100, value);
    }

    public int TotalLines => _lines.Count;
    public int FilteredLineCount => _isDirty ? _lines.Count : _filteredLines.Count;
    public bool HasSelection => _hasSelection;
    public string SelectedText => GetSelectedText();

    /// <summary>
    /// Gets the current scroll offset (number of lines scrolled from top).
    /// </summary>
    public int ScrollOffset => _scrollOffset;

    /// <summary>
    /// Sets the scroll offset directly. Useful for preserving scroll position during updates.
    /// </summary>
    public void SetScrollOffset(int offset)
    {
        _scrollOffset = Math.Max(0, offset);
        _autoScroll = false; // Disable auto-scroll when manually setting position
    }

    public TextBuffer(string id) { Id = id; }

    /// <summary>
    /// Appends a line with default color and category (ITextDisplay interface).
    /// </summary>
    public void AppendLine(string text)
    {
        AppendLine(text, UITheme.Dark.TextPrimary, "General");
    }

    /// <summary>
    /// Appends a line with specified color and default category (ITextDisplay interface).
    /// </summary>
    public void AppendLine(string text, Color color)
    {
        AppendLine(text, color, "General");
    }

    /// <summary>
    /// Appends a line to the buffer with full control over color and category.
    /// </summary>
    public void AppendLine(string text, Color color, string category)
    {
        // Split multi-line text
        var lines = text.Split('\n', StringSplitOptions.None);
        foreach (var line in lines)
        {
            _lines.Add(new TextBufferLine(line, color, category));
        }

        // Enforce max lines
        while (_lines.Count > _maxLines)
        {
            _lines.RemoveAt(0);

            // Adjust scroll offset
            if (_scrollOffset > 0)
                _scrollOffset--;
        }

        _isDirty = true;

        // Auto-scroll to bottom
        if (_autoScroll)
        {
            ScrollToBottom();
        }
    }

    /// <summary>
    /// Clears all lines from the buffer.
    /// </summary>
    public void Clear()
    {
        _lines.Clear();
        _filteredLines.Clear();
        _scrollOffset = 0;
        _isDirty = true;
        ClearSelection();
    }

    /// <summary>
    /// Scrolls to the bottom of the buffer.
    /// Safe to call even when not in render context (will defer to next render).
    /// </summary>
    public void ScrollToBottom()
    {
        // If we can get visible count (in render context), scroll immediately
        // Otherwise, just set to max possible - will be clamped during render
        try
        {
            _scrollOffset = Math.Max(0, FilteredLineCount - GetVisibleLineCount());
        }
        catch
        {
            // Not in render context - set to high value to ensure we're at bottom
            // Will be properly clamped during next render
            _scrollOffset = int.MaxValue;
        }
    }

    /// <summary>
    /// Scrolls to the top of the buffer.
    /// </summary>
    public void ScrollToTop()
    {
        _scrollOffset = 0;
    }

    /// <summary>
    /// Scrolls up by the specified number of lines.
    /// </summary>
    public void ScrollUp(int lines = 1)
    {
        _scrollOffset = Math.Max(0, _scrollOffset - lines);
        _autoScroll = false; // Disable auto-scroll when manually scrolling
    }

    /// <summary>
    /// Scrolls down by the specified number of lines.
    /// </summary>
    public void ScrollDown(int lines = 1)
    {
        var maxScroll = Math.Max(0, FilteredLineCount - GetVisibleLineCount());
        _scrollOffset = Math.Min(maxScroll, _scrollOffset + lines);

        // Re-enable auto-scroll if at bottom
        if (_scrollOffset >= maxScroll)
        {
            _autoScroll = true;
        }
    }

    /// <summary>
    /// Sets the search filter.
    /// </summary>
    public void SetSearchFilter(string? filter)
    {
        if (_searchFilter != filter)
        {
            _searchFilter = filter;
            RebuildSearchMatches();
        }
    }

    /// <summary>
    /// Performs a search and returns the number of matches found.
    /// </summary>
    public int Search(string searchText)
    {
        // Treat empty string as null (no search)
        _searchFilter = string.IsNullOrWhiteSpace(searchText) ? null : searchText;
        RebuildSearchMatches();

        // Jump to first match if any found
        if (_searchMatches.Count > 0)
        {
            _currentSearchMatchIndex = 0;
            ScrollToMatch(_currentSearchMatchIndex);
        }

        return _searchMatches.Count;
    }

    /// <summary>
    /// Clears the search and match highlighting.
    /// </summary>
    public void ClearSearch()
    {
        _searchFilter = null;
        _searchMatches.Clear();
        _currentSearchMatchIndex = -1;
    }

    /// <summary>
    /// Navigates to the next search match.
    /// </summary>
    public void FindNext()
    {
        if (_searchMatches.Count == 0) return;

        _currentSearchMatchIndex = (_currentSearchMatchIndex + 1) % _searchMatches.Count;
        ScrollToMatch(_currentSearchMatchIndex);
    }

    /// <summary>
    /// Navigates to the previous search match.
    /// </summary>
    public void FindPrevious()
    {
        if (_searchMatches.Count == 0) return;

        _currentSearchMatchIndex--;
        if (_currentSearchMatchIndex < 0)
            _currentSearchMatchIndex = _searchMatches.Count - 1;

        ScrollToMatch(_currentSearchMatchIndex);
    }

    /// <summary>
    /// Gets the total number of search matches.
    /// </summary>
    public int GetSearchMatchCount() => _searchMatches.Count;

    /// <summary>
    /// Gets the current match index (1-based for display).
    /// </summary>
    public int GetCurrentSearchMatchIndex() => _currentSearchMatchIndex >= 0 ? _currentSearchMatchIndex + 1 : 0;

    /// <summary>
    /// Enables or disables a category filter.
    /// </summary>
    public void SetCategoryFilter(string category, bool enabled)
    {
        if (enabled)
            _enabledCategories.Add(category);
        else
            _enabledCategories.Remove(category);

        _isDirty = true;
    }

    /// <summary>
    /// Clears all category filters (shows all categories).
    /// </summary>
    public void ClearCategoryFilters()
    {
        _enabledCategories.Clear();
        _isDirty = true;
    }

    /// <summary>
    /// Selects lines in the specified range.
    /// </summary>
    public void SelectLines(int startLine, int endLine)
    {
        _hasSelection = true;
        _selectionStartLine = Math.Min(startLine, endLine);
        _selectionEndLine = Math.Max(startLine, endLine);
    }

    /// <summary>
    /// Clears the current selection.
    /// </summary>
    public void ClearSelection()
    {
        _hasSelection = false;
        _selectionStartLine = 0;
        _selectionEndLine = 0;
    }

    /// <summary>
    /// Gets the selected text as a string.
    /// </summary>
    private string GetSelectedText()
    {
        if (!_hasSelection)
            return string.Empty;

        var lines = GetFilteredLines();
        var sb = new StringBuilder();

        for (int i = _selectionStartLine; i <= _selectionEndLine && i < lines.Count; i++)
        {
            sb.AppendLine(lines[i].Text);
        }

        return sb.ToString();
    }

    /// <summary>
    /// Gets the filtered lines based on current filters.
    /// Note: Search filter does NOT filter lines, it only highlights matches.
    /// </summary>
    private List<TextBufferLine> GetFilteredLines()
    {
        if (_isDirty)
        {
            _filteredLines.Clear();

            foreach (var line in _lines)
            {
                // Category filter (only filter that actually hides lines)
                if (_enabledCategories.Count > 0 && !_enabledCategories.Contains(line.Category))
                    continue;

                // Note: Search filter removed - we highlight matches, not filter lines
                _filteredLines.Add(line);
            }

            _isDirty = false;
        }

        return _filteredLines;
    }

    /// <summary>
    /// Gets the number of visible lines based on the component's height.
    /// </summary>
    private int GetVisibleLineCount()
    {
        // Use Rect.Height (resolved layout) not Constraint.Height (input constraint)
        float height = Rect.Height > 0 ? Rect.Height : 400; // Fallback to 400 if not resolved yet

        // Try to use the font's actual line height, but don't throw if no context
        int fontLineHeight = LineHeight;
        try
        {
            if (Renderer != null)
            {
                fontLineHeight = Renderer.GetLineHeight();
            }
        }
        catch
        {
            // No context available - use default LineHeight
        }

        var effectiveLineHeight = Math.Max(fontLineHeight, LineHeight);

        return Math.Max(1, (int)((height - LinePadding * 2) / effectiveLineHeight));
    }

    /// <summary>
    /// Rebuilds the list of lines that match the current search filter.
    /// </summary>
    private void RebuildSearchMatches()
    {
        _searchMatches.Clear();
        _currentSearchMatchIndex = -1;

        if (string.IsNullOrEmpty(_searchFilter))
            return;

        var lines = GetFilteredLines();
        for (int i = 0; i < lines.Count; i++)
        {
            if (lines[i].Text.Contains(_searchFilter, StringComparison.OrdinalIgnoreCase))
            {
                _searchMatches.Add(i);
            }
        }
    }

    /// <summary>
    /// Scrolls to show the specified match index.
    /// </summary>
    private void ScrollToMatch(int matchIndex)
    {
        if (matchIndex < 0 || matchIndex >= _searchMatches.Count)
            return;

        var lineIndex = _searchMatches[matchIndex];
        var visibleLines = GetVisibleLineCount();
        var totalLines = GetFilteredLines().Count;

        // Center the match in the viewport
        // Ensure max value for Clamp is never negative
        var maxScroll = Math.Max(0, totalLines - visibleLines);
        _scrollOffset = Math.Clamp(lineIndex - visibleLines / 2, 0, maxScroll);
        _autoScroll = false; // Disable auto-scroll when manually scrolling to search results
    }

    protected override void OnRender(UIContext context)
    {
        var renderer = Renderer;
        var resolvedRect = Rect;
        var input = context?.Input;

        var lines = GetFilteredLines();
        var visibleCount = GetVisibleLineCount();

        // Check if scrollbar is needed
        var hasScrollbar = lines.Count > visibleCount;

        // Calculate content area (excluding scrollbar and padding if scrollbar is visible)
        var contentWidth = hasScrollbar ? resolvedRect.Width - ScrollbarWidth - ScrollbarPadding : resolvedRect.Width;
        var contentRect = new LayoutRect(
            resolvedRect.X,
            resolvedRect.Y,
            contentWidth,
            resolvedRect.Height
        );

        // Draw background only for content area
        renderer.DrawRectangle(contentRect, BackgroundColor);

        // If scrollbar is visible, draw a slightly darker background for the scrollbar area
        if (hasScrollbar)
        {
            var scrollbarBgRect = new LayoutRect(
                contentRect.Right,
                resolvedRect.Y,
                ScrollbarWidth + ScrollbarPadding,
                resolvedRect.Height
            );
            // Use a slightly darker shade for the scrollbar background
            var scrollbarBgColor = new Color(
                (int)(BackgroundColor.R * 0.9f),
                (int)(BackgroundColor.G * 0.9f),
                (int)(BackgroundColor.B * 0.9f),
                BackgroundColor.A
            );
            renderer.DrawRectangle(scrollbarBgRect, scrollbarBgColor);
        }

        // Clamp scroll offset now that we have proper context
        var maxScroll = Math.Max(0, lines.Count - visibleCount);
        if (_scrollOffset > maxScroll)
        {
            _scrollOffset = maxScroll;
        }

        var startIndex = Math.Max(0, Math.Min(_scrollOffset, lines.Count - visibleCount));
        var endIndex = Math.Min(lines.Count, startIndex + visibleCount);

        // Track mouse position for scrollbar hover detection
        if (input != null)
        {
            _lastMousePosition = input.MousePosition;
        }

        // Handle input for scrolling (mouse wheel and keyboard)
        if (input != null && resolvedRect.Contains(input.MousePosition))
        {
            // Handle scroll wheel
            if (input.ScrollWheelDelta != 0)
            {
                if (input.ScrollWheelDelta > 0)
                    ScrollUp(3);
                else
                    ScrollDown(3);
            }

            // Handle keyboard scrolling with key repeat
            if (input.IsKeyPressedWithRepeat(Microsoft.Xna.Framework.Input.Keys.PageUp))
            {
                ScrollUp(visibleCount);
            }
            else if (input.IsKeyPressedWithRepeat(Microsoft.Xna.Framework.Input.Keys.PageDown))
            {
                ScrollDown(visibleCount);
            }
            else if (input.IsKeyPressedWithRepeat(Microsoft.Xna.Framework.Input.Keys.Home))
            {
                ScrollToTop();
            }
            else if (input.IsKeyPressedWithRepeat(Microsoft.Xna.Framework.Input.Keys.End))
            {
                ScrollToBottom();
            }
        }

        // Handle scrollbar interaction - check BEFORE other input to get priority
        if (input != null && hasScrollbar)
        {
            // Apply padding to scrollbar to match content area
            var scrollbarY = resolvedRect.Y + LinePadding;
            var scrollbarHeight = resolvedRect.Height - (LinePadding * 2);

            var scrollbarRect = new LayoutRect(
                resolvedRect.Right - ScrollbarWidth,
                scrollbarY,
                ScrollbarWidth,
                scrollbarHeight
            );

            // Calculate thumb position and size
            float thumbHeight = Math.Max(20, (float)visibleCount / lines.Count * scrollbarHeight);
            float thumbY = scrollbarY + ((float)_scrollOffset / (lines.Count - visibleCount)) * (scrollbarHeight - thumbHeight);

            var thumbRect = new LayoutRect(
                resolvedRect.Right - ScrollbarWidth,
                thumbY,
                ScrollbarWidth,
                thumbHeight
            );

            bool isOverScrollbar = scrollbarRect.Contains(input.MousePosition);

            // Handle dragging (continuous while holding)
            if (_isDraggingScrollbar)
            {
                if (input.IsMouseButtonDown(MouseButton.Left))
                {
                    int deltaY = input.MousePosition.Y - _scrollbarDragStartY;
                    float scrollRatio = (float)deltaY / scrollbarHeight;
                    int scrollDelta = (int)(scrollRatio * lines.Count);

                    _scrollOffset = Math.Clamp(_scrollbarDragStartOffset + scrollDelta, 0, maxScroll);
                    _autoScroll = false;
                }
                else
                {
                    _isDraggingScrollbar = false;
                }
            }
            // Handle new click on scrollbar
            else if (isOverScrollbar && input.IsMouseButtonPressed(MouseButton.Left))
            {
                // Check if clicking on thumb (drag) or track (jump)
                if (thumbRect.Contains(input.MousePosition))
                {
                    // Start dragging the thumb
                    _isDraggingScrollbar = true;
                    _scrollbarDragStartY = input.MousePosition.Y;
                    _scrollbarDragStartOffset = _scrollOffset;
                }
                else
                {
                    // Click on track - jump to that position immediately
                    float clickRatio = (float)(input.MousePosition.Y - scrollbarY) / scrollbarHeight;
                    int targetScroll = (int)(clickRatio * lines.Count) - (visibleCount / 2);
                    _scrollOffset = Math.Clamp(targetScroll, 0, maxScroll);
                    _autoScroll = false;

                    // Also start dragging from this new position in case they want to continue dragging
                    _isDraggingScrollbar = true;
                    _scrollbarDragStartY = input.MousePosition.Y;
                    _scrollbarDragStartOffset = _scrollOffset;
                }
            }
        }

        // Push clipping rectangle to prevent text from rendering under scrollbar
        renderer.PushClip(contentRect);

        // Draw visible lines
        float y = resolvedRect.Y + LinePadding;

        for (int i = startIndex; i < endIndex; i++)
        {
            var line = lines[i];

            // Draw selection background
            if (_hasSelection && i >= _selectionStartLine && i <= _selectionEndLine)
            {
                var selectionRect = new LayoutRect(
                    resolvedRect.X,
                    y,
                    contentWidth,
                    LineHeight
                );
                renderer.DrawRectangle(selectionRect, SelectionColor);
            }

            // Draw search match highlights (only highlight the matching words, not entire line)
            if (!string.IsNullOrEmpty(_searchFilter))
            {
                var matchIndex = _searchMatches.IndexOf(i);
                if (matchIndex >= 0)
                {
                    // This line contains a search match - highlight each occurrence
                    var isCurrentMatch = matchIndex == _currentSearchMatchIndex;
                    var highlightColor = isCurrentMatch
                        ? new Color(255, 200, 0, 150) // Bright yellow for current match
                        : _searchHighlightColor; // Dimmer yellow for other matches

                    // Find all occurrences of the search term in this line
                    var searchIndex = 0;
                    while (searchIndex < line.Text.Length)
                    {
                        var foundIndex = line.Text.IndexOf(_searchFilter, searchIndex, StringComparison.OrdinalIgnoreCase);
                        if (foundIndex == -1)
                            break;

                        // Measure text before the match to get X position
                        var textBefore = line.Text.Substring(0, foundIndex);
                        var offsetX = renderer.MeasureText(textBefore).X;

                        // Measure the matched text to get width
                        var matchedText = line.Text.Substring(foundIndex, _searchFilter.Length);
                        var matchWidth = renderer.MeasureText(matchedText).X;

                        // Draw highlight rectangle for this specific match
                        var highlightRect = new LayoutRect(
                            resolvedRect.X + LinePadding + offsetX,
                            y,
                            matchWidth,
                            LineHeight
                        );
                        renderer.DrawRectangle(highlightRect, highlightColor);

                        // Move to next potential match
                        searchIndex = foundIndex + _searchFilter.Length;
                    }
                }
            }

            // Draw text - use integer positions and manually clip to content width
            var textPos = new Vector2((int)(resolvedRect.X + LinePadding), (int)y);

            // Manual text clipping workaround for FontStashSharp not respecting scissor test
            var availableWidth = contentWidth - (LinePadding * 2);
            var textSize = renderer.MeasureText(line.Text);

            if (textSize.X <= availableWidth)
            {
                // Text fits completely - draw normally
                renderer.DrawText(line.Text, textPos, line.Color);
            }
            else
            {
                // Text is too long - use binary search to find optimal truncation point
                int left = 0;
                int right = line.Text.Length;
                int bestFit = 0;

                while (left <= right)
                {
                    int mid = (left + right) / 2;
                    var testText = line.Text.Substring(0, mid);
                    var testSize = renderer.MeasureText(testText);

                    if (testSize.X <= availableWidth)
                    {
                        bestFit = mid;
                        left = mid + 1;
                    }
                    else
                    {
                        right = mid - 1;
                    }
                }

                if (bestFit > 0)
                {
                    var truncatedText = line.Text.Substring(0, bestFit);
                    renderer.DrawText(truncatedText, textPos, line.Color);
                }
            }

            // Use the font's actual line height
            var fontLineHeight = renderer.GetLineHeight();
            y += Math.Max(fontLineHeight, LineHeight);
        }

        // Pop clipping rectangle
        renderer.PopClip();

        // Draw scrollbar if needed (after popping clip so scrollbar isn't clipped)
        if (hasScrollbar)
        {
            DrawScrollbar(renderer, resolvedRect, lines.Count, visibleCount, startIndex);
        }
    }

    protected override bool IsInteractive() => true;

    private void DrawScrollbar(UIRenderer renderer, LayoutRect rect, int totalLines, int visibleLines, int scrollOffset)
    {
        // Apply padding to scrollbar to match content area
        var scrollbarY = rect.Y + LinePadding;
        var scrollbarHeight = rect.Height - (LinePadding * 2);

        // Scrollbar track (positioned at the right edge, with padding)
        var trackRect = new LayoutRect(
            rect.Right - ScrollbarWidth,
            scrollbarY,
            ScrollbarWidth,
            scrollbarHeight
        );
        renderer.DrawRectangle(trackRect, ScrollbarTrackColor);

        // Scrollbar thumb
        float thumbHeight = Math.Max(20, (float)visibleLines / totalLines * scrollbarHeight);
        float thumbY = scrollbarY + ((float)scrollOffset / (totalLines - visibleLines)) * (scrollbarHeight - thumbHeight);

        var thumbRect = new LayoutRect(
            rect.Right - ScrollbarWidth,
            thumbY,
            ScrollbarWidth,
            thumbHeight
        );

        var thumbColor = IsScrollbarHovered(thumbRect) ? ScrollbarThumbHoverColor : ScrollbarThumbColor;
        renderer.DrawRectangle(thumbRect, thumbColor);
    }

    private bool IsScrollbarHovered(LayoutRect thumbRect)
    {
        return thumbRect.Contains(_lastMousePosition);
    }

    private int GetLineAtPosition(Point mousePos, LayoutRect rect)
    {
        if (!rect.Contains(mousePos))
            return -1;

        float relativeY = mousePos.Y - rect.Y - LinePadding;
        int lineIndex = (int)(relativeY / LineHeight);
        int actualLine = _scrollOffset + lineIndex;

        var lines = GetFilteredLines();
        return actualLine >= 0 && actualLine < lines.Count ? actualLine : -1;
    }
}

