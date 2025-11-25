using System;
using Microsoft.Xna.Framework;
using Microsoft.Extensions.DependencyInjection;
using PokeSharp.Engine.UI.Debug.Core;
using PokeSharp.Engine.UI.Debug.Scenes;
using PokeSharp.Engine.Debug.Console.Features;
using PokeSharp.Engine.Debug.Console.Scripting;
using PokeSharp.Engine.Debug.Scripting;
using PokeSharp.Engine.Debug.Features;

namespace PokeSharp.Engine.Debug.Commands;

/// <summary>
/// Implementation of IConsoleContext that provides services to command execution.
/// </summary>
public class ConsoleContext : IConsoleContext
{
    private readonly NewConsoleScene _consoleScene;
    private readonly Action _closeAction;
    private readonly Func<bool> _isLoggingEnabledFunc;
    private readonly Action<bool> _setLoggingEnabledAction;
    private readonly Func<Microsoft.Extensions.Logging.LogLevel> _getLogLevelFunc;
    private readonly Action<Microsoft.Extensions.Logging.LogLevel> _setLogLevelAction;
    private readonly ConsoleCommandRegistry _commandRegistry;
    private readonly AliasMacroManager _aliasManager;
    private readonly ScriptManager _scriptManager;
    private readonly ConsoleScriptEvaluator _scriptEvaluator;
    private readonly ConsoleGlobals _consoleGlobals;
    private readonly BookmarkedCommandsManager _bookmarkManager;
    private readonly WatchPresetManager _watchPresetManager;

    public ConsoleContext(
        NewConsoleScene consoleScene,
        Action closeAction,
        Func<bool> isLoggingEnabledFunc,
        Action<bool> setLoggingEnabledAction,
        Func<Microsoft.Extensions.Logging.LogLevel> getLogLevelFunc,
        Action<Microsoft.Extensions.Logging.LogLevel> setLogLevelAction,
        ConsoleCommandRegistry commandRegistry,
        AliasMacroManager aliasManager,
        ScriptManager scriptManager,
        ConsoleScriptEvaluator scriptEvaluator,
        ConsoleGlobals consoleGlobals,
        BookmarkedCommandsManager bookmarkManager,
        WatchPresetManager watchPresetManager)
    {
        _consoleScene = consoleScene ?? throw new ArgumentNullException(nameof(consoleScene));
        _closeAction = closeAction ?? throw new ArgumentNullException(nameof(closeAction));
        _isLoggingEnabledFunc = isLoggingEnabledFunc ?? throw new ArgumentNullException(nameof(isLoggingEnabledFunc));
        _setLoggingEnabledAction = setLoggingEnabledAction ?? throw new ArgumentNullException(nameof(setLoggingEnabledAction));
        _getLogLevelFunc = getLogLevelFunc ?? throw new ArgumentNullException(nameof(getLogLevelFunc));
        _setLogLevelAction = setLogLevelAction ?? throw new ArgumentNullException(nameof(setLogLevelAction));
        _commandRegistry = commandRegistry ?? throw new ArgumentNullException(nameof(commandRegistry));
        _aliasManager = aliasManager ?? throw new ArgumentNullException(nameof(aliasManager));
        _scriptManager = scriptManager ?? throw new ArgumentNullException(nameof(scriptManager));
        _scriptEvaluator = scriptEvaluator ?? throw new ArgumentNullException(nameof(scriptEvaluator));
        _consoleGlobals = consoleGlobals ?? throw new ArgumentNullException(nameof(consoleGlobals));
        _bookmarkManager = bookmarkManager ?? throw new ArgumentNullException(nameof(bookmarkManager));
        _watchPresetManager = watchPresetManager ?? throw new ArgumentNullException(nameof(watchPresetManager));
    }

    public UITheme Theme => UITheme.Dark;

    public void WriteLine(string text)
    {
        _consoleScene.AppendOutput(text, Theme.TextPrimary);
    }

    public void WriteLine(string text, Color color)
    {
        _consoleScene.AppendOutput(text, color);
    }

    public void Clear()
    {
        _consoleScene.ClearOutput();
    }

    public bool IsLoggingEnabled => _isLoggingEnabledFunc();

    public void SetLoggingEnabled(bool enabled)
    {
        _setLoggingEnabledAction(enabled);
    }

    public Microsoft.Extensions.Logging.LogLevel MinimumLogLevel => _getLogLevelFunc();

    public void SetMinimumLogLevel(Microsoft.Extensions.Logging.LogLevel level)
    {
        _setLogLevelAction(level);
    }

