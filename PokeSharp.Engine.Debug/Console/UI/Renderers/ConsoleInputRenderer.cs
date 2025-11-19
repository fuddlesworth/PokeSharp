using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using PokeSharp.Engine.Debug.Console.Configuration;
using PokeSharp.Engine.Debug.Console.Features;
using static PokeSharp.Engine.Debug.Console.Configuration.ConsoleColors;
using System.Collections.Generic;

namespace PokeSharp.Engine.Debug.Console.UI.Renderers;

/// <summary>
/// Handles all input area rendering for the Quake console.
/// Extracted from QuakeConsole to follow Single Responsibility Principle.
/// </summary>
public class ConsoleInputRenderer
{
    private readonly SpriteBatch _spriteBatch;
    private readonly Texture2D _pixel;
    private readonly ConsoleFontRenderer _fontRenderer;
    private float _screenWidth;

    private const int Padding = 10;

    public ConsoleInputRenderer(
        SpriteBatch spriteBatch,
        Texture2D pixel,
        ConsoleFontRenderer fontRenderer,
        float screenWidth)
    {
        _spriteBatch = spriteBatch;
        _pixel = pixel;
        _fontRenderer = fontRenderer;
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
    /// Draws the input area with syntax highlighting and multi-line support.
    /// </summary>
    public void DrawInputArea(
        int y,
        int height,
        int lineHeight,
        ConsoleInputField input,
        ConsoleConfig config,
        List<ConsoleSyntaxHighlighter.ColoredSegment>? highlightedInput)
    {
        // Draw input background
        DrawRectangle(Padding, y, (int)_screenWidth - Padding * 2, height, Background_Secondary);

        int inputX;
        int inputY = y + ConsoleConstants.Rendering.InputYOffset;

        // Different layout for single-line vs multi-line
        if (input.IsMultiLine)
        {
            // Multi-line: Use line numbers instead of ">" prompt
            int maxLineNumber = input.LineCount;
            int digits = maxLineNumber.ToString().Length;
            int lineNumberWidth = (digits + ConsoleConstants.Rendering.LineNumberSuffixChars) *
                                   ConsoleConstants.Rendering.LineNumberCharWidth +
                                   ConsoleConstants.Rendering.LineNumberSpacing;

            inputX = Padding + ConsoleConstants.Rendering.GeneralSpacing + lineNumberWidth;
            DrawLineNumbers(Padding + 5, inputY, lineHeight, input.LineCount, input.GetCursorLine());
        }
        else
        {
            // Single-line: Use ">" prompt
            _fontRenderer.DrawString(">", Padding + ConsoleConstants.Rendering.GeneralSpacing, inputY, Prompt);
            inputX = Padding + ConsoleConstants.Rendering.InputPromptOffset;
        }

        // Draw bracket matching highlight if present
        DrawBracketMatchingHighlight(inputX, inputY, lineHeight, input);

        // Draw selection highlight if present
        if (input.HasSelection)
        {
            DrawSelectionHighlight(inputX, inputY, lineHeight, input);
        }

        // Draw input text with syntax highlighting or cursor
        if (config.SyntaxHighlightingEnabled && highlightedInput != null && highlightedInput.Count > 0)
        {
            // Draw syntax-highlighted text
            int currentX = inputX;
            int currentY = inputY;

            foreach (var segment in highlightedInput)
            {
                // Handle multi-line segments
                var lines = segment.Text.Split('\n');
                for (int i = 0; i < lines.Length; i++)
                {
                    _fontRenderer.DrawString(lines[i], currentX, currentY, segment.Color);

                    if (i < lines.Length - 1)
                    {
                        // New line
                        currentY += lineHeight;
                        currentX = inputX;
                    }
                    else
                    {
                        // Continue on same line
                        var size = _fontRenderer.MeasureString(lines[i]);
                        currentX += (int)size.X;
                    }
                }
            }

            // Draw cursor at current position (approximation)
            DrawCursor(inputX, inputY, lineHeight, input);
        }
        else
        {
            // Draw plain text with cursor
            var inputText = input.GetTextWithCursor();
            var lines = inputText.Split('\n');

            for (int i = 0; i < lines.Length; i++)
            {
                _fontRenderer.DrawString(lines[i], inputX, inputY + i * lineHeight, Text_Primary);
            }
        }

        // Draw multi-line indicator below input box (only if actually multi-line)
        if (input.LineCount > 1)
        {
            // Compact indicator text with better formatting
            var indicator = $"({input.LineCount} lines) [Enter] submit • [Shift+Enter] new line";
            var indicatorY = y + height + ConsoleConstants.Rendering.InputMultiLineIndicatorGap;
            _fontRenderer.DrawString(indicator, Padding + ConsoleConstants.Rendering.GeneralSpacing, indicatorY, Success_Dim);
        }
    }

    /// <summary>
    /// Draws selection highlight background behind selected text.
    /// </summary>
    private void DrawSelectionHighlight(int inputX, int inputY, int lineHeight, ConsoleInputField input)
    {
        if (!input.HasSelection || string.IsNullOrEmpty(input.Text))
            return;

        var text = input.Text;
        int selStart = input.SelectionStart;
        int selEnd = input.SelectionEnd;
        var selectionColor = Selection_Background; // Semi-transparent steel blue

        // Calculate positions for selection
        string textBeforeSelection = text.Substring(0, selStart);
        string selectedText = text.Substring(selStart, selEnd - selStart);

        // Split text by newlines to handle multi-line selections
        var linesBeforeSelection = textBeforeSelection.Split('\n');
        var selectedLines = selectedText.Split('\n');

        int currentLine = linesBeforeSelection.Length - 1;
        int startX = inputX;

        // Calculate X position for start of selection (offset by text before it on that line)
        if (linesBeforeSelection.Length > 0)
        {
            var lastLineBeforeSelection = linesBeforeSelection[^1];
            var offsetSize = _fontRenderer.MeasureString(lastLineBeforeSelection);
            startX += (int)offsetSize.X;
        }

        int startY = inputY + (currentLine * lineHeight);

        // Draw selection rectangles for each line
        // lineHeight from font renderer already includes proper spacing for descenders
        for (int i = 0; i < selectedLines.Length; i++)
        {
            var lineText = selectedLines[i];
            var lineSize = _fontRenderer.MeasureString(lineText);
            int lineWidth = (int)lineSize.X;

            // For empty lines (just newline char), use a minimum width
            if (string.IsNullOrEmpty(lineText))
                lineWidth = 5;

            // Cover the full line height (already includes descender space from font metrics)
            int rectY = startY + (i * lineHeight);
            int rectHeight = lineHeight;

            // First line uses calculated startX, subsequent lines start at inputX
            int rectX = (i == 0) ? startX : inputX;

            DrawRectangle(rectX, rectY, lineWidth, rectHeight, selectionColor);
        }
    }

    /// <summary>
    /// Draws line numbers for multi-line input.
    /// </summary>
    private void DrawLineNumbers(int x, int y, int lineHeight, int lineCount, int currentLine)
    {
        var lineNumColor = LineNumber_Dim; // Dimmed gray
        var currentLineColor = LineNumber_Current; // Highlight current line

        // Calculate the width needed for the largest line number
        var maxLineNum = lineCount.ToString();
        var maxWidth = _fontRenderer.MeasureString(maxLineNum + ": ");

        for (int i = 0; i < lineCount; i++)
        {
            var lineNum = (i + 1).ToString();
            var color = (i == currentLine) ? currentLineColor : lineNumColor;
            var lineY = y + (i * lineHeight);

            // Right-align the line number within the calculated width
            var lineNumText = lineNum + ":";
            var numSize = _fontRenderer.MeasureString(lineNumText);
            var numX = x + (int)maxWidth.X - (int)numSize.X;

            _fontRenderer.DrawString(lineNumText, numX, lineY, color);
        }
    }

    /// <summary>
    /// Draws bracket matching highlight.
    /// </summary>
    private void DrawBracketMatchingHighlight(int inputX, int inputY, int lineHeight, ConsoleInputField input)
    {
        // Find bracket near cursor
        int bracketPos = input.GetBracketNearCursor();
        if (bracketPos == -1)
            return;

        int matchingPos = input.FindMatchingBracket(bracketPos);

        // Highlight both brackets
        var highlightColor = matchingPos != -1
            ? Bracket_Match // Green if matched
            : Bracket_Mismatch; // Red if unmatched

        HighlightCharacterAt(bracketPos, inputX, inputY, lineHeight, highlightColor, input);

        if (matchingPos != -1)
        {
            HighlightCharacterAt(matchingPos, inputX, inputY, lineHeight, highlightColor, input);
        }
    }

    /// <summary>
    /// Highlights a single character at a given position in the input text.
    /// </summary>
    private void HighlightCharacterAt(int position, int inputX, int inputY, int lineHeight, Color color, ConsoleInputField input)
    {
        if (position < 0 || position >= input.Text.Length)
            return;

        // Calculate line and column for the position
        var textBefore = input.Text.Substring(0, position);
        var linesBefore = textBefore.Split('\n');
        int line = linesBefore.Length - 1;

        // Get text on the current line up to the character
        var textOnLine = linesBefore[^1];
        var offsetSize = _fontRenderer.MeasureString(textOnLine);

        // Measure the character itself
        var charText = input.Text[position].ToString();
        var charSize = _fontRenderer.MeasureString(charText);

        int highlightX = inputX + (int)offsetSize.X;
        int highlightY = inputY + (line * lineHeight);

        DrawRectangle(highlightX, highlightY, (int)charSize.X, lineHeight, color);
    }

    /// <summary>
    /// Draws a simple cursor indicator.
    /// </summary>
    private void DrawCursor(int x, int y, int lineHeight, ConsoleInputField input)
    {
        // Calculate cursor position based on line and column
        int cursorLine = input.GetCursorLine();
        int cursorColumn = input.GetCursorColumn();

        // Get the text up to the cursor position on the current line to measure width
        var textBeforeCursor = GetTextOnLine(cursorLine, input).Substring(0, Math.Min(cursorColumn, GetTextOnLine(cursorLine, input).Length));
        var size = _fontRenderer.MeasureString(textBeforeCursor);

        int cursorX = x + (int)size.X;
        int cursorY = y + (cursorLine * lineHeight);

        // Draw cursor rectangle
        DrawRectangle(cursorX, cursorY, 2, lineHeight, Cursor);
    }

    /// <summary>
    /// Gets the text on a specific line (0-based).
    /// </summary>
    private static string GetTextOnLine(int lineNumber, ConsoleInputField input)
    {
        var lines = input.Text.Split('\n');
        if (lineNumber >= 0 && lineNumber < lines.Length)
            return lines[lineNumber];
        return "";
    }

    /// <summary>
    /// Draws a filled rectangle.
    /// </summary>
    private void DrawRectangle(int x, int y, int width, int height, Color color)
    {
        _spriteBatch.Draw(_pixel, new Rectangle(x, y, width, height), color);
    }
}

