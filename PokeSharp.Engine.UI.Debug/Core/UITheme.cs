using Microsoft.Xna.Framework;

namespace PokeSharp.Engine.UI.Debug.Core;

/// <summary>
/// Centralized theme system for debug UI.
/// Replaces scattered ConsoleConstants and ConsoleColors with a cohesive styling system.
/// </summary>
public class UITheme
{
    /// <summary>Default dark theme for debug UI</summary>
    public static UITheme Dark { get; } = new UITheme
    {
        // Background colors
        BackgroundPrimary = new Color(20, 20, 30, 240),
        BackgroundSecondary = new Color(30, 30, 45, 255),
        BackgroundElevated = new Color(40, 40, 60, 255),

        // Text colors
        TextPrimary = new Color(220, 220, 230),
        TextSecondary = new Color(160, 160, 180),
        TextDim = new Color(100, 100, 120),

        // Interactive element colors
        ButtonNormal = new Color(50, 50, 70),
        ButtonHover = new Color(60, 60, 90),
        ButtonPressed = new Color(40, 40, 60),
        ButtonText = new Color(220, 220, 230),

        // Input colors
        InputBackground = new Color(30, 30, 45),
        InputText = new Color(220, 220, 230),
        InputCursor = new Color(255, 255, 255),
        InputSelection = new Color(51, 153, 255, 80),

        // Border colors
        BorderPrimary = new Color(60, 60, 80),
        BorderFocus = new Color(100, 150, 255),

        // Status colors
        Success = new Color(80, 200, 120),
        SuccessDim = new Color(60, 150, 90),
        Warning = new Color(255, 200, 0),
        WarningDim = new Color(200, 150, 0),
        Error = new Color(220, 50, 50),
        ErrorDim = new Color(180, 40, 40),
        Info = new Color(100, 150, 255),
        InfoDim = new Color(80, 120, 200),

        // Special purpose colors
        Prompt = new Color(100, 200, 255),
        Highlight = new Color(255, 255, 100),
        ScrollbarTrack = new Color(40, 40, 60, 200),
        ScrollbarThumb = new Color(80, 80, 100, 200),
        ScrollbarThumbHover = new Color(100, 100, 120, 255),

        // Spacing (pixels)
        PaddingTiny = 2,
        PaddingSmall = 4,
        PaddingMedium = 8,
        PaddingLarge = 12,
        PaddingXLarge = 20,

        MarginTiny = 2,
        MarginSmall = 4,
        MarginMedium = 8,
        MarginLarge = 12,
        MarginXLarge = 20,

        // Sizes
        FontSize = 16,
        LineHeight = 20,
        ScrollbarWidth = 10,
        BorderWidth = 1,

        // Control sizes
        ButtonHeight = 30,
        InputHeight = 30,
        DropdownItemHeight = 25,

        // Animation
        AnimationSpeed = 1200f, // pixels per second
        FadeSpeed = 4f, // opacity change per second
        CursorBlinkRate = 0.5f, // seconds per blink cycle

        // ═══════════════════════════════════════════════════════════════
        // CONSOLE-SPECIFIC COLORS
        // ═══════════════════════════════════════════════════════════════

        // Console semantic colors (from ConsoleColors Primary)
        ConsolePrimary = new Color(100, 200, 255),
        ConsolePrimaryDim = new Color(150, 200, 255),
        ConsolePrimaryBright = new Color(120, 220, 255),
        ConsolePrompt = new Color(100, 200, 255),

        // Console output colors
        ConsoleOutputDefault = Color.White,
        ConsoleOutputSuccess = new Color(150, 255, 150),
        ConsoleOutputInfo = new Color(200, 255, 200),
        ConsoleOutputWarning = Color.Orange,
        ConsoleOutputError = new Color(255, 100, 100),
        ConsoleOutputCommand = new Color(100, 200, 255),
        ConsoleOutputAliasExpansion = new Color(150, 150, 150),

        // Console component backgrounds
        ConsoleBackground = new Color(20, 20, 30, 240),        // Panel background
        ConsoleOutputBackground = new Color(15, 15, 25, 255),  // Output buffer background
        ConsoleInputBackground = new Color(25, 25, 35, 255),   // Command editor background
        ConsoleSearchBackground = new Color(30, 30, 40, 255),  // Search bar background
        ConsoleHintText = new Color(100, 200, 100, 200),       // Hint bar text

        // Syntax highlighting (Visual Studio Code Dark+ theme colors)
        SyntaxKeyword = new Color(86, 156, 214),         // C# keywords
        SyntaxString = new Color(206, 145, 120),         // String literals
        SyntaxStringInterpolation = new Color(255, 220, 100), // String interpolation
        SyntaxNumber = new Color(181, 206, 168),         // Numbers
        SyntaxComment = new Color(106, 153, 85),         // Comments
        SyntaxType = new Color(78, 201, 176),            // Type names
        SyntaxMethod = new Color(220, 220, 170),         // Method names
        SyntaxOperator = new Color(180, 180, 180),       // Operators
        SyntaxDefault = Color.White,

        // Auto-complete
        AutoCompleteSelected = new Color(100, 200, 255),
        AutoCompleteUnselected = Color.LightGray,
        AutoCompleteHover = new Color(150, 210, 255),
        AutoCompleteDescription = new Color(128, 128, 128),
        AutoCompleteLoading = new Color(150, 150, 150),

        // Search
        SearchCurrentMatch = new Color(255, 200, 0, 180),
        SearchOtherMatches = new Color(100, 150, 255, 120),
        SearchPrompt = Color.Yellow,
        SearchSuccess = new Color(150, 255, 150),
        SearchDisabled = new Color(150, 150, 150),
        ReverseSearchPrompt = new Color(255, 200, 100),
        ReverseSearchMatchHighlight = new Color(255, 255, 100),

        // Bracket matching
        BracketMatch = new Color(100, 255, 100, 150),
        BracketMismatch = new Color(255, 100, 100, 150),

        // Line numbers
        LineNumberDim = new Color(100, 100, 120),
        LineNumberCurrent = new Color(150, 200, 255),

        // Help/Documentation
        HelpTitle = new Color(255, 255, 100),
        HelpText = Color.LightGray,
        HelpSectionHeader = new Color(100, 200, 255),
        HelpExample = new Color(150, 255, 150),
        DocumentationTypeInfo = new Color(255, 200, 100),
        DocumentationLabel = new Color(150, 200, 255),
        DocumentationInstruction = new Color(150, 150, 150),

        // Section headers
        SectionCommand = new Color(150, 200, 255),
        SectionError = new Color(255, 100, 100),
        SectionCategory = new Color(200, 200, 150),
        SectionManual = new Color(200, 150, 255),
        SectionSearch = new Color(255, 255, 100),

        // Section folding
        SectionFoldBackground = new Color(0, 0, 0, 255),
        SectionFoldHover = new Color(80, 80, 90, 255),
        SectionFoldHoverOutline = new Color(120, 120, 140, 255),

        // ═══════════════════════════════════════════════════════════════
        // CONSOLE-SPECIFIC SIZES & SPACING
        // ═══════════════════════════════════════════════════════════════

        // Console component sizes
        MinInputHeight = 35f,
        MaxSuggestionsHeight = 300f,

        // Gaps & offsets
        TooltipGap = 5f,
        ComponentGap = 10f,
        PanelEdgeGap = 20f,
        SuggestionPadding = 20f,

        // Interactive thresholds
        DragThreshold = 5f,
        DoubleClickMaxDistance = 5f,
        DoubleClickThreshold = 0.5f, // 500ms max time between clicks

        // ═══════════════════════════════════════════════════════════════
        // TAB-SPECIFIC COLORS
        // ═══════════════════════════════════════════════════════════════

        // Tab colors
        TabActive = new Color(40, 40, 60, 255),
        TabInactive = new Color(50, 50, 70),
        TabHover = new Color(60, 60, 90),
        TabPressed = new Color(40, 40, 60),
        TabBorder = new Color(60, 60, 80),
        TabActiveIndicator = new Color(100, 150, 255),
        TabBarBackground = new Color(30, 30, 45, 255),
    };

