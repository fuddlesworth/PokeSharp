using System;
using System.Collections.Generic;
using System.Linq;
using Microsoft.Extensions.Logging;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using PokeSharp.Engine.Scenes;
using PokeSharp.Engine.UI.Debug.Components.Controls;
using PokeSharp.Engine.UI.Debug.Components.Debug;
using PokeSharp.Engine.UI.Debug.Core;
using PokeSharp.Engine.UI.Debug.Input;
using PokeSharp.Engine.UI.Debug.Layout;

namespace PokeSharp.Engine.UI.Debug.Scenes;

/// <summary>
/// Console scene using the new UI framework.
/// This is the modern replacement for the old QuakeConsole.
/// </summary>
public class NewConsoleScene : SceneBase
{
    private UIContext? _uiContext;
    private InputState _inputState = new();
    private TabContainer? _tabContainer;
    private ConsolePanel? _consolePanel;
    private WatchPanel? _watchPanel;
    private LogsPanel? _logsPanel;
    private VariablesPanel? _variablesPanel;

    // Events for integration with ConsoleSystem
    public event Action<string>? OnCommandSubmitted;
    public event Action<string>? OnRequestCompletions;
    public event Action<string, int>? OnRequestParameterHints; // (text, cursorPos)
    public event Action<string>? OnRequestDocumentation; // (completionText)
    public event Action? OnCloseRequested;

    // Configuration
    private float _consoleHeightPercent = 0.5f; // 50% of screen height by default

    public NewConsoleScene(
        GraphicsDevice graphicsDevice,
        IServiceProvider services,
        ILogger<NewConsoleScene> logger
    ) : base(graphicsDevice, services, logger)
    {
        // Console should block input to scenes below
        ExclusiveInput = true;

        // Render scenes below so game is visible behind console
        RenderScenesBelow = true;
    }

    /// <summary>
    /// Sets the console height as a percentage of screen height.
    /// </summary>
    public void SetHeightPercent(float percent)
    {
        _consoleHeightPercent = Math.Clamp(percent, 0.25f, 1.0f);

        if (_tabContainer != null)
        {
            _tabContainer.Constraint = new LayoutConstraint
            {
                Anchor = Anchor.StretchTop,
                HeightPercent = _consoleHeightPercent,
                Padding = 10f // Preserve padding
            };
        }
    }

    /// <summary>
    /// Appends a line to the console output.
    /// </summary>
    public void AppendOutput(string text, Color color, string category = "General")
    {
        _consolePanel?.AppendOutput(text, color, category);
    }

    /// <summary>
    /// Clears all console output.
    /// </summary>
    public void ClearOutput()
    {
        _consolePanel?.ClearOutput();
    }

    /// <summary>
    /// Sets auto-completion suggestions.
    /// </summary>
    public void SetCompletions(List<string> completions)
    {
        _consolePanel?.SetCompletions(completions);
    }

    public void SetCompletions(List<SuggestionItem> suggestions)
    {
        _consolePanel?.SetCompletions(suggestions);
    }

    /// <summary>
    /// Gets the current cursor position in the command input.
    /// </summary>
    public int GetCursorPosition()
    {
        return _consolePanel?.GetCursorPosition() ?? 0;
    }

    /// <summary>
    /// Sets parameter hints for the current method call.
    /// </summary>
    public void SetParameterHints(ParamHints hints, int currentParameterIndex = 0)
    {
        _consolePanel?.SetParameterHints(hints, currentParameterIndex);
    }

    /// <summary>
    /// Clears parameter hints.
    /// </summary>
    public void ClearParameterHints()
    {
        _consolePanel?.ClearParameterHints();
    }

    /// <summary>
    /// Sets documentation for display.
    /// </summary>
    public void SetDocumentation(DocInfo doc)
    {
        _consolePanel?.SetDocumentation(doc);
    }

