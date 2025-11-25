namespace PokeSharp.Engine.Debug.Commands.BuiltIn;

/// <summary>
/// Toggles console logging on/off.
/// </summary>
[ConsoleCommand("logging", "Toggle console logging on/off")]
public class LoggingCommand : IConsoleCommand
{
    public string Name => "logging";
    public string Description => "Toggle console logging on/off";
    public string Usage => "logging <on|off>";

    public Task ExecuteAsync(IConsoleContext context, string[] args)
    {
        if (args.Length == 0)
        {
            // Show current status
            context.WriteLine($"Logging is currently: {(context.IsLoggingEnabled ? "ON" : "OFF")}", context.Theme.Info);
            context.WriteLine($"Minimum log level: {context.MinimumLogLevel}", context.Theme.Info);
            context.WriteLine("Usage: logging <on|off>", context.Theme.TextSecondary);
            return Task.CompletedTask;
        }

        var action = args[0].ToLower();
        switch (action)
        {
            case "on":
            case "true":
            case "1":
                context.SetLoggingEnabled(true);
                context.WriteLine("Logging enabled", context.Theme.Success);
                context.WriteLine($"Showing logs at level {context.MinimumLogLevel} and above", context.Theme.Info);
                break;

            case "off":
            case "false":
            case "0":
                context.SetLoggingEnabled(false);
                context.WriteLine("Logging disabled", context.Theme.Info);
                break;

            default:
                context.WriteLine($"Invalid option: {action}", context.Theme.Error);
                context.WriteLine("Usage: logging <on|off>", context.Theme.TextSecondary);
                break;
        }

        return Task.CompletedTask;
    }
}

