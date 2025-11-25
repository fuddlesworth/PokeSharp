using Microsoft.Xna.Framework;
using System.Text.RegularExpressions;
using PokeSharp.Engine.UI.Debug.Core;

namespace PokeSharp.Engine.Debug.Console.Features;

/// <summary>
/// Provides syntax highlighting for C# code in the console.
/// Uses UITheme for consistent coloring.
/// </summary>
public static partial class ConsoleSyntaxHighlighter
{
    // Theme reference
    private static UITheme Theme => UITheme.Dark;

    // Color scheme (delegated to UITheme)
    private static Color KeywordColor => Theme.SyntaxKeyword;
    private static Color StringColor => Theme.SyntaxString;
    private static Color InterpolationColor => Theme.SyntaxStringInterpolation;
    private static Color NumberColor => Theme.SyntaxNumber;
    private static Color CommentColor => Theme.SyntaxComment;
    private static Color TypeColor => Theme.SyntaxType;
    private static Color BuiltInTypeColor => Theme.SyntaxKeyword;  // Same as keywords
    private static Color MethodColor => Theme.SyntaxMethod;
    private static Color OperatorColor => Theme.SyntaxOperator;
    private static Color DefaultColor => Theme.SyntaxDefault;

    // C# keywords
    private static readonly HashSet<string> Keywords = new()
    {
        "abstract", "as", "base", "break", "case", "catch",
        "checked", "class", "const", "continue", "default",
        "delegate", "do", "else", "enum", "event", "explicit",
        "extern", "false", "finally", "fixed", "for", "foreach",
        "goto", "if", "implicit", "in", "interface", "internal", "is",
        "lock", "namespace", "new", "null", "operator", "out",
        "override", "params", "private", "protected", "public", "readonly", "ref",
        "return", "sealed", "sizeof", "stackalloc", "static",
        "struct", "switch", "this", "throw", "true", "try", "typeof",
        "unchecked", "unsafe", "using", "var",
        "virtual", "volatile", "while", "async", "await", "nameof",
        "when", "from", "select", "where", "orderby", "group", "join", "let",
        "into", "on", "equals", "by", "ascending", "descending"
    };

    // Built-in types (primitives)
    private static readonly HashSet<string> BuiltInTypes = new()
    {
        "bool", "byte", "sbyte", "char", "decimal", "double", "float",
        "int", "uint", "long", "ulong", "short", "ushort", "object", "string", "void"
    };

    // Operators
    private static readonly HashSet<string> Operators = new()
    {
        "+", "-", "*", "/", "%", "=", "==", "!=", "<", ">", "<=", ">=",
        "&&", "||", "!", "&", "|", "^", "~", "<<", ">>",
        "++", "--", "+=", "-=", "*=", "/=", "%=", "&=", "|=", "^=", "<<=", ">>=",
        "=>", "??", "?.", "??"
    };

    /// <summary>
    /// Represents a colored text segment.
    /// </summary>
    public record ColoredSegment(string Text, Color Color);

    /// <summary>
    /// Highlights C# code and returns colored segments.
    /// </summary>
    public static List<ColoredSegment> Highlight(string code)
    {
        if (string.IsNullOrWhiteSpace(code))
        {
            return new List<ColoredSegment>();
        }

        var segments = new List<ColoredSegment>();
        var position = 0;

        // First pass: identify interpolated strings, regular strings, and comments (they take precedence)
        var protectedRanges = new List<(int start, int end, Color color, bool isInterpolated)>();

        // Find interpolated strings first (they need special handling)
        foreach (Match match in InterpolatedStringRegex().Matches(code))
        {
            protectedRanges.Add((match.Index, match.Index + match.Length - 1, StringColor, true));
        }

        // Find regular strings (skip if inside interpolated string)
        foreach (Match match in StringRegex().Matches(code))
        {
            if (!IsWithinAnyRange(match.Index, protectedRanges))
            {
                protectedRanges.Add((match.Index, match.Index + match.Length - 1, StringColor, false));
            }
        }

        // Find comments (skip if inside strings)
        foreach (Match match in CommentRegex().Matches(code))
        {
            if (!IsWithinAnyRange(match.Index, protectedRanges))
            {
                protectedRanges.Add((match.Index, match.Index + match.Length - 1, CommentColor, false));
            }
        }

        // Sort ranges by start position
        protectedRanges.Sort((a, b) => a.start.CompareTo(b.start));

        // Second pass: tokenize the rest
        while (position < code.Length)
        {
            // Check if we're in a protected range (string or comment)
            var range = protectedRanges.FirstOrDefault(r => r.start <= position && position <= r.end);
            if (range != default)
            {
                // Extract the protected text
                var text = code.Substring(range.start, range.end - range.start + 1);

                // If it's an interpolated string, we need to highlight the interpolations
                if (range.isInterpolated)
                {
                    HighlightInterpolatedString(text, range.color, segments);
                }
                else
                {
                    segments.Add(new ColoredSegment(text, range.color));
                }

                position = range.end + 1;
                continue;
            }

            // Not in a protected range - process normally
            var ch = code[position];

            // Whitespace
            if (char.IsWhiteSpace(ch))
            {
                var start = position;
                while (position < code.Length && char.IsWhiteSpace(code[position]))
                {
                    position++;
                }
                segments.Add(new ColoredSegment(code.Substring(start, position - start), DefaultColor));
                continue;
            }

            // Numbers
            if (char.IsDigit(ch) || (ch == '.' && position + 1 < code.Length && char.IsDigit(code[position + 1])))
            {
                var start = position;
                while (position < code.Length &&
                       (char.IsLetterOrDigit(code[position]) || code[position] == '.' || code[position] == '_'))
                {
                    position++;
                }
                segments.Add(new ColoredSegment(code.Substring(start, position - start), NumberColor));
                continue;
            }

            // Identifiers and keywords
            if (char.IsLetter(ch) || ch == '_' || ch == '@')
            {
                var start = position;
                position++; // Skip first char

                while (position < code.Length && (char.IsLetterOrDigit(code[position]) || code[position] == '_'))
                {
                    position++;
                }

                var word = code.Substring(start, position - start);

                // Determine color based on token type
                Color color;
                if (Keywords.Contains(word))
                {
                    color = KeywordColor;
                }
                else if (BuiltInTypes.Contains(word))
                {
                    color = BuiltInTypeColor;
                }
                else
                {
                    // Check if it's a method call (next non-whitespace is '(')
                    var nextPos = position;
                    while (nextPos < code.Length && char.IsWhiteSpace(code[nextPos]))
                    {
                        nextPos++;
                    }

                    if (nextPos < code.Length && code[nextPos] == '(')
                    {
                        color = MethodColor;
                    }
                    // Check if it's a type (next non-whitespace is alphanumeric - likely a variable declaration)
                    else if (char.IsUpper(word[0]))
                    {
                        color = TypeColor;
                    }
                    else
                    {
                        color = DefaultColor;
                    }
                }

                segments.Add(new ColoredSegment(word, color));
                continue;
            }

            // Operators (multi-character first)
            var foundOp = false;
            for (int len = 3; len >= 1; len--)
            {
                if (position + len <= code.Length)
                {
                    var op = code.Substring(position, len);
                    if (Operators.Contains(op))
                    {
                        segments.Add(new ColoredSegment(op, OperatorColor));
                        position += len;
                        foundOp = true;
                        break;
                    }
                }
            }

            if (foundOp)
                continue;

            // Single character (punctuation, brackets, etc)
            segments.Add(new ColoredSegment(ch.ToString(), DefaultColor));
            position++;
        }

        return segments;
    }

