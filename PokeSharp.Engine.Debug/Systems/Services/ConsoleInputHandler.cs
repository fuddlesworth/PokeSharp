using Microsoft.Extensions.Logging;
using Microsoft.Xna.Framework.Input;
using Microsoft.Xna.Framework;
using PokeSharp.Engine.Debug.Console.Configuration;
using PokeSharp.Engine.Debug.Console.Features;
using PokeSharp.Engine.Debug.Console.UI;
using TextCopy;
using static PokeSharp.Engine.Debug.Console.Configuration.ConsoleColors;

namespace PokeSharp.Engine.Debug.Systems.Services;

/// <summary>
/// Handles keyboard and mouse input for the console.
/// Extracted from ConsoleSystem to follow Single Responsibility Principle.
/// </summary>
public class ConsoleInputHandler : IConsoleInputHandler
{
    private readonly QuakeConsole _console;
    private readonly ConsoleCommandHistory _history;
    private readonly ConsoleHistoryPersistence _persistence;
    private readonly ILogger _logger;
    private readonly IConsoleAutoCompleteCoordinator? _autoCompleteCoordinator;
    private readonly DocumentationProvider _documentationProvider;
    private readonly ParameterHintProvider? _parameterHintProvider;
    private readonly BookmarkedCommandsManager? _bookmarksManager;

    // Key repeat tracking for smooth editing
    private Keys? _lastHeldKey;
    private float _keyHoldTime;
    private float _lastKeyRepeatTime;
    private const float InitialKeyRepeatDelay = ConsoleConstants.Input.InitialKeyRepeatDelay;
    private const float KeyRepeatInterval = ConsoleConstants.Input.KeyRepeatInterval;

    // Key repeat tracking for suggestion scrolling (separate from text input)
    private Keys? _lastHeldScrollKey;
    private float _scrollKeyHoldTime;
    private float _lastScrollKeyRepeatTime;

    // Key repeat tracking for suggestion navigation (Up/Down)
    private Keys? _lastHeldNavKey;
    private float _navKeyHoldTime;
    private float _lastNavKeyRepeatTime;

    // Debouncing for filtering to prevent excessive operations
    private string _lastFilteredText = "";
    private const float FilterDebounceDelay = 0.05f; // 50ms debounce

    // Mouse click detection (immediate processing on button press)
    // No need for time window - clicks are processed instantly on button press

    // Mouse drag selection state
    private bool _isDragging = false;
    private Point _dragStartPosition;
    private int _dragStartCharPosition = -1;
    private Point _lastDragPosition;

    /// <summary>
    /// Initializes a new instance of the ConsoleInputHandler.
    /// </summary>
    public ConsoleInputHandler(
        QuakeConsole console,
        ConsoleCommandHistory history,
        ConsoleHistoryPersistence persistence,
        ILogger logger,
        IConsoleAutoCompleteCoordinator? autoCompleteCoordinator = null,
        ParameterHintProvider? parameterHintProvider = null,
        BookmarkedCommandsManager? bookmarksManager = null)
    {
        _console = console ?? throw new ArgumentNullException(nameof(console));
        _history = history ?? throw new ArgumentNullException(nameof(history));
        _persistence = persistence ?? throw new ArgumentNullException(nameof(persistence));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        _autoCompleteCoordinator = autoCompleteCoordinator;
        _parameterHintProvider = parameterHintProvider;
        _bookmarksManager = bookmarksManager;
        _documentationProvider = new DocumentationProvider();
    }

