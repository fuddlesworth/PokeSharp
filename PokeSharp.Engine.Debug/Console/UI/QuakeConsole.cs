using Microsoft.CodeAnalysis.Completion;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using PokeSharp.Engine.Debug.Console.Configuration;
using PokeSharp.Engine.Debug.Console.Features;
using PokeSharp.Engine.Debug.Console.UI.Renderers;
using static PokeSharp.Engine.Debug.Console.Configuration.ConsoleColors;

namespace PokeSharp.Engine.Debug.Console.UI;

/// <summary>
///     Quake-style drop-down debug console.
///     - Better font rendering (FontStashSharp)
///     - Configurable console size
///     - Syntax highlighting
///     - Multi-line input support
///     - Auto-complete
/// </summary>
public class QuakeConsole : IDisposable
{
    private const int HoverRecalculationThreshold = 3; // pixels

    private const int Padding = 10;

    private readonly ConsoleAnimator _animator;

    // Renderer components (following Single Responsibility Principle)
    private readonly ConsoleAutoCompleteRenderer _autoCompleteRenderer;
    private readonly ConsoleDocumentationRenderer _documentationRenderer;
    private readonly ConsoleFontRenderer _fontRenderer;
    private readonly ConsoleInputRenderer _inputRenderer;
    private readonly ConsoleOutputRenderer _outputRenderer;
    private readonly ConsoleParameterHintRenderer _parameterHintRenderer;
    private readonly Texture2D _pixel;

    // Search functionality (managed by ConsoleSearchManager)
    private readonly ConsoleSearchManager _searchManager = new();
    private readonly ConsoleSearchRenderer _searchRenderer;
    private readonly SpriteBatch _spriteBatch;

    // Auto-complete state
    private List<CompletionItem> _allAutoCompleteSuggestions = new();
    private string _autoCompleteFilterText = string.Empty;
    private int _cachedHoverAutoCompleteIndex = -1;
    private int _cachedHoverSectionIndex = -1;
    private float _consoleHeight;

    // Mouse state for hover effects
    private Point _currentMousePosition = Point.Zero;
    private int _currentParameterIndex;
    private bool _disposed;
    private string? _documentationText;
    private List<CompletionItem> _filteredAutoCompleteSuggestions = new();
    private float _hiddenY;

    // Syntax highlighting (null when not highlighting)
    private List<ConsoleSyntaxHighlighter.ColoredSegment>? _highlightedInput;
    private bool _isLoadingSuggestions;

    // Documentation display

    private int _lastCachedSectionCount;
    private int _lastCachedTotalLines;
    private int _lastCalculatedVisibleCount = 10;
    private Point _lastHoverCalculationPosition = new(-1000, -1000);

    // Parameter hints
    private ParameterHintInfo? _parameterHints;
    private float _screenHeight;
    private float _screenWidth;
    private int _selectedSuggestionIndex = -1;
    private int _suggestionHorizontalScroll;
    private int _suggestionScrollOffset;

    // Line mapping cache (for performance optimization)
    private Dictionary<int, int>? _visibleToOriginalLineMapping;

    /// <summary>
    ///     Initializes a new instance of the QuakeConsole.
    /// </summary>
    /// <param name="graphicsDevice">The graphics device.</param>
    /// <param name="screenWidth">The screen width.</param>
    /// <param name="screenHeight">The screen height.</param>
    /// <param name="config">Optional console configuration.</param>
    public QuakeConsole(
        GraphicsDevice graphicsDevice,
        float screenWidth,
        float screenHeight,
        ConsoleConfig? config = null
    )
    {
        _spriteBatch = new SpriteBatch(graphicsDevice);
        _screenWidth = screenWidth;
        _screenHeight = screenHeight;
        Config = config ?? new ConsoleConfig();

        // Calculate console height based on config
        _consoleHeight = screenHeight * Config.GetHeightMultiplier();
        _hiddenY = -_consoleHeight;

        // Create a 1x1 white pixel texture for drawing rectangles
        _pixel = new Texture2D(graphicsDevice, 1, 1);
        _pixel.SetData(new[] { Color.White });

        // Initialize font renderer
        _fontRenderer = new ConsoleFontRenderer(graphicsDevice, _spriteBatch);
        _fontRenderer.SetFontSize(Config.FontSize);

        // Initialize renderer components
        _autoCompleteRenderer = new ConsoleAutoCompleteRenderer(
            _fontRenderer,
            _spriteBatch,
            _pixel
        );
        _parameterHintRenderer = new ConsoleParameterHintRenderer(
            _fontRenderer,
            _spriteBatch,
            _pixel
        );
        _searchRenderer = new ConsoleSearchRenderer(
            _fontRenderer,
            _spriteBatch,
            _pixel,
            screenWidth
        );
        _documentationRenderer = new ConsoleDocumentationRenderer(
            _fontRenderer,
            _spriteBatch,
            _pixel,
            screenWidth,
            screenHeight
        );
        _inputRenderer = new ConsoleInputRenderer(_spriteBatch, _pixel, _fontRenderer, screenWidth);
        _outputRenderer = new ConsoleOutputRenderer(
            _spriteBatch,
            _pixel,
            screenWidth,
            _consoleHeight
        );

        // Initialize components
        _animator = new ConsoleAnimator();
        _animator.Initialize(_hiddenY);

        // Calculate visible lines based on line height
        var lineHeight = _fontRenderer.GetLineHeight();
        var inputAreaHeight = lineHeight + 10; // Single line input (5px top + 5px bottom padding)
        var availableHeight = (int)_consoleHeight - inputAreaHeight - Padding * 3;
        Output = new ConsoleOutput { VisibleLines = availableHeight / lineHeight };
        Input = new ConsoleInputField();

        // Start hidden
        IsVisible = false;

        // Note: Welcome message is displayed by ConsoleSystem.Initialize() after full setup
    }

    /// <summary>
    ///     Gets whether the console is visible.
    /// </summary>
    public bool IsVisible { get; private set; }

    /// <summary>
    ///     Gets the input field for text entry.
    /// </summary>
    public ConsoleInputField Input { get; }

    /// <summary>
    ///     Gets the output manager.
    /// </summary>
    public ConsoleOutput Output { get; }

    /// <summary>
    ///     Gets the console configuration.
    /// </summary>
    public ConsoleConfig Config { get; private set; }

    public void Dispose()
    {
        Dispose(true);
        GC.SuppressFinalize(this);
    }

    /// <summary>
    ///     Toggles the console visibility.
    /// </summary>
    public void Toggle()
    {
        IsVisible = !IsVisible;

        if (IsVisible)
            _animator.Show();
        else
            _animator.Hide(_hiddenY);
    }

    /// <summary>
    ///     Shows the console.
    /// </summary>
    public void Show()
    {
        IsVisible = true;
        _animator.Show();
    }

    /// <summary>
    ///     Hides the console.
    /// </summary>
    public void Hide()
    {
        IsVisible = false;
        _animator.Hide(_hiddenY);
    }

    /// <summary>
    ///     Updates the console animation and syntax highlighting.
    /// </summary>
    /// <param name="deltaTime">Time elapsed since last frame.</param>
    public void Update(float deltaTime)
    {
        _animator.Update(deltaTime);

        // Update syntax highlighting if enabled
        if (Config.SyntaxHighlightingEnabled && !string.IsNullOrEmpty(Input.Text))
            _highlightedInput = ConsoleSyntaxHighlighter.Highlight(Input.Text);
        else
            _highlightedInput = null;
    }