    /// <summary>
    /// Clears documentation.
    /// </summary>
    public void ClearDocumentation()
    {
        _consolePanel?.ClearDocumentation();
    }

    /// <summary>
    /// Adds a watch variable to the watch panel.
    /// </summary>
    /// <param name="name">Display name for the watch</param>
    /// <param name="expression">The expression being watched (for display)</param>
    /// <param name="valueGetter">Function that returns the current value</param>
    public bool AddWatch(string name, string expression, Func<object?> valueGetter,
                        string? group = null, string? condition = null, Func<bool>? conditionEvaluator = null)
    {
        if (_watchPanel == null)
            return false;

        try
        {
            return _watchPanel.AddWatch(name, expression, valueGetter, group, condition, conditionEvaluator);
        }
        catch
        {
            return false;
        }
    }

    /// <summary>
    /// Removes a watch variable from the watch panel.
    /// </summary>
    public bool RemoveWatch(string name)
    {
        return _watchPanel?.RemoveWatch(name) ?? false;
    }

    /// <summary>
    /// Gets the active tab index.
    /// </summary>
    public int GetActiveTab()
    {
        return _tabContainer?.ActiveTabIndex ?? 0;
    }

    /// <summary>
    /// Sets the active tab by index.
    /// </summary>
    public void SetActiveTab(int index)
    {
        _tabContainer?.SetActiveTab(index);
    }

    /// <summary>
    /// Clears all watches.
    /// </summary>
    public void ClearWatches()
    {
        _watchPanel?.ClearWatches();
    }

    /// <summary>
    /// Toggles watch auto-update.
    /// </summary>
    public bool ToggleWatchAutoUpdate()
    {
        if (_watchPanel == null)
            return false;

        _watchPanel.AutoUpdate = !_watchPanel.AutoUpdate;
        return _watchPanel.AutoUpdate;
    }

    /// <summary>
    /// Gets watch count.
    /// </summary>
    public int GetWatchCount()
    {
        return _watchPanel?.Count ?? 0;
    }

    /// <summary>
    /// Pins a watch to the top.
    /// </summary>
    public bool PinWatch(string name)
    {
        return _watchPanel?.PinWatch(name) ?? false;
    }

    /// <summary>
    /// Unpins a watch.
    /// </summary>
    public bool UnpinWatch(string name)
    {
        return _watchPanel?.UnpinWatch(name) ?? false;
    }

    /// <summary>
    /// Checks if a watch is pinned.
    /// </summary>
    public bool IsWatchPinned(string name)
    {
        return _watchPanel?.IsWatchPinned(name) ?? false;
    }

    /// <summary>
    /// Sets the watch update interval.
    /// </summary>
    public bool SetWatchInterval(double intervalSeconds)
    {
        if (_watchPanel == null)
            return false;

        // Validate interval
        if (intervalSeconds < WatchPanel.MinUpdateInterval || intervalSeconds > WatchPanel.MaxUpdateInterval)
            return false;

        _watchPanel.UpdateInterval = intervalSeconds;
        return true;
    }

    /// <summary>
    /// Collapses a watch group.
    /// </summary>
    public bool CollapseWatchGroup(string groupName)
    {
        return _watchPanel?.CollapseGroup(groupName) ?? false;
    }

    /// <summary>
    /// Expands a watch group.
    /// </summary>
    public bool ExpandWatchGroup(string groupName)
    {
        return _watchPanel?.ExpandGroup(groupName) ?? false;
    }

    /// <summary>
    /// Toggles a watch group's collapsed state.
    /// </summary>
    public bool ToggleWatchGroup(string groupName)
    {
        return _watchPanel?.ToggleGroup(groupName) ?? false;
    }

    /// <summary>
    /// Gets all watch group names.
    /// </summary>
    public IEnumerable<string> GetWatchGroups()
    {
        return _watchPanel?.GetGroups() ?? Enumerable.Empty<string>();
    }

