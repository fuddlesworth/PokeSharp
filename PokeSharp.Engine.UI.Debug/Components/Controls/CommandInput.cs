using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Input;
using PokeSharp.Engine.UI.Debug.Components.Base;
using PokeSharp.Engine.UI.Debug.Core;
using PokeSharp.Engine.UI.Debug.Input;
using PokeSharp.Engine.UI.Debug.Layout;
using PokeSharp.Engine.UI.Debug.Utilities;

namespace PokeSharp.Engine.UI.Debug.Components.Controls;

/// <summary>
/// Enhanced input field specifically for command-line interfaces.
/// Supports command history, auto-completion, and multi-line input.
/// </summary>
public class CommandInput : UIComponent, ITextInput
{
    private string _text = string.Empty;
    private int _cursorPosition = 0;
    private float _cursorBlinkTimer = 0;
    private static float CursorBlinkRate => UITheme.Dark.CursorBlinkRate;

    // Selection
    private int _selectionStart = 0;
    private int _selectionEnd = 0;
    private bool _hasSelection = false;

    // Command history
    private readonly List<string> _history = new();
    private int _historyIndex = -1; // -1 = not navigating history
    private string _temporaryInput = string.Empty; // Stores current input when navigating history
    private int _maxHistory = 100;

    // Auto-completion (managed externally by parent component like ConsolePanel)
    // These are kept for backward compatibility but not used in the new architecture
    private List<string> _completionSuggestions = new();
    private int _selectedSuggestionIndex = -1;
    private bool _showSuggestions = false;

    // Multi-line support
    private bool _multiLineMode = false;

    // Visual properties
    public Color BackgroundColor { get; set; } = UITheme.Dark.InputBackground;
    public Color TextColor { get; set; } = UITheme.Dark.InputText;
    public Color CursorColor { get; set; } = UITheme.Dark.InputCursor;
    public Color SelectionColor { get; set; } = UITheme.Dark.InputSelection;
    public Color BorderColor { get; set; } = UITheme.Dark.BorderPrimary;
    public Color FocusBorderColor { get; set; } = UITheme.Dark.BorderFocus;
    public float BorderThickness { get; set; } = 1;
    public float Padding { get; set; } = UITheme.Dark.PaddingMedium;

    // Prompt string (e.g., "> ")
    public string Prompt { get; set; } = "> ";
    public Color PromptColor { get; set; } = UITheme.Dark.Prompt;

    // Properties
    public string Text => _text;
    public int CursorPosition => _cursorPosition;
    public bool HasSelection => _hasSelection;
    public string SelectedText => _hasSelection ? _text.Substring(SelectionStart, SelectionLength) : string.Empty;
    public bool IsMultiLine { get => _multiLineMode; set => _multiLineMode = value; }

    private int SelectionStart => Math.Min(_selectionStart, _selectionEnd);
    private int SelectionEnd => Math.Max(_selectionStart, _selectionEnd);
    private int SelectionLength => SelectionEnd - SelectionStart;

    // Events (ITextInput interface)
    public event Action<string>? OnSubmit;
    public event Action<string>? OnTextChanged;

    // Additional events (not in ITextInput)
    public Action<string>? OnRequestCompletions { get; set; }
    public Action? OnEscape { get; set; }

    public CommandInput(string id) { Id = id; }

    /// <summary>
    /// Requests focus for this input component (ITextInput interface).
    /// </summary>
    public void Focus()
    {
        Context?.SetFocus(Id);
    }

    /// <summary>
    /// Sets the input text programmatically.
    /// </summary>
    public void SetText(string text)
    {
        _text = text;
        _cursorPosition = Math.Clamp(_cursorPosition, 0, _text.Length);
        OnTextChanged?.Invoke(_text);
    }

    /// <summary>
    /// Completes the current word at the cursor position with the given completion text.
    /// Finds the word boundary before the cursor and replaces it.
    /// </summary>
    public void CompleteText(string completionText)
    {
        // Find the start of the current word (go back until we hit a word boundary)
        int wordStart = _cursorPosition;
        while (wordStart > 0)
        {
            char c = _text[wordStart - 1];
            // Word boundary: whitespace, operators, or dot (for member access like Console.WriteLine)
            if (char.IsWhiteSpace(c) || c == '(' || c == ')' || c == '[' || c == ']' ||
                c == '{' || c == '}' || c == ',' || c == ';' || c == '=' || c == '.')
            {
                break;
            }
            wordStart--;
        }

        // Remove the partial word
        if (wordStart < _cursorPosition)
        {
            _text = _text.Remove(wordStart, _cursorPosition - wordStart);
            _cursorPosition = wordStart;
        }

        // Insert the completion
        _text = _text.Insert(_cursorPosition, completionText);
        _cursorPosition += completionText.Length;

        _historyIndex = -1; // Reset history navigation

        OnTextChanged?.Invoke(_text);
    }

