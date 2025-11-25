using Microsoft.Xna.Framework;
using PokeSharp.Engine.UI.Debug.Core;

namespace PokeSharp.Engine.Debug.Commands;

/// <summary>
/// Provides context and services for console command execution.
/// </summary>
public interface IConsoleContext
{
    /// <summary>
    /// Gets the current UI theme.
    /// </summary>
    UITheme Theme { get; }

    /// <summary>
    /// Writes a line of text to the console output with default color.
    /// </summary>
    void WriteLine(string text);

    /// <summary>
    /// Writes a line of text to the console output with specified color.
    /// </summary>
    void WriteLine(string text, Color color);

    /// <summary>
    /// Clears all console output.
    /// </summary>
    void Clear();

    /// <summary>
    /// Gets whether console logging is currently enabled.
    /// </summary>
    bool IsLoggingEnabled { get; }

    /// <summary>
    /// Sets whether console logging is enabled.
    /// </summary>
    void SetLoggingEnabled(bool enabled);

    /// <summary>
    /// Gets the current minimum log level.
    /// </summary>
    Microsoft.Extensions.Logging.LogLevel MinimumLogLevel { get; }

    /// <summary>
    /// Sets the minimum log level for console output.
    /// </summary>
    void SetMinimumLogLevel(Microsoft.Extensions.Logging.LogLevel level);

    /// <summary>
    /// Requests the console to close.
    /// </summary>
    void Close();

    /// <summary>
    /// Gets all registered console commands.
    /// </summary>
    IEnumerable<IConsoleCommand> GetAllCommands();

    /// <summary>
    /// Gets a specific command by name.
    /// </summary>
    IConsoleCommand? GetCommand(string name);

    /// <summary>
    /// Gets the command history.
    /// </summary>
    IReadOnlyList<string> GetCommandHistory();

    /// <summary>
    /// Clears the command history.
    /// </summary>
    void ClearCommandHistory();

    /// <summary>
    /// Saves the command history to disk.
    /// </summary>
    void SaveCommandHistory();

    /// <summary>
    /// Loads the command history from disk.
    /// </summary>
    void LoadCommandHistory();

    /// <summary>
    /// Defines a command alias.
    /// </summary>
    /// <returns>True if successful, false if invalid name.</returns>
    bool DefineAlias(string name, string command);

    /// <summary>
    /// Removes a command alias.
    /// </summary>
    /// <returns>True if removed, false if not found.</returns>
    bool RemoveAlias(string name);

    /// <summary>
    /// Gets all defined aliases.
    /// </summary>
    IReadOnlyDictionary<string, string> GetAllAliases();

    /// <summary>
    /// Lists all available script files.
    /// </summary>
    List<string> ListScripts();

    /// <summary>
    /// Gets the scripts directory path.
    /// </summary>
    string GetScriptsDirectory();

    /// <summary>
    /// Loads a script file and returns its content.
    /// </summary>
    /// <returns>Script content, or null if failed.</returns>
    string? LoadScript(string filename);

    /// <summary>
    /// Saves content to a script file.
    /// </summary>
    /// <returns>True if successful.</returns>
    bool SaveScript(string filename, string content);

    /// <summary>
    /// Executes script code.
    /// </summary>
    Task ExecuteScriptAsync(string scriptContent);

    /// <summary>
    /// Resets the script evaluator state (clears variables).
    /// </summary>
    void ResetScriptState();

    /// <summary>
    /// Gets all bookmarked commands.
    /// </summary>
    IReadOnlyDictionary<int, string> GetAllBookmarks();

    /// <summary>
    /// Gets the command bookmarked to a specific F-key.
    /// </summary>
    string? GetBookmark(int fkeyNumber);

    /// <summary>
    /// Sets/updates a bookmark for a specific F-key.
    /// </summary>
    bool SetBookmark(int fkeyNumber, string command);

    /// <summary>
    /// Removes a bookmark from a specific F-key.
    /// </summary>
    bool RemoveBookmark(int fkeyNumber);

    /// <summary>
    /// Clears all bookmarks.
    /// </summary>
    void ClearAllBookmarks();

    /// <summary>
    /// Saves bookmarks to disk.
    /// </summary>
    bool SaveBookmarks();

    /// <summary>
    /// Loads bookmarks from disk.
    /// </summary>
    int LoadBookmarks();

