using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Input;
using PokeSharp.Engine.UI.Debug.Components.Base;
using PokeSharp.Engine.UI.Debug.Core;
using PokeSharp.Engine.UI.Debug.Input;
using PokeSharp.Engine.UI.Debug.Layout;
using PokeSharp.Engine.UI.Debug.Utilities;

namespace PokeSharp.Engine.UI.Debug.Components.Controls;

/// <summary>
/// Multi-line text editor with syntax highlighting, command history, and auto-completion support.
/// Designed for code input in console-like interfaces.
/// </summary>
public class TextEditor : UIComponent, ITextInput
{
    private List<string> _lines = new() { "" }; // Start with one empty line
    private int _cursorLine = 0;
    private int _cursorColumn = 0;
    private float _cursorBlinkTimer = 0;
    private const float CursorBlinkRate = 0.5f;

    // Undo/Redo
    private readonly TextEditorUndoRedo _undoRedo = new();

    // Scrolling
    private int _scrollOffsetY = 0; // Line offset for vertical scrolling

    // Selection
    private bool _hasSelection = false;
    private int _selectionStartLine = 0;
    private int _selectionStartColumn = 0;
    private int _selectionEndLine = 0;
    private int _selectionEndColumn = 0;

    // Mouse state for drag selection
    private bool _isMouseDown = false;
    private float _lastClickTime = 0;
    private const float DoubleClickThreshold = 0.5f; // 500ms

    // Command history
    private readonly List<string> _history = new();
    private int _historyIndex = -1;
    private string _temporaryInput = string.Empty;
    private int _maxHistory = 100;
    private bool _historyPersistenceEnabled = true;

    // Visual properties
    public Color BackgroundColor { get; set; } = UITheme.Dark.InputBackground;
    public Color TextColor { get; set; } = UITheme.Dark.InputText;
    public Color CursorColor { get; set; } = UITheme.Dark.InputCursor;
    public Color SelectionColor { get; set; } = UITheme.Dark.InputSelection;
    public Color BracketMatchColor { get; set; } = UITheme.Dark.BracketMatch;
    public Color BorderColor { get; set; } = UITheme.Dark.BorderPrimary;
    public Color FocusBorderColor { get; set; } = UITheme.Dark.BorderFocus;
    public Color LineNumberColor { get; set; } = UITheme.Dark.LineNumberDim;
    public Color CurrentLineNumberColor { get; set; } = UITheme.Dark.LineNumberCurrent;
    public float BorderThickness { get; set; } = 1;
    public float Padding { get; set; } = UITheme.Dark.PaddingMedium;
    public bool ShowLineNumbers { get; set; } = false;

    // Prompt string (e.g., "> ")
    public string Prompt { get; set; } = "> ";
    public Color PromptColor { get; set; } = UITheme.Dark.Prompt;

    // Properties
    public string Text
    {
        get => string.Join("\n", _lines);
        set => SetText(value);
    }

    public int LineCount => _lines.Count;
    public int CursorPosition => GetAbsoluteCursorPosition();
    public bool HasSelection => _hasSelection;
    public string SelectedText => GetSelectedText();
    public bool IsMultiLine => _lines.Count > 1;

    // Dynamic sizing
    public int MaxVisibleLines { get; set; } = 10; // Maximum lines before scrolling
    public int MinVisibleLines { get; set; } = 1; // Minimum height in lines

    /// <summary>
    /// Calculates the desired height based on the number of lines.
    /// </summary>
    /// <param name="renderer">Optional renderer to use. If null, uses a default line height.</param>
    public float GetDesiredHeight(UIRenderer? renderer = null)
    {
        // Use provided renderer, or try to get from context, or use default
        float lineHeight = 20f; // Default fallback

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

        var visibleLines = Math.Clamp(_lines.Count, MinVisibleLines, MaxVisibleLines);
        return Padding * 2 + (visibleLines * lineHeight);
    }

    // Events (ITextInput interface)
    public event Action<string>? OnSubmit;
    public event Action<string>? OnTextChanged;

    // Additional events (not in ITextInput)
    public Action<string>? OnRequestCompletions { get; set; }
    public Action? OnEscape { get; set; }

    public TextEditor(string id) { Id = id; }

    /// <summary>
    /// Sets the text programmatically.
    /// </summary>
    public void SetText(string text)
    {
        if (string.IsNullOrEmpty(text))
        {
            _lines = new List<string> { "" };
            _cursorLine = 0;
            _cursorColumn = 0;
        }
        else
        {
            _lines = text.Split('\n').ToList();
            _cursorLine = Math.Clamp(_cursorLine, 0, _lines.Count - 1);
            _cursorColumn = Math.Clamp(_cursorColumn, 0, _lines[_cursorLine].Length);
        }

        OnTextChanged?.Invoke(Text);
    }

    /// <summary>
    /// Completes the current word at cursor with the given text.
    /// </summary>
    public void CompleteText(string completionText)
    {
        var currentLine = _lines[_cursorLine];

        // Find word start
        int wordStart = _cursorColumn;
        while (wordStart > 0)
        {
            char c = currentLine[wordStart - 1];
            if (char.IsWhiteSpace(c) || c == '(' || c == ')' || c == '[' || c == ']' ||
                c == '{' || c == '}' || c == ',' || c == ';' || c == '=' || c == '.')
            {
                break;
            }
            wordStart--;
        }

        // Remove partial word and insert completion
        if (wordStart < _cursorColumn)
        {
            currentLine = currentLine.Remove(wordStart, _cursorColumn - wordStart);
            _cursorColumn = wordStart;
        }

        currentLine = currentLine.Insert(_cursorColumn, completionText);
        _lines[_cursorLine] = currentLine;
        _cursorColumn += completionText.Length;

        _historyIndex = -1;
        OnTextChanged?.Invoke(Text);
    }