    /// <summary>
    /// Clears the input field.
    /// </summary>
    public void Clear()
    {
        _text = string.Empty;
        _cursorPosition = 0;
        ClearSelection();
        _historyIndex = -1;
        _temporaryInput = string.Empty;
        OnTextChanged?.Invoke(_text);
    }

    /// <summary>
    /// Submits the current input.
    /// </summary>
    public void Submit()
    {
        if (string.IsNullOrWhiteSpace(_text))
            return;

        // Add to history
        if (_history.Count == 0 || _history[^1] != _text)
        {
            _history.Add(_text);

            // Limit history size
            while (_history.Count > _maxHistory)
            {
                _history.RemoveAt(0);
            }
        }

        // Fire submit event
        OnSubmit?.Invoke(_text);

        // Clear input
        Clear();
    }

    /// <summary>
    /// Navigates to the previous command in history.
    /// </summary>
    public void HistoryPrevious()
    {
        if (_history.Count == 0)
            return;

        // Save current input if we're starting history navigation
        if (_historyIndex == -1)
        {
            _temporaryInput = _text;
            _historyIndex = _history.Count;
        }

        _historyIndex = Math.Max(0, _historyIndex - 1);
        SetText(_history[_historyIndex]);
        _cursorPosition = _text.Length;
    }

    /// <summary>
    /// Navigates to the next command in history.
    /// </summary>
    public void HistoryNext()
    {
        if (_historyIndex == -1)
            return;

        _historyIndex++;

        if (_historyIndex >= _history.Count)
        {
            // Restore temporary input
            SetText(_temporaryInput);
            _historyIndex = -1;
            _temporaryInput = string.Empty;
        }
        else
        {
            SetText(_history[_historyIndex]);
        }

        _cursorPosition = _text.Length;
    }

    /// <summary>
    /// Loads command history from a list.
    /// </summary>
    public void LoadHistory(List<string> history)
    {
        _history.Clear();
        _history.AddRange(history);
    }

    /// <summary>
    /// Gets the current command history.
    /// </summary>
    public List<string> GetHistory() => new List<string>(_history);

    /// <summary>
    /// Sets auto-completion suggestions.
    /// </summary>
    public void SetCompletions(List<string> completions)
    {
        _completionSuggestions = completions;
        _selectedSuggestionIndex = completions.Count > 0 ? 0 : -1;
        _showSuggestions = completions.Count > 0;
    }

    /// <summary>
    /// Accepts the currently selected completion suggestion.
    /// </summary>
    public void AcceptCompletion()
    {
        if (_selectedSuggestionIndex >= 0 && _selectedSuggestionIndex < _completionSuggestions.Count)
        {
            SetText(_completionSuggestions[_selectedSuggestionIndex]);
            _cursorPosition = _text.Length;
            _showSuggestions = false;
        }
    }

    /// <summary>
    /// Selects the next completion suggestion.
    /// </summary>
    public void NextCompletion()
    {
        if (_completionSuggestions.Count == 0)
            return;

        _selectedSuggestionIndex = (_selectedSuggestionIndex + 1) % _completionSuggestions.Count;
    }

    /// <summary>
    /// Selects the previous completion suggestion.
    /// </summary>
    public void PreviousCompletion()
    {
        if (_completionSuggestions.Count == 0)
            return;

        _selectedSuggestionIndex--;
        if (_selectedSuggestionIndex < 0)
            _selectedSuggestionIndex = _completionSuggestions.Count - 1;
    }

    private void ClearSelection()
    {
        _hasSelection = false;
        _selectionStart = 0;
        _selectionEnd = 0;
    }

    private void InsertText(string text)
    {
        if (_hasSelection)
        {
            // Replace selection
            _text = _text.Remove(SelectionStart, SelectionLength);
            _cursorPosition = SelectionStart;
            ClearSelection();
        }

        _text = _text.Insert(_cursorPosition, text);
        _cursorPosition += text.Length;
        _historyIndex = -1; // Reset history navigation
        OnTextChanged?.Invoke(_text);
    }

    private void DeleteBackward()
    {
        if (_hasSelection)
        {
            _text = _text.Remove(SelectionStart, SelectionLength);
            _cursorPosition = SelectionStart;
            ClearSelection();
        }
        else if (_cursorPosition > 0)
        {
            _text = _text.Remove(_cursorPosition - 1, 1);
            _cursorPosition--;
        }

        _historyIndex = -1;
        OnTextChanged?.Invoke(_text);
    }

