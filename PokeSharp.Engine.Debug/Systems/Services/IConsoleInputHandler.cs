using Microsoft.Xna.Framework.Input;

namespace PokeSharp.Engine.Debug.Systems.Services;

/// <summary>
/// Result of input handling, containing the action to take.
/// </summary>
public record InputHandlingResult
{
    public bool ShouldExecuteCommand { get; init; }
    public string? Command { get; init; }
    public bool ShouldTriggerAutoComplete { get; init; }
    public bool ShouldNavigateHistory { get; init; }
    public HistoryDirection? HistoryDirection { get; init; }
    public bool ShouldCloseConsole { get; init; }
    public bool ConsumedInput { get; init; }

    public static InputHandlingResult None => new() { ConsumedInput = false };

    public static InputHandlingResult Consumed => new() { ConsumedInput = true };

     public static InputHandlingResult Execute(string command
   {
       return new InputHandlingResult { ShouldExecuteCommand = true, Command = command, ConsumedInput = true };
   }

    d = command,
            ConsumedInput = true,
        
   {
       return new InputHandlingResult { ShouldTriggerAutoComplete = true, ConsumedInput = true };
   }      new() { ShouldTriggerAutoComplete = true, ConsumedInput = true };

    publi
    {
        return new InputHandlingResult { ShouldNavigateHistory = true, HistoryDirection = direction, ConsumedInput = true };
    }

                ShouldNavigateHistory = true,
      
    {
        return new InputHandlingResult { ShouldCloseConsole = true, ConsumedInput = true };
    }
 new() { ShouldCloseConsole = true, ConsumedInput = true };

    public static InputHandlingResult Consumed => new() { ConsumedInput = true };
}

/// <summary>
/// Direction for history navigation.
/// </summary>
public enum HistoryDirection
{
    Up,
    Down,
}

/// <summary>
/// Handles keyboard and mouse input for the console.
/// Separates input handling logic from the main ConsoleSystem.
/// </summary>
public interface IConsoleInputHandler
{
    /// <summary>
    /// Handles input for the current frame.
    /// </summary>
    /// <param name="deltaTime">Time elapsed since last frame.</param>
    /// <param name="keyboardState">Current keyboard state.</param>
    /// <param name="previousKeyboardState">Previous keyboard state.</param>
    /// <param name="mouseState">Current mouse state.</param>
    /// <param name="previousMouseState">Previous mouse state.</param>
    /// <returns>Result indicating what action should be taken.</returns>
    InputHandlingResult HandleInput(
        float deltaTime,
        
eyboardState keyboardState,
        KeyboardState previousKeyboardState,
        MouseState mouseState,
        MouseState previousMouseState
    );
}