    /// <summary>
    /// Clears all text.
    /// </summary>
    public void Clear()
    {
        _lines = new List<string> { "" };
        _cursorLine = 0;
        _cursorColumn = 0;
        _scrollOffsetY = 0;
        ClearSelection();
        _historyIndex = -1;
        _undoRedo.Clear(); // Clear undo/redo history
        OnTextChanged?.Invoke(Text);
    }

    /// <summary>
    /// Requests focus for this input component (ITextInput interface).
    /// </summary>
    public void Focus()
    {
        Context?.SetFocus(Id);
    }

    /// <summary>
    /// Submits the current text (Ctrl+Enter or when single-line and Enter pressed).
    /// </summary>
    public void Submit()
    {
        var text = Text;
        if (string.IsNullOrWhiteSpace(text))
            return;

        // Add to history
        if (_history.Count == 0 || _history[^1] != text)
        {
            _history.Add(text);
            if (_history.Count > _maxHistory)
                _history.RemoveAt(0);

            // Save history to disk if persistence is enabled
            if (_historyPersistenceEnabled)
            {
                SaveHistoryToDisk();
            }
        }

        OnSubmit?.Invoke(text);
        Clear();
    }

    /// <summary>
    /// Loads command history from disk.
    /// </summary>
    public void LoadHistoryFromDisk()
    {
        if (!_historyPersistenceEnabled)
            return;

        var loadedHistory = HistoryPersistence.LoadHistory();
        _history.Clear();
        _history.AddRange(loadedHistory);
        _historyIndex = -1; // Reset history navigation
    }

    /// <summary>
    /// Saves command history to disk.
    /// </summary>
    public void SaveHistoryToDisk()
    {
        if (!_historyPersistenceEnabled)
            return;

        HistoryPersistence.SaveHistory(_history, _maxHistory);
    }

    /// <summary>
    /// Clears command history from memory and disk.
    /// </summary>
    public void ClearHistory()
    {
        _history.Clear();
        _historyIndex = -1;
        _temporaryInput = string.Empty;

        if (_historyPersistenceEnabled)
        {
            HistoryPersistence.ClearHistory();
        }
    }

    /// <summary>
    /// Enables or disables history persistence to disk.
    /// </summary>
    public void SetHistoryPersistence(bool enabled)
    {
        _historyPersistenceEnabled = enabled;
    }

    /// <summary>
    /// Gets the command history.
    /// </summary>
    public List<string> GetHistory()
    {
        return new List<string>(_history);
    }

    /// <summary>
    /// Moves the cursor to the end of the text.
    /// </summary>
    public void MoveCursorToEnd()
    {
        _cursorLine = _lines.Count - 1;
        _cursorColumn = _lines[_cursorLine].Length;
    }

    private void InsertText(string text)
    {
        // Save state for undo
        SaveUndoState();

        if (_hasSelection)
        {
            DeleteSelection();
        }

        var currentLine = _lines[_cursorLine];
        currentLine = currentLine.Insert(_cursorColumn, text);
        _lines[_cursorLine] = currentLine;
        _cursorColumn += text.Length;

        _historyIndex = -1;
        OnTextChanged?.Invoke(Text);
    }

    private void InsertNewLine()
    {
        // Save state for undo
        SaveUndoState();

        if (_hasSelection)
        {
            DeleteSelection();
        }

        var currentLine = _lines[_cursorLine];
        var textAfterCursor = currentLine.Substring(_cursorColumn);
        var textBeforeCursor = currentLine.Substring(0, _cursorColumn);

        _lines[_cursorLine] = textBeforeCursor;
        _lines.Insert(_cursorLine + 1, textAfterCursor);

        _cursorLine++;
        _cursorColumn = 0;

        // Don't call EnsureCursorVisible() here - let the natural layout handle it
        // The editor grows dynamically to show all lines up to MaxVisibleLines
        _historyIndex = -1;
        OnTextChanged?.Invoke(Text);
    }

    private void DeleteBackward()
    {
        // Save state for undo
        SaveUndoState();

        if (_hasSelection)
        {
            DeleteSelection();
            return;
        }

        if (_cursorColumn > 0)
        {
            // Delete character in current line
            var currentLine = _lines[_cursorLine];
            currentLine = currentLine.Remove(_cursorColumn - 1, 1);
            _lines[_cursorLine] = currentLine;
            _cursorColumn--;
        }
        else if (_cursorLine > 0)
        {
            // Merge with previous line
            var previousLine = _lines[_cursorLine - 1];
            var currentLine = _lines[_cursorLine];
            _cursorColumn = previousLine.Length;
            _lines[_cursorLine - 1] = previousLine + currentLine;
            _lines.RemoveAt(_cursorLine);
            _cursorLine--;
        }

        _historyIndex = -1;
        OnTextChanged?.Invoke(Text);
    }

    private void DeleteForward()
    {
        // Save state for undo
        SaveUndoState();

        if (_hasSelection)
        {
            DeleteSelection();
            return;
        }

        var currentLine = _lines[_cursorLine];
        if (_cursorColumn < currentLine.Length)
        {
            // Delete character in current line
            currentLine = currentLine.Remove(_cursorColumn, 1);
            _lines[_cursorLine] = currentLine;
        }
        else if (_cursorLine < _lines.Count - 1)
        {
            // Merge with next line
            var nextLine = _lines[_cursorLine + 1];
            _lines[_cursorLine] = currentLine + nextLine;
            _lines.RemoveAt(_cursorLine + 1);
        }

        _historyIndex = -1;
        OnTextChanged?.Invoke(Text);
    }

