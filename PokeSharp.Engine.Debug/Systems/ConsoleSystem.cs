using Arch.Core;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Microsoft.Xna.Framework.Input;
using System.Reflection;
using PokeSharp.Engine.Core.Systems;
using PokeSharp.Engine.Debug.Commands;
using PokeSharp.Engine.Debug.Console.Configuration;
using PokeSharp.Engine.Debug.Console.Features;
using PokeSharp.Engine.Debug.Console.Scripting;
using PokeSharp.Engine.Debug.Features;
using PokeSharp.Engine.Debug.Logging;
using PokeSharp.Engine.Debug.Scripting;
using PokeSharp.Engine.Debug.Services;
using PokeSharp.Engine.Scenes;
using PokeSharp.Engine.Systems.Management;
using PokeSharp.Game.Scripting.Api;
using PokeSharp.Engine.UI.Debug.Scenes;
using PokeSharp.Engine.UI.Debug.Components.Controls;
using PokeSharp.Engine.UI.Debug.Utilities;
using PokeSharp.Engine.UI.Debug.Core;

namespace PokeSharp.Engine.Debug.Systems;

/// <summary>
/// Console system that manages the modern debug console as a scene.
/// Monitors for toggle key and pushes/pops console scene onto the scene stack.
/// </summary>
public class ConsoleSystem : IUpdateSystem
{
    private readonly ILogger _logger;
    private readonly World _world;
    private readonly IScriptingApiProvider _apiProvider;
    private readonly SystemManager _systemManager;
    private readonly GraphicsDevice _graphicsDevice;
    private readonly SceneManager _sceneManager;
    private readonly IServiceProvider _services;
    private readonly ConsoleLoggerProvider? _loggerProvider;

    // Core console components (shared between console features)
    private ConsoleScriptEvaluator _evaluator = null!;
    private ConsoleGlobals _globals = null!;
    private ParameterHintProvider _parameterHintProvider = null!;
    private List<Assembly>? _referencedAssemblies;
    private ConsoleCommandRegistry _commandRegistry = null!;
    private ConsoleCompletionProvider _completionProvider = null!;
    private ConsoleDocumentationProvider _documentationProvider = null!;

    // Console state
    private KeyboardState _previousKeyboardState;
    private bool _isConsoleOpen;
    private NewConsoleScene? _consoleScene;

    // Console logging state
    private bool _loggingEnabled = false;
    private Microsoft.Extensions.Logging.LogLevel _minimumLogLevel = Microsoft.Extensions.Logging.LogLevel.Information;

    // Auto-completion debouncing
    private CancellationTokenSource? _completionCts;
    private const int CompletionDebounceMs = 50;

    // IUpdateSystem properties
    public int Priority => ConsoleConstants.System.UpdatePriority;
    public bool Enabled { get; set; } = true;

    // Theme reference
    private static UITheme Theme => UITheme.Dark;

    /// <summary>
    /// Initializes a new instance of the console system.
    /// </summary>
    public ConsoleSystem(
        World world,
        IScriptingApiProvider apiProvider,
        GraphicsDevice graphicsDevice,
        SystemManager systemManager,
        SceneManager sceneManager,
        IServiceProvider services,
        ILogger logger,
        ConsoleLoggerProvider? loggerProvider = null)
    {
        _world = world ?? throw new ArgumentNullException(nameof(world));
        _apiProvider = apiProvider ?? throw new ArgumentNullException(nameof(apiProvider));
        _graphicsDevice = graphicsDevice ?? throw new ArgumentNullException(nameof(graphicsDevice));
        _systemManager = systemManager ?? throw new ArgumentNullException(nameof(systemManager));
        _sceneManager = sceneManager ?? throw new ArgumentNullException(nameof(sceneManager));
        _services = services ?? throw new ArgumentNullException(nameof(services));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        _loggerProvider = loggerProvider;

        _previousKeyboardState = Keyboard.GetState();
    }

