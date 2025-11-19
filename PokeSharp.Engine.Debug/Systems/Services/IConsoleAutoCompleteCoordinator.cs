using Microsoft.CodeAnalysis.Completion;

namespace PokeSharp.Engine.Debug.Systems.Services;

/// <summary>
/// Coordinates auto-completion functionality for the console.
/// Separates auto-complete coordination logic from the main ConsoleSystem.
/// </summary>
public interface IConsoleAutoCompleteCoordinator
{
    /// <summary>
    /// Triggers auto-completion for the current input.
    /// </summary>
    /// <param name="code">The current code/input text.</param>
    /// <param name="cursorPosition">The cursor position in the input.</param>
    /// <returns>List of completion suggestions.</returns>
    Task<List<CompletionItem>> GetCompletionsAsync(string code, int cursorPosition);

    /// <summary>
    /// Updates the auto-complete state with the latest script state.
    /// </summary>
    /// <param name="scriptState">The current script state from the evaluator.</param>
    void UpdateScriptState(Microsoft.CodeAnalysis.Scripting.ScriptState<object>? scriptState);

    /// <summary>
    /// Checks if delayed auto-complete should be triggered based on timing.
    /// </summary>
    /// <returns>True if auto-complete should be triggered, false otherwise.</returns>
    bool ShouldTriggerDelayedAutoComplete();

    /// <summary>
    /// Updates timing for delayed auto-complete triggering.
    /// Should be called every frame with deltaTime.
    /// </summary>
    /// <param name="deltaTime">Time elapsed since last frame.</param>
    void Update(float deltaTime);

    /// <summary>
    /// Notifies that user is typing (for delayed auto-complete).
    /// Resets the delay timer so autocomplete triggers after user stops typing.
    /// </summary>
    void NotifyTyping();

    /// <summary>
    /// Gets the currently selected autocomplete suggestion.
    /// </summary>
    /// <returns>The selected completion item, or null if no selection.</returns>
    CompletionItem? GetSelectedSuggestion();
}

