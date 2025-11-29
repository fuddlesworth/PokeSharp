namespace PokeSharp.Engine.Debug.Systems.Services;

/// <summary>
///     Result of command execution.
/// </summary>
public record CommandExecutionResult
{
    public bool Success { get; init; }
    public string? Output { get; init; }
    public bool IsBuiltInCommand { get; init; }
    public Exception? Error { get; init; }

    public static CommandExecutionResult SuccessOutput(string output, bool isBuiltIn = false)
    {
        return new CommandExecutionResult
        {
            Success = true,
            Output = output,
            IsBuiltInCommand = isBuiltIn,
        };
    }

    public static CommandExecutionResult SuccessNoOutput(bool isBuiltIn = false)
    {
        return new CommandExecutionResult { Success = true, IsBuiltInCommand = isBuiltIn };
    }

    public static CommandExecutionResult Failure(string error)
    {
        return new CommandExecutionResult { Success = false, Output = error };
    }

    public static CommandExecutionResult Failure(Exception exception)
    {
        return new CommandExecutionResult
        {
            Success = false,
            Output = exception.Message,
            Error = exception,
        };
    }
}

/// <summary>
///     Executes console commands (built-in, aliases, scripts).
///     Separates command execution logic from the main ConsoleSystem.
/// </summary>
public interface IConsoleCommandExecutor
{
    /// <summary>
    ///     Executes a command and returns the result.
    /// </summary>
    /// <param name="command">The command to execute.</param>
    /// <returns>The result of the execution.</returns>
    Task<CommandExecutionResult> ExecuteAsync(string command);
}