    public void Initialize(World world)
    {
        try
        {
            // Create command registry
            _commandRegistry = new ConsoleCommandRegistry(_logger);

            // Create script evaluator (shared component)
            _evaluator = new ConsoleScriptEvaluator(_logger);

            // Create console globals (script API)
            _globals = new ConsoleGlobals(_apiProvider, _world, _systemManager, _graphicsDevice, _logger);

            // Create parameter hint provider (shared component)
            _parameterHintProvider = new ParameterHintProvider(_logger);
            _parameterHintProvider.SetGlobals(_globals);

            // Create completion provider
            _completionProvider = new ConsoleCompletionProvider(_logger);
            _completionProvider.SetGlobals(_globals);

            // Create documentation provider
            _documentationProvider = new ConsoleDocumentationProvider(_logger);
            _documentationProvider.SetGlobals(_globals);
            _documentationProvider.SetEvaluator(_evaluator);

            // Store references for documentation
            _referencedAssemblies = ConsoleScriptEvaluator.GetDefaultReferences().ToList();
            _documentationProvider.SetReferencedAssemblies(_referencedAssemblies);

            _parameterHintProvider.SetReferences(
                _referencedAssemblies,
                ConsoleScriptEvaluator.GetDefaultImports()
            );

            // Set up console logger if provided
            if (_loggerProvider != null)
            {
                _loggerProvider.SetConsoleWriter((message, color) =>
                {
                    // Write logs to console if it's open and logging is enabled
                    if (_consoleScene != null && _loggingEnabled)
                    {
                        _consoleScene.AppendOutput(message, color, "Log");
                    }
                });

                // Set up log level filter based on console logging state
                _loggerProvider.SetLogLevelFilter(logLevel =>
                {
                    // Check console logging state
                    if (_consoleScene != null && _loggingEnabled)
                        return logLevel >= _minimumLogLevel;

                    return false;
                });
            }

            _logger.LogInformation("Console system initialized successfully");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to initialize console system");
        }
    }

    public void Update(World world, float deltaTime)
    {
        if (!Enabled)
            return;

        try
        {
            var currentKeyboard = Keyboard.GetState();

            // Check for console toggle key (`) when console is closed
            if (!_isConsoleOpen)
            {
                bool isShiftPressed = currentKeyboard.IsKeyDown(Keys.LeftShift) || currentKeyboard.IsKeyDown(Keys.RightShift);
                bool togglePressed = currentKeyboard.IsKeyDown(Keys.OemTilde) &&
                                     _previousKeyboardState.IsKeyUp(Keys.OemTilde) &&
                                     !isShiftPressed;

                if (togglePressed)
                {
                    ToggleConsole();
                }
            }

            _previousKeyboardState = currentKeyboard;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error updating console system");
        }
    }