    /// <summary>
    /// Sets an alert on a watch.
    /// </summary>
    public bool SetWatchAlert(string name, string alertType, object? threshold)
    {
        return _watchPanel?.SetAlert(name, alertType, threshold, OnWatchAlertTriggered) ?? false;
    }

    /// <summary>
    /// Removes an alert from a watch.
    /// </summary>
    public bool RemoveWatchAlert(string name)
    {
        return _watchPanel?.RemoveAlert(name) ?? false;
    }

    /// <summary>
    /// Gets all watches with alerts.
    /// </summary>
    public IEnumerable<(string Name, string AlertType, bool Triggered)> GetWatchesWithAlerts()
    {
        return _watchPanel?.GetWatchesWithAlerts() ?? Enumerable.Empty<(string, string, bool)>();
    }

    /// <summary>
    /// Clears alert triggered status for a watch.
    /// </summary>
    public bool ClearWatchAlertStatus(string name)
    {
        return _watchPanel?.ClearAlertStatus(name) ?? false;
    }

    /// <summary>
    /// Sets up a comparison between two watches.
    /// </summary>
    public bool SetWatchComparison(string watchName, string compareWithName, string comparisonLabel = "Expected")
    {
        return _watchPanel?.SetComparison(watchName, compareWithName, comparisonLabel) ?? false;
    }

    /// <summary>
    /// Removes comparison from a watch.
    /// </summary>
    public bool RemoveWatchComparison(string name)
    {
        return _watchPanel?.RemoveComparison(name) ?? false;
    }

    /// <summary>
    /// Gets all watches with comparisons.
    /// </summary>
    public IEnumerable<(string Name, string ComparedWith)> GetWatchesWithComparisons()
    {
        return _watchPanel?.GetWatchesWithComparisons() ?? Enumerable.Empty<(string, string)>();
    }

    /// <summary>
    /// Called when a watch alert is triggered.
    /// </summary>
    private void OnWatchAlertTriggered(string watchName, object? value, object? threshold)
    {
        // Log the alert to console output
        var thresholdStr = threshold != null ? $" (threshold: {threshold})" : "";
        AppendOutput($"⚠ WATCH ALERT: '{watchName}' = {value}{thresholdStr}", UITheme.Dark.Error);
    }

    /// <summary>
    /// Sets log filter level.
    /// </summary>
    public void SetLogFilter(Microsoft.Extensions.Logging.LogLevel level)
    {
        _logsPanel?.SetFilterLevel(level);
    }

    /// <summary>
    /// Sets log search filter.
    /// </summary>
    public void SetLogSearch(string? searchText)
    {
        _logsPanel?.SetSearchFilter(searchText);
    }

    /// <summary>
    /// Adds a log entry.
    /// </summary>
    public void AddLog(Microsoft.Extensions.Logging.LogLevel level, string message, string category = "General")
    {
        _logsPanel?.AddLog(level, message, category);
    }

    /// <summary>
    /// Clears all logs.
    /// </summary>
    public void ClearLogs()
    {
        _logsPanel?.ClearLogs();
    }

    /// <summary>
    /// Gets total log count.
    /// </summary>
    public int GetLogCount()
    {
        return _logsPanel?.GetTotalLogCount() ?? 0;
    }

    /// <summary>
    /// Exports current watch configuration.
    /// </summary>
    public (List<(string Name, string Expression, string? Group, string? Condition, bool IsPinned,
                   string? AlertType, object? AlertThreshold, string? ComparisonWith, string? ComparisonLabel)> Watches,
            double UpdateInterval,
            bool AutoUpdateEnabled)? ExportWatchConfiguration()
    {
        if (_watchPanel == null)
            return null;

        return _watchPanel.ExportConfiguration();
    }