    /// <summary>
    /// Handles input for the current frame.
    /// </summary>
    public InputHandlingResult HandleInput(
        float deltaTime,
        KeyboardState keyboardState,
        KeyboardState previousKeyboardState,
        MouseState mouseState,
        MouseState previousMouseState)
    {
        var isShiftPressed = keyboardState.IsKeyDown(Keys.LeftShift) || keyboardState.IsKeyDown(Keys.RightShift);

        // Toggle console with backtick (`) key only
        if (WasKeyJustPressed(Keys.OemTilde, keyboardState, previousKeyboardState))
        {
            _console.Toggle();
            _logger.LogInformation("Console toggled: {IsVisible}", _console.IsVisible);

            // Save history when closing console
            if (!_console.IsVisible && _console.Config.PersistHistory)
            {
                _persistence.SaveHistory(_history.GetAll());
            }

            return InputHandlingResult.Consumed;
        }

        // Only process console input if visible
        if (!_console.IsVisible)
        {
            return InputHandlingResult.None;
        }

        // Update mouse position for hover effects
        _console.UpdateMousePosition(new Point(mouseState.X, mouseState.Y));

        // Handle mouse clicks on console output (for section toggling)
        var mouseResult = HandleMouseInput(mouseState, previousMouseState, deltaTime);
        if (mouseResult != InputHandlingResult.None)
            return mouseResult;

        // Handle Enter key - accept reverse-i-search OR suggestion OR execute command (not with Shift for multi-line)
        if (WasKeyJustPressed(Keys.Enter, keyboardState, previousKeyboardState) && !isShiftPressed)
        {
            // Check if reverse-i-search is active
            if (_console.IsReverseSearchMode)
            {
                _console.AcceptReverseSearchMatch();
                _logger.LogInformation("Accepted reverse-i-search match");
                // Re-evaluate parameter hints for the accepted command
                UpdateParameterHints();
                return InputHandlingResult.Consumed;
            }

            // Check if auto-complete suggestions are visible
            if (_console.HasSuggestions())
            {
                // Accept the selected suggestion
                var suggestion = _console.GetSelectedSuggestion();
                if (suggestion != null)
                {
                    _logger.LogInformation("Enter pressed - accepting auto-complete suggestion: {Suggestion}", suggestion);
                    InsertCompletion(suggestion);
                }
                _console.ClearAutoCompleteSuggestions();
                _lastFilteredText = ""; // Reset debounce tracking after accepting
                return InputHandlingResult.Consumed;
            }

            // No suggestions, execute command normally
            // Save history before executing
            if (_console.Config.PersistHistory)
            {
                _persistence.SaveHistory(_history.GetAll());
            }

            var command = _console.GetInputText();
            _console.ClearAutoCompleteSuggestions();
            _console.ClearParameterHints(); // Clear parameter hints when executing command
            return InputHandlingResult.Execute(command);
        }

        // Handle Tab - accept auto-complete suggestion OR trigger if none shown
        if (WasKeyJustPressed(Keys.Tab, keyboardState, previousKeyboardState) && _console.Config.AutoCompleteEnabled)
        {
            _logger.LogInformation("Tab key pressed! Auto-complete enabled: {Enabled}", _console.Config.AutoCompleteEnabled);

            var suggestion = _console.GetSelectedSuggestion();
            _logger.LogInformation("Current selected suggestion: {Suggestion}", suggestion ?? "null");

            if (suggestion != null)
            {
                // Accept selected suggestion - insert it intelligently
                _logger.LogInformation("Accepting suggestion: {Suggestion}", suggestion);
                InsertCompletion(suggestion);
                _console.ClearAutoCompleteSuggestions();
                _lastFilteredText = ""; // Reset debounce tracking after accepting
            }
            else
            {
                // No suggestions shown, trigger auto-complete
                _logger.LogInformation("No suggestions, triggering auto-complete...");
                return InputHandlingResult.TriggerAutoComplete();
            }
            return InputHandlingResult.Consumed;
        }

        // Handle Ctrl/Cmd key combinations
        bool isCtrlPressed = keyboardState.IsKeyDown(Keys.LeftControl) || keyboardState.IsKeyDown(Keys.RightControl);
        bool isCmdPressed = keyboardState.IsKeyDown(Keys.LeftWindows) || keyboardState.IsKeyDown(Keys.RightWindows);

        // Handle Ctrl/Cmd+A - select all
        if (WasKeyJustPressed(Keys.A, keyboardState, previousKeyboardState) && (isCtrlPressed || isCmdPressed))
        {
            _console.Input.SelectAll();
            _logger.LogInformation("Select all triggered");
            return InputHandlingResult.Consumed;
        }

        // Handle Ctrl/Cmd+C - copy
        if (WasKeyJustPressed(Keys.C, keyboardState, previousKeyboardState) && (isCtrlPressed || isCmdPressed))
        {
            if (_console.Input.HasSelection)
            {
                try
                {
                    var selectedText = _console.Input.SelectedText;
                    ClipboardService.SetText(selectedText);
                    _logger.LogInformation("Copied {Length} characters to clipboard", selectedText.Length);
                }
                catch (Exception ex)
                {
                    _logger.LogWarning(ex, "Failed to copy to clipboard");
                    _console.AppendOutput("Failed to copy to clipboard", Output_Warning);
                }
            }
            return InputHandlingResult.Consumed;
        }

        // Handle Ctrl/Cmd+X - cut
        if (WasKeyJustPressed(Keys.X, keyboardState, previousKeyboardState) && (isCtrlPressed || isCmdPressed))
        {
            if (_console.Input.HasSelection)
            {
                try
                {
                    var selectedText = _console.Input.SelectedText;
                    ClipboardService.SetText(selectedText);
                    _console.Input.DeleteSelection();
                    _logger.LogInformation("Cut {Length} characters to clipboard", selectedText.Length);

                    // Clear suggestions after cut
                    if (_console.HasSuggestions())
                    {
                        _console.ClearAutoCompleteSuggestions();
                        _lastFilteredText = "";
                    }

                    // Re-evaluate parameter hints after cut
                    UpdateParameterHints();
                }
                catch (Exception ex)
                {
                    _logger.LogWarning(ex, "Failed to cut to clipboard");
                    _console.AppendOutput("Failed to cut to clipboard", Output_Warning);
                }
            }
            return InputHandlingResult.Consumed;
        }

        // Handle Ctrl/Cmd+V - paste from clipboard
        if (WasKeyJustPressed(Keys.V, keyboardState, previousKeyboardState) && (isCtrlPressed || isCmdPressed))
        {
            try
            {
                var clipboardText = ClipboardService.GetText();
                if (!string.IsNullOrEmpty(clipboardText))
                {
                    _logger.LogInformation("Pasting {Length} characters from clipboard", clipboardText.Length);
                    _console.Input.InsertText(clipboardText);

                    // Clear suggestions after paste
                    if (_console.HasSuggestions())
                    {
                        _console.ClearAutoCompleteSuggestions();
                        _lastFilteredText = ""; // Reset debounce tracking
                    }

                    // Re-evaluate parameter hints after paste
                    UpdateParameterHints();
                }
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Failed to paste from clipboard");
                _console.AppendOutput("Failed to paste from clipboard", Output_Warning);
            }
            return InputHandlingResult.Consumed;
        }

        // Handle Ctrl+Space - explicitly trigger auto-complete
        if (WasKeyJustPressed(Keys.Space, keyboardState, previousKeyboardState) &&
            (keyboardState.IsKeyDown(Keys.LeftControl) || keyboardState.IsKeyDown(Keys.RightControl)) &&
            _console.Config.AutoCompleteEnabled)
        {
            return InputHandlingResult.TriggerAutoComplete();
        }

        // Handle font size changes
        var fontSizeResult = HandleFontSizeChanges(keyboardState, previousKeyboardState, isShiftPressed, isCtrlPressed, isCmdPressed);
        if (fontSizeResult != InputHandlingResult.None)
            return fontSizeResult;

        // Handle Ctrl/Cmd+Z - undo
        if (WasKeyJustPressed(Keys.Z, keyboardState, previousKeyboardState) &&
            (isCtrlPressed || isCmdPressed) && !isShiftPressed)
        {
            if (_console.Input.Undo())
            {
                _logger.LogDebug("Undo performed");
                // Re-evaluate parameter hints after undo
                UpdateParameterHints();
                return InputHandlingResult.Consumed;
            }
            return InputHandlingResult.Consumed; // Consume even if no undo available
        }

        // Handle Ctrl/Cmd+Y (or Ctrl/Cmd+Shift+Z) - redo
        if (((WasKeyJustPressed(Keys.Y, keyboardState, previousKeyboardState) && (isCtrlPressed || isCmdPressed)) ||
             (WasKeyJustPressed(Keys.Z, keyboardState, previousKeyboardState) && (isCtrlPressed || isCmdPressed) && isShiftPressed)))
        {
            if (_console.Input.Redo())
            {
                _logger.LogDebug("Redo performed");
                // Re-evaluate parameter hints after redo
                UpdateParameterHints();
                return InputHandlingResult.Consumed;
            }
            return InputHandlingResult.Consumed; // Consume even if no redo available
        }

        // Handle Ctrl/Cmd+F - open search mode
        if (WasKeyJustPressed(Keys.F, keyboardState, previousKeyboardState) &&
            (isCtrlPressed || isCmdPressed))
        {
            _console.StartSearch();
            _logger.LogInformation("Search mode activated");
            return InputHandlingResult.Consumed;
        }

        // Handle Ctrl/Cmd+R - open reverse-i-search mode
        if (WasKeyJustPressed(Keys.R, keyboardState, previousKeyboardState) &&
            (isCtrlPressed || isCmdPressed) && !_console.IsReverseSearchMode)
        {
            _console.StartReverseSearch();
            _logger.LogInformation("Reverse-i-search mode activated");
            return InputHandlingResult.Consumed;
        }

        // Handle Ctrl/Cmd+R in reverse-i-search - cycle to next match
        if (_console.IsReverseSearchMode &&
            WasKeyJustPressed(Keys.R, keyboardState, previousKeyboardState) &&
            (isCtrlPressed || isCmdPressed))
        {
            _console.ReverseSearchNextMatch();
            _logger.LogDebug("Reverse-i-search next match");
            return InputHandlingResult.Consumed;
        }

        // Handle Ctrl/Cmd+S in reverse-i-search - cycle to previous match
        if (_console.IsReverseSearchMode &&
            WasKeyJustPressed(Keys.S, keyboardState, previousKeyboardState) &&
            (isCtrlPressed || isCmdPressed))
        {
            _console.ReverseSearchPreviousMatch();
            _logger.LogDebug("Reverse-i-search previous match");
            return InputHandlingResult.Consumed;
        }

        // Handle function keys (F1-F12)
        var functionKeyResult = HandleFunctionKeys(keyboardState, previousKeyboardState, isShiftPressed);
        if (functionKeyResult != InputHandlingResult.None)
            return functionKeyResult;

        // Handle Escape - close documentation OR suggestions OR exit search modes OR close console
        // Handle Ctrl+Shift+Up/Down - cycle through parameter hint overloads
        bool isCtrlShiftPressed = (keyboardState.IsKeyDown(Keys.LeftControl) || keyboardState.IsKeyDown(Keys.RightControl)) &&
                                   (keyboardState.IsKeyDown(Keys.LeftShift) || keyboardState.IsKeyDown(Keys.RightShift));

        if (isCtrlShiftPressed && _console.HasParameterHints())
        {
            if (WasKeyJustPressed(Keys.Up, keyboardState, previousKeyboardState))
            {
                _console.PreviousParameterHintOverload();
                _logger.LogDebug("Cycled to previous parameter hint overload");
                return InputHandlingResult.Consumed;
            }
            else if (WasKeyJustPressed(Keys.Down, keyboardState, previousKeyboardState))
            {
                _console.NextParameterHintOverload();
                _logger.LogDebug("Cycled to next parameter hint overload");
                return InputHandlingResult.Consumed;
            }
        }

        if (WasKeyJustPressed(Keys.Escape, keyboardState, previousKeyboardState))
        {
            // Check if parameter hints are showing
            if (_console.HasParameterHints())
            {
                _console.ClearParameterHints();
                _logger.LogDebug("Closed parameter hints");
                return InputHandlingResult.Consumed;
            }

            // Check if documentation is showing
            if (_console.IsShowingDocumentation)
            {
                _console.HideDocumentation();
                _logger.LogDebug("Closed documentation popup");
                return InputHandlingResult.Consumed;
            }

            // Check if reverse-i-search mode is active
            if (_console.IsReverseSearchMode)
            {
                _console.ExitReverseSearch();
                _logger.LogInformation("Escape pressed - exiting reverse-i-search mode");
                return InputHandlingResult.Consumed;
            }

            // Check if search mode is active
            if (_console.IsSearchMode)
            {
                _console.ExitSearch();
                _logger.LogInformation("Escape pressed - exiting search mode");
                return InputHandlingResult.Consumed;
            }

            // Check if auto-complete suggestions are visible
            if (_console.HasSuggestions())
            {
                // Just close the suggestions, don't close the console
                _logger.LogInformation("Escape pressed - closing auto-complete suggestions");
                _console.ClearAutoCompleteSuggestions();
                _lastFilteredText = ""; // Reset debounce tracking
                return InputHandlingResult.Consumed;
            }

            // No suggestions, close the console
            _console.Hide();

            // Save history when closing
            if (_console.Config.PersistHistory)
            {
                _persistence.SaveHistory(_history.GetAll());
            }

            return InputHandlingResult.Consumed;
        }

        // Handle PageUp - scroll suggestions OR scroll output up
        if (WasKeyJustPressed(Keys.PageUp, keyboardState, previousKeyboardState))
        {
            if (_console.HasSuggestions())
            {
                // Scroll suggestions up by multiple items (5 at a time)
                _console.ScrollSuggestions(-5);
                return InputHandlingResult.Consumed;
            }
            else
        {
            _console.Output.PageUp();
            return InputHandlingResult.Consumed;
            }
        }

        // Handle PageDown - scroll suggestions OR scroll output down
        if (WasKeyJustPressed(Keys.PageDown, keyboardState, previousKeyboardState))
        {
            if (_console.HasSuggestions())
            {
                // Scroll suggestions down by multiple items (5 at a time)
                _console.ScrollSuggestions(5);
                return InputHandlingResult.Consumed;
            }
            else
        {
            _console.Output.PageDown();
            return InputHandlingResult.Consumed;
            }
        }

        // Handle Ctrl+Home - scroll to top
        if (WasKeyJustPressed(Keys.Home, keyboardState, previousKeyboardState) &&
            (keyboardState.IsKeyDown(Keys.LeftControl) || keyboardState.IsKeyDown(Keys.RightControl)))
        {
            _console.Output.ScrollToTop();
            return InputHandlingResult.Consumed;
        }

        // Handle Ctrl+End - scroll to bottom
        if (WasKeyJustPressed(Keys.End, keyboardState, previousKeyboardState) &&
            (keyboardState.IsKeyDown(Keys.LeftControl) || keyboardState.IsKeyDown(Keys.RightControl)))
        {
            _console.Output.ScrollToBottom();
            return InputHandlingResult.Consumed;
        }

        // Handle Ctrl/Cmd+Left (+ Shift for selection) - move to previous word
        if (WasKeyJustPressed(Keys.Left, keyboardState, previousKeyboardState) && (isCtrlPressed || isCmdPressed))
        {
            _console.Input.MoveToPreviousWord(extendSelection: isShiftPressed);
            return InputHandlingResult.Consumed;
        }

        // Handle Ctrl/Cmd+Right (+ Shift for selection) - move to next word
        if (WasKeyJustPressed(Keys.Right, keyboardState, previousKeyboardState) && (isCtrlPressed || isCmdPressed))
        {
            _console.Input.MoveToNextWord(extendSelection: isShiftPressed);
            return InputHandlingResult.Consumed;
        }

        // Handle Ctrl+Backspace (Cmd+Backspace on macOS) - delete previous word
        if (WasKeyJustPressed(Keys.Back, keyboardState, previousKeyboardState) && (isCtrlPressed || isCmdPressed))
        {
            _console.Input.DeleteWordBackward();

            // Clear suggestions if present
            if (_console.HasSuggestions())
            {
                _console.ClearAutoCompleteSuggestions();
                _lastFilteredText = "";
            }

            // Re-evaluate parameter hints after deletion
            UpdateParameterHints();

            return InputHandlingResult.Consumed;
        }

        // Handle Ctrl+Delete (Cmd+Delete on macOS) - delete next word
        if (WasKeyJustPressed(Keys.Delete, keyboardState, previousKeyboardState) && (isCtrlPressed || isCmdPressed))
        {
            _console.Input.DeleteWordForward();

            // Clear suggestions if present
            if (_console.HasSuggestions())
            {
                _console.ClearAutoCompleteSuggestions();
                _lastFilteredText = "";
            }

            // Re-evaluate parameter hints after deletion
            UpdateParameterHints();

            return InputHandlingResult.Consumed;
        }

        // Handle Alt+[ and Alt+] - Collapse/Expand all sections
        var isAltPressed = keyboardState.IsKeyDown(Keys.LeftAlt) || keyboardState.IsKeyDown(Keys.RightAlt);
        if (isAltPressed)
        {
            // Alt+[ - Collapse all sections
            if (WasKeyJustPressed(Keys.OemOpenBrackets, keyboardState, previousKeyboardState))
            {
                _console.Output.CollapseAllSections();
                _logger.LogDebug("Collapsed all sections");
                return InputHandlingResult.Consumed;
            }

            // Alt+] - Expand all sections
            if (WasKeyJustPressed(Keys.OemCloseBrackets, keyboardState, previousKeyboardState))
            {
                _console.Output.ExpandAllSections();
                _logger.LogDebug("Expanded all sections");
                return InputHandlingResult.Consumed;
            }
        }

        // Handle Mouse Wheel - scroll output
        int scrollDelta = mouseState.ScrollWheelValue - previousMouseState.ScrollWheelValue;
        if (scrollDelta != 0)
        {
            int scrollLines = scrollDelta / ConsoleConstants.Limits.MouseWheelUnitsPerNotch * ConsoleConstants.Limits.ScrollLinesPerNotch;
            if (scrollLines > 0)
            {
                for (int i = 0; i < scrollLines; i++)
                    _console.Output.ScrollUp();
            }
            else if (scrollLines < 0)
            {
                for (int i = 0; i < -scrollLines; i++)
                    _console.Output.ScrollDown();
            }
            return InputHandlingResult.Consumed;
        }

        // Handle Up/Down arrow with key repeat for suggestion navigation OR command history
        // This consumes the input if suggestions are visible
        var navResult = HandleSuggestionNavigation(keyboardState, previousKeyboardState, deltaTime);
        if (navResult == InputHandlingResult.Consumed)
        {
            return InputHandlingResult.Consumed;
        }

        // Handle Left/Right arrow with key repeat for suggestion scrolling
        // This consumes the input if suggestions are visible
        var scrollResult = HandleSuggestionScrolling(keyboardState, previousKeyboardState, deltaTime);
        if (scrollResult == InputHandlingResult.Consumed)
        {
            return InputHandlingResult.Consumed;
        }

        // Handle text input with key repeat
        HandleTextInput(keyboardState, previousKeyboardState, deltaTime);

        return InputHandlingResult.None;
    }

