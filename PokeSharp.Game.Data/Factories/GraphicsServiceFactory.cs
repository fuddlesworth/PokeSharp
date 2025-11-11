using Microsoft.Extensions.Logging;
using Microsoft.Xna.Framework.Graphics;
using PokeSharp.Engine.Systems.Factories;
using PokeSharp.Game.Data.PropertyMapping;
using PokeSharp.Engine.Rendering.Assets;
using PokeSharp.Game.Data.MapLoading.Tiled;
using PokeSharp.Engine.Systems.Management;

namespace PokeSharp.Game.Data.Factories;

/// <summary>
///     Concrete implementation of IGraphicsServiceFactory using Dependency Injection.
///     Resolves loggers, PropertyMapperRegistry, and SystemManager from the service provider.
/// </summary>
public class GraphicsServiceFactory : IGraphicsServiceFactory
{
    private readonly ILoggerFactory _loggerFactory;
    private readonly PropertyMapperRegistry? _propertyMapperRegistry;
    private readonly SystemManager _systemManager;

    /// <summary>
    ///     Initializes a new instance of the GraphicsServiceFactory.
    /// </summary>
    /// <param name="loggerFactory">Factory for creating loggers for graphics services.</param>
    /// <param name="systemManager">System manager for accessing SpatialHashSystem.</param>
    /// <param name="propertyMapperRegistry">Optional property mapper registry for map loading.</param>
    public GraphicsServiceFactory(
        ILoggerFactory loggerFactory,
        SystemManager systemManager,
        PropertyMapperRegistry? propertyMapperRegistry = null)
    {
        _loggerFactory = loggerFactory ?? throw new ArgumentNullException(nameof(loggerFactory));
        _systemManager = systemManager ?? throw new ArgumentNullException(nameof(systemManager));
        _propertyMapperRegistry = propertyMapperRegistry;
    }

    /// <inheritdoc />
    public AssetManager CreateAssetManager(
        GraphicsDevice graphicsDevice,
        string assetRoot = "Assets"
    )
    {
        if (graphicsDevice == null)
            throw new ArgumentNullException(nameof(graphicsDevice));

        var logger = _loggerFactory.CreateLogger<AssetManager>();
        return new AssetManager(graphicsDevice, assetRoot, logger);
    }

    /// <inheritdoc />
    public MapLoader CreateMapLoader(
        AssetManager assetManager,
        IEntityFactoryService? entityFactory = null
    )
    {
        if (assetManager == null)
            throw new ArgumentNullException(nameof(assetManager));

        var logger = _loggerFactory.CreateLogger<MapLoader>();
        return new MapLoader(assetManager, _systemManager, _propertyMapperRegistry, entityFactory, logger);
    }
}
