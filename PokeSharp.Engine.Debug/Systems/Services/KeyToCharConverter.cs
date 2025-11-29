using Microsoft.Xna.Framework.Input;

namespace PokeSharp.Engine.Debug.Systems.Services;

/// <summary>
///     Converts MonoGame Keys to characters for text input.
///     Handles shifted keys and special characters.
/// </summary>
public static class KeyToCharConverter
{
    /// <summary>
    ///     Converts a Keys value to its character representation.
    /// </summary>
    /// <param name="key">The key to convert.</param>
    /// <param name="isShiftPressed">Whether Shift is currently pressed.</param>
    /// <returns>The character representation, or null if the key doesn't produce a character.</returns>
    public static char? ToChar(Keys key, bool isShiftPressed)
    {
        // Letters
        if (key >= Keys.A && key <= Keys.Z)
        {
            var baseChar = (char)('a' + (key - Keys.A));
            return isShiftPressed ? char.ToUpper(baseChar) : baseChar;
        }

        // Numbers (top row)
        if (key >= Keys.D0 && key <= Keys.D9)
        {
            if (isShiftPressed)
                // Shifted number keys produce symbols
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
                    _ => null,
                };
            return (char)('0' + (key - Keys.D0));
        }

        // Numpad numbers
        if (key >= Keys.NumPad0 && key <= Keys.NumPad9)
            return (char)('0' + (key - Keys.NumPad0));

        // Special keys and symbols
        return key switch
        {
            Keys.Space => ' ',

            // Punctuation (unshifted)
            Keys.OemPeriod => isShiftPressed ? '>' : '.',
            Keys.OemComma => isShiftPressed ? '<' : ',',
            Keys.OemQuestion => isShiftPressed ? '?' : '/',
            Keys.OemSemicolon => isShiftPressed ? ':' : ';',
            Keys.OemQuotes => isShiftPressed ? '"' : '\'',
            Keys.OemOpenBrackets => isShiftPressed ? '{' : '[',
            Keys.OemCloseBrackets => isShiftPressed ? '}' : ']',
            Keys.OemPipe => isShiftPressed ? '|' : '\\',
            Keys.OemMinus => isShiftPressed ? '_' : '-',
            Keys.OemPlus => isShiftPressed ? '+' : '=',
            Keys.OemTilde => isShiftPressed ? '~' : '`',

            // Numpad operators
            Keys.Add => '+',
            Keys.Subtract => '-',
            Keys.Multiply => '*',
            Keys.Divide => '/',
            Keys.Decimal => '.',

            // Not a printable character
            _ => null,
        };
    }
}