    /// <summary>
    /// Handles text input with key repeat support.
    /// </summary>
    private void HandleTextInput(KeyboardState keyboardState, KeyboardState previousKeyboardState, float deltaTime)
    {
        var pressedKeys = keyboardState.GetPressedKeys();

        foreach (var key in pressedKeys)
        {
            if (!ShouldProcessKey(key, previousKeyboardState, deltaTime))
                continue;

            bool isShiftPressed = keyboardState.IsKeyDown(Keys.LeftShift) || keyboardState.IsKeyDown(Keys.RightShift);
            char? character = KeyToCharConverter.ToChar(key, isShiftPressed);

            ProcessKeyPress(key, character, isShiftPressed);
        }

        // Reset key repeat if no keys are pressed
        if (pressedKeys.Length == 0)
        {
            ResetKeyRepeatState();
        }
    }

    /// <summary>
    /// Determines if a key should be processed based on key repeat timing.
    /// </summary>
    private bool ShouldProcessKey(Keys key, KeyboardState previousKeyboardState, float deltaTime)
    {
        // Check if key was just pressed
        if (!previousKeyboardState.IsKeyDown(key))
        {
            _lastHeldKey = key;
            _keyHoldTime = 0;
            _lastKeyRepeatTime = 0;
            return true;
        }

        // Check for key repeat
        if (_lastHeldKey != key)
            return false;

        _keyHoldTime += deltaTime;

        // Start repeating after initial delay
        if (_keyHoldTime < InitialKeyRepeatDelay)
            return false;

        _lastKeyRepeatTime += deltaTime;

        // Repeat at interval
        if (_lastKeyRepeatTime < KeyRepeatInterval)
            return false;

        _lastKeyRepeatTime = 0;
        return true;
    }