    /// <summary>
    /// Imports watch configuration. Note: This only sets the configuration metadata.
    /// Actual watch entries must be added separately with AddWatch() since they require
    /// function delegates that can't be serialized.
    /// </summary>
    public void ImportWatchConfiguration(double updateInterval, bool autoUpdateEnabled)
    {
        if (_watchPanel == null)
            return;

        _watchPanel.ClearWatches();
        _watchPanel.UpdateInterval = updateInterval;
        _watchPanel.AutoUpdate = autoUpdateEnabled;
    }

    /// <summary>
    /// Gets the command history.
    /// </summary>
    public IReadOnlyList<string> GetCommandHistory()
    {
        return _consolePanel?.GetCommandHistory() ?? Array.Empty<string>();
    }

    /// <summary>
    /// Clears the command history.
    /// </summary>
    public void ClearCommandHistory()
    {
        _consolePanel?.ClearCommandHistory();
    }

    /// <summary>
    /// Saves the command history to disk.
    /// </summary>
    public void SaveCommandHistory()
    {
        _consolePanel?.SaveCommandHistory();
    }

    /// <summary>
    /// Loads the command history from disk.
    /// </summary>
    public void LoadCommandHistory()
    {
        _consolePanel?.LoadCommandHistory();
    }

    public override void Initialize()
    {
        base.Initialize();

        try
        {
            _uiContext = new UIContext(GraphicsDevice);
            Logger.LogInformation("NewConsoleScene UI context initialized");
        }
        catch (Exception ex)
        {
            Logger.LogError(ex, "Failed to initialize NewConsoleScene UI context");
            throw;
        }
    }

