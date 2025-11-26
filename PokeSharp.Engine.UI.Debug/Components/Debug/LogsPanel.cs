using Microsoft.Xna.Framework;
using Microsoft.Extensions.Logging;
using PokeSharp.Engine.UI.Debug.Components.Base;
using PokeSharp.Engine.UI.Debug.Components.Controls;
using PokeSharp.Engine.UI.Debug.Components.Layout;
using PokeSharp.Engine.UI.Debug.Core;
using PokeSharp.Engine.UI.Debug.Layout;
using System.Collections.Generic;
using System.Linq;

namespace PokeSharp.Engine.UI.Debug.Components.Debug;

/// <summary>
/// Panel for viewing and filtering console logs.
/// </summary>
public class LogsPanel : Panel
{
    private readonly TextBuffer _logBuffer;
    private readonly List<LogEntry> _allLogs = new();
    private LogLevel _filterLevel = LogLevel.Trace; // Show all by default
    private string? _searchFilter = null;
    private readonly int _maxLogs;

    public class LogEntry
    {
        public DateTime Timestamp { get; set; }
        public LogLevel Level { get; set; }
        public string Message { get; set; } = string.Empty;
        public string Category { get; set; } = "General";
    }

    /// <summary>
    /// Creates a LogsPanel with the specified components.
    /// Use <see cref="LogsPanelBuilder"/> to construct instances.
    /// </summary>
    internal LogsPanel(TextBuffer logBuffer, int maxLogs, LogLevel filterLevel)
    {
        _logBuffer = logBuffer;
        _maxLogs = maxLogs;
        _filterLevel = filterLevel;

        Id = "logs_panel";
        BackgroundColor = UITheme.Dark.ConsoleBackground;
        BorderColor = UITheme.Dark.BorderPrimary;
        BorderThickness = 1;
        Constraint.Padding = UITheme.Dark.PaddingMedium;

        AddChild(_logBuffer);
        UpdateLogDisplay();
    }

    /// <summary>
    /// Adds a log entry.
    /// </summary>
    public void AddLog(LogLevel level, string message, string category = "General")
    {
        var entry = new LogEntry
        {
            Timestamp = DateTime.Now,
            Level = level,
            Message = message,
            Category = category
        };

        _allLogs.Add(entry);

        // Trim if we have too many logs
        if (_allLogs.Count > _maxLogs)
        {
            _allLogs.RemoveAt(0);
        }

        // Update display if this log passes the filter
        if (PassesFilter(entry))
        {
            AppendLogToBuffer(entry);
        }
    }

    /// <summary>
    /// Sets the log level filter (only show logs at this level or higher).
    /// </summary>
    public void SetFilterLevel(LogLevel level)
    {
        _filterLevel = level;
        UpdateLogDisplay();
    }

    /// <summary>
    /// Sets a text search filter (only show logs containing this text).
    /// </summary>
    public void SetSearchFilter(string? filter)
    {
        _searchFilter = string.IsNullOrWhiteSpace(filter) ? null : filter;
        UpdateLogDisplay();
    }

    /// <summary>
    /// Clears all logs.
    /// </summary>
    public void ClearLogs()
    {
        _allLogs.Clear();
        _logBuffer.Clear();
        UpdateLogDisplay();
    }

    /// <summary>
    /// Gets the total number of logs (unfiltered).
    /// </summary>
    public int GetTotalLogCount() => _allLogs.Count;

    /// <summary>
    /// Gets the number of filtered logs currently displayed.
    /// </summary>
    public int GetFilteredLogCount()
    {
        return _allLogs.Count(PassesFilter);
    }

    /// <summary>
    /// Checks if a log entry passes the current filters.
    /// </summary>
    private bool PassesFilter(LogEntry entry)
    {
        // Check log level filter
        if (entry.Level < _filterLevel)
            return false;

        // Check text search filter
        if (_searchFilter != null && !entry.Message.Contains(_searchFilter, StringComparison.OrdinalIgnoreCase))
            return false;

        return true;
    }