    /// <summary>
    /// Toggles the console scene on/off.
    /// </summary>
    private void ToggleConsole()
    {
        if (_isConsoleOpen)
        {
            // Close console - pop the scene
            if (_consoleScene != null)
            {
                _sceneManager.PopScene();
                _consoleScene.OnCommandSubmitted -= HandleConsoleCommand;
                _consoleScene.OnRequestCompletions -= HandleConsoleCompletions;
                _consoleScene.OnRequestParameterHints -= HandleConsoleParameterHints;
                _consoleScene.OnRequestDocumentation -= HandleConsoleDocumentation;
                _consoleScene.OnCloseRequested -= OnConsoleClosed;
                _consoleScene = null;

                // Cancel any pending completion requests
                _completionCts?.Cancel();
                _completionCts?.Dispose();
                _completionCts = null;

                // Clear Print() output action
                _globals.OutputAction = null;
            }

            _isConsoleOpen = false;
        }
        else
        {
            // Open console - push the scene
            try
            {
                var consoleLogger = _services.GetRequiredService<ILogger<NewConsoleScene>>();

                _consoleScene = new NewConsoleScene(
                    _graphicsDevice,
                    _services,
                    consoleLogger
                );

                // Wire up event handlers
                _consoleScene.OnCommandSubmitted += HandleConsoleCommand;
                _consoleScene.OnRequestCompletions += HandleConsoleCompletions;
                _consoleScene.OnRequestParameterHints += HandleConsoleParameterHints;
                _consoleScene.OnRequestDocumentation += HandleConsoleDocumentation;
                _consoleScene.OnCloseRequested += OnConsoleClosed;

                // Wire up Print() output to the console
                _globals.OutputAction = (text) => _consoleScene?.AppendOutput(text, Theme.TextPrimary);

                // Set console height to 50% (medium size)
                _consoleScene.SetHeightPercent(0.5f);

                // Push scene
                _sceneManager.PushScene(_consoleScene);
                _isConsoleOpen = true;

                // Welcome message
                _consoleScene.AppendOutput("=== PokeSharp Debug Console ===", Theme.Success);
                _consoleScene.AppendOutput("Type 'help' for available commands", Theme.Info);
                _consoleScene.AppendOutput("Press ` to close", Theme.TextSecondary);
                _consoleScene.AppendOutput("", Theme.TextPrimary);

                // Execute startup script if it exists
                ExecuteStartupScript();

                _logger.LogInformation("Console opened successfully. Press ` to close.");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to open console");
                _isConsoleOpen = false;
                _consoleScene = null;
            }
        }
    }

    /// <summary>
    /// Handles the console scene being closed.
    /// </summary>
    private void OnConsoleClosed()
    {
        if (_isConsoleOpen)
        {
            ToggleConsole();
        }
    }

    /// <summary>
    /// Handles commands submitted from the console.
    /// </summary>
    private void HandleConsoleCommand(string command)
    {
        try
        {
            _ = ExecuteConsoleCommand(command);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error executing command from console: {Command}", command);
            _consoleScene?.AppendOutput($"Error: {ex.Message}", Theme.Error);
        }
    }

    /// <summary>
    /// Handles auto-completion requests from the console.
    /// Uses debouncing to prevent excessive requests during fast typing.
    /// </summary>
    private void HandleConsoleCompletions(string partialCommand)
    {
        // Cancel any pending completion request
        _completionCts?.Cancel();
        _completionCts = new CancellationTokenSource();

        // Fire and forget with proper error handling (not async void)
        _ = GetCompletionsWithDebounceAsync(partialCommand, _completionCts.Token);
    }

    /// <summary>
    /// Gets completions with debouncing to avoid flooding during fast typing.
    /// </summary>
    private async Task GetCompletionsWithDebounceAsync(string partialCommand, CancellationToken ct)
    {
        try
        {
            // Wait for typing to pause (debounce)
            await Task.Delay(CompletionDebounceMs, ct);

            // Get cursor position and completions
            var cursorPosition = _consoleScene?.GetCursorPosition() ?? partialCommand.Length;
            var suggestions = await _completionProvider.GetCompletionsAsync(partialCommand, cursorPosition);

            // Only update UI if this request wasn't cancelled
            if (!ct.IsCancellationRequested)
            {
                _consoleScene?.SetCompletions(suggestions);
            }
        }
        catch (OperationCanceledException)
        {
            // Expected when typing quickly - new request cancelled this one
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting completions for: {PartialCommand}", partialCommand);
        }
    }