    private void DeleteForward()
    {
        if (_hasSelection)
        {
            _text = _text.Remove(SelectionStart, SelectionLength);
            _cursorPosition = SelectionStart;
            ClearSelection();
        }
        else if (_cursorPosition < _text.Length)
        {
            _text = _text.Remove(_cursorPosition, 1);
        }

        _historyIndex = -1;
        OnTextChanged?.Invoke(_text);
    }

    protected override void OnRender(UIContext context)
    {
        // Handle mouse click for cursor positioning
        var input = context.Input;
        if (input.IsMouseButtonPressed(MouseButton.Left))
        {
            var mousePos = input.MousePosition;
            if (Rect.Contains(mousePos))
            {
                HandleMouseClick(mousePos, context.Renderer);
            }
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

        // Calculate text position
        var textPos = new Vector2(resolvedRect.X + Padding, resolvedRect.Y + Padding);

        // Draw prompt
        renderer.DrawText(Prompt, textPos, PromptColor);
        var promptWidth = renderer.MeasureText(Prompt).X;
        textPos.X += promptWidth;

        // Draw selection background
        if (_hasSelection)
        {
            var beforeSelection = _text.Substring(0, SelectionStart);
            var selectedText = _text.Substring(SelectionStart, SelectionLength);

            var beforeWidth = renderer.MeasureText(beforeSelection).X;
            var selectionWidth = renderer.MeasureText(selectedText).X;

            var selectionRect = new LayoutRect(
                textPos.X + beforeWidth,
                textPos.Y,
                selectionWidth,
                UITheme.Dark.LineHeight
            );
            renderer.DrawRectangle(selectionRect, SelectionColor);
        }

        // Draw text with syntax highlighting
        if (!string.IsNullOrEmpty(_text))
        {
            var segments = SyntaxHighlighter.Highlight(_text);
            float currentX = textPos.X;

            foreach (var segment in segments)
            {
                renderer.DrawText(segment.Text, new Vector2(currentX, textPos.Y), segment.Color);
                currentX += renderer.MeasureText(segment.Text).X;
            }
        }

        // Draw cursor (using base class IsFocused() method)
        if (IsFocused())
        {
            _cursorBlinkTimer += (float)context.Input.GameTime.ElapsedGameTime.TotalSeconds;
            if (_cursorBlinkTimer > CursorBlinkRate)
                _cursorBlinkTimer = 0;

            if (_cursorBlinkTimer < CursorBlinkRate / 2)
            {
                var textBeforeCursor = _text.Substring(0, _cursorPosition);
                var cursorX = textPos.X + renderer.MeasureText(textBeforeCursor).X;

                var cursorRect = new LayoutRect(
                    cursorX,
                    textPos.Y,
                    2,
                    UITheme.Dark.LineHeight
                );
                renderer.DrawRectangle(cursorRect, CursorColor);
            }
        }
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

    /// <summary>
    /// Handles mouse click to position cursor at the clicked character.
    /// Uses binary search for efficiency with long text.
    /// </summary>
    private void HandleMouseClick(Point mousePos, UIRenderer renderer)
    {
        if (string.IsNullOrEmpty(_text))
        {
            _cursorPosition = 0;
            ClearSelection();
            return;
        }

        // Calculate text start position (after padding and prompt)
        var promptWidth = renderer.MeasureText(Prompt).X;
        float textStartX = Rect.X + Padding + promptWidth;
        float relativeX = mousePos.X - textStartX;

        // Click before text start
        if (relativeX <= 0)
        {
            _cursorPosition = 0;
            ClearSelection();
            return;
        }

        // Click after text end
        float totalWidth = renderer.MeasureText(_text).X;
        if (relativeX >= totalWidth)
        {
            _cursorPosition = _text.Length;
            ClearSelection();
            return;
        }

        // Binary search to find character position
        int left = 0;
        int right = _text.Length;

        while (left < right)
        {
            int mid = (left + right) / 2;
            string substring = _text.Substring(0, mid);
            float widthAtMid = renderer.MeasureText(substring).X;

            if (widthAtMid < relativeX)
            {
                left = mid + 1;
            }
            else
            {
                right = mid;
            }
        }

        // Check if we should round to previous or next character
        if (left > 0)
        {
            string substringAtLeft = _text.Substring(0, left);
            string substringAtPrev = _text.Substring(0, left - 1);
            float widthAtLeft = renderer.MeasureText(substringAtLeft).X;
            float widthAtPrev = renderer.MeasureText(substringAtPrev).X;
            float midPoint = (widthAtPrev + widthAtLeft) / 2;

            if (relativeX < midPoint)
            {
                left--;
            }
        }

        _cursorPosition = left;
        ClearSelection();
    }

    protected override bool IsInteractive() => true;

    protected override void OnInput(InputEvent inputEvent)
    {
        var input = Context?.Input;
        if (input == null)
            return;

        // Handle focus on mouse RELEASE (standard click behavior)
        if (input.IsMouseButtonReleased(MouseButton.Left))
        {
            if (IsHovered())
            {
                Context?.SetFocus(Id);
            }
            else
            {
                Context?.ClearFocus();
            }
        }

        if (!IsFocused())
            return;

        // Handle keyboard input
        HandleKeyboardInput(input);
    }

    private void HandleKeyboardInput(InputState input)
    {
        // Enter - Submit (parent component handles suggestions)
        if (input.IsKeyPressed(Keys.Enter))
        {
            // In the new architecture, parent (ConsolePanel) handles suggestion acceptance
            // We just handle command submission
            if (!_multiLineMode || !input.IsShiftDown())
            {
                Submit();
            }
            else
            {
                InsertText("\n");
            }
            return;
        }

        // Escape (parent component handles suggestions)
        if (input.IsKeyPressed(Keys.Escape))
        {
            OnEscape?.Invoke();
            return;
        }

        // Up/Down arrows (parent handles suggestions, we handle history)
        if (input.IsKeyPressed(Keys.Up))
        {
            HistoryPrevious();
            return;
        }

        if (input.IsKeyPressed(Keys.Down))
        {
            HistoryNext();
            return;
        }

        // Tab - Auto-complete
        if (input.IsKeyPressed(Keys.Tab))
        {
            OnRequestCompletions?.Invoke(_text);
            return;
        }

        // Backspace - with repeat
        if (input.IsKeyPressedWithRepeat(Keys.Back))
        {
            DeleteBackward();
            return;
        }

        // Delete - with repeat
        if (input.IsKeyPressedWithRepeat(Keys.Delete))
        {
            DeleteForward();
            return;
        }

        // Left/Right arrows - with repeat
        if (input.IsKeyPressedWithRepeat(Keys.Left))
        {
            _cursorPosition = Math.Max(0, _cursorPosition - 1);
            ClearSelection();
            return;
        }

        if (input.IsKeyPressedWithRepeat(Keys.Right))
        {
            _cursorPosition = Math.Min(_text.Length, _cursorPosition + 1);
            ClearSelection();
            return;
        }

        // Home/End - with repeat
        if (input.IsKeyPressedWithRepeat(Keys.Home))
        {
            _cursorPosition = 0;
            ClearSelection();
            return;
        }

        if (input.IsKeyPressedWithRepeat(Keys.End))
        {
            _cursorPosition = _text.Length;
            ClearSelection();
            return;
        }

        // Ctrl+A - Select all
        if (input.IsCtrlDown() && input.IsKeyPressed(Keys.A))
        {
            _hasSelection = true;
            _selectionStart = 0;
            _selectionEnd = _text.Length;
            return;
        }

        // Ctrl+C - Copy
        if (input.IsCtrlDown() && input.IsKeyPressed(Keys.C))
        {
            if (_hasSelection)
            {
                // Would need clipboard access
                // SDL2.SDL.SDL_SetClipboardText(SelectedText);
            }
            return;
        }

        // Ctrl+V - Paste
        if (input.IsCtrlDown() && input.IsKeyPressed(Keys.V))
        {
            // Would need clipboard access
            // var clipboardText = SDL2.SDL.SDL_GetClipboardText();
            // InsertText(clipboardText);
            return;
        }

        // Regular character input - with repeat for smooth typing when held
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

        // Numbers
        if (key >= Keys.D0 && key <= Keys.D9)
        {
            if (shift)
            {
                return key switch
                {
                    Keys.D1 => '!',
                    Keys.D2 => '@',
                    Keys.D3 => '#',
                    Keys.D4 => '$',
                    Keys.D5 => '%',
                    Keys.D6 => '^',
                    Keys.D7 => '&',
                    Keys.D8 => '*',
                    Keys.D9 => '(',
                    Keys.D0 => ')',
                    _ => null
                };
            }
            return (char)('0' + (key - Keys.D0));
        }

        // Numpad
        if (key >= Keys.NumPad0 && key <= Keys.NumPad9)
        {
            return (char)('0' + (key - Keys.NumPad0));
        }

        // Special keys
        return key switch
        {
            Keys.Space => ' ',
            Keys.OemPeriod => shift ? '>' : '.',
            Keys.OemComma => shift ? '<' : ',',
            Keys.OemQuestion => shift ? '?' : '/',
            Keys.OemSemicolon => shift ? ':' : ';',
            Keys.OemQuotes => shift ? '"' : '\'',
            Keys.OemOpenBrackets => shift ? '{' : '[',
            Keys.OemCloseBrackets => shift ? '}' : ']',
            Keys.OemPipe => shift ? '|' : '\\',
            Keys.OemPlus => shift ? '+' : '=',
            Keys.OemMinus => shift ? '_' : '-',
            Keys.OemTilde => shift ? '~' : '`',
            _ => null
        };
    }
}