    /// <summary>
    ///     Updates the console configuration.
    ///     Config is immutable, so this replaces the entire config instance.
    /// </summary>
    /// <param name="newConfig">The new configuration to apply.</param>
    public void UpdateConfig(ConsoleConfig newConfig)
    {
        var oldSize = Config.Size;
        Config = newConfig;

        // Recalculate console size if it changed
        if (oldSize != newConfig.Size)
            UpdateSize();
    }

    /// <summary>
    ///     Updates console size based on config changes.
    /// </summary>
    public void UpdateSize()
    {
        _consoleHeight = _screenHeight * Config.GetHeightMultiplier();
        _hiddenY = -_consoleHeight;

        // Update output renderer with new console height
        _outputRenderer.UpdateSize(_screenWidth, _consoleHeight);

        // Recalculate visible lines
        var lineHeight = _fontRenderer.GetLineHeight();
        var inputAreaHeight = lineHeight + 10; // Single line input (5px top + 5px bottom padding)
        var availableHeight = (int)_consoleHeight - inputAreaHeight - Padding * 3;
        Output.VisibleLines = availableHeight / lineHeight;
    }

    /// <summary>
    ///     Updates screen dimensions when window is resized.
    ///     This propagates the new dimensions to all renderer components.
    /// </summary>
    /// <param name="screenWidth">New screen width.</param>
    /// <param name="screenHeight">New screen height.</param>
    public void UpdateScreenSize(float screenWidth, float screenHeight)
    {
        _screenWidth = screenWidth;
        _screenHeight = screenHeight;

        // Recalculate console height based on new screen height
        _consoleHeight = _screenHeight * Config.GetHeightMultiplier();
        _hiddenY = -_consoleHeight;

        // Update all renderer components with new dimensions
        _searchRenderer.UpdateScreenSize(_screenWidth);
        _documentationRenderer.UpdateScreenSize(_screenWidth, _screenHeight);
        _inputRenderer.UpdateScreenSize(_screenWidth);
        _outputRenderer.UpdateSize(_screenWidth, _consoleHeight);

        // Recalculate visible lines (same as UpdateSize)
        var lineHeight = _fontRenderer.GetLineHeight();
        var inputAreaHeight = lineHeight + 10;
        var availableHeight = (int)_consoleHeight - inputAreaHeight - Padding * 3;
        Output.VisibleLines = availableHeight / lineHeight;
    }

    /// <summary>
    ///     Increases the font size and updates the layout.
    /// </summary>
    public int IncreaseFontSize()
    {
        var newSize = _fontRenderer.IncreaseFontSize();
        UpdateSize(); // Recalculate layout for new font size
        Config = Config with { FontSize = newSize }; // Update config
        return newSize;
    }

    /// <summary>
    ///     Decreases the font size and updates the layout.
    /// </summary>
    public int DecreaseFontSize()
    {
        var newSize = _fontRenderer.DecreaseFontSize();
        UpdateSize(); // Recalculate layout for new font size
        Config = Config with { FontSize = newSize }; // Update config
        return newSize;
    }

    /// <summary>
    ///     Resets the font size to default and updates the layout.
    /// </summary>
    public int ResetFontSize()
    {
        var newSize = _fontRenderer.ResetFontSize();
        UpdateSize(); // Recalculate layout for new font size
        Config = Config with { FontSize = newSize }; // Update config
        return newSize;
    }

    /// <summary>
    ///     Gets the current font size.
    /// </summary>
    public int GetFontSize()
    {
        return _fontRenderer.GetFontSize();
    }

    /// <summary>
    ///     Renders the console.
    /// </summary>
    public void Render()
    {
        var yPos = _animator.CurrentY;

        // Don't render if completely hidden
        if (yPos <= _hiddenY)
            return;

        _spriteBatch.Begin();

        try
        {
            // Draw semi-transparent background
            DrawRectangle(0, (int)yPos, (int)_screenWidth, (int)_consoleHeight, Background_Primary);

            // Draw output text (line by line with colors)
            var outputY = yPos + Padding;
            var visibleLines = Output.GetVisibleLines();
            var lineY = (int)outputY;
            var lineHeight = _fontRenderer.GetLineHeight();

            // Draw search highlights first (if searching)
            if (_searchManager.OutputSearcher.IsSearching)
                _searchRenderer.DrawSearchHighlights(
                    (int)outputY,
                    lineHeight,
                    _searchManager.OutputSearcher,
                    Output.ScrollOffset,
                    Output.GetAllLines(),
                    Output
                );

            // Draw output text selection highlight (if any)
            if (Output.HasOutputSelection)
                DrawOutputSelectionHighlight((int)outputY, lineHeight, visibleLines);

            // Draw text on top of highlights
            foreach (var line in visibleLines)
            {
                _fontRenderer.DrawString(line.Text, Padding, lineY, line.Color);
                lineY += lineHeight;
            }

            // Draw section fold/unfold boxes with hover effects
            var hoveredSectionLineIndex = GetHoveredSectionHeaderIndex();
            _outputRenderer.DrawSectionFoldBoxes(
                (int)outputY,
                lineHeight,
                Output,
                hoveredSectionLineIndex
            );

            // Draw scroll indicator if there's more content (considering filters and folding)
            if (Output.GetEffectiveLineCount() > Output.VisibleLines)
                _outputRenderer.DrawScrollIndicator((int)yPos, lineHeight, Output);

            // Draw input area - dynamically size based on actual line count
            // Include extra space for multi-line indicator if present
            // Use 10 pixels total padding (5 top + 5 bottom) for balanced spacing
            var inputHeight = lineHeight * Math.Max(1, Input.LineCount) + 10;
            var totalInputAreaHeight = inputHeight;

            // Add space for indicator if multi-line
            if (Input.LineCount > 1)
                totalInputAreaHeight += lineHeight + 6; // indicator height + spacing

            var inputY = yPos + _consoleHeight - totalInputAreaHeight - Padding;
            _inputRenderer.DrawInputArea(
                (int)inputY,
                inputHeight,
                lineHeight,
                Input,
                Config,
                _highlightedInput
            );

            // Draw auto-complete suggestions or loading indicator
            if (Config.AutoCompleteEnabled)
            {
                if (_isLoadingSuggestions)
                {
                    _autoCompleteRenderer.DrawLoading(
                        (int)inputY - 5,
                        lineHeight,
                        _animator.CurrentY
                    );
                }
                else if (_filteredAutoCompleteSuggestions.Count > 0)
                {
                    // Calculate hover index for visual feedback
                    var hoverIndex = GetHoveredAutoCompleteItemIndex();

                    var adjustedScrollOffset = _autoCompleteRenderer.DrawSuggestions(
                        (int)inputY - 5,
                        lineHeight,
                        _filteredAutoCompleteSuggestions,
                        _selectedSuggestionIndex,
                        _suggestionScrollOffset,
                        _suggestionHorizontalScroll,
                        hoverIndex,
                        out _lastCalculatedVisibleCount
                    );

                    if (adjustedScrollOffset >= 0)
                        _suggestionScrollOffset = adjustedScrollOffset;
                }
            }

            // Draw parameter hints if active
            if (_parameterHints != null && _parameterHints.Overloads.Count > 0)
                _parameterHintRenderer.DrawParameterHints(
                    (int)inputY - 5,
                    lineHeight,
                    _parameterHints,
                    _currentParameterIndex
                );

            // Draw search bar if in search mode
            if (_searchManager.IsSearchMode)
                _searchRenderer.DrawSearchBar(
                    (int)_consoleHeight,
                    lineHeight,
                    _searchManager.SearchInput,
                    _searchManager.OutputSearcher
                );

            // Draw reverse-i-search bar if in reverse search mode
            if (_searchManager.IsReverseSearchMode)
                _searchRenderer.DrawReverseSearchBar(
                    (int)_consoleHeight,
                    lineHeight,
                    _searchManager.ReverseSearchInput,
                    _searchManager.ReverseSearchMatches,
                    _searchManager.ReverseSearchIndex,
                    _searchManager.CurrentReverseSearchMatch ?? ""
                );

            // Draw documentation popup if showing
            if (IsShowingDocumentation && !string.IsNullOrEmpty(_documentationText))
                _documentationRenderer.DrawDocumentationPopup(_documentationText, lineHeight);
        }
        finally
        {
            _spriteBatch.End();
        }
    }

