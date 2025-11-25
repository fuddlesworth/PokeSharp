using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Input;
using PokeSharp.Engine.UI.Debug.Components.Base;
using PokeSharp.Engine.UI.Debug.Components.Controls;
using PokeSharp.Engine.UI.Debug.Components.Layout;
using PokeSharp.Engine.UI.Debug.Core;
using PokeSharp.Engine.UI.Debug.Input;
using PokeSharp.Engine.UI.Debug.Layout;

namespace PokeSharp.Engine.UI.Debug.Components.Debug;

/// <summary>
/// Complete console panel combining text output, command input, and auto-completion.
/// This is the new UI framework version of the Quake console.
/// </summary>
public class ConsolePanel : Panel
{
    private readonly TextBuffer _outputBuffer;
    private readonly TextEditor _commandEditor;
    private readonly SuggestionsDropdown _suggestionsDropdown;
    private readonly HintBar _hintBar;
    private readonly SearchBar _searchBar;
    private readonly HintBar _searchHintBar;
    private readonly ParameterHintTooltip _parameterHints;
    private readonly DocumentationPopup _documentationPopup;

    // Overlay state (mutually exclusive modes)
    private ConsoleOverlayMode _overlayMode = ConsoleOverlayMode.None;

    // Other state flags (different concerns)
    private bool _isCompletingText = false; // Flag to prevent completion requests during completion

    // Layout constraints (from UITheme)
    private static float InputMinHeight => UITheme.Dark.MinInputHeight; // Minimum height for input (1 line)
    private static float SuggestionsMaxHeight => UITheme.Dark.MaxSuggestionsHeight;
    private static float Padding => UITheme.Dark.ComponentGap;
    private static float ComponentSpacing => UITheme.Dark.ComponentGap; // Semantic name for gaps between components
    private static float TooltipGap => UITheme.Dark.TooltipGap; // Gap for tooltips above components
    private static float PanelEdgeGap => UITheme.Dark.PanelEdgeGap; // Gap from panel edges

    // Console state
    private bool _closeRequested = false; // Defer close until after rendering
    private ConsoleSize _currentSize = ConsoleSize.Medium; // Default to 50% height

    // Events
    public Action<string>? OnCommandSubmitted { get; set; }
    public Action<string>? OnCommandChanged { get; set; }
    public Action<string>? OnRequestCompletions { get; set; }
    public Action<string, int>? OnRequestParameterHints { get; set; } // (text, cursorPos)
    public Action<string>? OnRequestDocumentation { get; set; } // (completionText)
    public Action? OnCloseRequested { get; set; }
    public Action<ConsoleSize>? OnSizeChanged { get; set; }

