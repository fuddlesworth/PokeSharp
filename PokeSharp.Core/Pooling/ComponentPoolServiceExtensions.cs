using Microsoft.Extensions.Logging;
using PokeSharp.Core.DependencyInjection;

namespace PokeSharp.Core.Pooling;

/// <summary>
///     Extension methods for registering component pooling services.
/// </summary>
public static class ComponentPoolServiceExtensions
{
    /// <summary>
    ///     Register component pooling services in the DI container.
    /// </summary>
    /// <param name="container">Service container</param>
    /// <param name="enableStatistics">Whether to enable statistics tracking</param>
    /// <returns>Service container for chaining</returns>
    public static ServiceContainer AddComponentPooling(
        this ServiceContainer container,
        bool enableStatistics = true
    )
    {
        // Register ComponentPoolManager as singleton
        container.RegisterSingleton<ComponentPoolManager>(serviceProvider =>
        {
            serviceProvider.TryResolve<ILogger<ComponentPoolManager>>(out var logger);
            return new ComponentPoolManager(logger, enableStatistics);
        });

        return container;
    }
}