    private void DeleteSelection()
    {
        if (!_hasSelection)
            return;

        var (startLine, startCol, endLine, endCol) = GetNormalizedSelection();

        if (startLine == endLine)
        {
            // Single line selection
            var line = _lines[startLine];
            _lines[startLine] = line.Remove(startCol, endCol - startCol);
        }
        else
        {
            // Multi-line selection
            var firstLine = _lines[startLine].Substring(0, startCol);
            var lastLine = _lines[endLine].Substring(endCol);

            // Remove lines in between
            for (int i = endLine; i >= startLine; i--)
            {
                _lines.RemoveAt(i);
            }

            // Insert merged line
            _lines.Insert(startLine, firstLine + lastLine);
        }

        _cursorLine = startLine;
        _cursorColumn = startCol;
        ClearSelection();
        OnTextChanged?.Invoke(Text);
    }

    private (int startLine, int startCol, int endLine, int endCol) GetNormalizedSelection()
    {
        if (_selectionStartLine < _selectionEndLine ||
            (_selectionStartLine == _selectionEndLine && _selectionStartColumn < _selectionEndColumn))
        {
            return (_selectionStartLine, _selectionStartColumn, _selectionEndLine, _selectionEndColumn);
        }
        else
        {
            return (_selectionEndLine, _selectionEndColumn, _selectionStartLine, _selectionStartColumn);
        }
    }

    private void ClearSelection()
    {
        _hasSelection = false;
        _selectionStartLine = 0;
        _selectionStartColumn = 0;
        _selectionEndLine = 0;
        _selectionEndColumn = 0;
    }

    private void SelectAll()
    {
        if (_lines.Count == 0)
            return;

        _hasSelection = true;
        _selectionStartLine = 0;
        _selectionStartColumn = 0;
        _selectionEndLine = _lines.Count - 1;
        _selectionEndColumn = _lines[^1].Length;
    }

    private string GetSelectedText()
    {
        if (!_hasSelection)
            return string.Empty;

        var (startLine, startCol, endLine, endCol) = GetNormalizedSelection();

        if (startLine == endLine)
        {
            // Single line selection
            return _lines[startLine].Substring(startCol, endCol - startCol);
        }
        else
        {
            // Multi-line selection
            var result = new System.Text.StringBuilder();

            // First line
            result.Append(_lines[startLine].Substring(startCol));
            result.Append('\n');

            // Middle lines
            for (int i = startLine + 1; i < endLine; i++)
            {
                result.Append(_lines[i]);
                result.Append('\n');
            }

            // Last line
            result.Append(_lines[endLine].Substring(0, endCol));

            return result.ToString();
        }
    }

    private void CopyToClipboard()
    {
        string textToCopy;

        if (_hasSelection)
        {
            // Copy selection
            textToCopy = GetSelectedText();
        }
        else
        {
            // No selection - copy current line
            textToCopy = _lines[_cursorLine];
        }

        if (!string.IsNullOrEmpty(textToCopy))
        {
            ClipboardManager.SetText(textToCopy);
        }
    }

    private void CutToClipboard()
    {
        if (!_hasSelection)
        {
            // No selection - select current line and cut it
            _hasSelection = true;
            _selectionStartLine = _cursorLine;
            _selectionStartColumn = 0;
            _selectionEndLine = _cursorLine;
            _selectionEndColumn = _lines[_cursorLine].Length;
        }

        var textToCut = GetSelectedText();
        if (!string.IsNullOrEmpty(textToCut))
        {
            ClipboardManager.SetText(textToCut);
            DeleteSelection();
        }
    }

    private void PasteFromClipboard()
    {
        var clipboardText = ClipboardManager.GetText();
        if (string.IsNullOrEmpty(clipboardText))
            return;

        // Save state for undo
        SaveUndoState();

        // Delete selection if present
        if (_hasSelection)
        {
            DeleteSelection();
        }

        // Insert clipboard text (handles multi-line)
        var lines = clipboardText.Split('\n');

        if (lines.Length == 1)
        {
            // Single line paste - use InsertText which already saves undo
            // But we already saved above, so directly insert
            var currentLine = _lines[_cursorLine];
            currentLine = currentLine.Insert(_cursorColumn, lines[0]);
            _lines[_cursorLine] = currentLine;
            _cursorColumn += lines[0].Length;
        }
        else
        {
            // Multi-line paste
            var currentLine = _lines[_cursorLine];
            var textAfterCursor = currentLine.Substring(_cursorColumn);
            var textBeforeCursor = currentLine.Substring(0, _cursorColumn);

            // First line: append to current cursor position
            _lines[_cursorLine] = textBeforeCursor + lines[0];

            // Middle lines: insert new lines
            for (int i = 1; i < lines.Length - 1; i++)
            {
                _lines.Insert(_cursorLine + i, lines[i]);
            }

            // Last line: prepend remaining text after cursor
            var lastLine = lines[^1] + textAfterCursor;
            _lines.Insert(_cursorLine + lines.Length - 1, lastLine);

            // Move cursor to end of pasted text
            _cursorLine += lines.Length - 1;
            _cursorColumn = lines[^1].Length;
        }

        _historyIndex = -1;
        OnTextChanged?.Invoke(Text);
    }

    private void SaveUndoState()
    {
        _undoRedo.SaveState(_lines, _cursorLine, _cursorColumn);
    }

    private void Undo()
    {
        var state = _undoRedo.Undo(_lines, _cursorLine, _cursorColumn);
        if (state.HasValue)
        {
            _lines = state.Value.lines;
            _cursorLine = Math.Clamp(state.Value.cursorLine, 0, _lines.Count - 1);
            _cursorColumn = Math.Clamp(state.Value.cursorColumn, 0, _lines[_cursorLine].Length);
            ClearSelection();
            EnsureCursorVisible();
            OnTextChanged?.Invoke(Text);
        }
    }