    public ConsolePanel()
    {
        Id = "console_panel";
        BackgroundColor = UITheme.Dark.ConsoleBackground;
        BorderColor = UITheme.Dark.BorderPrimary;
        BorderThickness = 1;

        // Add padding to the console panel - this creates the ContentRect for children
        Constraint.Padding = Padding;

        // Create output buffer - it will be laid out within the padded ContentRect
        _outputBuffer = new TextBuffer("console_output")
        {
            BackgroundColor = UITheme.Dark.ConsoleOutputBackground,
            AutoScroll = true,
            MaxLines = 5000,
            Constraint = new LayoutConstraint
            {
                Anchor = Anchor.StretchTop,
                Height = 400 // Will be dynamically calculated in OnRenderContainer
            }
        };

        // Create command editor - anchored above the hint bar
        _commandEditor = new TextEditor("console_input")
        {
            Prompt = "> ",
            BackgroundColor = UITheme.Dark.ConsoleInputBackground,
            MinVisibleLines = 1,
            MaxVisibleLines = 10, // Max 10 lines before scrolling
            Constraint = new LayoutConstraint
            {
                Anchor = Anchor.StretchBottom, // Stretch horizontally, positioned from bottom
                OffsetY = 0, // Will be updated dynamically to sit above hint bar
                Height = InputMinHeight // Will be updated dynamically
            }
        };

        // Create suggestions dropdown - positioned above the input box
        // The bottom edge is fixed at a consistent position; only the top edge moves
        _suggestionsDropdown = new SuggestionsDropdown("console_suggestions")
        {
            MaxVisibleItems = 8,
            Constraint = new LayoutConstraint
            {
                Anchor = Anchor.BottomLeft, // Anchor to bottom-left
                OffsetX = Padding, // Align with parameter hints (respects panel padding)
                OffsetY = -(InputMinHeight + Padding), // Will be updated dynamically
                WidthPercent = 0.5f, // 50% of content width
                MinWidth = 400f, // Minimum width for readability
                MaxWidth = 800f, // Maximum width to prevent excessive size
                Height = 0 // Dynamically calculated, grows upward
            }
        };

        // Create hint bar - shows helpful text at the absolute bottom (below input)
        _hintBar = new HintBar("console_hints")
        {
            TextColor = UITheme.Dark.ConsoleHintText,
            Constraint = new LayoutConstraint
            {
                Anchor = Anchor.StretchBottom, // Stretch horizontally at absolute bottom
                OffsetY = 0, // At the very bottom
                Height = 0 // Dynamically calculated (0 when hidden)
            }
        };

        // Create search bar - positioned at bottom (replaces input when active)
        _searchBar = new SearchBar("console_search")
        {
            BackgroundColor = UITheme.Dark.ConsoleSearchBackground,
            BorderColor = Color.Transparent, // No border - use console panel's border
            BorderThickness = 0,
            Constraint = new LayoutConstraint
            {
                Anchor = Anchor.StretchBottom,
                OffsetY = 0, // Will be positioned above search hint
                Height = InputMinHeight
            }
        };

        // Create search hint bar - shows keyboard shortcuts for search
        _searchHintBar = new HintBar("search_hints")
        {
            TextColor = UITheme.Dark.ConsoleHintText,
            Constraint = new LayoutConstraint
            {
                Anchor = Anchor.StretchBottom,
                OffsetY = 0,
                Height = 0 // Dynamically calculated
            }
        };

        // Create parameter hints tooltip - shows method signatures
        _parameterHints = new ParameterHintTooltip("parameter_hints")
        {
            Constraint = new LayoutConstraint
            {
                Anchor = Anchor.BottomLeft,
                OffsetY = 0, // Will be positioned above input editor
                Height = 0, // Dynamically calculated
                Width = 0, // Dynamically calculated
                MaxWidth = 800f, // Prevent tooltip from being too wide
                MaxHeight = 300f // Prevent tooltip from being too tall
            }
        };

        // Create documentation popup - shows detailed docs for selected completion
        // Positioned as a side panel on the right, doesn't overlap with output
        _documentationPopup = new DocumentationPopup("documentation_popup")
        {
            Constraint = new LayoutConstraint
            {
                Anchor = Anchor.TopRight, // Top-right panel (clear separation from action popups)
                OffsetX = -PanelEdgeGap, // Gap from right edge
                OffsetY = PanelEdgeGap, // Gap from top edge
                WidthPercent = 0.35f, // 35% of panel width
                MinWidth = 400f, // Minimum width for readability
                MaxWidth = 600f, // Maximum width to prevent excessive size
                HeightPercent = 0.7f, // 70% of panel height
                Height = 0 // Dynamically calculated (will override percentage when hidden)
            }
        };

        // Load command history from disk
        _commandEditor.LoadHistoryFromDisk();

        // Wire up events
        _commandEditor.OnSubmit += HandleCommandSubmit;
        _commandEditor.OnTextChanged += HandleTextChanged;
        _commandEditor.OnRequestCompletions = HandleRequestCompletions;
        _commandEditor.OnEscape = HandleEscape;

        _suggestionsDropdown.OnItemSelected = HandleSuggestionSelected;
        _suggestionsDropdown.OnCancelled = () => _overlayMode = ConsoleOverlayMode.None;

        _searchBar.OnSearchTextChanged = HandleSearchTextChanged;
        _searchBar.OnNextMatch = HandleNextMatch;
        _searchBar.OnPreviousMatch = HandlePreviousMatch;
        _searchBar.OnClose = HandleSearchClose;

        // Add children
        AddChild(_outputBuffer);
        AddChild(_commandEditor);
        AddChild(_suggestionsDropdown);
        AddChild(_hintBar);
        AddChild(_searchBar);
        AddChild(_searchHintBar);
        AddChild(_parameterHints);
        AddChild(_documentationPopup);
    }