    /// <summary>
    ///     Draws a filled rectangle.
    /// </summary>
    private void DrawRectangle(int x, int y, int width, int height, Color color)
    {
        _spriteBatch.Draw(_pixel, new Rectangle(x, y, width, height), color);
    }

    /// <summary>
    ///     Sets auto-complete suggestions to display (stores full list for filtering).
    /// </summary>
    public void SetAutoCompleteSuggestions(List<CompletionItem> suggestions, string filterText = "")
    {
        // Create a NEW list to prevent external modifications from affecting our internal state
        _allAutoCompleteSuggestions =
            suggestions != null
                ? new List<CompletionItem>(suggestions)
                : new List<CompletionItem>();
        _autoCompleteFilterText = filterText;

        // Apply initial filter
        FilterSuggestions(filterText);

        // Auto-select first suggestion by default (VS Code/IntelliJ behavior)
        if (_filteredAutoCompleteSuggestions.Count > 0)
            _selectedSuggestionIndex = 0;

        // Reset scroll to top when new suggestions arrive
        _suggestionScrollOffset = 0;

        // Clear loading state
        _isLoadingSuggestions = false;

        // Suggestions tracked - logging happens in FilterSuggestions if needed
    }

    /// <summary>
    ///     Sets loading state for auto-complete.
    /// </summary>
    public void SetAutoCompleteLoading(bool isLoading)
    {
        _isLoadingSuggestions = isLoading;
        if (isLoading)
        {
            // Clear suggestions when starting to load
            _filteredAutoCompleteSuggestions.Clear();
            _selectedSuggestionIndex = -1;
        }
    }

    /// <summary>
    ///     Filters the current autocomplete suggestions based on the current input text.
    ///     Supports prefix matching (preferred) and fuzzy matching (fallback).
    /// </summary>
    /// <param name="currentInput">The current input text to filter against (null is treated as empty).</param>
    public void FilterSuggestions(string? currentInput)
    {
        // Handle null or empty suggestions list
        if (_allAutoCompleteSuggestions.Count == 0)
        {
            _filteredAutoCompleteSuggestions.Clear();
            _selectedSuggestionIndex = -1;
            return;
        }

        // Handle null input (treat as empty string)
        if (currentInput == null)
        {
            _filteredAutoCompleteSuggestions = new List<CompletionItem>(
                _allAutoCompleteSuggestions
            );
            _selectedSuggestionIndex = _filteredAutoCompleteSuggestions.Count > 0 ? 0 : -1;
            return;
        }

        // Get the word being typed (after last space/operator) - use span to avoid allocations
        var wordStart = currentInput.LastIndexOfAny(ConsoleConstants.AutoComplete.WordSeparators);
        var startIndex = wordStart >= 0 ? wordStart + 1 : 0;

        // Trim whitespace manually without allocation
        while (startIndex < currentInput.Length && char.IsWhiteSpace(currentInput[startIndex]))
            startIndex++;

        var endIndex = currentInput.Length;
        while (endIndex > startIndex && char.IsWhiteSpace(currentInput[endIndex - 1]))
            endIndex--;

        // Extract filter substring (one allocation, but unavoidable for LINQ)
        var filterText =
            startIndex < endIndex
                ? currentInput.Substring(startIndex, endIndex - startIndex)
                : string.Empty;

        if (string.IsNullOrEmpty(filterText))
        {
            // No filter, show all
            _filteredAutoCompleteSuggestions = new List<CompletionItem>(
                _allAutoCompleteSuggestions
            );
        }
        else
        {
            // Filter by prefix match (case-insensitive)
            // Note: We need the string for LINQ (can't use Span in lambda expressions)
            _filteredAutoCompleteSuggestions = _allAutoCompleteSuggestions
                .Where(item =>
                    item.DisplayText.StartsWith(filterText, StringComparison.OrdinalIgnoreCase)
                )
                .ToList();

            // If no prefix matches, try contains match (fuzzy)
            if (_filteredAutoCompleteSuggestions.Count == 0)
            {
                var filterLower = filterText.ToLower();
                _filteredAutoCompleteSuggestions = _allAutoCompleteSuggestions
                    .Where(item => item.DisplayText.ToLower().Contains(filterLower))
                    .ToList();
            }
        }

        // Reset selection or keep it valid
        if (_filteredAutoCompleteSuggestions.Count == 0)
            _selectedSuggestionIndex = -1;
        else if (_selectedSuggestionIndex >= _filteredAutoCompleteSuggestions.Count)
            _selectedSuggestionIndex = 0; // Reset to first item

        // Filter results tracked internally
    }

    /// <summary>
    ///     Clears auto-complete suggestions.
    /// </summary>
    public void ClearAutoCompleteSuggestions()
    {
        _allAutoCompleteSuggestions.Clear();
        _filteredAutoCompleteSuggestions.Clear();
        _autoCompleteFilterText = string.Empty;
        _selectedSuggestionIndex = -1;
        _isLoadingSuggestions = false;
        _suggestionScrollOffset = 0;
        _suggestionHorizontalScroll = 0;

        // Invalidate hover cache
        _cachedHoverAutoCompleteIndex = -2;
    }

    /// <summary>
    ///     Sets parameter hints for the current method call.
    /// </summary>
    public void SetParameterHints(ParameterHintInfo? hints, int currentParameterIndex = 0)
    {
        _parameterHints = hints;
        _currentParameterIndex = currentParameterIndex;
    }

    /// <summary>
    ///     Clears parameter hints.
    /// </summary>
    public void ClearParameterHints()
    {
        _parameterHints = null; // Explicitly null to release memory
        _currentParameterIndex = 0;
    }

    /// <summary>
    ///     Gets whether parameter hints are currently shown.
    /// </summary>
    public bool HasParameterHints()
    {
        return _parameterHints != null && _parameterHints.Overloads.Count > 0;
    }

    /// <summary>
    ///     Cycles to the next parameter hint overload.
    /// </summary>
    public void NextParameterHintOverload()
    {
        if (_parameterHints == null || _parameterHints.Overloads.Count <= 1)
            return;

        _parameterHints.CurrentOverloadIndex =
            (_parameterHints.CurrentOverloadIndex + 1) % _parameterHints.Overloads.Count;
    }