    private void Redo()
    {
        var state = _undoRedo.Redo();
        if (state.HasValue)
        {
            _lines = state.Value.lines;
            _cursorLine = Math.Clamp(state.Value.cursorLine, 0, _lines.Count - 1);
            _cursorColumn = Math.Clamp(state.Value.cursorColumn, 0, _lines[_cursorLine].Length);
            ClearSelection();
            EnsureCursorVisible();
            OnTextChanged?.Invoke(Text);
        }
    }

    private void MoveToPreviousWord()
    {
        var currentLine = _lines[_cursorLine];
        var newColumn = WordNavigationHelper.FindPreviousWordStart(currentLine, _cursorColumn);

        if (newColumn < _cursorColumn)
        {
            // Found word boundary on current line
            _cursorColumn = newColumn;
        }
        else if (_cursorLine > 0)
        {
            // Jump to end of previous line
            _cursorLine--;
            _cursorColumn = _lines[_cursorLine].Length;
        }
    }

    private void MoveToNextWord()
    {
        var currentLine = _lines[_cursorLine];
        var newColumn = WordNavigationHelper.FindNextWordEnd(currentLine, _cursorColumn);

        if (newColumn > _cursorColumn)
        {
            // Found word boundary on current line
            _cursorColumn = newColumn;
        }
        else if (_cursorLine < _lines.Count - 1)
        {
            // Jump to start of next line
            _cursorLine++;
            _cursorColumn = 0;
        }
    }

    private void DeleteWordBackward()
    {
        // Save state for undo
        SaveUndoState();

        if (_hasSelection)
        {
            DeleteSelection();
            return;
        }

        var currentLine = _lines[_cursorLine];
        var wordStart = WordNavigationHelper.FindPreviousWordStart(currentLine, _cursorColumn);

        if (wordStart < _cursorColumn)
        {
            // Delete from word start to cursor on current line
            currentLine = currentLine.Remove(wordStart, _cursorColumn - wordStart);
            _lines[_cursorLine] = currentLine;
            _cursorColumn = wordStart;
        }
        else if (_cursorLine > 0)
        {
            // Merge with previous line (delete newline)
            var previousLine = _lines[_cursorLine - 1];
            _cursorColumn = previousLine.Length;
            _lines[_cursorLine - 1] = previousLine + currentLine;
            _lines.RemoveAt(_cursorLine);
            _cursorLine--;
        }

        _historyIndex = -1;
        OnTextChanged?.Invoke(Text);
    }

    private void DeleteWordForward()
    {
        // Save state for undo
        SaveUndoState();

        if (_hasSelection)
        {
            DeleteSelection();
            return;
        }

        var currentLine = _lines[_cursorLine];
        var wordEnd = WordNavigationHelper.FindNextWordEnd(currentLine, _cursorColumn);

        if (wordEnd > _cursorColumn)
        {
            // Delete from cursor to word end on current line
            currentLine = currentLine.Remove(_cursorColumn, wordEnd - _cursorColumn);
            _lines[_cursorLine] = currentLine;
        }
        else if (_cursorLine < _lines.Count - 1)
        {
            // Merge with next line (delete newline)
            var nextLine = _lines[_cursorLine + 1];
            _lines[_cursorLine] = currentLine + nextLine;
            _lines.RemoveAt(_cursorLine + 1);
        }

        _historyIndex = -1;
        OnTextChanged?.Invoke(Text);
    }

    private void BeginSelection()
    {
        _hasSelection = true;
        _selectionStartLine = _cursorLine;
        _selectionStartColumn = _cursorColumn;
        _selectionEndLine = _cursorLine;
        _selectionEndColumn = _cursorColumn;
    }

    private void UpdateSelection()
    {
        if (!_hasSelection)
            return;

        _selectionEndLine = _cursorLine;
        _selectionEndColumn = _cursorColumn;
    }

    private void HandleDoubleClick(Point mousePos, UIRenderer renderer)
    {
        // Position cursor at click location
        var (clickedLine, clickedColumn) = GetCursorPositionAtMouse(mousePos, renderer);
        _cursorLine = clickedLine;
        _cursorColumn = clickedColumn;

        // Select word at cursor
        SelectWordAtCursor();
    }

    private void HandleMouseDrag(Point mousePos, UIRenderer renderer)
    {
        // Update cursor position
        var (dragLine, dragColumn) = GetCursorPositionAtMouse(mousePos, renderer);

        // If no selection yet, start one
        if (!_hasSelection)
        {
            BeginSelection();
        }

        // Update cursor and selection
        _cursorLine = dragLine;
        _cursorColumn = dragColumn;
        UpdateSelection();
    }

    private void SelectWordAtCursor()
    {
        var currentLine = _lines[_cursorLine];
        if (string.IsNullOrEmpty(currentLine) || _cursorColumn >= currentLine.Length)
        {
            return;
        }

        // Find word boundaries
        var wordStart = WordNavigationHelper.FindPreviousWordStart(currentLine, _cursorColumn + 1);
        var wordEnd = WordNavigationHelper.FindNextWordEnd(currentLine, _cursorColumn);

        // Select the word
        _hasSelection = true;
        _selectionStartLine = _cursorLine;
        _selectionStartColumn = wordStart;
        _selectionEndLine = _cursorLine;
        _selectionEndColumn = wordEnd;

        // Move cursor to end of word
        _cursorColumn = wordEnd;
    }

