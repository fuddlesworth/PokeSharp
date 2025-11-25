using Microsoft.Extensions.Logging;

namespace PokeSharp.Engine.Debug.Commands.BuiltIn;

/// <summary>
/// Sets the minimum log level for console output.
/// </summary>
[ConsoleCommand("loglevel", "Set minimum log level")]
public class LogLevelCommand : IConsoleCommand
{
    public string Name => "loglevel";
    public string Description => "Set minimum log level";
    public string Usage => "loglevel <Trace|Debug|Information|Warning|Error|Critical>";

    public Task ExecuteAsync(IConsoleContext context, string[] args)
    {
        if (args.Length == 0)
        {
            // Show current level
            context.WriteLine($"Current minimum log level: {context.MinimumLogLevel}", context.Theme.Info);
            context.WriteLine("Available levels: Trace, Debug, Information, Warning, Error, Critical", context.Theme.TextSecondary);
            context.WriteLine("Usage: loglevel <level>", context.Theme.TextSecondary);
            return Task.CompletedTask;
        }

        var levelStr = args[0];
        if (Enum.TryParse<LogLevel>(levelStr, true, out var level))
        {
            context.SetMinimumLogLevel(level);
            context.WriteLine($"Minimum log level set to: {level}", context.Theme.Success);

            if (context.IsLoggingEnabled)
            {
                context.WriteLine($"Now showing logs at level {level} and above", context.Theme.Info);
            }
            else
            {
                context.WriteLine("(Logging is currently disabled - use 'logging on' to enable)", context.Theme.TextSecondary);
            }
        }
        else
        {
            context.WriteLine($"Invalid log level: {levelStr}", context.Theme.Error);
            context.WriteLine("Available levels: Trace, Debug, Information, Warning, Error, Critical", context.Theme.TextSecondary);
        }

        return Task.CompletedTask;
    }
}