    /// <summary>
    /// Processes a single key press and routes to the appropriate handler.
    /// </summary>
    private void ProcessKeyPress(Keys key, char? character, bool isShiftPressed)
    {
        // Handle reverse-i-search mode input
        if (_console.IsReverseSearchMode)
        {
            HandleReverseSearchModeInput(key, character);
            return;
        }

        // Handle search mode input
        if (_console.IsSearchMode)
        {
            HandleSearchModeInput(key, character);
            return;
        }

        // Normal input handling
        HandleNormalModeInput(key, character, isShiftPressed);
    }

    /// <summary>
    /// Handles input in normal mode (not search or reverse-i-search).
    /// </summary>
    private void HandleNormalModeInput(Keys key, char? character, bool isShiftPressed)
    {
        _console.Input.HandleKeyPress(key, character, isShiftPressed);

        // Notify auto-complete coordinator that user is typing
        if (character.HasValue && !char.IsControl(character.Value))
        {
            _autoCompleteCoordinator?.NotifyTyping();
        }

        UpdateParameterHintsForKeyPress(key, character);
        UpdateAutoCompleteSuggestionsForKeyPress(key, character);
    }

    /// <summary>
    /// Updates parameter hints based on the key press.
    /// </summary>
    private void UpdateParameterHintsForKeyPress(Keys key, char? character)
    {
        if (!character.HasValue)
        {
            if (key == Keys.Back || key == Keys.Delete)
                UpdateParameterHints();
            return;
        }

        // Update parameter hints when typing '(' or ','
        if (character.Value == '(' || character.Value == ',')
        {
            UpdateParameterHints();
        }
        // Clear parameter hints when typing ')'
        else if (character.Value == ')')
        {
            _console.ClearParameterHints();
        }
    }