    /// <summary>
    ///     Cycles to the previous parameter hint overload.
    /// </summary>
    public void PreviousParameterHintOverload()
    {
        if (_parameterHints == null || _parameterHints.Overloads.Count <= 1)
            return;

        _parameterHints.CurrentOverloadIndex--;
        if (_parameterHints.CurrentOverloadIndex < 0)
            _parameterHints.CurrentOverloadIndex = _parameterHints.Overloads.Count - 1;
    }

    /// <summary>
    ///     Navigates through autocomplete suggestions up or down with wrapping and auto-scrolling.
    /// </summary>
    /// <param name="up">True to navigate up, false to navigate down.</param>
    /// <returns>True if the selection changed, false otherwise.</returns>
    public bool NavigateSuggestions(bool up)
    {
        if (_filteredAutoCompleteSuggestions.Count == 0)
            return false;

        var oldIndex = _selectedSuggestionIndex;

        // Handle initial selection (-1 means no selection)
        if (_selectedSuggestionIndex == -1)
        {
            // First navigation - select first or last item
            _selectedSuggestionIndex = up ? _filteredAutoCompleteSuggestions.Count - 1 : 0;
            EnsureSelectedItemVisible();
            return true; // Always changed from no selection
        }

        if (up)
            // Move up, wrap to bottom if at top
            _selectedSuggestionIndex =
                _selectedSuggestionIndex > 0
                    ? _selectedSuggestionIndex - 1
                    : _filteredAutoCompleteSuggestions.Count - 1;
        else
            // Move down, wrap to top if at bottom
            _selectedSuggestionIndex =
                (_selectedSuggestionIndex + 1) % _filteredAutoCompleteSuggestions.Count;

        // Auto-scroll to keep selected item visible
        EnsureSelectedItemVisible();

        // Reset horizontal scroll when changing selection
        if (_selectedSuggestionIndex != oldIndex)
            _suggestionHorizontalScroll = 0;

        return _selectedSuggestionIndex != oldIndex;
    }

    /// <summary>
    ///     Scrolls the selected suggestion text to the left (shows more of the end).
    /// </summary>
    public void ScrollSuggestionLeft()
    {
        if (_selectedSuggestionIndex < 0 || _filteredAutoCompleteSuggestions.Count == 0)
            return;

        _suggestionHorizontalScroll += ConsoleConstants.Rendering.SuggestionHorizontalScrollAmount;
    }

    /// <summary>
    ///     Scrolls the selected suggestion text to the right (shows more of the beginning).
    /// </summary>
    public void ScrollSuggestionRight()
    {
        if (_selectedSuggestionIndex < 0 || _filteredAutoCompleteSuggestions.Count == 0)
            return;

        _suggestionHorizontalScroll = Math.Max(
            0,
            _suggestionHorizontalScroll
                - ConsoleConstants.Rendering.SuggestionHorizontalScrollAmount
        );
    }

    /// <summary>
    ///     Adjusts scroll offset to ensure the selected item is visible.
    /// </summary>
    private void EnsureSelectedItemVisible()
    {
        if (_selectedSuggestionIndex < 0 || _filteredAutoCompleteSuggestions.Count == 0)
            return;

        // Use the last calculated visible count from DrawAutoCompleteSuggestions
        // This ensures we use the actual number of items that fit on screen
        var maxVisible = _lastCalculatedVisibleCount;

        // If selected item is above visible range, scroll up
        if (_selectedSuggestionIndex < _suggestionScrollOffset)
            _suggestionScrollOffset = _selectedSuggestionIndex;
        // If selected item is below visible range, scroll down
        else if (_selectedSuggestionIndex >= _suggestionScrollOffset + maxVisible)
            _suggestionScrollOffset = _selectedSuggestionIndex - maxVisible + 1;

        // Ensure scroll offset stays within valid bounds
        var maxScroll = Math.Max(0, _filteredAutoCompleteSuggestions.Count - maxVisible);
        _suggestionScrollOffset = Math.Clamp(_suggestionScrollOffset, 0, maxScroll);
    }

    /// <summary>
    ///     Scrolls the suggestions list by the specified number of items.
    /// </summary>
    /// <param name="delta">Number of items to scroll (positive = down, negative = up).</param>
    public void ScrollSuggestions(int delta)
    {
        if (_filteredAutoCompleteSuggestions.Count == 0)
            return;

        // Calculate max visible items
        var maxVisible = ConsoleConstants.Limits.MaxAutoCompleteSuggestions;
        var maxScroll = Math.Max(0, _filteredAutoCompleteSuggestions.Count - maxVisible);

        // Update scroll offset
        _suggestionScrollOffset = Math.Clamp(_suggestionScrollOffset + delta, 0, maxScroll);

        // Update selection to stay within visible range
        if (_selectedSuggestionIndex >= 0)
        {
            // Keep selection within visible range
            if (_selectedSuggestionIndex < _suggestionScrollOffset)
                _selectedSuggestionIndex = _suggestionScrollOffset;
            else if (_selectedSuggestionIndex >= _suggestionScrollOffset + maxVisible)
                _selectedSuggestionIndex = _suggestionScrollOffset + maxVisible - 1;

            // Ensure selection is still within total bounds
            _selectedSuggestionIndex = Math.Clamp(
                _selectedSuggestionIndex,
                0,
                _filteredAutoCompleteSuggestions.Count - 1
            );
        }
    }

    /// <summary>
    ///     Gets the currently selected auto-complete suggestion text.
    /// </summary>
    public string? GetSelectedSuggestion()
    {
        if (
            _selectedSuggestionIndex >= 0
            && _selectedSuggestionIndex < _filteredAutoCompleteSuggestions.Count
        )
            return _filteredAutoCompleteSuggestions[_selectedSuggestionIndex].DisplayText;
        return null;
    }

    /// <summary>
    ///     Gets the currently selected auto-complete item.
    /// </summary>
    public CompletionItem? GetSelectedCompletionItem()
    {
        if (
            _selectedSuggestionIndex >= 0
            && _selectedSuggestionIndex < _filteredAutoCompleteSuggestions.Count
        )
            return _filteredAutoCompleteSuggestions[_selectedSuggestionIndex];
        return null;
    }

    /// <summary>
    ///     Gets the index of the currently selected suggestion.
    /// </summary>
    public int GetSelectedSuggestionIndex()
    {
        return _selectedSuggestionIndex;
    }

    /// <summary>
    ///     Checks if auto-complete suggestions are currently visible.
    /// </summary>
    public bool HasSuggestions()
    {
        return _filteredAutoCompleteSuggestions.Count > 0;
    }

    /// <summary>
    ///     Appends output to the console with default color (white).
    /// </summary>
    public void AppendOutput(string text)
    {
        Output.AppendLine(text, Output_Default);
    }

    /// <summary>
    ///     Appends output to the console with specified color.
    /// </summary>
    public void AppendOutput(string text, Color color)
    {
        Output.AppendLine(text, color);
    }

    /// <summary>
    ///     Gets the current input text.
    /// </summary>
    public string GetInputText()
    {
        return Input.Text;
    }

    /// <summary>
    ///     Clears the input field.
    /// </summary>
    public void ClearInput()
    {
        Input.Clear();
        _highlightedInput = null;
        ClearAutoCompleteSuggestions();
        ClearParameterHints(); // Clear parameter hints when clearing input
    }

    /// <summary>
    ///     Clears the output.
    /// </summary>
    public void ClearOutput()
    {
        Output.Clear();
    }