    public override void LoadContent()
    {
        base.LoadContent();

        try
        {
            // Load font system
            var fontSystem = Utilities.FontLoader.LoadSystemMonospaceFont();
            if (fontSystem == null)
            {
                Logger.LogError("Failed to load font system for console");
                throw new InvalidOperationException("Font system loading failed");
            }

            _uiContext?.SetFontSystem(fontSystem);

            // Create tab container
            _tabContainer = new TabContainer
            {
                Id = "debug_tab_container",
                BackgroundColor = UITheme.Dark.ConsoleBackground,
                BorderColor = UITheme.Dark.BorderPrimary,
                BorderThickness = 1,
                Constraint = new LayoutConstraint
                {
                    Anchor = Anchor.StretchTop,
                    HeightPercent = _consoleHeightPercent,
                    Padding = 10f // Apply padding to create space around console content
                }
            };
            // Create console panel via builder
            _consolePanel = ConsolePanelBuilder.Create().Build();
            _consolePanel.BackgroundColor = Color.Transparent; // Tab container provides background
            _consolePanel.BorderColor = Color.Transparent; // No border - container has it
            _consolePanel.BorderThickness = 0;
            _consolePanel.Constraint = new LayoutConstraint
            {
                Anchor = Anchor.Fill,
                Padding = 0
            };
            Logger.LogInformation("Console panel created");

            Logger.LogInformation("Wiring up console events...");
            // Wire up console events
            _consolePanel.OnCommandSubmitted = (cmd) => OnCommandSubmitted?.Invoke(cmd);
            _consolePanel.OnRequestCompletions = (text) => OnRequestCompletions?.Invoke(text);
            _consolePanel.OnRequestParameterHints = (text, cursorPos) => OnRequestParameterHints?.Invoke(text, cursorPos);
            _consolePanel.OnRequestDocumentation = (completionText) => OnRequestDocumentation?.Invoke(completionText);
            _consolePanel.OnCloseRequested = () => OnCloseRequested?.Invoke();
            _consolePanel.OnSizeChanged = (size) => SetHeightPercent(size.GetHeightPercent());
            Logger.LogInformation("Console events wired up");

            Logger.LogInformation("Creating watch panel...");
            _watchPanel = WatchPanelBuilder.Create().Build();
            _watchPanel.BackgroundColor = Color.Transparent;
            _watchPanel.BorderColor = Color.Transparent;
            _watchPanel.BorderThickness = 0;
            _watchPanel.Constraint = new LayoutConstraint { Anchor = Anchor.Fill, Padding = 0 };
            Logger.LogInformation("Watch panel created");

            Logger.LogInformation("Console initialization complete. Use 'watch add' to monitor expressions.");

            Logger.LogInformation("Creating logs panel...");
            _logsPanel = LogsPanelBuilder.Create().Build();
            _logsPanel.BackgroundColor = Color.Transparent;
            _logsPanel.BorderColor = Color.Transparent;
            _logsPanel.BorderThickness = 0;
            _logsPanel.Constraint = new LayoutConstraint { Anchor = Anchor.Fill, Padding = 0 };
            Logger.LogInformation("Logs panel created");

            Logger.LogInformation("Creating variables panel...");
            _variablesPanel = VariablesPanelBuilder.Create().Build();
            _variablesPanel.BackgroundColor = Color.Transparent;
            _variablesPanel.BorderColor = Color.Transparent;
            _variablesPanel.BorderThickness = 0;
            _variablesPanel.Constraint = new LayoutConstraint { Anchor = Anchor.Fill, Padding = 0 };

            // Set up global variables display
            _variablesPanel.SetGlobals(new[]
            {
                new VariablesPanel.GlobalInfo
                {
                    Name = "Player",
                    TypeName = "IPlayerScripting",
                    Description = "Player control and state API"
                },
                new VariablesPanel.GlobalInfo
                {
                    Name = "World",
                    TypeName = "Arch.Core.World",
                    Description = "ECS World instance"
                },
                new VariablesPanel.GlobalInfo
                {
                    Name = "Api",
                    TypeName = "IScriptingApiProvider",
                    Description = "Scripting API provider"
                },
                new VariablesPanel.GlobalInfo
                {
                    Name = "Systems",
                    TypeName = "SystemManager",
                    Description = "ECS system manager"
                }
            });
            Logger.LogInformation("Variables panel created");

            // Add tabs to container
            try
            {
                Logger.LogInformation("Adding Console tab...");
                _tabContainer.AddTab("Console", _consolePanel);
                Logger.LogInformation("Console tab added successfully");

                Logger.LogInformation("Adding Watch tab...");
                _tabContainer.AddTab("Watch", _watchPanel);
                Logger.LogInformation("Watch tab added successfully");

                Logger.LogInformation("Adding Logs tab...");
                _tabContainer.AddTab("Logs", _logsPanel);
                Logger.LogInformation("Logs tab added successfully");

                Logger.LogInformation("Adding Variables tab...");
                _tabContainer.AddTab("Variables", _variablesPanel);
                Logger.LogInformation("Variables tab added successfully");

                // Set default tab to Console
                Logger.LogInformation($"Setting active tab to 0 (Console)");
                _tabContainer.SetActiveTab(0);

                Logger.LogInformation($"Tab container initialized with {_tabContainer.ActiveTabIndex} as active tab");
            }
            catch (Exception ex)
            {
                Logger.LogError(ex, "FAILED TO CREATE TAB SYSTEM - Exception details:");
                throw; // Re-throw to see full stack trace
            }

            // Show the console with animation
            _consolePanel.Show();

            // Add welcome messages
            Logger.LogInformation("Adding welcome messages to console...");
            AppendOutput("=== PokeSharp Debug Console ===", new Color(100, 200, 255));
            AppendOutput("Type 'help' for available commands", Color.LightGray);
            AppendOutput("Press Ctrl+~ or type 'exit' to close", Color.Gray);
            AppendOutput("", Color.White); // Blank line

            // Verify messages were added
            Logger.LogInformation($"Welcome messages added. TextBuffer has {_consolePanel?.GetOutputLineCount() ?? -1} lines. Console ready.");

            Logger.LogInformation("=== NewConsoleScene.LoadContent() COMPLETE ===");
        }
        catch (Exception ex)
        {
            Logger.LogError(ex, "Failed to load NewConsoleScene content");
            throw;
        }
    }