    /// <summary>
    /// Updates auto-complete suggestions based on the key press.
    /// </summary>
    private void UpdateAutoCompleteSuggestionsForKeyPress(Keys key, char? character)
    {
        if (!_console.HasSuggestions())
            return;

        // Clear suggestions on certain keys
        if (ShouldClearSuggestions(key))
        {
            _console.ClearAutoCompleteSuggestions();
            _lastFilteredText = "";
            return;
        }

        // Filter suggestions as user types
        if (character.HasValue && !char.IsControl(character.Value))
        {
            var currentText = _console.GetInputText();

            // Only filter if text has actually changed (debounce check)
            if (currentText != _lastFilteredText)
            {
                _console.FilterSuggestions(currentText);
                _lastFilteredText = currentText;
            }
        }
    }

    /// <summary>
    /// Resets the key repeat tracking state.
    /// </summary>
    private void ResetKeyRepeatState()
    {
        _lastHeldKey = null;
        _keyHoldTime = 0;
        _lastKeyRepeatTime = 0;
    }

    /// <summary>
    /// Handles left/right arrow scrolling in autocomplete suggestions with key repeat support.
    /// </summary>
    /// <returns>InputHandlingResult indicating if the input was consumed.</returns>
    /// <summary>
    /// Handles Up/Down arrow navigation through suggestions with key repeat support.
    /// Also handles command history navigation when no suggestions are visible.
    /// </summary>
    private InputHandlingResult HandleSuggestionNavigation(KeyboardState keyboardState, KeyboardState previousKeyboardState, float deltaTime)
    {
        Keys? currentNavKey = null;

        // Determine which navigation key is pressed
        if (keyboardState.IsKeyDown(Keys.Up))
            currentNavKey = Keys.Up;
        else if (keyboardState.IsKeyDown(Keys.Down))
            currentNavKey = Keys.Down;

        if (currentNavKey.HasValue)
        {
            bool shouldNavigate = false;

            // Check if key was just pressed
            if (!previousKeyboardState.IsKeyDown(currentNavKey.Value))
            {
                _lastHeldNavKey = currentNavKey.Value;
                _navKeyHoldTime = 0;
                _lastNavKeyRepeatTime = 0;
                shouldNavigate = true;
            }
            // Check for key repeat
            else if (_lastHeldNavKey == currentNavKey.Value)
            {
                _navKeyHoldTime += deltaTime;

                // Start repeating after initial delay
                if (_navKeyHoldTime >= InitialKeyRepeatDelay)
                {
                    _lastNavKeyRepeatTime += deltaTime;

                    // Repeat at interval
                    if (_lastNavKeyRepeatTime >= KeyRepeatInterval)
                    {
                        shouldNavigate = true;
                        _lastNavKeyRepeatTime = 0;
                    }
                }
            }

            if (shouldNavigate)
            {
                if (_console.HasSuggestions())
                {
                    // Navigate auto-complete suggestions
                    if (currentNavKey.Value == Keys.Up)
                    {
                        _console.NavigateSuggestions(up: true);
                    }
                    else if (currentNavKey.Value == Keys.Down)
                    {
                        _console.NavigateSuggestions(up: false);
                    }
                }
                else
                {
                    // Navigate command history
                    if (currentNavKey.Value == Keys.Up)
                    {
                        var prevCommand = _history.NavigateUp();
                        if (prevCommand != null)
                        {
                            _console.Input.SetText(prevCommand);
                            // Re-evaluate parameter hints for the new command
                            UpdateParameterHints();
                        }
                    }
                    else if (currentNavKey.Value == Keys.Down)
                    {
                        var nextCommand = _history.NavigateDown();
                        if (nextCommand != null)
                        {
                            _console.Input.SetText(nextCommand);
                            // Re-evaluate parameter hints for the new command
                            UpdateParameterHints();
                        }
                    }
                }
            }

            // Consume the input to prevent other actions
            return InputHandlingResult.Consumed;
        }
        else
        {
            // Reset navigation key repeat if no nav keys are pressed
            _lastHeldNavKey = null;
            _navKeyHoldTime = 0;
            _lastNavKeyRepeatTime = 0;
            return InputHandlingResult.None;
        }
    }

    private InputHandlingResult HandleSuggestionScrolling(KeyboardState keyboardState, KeyboardState previousKeyboardState, float deltaTime)
    {
        // Only handle if suggestions are visible
        if (!_console.HasSuggestions())
        {
            // Reset scroll key repeat state when no suggestions
            _lastHeldScrollKey = null;
            _scrollKeyHoldTime = 0;
            _lastScrollKeyRepeatTime = 0;
            return InputHandlingResult.None;
        }

        Keys? currentScrollKey = null;

        // Determine which scroll key is pressed
        if (keyboardState.IsKeyDown(Keys.Left))
            currentScrollKey = Keys.Left;
        else if (keyboardState.IsKeyDown(Keys.Right))
            currentScrollKey = Keys.Right;

        if (currentScrollKey.HasValue)
        {
            bool shouldScroll = false;

            // Check if key was just pressed
            if (!previousKeyboardState.IsKeyDown(currentScrollKey.Value))
            {
                _lastHeldScrollKey = currentScrollKey.Value;
                _scrollKeyHoldTime = 0;
                _lastScrollKeyRepeatTime = 0;
                shouldScroll = true;
            }
            // Check for key repeat
            else if (_lastHeldScrollKey == currentScrollKey.Value)
            {
                _scrollKeyHoldTime += deltaTime;

                // Start repeating after initial delay
                if (_scrollKeyHoldTime >= InitialKeyRepeatDelay)
                {
                    _lastScrollKeyRepeatTime += deltaTime;

                    // Repeat at interval
                    if (_lastScrollKeyRepeatTime >= KeyRepeatInterval)
                    {
                        shouldScroll = true;
                        _lastScrollKeyRepeatTime = 0;
                    }
                }
            }

            if (shouldScroll)
            {
                if (currentScrollKey.Value == Keys.Left)
                {
                    _console.ScrollSuggestionRight(); // Scroll right (show more of beginning)
                }
                else if (currentScrollKey.Value == Keys.Right)
                {
                    _console.ScrollSuggestionLeft(); // Scroll left (show more of end)
                }
            }

            // Consume the input to prevent cursor movement in input field
            return InputHandlingResult.Consumed;
        }
        else
        {
            // Reset scroll key repeat if no scroll keys are pressed
            _lastHeldScrollKey = null;
            _scrollKeyHoldTime = 0;
            _lastScrollKeyRepeatTime = 0;
            return InputHandlingResult.None;
        }
    }

