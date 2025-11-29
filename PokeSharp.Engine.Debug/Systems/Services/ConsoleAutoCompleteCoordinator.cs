using Microsoft.CodeAnalysis.Completion;
using Microsoft.CodeAnalysis.Scripting;
using Microsoft.Extensions.Logging;
using PokeSharp.Engine.Debug.Console.Configuration;
using PokeSharp.Engine.Debug.Console.Features;
using PokeSharp.Engine.Debug.Console.UI;

namespace PokeSharp.Engine.Debug.Systems.Services;

/// <summary>
/// Coordinates auto-completion functionality for the console.
/// Extracted from ConsoleSystem to follow Single Responsibility Principle.
///
/// Thread Safety:
/// - This class is NOT thread-safe and must be accessed only from the main game thread.
/// - All methods (Update, NotifyTyping, GetCompletionsAsync) should be called sequentially.
/// - Concurrent calls to GetCompletionsAsync may result in race conditions.
/// </summary>
public class ConsoleAutoCompleteCoordinator : IConsoleAutoCompleteCoordinator
{
    private readonly QuakeConsole _console;
    private readonly ConsoleAutoComplete _autoComplete;
    private readonly HistorySuggestionProvider _historyProvider;
    private readonly ILogger _logger;

    // Auto-complete delay tracking
    private float _lastTypingTime;
    private float _totalTime;
    private const float AutoCompleteDelay = ConsoleConstants.Input.AutoCompleteDelay;
    private const int MaxHistorySuggestions = 3; // Limit history suggestions to avoid cluttering

    /// <summary>
    /// Initializes a new instance of the ConsoleAutoCompleteCoordinator.
    /// </summary>
    public ConsoleAutoCompleteCoordinator(
        QuakeConsole console,
        ConsoleAutoComplete autoComplete,
        HistorySuggestionProvider historyProvider,
        ILogger logger
    )
    {
        _console = console ?? throw new ArgumentNullException(nameof(console));
        _autoComplete = autoComplete ?? throw new ArgumentNullException(nameof(autoComplete));
        _historyProvider =
            historyProvider ?? throw new ArgumentNullException(nameof(historyProvider));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    /// <summary>
    /// Updates timing for delayed auto-complete triggering.
    /// Should be called every frame with deltaTime.
    /// </summary>
    public void Update(float deltaTime)
    {
        _totalTime += deltaTime;

        // Check if we should trigger auto-complete after delay
        if (
            _console.IsVisible
            && _lastTypingTime > 0
            && (_totalTime - _lastTypingTime) >= AutoCompleteDelay
        )
        {
            _lastTypingTime = 0; // Reset
            // Trigger auto-complete (caller should handle this)
        }
    }

    /// <summary>
    /// Notifies that user is typing (for delayed auto-complete).
    /// </summary>
    public void NotifyTyping()
    {
        _lastTypingTime = _totalTime;
    }

    /// <summary>
    /// Checks if delayed auto-complete should be triggered.
    /// </summary>
    public bool ShouldTriggerDelayedAutoComplete()
    {
        if (!_console.IsVisible || _lastTypingTime <= 0)
            return false;

        if ((_totalTime - _lastTypingTime) >= AutoCompleteDelay)
        {
            _lastTypingTime = 0; // Reset
            return true;
        }

        return false;
    }

    /// <summary>
    /// Triggers auto-completion for the current input, mixing API completions with command history.
    /// </summary>
    public async Task<List<CompletionItem>> GetCompletionsAsync(string code, int cursorPosition)
    {
        try
        {
            _logger.LogInformation(
                "Triggering auto-complete: code='{Code}', pos={Pos}",
                code,
                cursorPosition
            );

            // Get API completions from Roslyn
            var apiSuggestions = await _autoComplete.GetCompletionsAsync(code, cursorPosition);

            // Get history suggestions
            var historySuggestions = _historyProvider.GetSuggestions(code, MaxHistorySuggestions);

            _logger.LogInformation(
                "Got {ApiCount} API suggestions and {HistoryCount} history suggestions",
                apiSuggestions.Count,
                historySuggestions.Count
            );

            // If we have history suggestions and they're relevant, add them at the top
            // Create synthetic CompletionItems for history suggestions for now
            // (We'll need to update QuakeConsole to handle ConsoleSuggestion type later)
            var result = new List<CompletionItem>();

            // Add history items first (they appear at top)
            foreach (var historySuggestion in historySuggestions)
            {
                // Create a special CompletionItem with a prefix to indicate it's from history
                var historyItem = CompletionItem.Create(
                    displayText: $"@ {historySuggestion.Command}",
                    filterText: historySuggestion.Command,
                    sortText: $"000_{historySuggestion.Command}", // Sort first with "000_" prefix
                    inlineDescription: $"(used {historySuggestion.UseCount}x)",
                    tags: System.Collections.Immutable.ImmutableArray.Create("history")
                );
                result.Add(historyItem);
            }

            // Add API suggestions
            result.AddRange(apiSuggestions);

            if (result.Count > 0)
            {
                _logger.LogInformation(
                    "Total suggestions: {Suggestions}",
                    string.Join(
                        ", ",
                        result
                            .Take(ConsoleConstants.Limits.MaxSuggestionsInLog)
                            .Select(s => s.DisplayText)
                    )
                );
            }

            return result;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting auto-complete suggestions");
            return new List<CompletionItem>();
        }
    }

    /// <summary>
    /// Updates the auto-complete state with the latest script state.
    /// </summary>
    public void UpdateScriptState(ScriptState<object>? scriptState)
    {
        _autoComplete.UpdateScriptState(scriptState);
    }

    /// <summary>
    /// Gets the currently selected autocomplete suggestion.
    /// </summary>
    public CompletionItem? GetSelectedSuggestion()
    {
        return _console.GetSelectedCompletionItem();
    }
}
