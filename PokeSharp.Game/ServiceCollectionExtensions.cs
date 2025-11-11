using Arch.Core;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using PokeSharp.Engine.Core.Events;
using PokeSharp.Engine.Systems.Factories;
using PokeSharp.Game.Data.PropertyMapping;
using PokeSharp.Engine.Systems.Pooling;
using PokeSharp.Game.Scripting.Services;
using PokeSharp.Game.Scripting.Api;
using PokeSharp.Game.Systems.Services;
using PokeSharp.Game.Systems;
using PokeSharp.Engine.Systems.Management;
using PokeSharp.Engine.Core.Templates;
using PokeSharp.Engine.Core.Types;
using PokeSharp.Game.Diagnostics;
using PokeSharp.Game.Initialization;
using PokeSharp.Game.Input;
using PokeSharp.Game.Services;
using PokeSharp.Game.Templates;
using PokeSharp.Game.Data.Factories;

namespace PokeSharp.Game;

/// <summary>
///     Extension methods for configuring game services in the DI container.
/// </summary>
public static class ServiceCollectionExtensions
{
    /// <summary>
    ///     Adds all game services to the service collection.
    /// </summary>
    public static IServiceCollection AddGameServices(this IServiceCollection services)
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

        // Note: ComponentPoolManager registration removed - it was never used.
        // ECS systems work directly with component references via queries.
        // If temporary component copies are needed in the future, add it back.

        // Entity Pool Manager (Phase 4A) - For entity recycling and pooling
        services.AddSingleton(sp =>
        {
            var world = sp.GetRequiredService<World>();
            return new EntityPoolManager(world);
        });

        // Entity Factory & Templates
        services.AddSingleton(sp =>
        {
            var cache = new TemplateCache();
            TemplateRegistry.RegisterAllTemplates(cache);
            return cache;
        });
        services.AddSingleton<IEntityFactoryService, EntityFactoryService>();

        // Type Registry for Behaviors
        services.AddSingleton(sp =>
        {
            var logger = sp.GetRequiredService<ILogger<TypeRegistry<BehaviorDefinition>>>();
            return new TypeRegistry<BehaviorDefinition>("Assets/Types/Behaviors", logger);
        });

        // Event Bus
        services.AddSingleton<IEventBus>(sp =>
        {
            var logger = sp.GetRequiredService<ILogger<EventBus>>();
            return new EventBus(logger);
        });

        // Property Mappers (for extensible Tiled property → ECS component mapping)
        services.AddPropertyMappers();

        // Abstract Factory Pattern: Graphics services that depend on GraphicsDevice
        // The factory allows deferred creation of AssetManager and MapLoader until
        // GraphicsDevice is available at runtime (in PokeSharpGame.Initialize)
        services.AddSingleton<IGraphicsServiceFactory, GraphicsServiceFactory>();

        // Game Time Service
        services.AddSingleton<IGameTimeService, GameTimeService>();

        // Collision Service - provides on-demand collision checking (not a system)
        services.AddSingleton<ICollisionService>(sp =>
        {
            var systemManager = sp.GetRequiredService<SystemManager>();
            // SpatialHashSystem is registered as a system and implements ISpatialQuery
            var spatialQuery = systemManager.GetSystem<SpatialHashSystem>();
            var logger = sp.GetService<ILogger<CollisionService>>();
            return new CollisionService(spatialQuery, logger);
        });

        // Scripting API Services
        services.AddSingleton<PlayerApiService>();
        services.AddSingleton<NpcApiService>();
        services.AddSingleton<MapApiService>(sp =>
        {
            var world = sp.GetRequiredService<World>();
            var logger = sp.GetRequiredService<ILogger<MapApiService>>();
            // SpatialHashSystem is initialized later in GameInitializer
            // It will be set via SetSpatialQuery method after initialization
            return new MapApiService(world, logger);
        });
        services.AddSingleton<GameStateApiService>();
        services.AddSingleton<DialogueApiService>();
        services.AddSingleton<EffectApiService>();
        // WorldApi removed - scripts now use domain APIs directly via ScriptContext

        // Scripting API Provider
        services.AddSingleton<IScriptingApiProvider, ScriptingApiProvider>();

        // Scripting Service
        services.AddSingleton(sp =>
        {
            var logger = sp.GetRequiredService<ILogger<ScriptService>>();
            var apis = sp.GetRequiredService<IScriptingApiProvider>();
            return new ScriptService("Assets/Scripts", logger, apis);
        });

        // Game Services Provider (Phase 4B facade)
        services.AddSingleton<IGameServicesProvider, GameServicesProvider>();

        // Logging Provider (Phase 5 facade)
        services.AddSingleton<ILoggingProvider, LoggingProvider>();

        // Initialization Provider (Phase 7 facade)
        services.AddSingleton<IInitializationProvider, InitializationProvider>();

        // Game Initializers and Helpers
        services.AddSingleton<PerformanceMonitor>();
        services.AddSingleton<InputManager>();
        services.AddSingleton<PlayerFactory>();

        // Note: GameInitializer, MapInitializer, NPCBehaviorInitializer, and SpatialHashSystem
        // are created after GraphicsDevice is available in PokeSharpGame.Initialize()
        // AssetManager and MapLoader are now created via IGraphicsServiceFactory

        return services;
    }
}