    private (int line, int column) GetCursorPositionAtMouse(Point mousePos, UIRenderer renderer)
    {
        var lineHeight = renderer.GetLineHeight();
        var textStartY = Rect.Y + Padding;
        var textStartX = Rect.X + Padding;

        // Calculate left margin width (line numbers or prompt)
        var isMultiLine = _lines.Count > 1;
        float leftMarginWidth;

        if (isMultiLine)
        {
            var maxLineNumText = _lines.Count.ToString() + ": ";
            leftMarginWidth = renderer.MeasureText(maxLineNumText).X + 8;
        }
        else
        {
            leftMarginWidth = renderer.MeasureText(Prompt).X;
        }

        // Calculate which line was clicked
        int clickedLine = _scrollOffsetY + (int)((mousePos.Y - textStartY) / lineHeight);
        clickedLine = Math.Clamp(clickedLine, 0, _lines.Count - 1);

        // Calculate column position
        var lineX = textStartX + leftMarginWidth;
        var relativeX = mousePos.X - lineX;

        int clickedColumn;
        if (relativeX <= 0)
        {
            clickedColumn = 0;
        }
        else
        {
            var lineText = _lines[clickedLine];
            var totalWidth = renderer.MeasureText(lineText).X;

            if (relativeX >= totalWidth)
            {
                clickedColumn = lineText.Length;
            }
            else
            {
                // Binary search for column position
                int left = 0;
                int right = lineText.Length;

                while (left < right)
                {
                    int mid = (left + right) / 2;
                    var substring = lineText.Substring(0, mid);
                    var widthAtMid = renderer.MeasureText(substring).X;

                    if (widthAtMid < relativeX)
                        left = mid + 1;
                    else
                        right = mid;
                }

                // Check midpoint for rounding
                if (left > 0)
                {
                    var widthAtLeft = renderer.MeasureText(lineText.Substring(0, left)).X;
                    var widthAtPrev = renderer.MeasureText(lineText.Substring(0, left - 1)).X;
                    var midPoint = (widthAtPrev + widthAtLeft) / 2;

                    if (relativeX < midPoint)
                        left--;
                }

                clickedColumn = left;
            }
        }

        return (clickedLine, clickedColumn);
    }

    private void DrawBracketMatching(UIRenderer renderer, float textStartX, float textStartY, float leftMarginWidth, float lineHeight)
    {
        // Find bracket pair near cursor
        var bracketPair = BracketMatcher.FindBracketPairNearCursor(_lines, _cursorLine, _cursorColumn);
        if (!bracketPair.HasValue)
            return;

        var (cursor, match) = bracketPair.Value;

        // Draw highlight for cursor bracket
        DrawBracketHighlight(renderer, cursor.line, cursor.col, textStartX, textStartY, leftMarginWidth, lineHeight);

        // Draw highlight for matching bracket
        DrawBracketHighlight(renderer, match.line, match.col, textStartX, textStartY, leftMarginWidth, lineHeight);
    }

    private void DrawBracketHighlight(UIRenderer renderer, int line, int column, float textStartX, float textStartY, float leftMarginWidth, float lineHeight)
    {
        // Check if line is visible
        if (line < _scrollOffsetY || line >= _scrollOffsetY + GetVisibleLineCount())
            return;

        var lineY = textStartY + (line - _scrollOffsetY) * lineHeight;
        var lineX = textStartX + leftMarginWidth;

        // Calculate X position of the bracket
        var textBeforeBracket = _lines[line].Substring(0, column);
        var bracketX = lineX + renderer.MeasureText(textBeforeBracket).X;

        // Get bracket width
        var bracket = _lines[line][column].ToString();
        var bracketWidth = renderer.MeasureText(bracket).X;

        // Draw subtle background highlight
        var highlightRect = new LayoutRect(bracketX, lineY, bracketWidth, lineHeight);
        renderer.DrawRectangle(highlightRect, BracketMatchColor);
    }

    private int GetAbsoluteCursorPosition()
    {
        int position = 0;
        for (int i = 0; i < _cursorLine; i++)
        {
            position += _lines[i].Length + 1; // +1 for newline
        }
        position += _cursorColumn;
        return position;
    }

    private void EnsureCursorVisible()
    {
        // Calculate visible line count
        var lineHeight = Renderer?.GetLineHeight() ?? 20;
        var visibleHeight = Rect.Height - Padding * 2;
        var visibleLines = Math.Max(1, (int)(visibleHeight / lineHeight));

        // If all lines fit on screen, don't scroll at all
        if (_lines.Count <= visibleLines)
        {
            _scrollOffsetY = 0;
            return;
        }

        // Adjust scroll only if cursor is actually out of view
        if (_cursorLine < _scrollOffsetY)
        {
            _scrollOffsetY = _cursorLine;
        }
        else if (_cursorLine >= _scrollOffsetY + visibleLines)
        {
            _scrollOffsetY = _cursorLine - visibleLines + 1;
        }

        _scrollOffsetY = Math.Max(0, _scrollOffsetY);
    }

    /// <summary>
    /// Navigates through command history.
    /// </summary>
    /// <param name="direction">-1 for previous (older), 1 for next (newer)</param>
    private void NavigateHistory(int direction)
    {
        if (_history.Count == 0)
            return;

        // Store current text as temporary if we're starting history navigation
        if (_historyIndex == -1 && direction == -1)
        {
            _temporaryInput = Text;
            // Jump to most recent history item
            _historyIndex = _history.Count - 1;
        }
        else if (_historyIndex == -1 && direction == 1)
        {
            // Already at current input, can't go forward
            return;
        }
        else
        {
            // Navigate within history
            _historyIndex += direction;

            // Clamp to valid range
            if (_historyIndex < 0)
                _historyIndex = 0;
            else if (_historyIndex >= _history.Count)
            {
                // Went past newest history item, return to current input
                _historyIndex = -1;
            }
        }

        // Set text
        if (_historyIndex == -1)
        {
            // Restore temporary input (current/new input)
            SetText(_temporaryInput);
        }
        else
        {
            // Show history item (history is stored oldest to newest)
            SetText(_history[_historyIndex]);
        }

        // Move cursor to end
        _cursorLine = _lines.Count - 1;
        _cursorColumn = _lines[^1].Length;
        EnsureCursorVisible();
    }

