using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using PokeSharp.Engine.Debug.Console.Configuration;
using PokeSharp.Engine.Debug.Console.Features;
using System.Collections.Generic;
using static PokeSharp.Engine.Debug.Console.Configuration.ConsoleColors;

namespace PokeSharp.Engine.Debug.Console.UI.Renderers;

/// <summary>
/// Handles rendering of search functionality (forward search and reverse-i-search).
/// Separated from QuakeConsole to follow Single Responsibility Principle.
/// </summary>
public class ConsoleSearchRenderer
{
    private readonly ConsoleFontRenderer _fontRenderer;
    private readonly SpriteBatch _spriteBatch;
    private readonly Texture2D _pixel;
    private float _screenWidth;

    public ConsoleSearchRenderer(ConsoleFontRenderer fontRenderer, SpriteBatch spriteBatch, Texture2D pixel, float screenWidth)
    {
        _fontRenderer = fontRenderer;
        _spriteBatch = spriteBatch;
        _pixel = pixel;
        _screenWidth = screenWidth;
    }

    /// <summary>
    /// Updates the screen width when window is resized.
    /// </summary>
    public void UpdateScreenSize(float screenWidth)
    {
        _screenWidth = screenWidth;
    }

    /// <summary>
    /// Draws search highlights for matching text in the output.
    /// </summary>
    public void DrawSearchHighlights(int outputY, int lineHeight, OutputSearcher searcher, int scrollOffset, IReadOnlyList<ConsoleLine> allLines)
    {
        var currentMatchColor = Search_CurrentMatch; // Bright yellow/orange
        var matchColor = Search_OtherMatches; // Light blue
        var currentMatch = searcher.GetCurrentMatch();

        foreach (var match in searcher.Matches)
        {
            // Check if match is in visible area
            int relativeLineIndex = match.LineIndex - scrollOffset;
            if (relativeLineIndex < 0 || relativeLineIndex >= ConsoleConstants.Limits.DefaultVisibleLines)
                continue;

            if (match.LineIndex >= allLines.Count)
                continue;

            var lineText = allLines[match.LineIndex].Text;
            if (match.StartColumn + match.Length > lineText.Length)
                continue;

            // Measure text position
            var textBeforeMatch = lineText.Substring(0, match.StartColumn);
            var matchText = lineText.Substring(match.StartColumn, match.Length);
            var offsetSize = _fontRenderer.MeasureString(textBeforeMatch);
            var matchSize = _fontRenderer.MeasureString(matchText);

            int highlightX = ConsoleConstants.Rendering.Padding + (int)offsetSize.X;
            int highlightY = outputY + (relativeLineIndex * lineHeight);

            bool isCurrentMatch = currentMatch != null &&
                                 currentMatch.LineIndex == match.LineIndex &&
                                 currentMatch.StartColumn == match.StartColumn;

            var color = isCurrentMatch ? currentMatchColor : matchColor;
            DrawRectangle(highlightX, highlightY, (int)matchSize.X, lineHeight, color);
        }
    }

    /// <summary>
    /// Draws the forward search bar UI.
    /// </summary>
    public void DrawSearchBar(int consoleHeight, int lineHeight, string searchInput, OutputSearcher searcher)
    {
        int searchBarHeight = lineHeight + ConsoleConstants.Rendering.SearchBarHeight;
        int searchBarY = consoleHeight - searchBarHeight - ConsoleConstants.Rendering.SearchBarBottomOffset;

        // Background
        DrawRectangle(0, searchBarY, (int)_screenWidth, searchBarHeight, Background_Elevated);

        int textY = searchBarY + 3;

        // Draw search prompt and input
        string prompt = "Search: ";
        _fontRenderer.DrawString(prompt, ConsoleConstants.Rendering.Padding, textY, Search_Prompt);
        var promptSize = _fontRenderer.MeasureString(prompt);
        int inputX = ConsoleConstants.Rendering.Padding + (int)promptSize.X;
        _fontRenderer.DrawString(searchInput + "_", inputX, textY, Text_Primary);

        // Draw match count and hints
        string rightSide = BuildSearchRightSideText(searcher, searchInput);
        var rightSize = _fontRenderer.MeasureString(rightSide);
        int rightX = (int)_screenWidth - ConsoleConstants.Rendering.Padding - (int)rightSize.X;

        var rightColor = searcher.IsSearching ? Search_Success : Search_Disabled;
        _fontRenderer.DrawString(rightSide, rightX, textY, rightColor);
    }

