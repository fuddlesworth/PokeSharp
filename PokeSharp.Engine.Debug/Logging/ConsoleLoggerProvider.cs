using Microsoft.Extensions.Logging;
using Microsoft.Xna.Framework;
using System;
using System.Collections.Concurrent;

namespace PokeSharp.Engine.Debug.Logging;

/// <summary>
///     Logger provider that creates loggers writing to the debug console.
/// </summary>
[ProviderAlias("DebugConsole")]
public class ConsoleLoggerProvider : ILoggerProvider
{
    private readonly ConcurrentDictionary<string, ConsoleLogger> _loggers = new();
    private Action<string, Color>? _writeToConsole;
    private Func<LogLevel, bool> _isEnabled = _ => true;

    /// <summary>
    ///     Sets the function to write messages to the console.
    /// </summary>
    public void SetConsoleWriter(Action<string, Color> writeToConsole)
    {
        _writeToConsole = writeToConsole;
    }

    /// <summary>
    ///     Sets the function to check if a log level is enabled.
    /// </summary>
    public void SetLogLevelFilter(Func<LogLevel, bool> isEnabled)
    {
        _isEnabled = isEnabled;
    }

    public ILogger CreateLogger(string categoryName)
    {
        return _loggers.GetOrAdd(categoryName, name =>
            new ConsoleLogger(name, WriteToConsole, _isEnabled));
    }

    private void WriteToConsole(string message, Color color)
    {
        _writeToConsole?.Invoke(message, color);
    }

    public void Dispose()
    {
        _loggers.Clear();
    }
}