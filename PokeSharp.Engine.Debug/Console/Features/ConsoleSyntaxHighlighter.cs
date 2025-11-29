using System.Text.RegularExpressions;
using Microsoft.Xna.Framework;

namespace PokeSharp.Engine.Debug.Console.Features;

/// <summary>
///     Provides syntax highlighting for C# code in the console.
/// </summary>
public static partial class ConsoleSyntaxHighlighter
{
    // Color scheme
    private static readonly Color KeywordColor = new(86, 156, 214); // Blue
    private static readonly Color StringColor = new(206, 145, 120); // Orange/Brown
    private static readonly Color InterpolationColor = new(255, 220, 100); // Bright Yellow
    private static readonly Color NumberColor = new(181, 206, 168); // Light Green
    private static readonly Color CommentColor = new(106, 153, 85); // Dark Green
    private static readonly Color TypeColor = new(78, 201, 176); // Cyan
    private static readonly Color BuiltInTypeColor = new(86, 156, 214); // Blue (same as keywords)
    private static readonly Color MethodColor = new(220, 220, 170); // Yellow
    private static readonly Color OperatorColor = new(180, 180, 180); // Light Gray
    private static readonly Color DefaultColor = Color.White;

    // C# keywords
    private static readonly HashSet<string> Keywords = new()
    {
        "abstract",
        "as",
        "base",
        "break",
        "case",
        "catch",
        "checked",
        "class",
        "const",
        "continue",
        "default",
        "delegate",
        "do",
        "else",
        "enum",
        "event",
        "explicit",
        "extern",
        "false",
        "finally",
        "fixed",
        "for",
        "foreach",
        "goto",
        "if",
        "implicit",
        "in",
        "interface",
        "internal",
        "is",
        "lock",
        "namespace",
        "new",
        "null",
        "operator",
        "out",
        "override",
        "params",
        "private",
        "protected",
        "public",
        "readonly",
        "ref",
        "return",
        "sealed",
        "sizeof",
        "stackalloc",
        "static",
        "struct",
        "switch",
        "this",
        "throw",
        "true",
        "try",
        "typeof",
        "unchecked",
        "unsafe",
        "using",
        "var",
        "virtual",
        "volatile",
        "while",
        "async",
        "await",
        "nameof",
        "when",
        "from",
        "select",
        "where",
        "orderby",
        "group",
        "join",
        "let",
        "into",
        "on",
        "equals",
        "by",
        "ascending",
        "descending",
    };

    // Built-in types (primitives)
    private static readonly HashSet<string> BuiltInTypes = new()
    {
        "bool",
        "byte",
        "sbyte",
        "char",
        "decimal",
        "double",
        "float",
        "int",
        "uint",
        "long",
        "ulong",
        "short",
        "ushort",
        "object",
        "string",
        "void",
    };

    // Operators
    private static readonly HashSet<string> Operators = new()
    {
        "+",
        "-",
        "*",
        "/",
        "%",
        "=",
        "==",
        "!=",
        "<",
        ">",
        "<=",
        ">=",
        "&&",
        "||",
        "!",
        "&",
        "|",
        "^",
        "~",
        "<<",
        ">>",
        "++",
        "--",
        "+=",
        "-=",
        "*=",
        "/=",
        "%=",
        "&=",
        "|=",
        "^=",
        "<<=",
        ">>=",
        "=>",
        "??",
        "?.",
        "??",
    };

