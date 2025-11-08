using Microsoft.Extensions.Logging;

namespace PokeSharp.Game.Services;

/// <summary>
///     Provides unified access to logging infrastructure.
///     Facade simplifies DI by grouping logger and logger factory into single provider.
/// </summary>
/// <remarks>
///     This pattern follows Phase 3 (IScriptingApiProvider) and Phase 4B (IGameServicesProvider)
///     to maintain architectural consistency across all facade implementations.
/// </remarks>
public interface ILoggingProvider
{
    /// <summary>
    ///     Gets the logger factory for creating child loggers.
    /// </summary>
    /// <remarks>
    ///     Used to create loggers for child components and initializers.
    ///     Example: loggerFactory.CreateLogger&lt;GameInitializer&gt;()
    /// </remarks>
    ILoggerFactory LoggerFactory { get; }

    /// <summary>
    ///     Creates a logger for the specified type.
    /// </summary>
    /// <typeparam name="T">Type to create logger for</typeparam>
    /// <returns>Typed logger instance</returns>
    ILogger<T> CreateLogger<T>();
}