    /// <summary>
    /// Appends a line to the console output.
    /// </summary>
    public void AppendOutput(string text, Color color, string category = "General")
    {
        _outputBuffer.AppendLine(text, color, category);
    }

    /// <summary>
    /// Clears all output.
    /// </summary>
    public void ClearOutput()
    {
        _outputBuffer.Clear();
    }

    /// <summary>
    /// Clears command history from memory and disk.
    /// </summary>
    public void ClearHistory()
    {
        _commandEditor.ClearHistory();
    }

    /// <summary>
    /// Gets the current console size.
    /// </summary>
    public ConsoleSize GetSize() => _currentSize;

    /// <summary>
    /// Sets the console size to a specific preset.
    /// </summary>
    public void SetSize(ConsoleSize size)
    {
        _currentSize = size;
        OnSizeChanged?.Invoke(size);
    }

    /// <summary>
    /// Sets parameter hints for the current method call.
    /// </summary>
    public void SetParameterHints(ParamHints? hints, int currentParameterIndex = 0)
    {
        _parameterHints.SetHints(hints, currentParameterIndex);
        _overlayMode = (hints != null && hints.Overloads.Count > 0) ? ConsoleOverlayMode.ParameterHints : ConsoleOverlayMode.None;
    }

    /// <summary>
    /// Clears parameter hints.
    /// </summary>
    public void ClearParameterHints()
    {
        _parameterHints.Clear();
        if (_overlayMode == ConsoleOverlayMode.ParameterHints)
            _overlayMode = ConsoleOverlayMode.None;
    }

    /// <summary>
    /// Cycles to the next parameter hint overload.
    /// </summary>
    public void NextParameterHintOverload()
    {
        _parameterHints.NextOverload();
    }

    /// <summary>
    /// Cycles to the previous parameter hint overload.
    /// </summary>
    public void PreviousParameterHintOverload()
    {
        _parameterHints.PreviousOverload();
    }

    /// <summary>
    /// Sets documentation for display.
    /// </summary>
    public void SetDocumentation(DocInfo doc)
    {
        _documentationPopup.SetDocumentation(doc);
        _overlayMode = ConsoleOverlayMode.Documentation;
    }

    /// <summary>
    /// Clears documentation.
    /// </summary>
    public void ClearDocumentation()
    {
        _documentationPopup.Clear();
        if (_overlayMode == ConsoleOverlayMode.Documentation)
            _overlayMode = ConsoleOverlayMode.None;
    }

    /// <summary>
    /// Cycles to the next larger console size.
    /// </summary>
    public void CycleSizeUp()
    {
        SetSize(_currentSize.Next());
    }

    /// <summary>
    /// Cycles to the next smaller console size.
    /// </summary>
    public void CycleSizeDown()
    {
        SetSize(_currentSize.Previous());
    }

    /// <summary>
    /// Gets the number of lines in the output buffer.
    /// </summary>
    public int GetOutputLineCount()
    {
        return _outputBuffer.TotalLines;
    }

    /// <summary>
    /// Gets the current cursor position in the command editor.
    /// </summary>
    public int GetCursorPosition()
    {
        return _commandEditor.CursorPosition;
    }

    /// <summary>
    /// Gets the command history.
    /// </summary>
    public IReadOnlyList<string> GetCommandHistory()
    {
        return _commandEditor.GetHistory();
    }

    /// <summary>
    /// Clears the command history.
    /// </summary>
    public void ClearCommandHistory()
    {
        _commandEditor.ClearHistory();
    }

    /// <summary>
    /// Saves the command history to disk.
    /// </summary>
    public void SaveCommandHistory()
    {
        _commandEditor.SaveHistoryToDisk();
    }

