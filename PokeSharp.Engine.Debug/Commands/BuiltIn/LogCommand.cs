using Microsoft.Extensions.Logging;
using System;
using System.Threading.Tasks;

namespace PokeSharp.Engine.Debug.Commands.BuiltIn;

[ConsoleCommand("log", "Manage and view system logs")]
public class LogCommand : IConsoleCommand
{
    public string Name => "log";
    public string Description => "Manage and view system logs";
    public string Usage => "log [show | clear | filter <level> | search <text>]\n  show: Switch to Logs tab\n  clear: Clear all logs\n  filter <level>: Filter by log level (Trace|Debug|Info|Warning|Error|Critical)\n  search <text>: Search logs by text";

    public Task ExecuteAsync(IConsoleContext context, string[] args)
    {
        var theme = context.Theme;

        if (args.Length == 0 || args[0].Equals("show", StringComparison.OrdinalIgnoreCase))
        {
            // Show log count and switch to logs tab
            var logCount = context.GetLogCount();
            context.WriteLine($"Showing logs panel ({logCount} log(s))", theme.Success);
            context.SwitchToTab(2); // Switch to Logs tab (index 2)
        }
        else if (args[0].Equals("clear", StringComparison.OrdinalIgnoreCase))
        {
            context.ClearLogs();
            context.WriteLine("All logs cleared", theme.Success);
        }
        else if (args[0].Equals("filter", StringComparison.OrdinalIgnoreCase))
        {
            // Set filter level
            if (args.Length < 2)
            {
                context.WriteLine("Usage: log filter <level>", theme.Warning);
                context.WriteLine("Levels: Trace, Debug, Information, Warning, Error, Critical", theme.TextSecondary);
                return Task.CompletedTask;
            }

            var levelStr = args[1];
            if (Enum.TryParse<LogLevel>(levelStr, ignoreCase: true, out var level))
            {
                context.SetLogFilter(level);
                context.WriteLine($"Log filter set to: {level}", theme.Success);
                context.WriteLine("Switch to Logs tab (Ctrl+3) to view filtered logs", theme.TextSecondary);
            }
            else
            {
                context.WriteLine($"Invalid log level: '{levelStr}'", theme.Error);
                context.WriteLine("Valid levels: Trace, Debug, Information, Warning, Error, Critical", theme.TextSecondary);
            }
        }
        else if (args[0].Equals("search", StringComparison.OrdinalIgnoreCase))
        {
            // Set search filter
            if (args.Length < 2)
            {
                // Clear search
                context.SetLogSearch(null);
                context.WriteLine("Log search filter cleared", theme.Success);
            }
            else
            {
                var searchText = string.Join(" ", args.Skip(1));
                context.SetLogSearch(searchText);
                context.WriteLine($"Log search filter set to: '{searchText}'", theme.Success);
                context.WriteLine("Switch to Logs tab (Ctrl+3) to view filtered logs", theme.TextSecondary);
            }
        }
        else
        {
            context.WriteLine($"Unknown log subcommand: '{args[0]}'", theme.Error);
            context.WriteLine(Usage, theme.TextSecondary);
        }

        return Task.CompletedTask;
    }
}