    /// <summary>
    ///     Scrolls the console output by the specified number of lines.
    /// </summary>
    /// <param name="lines">Number of lines to scroll. Positive = down, Negative = up.</param>
    public void ScrollOutput(int lines)
    {
        if (lines > 0)
            // Scroll down
            for (var i = 0; i < lines; i++)
                Output.ScrollDown();
        else if (lines < 0)
            // Scroll up
            for (var i = 0; i < -lines; i++)
                Output.ScrollUp();
    }

    /// <summary>
    ///     Scrolls the auto-complete list by the specified number of items.
    /// </summary>
    /// <param name="items">Number of items to scroll. Positive = down, Negative = up.</param>
    public void ScrollAutoComplete(int items)
    {
        if (!HasSuggestions())
            return;

        if (items > 0)
            // Scroll down
            for (var i = 0; i < items; i++)
                NavigateSuggestions(false); // down
        else if (items < 0)
            // Scroll up
            for (var i = 0; i < -items; i++)
                NavigateSuggestions(true); // up
    }

    /// <summary>
    ///     Checks if the mouse is over the auto-complete window.
    /// </summary>
    /// <param name="mousePosition">Mouse position.</param>
    /// <returns>True if mouse is over auto-complete.</returns>
    public bool IsMouseOverAutoComplete(Point mousePosition)
    {
        if (!HasSuggestions() || !IsVisible)
            return false;

        // Calculate auto-complete bounds using the EXACT same calculation as rendering
        var lineHeight = _fontRenderer.GetLineHeight();

        // Match the EXACT calculation from Draw() method
        var inputHeight = lineHeight * Math.Max(1, Input.LineCount) + 10;
        var totalInputAreaHeight = inputHeight;
        if (Input.LineCount > 1)
            totalInputAreaHeight += lineHeight + 6;

        var yPos = _animator.CurrentY;
        var inputY = yPos + _consoleHeight - totalInputAreaHeight - Padding;
        var bottomY = (int)inputY - 5;

        // Calculate panel dimensions
        var maxAvailableHeight = bottomY - ConsoleConstants.Rendering.Padding * 2;
        var maxVisibleSuggestions = Math.Max(
            1,
            (maxAvailableHeight - ConsoleConstants.Rendering.Padding * 2) / lineHeight
        );
        maxVisibleSuggestions = Math.Min(
            maxVisibleSuggestions,
            ConsoleConstants.Limits.MaxAutoCompleteSuggestions
        );
        var visibleCount = Math.Min(maxVisibleSuggestions, _filteredAutoCompleteSuggestions.Count);

        var scrollIndicatorSpace = lineHeight;
        var contentHeight = visibleCount * lineHeight;
        var panelHeight = contentHeight + scrollIndicatorSpace * 2;
        var autoCompleteY = bottomY - panelHeight;

        return mousePosition.Y >= autoCompleteY
            && mousePosition.Y <= autoCompleteY + panelHeight
            && mousePosition.X >= 0
            && mousePosition.X <= _screenWidth;
    }

    /// <summary>
    ///     Updates the current mouse position for hover effects.
    /// </summary>
    /// <param name="mousePosition">Current mouse position.</param>
    public void UpdateMousePosition(Point mousePosition)
    {
        _currentMousePosition = mousePosition;

        // Invalidate hover cache if mouse moved significantly
        var deltaX = Math.Abs(mousePosition.X - _lastHoverCalculationPosition.X);
        var deltaY = Math.Abs(mousePosition.Y - _lastHoverCalculationPosition.Y);

        if (deltaX > HoverRecalculationThreshold || deltaY > HoverRecalculationThreshold)
        {
            _cachedHoverAutoCompleteIndex = -2; // -2 means needs recalculation
            _cachedHoverSectionIndex = -2;
        }
    }

    /// <summary>
    ///     Checks if the mouse is over the input field area.
    /// </summary>
    /// <param name="mousePosition">Mouse position to check.</param>
    /// <returns>True if mouse is over input field.</returns>
    public bool IsMouseOverInputField(Point mousePosition)
    {
        if (!IsVisible)
            return false;

        var lineHeight = _fontRenderer.GetLineHeight();
        var inputHeight = lineHeight * Math.Max(1, Input.LineCount) + 10;
        var totalInputAreaHeight = inputHeight;
        if (Input.LineCount > 1)
            totalInputAreaHeight += lineHeight + 6;

        var yPos = _animator.CurrentY;
        var inputY = yPos + _consoleHeight - totalInputAreaHeight - Padding;
        var inputAreaY = (int)inputY;
        var inputAreaHeight = totalInputAreaHeight;

        return mousePosition.Y >= inputAreaY
            && mousePosition.Y <= inputAreaY + inputAreaHeight
            && mousePosition.X >= Padding
            && mousePosition.X <= _screenWidth - Padding;
    }

    /// <summary>
    ///     Gets the character position at the given mouse X coordinate in the input field.
    /// </summary>
    /// <param name="mousePosition">Mouse position.</param>
    /// <returns>Character index, or -1 if not over input field.</returns>
    public int GetCharacterPositionAtMouse(Point mousePosition)
    {
        if (!IsMouseOverInputField(mousePosition))
            return -1;

        // Get the input text
        var inputText = Input.Text;
        if (string.IsNullOrEmpty(inputText))
            return 0;

        // Calculate the X position where text starts (with padding and prompt)
        var textStartX = Padding + ConsoleConstants.Rendering.InputPromptOffset;

        // Get relative X position within the text
        var relativeX = mousePosition.X - textStartX;
        if (relativeX <= 0)
            return 0;

        // Quick check: if click is beyond the entire text, return end position
        var totalWidth = _fontRenderer.MeasureString(inputText).X;
        if (relativeX >= totalWidth)
            return inputText.Length;

        // Binary search for the character position (much faster than linear)
        var left = 0;
        var right = inputText.Length;

        while (left < right)
        {
            var mid = (left + right) / 2;
            var widthAtMid = _fontRenderer.MeasureString(inputText.Substring(0, mid)).X;

            if (widthAtMid < relativeX)
                left = mid + 1;
            else
                right = mid;
        }

        // Check if we should round to the previous or next character
        if (left > 0)
        {
            var widthAtLeft = _fontRenderer.MeasureString(inputText.Substring(0, left)).X;
            var widthAtPrev = _fontRenderer.MeasureString(inputText.Substring(0, left - 1)).X;
            var midPoint = (widthAtPrev + widthAtLeft) / 2;

            if (relativeX < midPoint)
                return left - 1;
        }

        return left;
    }

    /// <summary>
    ///     Handles a click on the input field to position the cursor.
    /// </summary>
    /// <param name="mousePosition">Mouse click position.</param>
    /// <returns>True if click was handled.</returns>
    public bool HandleInputFieldClick(Point mousePosition)
    {
        if (!IsMouseOverInputField(mousePosition))
            return false;

        var charPosition = GetCharacterPositionAtMouse(mousePosition);
        if (charPosition >= 0)
        {
            // Move cursor to the clicked position
            Input.SetCursorPosition(charPosition);
            return true;
        }

        return false;
    }