    public void Close()
    {
        _closeAction();
    }

    public IEnumerable<IConsoleCommand> GetAllCommands()
    {
        return _commandRegistry.GetAllCommands();
    }

    public IConsoleCommand? GetCommand(string name)
    {
        return _commandRegistry.GetCommand(name);
    }

    public IReadOnlyList<string> GetCommandHistory()
    {
        return _consoleScene.GetCommandHistory();
    }

    public void ClearCommandHistory()
    {
        _consoleScene.ClearCommandHistory();
    }

    public void SaveCommandHistory()
    {
        _consoleScene.SaveCommandHistory();
    }

    public void LoadCommandHistory()
    {
        _consoleScene.LoadCommandHistory();
    }

    public bool DefineAlias(string name, string command)
    {
        var result = _aliasManager.DefineAlias(name, command);
        if (result)
        {
            _aliasManager.SaveAliases();
        }
        return result;
    }

    public bool RemoveAlias(string name)
    {
        var result = _aliasManager.RemoveAlias(name);
        if (result)
        {
            _aliasManager.SaveAliases();
        }
        return result;
    }

    public IReadOnlyDictionary<string, string> GetAllAliases()
    {
        return _aliasManager.GetAllAliases();
    }

    public List<string> ListScripts()
    {
        return _scriptManager.ListScripts();
    }

    public string GetScriptsDirectory()
    {
        return _scriptManager.ScriptsDirectory;
    }

    public string? LoadScript(string filename)
    {
        var result = _scriptManager.LoadScript(filename);
        return result.IsSuccess ? result.Value : null;
    }

    public bool SaveScript(string filename, string content)
    {
        var result = _scriptManager.SaveScript(filename, content);
        return result.IsSuccess;
    }

    public async Task ExecuteScriptAsync(string scriptContent)
    {
        var result = await _scriptEvaluator.EvaluateAsync(scriptContent, _consoleGlobals);

        // Handle compilation errors
        if (result.IsCompilationError && result.Errors != null)
        {
            WriteLine("Compilation Error:", Theme.Error);
            foreach (var error in result.Errors)
            {
                WriteLine($"  {error.Message}", Theme.Error);
            }
            return;
        }

        // Handle runtime errors
        if (result.IsRuntimeError)
        {
            WriteLine($"Runtime Error: {result.RuntimeException?.Message ?? "Unknown error"}", Theme.Error);
            if (result.RuntimeException != null)
            {
                WriteLine($"  {result.RuntimeException.GetType().Name}", Theme.TextSecondary);
            }
            return;
        }

        // Display result if there is one
        if (!string.IsNullOrWhiteSpace(result.Output))
        {
            WriteLine(result.Output, Theme.Success);
        }
    }

    public void ResetScriptState()
    {
        _scriptEvaluator.Reset();
    }

    public IReadOnlyDictionary<int, string> GetAllBookmarks()
    {
        return _bookmarkManager.GetAllBookmarks();
    }

    public string? GetBookmark(int fkeyNumber)
    {
        return _bookmarkManager.GetBookmark(fkeyNumber);
    }

    public bool SetBookmark(int fkeyNumber, string command)
    {
        var result = _bookmarkManager.BookmarkCommand(fkeyNumber, command);
        if (result) _bookmarkManager.SaveBookmarks(); // Auto-save on change
        return result;
    }

    public bool RemoveBookmark(int fkeyNumber)
    {
        var result = _bookmarkManager.RemoveBookmark(fkeyNumber);
        if (result) _bookmarkManager.SaveBookmarks(); // Auto-save on change
        return result;
    }

    public void ClearAllBookmarks()
    {
        _bookmarkManager.ClearAll();
        _bookmarkManager.SaveBookmarks(); // Auto-save on change
    }

    public bool SaveBookmarks()
    {
        return _bookmarkManager.SaveBookmarks();
    }

    public int LoadBookmarks()
    {
        return _bookmarkManager.LoadBookmarks();
    }

    public bool AddWatch(string name, string expression)
    {
        return AddWatch(name, expression, null, null);
    }

