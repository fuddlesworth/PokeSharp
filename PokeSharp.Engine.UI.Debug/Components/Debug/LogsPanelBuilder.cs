using Microsoft.Extensions.Logging;
using PokeSharp.Engine.UI.Debug.Components.Controls;
using PokeSharp.Engine.UI.Debug.Core;
using PokeSharp.Engine.UI.Debug.Layout;

namespace PokeSharp.Engine.UI.Debug.Components.Debug;

/// <summary>
/// Builder for creating LogsPanel with customizable components.
/// </summary>
public class LogsPanelBuilder
{
    private TextBuffer? _logBuffer;
    private int _maxLogs = 5000;
    private LogLevel _filterLevel = LogLevel.Trace;

    public static LogsPanelBuilder Create() => new();

    public LogsPanelBuilder WithLogBuffer(TextBuffer buffer)
    {
        _logBuffer = buffer;
        return this;
    }

    public LogsPanelBuilder WithMaxLogs(int maxLogs)
    {
        _maxLogs = maxLogs;
        return this;
    }

    public LogsPanelBuilder WithFilterLevel(LogLevel level)
    {
        _filterLevel = level;
        return this;
    }

    public LogsPanel Build()
    {
        return new LogsPanel(
            _logBuffer ?? CreateDefaultLogBuffer(),
            _maxLogs,
            _filterLevel
        );
    }

    private TextBuffer CreateDefaultLogBuffer()
    {
        return new TextBuffer("log_buffer")
        {
            BackgroundColor = UITheme.Dark.ConsoleOutputBackground,
            AutoScroll = true,
            MaxLines = _maxLogs,
            Constraint = new LayoutConstraint
            {
                Anchor = Anchor.Fill
            }
        };
    }
}

