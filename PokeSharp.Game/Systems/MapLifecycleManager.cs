using Arch.Core;
using Arch.Core.Extensions;
using Microsoft.Extensions.Logging;
using PokeSharp.Engine.Rendering.Assets;
using PokeSharp.Game.Components.Maps;
using PokeSharp.Game.Components.Tiles;

namespace PokeSharp.Game.Systems;

/// <summary>
/// Manages map lifecycle: loading, unloading, and memory cleanup.
/// Ensures only active maps remain in memory to prevent entity/texture accumulation.
/// </summary>
public class MapLifecycleManager
{
    private readonly World _world;
    private readonly IAssetProvider _assetProvider;
    private readonly ILogger<MapLifecycleManager>? _logger;
    private readonly Dictionary<int, MapMetadata> _loadedMaps = new();
    private int _currentMapId = -1;
    private int _previousMapId = -1;

    public MapLifecycleManager(
        World world,
        IAssetProvider assetProvider,
        ILogger<MapLifecycleManager>? logger = null
    )
    {
        _world = world ?? throw new ArgumentNullException(nameof(world));
        _assetProvider = assetProvider ?? throw new ArgumentNullException(nameof(assetProvider));
        _logger = logger;
    }

    /// <summary>
    /// Gets the current active map ID
    /// </summary>
    public int CurrentMapId => _currentMapId;

    /// <summary>
    /// Registers a newly loaded map
    /// </summary>
    public void RegisterMap(int mapId, string mapName, HashSet<string> textureIds)
    {
        _loadedMaps[mapId] = new MapMetadata(mapName, textureIds);
        _logger?.LogInformation(
            "Registered map: {MapName} (ID: {MapId}) with {TextureCount} textures",
            mapName,
            mapId,
            textureIds.Count
        );
    }

    /// <summary>
    /// Transitions to a new map, cleaning up old map entities and textures
    /// </summary>
    public void TransitionToMap(int newMapId)
    {
        if (newMapId == _currentMapId)
        {
            _logger?.LogDebug("Already on map {MapId}, skipping transition", newMapId);
            return;
        }

        var oldMapId = _currentMapId;
        _previousMapId = _currentMapId;
        _currentMapId = newMapId;

        _logger?.LogInformation(
            "Transitioning from map {OldMapId} to {NewMapId}",
            oldMapId,
            newMapId
        );

        // Clean up old maps (keep current + previous for smooth transitions)
        var mapsToUnload = _loadedMaps
            .Keys.Where(id => id != _currentMapId && id != _previousMapId)
            .ToList();

        foreach (var mapId in mapsToUnload)
        {
            UnloadMap(mapId);
        }
    }

    /// <summary>
    /// Unloads a specific map: destroys entities and unloads textures
    /// </summary>
    public void UnloadMap(int mapId)
    {
        if (!_loadedMaps.TryGetValue(mapId, out var metadata))
        {
            _logger?.LogWarning("Attempted to unload unknown map: {MapId}", mapId);
            return;
        }

        _logger?.LogInformation("Unloading map: {MapName} (ID: {MapId})", metadata.Name, mapId);

        // 1. Destroy all tile entities for this map
        var tilesDestroyed = DestroyMapEntities(mapId);

        // 2. Unload textures (if AssetManager supports it)
        var texturesUnloaded = UnloadMapTextures(metadata.TextureIds);

        _loadedMaps.Remove(mapId);

        _logger?.LogInformation(
            "Map {MapName} unloaded: {TilesDestroyed} entities destroyed, {TexturesUnloaded} textures freed",
            metadata.Name,
            tilesDestroyed,
            texturesUnloaded
        );
    }

    /// <summary>
    /// Destroys all entities belonging to a specific map
    /// </summary>
    private int DestroyMapEntities(int mapId)
    {
        // CRITICAL FIX: Collect entities first, then destroy (can't modify during query)
        var entitiesToDestroy = new List<Entity>();
        var query = new QueryDescription().WithAll<TilePosition>();

        _world.Query(
            in query,
            (Entity entity, ref TilePosition pos) =>
            {
                if (pos.MapId == mapId)
                {
                    entitiesToDestroy.Add(entity);
                }
            }
        );

        // Now destroy entities outside the query
        foreach (var entity in entitiesToDestroy)
        {
            _world.Destroy(entity);
        }

        return entitiesToDestroy.Count;
    }

    /// <summary>
    /// Unloads textures for a map (if AssetManager supports UnregisterTexture)
    /// </summary>
    private int UnloadMapTextures(HashSet<string> textureIds)
    {
        if (_assetProvider is not AssetManager assetManager)
            return 0;

        var unloaded = 0;
        foreach (var textureId in textureIds)
        {
            // Check if texture is used by other loaded maps
            var isShared = _loadedMaps.Values.Any(m => m.TextureIds.Contains(textureId));

            if (!isShared)
            {
                if (assetManager.UnregisterTexture(textureId))
                {
                    unloaded++;
                }
            }
        }

        return unloaded;
    }

    /// <summary>
    /// Forces cleanup of all inactive maps (emergency memory cleanup)
    /// </summary>
    public void ForceCleanup()
    {
        _logger?.LogWarning("Force cleanup triggered - unloading all inactive maps");

        var mapsToUnload = _loadedMaps.Keys.Where(id => id != _currentMapId).ToList();

        foreach (var mapId in mapsToUnload)
        {
            UnloadMap(mapId);
        }

        GC.Collect();
        GC.WaitForPendingFinalizers();
        GC.Collect();
    }

    private record MapMetadata(string Name, HashSet<string> TextureIds);
}
