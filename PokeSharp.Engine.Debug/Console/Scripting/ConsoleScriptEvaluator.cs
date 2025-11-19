using System.Reflection;
using System.Text;
using Arch.Core;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Scripting;
using Microsoft.CodeAnalysis.Scripting;
using Microsoft.Extensions.Logging;
using Microsoft.Xna.Framework;
using PokeSharp.Game.Components.Movement;
using PokeSharp.Game.Scripting.Api;
using PokeSharp.Game.Systems.Services;

namespace PokeSharp.Engine.Debug.Console.Scripting;

/// <summary>
///     Evaluates C# code snippets using Roslyn scripting for the console.
///     Maintains script state between evaluations for persistent variables.
/// </summary>
public class ConsoleScriptEvaluator
{
    private readonly ILogger _logger;
    private readonly ScriptOptions _scriptOptions;
    private ScriptState<object>? _scriptState;

    /// <summary>
    /// Gets the current script state (for auto-completion tracking).
    /// </summary>
    public ScriptState<object>? CurrentState => _scriptState;

    /// <summary>
    ///     Initializes a new instance of the console script evaluator.
    /// </summary>
    public ConsoleScriptEvaluator(ILogger logger)
    {
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));

        // Configure script options with all necessary references and imports
        _scriptOptions = ScriptOptions.Default
            .AddReferences(GetDefaultReferences())
            .AddImports(GetDefaultImports());

        _logger.LogInformation("Console script evaluator initialized");
    }

    /// <summary>
    ///     Evaluates a C# code snippet and returns the result.
    /// </summary>
    /// <param name="code">The C# code to evaluate.</param>
    /// <param name="globals">Global variables available to the script.</param>
    /// <returns>The result of the evaluation.</returns>
    public async Task<EvaluationResult> EvaluateAsync(string code, ConsoleGlobals globals)
    {
        if (string.IsNullOrWhiteSpace(code))
            return EvaluationResult.Empty();

        try
        {
            _logger.LogDebug("Evaluating code: {Code}", code);

            if (_scriptState == null)
            {
                // First execution - create new script state
                _scriptState = await CSharpScript.RunAsync(code, _scriptOptions, globals);
            }
            else
            {
                // Continue from previous state (preserves variables)
                _scriptState = await _scriptState.ContinueWithAsync(code);
            }

            var result = _scriptState.ReturnValue;
            return EvaluationResult.Success(FormatResult(result));
        }
        catch (CompilationErrorException ex)
        {
            _logger.LogWarning(ex, "Compilation error in console script");
            var errors = ErrorFormatter.FormatErrors(ex.Diagnostics, code);
            return EvaluationResult.CompilationError(errors, code);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Runtime error in console script");
            return EvaluationResult.RuntimeError(ex);
        }
    }

    /// <summary>
    ///     Resets the script state, clearing all variables.
    /// </summary>
    public void Reset()
    {
        _scriptState = null;
        _logger.LogDebug("Console script state reset");
    }

    /// <summary>
    ///     Formats the result of a script evaluation.
    /// </summary>
    private static string FormatResult(object? result)
    {
        if (result == null)
            return "null";

        // Handle common MonoGame types
        if (result is Vector2 v2)
            return $"Vector2({v2.X:F2}, {v2.Y:F2})";

        if (result is Point p)
            return $"Point({p.X}, {p.Y})";

        if (result is Rectangle rect)
            return $"Rectangle(X:{rect.X}, Y:{rect.Y}, W:{rect.Width}, H:{rect.Height})";

        if (result is Color color)
            return $"Color(R:{color.R}, G:{color.G}, B:{color.B}, A:{color.A})";

        // Handle Entity
        if (result is Entity entity)
            return $"Entity(Id: {entity.Id})";

        // Handle collections
        if (result is System.Collections.IEnumerable enumerable and not string)
        {
            var items = enumerable.Cast<object>().Take(10).ToList();
            var moreItems = enumerable.Cast<object>().Count() > 10;
            var itemsStr = string.Join(", ", items.Select(FormatResult));
            return moreItems ? $"[{itemsStr}, ...]" : $"[{itemsStr}]";
        }

        return result.ToString() ?? "null";
    }


    /// <summary>
    ///     Gets the default assembly references for console scripts.
    /// </summary>
    public static IEnumerable<Assembly> GetDefaultReferences()
    {
        return new[]
        {
            typeof(object).Assembly,                    // System.Private.CoreLib
            typeof(System.Console).Assembly,            // System.Console
            typeof(Enumerable).Assembly,                // System.Linq
            typeof(List<>).Assembly,                    // System.Collections
            typeof(World).Assembly,                     // Arch.Core
            typeof(Entity).Assembly,                    // Arch.Core
            typeof(Point).Assembly,                     // MonoGame.Framework
            typeof(Vector2).Assembly,                   // MonoGame.Framework
            typeof(Direction).Assembly,                 // PokeSharp.Game.Components
            typeof(IScriptingApiProvider).Assembly,     // PokeSharp.Game.Scripting
            typeof(ILogger).Assembly,                   // Microsoft.Extensions.Logging.Abstractions
        };
    }

    /// <summary>
    ///     Gets the default namespace imports for console scripts.
    /// </summary>
    public static IEnumerable<string> GetDefaultImports()
    {
        return new[]
        {
            "System",
            "System.Linq",
            "System.Collections.Generic",
            "Arch.Core",
            "Microsoft.Xna.Framework",
            "Microsoft.Extensions.Logging",
            "PokeSharp.Game.Components.Movement",
            "PokeSharp.Game.Components.Player",
            "PokeSharp.Game.Components.Rendering",
            "PokeSharp.Game.Scripting.Api",
        };
    }
}

