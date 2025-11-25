namespace PokeSharp.Engine.UI.Debug.Components.Controls;

/// <summary>
/// Manages text selection state for a text editor.
/// Handles selection ranges, manipulation, and querying.
/// </summary>
public class TextEditorSelection
{
    private bool _hasSelection;
    private int _startLine;
    private int _startColumn;
    private int _endLine;
    private int _endColumn;

    /// <summary>
    /// Gets whether there is an active selection.
    /// </summary>
    public bool HasSelection => _hasSelection;

    /// <summary>
    /// Gets the selection start line.
    /// </summary>
    public int StartLine => _startLine;

    /// <summary>
    /// Gets the selection start column.
    /// </summary>
    public int StartColumn => _startColumn;

    /// <summary>
    /// Gets the selection end line.
    /// </summary>
    public int EndLine => _endLine;

    /// <summary>
    /// Gets the selection end column.
    /// </summary>
    public int EndColumn => _endColumn;

    /// <summary>
    /// Starts a new selection at the given position.
    /// </summary>
    public void Start(int line, int column)
    {
        _hasSelection = true;
        _startLine = line;
        _startColumn = column;
        _endLine = line;
        _endColumn = column;
    }

    /// <summary>
    /// Extends the selection to the given position.
    /// </summary>
    public void ExtendTo(int line, int column)
    {
        if (!_hasSelection)
        {
            Start(line, column);
            return;
        }

        _endLine = line;
        _endColumn = column;
    }

    /// <summary>
    /// Clears the selection.
    /// </summary>
    public void Clear()
    {
        _hasSelection = false;
    }

    /// <summary>
    /// Selects all text.
    /// </summary>
    public void SelectAll(int totalLines, Func<int, int> getLineLength)
    {
        _hasSelection = true;
        _startLine = 0;
        _startColumn = 0;
        _endLine = totalLines - 1;
        _endColumn = getLineLength(_endLine);
    }

    /// <summary>
    /// Selects the word at the given position.
    /// </summary>
    public void SelectWord(int line, int column, string lineText)
    {
        if (string.IsNullOrEmpty(lineText) || column > lineText.Length)
        {
            Clear();
            return;
        }

        // Find word boundaries
        int wordStart = column;
        int wordEnd = column;

        // Move start back to word boundary
        while (wordStart > 0 && IsWordChar(lineText[wordStart - 1]))
            wordStart--;

        // Move end forward to word boundary
        while (wordEnd < lineText.Length && IsWordChar(lineText[wordEnd]))
            wordEnd++;

        _hasSelection = true;
        _startLine = line;
        _startColumn = wordStart;
        _endLine = line;
        _endColumn = wordEnd;
    }

    /// <summary>
    /// Selects the entire line at the given position.
    /// </summary>
    public void SelectLine(int line, int lineLength)
    {
        _hasSelection = true;
        _startLine = line;
        _startColumn = 0;
        _endLine = line;
        _endColumn = lineLength;
    }

    /// <summary>
    /// Gets the normalized selection range (start before end).
    /// </summary>
    public (int startLine, int startColumn, int endLine, int endColumn) GetNormalizedRange()
    {
        if (!_hasSelection)
            return (0, 0, 0, 0);

        // Normalize so start is always before end
        if (_startLine > _endLine || (_startLine == _endLine && _startColumn > _endColumn))
        {
            return (_endLine, _endColumn, _startLine, _startColumn);
        }

        return (_startLine, _startColumn, _endLine, _endColumn);
    }

    /// <summary>
    /// Checks if a given position is within the selection.
    /// </summary>
    public bool ContainsPosition(int line, int column)
    {
        if (!_hasSelection)
            return false;

        var (startLine, startColumn, endLine, endColumn) = GetNormalizedRange();

        if (line < startLine || line > endLine)
            return false;

        if (line == startLine && line == endLine)
            return column >= startColumn && column < endColumn;

        if (line == startLine)
            return column >= startColumn;

        if (line == endLine)
            return column < endColumn;

        return true; // Middle line
    }

    /// <summary>
    /// Gets the selected text from the given lines.
    /// </summary>
    public string GetSelectedText(List<string> lines)
    {
        if (!_hasSelection || lines.Count == 0)
            return string.Empty;

        var (startLine, startColumn, endLine, endColumn) = GetNormalizedRange();

        // Ensure indices are valid
        startLine = Math.Clamp(startLine, 0, lines.Count - 1);
        endLine = Math.Clamp(endLine, 0, lines.Count - 1);

        if (startLine == endLine)
        {
            // Single line selection
            var line = lines[startLine];
            startColumn = Math.Clamp(startColumn, 0, line.Length);
            endColumn = Math.Clamp(endColumn, 0, line.Length);
            return line.Substring(startColumn, endColumn - startColumn);
        }

        // Multi-line selection
        var result = new System.Text.StringBuilder();

        for (int i = startLine; i <= endLine; i++)
        {
            var line = lines[i];

            if (i == startLine)
            {
                // First line: from start column to end
                startColumn = Math.Clamp(startColumn, 0, line.Length);
                result.Append(line.Substring(startColumn));
            }
            else if (i == endLine)
            {
                // Last line: from beginning to end column
                endColumn = Math.Clamp(endColumn, 0, line.Length);
                result.Append(line.Substring(0, endColumn));
            }
            else
            {
                // Middle lines: entire line
                result.Append(line);
            }

            if (i < endLine)
                result.Append('\n');
        }

        return result.ToString();
    }

    /// <summary>
    /// Checks if a character is part of a word (for word selection).
    /// </summary>
    private static bool IsWordChar(char c)
    {
        return char.IsLetterOrDigit(c) || c == '_';
    }
}

