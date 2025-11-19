using Microsoft.Xna.Framework;

namespace PokeSharp.Engine.Debug.Console.UI;

/// <summary>
/// Types of output sections.
/// </summary>
public enum SectionType
{
    Command,        // Command execution block
    Error,          // Error messages with stack traces
    Category,       // Grouped by log category
    Manual,         // User-defined sections
    Search          // Search result sections
}

/// <summary>
/// Represents a foldable section of console output.
/// </summary>
public class OutputSection
{
    /// <summary>
    /// Display header for the section.
    /// </summary>
    public string Header { get; init; } = string.Empty;

    /// <summary>
    /// Starting line index in the output buffer (inclusive).
    /// </summary>
    public int StartLine { get; init; }

    /// <summary>
    /// Ending line index in the output buffer (inclusive).
    /// </summary>
    public int EndLine { get; set; }

    /// <summary>
    /// Whether the section is currently folded.
    /// </summary>
    public bool IsFolded { get; set; }

    /// <summary>
    /// Type of section.
    /// </summary>
    public SectionType Type { get; init; }

    /// <summary>
    /// Color for the section header.
    /// </summary>
    public Color HeaderColor { get; init; } = Color.LightGray;

    /// <summary>
    /// Unique identifier for the section.
    /// </summary>
    public string Id { get; init; } = Guid.NewGuid().ToString();

    /// <summary>
    /// Gets the number of lines in this section.
    /// </summary>
    public int LineCount => Math.Max(0, EndLine - StartLine + 1);

    /// <summary>
    /// Gets the number of non-header lines in this section.
    /// </summary>
    public int ContentLineCount => Math.Max(0, LineCount - 1);
}