    /// <summary>
    ///     Checks if the mouse is over the console output area.
    /// </summary>
    /// <param name="mousePosition">Mouse position.</param>
    /// <returns>True if mouse is over output area.</returns>
    public bool IsMouseOverOutputArea(Point mousePosition)
    {
        var yPos = _animator.CurrentY;
        var lineHeight = _fontRenderer.GetLineHeight();

        // Calculate input area height
        var inputAreaHeight = lineHeight * Math.Max(1, Input.LineCount) + 10;
        if (Input.LineCount > 1)
            inputAreaHeight += lineHeight + 6; // Multi-line indicator

        // Output area is between top padding and input area
        var outputY = (int)(yPos + Padding);
        var outputHeight = (int)(_consoleHeight - inputAreaHeight - Padding * 3);

        return mousePosition.Y >= outputY
            && mousePosition.Y < outputY + outputHeight
            && mousePosition.X >= Padding
            && mousePosition.X <= _screenWidth - Padding;
    }

    /// <summary>
    ///     Gets the line and column position in the output area at the given mouse position.
    /// </summary>
    /// <param name="mousePosition">Mouse position.</param>
    /// <returns>Tuple of (line, column) or (-1, -1) if not over output.</returns>
    public (int Line, int Column) GetOutputPositionAtMouse(Point mousePosition)
    {
        if (!IsMouseOverOutputArea(mousePosition))
            return (-1, -1);

        var yPos = _animator.CurrentY;
        var lineHeight = _fontRenderer.GetLineHeight();
        var outputY = (int)(yPos + Padding);

        // Calculate which visible line was clicked
        var relativeY = mousePosition.Y - outputY;
        var clickedLine = relativeY / lineHeight;

        // Check if line is within visible range
        var visibleLines = Output.GetVisibleLines();
        if (clickedLine < 0 || clickedLine >= visibleLines.Count)
            return (-1, -1);

        // Get the line text
        var lineText = visibleLines[clickedLine].Text;

        // Calculate column position
        var relativeX = mousePosition.X - Padding;
        if (relativeX <= 0)
            return (clickedLine, 0);

        // Quick check: if click is beyond the entire text, return end position
        var totalWidth = _fontRenderer.MeasureString(lineText).X;
        if (relativeX >= totalWidth)
            return (clickedLine, lineText.Length);

        // Binary search for the character position
        var left = 0;
        var right = lineText.Length;

        while (left < right)
        {
            var mid = (left + right) / 2;
            var widthAtMid = _fontRenderer.MeasureString(lineText.Substring(0, mid)).X;

            if (widthAtMid < relativeX)
                left = mid + 1;
            else
                right = mid;
        }

        // Check if we should round to the previous or next character
        if (left > 0)
        {
            var widthAtLeft = _fontRenderer.MeasureString(lineText.Substring(0, left)).X;
            var widthAtPrev = _fontRenderer.MeasureString(lineText.Substring(0, left - 1)).X;
            var midPoint = (widthAtPrev + widthAtLeft) / 2;

            if (relativeX < midPoint)
                return (clickedLine, left - 1);
        }

        return (clickedLine, left);
    }

    /// <summary>
    ///     Gets the auto-complete item index at the given mouse position.
    ///     Returns -1 if no item is at that position.
    /// </summary>
    /// <param name="mousePosition">Mouse position.</param>
    /// <returns>Index of the auto-complete item, or -1 if none.</returns>
    /// <summary>
    ///     Gets the hovered autocomplete item index (display index only, for visual feedback).
    ///     Returns -1 if no item is hovered.
    /// </summary>
    private int GetHoveredAutoCompleteItemIndex()
    {
        // Return cached value if available
        if (_cachedHoverAutoCompleteIndex >= -1)
            return _cachedHoverAutoCompleteIndex;

        // Calculate new value
        if (!HasSuggestions() || !IsVisible)
        {
            _cachedHoverAutoCompleteIndex = -1;
            _lastHoverCalculationPosition = _currentMousePosition;
            return -1;
        }

        if (!IsMouseOverAutoComplete(_currentMousePosition))
        {
            _cachedHoverAutoCompleteIndex = -1;
            _lastHoverCalculationPosition = _currentMousePosition;
            return -1;
        }

        // Use the same calculation as GetAutoCompleteItemAt but return display index only
        var lineHeight = _fontRenderer.GetLineHeight();

        // Match the EXACT calculation from Draw() method
        var inputHeight = lineHeight * Math.Max(1, Input.LineCount) + 10;
        var totalInputAreaHeight = inputHeight;
        if (Input.LineCount > 1)
            totalInputAreaHeight += lineHeight + 6;

        var yPos = _animator.CurrentY;
        var inputY = yPos + _consoleHeight - totalInputAreaHeight - Padding;
        var bottomY = (int)inputY - 5;

        var maxAvailableHeight = bottomY - ConsoleConstants.Rendering.Padding * 2;
        var maxVisibleSuggestions = Math.Max(
            1,
            (maxAvailableHeight - ConsoleConstants.Rendering.Padding * 2) / lineHeight
        );
        maxVisibleSuggestions = Math.Min(
            maxVisibleSuggestions,
            ConsoleConstants.Limits.MaxAutoCompleteSuggestions
        );

        var visibleCount = Math.Min(maxVisibleSuggestions, _filteredAutoCompleteSuggestions.Count);

        var scrollIndicatorSpace = lineHeight;
        var contentHeight = visibleCount * lineHeight;
        var panelHeight = contentHeight + scrollIndicatorSpace * 2;
        var autoCompleteY = bottomY - panelHeight;

        var contentStartY = autoCompleteY + scrollIndicatorSpace;
        var relativeY = _currentMousePosition.Y - contentStartY;

        if (relativeY < 0 || relativeY >= contentHeight)
        {
            _cachedHoverAutoCompleteIndex = -1;
            _lastHoverCalculationPosition = _currentMousePosition;
            return -1;
        }

        var displayIndex = relativeY / lineHeight;

        // Validate display index is within visible range
        if (displayIndex < 0 || displayIndex >= visibleCount)
        {
            _cachedHoverAutoCompleteIndex = -1;
            _lastHoverCalculationPosition = _currentMousePosition;
            return -1;
        }

        _cachedHoverAutoCompleteIndex = displayIndex;
        _lastHoverCalculationPosition = _currentMousePosition;
        return displayIndex;
    }

    public int GetAutoCompleteItemAt(Point mousePosition)
    {
        // Early exits without logging - these are normal
        if (!HasSuggestions() || !IsVisible)
            return -1;

        if (!IsMouseOverAutoComplete(mousePosition))
            return -1;

        // Calculate auto-complete bounds (MUST match ConsoleAutoCompleteRenderer.DrawSuggestions exactly!)
        var lineHeight = _fontRenderer.GetLineHeight();

        // Match the EXACT calculation from Draw() method (lines 482-492)
        var inputHeight = lineHeight * Math.Max(1, Input.LineCount) + 10;
        var totalInputAreaHeight = inputHeight;
        if (Input.LineCount > 1)
            totalInputAreaHeight += lineHeight + 6; // indicator height + spacing

        var yPos = _animator.CurrentY;
        var inputY = yPos + _consoleHeight - totalInputAreaHeight - Padding;
        var bottomY = (int)inputY - 5;

        // Calculate max available space (matches renderer line 75-77)
        var maxAvailableHeight = bottomY - ConsoleConstants.Rendering.Padding * 2;
        var maxVisibleSuggestions = Math.Max(
            1,
            (maxAvailableHeight - ConsoleConstants.Rendering.Padding * 2) / lineHeight
        );
        maxVisibleSuggestions = Math.Min(
            maxVisibleSuggestions,
            ConsoleConstants.Limits.MaxAutoCompleteSuggestions
        );

        // Get visible count (matches renderer line 79)
        var visibleCount = Math.Min(maxVisibleSuggestions, _filteredAutoCompleteSuggestions.Count);

        // Calculate panel dimensions (matches renderer lines 83-86)
        var scrollIndicatorSpace = lineHeight; // One line height for each indicator (NOT + Padding!)
        var contentHeight = visibleCount * lineHeight;
        var panelHeight = contentHeight + scrollIndicatorSpace * 2;
        var autoCompleteY = bottomY - panelHeight;

        // Content starts after top scroll indicator (matches renderer line 105)
        var contentStartY = autoCompleteY + scrollIndicatorSpace;

        // Calculate which item was clicked (relative to content area)
        var relativeY = mousePosition.Y - contentStartY;

        if (relativeY < 0 || relativeY >= contentHeight)
            return -1;

        var clickedDisplayIndex = relativeY / lineHeight;

        // Validate display index is within visible range
        if (clickedDisplayIndex < 0 || clickedDisplayIndex >= visibleCount)
            return -1;

        // Adjust for scroll offset to get actual item index
        var actualIndex = _suggestionScrollOffset + clickedDisplayIndex;

        // Validate index is within total suggestions
        if (actualIndex >= 0 && actualIndex < _filteredAutoCompleteSuggestions.Count)
            return actualIndex;

        return -1;
    }

