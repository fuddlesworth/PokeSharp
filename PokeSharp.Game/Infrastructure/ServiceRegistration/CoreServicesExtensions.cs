using Arch.Core;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using PokeSharp.Engine.Core.Events;
using PokeSharp.Engine.Core.Events.ECS;
using PokeSharp.Engine.Core.Events.Modding;
using PokeSharp.Engine.Core.Modding;
using PokeSharp.Engine.Systems.Management;
using PokeSharp.Engine.Systems.Pooling;
using PokeSharp.Game.Data;
using PokeSharp.Game.Data.Loading;
using PokeSharp.Game.Data.Services;
using PokeSharp.Game.Infrastructure.Services;

namespace PokeSharp.Game.Infrastructure.ServiceRegistration;

/// <summary>
///     Extension methods for registering core game services (ECS, data, modding).
/// </summary>
public static class CoreServicesExtensions
{
    /// <summary>
    ///     Registers core ECS services (World, SystemManager, EntityPoolManager).
    /// </summary>
    public static IServiceCollection AddCoreEcsServices(this IServiceCollection services)
    {
        // Core ECS
        services.AddSingleton(sp =>
        {
            var world = World.Create();
            return world;
        });

        // System Manager - Sequential execution (optimal for <500 entities per system)
        // Parallel overhead (1-2ms) exceeds work time (0.09ms) for Pokemon-style games
        services.AddSingleton<SystemManager>(sp =>
        {
            var logger = sp.GetService<ILogger<SystemManager>>();
            return new SystemManager(logger);
        });

        // Entity Pool Manager - For entity recycling and pooling
        services.AddSingleton(sp =>
        {
            var world = sp.GetRequiredService<World>();
            return new EntityPoolManager(world);
        });

        return services;
    }

    /// <summary>
    ///     Registers event bus services (ECS events and Mod events).
    /// </summary>
    public static IServiceCollection AddEventServices(this IServiceCollection services)
    {
        // High-performance ECS event bus for internal engine events
        services.AddSingleton<IEcsEventBus>(sp =>
        {
            var logger = sp.GetService<ILogger<ArchEcsEventBus>>();
            return new ArchEcsEventBus(logger);
        });

        // Mod-facing event bus with error isolation
        services.AddSingleton<IModEventBus>(sp =>
        {
            var logger = sp.GetService<ILogger<ModEventBus>>();
            return new ModEventBus(logger);
        });

        // Bridge that forwards ECS events to mod-safe events
        // This enables mods to react to internal engine events without accessing raw Entity references
        services.AddSingleton<EcsToModEventBridge>(sp =>
        {
            var ecsEventBus = sp.GetRequiredService<IEcsEventBus>();
            var modEventBus = sp.GetRequiredService<IModEventBus>();
            var logger = sp.GetService<ILogger<EcsToModEventBridge>>();
            return new EcsToModEventBridge(ecsEventBus, modEventBus, logger);
        });

        return services;
    }

    /// <summary>
    ///     Registers data services (database, data loaders, definition services).
    /// </summary>
    public static IServiceCollection AddDataServices(this IServiceCollection services)
    {
        // EF Core In-Memory Database for game data definitions
        // Register as Singleton since we're using In-Memory database for read-only data
        services.AddDbContext<GameDataContext>(
            options =>
            {
                options.UseInMemoryDatabase("GameData");

#if DEBUG
                options.EnableSensitiveDataLogging();
                options.EnableDetailedErrors();
#endif
            },
            ServiceLifetime.Singleton // In-Memory DB can be singleton
        );

        // Data loading and services
        services.AddSingleton<GameDataLoader>();
        services.AddSingleton<NpcDefinitionService>();
        services.AddSingleton<MapDefinitionService>();

        // NPC Sprite Loader - for loading sprites extracted from Pokemon Emerald
        services.AddSingleton<SpriteLoader>();

        return services;
    }

    /// <summary>
    ///     Registers modding services.
    /// </summary>
    public static IServiceCollection AddModdingServices(
        this IServiceCollection services,
        string modsDirectory = "Mods"
    )
    {
        ModdingExtensions.AddModdingServices(services, modsDirectory);
        return services;
    }
}