    /// <summary>
    /// Draws the reverse-i-search bar UI.
    /// </summary>
    public void DrawReverseSearchBar(int consoleHeight, int lineHeight, string reverseSearchInput,
                                    List<string> matches, int matchIndex, string currentMatch)
    {
        int searchBarHeight = lineHeight + ConsoleConstants.Rendering.SearchBarHeight;
        int searchBarY = consoleHeight - searchBarHeight - ConsoleConstants.Rendering.SearchBarBottomOffset;

        // Background
        DrawRectangle(0, searchBarY, (int)_screenWidth, searchBarHeight, Background_Elevated);

        int textY = searchBarY + 3;

        // Draw reverse-i-search prompt and input
        string prompt = "(reverse-i-search)` ";
        _fontRenderer.DrawString(prompt, ConsoleConstants.Rendering.Padding, textY, ReverseSearch_Prompt);
        var promptSize = _fontRenderer.MeasureString(prompt);
        int inputX = ConsoleConstants.Rendering.Padding + (int)promptSize.X;
        _fontRenderer.DrawString(reverseSearchInput + "_", inputX, textY, Text_Primary);

        // Draw match info
        string rightSide = BuildReverseSearchRightSideText(matches, matchIndex, reverseSearchInput);
        var rightSize = _fontRenderer.MeasureString(rightSide);
        int rightX = (int)_screenWidth - ConsoleConstants.Rendering.Padding - (int)rightSize.X;

        var rightColor = matches.Count > 0 ? Search_Success : Search_Disabled;
        _fontRenderer.DrawString(rightSide, rightX, textY, rightColor);

        // Show matched command preview
        if (currentMatch != null)
        {
            DrawReverseSearchPreview(searchBarY, lineHeight, currentMatch, reverseSearchInput);
        }
    }

    private void DrawReverseSearchPreview(int searchBarY, int lineHeight, string match, string searchInput)
    {
        int previewY = searchBarY - lineHeight - 6;
        DrawRectangle(0, previewY, (int)_screenWidth, lineHeight + 6, Background_Secondary);

        int matchIndex = match.IndexOf(searchInput, System.StringComparison.OrdinalIgnoreCase);
        if (matchIndex >= 0)
        {
            // Highlight the matched portion
            var beforeMatch = match.Substring(0, matchIndex);
            var matchText = match.Substring(matchIndex, searchInput.Length);
            var afterMatch = match.Substring(matchIndex + searchInput.Length);

            int previewTextY = previewY + 3;
            int currentX = ConsoleConstants.Rendering.Padding;

            if (beforeMatch.Length > 0)
            {
                _fontRenderer.DrawString(beforeMatch, currentX, previewTextY, Text_Secondary);
                currentX += (int)_fontRenderer.MeasureString(beforeMatch).X;
            }

            _fontRenderer.DrawString(matchText, currentX, previewTextY, ReverseSearch_MatchHighlight);
            currentX += (int)_fontRenderer.MeasureString(matchText).X;

            if (afterMatch.Length > 0)
            {
                _fontRenderer.DrawString(afterMatch, currentX, previewTextY, Text_Secondary);
            }
        }
        else
        {
            _fontRenderer.DrawString(match, ConsoleConstants.Rendering.Padding, previewY + 3, Text_Secondary);
        }
    }

    private string BuildSearchRightSideText(OutputSearcher searcher, string searchInput)
    {
        if (searcher.IsSearching)
        {
            return $"({searcher.CurrentMatchIndex + 1}/{searcher.MatchCount})  •  F3: Next  Shift+F3: Prev  Esc: Exit";
        }
        else if (!string.IsNullOrEmpty(searchInput))
        {
            return "No matches  •  F3: Next  Shift+F3: Prev  Esc: Exit";
        }
        else
        {
            return "F3: Next  Shift+F3: Prev  Esc: Exit";
        }
    }

    private string BuildReverseSearchRightSideText(List<string> matches, int matchIndex, string searchInput)
    {
        if (matches.Count > 0)
        {
            return $"({matchIndex + 1}/{matches.Count})  •  Ctrl+R: Next  Ctrl+S: Prev  Enter: Accept  Esc: Cancel";
        }
        else if (!string.IsNullOrEmpty(searchInput))
        {
            return "No matches  •  Ctrl+R: Next  Ctrl+S: Prev  Enter: Accept  Esc: Cancel";
        }
        else
        {
            return "Type to search history  •  Enter: Accept  Esc: Cancel";
        }
    }

    private void DrawRectangle(int x, int y, int width, int height, Color color)
    {
        _spriteBatch.Draw(_pixel, new Rectangle(x, y, width, height), color);
    }
}