    /// <summary>
    /// Highlights an interpolated string by coloring the interpolation expressions separately.
    /// </summary>
    private static void HighlightInterpolatedString(string text, Color stringColor, List<ColoredSegment> segments)
    {
        var position = 0;
        var nestLevel = 0;
        var inInterpolation = false;
        var interpolationStart = -1;

        // Start with $"
        var prefixEnd = text.IndexOf('"') + 1;
        segments.Add(new ColoredSegment(text.Substring(0, prefixEnd), stringColor));
        position = prefixEnd;

        while (position < text.Length - 1) // -1 to skip closing "
        {
            var ch = text[position];

            if (!inInterpolation)
            {
                // Check for interpolation start
                if (ch == '{' && position + 1 < text.Length && text[position + 1] != '{')
                {
                    // Flush string content up to here
                    if (position > prefixEnd)
                    {
                        segments.Add(new ColoredSegment(text.Substring(prefixEnd, position - prefixEnd), stringColor));
                    }

                    inInterpolation = true;
                    interpolationStart = position;
                    nestLevel = 1;
                    position++;
                    continue;
                }

                // Escaped {{ becomes {
                if (ch == '{' && position + 1 < text.Length && text[position + 1] == '{')
                {
                    position += 2;
                    continue;
                }

                position++;
            }
            else
            {
                // Track nested braces inside interpolation
                if (ch == '{')
                {
                    nestLevel++;
                }
                else if (ch == '}')
                {
                    nestLevel--;

                    if (nestLevel == 0)
                    {
                        // End of interpolation
                        var interpText = text.Substring(interpolationStart, position - interpolationStart + 1);
                        // Remove the { } and highlight the inner code
                        segments.Add(new ColoredSegment("{", InterpolationColor));

                        if (interpText.Length > 2)
                        {
                            var innerCode = interpText.Substring(1, interpText.Length - 2);
                            var innerSegments = Highlight(innerCode);
                            segments.AddRange(innerSegments);
                        }

                        segments.Add(new ColoredSegment("}", InterpolationColor));

                        inInterpolation = false;
                        prefixEnd = position + 1;
                        position++;
                        continue;
                    }
                }

                position++;
            }
        }

        // Flush remaining string content
        if (!inInterpolation && position > prefixEnd && prefixEnd < text.Length - 1)
        {
            segments.Add(new ColoredSegment(text.Substring(prefixEnd, text.Length - 1 - prefixEnd), stringColor));
        }

        // Add closing "
        if (text.Length > 0 && text[text.Length - 1] == '"')
        {
            segments.Add(new ColoredSegment("\"", stringColor));
        }
    }

    /// <summary>
    /// Checks if a position is within any of the protected ranges.
    /// </summary>
    private static bool IsWithinAnyRange(int position, List<(int start, int end, Color color, bool isInterpolated)> ranges)
    {
        return ranges.Any(r => r.start <= position && position <= r.end);
    }

    // Regex patterns (compiled for performance)
    [GeneratedRegex(@"\$""(?:[^""\\]|\\.|\{\{|\}\}|\{(?:[^}""\\]|\\.)*\})*""")]
    private static partial Regex InterpolatedStringRegex();

    [GeneratedRegex(@"""(?:[^""\\]|\\.)*""")]
    private static partial Regex StringRegex();

    [GeneratedRegex(@"//.*$", RegexOptions.Multiline)]
    private static partial Regex CommentRegex();
}