    /// <summary>
    /// Checks if a key was just pressed (down now, up before).
    /// </summary>
    private static bool WasKeyJustPressed(Keys key, KeyboardState current, KeyboardState previous)
    {
        return current.IsKeyDown(key) && !previous.IsKeyDown(key);
    }

    /// <summary>
    /// Updates parameter hints based on the current input.
    /// </summary>
    private void UpdateParameterHints()
    {
        if (_parameterHintProvider == null)
            return;

        var inputText = _console.GetInputText();
        var cursorPos = _console.Input.CursorPosition;

        // Get parameter hints from provider
        var hints = _parameterHintProvider.GetParameterHints(inputText, cursorPos);

        if (hints != null)
        {
            // Count commas from the opening parenthesis to determine current parameter
            int currentParamIndex = CountParameterIndex(inputText, cursorPos);
            _console.SetParameterHints(hints, currentParamIndex);
        }
        else
        {
            _console.ClearParameterHints();
        }
    }

    /// <summary>
    /// Counts which parameter the cursor is currently on by counting commas.
    /// </summary>
    private int CountParameterIndex(string text, int cursorPos)
    {
        if (string.IsNullOrEmpty(text) || cursorPos <= 0)
            return 0;

        int openParenPos = FindOpeningParenthesis(text, cursorPos);
        if (openParenPos == -1)
            return 0;

        return CountCommasInParameterList(text, openParenPos, cursorPos);
    }

    /// <summary>
    /// Finds the last opening parenthesis before the cursor position.
    /// </summary>
    private int FindOpeningParenthesis(string text, int cursorPos)
    {
        int parenDepth = 0;

        for (int i = cursorPos - 1; i >= 0; i--)
        {
            char c = text[i];

            if (c == ')')
            {
                parenDepth++;
            }
            else if (c == '(')
            {
                if (parenDepth == 0)
                    return i;

                parenDepth--;
            }
        }

        return -1;
    }

    /// <summary>
    /// Counts commas between opening parenthesis and cursor at the same nesting level.
    /// </summary>
    private int CountCommasInParameterList(string text, int openParenPos, int cursorPos)
    {
        int paramIndex = 0;
        int parenDepth = 0;
        bool inString = false;
        char stringChar = '\0';

        for (int i = openParenPos + 1; i < cursorPos; i++)
        {
            char c = text[i];

            if (UpdateStringState(c, text, i, ref inString, ref stringChar))
                continue;

            if (inString)
                continue;

            UpdateParameterTracking(c, ref parenDepth, ref paramIndex);
        }

        return paramIndex;
    }

    /// <summary>
    /// Updates the string literal tracking state.
    /// </summary>
    private bool UpdateStringState(char c, string text, int index, ref bool inString, ref char stringChar)
    {
        if (c != '"' && c != '\'')
            return false;

        if (!inString)
        {
            inString = true;
            stringChar = c;
            return true;
        }

        // Check if this is the closing quote (not escaped)
        if (c == stringChar && (index == 0 || text[index - 1] != '\\'))
        {
            inString = false;
        }

        return true;
    }

    /// <summary>
    /// Updates parenthesis depth and parameter count.
    /// </summary>
    private void UpdateParameterTracking(char c, ref int parenDepth, ref int paramIndex)
    {
        if (c == '(')
        {
            parenDepth++;
        }
        else if (c == ')')
        {
            parenDepth--;
        }
        else if (c == ',' && parenDepth == 0)
        {
            paramIndex++;
        }
    }

    /// <summary>
    /// Inserts an auto-complete suggestion at the cursor position.
    /// Handles complex C# syntax including partial identifiers after operators.
    /// </summary>
    private void InsertCompletion(string completion)
    {
        // Validation: ensure completion is not null or empty
        if (string.IsNullOrEmpty(completion))
        {
            _logger.LogWarning("Attempted to insert null or empty completion");
            return;
        }

        // Check if this is a history suggestion (starts with @ prefix)
        const string historyPrefix = "@ ";
        if (completion.StartsWith(historyPrefix))
        {
            // History suggestion - replace entire input with the command
            var historicalCommand = completion.Substring(historyPrefix.Length);
            _console.Input.SetText(historicalCommand);
            _console.Input.SetCursorPosition(historicalCommand.Length);
            _logger.LogInformation("Inserted historical command: {Command}", historicalCommand);
            // Re-evaluate parameter hints for the historical command
            UpdateParameterHints();
            return;
        }

        var currentText = _console.Input.Text;
        var cursorPos = _console.Input.CursorPosition;

        // Validation: ensure cursor position is within valid bounds
        if (cursorPos < 0 || cursorPos > currentText.Length)
        {
            _logger.LogWarning("Invalid cursor position {CursorPos} for text length {Length}", cursorPos, currentText.Length);
            cursorPos = Math.Clamp(cursorPos, 0, currentText.Length);
        }

        // Find the start of the current word being typed
        // Handle edge case: cursor at start of text
        if (cursorPos == 0)
        {
            _console.Input.SetText(completion + currentText);
            _console.Input.SetCursorPosition(completion.Length);
            _logger.LogInformation("Inserted completion at start: {Completion}", completion);
            // Re-evaluate parameter hints after inserting completion
            UpdateParameterHints();
            return;
        }

        int wordStart = cursorPos - 1;

        // Walk backwards to find word start, handling C# syntax:
        // - Stop at word separators (space, dot, comma, etc.)
        // - But continue through valid identifier characters
        while (wordStart >= 0)
        {
            char c = currentText[wordStart];

            // Stop at word separator (don't include it)
            if (IsWordSeparator(c))
        {
                wordStart++;
                break;
            }

            // Stop at invalid identifier characters
            if (!char.IsLetterOrDigit(c) && c != '_')
            {
                wordStart++;
                break;
            }

            wordStart--;
        }

        // Adjust if we went all the way to start
        if (wordStart < 0)
            wordStart = 0;

        // Replace from word start to cursor with the completion
        var before = currentText.Substring(0, wordStart);
        var after = currentText.Substring(cursorPos);
        var newText = before + completion + after;
        var newCursorPos = wordStart + completion.Length;

        _console.Input.SetText(newText);
        // Directly set cursor position to end of inserted completion
        _console.Input.SetCursorPosition(newCursorPos);

        _logger.LogInformation("Inserted completion: '{Completion}' replacing '{Partial}' at position {Position}",
            completion,
            currentText.Substring(wordStart, cursorPos - wordStart),
            wordStart);

        // Re-evaluate parameter hints after inserting completion
        UpdateParameterHints();
    }

    /// <summary>
    /// Checks if a character is a word separator for auto-completion.
    /// </summary>
    private static bool IsWordSeparator(char c)
    {
        return Array.IndexOf(ConsoleConstants.AutoComplete.WordSeparators, c) >= 0;
    }

