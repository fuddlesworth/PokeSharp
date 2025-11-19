using Microsoft.Xna.Framework;

namespace PokeSharp.Engine.Debug.Console.Configuration;

/// <summary>
/// Centralized color palette for the debug console UI.
/// Provides semantic color definitions for consistent theming across all console components.
/// Theme: "Dark Blue Console" - Professional, cohesive, and accessible.
/// </summary>
public static class ConsoleColors
{
    // ═══════════════════════════════════════════════════════════════════════
    // BASE COLORS - Backgrounds and Overlays
    // ═══════════════════════════════════════════════════════════════════════

    /// <summary>
    /// Main console background (semi-transparent black).
    /// Used for: Primary console window background.
    /// </summary>
    public static readonly Color Background_Primary = new(0, 0, 0, 230);

    /// <summary>
    /// Secondary background (dark gray).
    /// Used for: Input area, elevated panels.
    /// </summary>
    public static readonly Color Background_Secondary = new(30, 30, 30, 255);

    /// <summary>
    /// Elevated background (lighter dark gray, semi-transparent).
    /// Used for: Auto-complete, parameter hints, floating panels.
    /// </summary>
    public static readonly Color Background_Elevated = new(40, 40, 40, 240);

    /// <summary>
    /// Modal overlay background (semi-transparent black).
    /// Used for: Documentation popup overlay.
    /// </summary>
    public static readonly Color Background_Overlay = new(0, 0, 0, 180);

    // ═══════════════════════════════════════════════════════════════════════
    // TEXT COLORS - Hierarchical text colors
    // ═══════════════════════════════════════════════════════════════════════

    /// <summary>
    /// Primary text color (white).
    /// Used for: Main content, user input, important text.
    /// </summary>
    public static readonly Color Text_Primary = Color.White;

    /// <summary>
    /// Secondary text color (light gray).
    /// Used for: Helper text, descriptions, instructions.
    /// </summary>
    public static readonly Color Text_Secondary = Color.LightGray;

    /// <summary>
    /// Tertiary text color (medium gray).
    /// Used for: Dimmed text, less important information.
    /// </summary>
    public static readonly Color Text_Tertiary = new(128, 128, 128);

    /// <summary>
    /// Disabled text color (dark gray).
    /// Used for: Disabled or inactive elements.
    /// </summary>
    public static readonly Color Text_Disabled = new(100, 100, 100);

    // ═══════════════════════════════════════════════════════════════════════
    // SEMANTIC COLORS - Brand and State Colors
    // ═══════════════════════════════════════════════════════════════════════

    #region Primary (Blue - Brand/Theme Color)

    /// <summary>
    /// Primary brand color (bright blue).
    /// Used for: Prompt (>), cursor, command echo, borders, interactive elements.
    /// This is the main accent color of the console theme.
    /// </summary>
    public static readonly Color Primary = new(100, 200, 255);

    /// <summary>
    /// Dimmed primary color (lighter blue).
    /// Used for: Secondary highlights, line numbers (current), labels.
    /// </summary>
    public static readonly Color Primary_Dim = new(150, 200, 255);

    /// <summary>
    /// Bright primary color (brightest blue).
    /// Used for: Hover states, active selections.
    /// </summary>
    public static readonly Color Primary_Bright = new(120, 220, 255);

    #endregion

    #region Success (Green)

    /// <summary>
    /// Success color (bright green).
    /// Used for: Successful command execution, positive feedback, current parameter.
    /// </summary>
    public static readonly Color Success = new(150, 255, 150);

    /// <summary>
    /// Bright success color (very bright green).
    /// Used for: Strong success indicators, matching brackets, feature highlights.
    /// </summary>
    public static readonly Color Success_Bright = new(100, 255, 100);

    /// <summary>
    /// Dimmed success color (subtle green with transparency).
    /// Used for: Multi-line indicator, subtle success hints.
    /// </summary>
    public static readonly Color Success_Dim = new(150, 200, 150, 200);

    #endregion

    #region Warning (Orange)

    /// <summary>
    /// Warning color (orange).
    /// Used for: Warnings, important info, bookmarked commands, reverse-i-search prompt.
    /// </summary>
    public static readonly Color Warning = new(255, 200, 100);

    /// <summary>
    /// Bright warning color (bright orange-yellow).
    /// Used for: Strong warnings, search match highlights, scroll indicators.
    /// </summary>
    public static readonly Color Warning_Bright = new(255, 220, 100);

    /// <summary>
    /// Dimmed warning color (subtle orange).
    /// Used for: Subtle warnings, dimmed indicators.
    /// </summary>
    public static readonly Color Warning_Dim = new(200, 150, 100);

