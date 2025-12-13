using Microsoft.Extensions.Logging;
using Microsoft.Xna.Framework.Graphics;
using MonoBallFramework.Game.Engine.Rendering.Assets;
using MonoBallFramework.Game.Engine.Systems.Management;
using MonoBallFramework.Game.Engine.Systems.Pooling;
using MonoBallFramework.Game.GameData.MapLoading.Tiled.Core;
using MonoBallFramework.Game.GameData.MapLoading.Tiled.Processors;
using MonoBallFramework.Game.GameData.PropertyMapping;
using MonoBallFramework.Game.GameData.Services;
using MonoBallFramework.Game.Scripting.Api;

namespace MonoBallFramework.Game.GameData.Factories;

/// <summary>
///     Concrete implementation of IGraphicsServiceFactory using Dependency Injection.
///     Resolves loggers, PropertyMapperRegistry, SystemManager,
///     EntityPoolManager, and MapEntityService from the service provider.
/// </summary>
public class GraphicsServiceFactory : IGraphicsServiceFactory
{
    private readonly ILoggerFactory _loggerFactory;
    private readonly MapEntityService? _mapDefinitionService;
    private readonly EntityPoolManager? _poolManager;
    private readonly PropertyMapperRegistry? _propertyMapperRegistry;
    private readonly SystemManager _systemManager;
    private readonly IGameStateApi? _gameStateApi;

    /// <summary>
    ///     Initializes a new instance of the GraphicsServiceFactory.
    /// </summary>
    /// <param name="loggerFactory">Factory for creating loggers for graphics services.</param>
    /// <param name="systemManager">System manager for accessing SpatialHashSystem.</param>
    /// <param name="poolManager">Entity pool manager for tile pooling.</param>
    /// <param name="propertyMapperRegistry">Optional property mapper registry for map loading.</param>
    /// <param name="mapDefinitionService">Optional map definition service for definition-based map loading.</param>
    /// <param name="gameStateApi">Optional game state API for flag-based NPC visibility.</param>
    public GraphicsServiceFactory(
        ILoggerFactory loggerFactory,
        SystemManager systemManager,
        EntityPoolManager poolManager,
        PropertyMapperRegistry? propertyMapperRegistry = null,
        MapEntityService? mapDefinitionService = null,
        IGameStateApi? gameStateApi = null
    )
    {
        _loggerFactory = loggerFactory ?? throw new ArgumentNullException(nameof(loggerFactory));
        _systemManager = systemManager ?? throw new ArgumentNullException(nameof(systemManager));
        _poolManager = poolManager ?? throw new ArgumentNullException(nameof(poolManager));
        _propertyMapperRegistry = propertyMapperRegistry;
        _mapDefinitionService = mapDefinitionService;
        _gameStateApi = gameStateApi;
    }

    /// <inheritdoc />
    public AssetManager CreateAssetManager(
        GraphicsDevice graphicsDevice,
        string assetRoot = "Assets"
    )
    {
        if (graphicsDevice == null)
        {
            throw new ArgumentNullException(nameof(graphicsDevice));
        }

        ILogger<AssetManager> logger = _loggerFactory.CreateLogger<AssetManager>();
        return new AssetManager(graphicsDevice, assetRoot, logger);
    }

    /// <inheritdoc />
    public MapLoader CreateMapLoader(AssetManager assetManager)
    {
        if (assetManager == null)
        {
            throw new ArgumentNullException(nameof(assetManager));
        }

        // Create processors with proper loggers
        var layerProcessor = new LayerProcessor(
            _propertyMapperRegistry,
            _loggerFactory.CreateLogger<LayerProcessor>()
        );

        var animatedTileProcessor = new AnimatedTileProcessor(
            _loggerFactory.CreateLogger<AnimatedTileProcessor>()
        );

        var borderProcessor = new BorderProcessor(_loggerFactory.CreateLogger<BorderProcessor>());

        return new MapLoader(
            assetManager,
            _systemManager,
            layerProcessor,
            animatedTileProcessor,
            borderProcessor,
            _propertyMapperRegistry,
            _mapDefinitionService,
            _gameStateApi,
            null, // MapLifecycleManager - not yet wired through DI
            _loggerFactory.CreateLogger<MapLoader>()
        );
    }
}