    /// <summary>
    /// Checks if typing this key should clear auto-complete suggestions.
    /// Clears on structural characters that typically indicate end of an identifier.
    /// </summary>
    private static bool ShouldClearSuggestions(Keys key)
    {
        // Clear on space, semicolon, braces, brackets, comma
        return key == Keys.Space
            || key == Keys.OemSemicolon      // ;
            || key == Keys.OemOpenBrackets   // [
            || key == Keys.OemCloseBrackets  // ]
            || key == Keys.OemComma          // ,
            || key == Keys.Enter;            // New line without Shift
    }

    /// <summary>
    /// Handles keyboard input in search mode.
    /// </summary>
    private void HandleSearchModeInput(Keys key, char? character)
    {
        string searchInput = _console.SearchInput;

        if (key == Keys.Back && searchInput.Length > 0)
        {
            // Delete last character
            searchInput = searchInput.Substring(0, searchInput.Length - 1);
            _console.UpdateSearchQuery(searchInput);
        }
        else if (key == Keys.Enter)
        {
            // Enter navigates to next match (like F3)
            _console.NextSearchMatch();
        }
        else if (character.HasValue && !char.IsControl(character.Value))
        {
            // Add character to search
            searchInput += character.Value;
            _console.UpdateSearchQuery(searchInput);
        }
    }

    /// <summary>
    /// Handles keyboard input in reverse-i-search mode.
    /// </summary>
    private void HandleReverseSearchModeInput(Keys key, char? character)
    {
        string searchInput = _console.ReverseSearchInput;

        if (key == Keys.Back && searchInput.Length > 0)
        {
            // Delete last character
            searchInput = searchInput.Substring(0, searchInput.Length - 1);
            _console.UpdateReverseSearchQuery(searchInput, _history.GetAll());
        }
        else if (character.HasValue && !char.IsControl(character.Value))
        {
            // Add character to search
            searchInput += character.Value;
            _console.UpdateReverseSearchQuery(searchInput, _history.GetAll());
        }
    }

    /// <summary>
    /// Shows documentation for the currently selected autocomplete suggestion.
    /// </summary>
    private void ShowDocumentationForSelectedSuggestion()
    {
        try
        {
            // Get the selected suggestion from the coordinator
            if (_autoCompleteCoordinator == null)
                return;

            var selectedItem = _autoCompleteCoordinator.GetSelectedSuggestion();
            if (selectedItem == null)
            {
                _logger.LogDebug("No suggestion selected");
                return;
            }

            // Get documentation synchronously (without extended Roslyn info)
            // We do this to keep the UI responsive - advanced docs can be added later if needed
            var documentation = _documentationProvider.GetDocumentationAsync(selectedItem).GetAwaiter().GetResult();

            // Format and display
            var formattedText = _documentationProvider.FormatForDisplay(documentation);
            _console.ShowDocumentation(formattedText);

            _logger.LogInformation("Displayed documentation for: {DisplayText}", selectedItem.DisplayText);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to show documentation");
        }
    }

    /// <summary>
    /// Handles mouse input for the console.
    /// Uses time-based click detection window for consistent behavior across frame rates.
    /// </summary>
    private InputHandlingResult HandleMouseInput(MouseState mouseState, MouseState previousMouseState, float deltaTime)
    {
        // Handle mouse wheel scrolling
        int scrollDelta = mouseState.ScrollWheelValue - previousMouseState.ScrollWheelValue;
        if (scrollDelta != 0)
        {
            // Standard mouse wheel delta is 120 per notch
            // Positive delta = scroll up (backward in output), Negative = scroll down (forward)
            int lines = scrollDelta / 120;

            Point mousePosition = new Point(mouseState.X, mouseState.Y);

            // Check if mouse is over auto-complete window
            if (_console.IsMouseOverAutoComplete(mousePosition))
            {
                // Scroll auto-complete (invert direction for natural scrolling)
                _console.ScrollAutoComplete(-lines);
                _logger.LogTrace("Scrolled auto-complete by {Lines} items", -lines);
            }
            else
            {
                // Scroll console output (invert direction for natural scrolling)
                _console.ScrollOutput(-lines);
                _logger.LogTrace("Scrolled console output by {Lines} lines", -lines);
            }

            return InputHandlingResult.Consumed;
        }

        // Handle mouse button press (start of click or drag)
        if (mouseState.LeftButton == Microsoft.Xna.Framework.Input.ButtonState.Pressed &&
            previousMouseState.LeftButton == Microsoft.Xna.Framework.Input.ButtonState.Released)
        {
            Point clickPosition = new Point(mouseState.X, mouseState.Y);

            // Check for auto-complete item click (higher priority)
            int autoCompleteItemIndex = _console.GetAutoCompleteItemAt(clickPosition);

            if (autoCompleteItemIndex >= 0)
            {
                // Get the suggestion BEFORE we change anything
                _console.SelectAutoCompleteItem(autoCompleteItemIndex);
                var suggestion = _console.GetSelectedSuggestion();

                // Clear autocomplete BEFORE inserting (prevents re-triggering)
                _console.ClearAutoCompleteSuggestions();
                _lastFilteredText = ""; // Reset debounce tracking

                // Now insert the completion
                if (suggestion != null)
                {
                    InsertCompletion(suggestion);
                }

                return InputHandlingResult.Consumed;
            }

            // Check for section header click
            bool sectionClicked = _console.HandleOutputClick(clickPosition);

            if (sectionClicked)
            {
                return InputHandlingResult.Consumed;
            }

            // Check for input field click - this might start a drag selection
            if (_console.IsMouseOverInputField(clickPosition))
            {
                int charPosition = _console.GetCharacterPositionAtMouse(clickPosition);
                if (charPosition >= 0)
                {
                    // Start potential drag operation
                    _isDragging = true;
                    _dragStartPosition = clickPosition;
                    _lastDragPosition = clickPosition;
                    _dragStartCharPosition = charPosition;

                    // Position cursor at click point
                    _console.Input.SetCursorPosition(charPosition);
                    _console.Input.ClearSelection();

                    return InputHandlingResult.Consumed;
                }
            }
        }

        // Handle mouse dragging for text selection
        if (_isDragging && mouseState.LeftButton == Microsoft.Xna.Framework.Input.ButtonState.Pressed)
        {
            Point currentPosition = new Point(mouseState.X, mouseState.Y);

            // Only process if mouse has moved since last check (performance optimization)
            if (currentPosition.X != _lastDragPosition.X || currentPosition.Y != _lastDragPosition.Y)
            {
                // Check if we've moved enough to start selection (avoid accidental selection on click)
                int dragDistance = Math.Abs(currentPosition.X - _dragStartPosition.X) +
                                 Math.Abs(currentPosition.Y - _dragStartPosition.Y);

                if (dragDistance > 5) // 5 pixel threshold
                {
                    int currentCharPosition = _console.GetCharacterPositionAtMouse(currentPosition);
                    if (currentCharPosition >= 0 && _dragStartCharPosition >= 0)
                    {
                        // Update selection range
                        _console.Input.SetSelection(_dragStartCharPosition, currentCharPosition);
                    }
                }

                _lastDragPosition = currentPosition;
            }

            return InputHandlingResult.Consumed;
        }

        // Handle mouse button release (end of drag)
        if (_isDragging && mouseState.LeftButton == Microsoft.Xna.Framework.Input.ButtonState.Released)
        {
            _isDragging = false;
            _dragStartCharPosition = -1;
            return InputHandlingResult.Consumed;
        }

        return InputHandlingResult.None;
    }