    /// <summary>
    /// Handles parameter hint requests from the console.
    /// </summary>
    private void HandleConsoleParameterHints(string text, int cursorPosition)
    {
        try
        {
            // Check if we're inside a method call
            var methodCallInfo = FindMethodCallAtCursor(text, cursorPosition);
            if (methodCallInfo == null)
            {
                _consoleScene?.ClearParameterHints();
                return;
            }

            // Update parameter hint provider with current script state
            _parameterHintProvider.UpdateScriptState(_evaluator.CurrentState);

            // Get parameter hints from provider (pass text up to the opening paren + opening paren)
            var textForHints = text.Substring(0, methodCallInfo.Value.OpenParenIndex + 1);
            var hints = _parameterHintProvider.GetParameterHints(textForHints, textForHints.Length)!;

            if (hints != null && hints.Overloads.Count > 0)
            {
                // Convert to UI types
                var uiHints = new ParamHints
                {
                    MethodName = hints.MethodName,
                    CurrentOverloadIndex = hints.CurrentOverloadIndex,
                    Overloads = hints.Overloads.Select(overload => new MethodSig
                    {
                        MethodName = overload.MethodName,
                    ReturnType = overload.ReturnType,
                    Parameters = overload.Parameters.Select(param => new ParamInfo
                    {
                        Name = param.Name ?? string.Empty,
                        Type = param.Type,
                        IsOptional = param.IsOptional,
                        DefaultValue = param.DefaultValue ?? string.Empty
                    }).ToList()
                    }).ToList()
                };

                _consoleScene?.SetParameterHints(uiHints, methodCallInfo.Value.ParameterIndex);
            }
            else
            {
                _consoleScene?.ClearParameterHints();
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting parameter hints for console");
        }
    }

    /// <summary>
    /// Finds the method call that the cursor is currently inside.
    /// Returns the method name, opening paren position, and current parameter index.
    /// </summary>
    private (string MethodName, int OpenParenIndex, int ParameterIndex)? FindMethodCallAtCursor(string text, int cursorPosition)
    {
        // Find the last unmatched opening parenthesis before the cursor
        int nestLevel = 0;
        int openParenIndex = -1;

        for (int i = cursorPosition - 1; i >= 0; i--)
        {
            char c = text[i];
            if (c == ')')
                nestLevel++;
            else if (c == '(')
            {
                if (nestLevel == 0)
                {
                    openParenIndex = i;
                    break;
                }
                nestLevel--;
            }
        }

        if (openParenIndex == -1)
        {
            return null;
        }

        // Extract method name before the opening paren
        // Handle both direct calls (Method() and member calls (obj.Method())
        int methodStartIndex = openParenIndex - 1;

        // Skip whitespace before paren
        while (methodStartIndex >= 0 && char.IsWhiteSpace(text[methodStartIndex]))
            methodStartIndex--;

        if (methodStartIndex < 0)
            return null;

        // Find the start of the method name (alphanumeric + underscore)
        int methodEndIndex = methodStartIndex;
        while (methodStartIndex >= 0 && (char.IsLetterOrDigit(text[methodStartIndex]) || text[methodStartIndex] == '_'))
            methodStartIndex--;

        methodStartIndex++; // Move back to first char of method name

        if (methodStartIndex > methodEndIndex)
        {
            return null;
        }

        string methodName = text.Substring(methodStartIndex, methodEndIndex - methodStartIndex + 1);

        // Count commas between opening paren and cursor to determine parameter index
        int parameterIndex = 0;
        nestLevel = 0;

        for (int i = openParenIndex + 1; i < cursorPosition && i < text.Length; i++)
        {
            char c = text[i];
            if (c == '(')
                nestLevel++;
            else if (c == ')')
                nestLevel--;
            else if (c == ',' && nestLevel == 0)
                parameterIndex++;
        }

        return (methodName, openParenIndex, parameterIndex);
    }


    /// <summary>
    /// Handles documentation requests from the console.
    /// </summary>
    private void HandleConsoleDocumentation(string completionText)
    {
        var doc = _documentationProvider.GetDocumentation(completionText);
        _consoleScene?.SetDocumentation(doc);
    }

    /// <summary>
    /// Executes a command from the console.
    /// </summary>
    private async Task ExecuteConsoleCommand(string command)
    {
        try
        {
            // Get dependencies for command execution
            var aliasManager = _services.GetRequiredService<AliasMacroManager>();
            var scriptManager = _services.GetRequiredService<ScriptManager>();
            var bookmarkManager = _services.GetRequiredService<BookmarkedCommandsManager>();
            var watchPresetManager = _services.GetRequiredService<WatchPresetManager>();

            // Try to expand alias first
            if (aliasManager.TryExpandAlias(command, out var expandedCommand))
            {
                _logger.LogDebug("Alias expanded: {Original} -> {Expanded}", command, expandedCommand);
                _consoleScene?.AppendOutput($"[alias] {expandedCommand}", Theme.TextSecondary);
                command = expandedCommand;
            }

            // Parse command
            var parts = command.Split(' ', StringSplitOptions.RemoveEmptyEntries);
            if (parts.Length == 0)
                return;

            var cmd = parts[0];
            var args = parts.Skip(1).ToArray();

            // Create console context for commands
            var context = new ConsoleContext(
                _consoleScene!,
                () => ToggleConsole(),
                () => _loggingEnabled,
                (enabled) => _loggingEnabled = enabled,
                () => _minimumLogLevel,
                (level) => _minimumLogLevel = level,
                _commandRegistry,
                aliasManager,
                scriptManager,
                _evaluator,
                _globals,
                bookmarkManager,
                watchPresetManager
            );

            // Try to execute as built-in command first
            var commandExecuted = await _commandRegistry.ExecuteAsync(cmd, args, context);

            if (!commandExecuted)
            {
                // Not a built-in command - try to execute as C# script
                try
                {
                    var result = await _evaluator.EvaluateAsync(command, _globals);

                    // Handle compilation errors
                    if (result.IsCompilationError && result.Errors != null)
                    {
                        _consoleScene?.AppendOutput("Compilation Error:", Theme.Error);
                        foreach (var error in result.Errors)
                        {
                            _consoleScene?.AppendOutput($"  {error.Message}", Theme.Error);
                        }
                        return;
                    }

                    // Handle runtime errors
                    if (result.IsRuntimeError)
                    {
                        _consoleScene?.AppendOutput($"Runtime Error: {result.RuntimeException?.Message ?? "Unknown error"}", Theme.Error);
                        if (result.RuntimeException != null)
                        {
                            _consoleScene?.AppendOutput($"  {result.RuntimeException.GetType().Name}", Theme.TextSecondary);
                        }
                        return;
                    }

                    // Handle successful execution
                    if (result.IsSuccess)
                    {
                        // Display output if available
                        if (!string.IsNullOrEmpty(result.Output) && result.Output != "null")
                        {
                            _consoleScene?.AppendOutput(result.Output, Theme.Success);
                        }
                        // If no output, that's fine (statement executed successfully but returned nothing)
                    }
                    else
                    {
                        // Fallback for unexpected state
                        _consoleScene?.AppendOutput("Command executed but status unclear", Theme.TextSecondary);
                    }
                }
                catch (Exception ex)
                {
                    // This catch should rarely be hit - log it for debugging
                    _logger.LogError(ex, "Unexpected exception executing command: {Command}", command);
                    _consoleScene?.AppendOutput($"Error: {ex.Message}", Theme.Error);
                    _consoleScene?.AppendOutput($"Type: {ex.GetType().Name}", Theme.TextSecondary);
                }
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error executing command in console");
            _consoleScene?.AppendOutput($"Error: {ex.Message}", Theme.Error);
        }
    }

    /// <summary>
    /// Executes the startup script if it exists.
    /// </summary>
    private void ExecuteStartupScript()
    {
        try
        {
            var scriptContent = StartupScriptLoader.LoadStartupScript();
            if (string.IsNullOrWhiteSpace(scriptContent))
            {
                return;
            }

            // Execute the startup script
            _ = ExecuteConsoleCommand(scriptContent);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error executing startup script");
            _consoleScene?.AppendOutput($"Startup script error: {ex.Message}", Theme.Error);
        }
    }
}