    protected override void OnRender(UIContext context)
    {
        // Handle mouse input
        var input = context.Input;
        var mousePos = input.MousePosition;

        // Mouse button down - start selection or position cursor
        if (input.IsMouseButtonPressed(MouseButton.Left))
        {
            if (Rect.Contains(mousePos))
            {
                // Check for double-click
                var currentTime = (float)context.Input.GameTime.TotalGameTime.TotalSeconds;
                var isDoubleClick = (currentTime - _lastClickTime) < DoubleClickThreshold;
                _lastClickTime = currentTime;

                if (isDoubleClick)
                {
                    HandleDoubleClick(mousePos, context.Renderer);
                }
                else
                {
                    HandleMouseClick(mousePos, context.Renderer);
                }

                _isMouseDown = true;
            }
        }

        // Mouse button released
        if (input.IsMouseButtonReleased(MouseButton.Left))
        {
            _isMouseDown = false;
        }

        // Mouse drag - extend selection
        if (_isMouseDown && Rect.Contains(mousePos))
        {
            HandleMouseDrag(mousePos, context.Renderer);
        }

        // Handle keyboard input if focused
        if (IsFocused())
        {
            HandleKeyboardInput(context.Input);
        }

        var renderer = Renderer;
        var resolvedRect = Rect;

        // Draw background
        renderer.DrawRectangle(resolvedRect, BackgroundColor);

        // Draw border
        var borderColor = IsFocused() ? FocusBorderColor : BorderColor;
        DrawBorder(renderer, resolvedRect, borderColor);

        // Calculate text area
        var lineHeight = renderer.GetLineHeight();
        var textStartX = resolvedRect.X + Padding;
        var textStartY = resolvedRect.Y + Padding;

        // Determine if we're in multi-line mode (more than 1 line of text)
        var isMultiLine = _lines.Count > 1;
        float leftMarginWidth;

        if (isMultiLine)
        {
            // Multi-line: Calculate line number width
            var maxLineNumber = _lines.Count;
            var maxLineNumText = maxLineNumber.ToString() + ": ";
            leftMarginWidth = renderer.MeasureText(maxLineNumText).X + 8; // Extra spacing after line numbers
        }
        else
        {
            // Single-line: Use prompt
            if (_scrollOffsetY == 0)
            {
                renderer.DrawText(Prompt, new Vector2(textStartX, textStartY), PromptColor);
            }
            leftMarginWidth = renderer.MeasureText(Prompt).X;
        }

        // Calculate visible lines
        var visibleHeight = resolvedRect.Height - Padding * 2;
        var visibleLines = Math.Max(1, (int)(visibleHeight / lineHeight));
        var endLine = Math.Min(_scrollOffsetY + visibleLines, _lines.Count);

        // Draw line numbers for multi-line mode
        if (isMultiLine)
        {
            DrawLineNumbers(renderer, textStartX, textStartY, lineHeight, endLine);
        }

        // Draw selection backgrounds
        if (_hasSelection)
        {
            DrawSelectionBackgrounds(renderer, textStartX, textStartY, leftMarginWidth, lineHeight);
        }

        // Draw bracket matching highlights
        if (IsFocused())
        {
            DrawBracketMatching(renderer, textStartX, textStartY, leftMarginWidth, lineHeight);
        }

        // Draw text with syntax highlighting
        for (int i = _scrollOffsetY; i < endLine; i++)
        {
            var lineY = textStartY + (i - _scrollOffsetY) * lineHeight;
            var lineX = textStartX + leftMarginWidth; // Always offset by left margin

            if (!string.IsNullOrEmpty(_lines[i]))
            {
                var segments = SyntaxHighlighter.Highlight(_lines[i]);
                float currentX = lineX;

                foreach (var segment in segments)
                {
                    renderer.DrawText(segment.Text, new Vector2(currentX, lineY), segment.Color);
                    currentX += renderer.MeasureText(segment.Text).X;
                }
            }
        }

        // Draw cursor
        if (IsFocused())
        {
            _cursorBlinkTimer += (float)context.Input.GameTime.ElapsedGameTime.TotalSeconds;
            if (_cursorBlinkTimer > CursorBlinkRate)
                _cursorBlinkTimer = 0;

            if (_cursorBlinkTimer < CursorBlinkRate / 2)
            {
                var cursorY = textStartY + (_cursorLine - _scrollOffsetY) * lineHeight;
                if (_cursorLine >= _scrollOffsetY && _cursorLine < _scrollOffsetY + visibleLines)
                {
                    var textBeforeCursor = _lines[_cursorLine].Substring(0, _cursorColumn);
                    var cursorX = textStartX + leftMarginWidth + renderer.MeasureText(textBeforeCursor).X;

                    var cursorRect = new LayoutRect(cursorX, cursorY, 2, lineHeight);
                    renderer.DrawRectangle(cursorRect, CursorColor);
                }
            }
        }

    }

    private void DrawLineNumbers(UIRenderer renderer, float textStartX, float textStartY, float lineHeight, int endLine)
    {
        var dimColor = LineNumberColor;
        var currentLineColor = CurrentLineNumberColor;

        // Calculate the width needed for the largest line number
        var maxLineNumText = _lines.Count.ToString() + ": ";
        var maxWidth = renderer.MeasureText(maxLineNumText).X;

        for (int i = _scrollOffsetY; i < endLine; i++)
        {
            var lineNum = (i + 1).ToString() + ":";
            var color = (i == _cursorLine) ? currentLineColor : dimColor;
            var lineY = textStartY + (i - _scrollOffsetY) * lineHeight;

            // Right-align the line number
            var numSize = renderer.MeasureText(lineNum).X;
            var numX = textStartX + maxWidth - numSize;

            renderer.DrawText(lineNum, new Vector2(numX, lineY), color);
        }
    }