    /// <summary>
    /// Loads the command history from disk.
    /// </summary>
    public void LoadCommandHistory()
    {
        _commandEditor.LoadHistoryFromDisk();
    }

    /// <summary>
    /// Shows the console panel.
    /// </summary>
    public void Show()
    {
        Console.WriteLine($"[ConsolePanel] Show() called - setting Visible to true (was {Visible})");
        Visible = true;
        Console.WriteLine($"[ConsolePanel] Show() complete - Visible is now {Visible}");
    }

    /// <summary>
    /// Hides the console panel.
    /// </summary>
    public void Hide()
    {
        // Defer the close request until after rendering completes
        // to avoid modifying scene collection during Draw
        _closeRequested = true;
        Visible = false;
    }

    /// <summary>
    /// Checks if a close was requested and triggers the OnCloseRequested event.
    /// This should be called during Update, not Draw, to avoid scene collection modification during rendering.
    /// </summary>
    public void ProcessDeferredClose()
    {
        if (_closeRequested)
        {
            _closeRequested = false;
            OnCloseRequested?.Invoke();
        }
    }

    /// <summary>
    /// Sets auto-completion suggestions.
    /// </summary>
    public void SetCompletions(List<string> completions)
    {
        // Don't show suggestions if we're currently completing text
        if (_isCompletingText)
        {
            return;
        }

        if (completions.Count > 0)
        {
            var suggestions = completions.Select(c => new SuggestionItem(c)).ToList();
            _suggestionsDropdown.SetItems(suggestions);
            // Clear any existing filter so all suggestions show
            _suggestionsDropdown.SetFilter(string.Empty);
            _overlayMode = ConsoleOverlayMode.Suggestions;
        }
        else
        {
            _overlayMode = ConsoleOverlayMode.None;
        }
    }

    /// <summary>
    /// Sets auto-completion suggestions with descriptions.
    /// </summary>
    public void SetCompletions(List<SuggestionItem> suggestions)
    {
        // Don't show suggestions if we're currently completing text
        if (_isCompletingText)
        {
            return;
        }

        if (suggestions.Count > 0)
        {
            _suggestionsDropdown.SetItems(suggestions);
            _overlayMode = ConsoleOverlayMode.Suggestions;
        }
        else
        {
            _overlayMode = ConsoleOverlayMode.None;
        }
    }

    /// <summary>
    /// Focuses the command input.
    /// </summary>
    public void FocusInput()
    {
        // Input will auto-focus when panel is shown
    }

    private void HandleCommandSubmit(string command)
    {
        // Echo command to output
        AppendOutput($"> {command}", UITheme.Dark.Prompt);

        // Execute command
        OnCommandSubmitted?.Invoke(command);

        // Hide suggestions and command history search
        if (_overlayMode == ConsoleOverlayMode.Suggestions || _overlayMode == ConsoleOverlayMode.CommandHistorySearch)
            _overlayMode = ConsoleOverlayMode.None;
    }

    private void HandleTextChanged(string text)
    {
        OnCommandChanged?.Invoke(text);

        // Don't handle text changes if we're currently completing text
        if (_isCompletingText)
        {
            return;
        }

        // Handle command history search mode
        if (_overlayMode == ConsoleOverlayMode.CommandHistorySearch)
        {
            // Re-filter history based on new text
            var history = _commandEditor.GetHistory();
            FilterCommandHistory(history, text);
            return;
        }

        var cursorPos = _commandEditor.CursorPosition;

        // Request parameter hints if we detect a method call
        OnRequestParameterHints?.Invoke(text, cursorPos);

        // If suggestions are visible, filter them based on the current partial word at cursor
        if (_overlayMode == ConsoleOverlayMode.Suggestions && _suggestionsDropdown.HasItems)
        {
            // Extract the partial word at the cursor position for filtering
            var partialWord = ExtractPartialWordAtCursor(text, cursorPos);

            _suggestionsDropdown.SetFilter(partialWord);

            // Hide suggestions if no items match the filter
            if (!_suggestionsDropdown.HasItems)
            {
                _overlayMode = ConsoleOverlayMode.None;
            }
        }
    }

