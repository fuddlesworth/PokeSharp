using Arch.Core;
using Microsoft.Extensions.Logging;
using Microsoft.Xna.Framework;
using PokeSharp.Core.Components.Maps;
using PokeSharp.Core.Logging;
using PokeSharp.Core.Systems;
using PokeSharp.Rendering.Loaders;
using PokeSharp.Rendering.Systems;

namespace PokeSharp.Game.Initialization;

/// <summary>
///     Handles map loading and initialization.
/// </summary>
public class MapInitializer(
    ILogger<MapInitializer> logger,
    World world,
    MapLoader mapLoader,
    SpatialHashSystem spatialHashSystem,
    ZOrderRenderSystem renderSystem
)
{
    /// <summary>
    ///     Loads the test map using the entity-based tile system.
    ///     Creates individual entities for each tile with appropriate components.
    /// </summary>
    /// <param name="mapPath">Path to the map file.</param>
    /// <returns>The MapInfo entity containing map metadata.</returns>
    public Entity? LoadMap(string mapPath)
    {
        try
        {
            logger.LogWorkflowStatus("Loading map", ("path", mapPath));

            // Load map as tile entities (ECS-based approach)
            var mapInfoEntity = mapLoader.LoadMapEntities(world, mapPath);
            logger.LogWorkflowStatus("Map entities created", ("entity", mapInfoEntity.Id));

            // Invalidate spatial hash to reindex static tiles
            spatialHashSystem.InvalidateStaticTiles();
            logger.LogWorkflowStatus("Spatial hash invalidated", ("cells", "static"));

            // Preload all textures used by the map to avoid loading spikes during gameplay
            renderSystem.PreloadMapAssets(world);
            logger.LogWorkflowStatus("Render assets preloaded");

            // Set camera bounds from MapInfo
            var mapInfoQuery = QueryCache.Get<MapInfo>();
            world.Query(
                in mapInfoQuery,
                (ref MapInfo mapInfo) =>
                {
                    logger.LogWorkflowStatus(
                        "Camera bounds updated",
                        ("widthPx", mapInfo.PixelWidth),
                        ("heightPx", mapInfo.PixelHeight)
                    );
                }
            );

            logger.LogWorkflowStatus("Map load complete", ("path", mapPath));
            return mapInfoEntity;
        }
        catch (Exception ex)
        {
            logger.LogExceptionWithContext(
                ex,
                "Failed to load map: {MapPath}. Game will continue without map",
                mapPath
            );
            return null;
        }
    }

}
