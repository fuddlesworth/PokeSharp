using Arch.Core;
using Arch.Core.Extensions;
using Microsoft.Extensions.Logging;
using Microsoft.Xna.Framework;
using PokeSharp.Game.Components.Maps;
using PokeSharp.Game.Components.Movement;
using PokeSharp.Engine.Common.Logging;
using PokeSharp.Game.Systems;
using PokeSharp.Game.Data.MapLoading.Tiled;
using PokeSharp.Engine.Rendering.Systems;
using EcsQueries = PokeSharp.Engine.Systems.Queries.Queries;

namespace PokeSharp.Game.Initialization;

/// <summary>
///     Handles map loading and initialization.
/// </summary>
public class MapInitializer(
    ILogger<MapInitializer> logger,
    World world,
    MapLoader mapLoader,
    SpatialHashSystem spatialHashSystem,
    ElevationRenderSystem renderSystem,
    MapLifecycleManager mapLifecycleManager
)
{
    /// <summary>
    ///     Loads a map from EF Core definition (NEW: Definition-based loading).
    ///     Creates individual entities for each tile with appropriate components.
    /// </summary>
    /// <param name="mapId">The map identifier (e.g., "test-map", "littleroot_town").</param>
    /// <returns>The MapInfo entity containing map metadata.</returns>
    public Entity? LoadMap(string mapId)
    {
        try
        {
            logger.LogWorkflowStatus("Loading map from definition", ("mapId", mapId));

            // Load map from EF Core definition (NEW: Definition-based)
            var mapInfoEntity = mapLoader.LoadMap(world, mapId);
            logger.LogWorkflowStatus("Map entities created", ("entity", mapInfoEntity.Id));

            // Get MapInfo to extract map ID and name for lifecycle tracking
            var mapInfo = mapInfoEntity.Get<MapInfo>();
            var textureIds = mapLoader.GetLoadedTextureIds(mapInfo.MapId);

            // Register map with lifecycle manager BEFORE transitioning
            mapLifecycleManager.RegisterMap(mapInfo.MapId, mapInfo.MapName, textureIds);

            // Transition to new map (cleans up old maps)
            mapLifecycleManager.TransitionToMap(mapInfo.MapId);
            logger.LogWorkflowStatus("Map lifecycle transition complete", ("mapId", mapInfo.MapId));

            // Invalidate spatial hash to reindex static tiles
            spatialHashSystem.InvalidateStaticTiles();
            logger.LogWorkflowStatus("Spatial hash invalidated", ("cells", "static"));

            // Preload all textures used by the map to avoid loading spikes during gameplay
            renderSystem.PreloadMapAssets(world);
            logger.LogWorkflowStatus("Render assets preloaded");

            // Set camera bounds and tile size from MapInfo
            world.Query(
                in EcsQueries.MapInfo,
                (ref MapInfo mapInfo) =>
                {
                    renderSystem.SetTileSize(mapInfo.TileSize);
                    logger.LogWorkflowStatus(
                        "Camera bounds updated",
                        ("widthPx", mapInfo.PixelWidth),
                        ("heightPx", mapInfo.PixelHeight)
                    );
                }
            );

            logger.LogWorkflowStatus("Map load complete", ("mapId", mapId));
            return mapInfoEntity;
        }
        catch (Exception ex)
        {
            logger.LogExceptionWithContext(
                ex,
                "Failed to load map: {MapId}. Game will continue without map",
                mapId
            );
            return null;
        }
    }

    /// <summary>
    ///     Loads a map from file path (LEGACY: Backward compatibility).
    ///     Use LoadMap(mapId) for definition-based loading instead.
    /// </summary>
    /// <param name="mapPath">Path to the map file.</param>
    /// <returns>The MapInfo entity containing map metadata.</returns>
    public Entity? LoadMapFromFile(string mapPath)
    {
        try
        {
            logger.LogWorkflowStatus("Loading map from file (LEGACY)", ("path", mapPath));

            // Load map as tile entities (file-based approach for backward compatibility)
            var mapInfoEntity = mapLoader.LoadMapEntities(world, mapPath);
            logger.LogWorkflowStatus("Map entities created", ("entity", mapInfoEntity.Id));

            // Get MapInfo to extract map ID and name for lifecycle tracking
            var mapInfo = mapInfoEntity.Get<MapInfo>();
            var textureIds = mapLoader.GetLoadedTextureIds(mapInfo.MapId);

            // Register map with lifecycle manager BEFORE transitioning
            mapLifecycleManager.RegisterMap(mapInfo.MapId, mapInfo.MapName, textureIds);

            // Transition to new map (cleans up old maps)
            mapLifecycleManager.TransitionToMap(mapInfo.MapId);
            logger.LogWorkflowStatus("Map lifecycle transition complete", ("mapId", mapInfo.MapId));

            // Invalidate spatial hash to reindex static tiles
            spatialHashSystem.InvalidateStaticTiles();
            logger.LogWorkflowStatus("Spatial hash invalidated", ("cells", "static"));

            // Preload all textures used by the map to avoid loading spikes during gameplay
            renderSystem.PreloadMapAssets(world);
            logger.LogWorkflowStatus("Render assets preloaded");

            // Set camera bounds and tile size from MapInfo
            world.Query(
                in EcsQueries.MapInfo,
                (ref MapInfo mapInfo) =>
                {
                    renderSystem.SetTileSize(mapInfo.TileSize);
                    logger.LogWorkflowStatus(
                        "Camera bounds updated",
                        ("widthPx", mapInfo.PixelWidth),
                        ("heightPx", mapInfo.PixelHeight)
                    );
                }
            );

            logger.LogWorkflowStatus("Map load complete (LEGACY)", ("path", mapPath));
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