    // Background colors
    public Color BackgroundPrimary { get; init; }
    public Color BackgroundSecondary { get; init; }
    public Color BackgroundElevated { get; init; }

    // Text colors
    public Color TextPrimary { get; init; }
    public Color TextSecondary { get; init; }
    public Color TextDim { get; init; }

    // Interactive element colors
    public Color ButtonNormal { get; init; }
    public Color ButtonHover { get; init; }
    public Color ButtonPressed { get; init; }
    public Color ButtonText { get; init; }

    // Input colors
    public Color InputBackground { get; init; }
    public Color InputText { get; init; }
    public Color InputCursor { get; init; }
    public Color InputSelection { get; init; }

    // Border colors
    public Color BorderPrimary { get; init; }
    public Color BorderFocus { get; init; }

    // Status colors
    public Color Success { get; init; }
    public Color SuccessDim { get; init; }
    public Color Warning { get; init; }
    public Color WarningDim { get; init; }
    public Color Error { get; init; }
    public Color ErrorDim { get; init; }
    public Color Info { get; init; }
    public Color InfoDim { get; init; }

    // Special purpose colors
    public Color Prompt { get; init; }
    public Color Highlight { get; init; }
    public Color ScrollbarTrack { get; init; }
    public Color ScrollbarThumb { get; init; }
    public Color ScrollbarThumbHover { get; init; }

    // Spacing
    public float PaddingTiny { get; init; }
    public float PaddingSmall { get; init; }
    public float PaddingMedium { get; init; }
    public float PaddingLarge { get; init; }
    public float PaddingXLarge { get; init; }