    public bool AddWatch(string name, string expression, string? group, string? condition)
    {
        // Create value evaluator lambda
        Func<object?> valueGetter = () =>
        {
            try
            {
                var task = _scriptEvaluator.EvaluateAsync(expression, _consoleGlobals);
                task.Wait();
                var result = task.Result;

                if (result.IsSuccess)
                {
                    return string.IsNullOrWhiteSpace(result.Output) ? "<null>" : result.Output;
                }
                else
                {
                    if (result.Errors != null && result.Errors.Count > 0)
                    {
                        return $"<error: {result.Errors[0].Message}>";
                    }
                    return "<error: evaluation failed>";
                }
            }
            catch (Exception ex)
            {
                return $"<error: {ex.Message}>";
            }
        };

        // Create condition evaluator lambda if condition provided
        Func<bool>? conditionEvaluator = null;
        if (!string.IsNullOrWhiteSpace(condition))
        {
            conditionEvaluator = () =>
            {
                try
                {
                    var task = _scriptEvaluator.EvaluateAsync(condition, _consoleGlobals);
                    task.Wait();
                    var result = task.Result;

                    if (result.IsSuccess && !string.IsNullOrWhiteSpace(result.Output))
                    {
                        // Try to parse as boolean
                        if (bool.TryParse(result.Output.Trim(), out var boolResult))
                        {
                            return boolResult;
                        }
                        // Non-empty result treated as true
                        return true;
                    }
                    return false;
                }
                catch
                {
                    return false;
                }
            };
        }

        return _consoleScene.AddWatch(name, expression, valueGetter, group, condition, conditionEvaluator);
    }

    public bool RemoveWatch(string name)
    {
        return _consoleScene.RemoveWatch(name);
    }

    public void ClearWatches()
    {
        _consoleScene.ClearWatches();
    }

    public bool ToggleWatchAutoUpdate()
    {
        return _consoleScene.ToggleWatchAutoUpdate();
    }

    public int GetWatchCount()
    {
        return _consoleScene.GetWatchCount();
    }

    public bool PinWatch(string name)
    {
        return _consoleScene.PinWatch(name);
    }

    public bool UnpinWatch(string name)
    {
        return _consoleScene.UnpinWatch(name);
    }

    public bool IsWatchPinned(string name)
    {
        return _consoleScene.IsWatchPinned(name);
    }

    public bool SetWatchInterval(double intervalSeconds)
    {
        return _consoleScene.SetWatchInterval(intervalSeconds);
    }

    public bool CollapseWatchGroup(string groupName)
    {
        return _consoleScene.CollapseWatchGroup(groupName);
    }

    public bool ExpandWatchGroup(string groupName)
    {
        return _consoleScene.ExpandWatchGroup(groupName);
    }

    public bool ToggleWatchGroup(string groupName)
    {
        return _consoleScene.ToggleWatchGroup(groupName);
    }

    public IEnumerable<string> GetWatchGroups()
    {
        return _consoleScene.GetWatchGroups();
    }

    public bool SetWatchAlert(string name, string alertType, object? threshold)
    {
        return _consoleScene.SetWatchAlert(name, alertType, threshold);
    }

    public bool RemoveWatchAlert(string name)
    {
        return _consoleScene.RemoveWatchAlert(name);
    }

    public IEnumerable<(string Name, string AlertType, bool Triggered)> GetWatchesWithAlerts()
    {
        return _consoleScene.GetWatchesWithAlerts();
    }

    public bool ClearWatchAlertStatus(string name)
    {
        return _consoleScene.ClearWatchAlertStatus(name);
    }

    public bool SetWatchComparison(string watchName, string compareWithName, string comparisonLabel = "Expected")
    {
        return _consoleScene.SetWatchComparison(watchName, compareWithName, comparisonLabel);
    }

    public bool RemoveWatchComparison(string name)
    {
        return _consoleScene.RemoveWatchComparison(name);
    }

    public IEnumerable<(string Name, string ComparedWith)> GetWatchesWithComparisons()
    {
        return _consoleScene.GetWatchesWithComparisons();
    }

    public void SetLogFilter(Microsoft.Extensions.Logging.LogLevel level)
    {
        _consoleScene.SetLogFilter(level);
    }

    public void SetLogSearch(string? searchText)
    {
        _consoleScene.SetLogSearch(searchText);
    }

    public void AddLog(Microsoft.Extensions.Logging.LogLevel level, string message, string category = "General")
    {
        _consoleScene.AddLog(level, message, category);
    }

    public void ClearLogs()
    {
        _consoleScene.ClearLogs();
    }

    public int GetLogCount()
    {
        return _consoleScene.GetLogCount();
    }