    private void DrawSelectionBackgrounds(UIRenderer renderer, float textStartX, float textStartY, float leftMarginWidth, float lineHeight)
    {
        var (startLine, startCol, endLine, endCol) = GetNormalizedSelection();

        for (int line = startLine; line <= endLine; line++)
        {
            if (line < _scrollOffsetY || line >= _scrollOffsetY + GetVisibleLineCount())
                continue;

            var lineY = textStartY + (line - _scrollOffsetY) * lineHeight;
            var lineX = textStartX + leftMarginWidth; // Always use left margin width

            int selStart = (line == startLine) ? startCol : 0;
            int selEnd = (line == endLine) ? endCol : _lines[line].Length;

            var beforeSelection = _lines[line].Substring(0, selStart);
            var selection = _lines[line].Substring(selStart, selEnd - selStart);

            var beforeWidth = renderer.MeasureText(beforeSelection).X;
            var selectionWidth = renderer.MeasureText(selection).X;

            var selectionRect = new LayoutRect(
                lineX + beforeWidth,
                lineY,
                selectionWidth,
                lineHeight
            );
            renderer.DrawRectangle(selectionRect, SelectionColor);
        }
    }

    private int GetVisibleLineCount()
    {
        var lineHeight = Renderer?.GetLineHeight() ?? 20;
        var visibleHeight = Rect.Height - Padding * 2;
        return Math.Max(1, (int)(visibleHeight / lineHeight));
    }

    private void HandleMouseClick(Point mousePos, UIRenderer renderer)
    {
        // Position cursor at click location
        var (clickedLine, clickedColumn) = GetCursorPositionAtMouse(mousePos, renderer);
        _cursorLine = clickedLine;
        _cursorColumn = clickedColumn;

        // Start selection anchor for potential drag
        BeginSelection();
    }

    private void HandleKeyboardInput(InputState input)
    {
        // Clipboard operations and undo/redo
        if (input.IsCtrlDown())
        {
            // Undo (Ctrl+Z)
            if (input.IsKeyPressed(Keys.Z))
            {
                Undo();
                return;
            }

            // Redo (Ctrl+Y or Ctrl+Shift+Z)
            if (input.IsKeyPressed(Keys.Y) || (input.IsShiftDown() && input.IsKeyPressed(Keys.Z)))
            {
                Redo();
                return;
            }

            // Copy (Ctrl+C)
            if (input.IsKeyPressed(Keys.C))
            {
                CopyToClipboard();
                return;
            }

            // Cut (Ctrl+X)
            if (input.IsKeyPressed(Keys.X))
            {
                CutToClipboard();
                return;
            }

            // Paste (Ctrl+V)
            if (input.IsKeyPressed(Keys.V))
            {
                PasteFromClipboard();
                return;
            }

            // Select All (Ctrl+A)
            if (input.IsKeyPressed(Keys.A))
            {
                SelectAll();
                return;
            }
        }

        // Shift+Enter - New line
        if (input.IsShiftDown() && input.IsKeyPressed(Keys.Enter))
        {
            InsertNewLine();
            return;
        }

        // Enter - Submit
        if (input.IsKeyPressed(Keys.Enter))
        {
            Submit();
            return;
        }

        // Escape
        if (input.IsKeyPressed(Keys.Escape))
        {
            OnEscape?.Invoke();
            return;
        }

        // Tab - Request completions
        if (input.IsKeyPressed(Keys.Tab))
        {
            OnRequestCompletions?.Invoke(Text);
            return;
        }

        // Backspace - with repeat
        if (input.IsKeyPressedWithRepeat(Keys.Back))
        {
            if (input.IsCtrlDown())
            {
                // Ctrl+Backspace - Delete word backward
                DeleteWordBackward();
            }
            else
            {
                // Regular Backspace
                DeleteBackward();
            }
            return;
        }

        // Delete - with repeat
        if (input.IsKeyPressedWithRepeat(Keys.Delete))
        {
            if (input.IsCtrlDown())
            {
                // Ctrl+Delete - Delete word forward
                DeleteWordForward();
            }
            else
            {
                // Regular Delete
                DeleteForward();
            }
            return;
        }

        // Arrow keys - with repeat
        if (input.IsKeyPressedWithRepeat(Keys.Left))
        {
            var isShift = input.IsShiftDown();

            // Start selection if Shift is held
            if (isShift && !_hasSelection)
            {
                BeginSelection();
            }

            if (input.IsCtrlDown())
            {
                // Ctrl+Left - Jump to previous word
                MoveToPreviousWord();
            }
            else
            {
                // Regular Left - Move one character
                if (_cursorColumn > 0)
                    _cursorColumn--;
                else if (_cursorLine > 0)
                {
                    _cursorLine--;
                    _cursorColumn = _lines[_cursorLine].Length;
                }
            }

            if (isShift)
            {
                UpdateSelection();
            }
            else
            {
                ClearSelection();
            }
            EnsureCursorVisible();
            return;
        }

        if (input.IsKeyPressedWithRepeat(Keys.Right))
        {
            var isShift = input.IsShiftDown();

            // Start selection if Shift is held
            if (isShift && !_hasSelection)
            {
                BeginSelection();
            }

            if (input.IsCtrlDown())
            {
                // Ctrl+Right - Jump to next word
                MoveToNextWord();
            }
            else
            {
                // Regular Right - Move one character
                if (_cursorColumn < _lines[_cursorLine].Length)
                    _cursorColumn++;
                else if (_cursorLine < _lines.Count - 1)
                {
                    _cursorLine++;
                    _cursorColumn = 0;
                }
            }

            if (isShift)
            {
                UpdateSelection();
            }
            else
            {
                ClearSelection();
            }
            EnsureCursorVisible();
            return;
        }

        if (input.IsKeyPressedWithRepeat(Keys.Up))
        {
            // In single-line mode, Up navigates to previous history (standard console behavior)
            // In multi-line mode, Ctrl+Up navigates history, plain Up moves cursor
            if (input.IsCtrlDown() || _lines.Count == 1)
            {
                NavigateHistory(-1);
                return;
            }

            var isShift = input.IsShiftDown();

            // Start selection if Shift is held
            if (isShift && !_hasSelection)
            {
                BeginSelection();
            }

            // Regular Up - Move cursor up (only in multi-line mode)
            if (_cursorLine > 0)
            {
                _cursorLine--;
                _cursorColumn = Math.Min(_cursorColumn, _lines[_cursorLine].Length);
                EnsureCursorVisible();
            }

            if (isShift)
            {
                UpdateSelection();
            }
            else
            {
                ClearSelection();
            }
            return;
        }

        if (input.IsKeyPressedWithRepeat(Keys.Down))
        {
            // In single-line mode, Down navigates to next history (standard console behavior)
            // In multi-line mode, Ctrl+Down navigates history, plain Down moves cursor
            if (input.IsCtrlDown() || _lines.Count == 1)
            {
                NavigateHistory(1);
                return;
            }

            var isShift = input.IsShiftDown();

            // Start selection if Shift is held
            if (isShift && !_hasSelection)
            {
                BeginSelection();
            }

            // Regular Down - Move cursor down (only in multi-line mode)
            if (_cursorLine < _lines.Count - 1)
            {
                _cursorLine++;
                _cursorColumn = Math.Min(_cursorColumn, _lines[_cursorLine].Length);
                EnsureCursorVisible();
            }

            if (isShift)
            {
                UpdateSelection();
            }
            else
            {
                ClearSelection();
            }
            return;
        }

        // Home/End
        if (input.IsKeyPressedWithRepeat(Keys.Home))
        {
            var isShift = input.IsShiftDown();

            // Start selection if Shift is held
            if (isShift && !_hasSelection)
            {
                BeginSelection();
            }

            _cursorColumn = 0;

            if (isShift)
            {
                UpdateSelection();
            }
            else
            {
                ClearSelection();
            }
            return;
        }

        if (input.IsKeyPressedWithRepeat(Keys.End))
        {
            var isShift = input.IsShiftDown();

            // Start selection if Shift is held
            if (isShift && !_hasSelection)
            {
                BeginSelection();
            }

            _cursorColumn = _lines[_cursorLine].Length;

            if (isShift)
            {
                UpdateSelection();
            }
            else
            {
                ClearSelection();
            }
            return;
        }

        // Regular character input
        foreach (var key in Enum.GetValues<Keys>())
        {
            if (input.IsKeyPressedWithRepeat(key))
            {
                var ch = KeyToChar(key, input.IsShiftDown());
                if (ch.HasValue)
                {
                    InsertText(ch.Value.ToString());
                }
            }
        }
    }