    /// <summary>
    /// Adds a watch expression.
    /// </summary>
    bool AddWatch(string name, string expression);

    /// <summary>
    /// Adds a watch expression with optional group and condition.
    /// </summary>
    bool AddWatch(string name, string expression, string? group, string? condition);

    /// <summary>
    /// Removes a watch expression.
    /// </summary>
    bool RemoveWatch(string name);

    /// <summary>
    /// Clears all watches.
    /// </summary>
    void ClearWatches();

    /// <summary>
    /// Toggles watch auto-update.
    /// </summary>
    bool ToggleWatchAutoUpdate();

    /// <summary>
    /// Gets watch count.
    /// </summary>
    int GetWatchCount();

    /// <summary>
    /// Pins a watch to the top.
    /// </summary>
    bool PinWatch(string name);

    /// <summary>
    /// Unpins a watch.
    /// </summary>
    bool UnpinWatch(string name);

    /// <summary>
    /// Checks if a watch is pinned.
    /// </summary>
    bool IsWatchPinned(string name);

    /// <summary>
    /// Sets the watch update interval.
    /// </summary>
    bool SetWatchInterval(double intervalSeconds);

    /// <summary>
    /// Collapses a watch group.
    /// </summary>
    bool CollapseWatchGroup(string groupName);

    /// <summary>
    /// Expands a watch group.
    /// </summary>
    bool ExpandWatchGroup(string groupName);

    /// <summary>
    /// Toggles a watch group's collapsed state.
    /// </summary>
    bool ToggleWatchGroup(string groupName);

    /// <summary>
    /// Gets all watch group names.
    /// </summary>
    IEnumerable<string> GetWatchGroups();

    /// <summary>
    /// Sets an alert on a watch.
    /// </summary>
    bool SetWatchAlert(string name, string alertType, object? threshold);

    /// <summary>
    /// Removes an alert from a watch.
    /// </summary>
    bool RemoveWatchAlert(string name);

    /// <summary>
    /// Gets all watches with alerts.
    /// </summary>
    IEnumerable<(string Name, string AlertType, bool Triggered)> GetWatchesWithAlerts();

    /// <summary>
    /// Clears alert triggered status for a watch.
    /// </summary>
    bool ClearWatchAlertStatus(string name);

    /// <summary>
    /// Sets up a comparison between two watches.
    /// </summary>
    bool SetWatchComparison(string watchName, string compareWithName, string comparisonLabel = "Expected");

    /// <summary>
    /// Removes comparison from a watch.
    /// </summary>
    bool RemoveWatchComparison(string name);

    /// <summary>
    /// Gets all watches with comparisons.
    /// </summary>
    IEnumerable<(string Name, string ComparedWith)> GetWatchesWithComparisons();

    /// <summary>
    /// Sets log filter level.
    /// </summary>
    void SetLogFilter(Microsoft.Extensions.Logging.LogLevel level);

    /// <summary>
    /// Sets log search filter.
    /// </summary>
    void SetLogSearch(string? searchText);

    /// <summary>
    /// Adds a log entry.
    /// </summary>
    void AddLog(Microsoft.Extensions.Logging.LogLevel level, string message, string category = "General");

    /// <summary>
    /// Clears all logs.
    /// </summary>
    void ClearLogs();

    /// <summary>
    /// Gets total log count.
    /// </summary>
    int GetLogCount();

    /// <summary>
    /// Saves current watch configuration as a preset.
    /// </summary>
    bool SaveWatchPreset(string name, string description);

    /// <summary>
    /// Loads a watch preset by name.
    /// </summary>
    bool LoadWatchPreset(string name);

    /// <summary>
    /// Lists all available watch presets.
    /// </summary>
    IEnumerable<(string Name, string Description, int WatchCount, DateTime CreatedAt)> ListWatchPresets();

    /// <summary>
    /// Deletes a watch preset.
    /// </summary>
    bool DeleteWatchPreset(string name);

    /// <summary>
    /// Checks if a watch preset exists.
    /// </summary>
    bool WatchPresetExists(string name);

    /// <summary>
    /// Creates built-in watch presets.
    /// </summary>
    void CreateBuiltInWatchPresets();

    /// <summary>
    /// Switches to a specific tab by index.
    /// </summary>
    void SwitchToTab(int tabIndex);

    /// <summary>
    /// Gets the current active tab index.
    /// </summary>
    int GetActiveTab();
}

