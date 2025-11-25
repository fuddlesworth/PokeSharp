using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Input;
using PokeSharp.Engine.UI.Debug.Components.Base;
using PokeSharp.Engine.UI.Debug.Core;
using PokeSharp.Engine.UI.Debug.Input;
using PokeSharp.Engine.UI.Debug.Layout;

namespace PokeSharp.Engine.UI.Debug.Components.Controls;

/// <summary>
/// Search bar component for finding text in output.
/// Shows search input, match count, and navigation buttons.
/// </summary>
public class SearchBar : UIComponent
{
    private string _searchText = string.Empty;
    private int _cursorPosition = 0;
    private float _cursorBlinkTimer = 0;
    private static float CursorBlinkRate => UITheme.Dark.CursorBlinkRate;

    // Visual properties
    public Color BackgroundColor { get; set; } = UITheme.Dark.ConsoleSearchBackground;
    public Color TextColor { get; set; } = UITheme.Dark.InputText;
    public Color CursorColor { get; set; } = UITheme.Dark.InputCursor;
    public Color BorderColor { get; set; } = UITheme.Dark.BorderPrimary;
    public Color FocusBorderColor { get; set; } = UITheme.Dark.BorderFocus;
    public Color InfoColor { get; set; } = UITheme.Dark.TextSecondary;
    public float Padding { get; set; } = UITheme.Dark.PaddingMedium;
    public float BorderThickness { get; set; } = 1f;

    // Search state
    public int TotalMatches { get; set; } = 0;
    public int CurrentMatchIndex { get; set; } = 0;

    // Events
    public Action<string>? OnSearchTextChanged { get; set; }
    public Action? OnNextMatch { get; set; }
    public Action? OnPreviousMatch { get; set; }
    public Action? OnClose { get; set; }

    public SearchBar(string id)
    {
        Id = id;
    }

    public string SearchText => _searchText;

    public void SetSearchText(string text)
    {
        _searchText = text ?? string.Empty;
        _cursorPosition = Math.Clamp(_cursorPosition, 0, _searchText.Length);
    }

    public void Clear()
    {
        _searchText = string.Empty;
        _cursorPosition = 0;
        TotalMatches = 0;
        CurrentMatchIndex = 0;
    }

    protected override void OnRender(UIContext context)
    {
        // Don't render if height is 0 (hidden)
        if (Rect.Height <= 0)
            return;

        var renderer = Renderer;
        var resolvedRect = Rect;

        // Draw background
        renderer.DrawRectangle(resolvedRect, BackgroundColor);

        // Draw border (use focus color when focused)
        var borderColor = IsFocused() ? FocusBorderColor : BorderColor;
        DrawBorder(renderer, resolvedRect, borderColor);

        // Handle input if focused
        if (IsFocused())
        {
            HandleInput(context.Input);
        }

        // Calculate layout
        var contentX = resolvedRect.X + Padding;
        var contentY = resolvedRect.Y + Padding;
        var contentHeight = resolvedRect.Height - Padding * 2;

        // Draw label
        var labelText = "Find: ";
        renderer.DrawText(labelText, new Vector2(contentX, contentY), InfoColor);
        var labelWidth = renderer.MeasureText(labelText).X;

        // Draw search text
        var textX = contentX + labelWidth;
        if (!string.IsNullOrEmpty(_searchText))
        {
            renderer.DrawText(_searchText, new Vector2(textX, contentY), TextColor);
        }

        // Draw cursor if focused
        if (IsFocused())
        {
            _cursorBlinkTimer += (float)context.Input.GameTime.ElapsedGameTime.TotalSeconds;
            if (_cursorBlinkTimer > CursorBlinkRate)
                _cursorBlinkTimer = 0;

            if (_cursorBlinkTimer < CursorBlinkRate / 2)
            {
                var textBeforeCursor = _searchText.Substring(0, _cursorPosition);
                var cursorX = textX + renderer.MeasureText(textBeforeCursor).X;
                var cursorRect = new LayoutRect(cursorX, contentY, 2, contentHeight);
                renderer.DrawRectangle(cursorRect, CursorColor);
            }
        }

        // Draw match count on the right
        if (TotalMatches > 0)
        {
            var matchText = $"{CurrentMatchIndex + 1}/{TotalMatches}";
            var matchWidth = renderer.MeasureText(matchText).X;
            var matchX = resolvedRect.Right - Padding - matchWidth;
            renderer.DrawText(matchText, new Vector2(matchX, contentY), InfoColor);
        }
        else if (!string.IsNullOrEmpty(_searchText))
        {
            var noMatchText = "No matches";
            var noMatchWidth = renderer.MeasureText(noMatchText).X;
            var noMatchX = resolvedRect.Right - Padding - noMatchWidth;
            renderer.DrawText(noMatchText, new Vector2(noMatchX, contentY), InfoColor);
        }
    }

    private void HandleInput(InputState input)
    {
        // Escape - Close search
        if (input.IsKeyPressed(Keys.Escape))
        {
            OnClose?.Invoke();
            input.ConsumeKey(Keys.Escape);
            return;
        }

        // Enter or F3 - Next match
        if (input.IsKeyPressed(Keys.Enter) || input.IsKeyPressed(Keys.F3))
        {
            if (input.IsShiftDown())
            {
                // Shift+Enter or Shift+F3 - Previous match
                OnPreviousMatch?.Invoke();
            }
            else
            {
                // Regular Enter or F3 - Next match
                OnNextMatch?.Invoke();
            }
            return;
        }

        // Backspace
        if (input.IsKeyPressedWithRepeat(Keys.Back))
        {
            if (_cursorPosition > 0)
            {
                _searchText = _searchText.Remove(_cursorPosition - 1, 1);
                _cursorPosition--;
                OnSearchTextChanged?.Invoke(_searchText);
            }
            return;
        }

        // Delete
        if (input.IsKeyPressedWithRepeat(Keys.Delete))
        {
            if (_cursorPosition < _searchText.Length)
            {
                _searchText = _searchText.Remove(_cursorPosition, 1);
                OnSearchTextChanged?.Invoke(_searchText);
            }
            return;
        }

        // Arrow keys
        if (input.IsKeyPressedWithRepeat(Keys.Left))
        {
            if (_cursorPosition > 0)
                _cursorPosition--;
            return;
        }

        if (input.IsKeyPressedWithRepeat(Keys.Right))
        {
            if (_cursorPosition < _searchText.Length)
                _cursorPosition++;
            return;
        }

        // Home/End
        if (input.IsKeyPressed(Keys.Home))
        {
            _cursorPosition = 0;
            return;
        }

        if (input.IsKeyPressed(Keys.End))
        {
            _cursorPosition = _searchText.Length;
            return;
        }

        // Character input
        foreach (var key in Enum.GetValues<Keys>())
        {
            if (input.IsKeyPressedWithRepeat(key))
            {
                var ch = KeyToChar(key, input.IsShiftDown());
                if (ch.HasValue)
                {
                    _searchText = _searchText.Insert(_cursorPosition, ch.Value.ToString());
                    _cursorPosition++;
                    OnSearchTextChanged?.Invoke(_searchText);
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