    private char? KeyToChar(Keys key, bool shift)
    {
        // Letters
        if (key >= Keys.A && key <= Keys.Z)
        {
            char c = (char)('a' + (key - Keys.A));
            return shift ? char.ToUpper(c) : c;
        }

        // Numbers and symbols
        if (key >= Keys.D0 && key <= Keys.D9)
        {
            if (shift)
            {
                return key switch
                {
                    Keys.D0 => ')',
                    Keys.D1 => '!',
                    Keys.D2 => '@',
                    Keys.D3 => '#',
                    Keys.D4 => '$',
                    Keys.D5 => '%',
                    Keys.D6 => '^',
                    Keys.D7 => '&',
                    Keys.D8 => '*',
                    Keys.D9 => '(',
                    _ => null
                };
            }
            return (char)('0' + (key - Keys.D0));
        }

        // Special keys
        return key switch
        {
            Keys.Space => ' ',
            Keys.OemPeriod => shift ? '>' : '.',
            Keys.OemComma => shift ? '<' : ',',
            Keys.OemSemicolon => shift ? ':' : ';',
            Keys.OemQuotes => shift ? '"' : '\'',
            Keys.OemQuestion => shift ? '?' : '/',
            Keys.OemPlus => shift ? '+' : '=',
            Keys.OemMinus => shift ? '_' : '-',
            Keys.OemOpenBrackets => shift ? '{' : '[',
            Keys.OemCloseBrackets => shift ? '}' : ']',
            Keys.OemPipe => shift ? '|' : '\\',
            Keys.OemTilde => shift ? '~' : '`',
            _ => null
        };
    }

    private void DrawBorder(UIRenderer renderer, LayoutRect rect, Color color)
    {
        if (BorderThickness <= 0)
            return;

        // Top
        renderer.DrawRectangle(new LayoutRect(rect.X, rect.Y, rect.Width, BorderThickness), color);
        // Bottom
        renderer.DrawRectangle(new LayoutRect(rect.X, rect.Bottom - BorderThickness, rect.Width, BorderThickness), color);
        // Left
        renderer.DrawRectangle(new LayoutRect(rect.X, rect.Y, BorderThickness, rect.Height), color);
        // Right
        renderer.DrawRectangle(new LayoutRect(rect.Right - BorderThickness, rect.Y, BorderThickness, rect.Height), color);
    }

    protected override bool IsInteractive() => true;
}