    /// <summary>
    /// Extracts the partial word at the cursor position for autocomplete filtering.
    /// Walks backward from cursor to find the word boundary.
    /// </summary>
    private string ExtractPartialWordAtCursor(string text, int cursorPos)
    {
        if (string.IsNullOrEmpty(text) || cursorPos == 0)
            return string.Empty;

        // Find word start by walking backward from cursor
        int wordStart = cursorPos;
        while (wordStart > 0)
        {
            char c = text[wordStart - 1];
            // Stop at word boundaries
            if (char.IsWhiteSpace(c) || c == '(' || c == ')' || c == '[' || c == ']' ||
                c == '{' || c == '}' || c == ',' || c == ';' || c == '=' || c == '.')
            {
                break;
            }
            wordStart--;
        }

        // Extract the partial word from word start to cursor
        return text.Substring(wordStart, cursorPos - wordStart);
    }

    private void HandleRequestCompletions(string text)
    {
        OnRequestCompletions?.Invoke(text);
    }

    private void HandleEscape()
    {
        // Priority: Search > Documentation > Suggestions > Console
        if (_overlayMode == ConsoleOverlayMode.Search)
        {
            _overlayMode = ConsoleOverlayMode.None;
            _outputBuffer.ClearSearch();
            _searchBar.Clear();
        }
        else if (_overlayMode == ConsoleOverlayMode.Documentation)
        {
            ClearDocumentation();
        }
        else if (_overlayMode == ConsoleOverlayMode.Suggestions || _overlayMode == ConsoleOverlayMode.CommandHistorySearch)
        {
            _overlayMode = ConsoleOverlayMode.None;
            _suggestionsDropdown.Clear();
        }
        else
        {
            Hide();
        }
    }

    private void ToggleSearch()
    {
        if (_overlayMode == ConsoleOverlayMode.Search)
        {
            _overlayMode = ConsoleOverlayMode.None;
        }
        else
        {
            _overlayMode = ConsoleOverlayMode.Search;
        }

        if (_overlayMode == ConsoleOverlayMode.Search)
        {
            // Focus search bar
            Context?.SetFocus(_searchBar.Id);
        }
        else
        {
            _searchBar.Clear();
        }
    }

    private void HandleSearchTextChanged(string searchText)
    {
        // If search text is empty or whitespace, clear the search
        if (string.IsNullOrWhiteSpace(searchText))
        {
            _outputBuffer.ClearSearch();
            _searchBar.TotalMatches = 0;
            _searchBar.CurrentMatchIndex = 0;
        }
        else
        {
            var matchCount = _outputBuffer.Search(searchText);
            _searchBar.TotalMatches = matchCount;
            _searchBar.CurrentMatchIndex = _outputBuffer.GetCurrentSearchMatchIndex();
        }
    }

    private void HandleNextMatch()
    {
        _outputBuffer.FindNext();
        _searchBar.CurrentMatchIndex = _outputBuffer.GetCurrentSearchMatchIndex();
    }

    private void HandlePreviousMatch()
    {
        _outputBuffer.FindPrevious();
        _searchBar.CurrentMatchIndex = _outputBuffer.GetCurrentSearchMatchIndex();
    }

    private void HandleSearchClose()
    {
        if (_overlayMode == ConsoleOverlayMode.Search)
            _overlayMode = ConsoleOverlayMode.None;
        _outputBuffer.ClearSearch();
        _searchBar.Clear();
    }

    private void ToggleCommandHistorySearch()
    {
        if (_overlayMode == ConsoleOverlayMode.CommandHistorySearch)
        {
            // Exit command history search mode
            _overlayMode = ConsoleOverlayMode.None;
            _suggestionsDropdown.Clear();
        }
        else
        {
            // Enter command history search mode
            _overlayMode = ConsoleOverlayMode.CommandHistorySearch;

            // Get all history and populate suggestions
            var history = _commandEditor.GetHistory();
            FilterCommandHistory(history, _commandEditor.Text);

            // Focus stays on command editor
            Context?.SetFocus(_commandEditor.Id);
        }
    }