    /// <summary>
    /// Rebuilds the log display with current filters.
    /// </summary>
    private void UpdateLogDisplay()
    {
        _logBuffer.Clear();

        // Display header
        var filteredCount = _allLogs.Count(PassesFilter);
        var hiddenCount = _allLogs.Count - filteredCount;

        _logBuffer.AppendLine("═══════════════════════════════════════════════════════════════════", UITheme.Dark.Info);
        _logBuffer.AppendLine($"  CONSOLE LOGS ({filteredCount} shown, {hiddenCount} hidden)", UITheme.Dark.Info);
        _logBuffer.AppendLine($"  Filter: {GetFilterLevelName(_filterLevel)} and above", UITheme.Dark.TextSecondary);
        if (_searchFilter != null)
        {
            _logBuffer.AppendLine($"  Search: \"{_searchFilter}\"", UITheme.Dark.TextSecondary);
        }
        _logBuffer.AppendLine("═══════════════════════════════════════════════════════════════════", UITheme.Dark.Info);
        _logBuffer.AppendLine("", Color.White);

        // Display filtered logs
        if (filteredCount == 0)
        {
            _logBuffer.AppendLine("No logs match current filter.", UITheme.Dark.TextDim);
            _logBuffer.AppendLine("", Color.White);
            _logBuffer.AppendLine("TIP: Use 'log filter <level>' to change filter level", UITheme.Dark.TextSecondary);
            _logBuffer.AppendLine("     Levels: Trace, Debug, Information, Warning, Error, Critical", UITheme.Dark.TextSecondary);
            return;
        }

        foreach (var entry in _allLogs.Where(PassesFilter))
        {
            AppendLogToBuffer(entry);
        }

        // Footer
        _logBuffer.AppendLine("", Color.White);
        _logBuffer.AppendLine("─────────────────────────────────────────────────────────────────", UITheme.Dark.BorderPrimary);
        _logBuffer.AppendLine($"Total: {_allLogs.Count} logs | Filtered: {filteredCount}", UITheme.Dark.TextSecondary);
    }

    /// <summary>
    /// Appends a single log entry to the buffer.
    /// </summary>
    private void AppendLogToBuffer(LogEntry entry)
    {
        var timestamp = entry.Timestamp.ToString("HH:mm:ss.fff");
        var levelStr = GetLogLevelShortName(entry.Level).PadRight(5);
        var color = GetLogLevelColor(entry.Level);

        // Format: [12:34:56.789] [INFO ] Message
        var logLine = $"[{timestamp}] [{levelStr}] {entry.Message}";
        _logBuffer.AppendLine(logLine, color, entry.Category);
    }

    /// <summary>
    /// Gets the color for a log level.
    /// </summary>
    private Color GetLogLevelColor(LogLevel level)
    {
        return level switch
        {
            LogLevel.Trace => UITheme.Dark.TextDim,
            LogLevel.Debug => UITheme.Dark.Info,
            LogLevel.Information => UITheme.Dark.TextPrimary,
            LogLevel.Warning => UITheme.Dark.Warning,
            LogLevel.Error => UITheme.Dark.Error,
            LogLevel.Critical => UITheme.Dark.Error,
            _ => UITheme.Dark.TextPrimary
        };
    }

    /// <summary>
    /// Gets a short name for a log level (5 chars).
    /// </summary>
    private string GetLogLevelShortName(LogLevel level)
    {
        return level switch
        {
            LogLevel.Trace => "TRACE",
            LogLevel.Debug => "DEBUG",
            LogLevel.Information => "INFO",
            LogLevel.Warning => "WARN",
            LogLevel.Error => "ERROR",
            LogLevel.Critical => "CRIT",
            _ => "LOG"
        };
    }

    /// <summary>
    /// Gets a display name for a log level.
    /// </summary>
    private string GetFilterLevelName(LogLevel level)
    {
        return level switch
        {
            LogLevel.Trace => "Trace",
            LogLevel.Debug => "Debug",
            LogLevel.Information => "Information",
            LogLevel.Warning => "Warning",
            LogLevel.Error => "Error",
            LogLevel.Critical => "Critical",
            _ => "All"
        };
    }
}