    public bool SaveWatchPreset(string name, string description)
    {
        try
        {
            var config = _consoleScene.ExportWatchConfiguration();
            if (config == null)
                return false;

            var (watches, updateInterval, autoUpdateEnabled) = config.Value;

            var preset = new WatchPreset
            {
                Name = name,
                Description = description,
                CreatedAt = DateTime.Now,
                UpdateInterval = updateInterval,
                AutoUpdateEnabled = autoUpdateEnabled,
                Watches = watches.Select(w => new WatchPresetEntry
                {
                    Name = w.Name,
                    Expression = w.Expression,
                    Group = w.Group,
                    Condition = w.Condition,
                    IsPinned = w.IsPinned,
                    Alert = w.AlertType != null ? new WatchAlertConfig
                    {
                        Type = w.AlertType,
                        Threshold = w.AlertThreshold?.ToString()
                    } : null,
                    Comparison = w.ComparisonWith != null ? new WatchComparisonConfig
                    {
                        CompareWith = w.ComparisonWith,
                        Label = w.ComparisonLabel ?? "Expected"
                    } : null
                }).ToList()
            };

            return _watchPresetManager.SavePreset(preset);
        }
        catch
        {
            return false;
        }
    }

    public bool LoadWatchPreset(string name)
    {
        try
        {
            var preset = _watchPresetManager.LoadPreset(name);
            if (preset == null)
                return false;

            // Import configuration (clears watches and sets update settings)
            _consoleScene.ImportWatchConfiguration(preset.UpdateInterval, preset.AutoUpdateEnabled);

            // Add watches from preset
            foreach (var watch in preset.Watches)
            {
                // Create value getter for the expression
                System.Func<object?> valueGetter = () =>
                {
                    try
                    {
                        var result = _scriptEvaluator.EvaluateAsync(watch.Expression, _consoleGlobals).Result;
                        return result.IsSuccess ? result.Output : $"<error: {result.Errors?[0].Message ?? "evaluation failed"}>";
                    }
                    catch (Exception ex)
                    {
                        return $"<error: {ex.Message}>";
                    }
                };

                // Create condition evaluator if condition exists
                System.Func<bool>? conditionEvaluator = null;
                if (!string.IsNullOrEmpty(watch.Condition))
                {
                    conditionEvaluator = () =>
                    {
                        try
                        {
                            var result = _scriptEvaluator.EvaluateAsync(watch.Condition, _consoleGlobals).Result;
                            if (!result.IsSuccess) return false;
                            // Output is a string representation, parse it as boolean
                            if (string.IsNullOrEmpty(result.Output)) return false;
                            if (bool.TryParse(result.Output, out var boolValue)) return boolValue;
                            // Consider "true" (case-insensitive) or non-empty strings as true
                            return result.Output.Equals("true", StringComparison.OrdinalIgnoreCase);
                        }
                        catch
                        {
                            return false;
                        }
                    };
                }

                // Add the watch
                AddWatch(watch.Name, watch.Expression, watch.Group, watch.Condition);

                // Pin if needed
                if (watch.IsPinned)
                {
                    PinWatch(watch.Name);
                }

                // Set up alert if configured
                if (watch.Alert != null)
                {
                    object? alertThreshold = null;
                    if (watch.Alert.Threshold != null)
                    {
                        if (double.TryParse(watch.Alert.Threshold, out var numThreshold))
                        {
                            alertThreshold = numThreshold;
                        }
                        else
                        {
                            alertThreshold = watch.Alert.Threshold;
                        }
                    }

                    SetWatchAlert(watch.Name, watch.Alert.Type, alertThreshold);
                }

                // Set up comparison if configured
                if (watch.Comparison != null)
                {
                    SetWatchComparison(watch.Name, watch.Comparison.CompareWith, watch.Comparison.Label);
                }
            }

            WriteLine($"Loaded preset '{name}' ({preset.Watches.Count} watches)", Theme.Success);
            return true;
        }
        catch (Exception ex)
        {
            WriteLine($"Failed to load preset '{name}': {ex.Message}", Theme.Error);
            return false;
        }
    }

    public IEnumerable<(string Name, string Description, int WatchCount, DateTime CreatedAt)> ListWatchPresets()
    {
        return _watchPresetManager.ListPresets();
    }

    public bool DeleteWatchPreset(string name)
    {
        return _watchPresetManager.DeletePreset(name);
    }

    public bool WatchPresetExists(string name)
    {
        return _watchPresetManager.PresetExists(name);
    }

    public void CreateBuiltInWatchPresets()
    {
        _watchPresetManager.CreateBuiltInPresets();
    }

    public void SwitchToTab(int tabIndex)
    {
        _consoleScene.SetActiveTab(tabIndex);
    }

    public int GetActiveTab()
    {
        return _consoleScene.GetActiveTab();
    }
}