    /// <summary>
    ///     Selects a specific auto-complete item by index.
    /// </summary>
    /// <param name="index">Index of the item to select.</param>
    public void SelectAutoCompleteItem(int index)
    {
        if (index >= 0 && index < _filteredAutoCompleteSuggestions.Count)
        {
            _selectedSuggestionIndex = index;
            EnsureSelectedItemVisible();
            _suggestionHorizontalScroll = 0; // Reset horizontal scroll
        }
    }

    /// <summary>
    ///     Gets the hovered section header line index (for visual feedback).
    ///     Returns -1 if no section header is hovered.
    /// </summary>
    private int GetHoveredSectionHeaderIndex()
    {
        // Return cached value if available
        if (_cachedHoverSectionIndex >= -1)
            return _cachedHoverSectionIndex;

        // Calculate new value
        _cachedHoverSectionIndex = GetSectionHeaderAtPosition(_currentMousePosition);
        _lastHoverCalculationPosition = _currentMousePosition;
        return _cachedHoverSectionIndex;
    }

    /// <summary>
    ///     Gets the section header line index at the given mouse position, if any.
    ///     Returns -1 if no section header is at that position.
    /// </summary>
    /// <param name="mousePosition">Mouse position.</param>
    /// <returns>Visual line index of the section header, or -1 if none.</returns>
    public int GetSectionHeaderAtPosition(Point mousePosition)
    {
        if (!IsVisible)
            return -1;

        // Calculate output area bounds
        var lineHeight = _fontRenderer.GetLineHeight();
        var inputAreaHeight = lineHeight + 10;
        var outputY = Padding;
        var outputHeight = (int)(_consoleHeight - inputAreaHeight - Padding * 3);

        // Check if click is within output area
        if (mousePosition.Y < outputY || mousePosition.Y > outputY + outputHeight)
            return -1;

        // Calculate which visible line
        var visualLineIndex = (mousePosition.Y - outputY) / lineHeight;

        // Check if this line is a section header
        var visibleHeaders = Output.GetVisibleSectionHeaders();
        foreach (var (section, lineIndex) in visibleHeaders)
            if (lineIndex == visualLineIndex)
                return visualLineIndex;

        return -1;
    }

    /// <summary>
    ///     Disposes of the console resources.
    /// </summary>
    /// <param name="disposing">True if called from Dispose(), false if called from finalizer.</param>
    protected virtual void Dispose(bool disposing)
    {
        if (_disposed)
            return;

        if (disposing)
        {
            // Dispose managed resources
            _spriteBatch?.Dispose();
            _pixel?.Dispose();
            _fontRenderer?.Dispose();
        }

        _disposed = true;
    }

    /// <summary>
    ///     Handles mouse clicks on the console output area to toggle sections.
    ///     Clicking anywhere on a section header line will toggle it.
    /// </summary>
    /// <param name="mousePosition">Mouse position.</param>
    /// <returns>True if a section was toggled.</returns>
    public bool HandleOutputClick(Point mousePosition)
    {
        // Only handle clicks when console is visible
        if (!IsVisible)
            return false;

        // Calculate console dimensions (console is at top of screen when open)
        float yPos = 0;

        // Calculate output area bounds
        var lineHeight = _fontRenderer.GetLineHeight();
        var inputAreaHeight = lineHeight + 10; // Single line input (5px top + 5px bottom padding)
        var outputY = (int)(yPos + Padding);
        var outputHeight = (int)(_consoleHeight - inputAreaHeight - Padding * 3);

        // Check if click is within output area
        if (mousePosition.Y < outputY || mousePosition.Y > outputY + outputHeight)
            return false;

        // Calculate which visible line was clicked
        var clickedVisibleLineIndex = (mousePosition.Y - outputY) / lineHeight;

        // Get visible section headers
        var visibleHeaders = Output.GetVisibleSectionHeaders();

        // Check if the clicked line is a section header
        // If so, toggle it (entire line is clickable for better UX)
        foreach (var (section, visualLineIndex) in visibleHeaders)
            if (visualLineIndex == clickedVisibleLineIndex)
            {
                // Find the original line index and toggle
                var originalLineIndex = FindOriginalLineIndex(clickedVisibleLineIndex);
                if (originalLineIndex >= 0)
                    return Output.ToggleSectionAtLine(originalLineIndex);
            }

        return false;
    }

    /// <summary>
    ///     Finds the original line index for a visible line index.
    ///     Uses cached mapping for performance when possible.
    /// </summary>
    private int FindOriginalLineIndex(int visibleLineIndex)
    {
        // Build or rebuild cache if needed
        if (NeedsCacheRebuild())
            RebuildLineMapping();

        // Use cached mapping
        var adjustedIndex = visibleLineIndex + Output.ScrollOffset;
        return
            _visibleToOriginalLineMapping?.TryGetValue(adjustedIndex, out var originalIndex) == true
            ? originalIndex
            : -1;
    }

    /// <summary>
    ///     Checks if the line mapping cache needs to be rebuilt.
    ///     Cache is invalidated when lines are added/removed or sections change.
    /// </summary>
    private bool NeedsCacheRebuild()
    {
        if (_visibleToOriginalLineMapping == null)
            return true;

        // Check if output has changed
        if (_lastCachedTotalLines != Output.TotalLines)
            return true;

        // Check if section count changed (folding/unfolding can change this)
        var currentSectionCount = Output.GetAllSections().Count;
        if (_lastCachedSectionCount != currentSectionCount)
            return true;

        return false;
    }

    /// <summary>
    ///     Rebuilds the mapping from visible line indices to original line indices.
    ///     This is cached for performance to avoid O(n) scans on every click.
    /// </summary>
    private void RebuildLineMapping()
    {
        _visibleToOriginalLineMapping = new Dictionary<int, int>();
        var visibleCount = 0;

        for (var i = 0; i < Output.TotalLines; i++)
            if (IsLineVisible(i))
            {
                _visibleToOriginalLineMapping[visibleCount] = i;
                visibleCount++;
            }

        // Update cache validity markers
        _lastCachedTotalLines = Output.TotalLines;
        _lastCachedSectionCount = Output.GetAllSections().Count;
    }