    private void FilterCommandHistory(List<string> history, string filterText)
    {
        // Filter by substring match (case-insensitive)
        var filtered = history
            .Where(cmd => string.IsNullOrWhiteSpace(filterText) ||
                         cmd.Contains(filterText, StringComparison.OrdinalIgnoreCase))
            .Reverse() // Most recent first
            .Select(cmd => new SuggestionItem(
                Text: cmd,
                Description: "from history",
                Category: "Command"
            ))
            .ToList();

        _suggestionsDropdown.SetItems(filtered);

        if (filtered.Count > 0)
        {
            _suggestionsDropdown.SelectedIndex = 0;
        }
    }

    private void HandleSuggestionSelected(SuggestionItem item)
    {
        // Handle command history search mode
        if (_overlayMode == ConsoleOverlayMode.CommandHistorySearch)
        {
            // Insert selected command into input
            _commandEditor.SetText(item.Text);
            _commandEditor.MoveCursorToEnd();

            // Exit search mode
            _overlayMode = ConsoleOverlayMode.None;
            _suggestionsDropdown.Clear();
            return;
        }

        // CRITICAL: Set flags to prevent re-requesting completions
        _overlayMode = ConsoleOverlayMode.None;
        _isCompletingText = true;

        try
        {
            // Clear the suggestions dropdown completely
            _suggestionsDropdown.Clear();

            // Complete the text
            _commandEditor.CompleteText(item.Text);
        }
        finally
        {
            // Always reset the flag, even if an exception occurs
            _isCompletingText = false;
        }
    }