    #endregion

    #region Error (Red)

    /// <summary>
    /// Error color (red).
    /// Used for: Error messages, failed operations, mismatched brackets.
    /// </summary>
    public static readonly Color Error = new(255, 100, 100);

    /// <summary>
    /// Bright error color (brighter red).
    /// Used for: Critical errors, strong negative feedback.
    /// </summary>
    public static readonly Color Error_Bright = new(255, 120, 120);

    /// <summary>
    /// Dimmed error color (subtle red).
    /// Used for: Subtle error indicators, error hints.
    /// </summary>
    public static readonly Color Error_Dim = new(200, 100, 100);

    #endregion

    #region Info (Yellow)

    /// <summary>
    /// Info color (bright yellow).
    /// Used for: Information messages, titles, highlights.
    /// </summary>
    public static readonly Color Info = new(255, 255, 100);

    /// <summary>
    /// Dimmed info color (subtle yellow).
    /// Used for: Dimmed information, scroll indicators, overload counts.
    /// </summary>
    public static readonly Color Info_Dim = new(200, 200, 100);

    #endregion

    // ═══════════════════════════════════════════════════════════════════════
    // UI COMPONENT COLORS - Specific UI Elements
    // ═══════════════════════════════════════════════════════════════════════

    #region Borders & Separators

    /// <summary>
    /// Default border color (primary blue).
    /// Used for: Panel borders, documentation borders, parameter hint borders.
    /// </summary>
    public static readonly Color Border_Default = new(100, 200, 255);

    /// <summary>
    /// Subtle border color (dark gray).
    /// Used for: Dividers, subtle separators.
    /// </summary>
    public static readonly Color Border_Subtle = new(60, 60, 60);

    #endregion

    #region Interactive Elements

    /// <summary>
    /// Selection background color (semi-transparent steel blue).
    /// Used for: Text selection highlight.
    /// </summary>
    public static readonly Color Selection_Background = new(70, 130, 180, 150);

    /// <summary>
    /// Selection highlight color (semi-transparent primary).
    /// Used for: Highlighted items, active selections.
    /// </summary>
    public static readonly Color Selection_Highlight = new(100, 200, 255, 120);

    /// <summary>
    /// Cursor color (primary blue).
    /// Used for: Text input cursor.
    /// </summary>
    public static readonly Color Cursor = new(100, 200, 255);

    /// <summary>
    /// Prompt color (primary blue).
    /// Used for: Console prompt (>) symbol.
    /// </summary>
    public static readonly Color Prompt = new(100, 200, 255);

    #endregion

    #region Scroll Elements

    /// <summary>
    /// Scrollbar track background (dark gray, semi-transparent).
    /// Used for: Scrollbar background track.
    /// </summary>
    public static readonly Color Scroll_Track = new(40, 40, 40, 200);

    /// <summary>
    /// Scrollbar thumb color (medium gray).
    /// Used for: Scrollbar thumb (inactive state).
    /// </summary>
    public static readonly Color Scroll_Thumb = new(150, 150, 150, 255);

    /// <summary>
    /// Active scrollbar thumb color (primary blue).
    /// Used for: Scrollbar thumb when at top/bottom.
    /// </summary>
    public static readonly Color Scroll_Thumb_Active = new(100, 200, 255, 255);

    /// <summary>
    /// Scroll position indicator color (warning orange).
    /// Used for: Triangle indicators showing more content above/below.
    /// </summary>
    public static readonly Color Scroll_Indicator = new(255, 200, 100);

    #endregion

    #region Line Numbers

    /// <summary>
    /// Dimmed line number color (gray).
    /// Used for: Line numbers in multi-line input (inactive).
    /// </summary>
    public static readonly Color LineNumber_Dim = new(100, 100, 120);

    /// <summary>
    /// Current line number color (primary dim).
    /// Used for: Line number of current line in multi-line input.
    /// </summary>
    public static readonly Color LineNumber_Current = new(150, 200, 255);

    #endregion

    #region Bracket Matching

    /// <summary>
    /// Matched bracket highlight (semi-transparent bright green).
    /// Used for: Highlighting matched brackets/parentheses.
    /// </summary>
    public static readonly Color Bracket_Match = new(100, 255, 100, 150);

    /// <summary>
    /// Unmatched bracket highlight (semi-transparent red).
    /// Used for: Highlighting unmatched brackets/parentheses.
    /// </summary>
    public static readonly Color Bracket_Mismatch = new(255, 100, 100, 150);