    /// <summary>
    ///     Checks if a line at the given original index would be visible
    ///     (after filtering and folding).
    /// </summary>
    private bool IsLineVisible(int originalIndex)
    {
        if (originalIndex < 0 || originalIndex >= Output.TotalLines)
            return false;

        // Check if section contains this line and is folded
        var section = Output.GetSectionContainingLine(originalIndex);
        if (section != null && section.IsFolded)
            // Only header is visible in folded sections
            return originalIndex == section.StartLine;

        return true;
    }

    /// <summary>
    ///     Draws the output text selection highlight.
    /// </summary>
    /// <param name="outputY">Y position of output area.</param>
    /// <param name="lineHeight">Height of each line.</param>
    /// <param name="visibleLines">The visible lines being rendered.</param>
    private void DrawOutputSelectionHighlight(
        int outputY,
        int lineHeight,
        IReadOnlyList<ConsoleLine> visibleLines
    )
    {
        var (startLine, startCol, endLine, endCol) = Output.GetSelectionRange();

        // Clamp to visible range
        if (startLine >= visibleLines.Count || endLine < 0)
            return;

        startLine = Math.Max(0, startLine);
        endLine = Math.Min(endLine, visibleLines.Count - 1);

        // Selection highlight color (semi-transparent blue, similar to most text editors)
        var selectionColor = new Color(51, 153, 255, 80); // Light blue with alpha

        for (var i = startLine; i <= endLine && i < visibleLines.Count; i++)
        {
            var lineText = visibleLines[i].Text;
            var lineY = outputY + i * lineHeight;

            if (i == startLine && i == endLine)
            {
                // Selection is within a single line
                var actualStartCol = Math.Min(startCol, lineText.Length);
                var actualEndCol = Math.Min(endCol, lineText.Length);

                if (actualStartCol < actualEndCol)
                {
                    var beforeSelection = lineText.Substring(0, actualStartCol);
                    var selection = lineText.Substring(
                        actualStartCol,
                        actualEndCol - actualStartCol
                    );

                    var startX = Padding + _fontRenderer.MeasureString(beforeSelection).X;
                    var selectionWidth = _fontRenderer.MeasureString(selection).X;

                    DrawRectangle(
                        (int)startX,
                        lineY,
                        (int)selectionWidth,
                        lineHeight,
                        selectionColor
                    );
                }
            }
            else if (i == startLine)
            {
                // First line of multi-line selection
                var actualStartCol = Math.Min(startCol, lineText.Length);
                var beforeSelection = lineText.Substring(0, actualStartCol);
                var selection = lineText.Substring(actualStartCol);

                var startX = Padding + _fontRenderer.MeasureString(beforeSelection).X;
                var selectionWidth = _fontRenderer.MeasureString(selection).X;

                DrawRectangle((int)startX, lineY, (int)selectionWidth, lineHeight, selectionColor);
            }
            else if (i == endLine)
            {
                // Last line of multi-line selection
                var actualEndCol = Math.Min(endCol, lineText.Length);
                var selection = lineText.Substring(0, actualEndCol);

                var selectionWidth = _fontRenderer.MeasureString(selection).X;

                DrawRectangle(Padding, lineY, (int)selectionWidth, lineHeight, selectionColor);
            }
            else
            {
                // Middle lines - select entire line
                var lineWidth = _fontRenderer.MeasureString(lineText).X;
                DrawRectangle(Padding, lineY, (int)lineWidth, lineHeight, selectionColor);
            }
        }
    }

    #region Search Functionality

    /// <summary>
    ///     Gets whether search mode is active.
    /// </summary>
    public bool IsSearchMode => _searchManager.IsSearchMode;

    /// <summary>
    ///     Gets the current search input.
    /// </summary>
    public string SearchInput => _searchManager.SearchInput;

    /// <summary>
    ///     Gets the output searcher.
    /// </summary>
    public OutputSearcher OutputSearcher => _searchManager.OutputSearcher;

    /// <summary>
    ///     Starts search mode.
    /// </summary>
    public void StartSearch()
    {
        _searchManager.StartSearch();
    }

    /// <summary>
    ///     Exits search mode.
    /// </summary>
    public void ExitSearch()
    {
        _searchManager.ExitSearch(Output);
    }

    /// <summary>
    ///     Updates the search query.
    /// </summary>
    public void UpdateSearchQuery(string query)
    {
        _searchManager.UpdateSearchQuery(query, Output);
    }

    /// <summary>
    ///     Navigates to the next search match.
    /// </summary>
    public void NextSearchMatch()
    {
        _searchManager.NextSearchMatch(Output);
    }

    /// <summary>
    ///     Navigates to the previous search match.
    /// </summary>
    public void PreviousSearchMatch()
    {
        _searchManager.PreviousSearchMatch(Output);
    }

    #endregion

    #region Reverse-i-search

    /// <summary>
    ///     Gets whether reverse-i-search mode is active.
    /// </summary>
    public bool IsReverseSearchMode => _searchManager.IsReverseSearchMode;

    /// <summary>
    ///     Gets the current reverse-i-search input.
    /// </summary>
    public string ReverseSearchInput => _searchManager.ReverseSearchInput;

    /// <summary>
    ///     Gets the current reverse-i-search match.
    /// </summary>
    public string? CurrentReverseSearchMatch => _searchManager.CurrentReverseSearchMatch;

    /// <summary>
    ///     Starts reverse-i-search mode.
    /// </summary>
    public void StartReverseSearch()
    {
        _searchManager.StartReverseSearch();
    }

    /// <summary>
    ///     Exits reverse-i-search mode.
    /// </summary>
    public void ExitReverseSearch()
    {
        _searchManager.ExitReverseSearch();
    }

    /// <summary>
    ///     Updates the reverse-i-search query and finds matches.
    /// </summary>
    public void UpdateReverseSearchQuery(string query, IEnumerable<string> historyCommands)
    {
        _searchManager.UpdateReverseSearchQuery(query, historyCommands);
    }

    /// <summary>
    ///     Moves to the next match in reverse-i-search.
    /// </summary>
    public void ReverseSearchNextMatch()
    {
        _searchManager.ReverseSearchNextMatch();
    }

    /// <summary>
    ///     Moves to the previous match in reverse-i-search.
    /// </summary>
    public void ReverseSearchPreviousMatch()
    {
        _searchManager.ReverseSearchPreviousMatch();
    }

    /// <summary>
    ///     Accepts the current reverse-i-search match and exits search mode.
    /// </summary>
    public void AcceptReverseSearchMatch()
    {
        var match = _searchManager.GetCurrentMatch();
        _searchManager.ExitReverseSearch();

        if (match != null)
        {
            Input.SetText(match);
            Input.SetCursorPosition(match.Length);
        }
    }

    #endregion

    #region Documentation Display

    /// <summary>
    ///     Gets whether documentation is currently being displayed.
    /// </summary>
    public bool IsShowingDocumentation { get; private set; }

    /// <summary>
    ///     Shows documentation popup with the given text.
    /// </summary>
    public void ShowDocumentation(string documentationText)
    {
        _documentationText = documentationText;
        IsShowingDocumentation = true;
    }

    /// <summary>
    ///     Hides the documentation popup.
    /// </summary>
    public void HideDocumentation()
    {
        IsShowingDocumentation = false;
        _documentationText = null; // Explicitly null to release memory
    }

    #endregion
}
