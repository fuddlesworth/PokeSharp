using System.Text.RegularExpressions;
using Microsoft.Extensions.Logging;
using Microsoft.Xna.Framework;

namespace PokeSharp.Engine.Debug.Logging;

/// <summary>
///     Custom logger that writes log messages to the debug console.
/// </summary>
public class ConsoleLogger : ILogger
{
    private readonly string _categoryName;
    private readonly Func<LogLevel, bool> _isEnabled;
    private readonly Action<string, Color> _writeToConsole;

    public ConsoleLogger(
        string categoryName,
        Action<string, Color> writeToConsole,
        Func<LogLevel, bool> isEnabled
    )
    {
        _categoryName = categoryName;
        _writeToConsole = writeToConsole;
        _isEnabled = isEnabled;
    }

    public IDisposable? BeginScope<TState>(TState state)
        where TState : notnull
    {
        return null; // Scopes not supported for now
    }

    public bool IsEnabled(LogLevel logLevel)
    {
        return _isEnabled(logLevel);
    }

    public void Log<TState>(
        LogLevel logLevel,
        EventId eventId,
        TState state,
        Exception? exception,
        Func<TState, Exception?, string> formatter
    )
    {
        if (!IsEnabled(logLevel))
            return;

        var message = formatter(state, exception);
        if (string.IsNullOrEmpty(message) && exception == null)
            return;

        // Strip Serilog/Spectre.Console markup tags (e.g., [cyan], [red], etc.)
        message = StripMarkupTags(message);

        // Format log message
        var timestamp = DateTime.Now.ToString("HH:mm:ss.fff");
        var level = GetLogLevelString(logLevel);
        var category = GetShortCategoryName(_categoryName);

        var formattedMessage = $"[{timestamp}] [{level}] \"{category}\": {message}";

        if (exception != null)
            formattedMessage += $"\n{exception}";

        // Get color based on log level
        var color = GetColorForLogLevel(logLevel);

        _writeToConsole(formattedMessage, color);
    }

    private static string GetLogLevelString(LogLevel logLevel)
    {
        return logLevel switch
        {
            LogLevel.Trace => "TRACE",
            LogLevel.Debug => "DEBUG",
            LogLevel.Information => "INFOR",
            LogLevel.Warning => "WARNI",
            LogLevel.Error => "ERROR",
            LogLevel.Critical => "CRIT!",
            _ => "UNKNO",
        };
    }

    private static string GetShortCategoryName(string category)
    {
        // Shorten category name if it's too long
        var lastDot = category.LastIndexOf('.');
        if (lastDot >= 0 && lastDot < category.Length - 1)
            return category.Substring(lastDot + 1);
        return category;
    }

    private static Color GetColorForLogLevel(LogLevel logLevel)
    {
        return logLevel switch
        {
            LogLevel.Trace => new Color(150, 150, 150), // Gray
            LogLevel.Debug => new Color(180, 180, 255), // Light Blue
            LogLevel.Information => Color.White, // White
            LogLevel.Warning => new Color(255, 200, 100), // Orange
            LogLevel.Error => new Color(255, 100, 100), // Red
            LogLevel.Critical => new Color(255, 50, 255), // Magenta
            _ => Color.LightGray,
        };
    }

    /// <summary>
    ///     Strips Serilog/Spectre.Console markup tags from log messages.
    ///     Examples: [cyan], [red], [/], [skyblue1], [cyan bold], etc.
    /// </summary>
    private static string StripMarkupTags(string message)
    {
        if (string.IsNullOrEmpty(message))
            return message;

        // Remove markup tags like [cyan], [red], [/], [skyblue1], [cyan bold], etc.
        // Pattern matches:
        // - [/] - closing tag
        // - [color] - simple color
        // - [color modifier] - color with modifier (e.g., "cyan bold")
        // - [color1] - numbered colors
        // This is more permissive to catch all Spectre.Console markup
        return Regex.Replace(message, @"\[/?[a-z0-9_ ]*\]", "", RegexOptions.IgnoreCase);
    }
}