    /// <summary>
    /// Handles font size changes with Ctrl/Cmd key combinations.
    /// </summary>
    private InputHandlingResult HandleFontSizeChanges(
        KeyboardState keyboardState,
        KeyboardState previousKeyboardState,
        bool isShiftPressed,
        bool isCtrlPressed,
        bool isCmdPressed)
    {
        bool isModifierPressed = isCtrlPressed || isCmdPressed;

        // Handle Ctrl/Cmd+Plus (or Equals) - increase font size
        if ((WasKeyJustPressed(Keys.OemPlus, keyboardState, previousKeyboardState) ||
             (WasKeyJustPressed(Keys.D0, keyboardState, previousKeyboardState) && isShiftPressed)) &&
            isModifierPressed)
        {
            int newSize = _console.IncreaseFontSize();
            _console.AppendOutput($"Font size increased to {newSize}pt", Success_Bright);
            _logger.LogInformation("Font size increased to {Size}pt", newSize);
            return InputHandlingResult.Consumed;
        }

        // Handle Ctrl/Cmd+Minus - decrease font size
        if (WasKeyJustPressed(Keys.OemMinus, keyboardState, previousKeyboardState) && isModifierPressed)
        {
            int newSize = _console.DecreaseFontSize();
            _console.AppendOutput($"Font size decreased to {newSize}pt", Success_Bright);
            _logger.LogInformation("Font size decreased to {Size}pt", newSize);
            return InputHandlingResult.Consumed;
        }

        // Handle Ctrl/Cmd+0 - reset font size to default
        if (WasKeyJustPressed(Keys.D0, keyboardState, previousKeyboardState) &&
            !isShiftPressed && isModifierPressed)
        {
            int newSize = _console.ResetFontSize();
            _console.AppendOutput($"Font size reset to {newSize}pt (default)", Success_Bright);
            _logger.LogInformation("Font size reset to {Size}pt", newSize);
            return InputHandlingResult.Consumed;
        }

        return InputHandlingResult.None;
    }

    /// <summary>
    /// Handles function keys (F1-F12) for bookmarks, search navigation, and documentation.
    /// </summary>
    private InputHandlingResult HandleFunctionKeys(
        KeyboardState keyboardState,
        KeyboardState previousKeyboardState,
        bool isShiftPressed)
    {
        // Handle F1-F12 - execute bookmarked commands
        var fKeyNumber = GetFKeyNumber(keyboardState, previousKeyboardState);
        if (fKeyNumber.HasValue && _bookmarksManager != null)
        {
            var command = _bookmarksManager.GetBookmark(fKeyNumber.Value);
            if (command != null)
            {
                _logger.LogInformation("Executing bookmarked command from F{FKey}: {Command}", fKeyNumber.Value, command);
                _console.AppendOutput($"> F{fKeyNumber.Value}: {command}", Warning);
                return InputHandlingResult.Execute(command);
            }
            // If no bookmark for this F-key, fall through to other handlers
        }

        // Handle F3 / Shift+F3 - navigate search results
        if (WasKeyJustPressed(Keys.F3, keyboardState, previousKeyboardState))
        {
            if (_console.IsSearchMode && _console.OutputSearcher.IsSearching)
            {
                if (isShiftPressed)
                {
                    _console.PreviousSearchMatch();
                    _logger.LogDebug("Moved to previous search match");
                }
                else
                {
                    _console.NextSearchMatch();
                    _logger.LogDebug("Moved to next search match");
                }
                return InputHandlingResult.Consumed;
            }
        }

        // Handle F1 - show/hide documentation for selected autocomplete item (if no bookmark)
        if (WasKeyJustPressed(Keys.F1, keyboardState, previousKeyboardState))
        {
            // If documentation is showing, hide it
            if (_console.IsShowingDocumentation)
            {
                _console.HideDocumentation();
                _logger.LogDebug("Closed documentation popup");
                return InputHandlingResult.Consumed;
            }

            // If autocomplete suggestions are visible, show docs for selected item
            if (_console.HasSuggestions())
            {
                ShowDocumentationForSelectedSuggestion();
                _logger.LogInformation("Showing documentation for selected suggestion");
                return InputHandlingResult.Consumed;
            }
        }

        return InputHandlingResult.None;
    }

    /// <summary>
    /// Gets the F-key number (1-12) if an F-key was just pressed.
    /// </summary>
    private static int? GetFKeyNumber(KeyboardState keyboardState, KeyboardState previousKeyboardState)
    {
        if (WasKeyJustPressed(Keys.F1, keyboardState, previousKeyboardState)) return 1;
        if (WasKeyJustPressed(Keys.F2, keyboardState, previousKeyboardState)) return 2;
        if (WasKeyJustPressed(Keys.F3, keyboardState, previousKeyboardState)) return 3;
        if (WasKeyJustPressed(Keys.F4, keyboardState, previousKeyboardState)) return 4;
        if (WasKeyJustPressed(Keys.F5, keyboardState, previousKeyboardState)) return 5;
        if (WasKeyJustPressed(Keys.F6, keyboardState, previousKeyboardState)) return 6;
        if (WasKeyJustPressed(Keys.F7, keyboardState, previousKeyboardState)) return 7;
        if (WasKeyJustPressed(Keys.F8, keyboardState, previousKeyboardState)) return 8;
        if (WasKeyJustPressed(Keys.F9, keyboardState, previousKeyboardState)) return 9;
        if (WasKeyJustPressed(Keys.F10, keyboardState, previousKeyboardState)) return 10;
        if (WasKeyJustPressed(Keys.F11, keyboardState, previousKeyboardState)) return 11;
        if (WasKeyJustPressed(Keys.F12, keyboardState, previousKeyboardState)) return 12;
        return null;
    }
}