    #endregion

    #region Auto-Complete

    /// <summary>
    /// Auto-complete selected item color (primary blue).
    /// Used for: Currently selected auto-complete suggestion.
    /// </summary>
    public static readonly Color AutoComplete_Selected = new(100, 200, 255);

    /// <summary>
    /// Auto-complete unselected item color (light gray).
    /// Used for: Unselected auto-complete suggestions.
    /// </summary>
    public static readonly Color AutoComplete_Unselected = Color.LightGray;

    /// <summary>
    /// Auto-complete hovered item color (lighter blue).
    /// Used for: Auto-complete suggestion under mouse cursor (hover state).
    /// </summary>
    public static readonly Color AutoComplete_Hover = new(150, 210, 255);

    /// <summary>
    /// Auto-complete description color (medium gray).
    /// Used for: Description text in auto-complete suggestions.
    /// </summary>
    public static readonly Color AutoComplete_Description = new(128, 128, 128);

    /// <summary>
    /// Auto-complete loading text color (medium gray).
    /// Used for: "Loading suggestions..." message.
    /// </summary>
    public static readonly Color AutoComplete_Loading = new(150, 150, 150);

    #endregion

    #region Search

    /// <summary>
    /// Current search match highlight (semi-transparent orange-yellow).
    /// Used for: Currently selected search match.
    /// </summary>
    public static readonly Color Search_CurrentMatch = new(255, 200, 0, 180);

    /// <summary>
    /// Other search matches highlight (semi-transparent light blue).
    /// Used for: Other search matches (not current).
    /// </summary>
    public static readonly Color Search_OtherMatches = new(100, 150, 255, 120);

    /// <summary>
    /// Search prompt color (yellow).
    /// Used for: Search mode prompt text.
    /// </summary>
    public static readonly Color Search_Prompt = Color.Yellow;

    /// <summary>
    /// Reverse-i-search prompt color (orange).
    /// Used for: Reverse-i-search prompt text.
    /// </summary>
    public static readonly Color ReverseSearch_Prompt = new(255, 200, 100);

    /// <summary>
    /// Search success indicator (success green).
    /// Used for: Indicator when search has matches.
    /// </summary>
    public static readonly Color Search_Success = new(150, 255, 150);

    /// <summary>
    /// Search disabled indicator (medium gray).
    /// Used for: Indicator when search has no matches.
    /// </summary>
    public static readonly Color Search_Disabled = new(150, 150, 150);

    /// <summary>
    /// Reverse-i-search match highlight (bright yellow).
    /// Used for: Highlighted match in reverse-i-search preview.
    /// </summary>
    public static readonly Color ReverseSearch_MatchHighlight = new(255, 255, 100);

    #endregion

    #region Output Colors

    /// <summary>
    /// Default output text color (white).
    /// Used for: Normal console output.
    /// </summary>
    public static readonly Color Output_Default = Color.White;

    /// <summary>
    /// Success output color (bright green).
    /// Used for: Success messages, positive results.
    /// </summary>
    public static readonly Color Output_Success = new(150, 255, 150);

    /// <summary>
    /// Info output color (light green).
    /// Used for: Informational messages.
    /// </summary>
    public static readonly Color Output_Info = new(200, 255, 200);

    /// <summary>
    /// Warning output color (orange).
    /// Used for: Warning messages, important notices.
    /// </summary>
    public static readonly Color Output_Warning = Color.Orange;

    /// <summary>
    /// Error output color (red).
    /// Used for: Error messages, failures.
    /// </summary>
    public static readonly Color Output_Error = new(255, 100, 100);

    /// <summary>
    /// Command echo color (primary blue).
    /// Used for: Echoing user commands in output.
    /// </summary>
    public static readonly Color Output_Command = new(100, 200, 255);

    /// <summary>
    /// Alias expansion color (medium gray).
    /// Used for: Showing expanded alias commands.
    /// </summary>
    public static readonly Color Output_AliasExpansion = new(150, 150, 150);

    #endregion

    #region Help & Documentation

    /// <summary>
    /// Help title color (bright yellow).
    /// Used for: Section titles in help text.
    /// </summary>
    public static readonly Color Help_Title = new(255, 255, 100);

    /// <summary>
    /// Help text color (light gray).
    /// Used for: Body text in help messages.
    /// </summary>
    public static readonly Color Help_Text = Color.LightGray;

    /// <summary>
    /// Help section header color (primary blue).
    /// Used for: Section headers in help text.
    /// </summary>
    public static readonly Color Help_SectionHeader = new(100, 200, 255);