    public float MarginTiny { get; init; }
    public float MarginSmall { get; init; }
    public float MarginMedium { get; init; }
    public float MarginLarge { get; init; }
    public float MarginXLarge { get; init; }

    // Sizes
    public int FontSize { get; init; }
    public int LineHeight { get; init; }
    public int ScrollbarWidth { get; init; }
    public int BorderWidth { get; init; }

    // Control sizes
    public int ButtonHeight { get; init; }
    public int InputHeight { get; init; }
    public int DropdownItemHeight { get; init; }

    // Animation
    public float AnimationSpeed { get; init; }
    public float FadeSpeed { get; init; }
    public float CursorBlinkRate { get; init; }

    // ═══════════════════════════════════════════════════════════════
    // CONSOLE-SPECIFIC COLORS
    // ═══════════════════════════════════════════════════════════════

    // Console semantic colors
    public Color ConsolePrimary { get; init; }
    public Color ConsolePrimaryDim { get; init; }
    public Color ConsolePrimaryBright { get; init; }
    public Color ConsolePrompt { get; init; }

    // Console output colors
    public Color ConsoleOutputDefault { get; init; }
    public Color ConsoleOutputSuccess { get; init; }
    public Color ConsoleOutputInfo { get; init; }
    public Color ConsoleOutputWarning { get; init; }
    public Color ConsoleOutputError { get; init; }
    public Color ConsoleOutputCommand { get; init; }
    public Color ConsoleOutputAliasExpansion { get; init; }

    // Console component backgrounds
    public Color ConsoleBackground { get; init; }
    public Color ConsoleOutputBackground { get; init; }
    public Color ConsoleInputBackground { get; init; }
    public Color ConsoleSearchBackground { get; init; }
    public Color ConsoleHintText { get; init; }

    // Syntax highlighting
    public Color SyntaxKeyword { get; init; }
    public Color SyntaxString { get; init; }
    public Color SyntaxStringInterpolation { get; init; }
    public Color SyntaxNumber { get; init; }
    public Color SyntaxComment { get; init; }
    public Color SyntaxType { get; init; }
    public Color SyntaxMethod { get; init; }
    public Color SyntaxOperator { get; init; }
    public Color SyntaxDefault { get; init; }

    // Auto-complete
    public Color AutoCompleteSelected { get; init; }
    public Color AutoCompleteUnselected { get; init; }
    public Color AutoCompleteHover { get; init; }
    public Color AutoCompleteDescription { get; init; }
    public Color AutoCompleteLoading { get; init; }

    // Search
    public Color SearchCurrentMatch { get; init; }
    public Color SearchOtherMatches { get; init; }
    public Color SearchPrompt { get; init; }
    public Color SearchSuccess { get; init; }
    public Color SearchDisabled { get; init; }
    public Color ReverseSearchPrompt { get; init; }
    public Color ReverseSearchMatchHighlight { get; init; }

    // Bracket matching
    public Color BracketMatch { get; init; }
    public Color BracketMismatch { get; init; }

    // Line numbers
    public Color LineNumberDim { get; init; }
    public Color LineNumberCurrent { get; init; }

    // Help/Documentation
    public Color HelpTitle { get; init; }
    public Color HelpText { get; init; }
    public Color HelpSectionHeader { get; init; }
    public Color HelpExample { get; init; }
    public Color DocumentationTypeInfo { get; init; }
    public Color DocumentationLabel { get; init; }
    public Color DocumentationInstruction { get; init; }

    // Section headers
    public Color SectionCommand { get; init; }
    public Color SectionError { get; init; }
    public Color SectionCategory { get; init; }
    public Color SectionManual { get; init; }
    public Color SectionSearch { get; init; }

    // Section folding
    public Color SectionFoldBackground { get; init; }
    public Color SectionFoldHover { get; init; }
    public Color SectionFoldHoverOutline { get; init; }

    // ═══════════════════════════════════════════════════════════════
    // CONSOLE-SPECIFIC SIZES & SPACING
    // ═══════════════════════════════════════════════════════════════

    // Console component sizes
    public float MinInputHeight { get; init; }
    public float MaxSuggestionsHeight { get; init; }

    // Gaps & offsets
    public float TooltipGap { get; init; }
    public float ComponentGap { get; init; }
    public float PanelEdgeGap { get; init; }
    public float SuggestionPadding { get; init; }

    // Interactive thresholds
    public float DragThreshold { get; init; }
    public float DoubleClickMaxDistance { get; init; }
    public float DoubleClickThreshold { get; init; }

    // ═══════════════════════════════════════════════════════════════
    // TAB-SPECIFIC COLORS
    // ═══════════════════════════════════════════════════════════════

    // Tab colors
    public Color TabActive { get; init; }
    public Color TabInactive { get; init; }
    public Color TabHover { get; init; }
    public Color TabPressed { get; init; }
    public Color TabBorder { get; init; }
    public Color TabActiveIndicator { get; init; }
    public Color TabBarBackground { get; init; }
}