    protected override void OnRenderContainer(UIContext context)
    {
        Console.WriteLine($"[ConsolePanel] OnRenderContainer called - Visible={Visible}, Rect=({Rect.X},{Rect.Y},{Rect.Width}x{Rect.Height})");
        Console.WriteLine($"[ConsolePanel] Current container's ContentRect=({context.CurrentContainer.ContentRect.X},{context.CurrentContainer.ContentRect.Y},{context.CurrentContainer.ContentRect.Width}x{context.CurrentContainer.ContentRect.Height})");

        float inputHeight, hintHeight;

        if (_overlayMode == ConsoleOverlayMode.Search)
        {
            // Search mode: Hide input/hint, show search/search-hint
            _commandEditor.Constraint.Height = 0; // Hide input
            _hintBar.Constraint.Height = 0; // Hide input hint
            _commandEditor.Visible = false;
            _hintBar.Visible = false;

            // Update search hint bar
            var searchHintText = "[Enter]/[F3] next • [Shift+F3] previous • [Esc] close";
            _searchHintBar.SetText(searchHintText);

            // Calculate search heights
            inputHeight = InputMinHeight; // Search bar uses fixed height
            hintHeight = _searchHintBar.GetDesiredHeight(context.Renderer);

            // Position search hint bar at absolute bottom
            _searchHintBar.Constraint.Height = hintHeight;
            _searchHintBar.Constraint.OffsetY = 0;
            _searchHintBar.Visible = true;

            // Position search bar above hint bar
            _searchBar.Constraint.Height = inputHeight;
            _searchBar.Constraint.OffsetY = -hintHeight;
            _searchBar.Visible = true;
        }
        else
        {
            // Normal mode: Show input/hint, hide search/search-hint
            _searchBar.Constraint.Height = 0; // Hide search
            _searchHintBar.Constraint.Height = 0; // Hide search hint
            _searchBar.Visible = false;
            _searchHintBar.Visible = false;
            _commandEditor.Visible = true;
            _hintBar.Visible = true;

            // Update hint bar based on mode and editor state
            if (_overlayMode == ConsoleOverlayMode.CommandHistorySearch)
            {
                _hintBar.SetText("[Ctrl+R] Toggle search • [Type] filter • [Up/Down] navigate • [Enter] select • [Esc] cancel");
            }
            else if (_commandEditor.IsMultiLine)
            {
                _hintBar.SetText($"({_commandEditor.LineCount} lines) [Ctrl+Enter] submit • [Shift+Enter] new line");
            }
            else
            {
                _hintBar.SetText("[Ctrl+F] Search • [Ctrl+R] History • [Tab] Complete • [Up/Down] History • [Enter] Submit");
            }

            // Calculate dynamic heights
            inputHeight = _commandEditor.GetDesiredHeight(context.Renderer);
            hintHeight = _hintBar.GetDesiredHeight(context.Renderer);

            // Position hint bar at absolute bottom
            _hintBar.Constraint.Height = hintHeight;
            _hintBar.Constraint.OffsetY = 0; // At the very bottom

            // Position input editor above hint bar
            _commandEditor.Constraint.Height = inputHeight;
            _commandEditor.Constraint.OffsetY = -hintHeight; // Moves up by hint height

            Console.WriteLine($"[ConsolePanel] Input bar: Height={inputHeight}, OffsetY={_commandEditor.Constraint.OffsetY}, Anchor={_commandEditor.Constraint.Anchor}");
        }

        // Update suggestions dropdown offset to stay above the input and hints
        _suggestionsDropdown.Constraint.OffsetY = -(inputHeight + hintHeight + ComponentSpacing);

        // Update parameter hints tooltip
        if (_overlayMode == ConsoleOverlayMode.ParameterHints && _parameterHints.HasContent)
        {
            var hintTooltipHeight = _parameterHints.GetDesiredHeight(context.Renderer);
            var hintTooltipWidth = _parameterHints.GetDesiredWidth(context.Renderer);

            _parameterHints.Constraint.Height = hintTooltipHeight;
            _parameterHints.Constraint.Width = hintTooltipWidth;
            // Position bottom of tooltip above the input bar with proper gap
            _parameterHints.Constraint.OffsetY = -(inputHeight + hintHeight + TooltipGap);
            _parameterHints.Constraint.OffsetX = Padding; // Align with left edge of content
        }
        else
        {
            _parameterHints.Constraint.Height = 0; // Hide when not active
        }

        // Update documentation popup
        if (_overlayMode == ConsoleOverlayMode.Documentation && _documentationPopup.HasContent)
        {
            var docHeight = _documentationPopup.GetDesiredHeight(context.Renderer);
            _documentationPopup.Constraint.Height = docHeight;
        }
        else
        {
            _documentationPopup.Constraint.Height = 0; // Hide when not active
        }

        // Calculate layout heights based on the actual constraint padding
        var paddingTop = Constraint.GetPaddingTop();
        var paddingBottom = Constraint.GetPaddingBottom();
        var contentHeight = Rect.Height - paddingTop - paddingBottom; // Use actual constraint padding, not static Padding property
        var outputHeight = contentHeight - inputHeight - hintHeight - ComponentSpacing; // Leave space for input + hints + gap

        Console.WriteLine($"[ConsolePanel] Layout calculation: Rect.Height={Rect.Height}, paddingTop={paddingTop}, paddingBottom={paddingBottom}, contentHeight={contentHeight}, inputHeight={inputHeight}, hintHeight={hintHeight}, outputHeight={outputHeight}");

        // Update output buffer height
        _outputBuffer.Constraint.Height = outputHeight;

        // Handle input for suggestions navigation BEFORE rendering
        var input = context.Input;
        if (input != null)
        {
            // Handle Ctrl+F - Open output search
            if (input.IsCtrlDown() && input.IsKeyPressed(Keys.F))
            {
                ToggleSearch();
                input.ConsumeKey(Keys.F);
            }

            // Handle Ctrl+R - Toggle command history search
            if (input.IsCtrlDown() && input.IsKeyPressed(Keys.R))
            {
                ToggleCommandHistorySearch();
                input.ConsumeKey(Keys.R);
            }

            // Handle F1 - Request documentation for selected suggestion
            if (input.IsKeyPressed(Keys.F1) && _overlayMode == ConsoleOverlayMode.Suggestions)
            {
                var selectedItem = _suggestionsDropdown.SelectedItem;
                if (selectedItem != null)
                {
                    OnRequestDocumentation?.Invoke(selectedItem.Text);
                    input.ConsumeKey(Keys.F1);
                }
            }

            // Handle Ctrl+Up/Down - Cycle console size OR parameter hint overloads
            if (input.IsCtrlDown() && input.IsKeyPressed(Keys.Up))
            {
                // If parameter hints are showing, cycle through overloads
                if (_overlayMode == ConsoleOverlayMode.ParameterHints && _parameterHints.HasContent)
                {
                    PreviousParameterHintOverload();
                    input.ConsumeKey(Keys.Up);
                }
                else
                {
                    CycleSizeUp();
                    input.ConsumeKey(Keys.Up);
                }
            }
            else if (input.IsCtrlDown() && input.IsKeyPressed(Keys.Down))
            {
                // If parameter hints are showing, cycle through overloads
                if (_overlayMode == ConsoleOverlayMode.ParameterHints && _parameterHints.HasContent)
                {
                    NextParameterHintOverload();
                    input.ConsumeKey(Keys.Down);
                }
                else
                {
                    CycleSizeDown();
                    input.ConsumeKey(Keys.Down);
                }
            }

            // Handle special keyboard shortcuts
            if (input.IsKeyPressed(Keys.Escape))
            {
                HandleEscape();
                input.ConsumeKey(Keys.Escape); // Consume so child components don't also handle it
            }

            // Handle suggestions keyboard navigation when visible
            if (_overlayMode == ConsoleOverlayMode.Suggestions || _overlayMode == ConsoleOverlayMode.CommandHistorySearch)
            {
                // Up/Down with repeat for smooth navigation
                if (input.IsKeyPressedWithRepeat(Keys.Down))
                {
                    _suggestionsDropdown.SelectNext();
                    input.ConsumeKey(Keys.Down); // Consume to prevent command history navigation
                }
                else if (input.IsKeyPressedWithRepeat(Keys.Up))
                {
                    _suggestionsDropdown.SelectPrevious();
                    input.ConsumeKey(Keys.Up); // Consume to prevent command history navigation
                }
                // Enter - accept suggestion if one is selected
                else if (input.IsKeyPressed(Keys.Enter))
                {
                    // Only consume Enter if we have a valid suggestion to accept
                    if (_suggestionsDropdown.SelectedItem != null)
                    {
                        _suggestionsDropdown.AcceptSelected();
                        input.ConsumeKey(Keys.Enter); // Consume to prevent command submission
                    }
                    else
                    {
                        // Don't consume - let CommandInput handle it normally
                    }
                }
            }
        }

        // Draw panel background with Y offset applied
        // NOTE: For now, we draw without offset since we can't easily transform children
        // Animation is functional but visual transform needs SpriteBatch.Begin with Matrix transform
        // which would require UIRenderer enhancement
        base.OnRenderContainer(context);

        // Auto-focus management when console is visible
        if (Visible)
        {
            if (_overlayMode == ConsoleOverlayMode.Search)
            {
                // Focus search bar when in search mode
                if (context.Frame.FocusedComponentId != _searchBar.Id)
                {
                    context.SetFocus(_searchBar.Id);
                }
            }
            else
            {
                // Focus command editor in normal mode
                if (context.Frame.FocusedComponentId != _commandEditor.Id)
                {
                    context.SetFocus(_commandEditor.Id);
                }
            }
        }

        // Don't render suggestions dropdown unless visible
        if (_overlayMode != ConsoleOverlayMode.Suggestions && _overlayMode != ConsoleOverlayMode.CommandHistorySearch)
        {
            // Remove suggestions from rendering
            _suggestionsDropdown.Constraint.Height = 0;
        }
        else
        {
            // Calculate suggestions height using the dropdown's item height
            var itemCount = Math.Min(_suggestionsDropdown.MaxVisibleItems, _suggestionsDropdown.ItemCount);
            var calculatedHeight = itemCount * (int)_suggestionsDropdown.ItemHeight + 20; // Item height + padding
            _suggestionsDropdown.Constraint.Height = calculatedHeight;
        }
    }

}