    public override void Update(GameTime gameTime)
    {
        if (_uiContext == null || _consolePanel == null)
            return;

        try
        {
            _inputState.GameTime = gameTime; // Set GameTime for cursor blinking
            _inputState.Update();

            // Handle tab switching shortcuts (Ctrl+1 through Ctrl+4)
            HandleTabShortcuts();

            // Process deferred close requests (safe to do during Update, not Draw)
            _consolePanel.ProcessDeferredClose();
        }
        catch (Exception ex)
        {
            Logger.LogError(ex, "Error updating console input state");
        }
    }

    /// <summary>
    /// Handles keyboard shortcuts for tab switching.
    /// </summary>
    private void HandleTabShortcuts()
    {
        if (_tabContainer == null || _inputState == null)
            return;

        // Check if Ctrl is held
        if (!_inputState.IsCtrlDown())
            return;

        // Check for number keys 1-4 (just pressed, not repeat)
        if (_inputState.IsKeyPressed(Microsoft.Xna.Framework.Input.Keys.D1))
        {
            _tabContainer.SetActiveTab(0); // Console
            Logger.LogDebug("Switched to Console tab (Ctrl+1)");
        }
        else if (_inputState.IsKeyPressed(Microsoft.Xna.Framework.Input.Keys.D2))
        {
            _tabContainer.SetActiveTab(1); // Watch
            Logger.LogDebug("Switched to Watch tab (Ctrl+2)");
        }
        else if (_inputState.IsKeyPressed(Microsoft.Xna.Framework.Input.Keys.D3))
        {
            _tabContainer.SetActiveTab(2); // Logs
            Logger.LogDebug("Switched to Logs tab (Ctrl+3)");
        }
        else if (_inputState.IsKeyPressed(Microsoft.Xna.Framework.Input.Keys.D4))
        {
            _tabContainer.SetActiveTab(3); // Variables
            Logger.LogDebug("Switched to Variables tab (Ctrl+4)");
        }
    }

    public override void Draw(GameTime gameTime)
    {
        if (_uiContext == null || _tabContainer == null)
        {
            Logger.LogWarning("Cannot draw: UIContext or TabContainer is null");
            return;
        }

        bool beginFrameCalled = false;
        try
        {
            // Don't clear - let the game render behind us
            // The console panel has a semi-transparent background

            // Update screen size in case window was resized
            _uiContext.UpdateScreenSize(GraphicsDevice.Viewport.Width, GraphicsDevice.Viewport.Height);

            // Begin frame and update input state
            _uiContext.BeginFrame(_inputState);
            beginFrameCalled = true;

            // CRITICAL: Update ONLY hover state before rendering
            // This provides immediate hover feedback using previous frame's positions
            // Incremental updates during RegisterComponent() will refine this with current positions
            _uiContext.UpdateHoverState();

            // Render tab container UI (includes all tabs and active content)
            // Components register during render and get accurate hover state via incremental updates
            _tabContainer.Render(_uiContext);

            // CRITICAL: Update pressed state AFTER rendering
            // This ensures pressed state is set based on correct hover state from incremental updates
            // This is the key fix - pressed state must use hover state AFTER components register
            _uiContext.UpdatePressedState();
        }
        catch (Exception ex)
        {
            Logger.LogError(ex, "Error rendering console");
        }
        finally
        {
            // CRITICAL: Always call EndFrame if BeginFrame was called, even if an exception occurred
            if (beginFrameCalled)
            {
                try
                {
                    _uiContext.EndFrame();
                }
                catch (Exception endEx)
                {
                    Logger.LogError(endEx, "Error in EndFrame");
                }
            }
        }
    }

    protected override void Dispose(bool disposing)
    {
        if (disposing)
        {
            _uiContext?.Dispose();
            Logger.LogInformation("NewConsoleScene disposed");
        }

        base.Dispose(disposing);
    }
}

