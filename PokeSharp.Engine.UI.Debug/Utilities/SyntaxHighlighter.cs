using Microsoft.Xna.Framework;
using System.Text.RegularExpressions;

namespace PokeSharp.Engine.UI.Debug.Utilities;

/// <summary>
/// Segment of text with a specific color for syntax highlighting.
/// </summary>
public readonly record struct ColoredSegment(string Text, Color Color, int StartIndex, int Length);

/// <summary>
/// Reusable syntax highlighter for C# code.
/// Provides simple keyword-based highlighting with support for strings, comments, and basic syntax.
/// </summary>
public static class SyntaxHighlighter
{
    // C# Keywords
    private static readonly HashSet<string> Keywords = new(StringComparer.Ordinal)
    {
        "abstract", "as", "base", "bool", "break", "byte", "case", "catch", "char", "checked",
        "class", "const", "continue", "decimal", "default", "delegate", "do", "double", "else",
        "enum", "event", "explicit", "extern", "false", "finally", "fixed", "float", "for",
        "foreach", "goto", "if", "implicit", "in", "int", "interface", "internal", "is", "lock",
        "long", "namespace", "new", "null", "object", "operator", "out", "override", "params",
        "private", "protected", "public", "readonly", "ref", "return", "sbyte", "sealed",
        "short", "sizeof", "stackalloc", "static", "string", "struct", "switch", "this", "throw",
        "true", "try", "typeof", "uint", "ulong", "unchecked", "unsafe", "ushort", "using",
        "var", "virtual", "void", "volatile", "while", "async", "await", "yield"
    };

    // Type keywords (different color from regular keywords)
    private static readonly HashSet<string> TypeKeywords = new(StringComparer.Ordinal)
    {
        "bool", "byte", "char", "decimal", "double", "float", "int", "long", "object",
        "sbyte", "short", "string", "uint", "ulong", "ushort", "void"
    };

    // Color scheme
    public static readonly Color DefaultColor = Color.White;
    public static readonly Color KeywordColor = new Color(86, 156, 214); // Blue
    public static readonly Color TypeColor = new Color(78, 201, 176); // Cyan
    public static readonly Color StringColor = new Color(214, 157, 133); // Orange-ish
    public static readonly Color CommentColor = new Color(87, 166, 74); // Green
    public static readonly Color NumberColor = new Color(181, 206, 168); // Light green
    public static readonly Color OperatorColor = new Color(180, 180, 180); // Light gray

    /// <summary>
    /// Highlights C# code and returns colored segments.
    /// </summary>
    /// <param name="code">The C# code to highlight.</param>
    /// <returns>List of colored segments.</returns>
    public static List<ColoredSegment> Highlight(string code)
    {
        if (string.IsNullOrEmpty(code))
            return new List<ColoredSegment>();

        var segments = new List<ColoredSegment>();
        int position = 0;

        while (position < code.Length)
        {
            // Skip whitespace
            if (char.IsWhiteSpace(code[position]))
            {
                int start = position;
                while (position < code.Length && char.IsWhiteSpace(code[position]))
                    position++;

                segments.Add(new ColoredSegment(
                    code.Substring(start, position - start),
                    DefaultColor,
                    start,
                    position - start
                ));
                continue;
            }

            // Single-line comment
            if (position < code.Length - 1 && code[position] == '/' && code[position + 1] == '/')
            {
                int start = position;
                while (position < code.Length && code[position] != '\n')
                    position++;

                segments.Add(new ColoredSegment(
                    code.Substring(start, position - start),
                    CommentColor,
                    start,
                    position - start
                ));
                continue;
            }

            // Multi-line comment
            if (position < code.Length - 1 && code[position] == '/' && code[position + 1] == '*')
            {
                int start = position;
                position += 2;
                while (position < code.Length - 1)
                {
                    if (code[position] == '*' && code[position + 1] == '/')
                    {
                        position += 2;
                        break;
                    }
                    position++;
                }

                segments.Add(new ColoredSegment(
                    code.Substring(start, position - start),
                    CommentColor,
                    start,
                    position - start
                ));
                continue;
            }

            // String literals
            if (code[position] == '"')
            {
                int start = position;
                position++; // Skip opening quote

                while (position < code.Length)
                {
                    if (code[position] == '\\' && position < code.Length - 1)
                    {
                        position += 2; // Skip escape sequence
                        continue;
                    }
                    if (code[position] == '"')
                    {
                        position++; // Include closing quote
                        break;
                    }
                    position++;
                }

                segments.Add(new ColoredSegment(
                    code.Substring(start, position - start),
                    StringColor,
                    start,
                    position - start
                ));
                continue;
            }

            // Character literals
            if (code[position] == '\'')
            {
                int start = position;
                position++; // Skip opening quote

                while (position < code.Length)
                {
                    if (code[position] == '\\' && position < code.Length - 1)
                    {
                        position += 2; // Skip escape sequence
                        continue;
                    }
                    if (code[position] == '\'')
                    {
                        position++; // Include closing quote
                        break;
                    }
                    position++;
                }

                segments.Add(new ColoredSegment(
                    code.Substring(start, position - start),
                    StringColor,
                    start,
                    position - start
                ));
                continue;
            }

            // Numbers
            if (char.IsDigit(code[position]))
            {
                int start = position;
                while (position < code.Length && (char.IsDigit(code[position]) || code[position] == '.' || code[position] == 'f' || code[position] == 'd'))
                    position++;

                segments.Add(new ColoredSegment(
                    code.Substring(start, position - start),
                    NumberColor,
                    start,
                    position - start
                ));
                continue;
            }

            // Identifiers and keywords
            if (char.IsLetter(code[position]) || code[position] == '_')
            {
                int start = position;
                while (position < code.Length && (char.IsLetterOrDigit(code[position]) || code[position] == '_'))
                    position++;

                string word = code.Substring(start, position - start);
                Color color = DefaultColor;

                if (TypeKeywords.Contains(word))
                    color = TypeColor;
                else if (Keywords.Contains(word))
                    color = KeywordColor;

                segments.Add(new ColoredSegment(
                    word,
                    color,
                    start,
                    position - start
                ));
                continue;
            }

            // Operators and punctuation
            int opStart = position;
            position++;
            segments.Add(new ColoredSegment(
                code.Substring(opStart, 1),
                OperatorColor,
                opStart,
                1
            ));
        }

        return segments;
    }
}


