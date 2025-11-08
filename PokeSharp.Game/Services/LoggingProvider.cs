using Microsoft.Extensions.Logging;

namespace PokeSharp.Game.Services;

/// <summary>
///     Default implementation of ILoggingProvider that wraps logger factory.
///     Facade simplifies DI by reducing logging dependencies from 2 to 1.
/// </summary>
/// <remarks>
///     Uses primary constructor pattern (C# 12) for concise initialization.
///     Follows exact pattern from GameServicesProvider and ScriptingApiProvider.
/// </remarks>
public class LoggingProvider(ILoggerFactory loggerFactory) : ILoggingProvider
{
    private readonly ILoggerFactory _loggerFactory =
        loggerFactory ?? throw new ArgumentNullException(nameof(loggerFactory));

    /// <inheritdoc />
    public ILoggerFactory LoggerFactory => _loggerFactory;

    /// <inheritdoc />
    public ILogger<T> CreateLogger<T>() => _loggerFactory.CreateLogger<T>();
}