    /// <summary>
    ///     Highlights C# code and returns colored segments.
    /// </summary>
    public static List<ColoredSegment> Highlight(string code)
    {
        if (string.IsNullOrWhiteSpace(code))
            return new List<ColoredSegment>();

        var segments = new List<ColoredSegment>();
        var position = 0;

        // First pass: identify interpolated strings, regular strings, and comments (they take precedence)
        var protectedRanges = new List<(int start, int end, Color color, bool isInterpolated)>();

        // Find interpolated strings first (they need special handling)
        foreach (Match match in InterpolatedStringRegex().Matches(code))
            protectedRanges.Add((match.Index, match.Index + match.Length, StringColor, true));

        // Find regular strings (both " and @" styles)
        foreach (Match match in StringRegex().Matches(code))
        {
            // Make sure it's not already covered by an interpolated string
            var overlaps = protectedRanges.Any(r => match.Index >= r.start && match.Index < r.end);
            if (!overlaps)
                protectedRanges.Add((match.Index, match.Index + match.Length, StringColor, false));
        }

        // Find comments (// and /* */ styles)
        foreach (Match match in CommentRegex().Matches(code))
            protectedRanges.Add((match.Index, match.Index + match.Length, CommentColor, false));

        // Sort ranges by start position
        protectedRanges = protectedRanges.OrderBy(r => r.start).ToList();

        while (position < code.Length)
        {
            // Check if we're in a protected range (string or comment)
            var protectedRange = protectedRanges.FirstOrDefault(r =>
                position >= r.start && position < r.end
            );
            if (protectedRange != default)
            {
                if (protectedRange.isInterpolated)
                {
                    // Handle interpolated string with special highlighting for {} parts
                    HighlightInterpolatedString(
                        code,
                        protectedRange.start,
                        protectedRange.end,
                        segments
                    );
                    position = protectedRange.end;
                }
                else
                {
                    var text = code.Substring(
                        protectedRange.start,
                        protectedRange.end - protectedRange.start
                    );
                    segments.Add(new ColoredSegment(text, protectedRange.color));
                    position = protectedRange.end;
                }

                continue;
            }

            // Check for numbers
            var numberMatch = NumberRegex().Match(code, position);
            if (numberMatch.Success && numberMatch.Index == position)
            {
                segments.Add(new ColoredSegment(numberMatch.Value, NumberColor));
                position += numberMatch.Length;
                continue;
            }

            // Check for multi-character operators first
            var operatorFound = false;
            foreach (var op in Operators.OrderByDescending(o => o.Length))
                if (
                    position + op.Length <= code.Length
                    && code.Substring(position, op.Length) == op
                )
                {
                    segments.Add(new ColoredSegment(op, OperatorColor));
                    position += op.Length;
                    operatorFound = true;
                    break;
                }

            if (operatorFound)
                continue;

            // Check for identifiers (keywords, types, methods)
            var identifierMatch = IdentifierRegex().Match(code, position);
            if (identifierMatch.Success && identifierMatch.Index == position)
            {
                var word = identifierMatch.Value;
                Color color;

                // Check if it's a keyword first
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
                    // Check if followed by '(' for method calls (before checking uppercase)
                    var nextNonWhitespace = position + word.Length;
                    while (
                        nextNonWhitespace < code.Length
                        && char.IsWhiteSpace(code[nextNonWhitespace])
                    )
                        nextNonWhitespace++;

                    if (nextNonWhitespace < code.Length && code[nextNonWhitespace] == '(')
                        color = MethodColor;
                    else if (char.IsUpper(word[0])) // Type names typically start with uppercase
                        color = TypeColor;
                    else
                        color = DefaultColor;
                }

                segments.Add(new ColoredSegment(word, color));
                position += word.Length;
                continue;
            }

            // Any other character (punctuation, whitespace, single-char operators)
            segments.Add(new ColoredSegment(code[position].ToString(), DefaultColor));
            position++;
        }

        return segments;
    }

    /// <summary>
    ///     Highlights an interpolated string with special colors for the interpolation parts.
    /// </summary>
    private static void HighlightInterpolatedString(
        string code,
        int start,
        int end,
        List<ColoredSegment> segments
    )
    {
        var position = start;

        // Add the $ prefix
        if (code[position] == '$')
        {
            segments.Add(new ColoredSegment("$", InterpolationColor));
            position++;
        }

        // Add the opening quote
        if (position < end && code[position] == '"')
        {
            segments.Add(new ColoredSegment("\"", StringColor));
            position++;
        }

        while (position < end - 1) // -1 to exclude closing quote
            if (code[position] == '{')
            {
                // Find matching closing brace
                var braceDepth = 1;
                var interpolationStart = position;
                position++;

                while (position < end - 1 && braceDepth > 0)
                {
                    if (code[position] == '{')
                        braceDepth++;
                    else if (code[position] == '}')
                        braceDepth--;
                    position++;
                }

                // Highlight the interpolation part (including braces)
                var interpolation = code.Substring(
                    interpolationStart,
                    position - interpolationStart
                );
                segments.Add(new ColoredSegment(interpolation, InterpolationColor));
            }
            else
            {
                // Regular string content
                var textStart = position;
                while (position < end - 1 && code[position] != '{')
                    position++;

                var text = code.Substring(textStart, position - textStart);
                if (text.Length > 0)
                    segments.Add(new ColoredSegment(text, StringColor));
            }

        // Add the closing quote
        if (position < end && code[position] == '"')
            segments.Add(new ColoredSegment("\"", StringColor));
    }

    // Regex patterns
    [GeneratedRegex(@"\$""(?:[^""\\{]|\\.|\{\{|\}\}|{[^}]*})*""")]
    private static partial Regex InterpolatedStringRegex();

    [GeneratedRegex(@"@?""(?:[^""\\]|\\.)*""")]
    private static partial Regex StringRegex();

    [GeneratedRegex(@"//.*$|/\*.*?\*/", RegexOptions.Multiline | RegexOptions.Singleline)]
    private static partial Regex CommentRegex();

    [GeneratedRegex(@"\b\d+\.?\d*[fFdDmMlLuU]?\b")]
    private static partial Regex NumberRegex();

    [GeneratedRegex(@"\b[a-zA-Z_][a-zA-Z0-9_]*\b")]
    private static partial Regex IdentifierRegex();

    /// <summary>
    ///     Represents a colored text segment.
    /// </summary>
    public record ColoredSegment(string Text, Color Color);
}