    /// <summary>
    /// Help example color (success green).
    /// Used for: Code examples in help text.
    /// </summary>
    public static readonly Color Help_Example = new(150, 255, 150);

    /// <summary>
    /// Documentation type info color (orange).
    /// Used for: Type information in documentation popup.
    /// </summary>
    public static readonly Color Documentation_TypeInfo = new(255, 200, 100);

    /// <summary>
    /// Documentation label color (primary dim).
    /// Used for: Labels in documentation popup.
    /// </summary>
    public static readonly Color Documentation_Label = new(150, 200, 255);

    /// <summary>
    /// Documentation instruction color (medium gray).
    /// Used for: Instructions in documentation popup (e.g., "Press ESC to close").
    /// </summary>
    public static readonly Color Documentation_Instruction = new(150, 150, 150);

    #endregion

    #region Section Folding

    /// <summary>
    /// Section fold box background (black).
    /// Used for: Background of section fold toggle boxes.
    /// </summary>
    public static readonly Color SectionFold_Background = new(0, 0, 0, 255);

    /// <summary>
    /// Hover color for section fold toggle boxes (lighter).
    /// Used for: Visual feedback when hovering over section headers.
    /// </summary>
    public static readonly Color SectionFold_Hover = new(80, 80, 90, 255);

    /// <summary>
    /// Outline color for hovered section fold toggle boxes.
    /// Used for: Visual outline when hovering over section headers.
    /// </summary>
    public static readonly Color SectionFold_HoverOutline = new(120, 120, 140, 255);

    #endregion

    // ═══════════════════════════════════════════════════════════════════════
    // SYNTAX HIGHLIGHTING COLORS
    // Note: These are kept from the original ConsoleSyntaxHighlighter.cs
    // They form a well-designed, cohesive color scheme and should not be changed.
    // ═══════════════════════════════════════════════════════════════════════

    /// <summary>
    /// C# keyword color (blue).
    /// Used for: Language keywords (if, for, class, etc.).
    /// </summary>
    public static readonly Color Syntax_Keyword = new(86, 156, 214);

    /// <summary>
    /// String literal color (orange-brown).
    /// Used for: String values.
    /// </summary>
    public static readonly Color Syntax_String = new(206, 145, 120);

    /// <summary>
    /// String interpolation color (bright yellow).
    /// Used for: String interpolation expressions.
    /// </summary>
    public static readonly Color Syntax_StringInterpolation = new(255, 220, 100);

    /// <summary>
    /// Number literal color (light green).
    /// Used for: Numeric values.
    /// </summary>
    public static readonly Color Syntax_Number = new(181, 206, 168);

    /// <summary>
    /// Comment color (dark green).
    /// Used for: Code comments.
    /// </summary>
    public static readonly Color Syntax_Comment = new(106, 153, 85);

    /// <summary>
    /// Type name color (cyan).
    /// Used for: Custom type names, class names.
    /// </summary>
    public static readonly Color Syntax_Type = new(78, 201, 176);

    /// <summary>
    /// Method name color (cream/yellow).
    /// Used for: Method and function names.
    /// </summary>
    public static readonly Color Syntax_Method = new(220, 220, 170);

    /// <summary>
    /// Operator color (light gray).
    /// Used for: Operators (+, -, *, /, etc.).
    /// </summary>
    public static readonly Color Syntax_Operator = new(180, 180, 180);

    /// <summary>
    /// Default syntax color (white).
    /// Used for: Fallback for unhighlighted code.
    /// </summary>
    public static readonly Color Syntax_Default = Color.White;

    // --- Section Headers (for output sections with folding) ---

    /// <summary>
    /// Section header color for command sections (blue).
    /// Used for: Command execution output sections.
    /// </summary>
    public static readonly Color Section_Command = new(150, 200, 255);

    /// <summary>
    /// Section header color for error sections (red).
    /// Used for: Error and exception output sections.
    /// </summary>
    public static readonly Color Section_Error = new(255, 100, 100);

    /// <summary>
    /// Section header color for category sections (yellow-gray).
    /// Used for: Help category sections and groupings.
    /// </summary>
    public static readonly Color Section_Category = new(200, 200, 150);

    /// <summary>
    /// Section header color for manual sections (purple).
    /// Used for: Manual/documentation sections.
    /// </summary>
    public static readonly Color Section_Manual = new(200, 150, 255);

    /// <summary>
    /// Section header color for search result sections (bright yellow).
    /// Used for: Search result output sections.
    /// </summary>
    public static readonly Color Section_Search = new(255, 255, 100);
}

