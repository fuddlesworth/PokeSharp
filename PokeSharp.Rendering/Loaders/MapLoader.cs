using Arch.Core;
using PokeSharp.Core.Components;
using PokeSharp.Rendering.Assets;

namespace PokeSharp.Rendering.Loaders;

/// <summary>
/// Loads Tiled maps and converts them to ECS components.
/// </summary>
public class MapLoader
{
    private readonly AssetManager _assetManager;

    /// <summary>
    /// Initializes a new instance of the MapLoader class.
    /// </summary>
    /// <param name="assetManager">Asset manager for texture loading.</param>
    public MapLoader(AssetManager assetManager)
    {
        _assetManager = assetManager ?? throw new ArgumentNullException(nameof(assetManager));
    }

    /// <summary>
    /// Loads a complete map entity with all components from a Tiled map file.
    /// Parses the file once and extracts all data (TileMap, TileProperties).
    /// Uses bounds checking and tile properties for collision (no TileCollider needed).
    /// </summary>
    /// <param name="world">The ECS world to create the entity in.</param>
    /// <param name="mapPath">Path to the Tiled JSON map file.</param>
    /// <returns>The created map entity with all components attached.</returns>
    public Entity LoadMapEntity(World world, string mapPath)
    {
        // Parse Tiled map once
        var tmxDoc = TiledMapLoader.Load(mapPath);

        // Extract components
        var tileMap = ExtractTileMap(tmxDoc, mapPath);
        var tileProperties = ExtractTileProperties(tmxDoc);

        // Add animated tiles to TileMap
        tileMap.AnimatedTiles = ExtractAnimatedTiles(tmxDoc);

        // Create entity with components (no TileCollider - using TileProperties instead)
        var mapEntity = world.Create(tileMap, tileProperties);

        Console.WriteLine(
            $"✅ Loaded map entity: {tileMap.MapId} ({tileMap.Width}x{tileMap.Height} tiles)"
        );
        Console.WriteLine($"   Entity ID: {mapEntity}");
        Console.WriteLine($"   Components: TileMap, TileProperties");
        Console.WriteLine($"   Collision: Tile-type (from tileset properties) + Bounds checking");
        Console.WriteLine($"   Animated tiles: {tileMap.AnimatedTiles?.Length ?? 0}");
        Console.WriteLine($"   Tiles with properties: {tileProperties.TileCount}");

        return mapEntity;
    }

    /// <summary>
    /// Loads a Tiled map from JSON file and converts to TileMap component.
    /// </summary>
    /// <param name="mapPath">Path to the .json map file.</param>
    /// <returns>TileMap component ready for ECS.</returns>
    [Obsolete("Use LoadMapEntity() instead for better performance (avoids multiple file parses)")]
    public TileMap LoadMap(string mapPath)
    {
        var tmxDoc = TiledMapLoader.Load(mapPath);
        return ExtractTileMap(tmxDoc, mapPath);
    }

    /// <summary>
    /// Extracts TileMap component from a parsed Tiled map document.
    /// </summary>
    private TileMap ExtractTileMap(TmxDocument tmxDoc, string mapPath)
    {
        // Extract layers by name
        var groundLayer = tmxDoc.Layers.FirstOrDefault(l =>
            l.Name.Equals("Ground", StringComparison.OrdinalIgnoreCase)
        );
        var objectLayer = tmxDoc.Layers.FirstOrDefault(l =>
            l.Name.Equals("Objects", StringComparison.OrdinalIgnoreCase)
        );
        var overheadLayer = tmxDoc.Layers.FirstOrDefault(l =>
            l.Name.Equals("Overhead", StringComparison.OrdinalIgnoreCase)
        );

        // Get tileset info and extract texture ID
        var tileset =
            tmxDoc.Tilesets.FirstOrDefault()
            ?? throw new InvalidOperationException($"Map '{mapPath}' has no tilesets");

        var tilesetId = ExtractTilesetId(tileset, mapPath);

        // Ensure tileset texture is loaded
        if (!_assetManager.HasTexture(tilesetId))
        {
            LoadTilesetTexture(tileset, mapPath, tilesetId);
        }

        // Create TileMap component
        return new TileMap
        {
            MapId = Path.GetFileNameWithoutExtension(mapPath),
            Width = tmxDoc.Width,
            Height = tmxDoc.Height,
            TilesetId = tilesetId,
            GroundLayer = groundLayer?.Data ?? new int[tmxDoc.Height, tmxDoc.Width],
            ObjectLayer = objectLayer?.Data ?? new int[tmxDoc.Height, tmxDoc.Width],
            OverheadLayer = overheadLayer?.Data ?? new int[tmxDoc.Height, tmxDoc.Width],
        };
    }

    /// <summary>
    /// Loads animated tile data from a Tiled map.
    /// </summary>
    /// <param name="mapPath">Path to the .json map file.</param>
    /// <returns>Array of AnimatedTile components for tiles with animations.</returns>
    [Obsolete("Use LoadMapEntity() instead for better performance (avoids multiple file parses)")]
    public AnimatedTile[] LoadAnimatedTiles(string mapPath)
    {
        var tmxDoc = TiledMapLoader.Load(mapPath);
        return ExtractAnimatedTiles(tmxDoc);
    }

    /// <summary>
    /// Extracts AnimatedTile array from a parsed Tiled map document.
    /// </summary>
    private AnimatedTile[] ExtractAnimatedTiles(TmxDocument tmxDoc)
    {
        var tileset = tmxDoc.Tilesets.FirstOrDefault();

        if (tileset == null || tileset.Animations.Count == 0)
        {
            return Array.Empty<AnimatedTile>();
        }

        var animatedTiles = new List<AnimatedTile>();

        foreach (var kvp in tileset.Animations)
        {
            int localTileId = kvp.Key;
            var animation = kvp.Value;

            // Convert local tile ID to global tile ID
            int globalTileId = tileset.FirstGid + localTileId;

            // Convert frame local IDs to global IDs
            var globalFrameIds = animation
                .FrameTileIds.Select(id => tileset.FirstGid + id)
                .ToArray();

            animatedTiles.Add(
                new AnimatedTile(globalTileId, globalFrameIds, animation.FrameDurations)
            );
        }

        return animatedTiles.ToArray();
    }

    /// <summary>
    /// Loads tile properties from a Tiled map (data-driven).
    /// Properties are defined in Tiled editor - no hardcoded tile types!
    /// Supports custom properties like "passable", "encounter_rate", "terrain_type", etc.
    /// </summary>
    /// <param name="mapPath">Path to the .json map file.</param>
    /// <returns>TileProperties component with all tile properties from Tiled.</returns>
    [Obsolete("Use LoadMapEntity() instead for better performance (avoids multiple file parses)")]
    public TileProperties LoadTileProperties(string mapPath)
    {
        var tmxDoc = TiledMapLoader.Load(mapPath);
        return ExtractTileProperties(tmxDoc);
    }

    /// <summary>
    /// Extracts TileProperties component from a parsed Tiled map document.
    /// </summary>
    private TileProperties ExtractTileProperties(TmxDocument tmxDoc)
    {
        var tileProps = new TileProperties();

        // Process each tileset
        foreach (var tileset in tmxDoc.Tilesets)
        {
            // Convert local tile IDs to global IDs and store properties
            foreach (var kvp in tileset.TileProperties)
            {
                int localTileId = kvp.Key;
                var properties = kvp.Value;

                // Convert to global tile ID
                int globalTileId = tileset.FirstGid + localTileId;

                // Store properties for this tile
                tileProps.TilePropertyMap[globalTileId] = properties;
            }
        }

        return tileProps;
    }

    private static string ExtractTilesetId(TmxTileset tileset, string mapPath)
    {
        // If tileset has an image, use the image filename as ID
        if (tileset.Image != null && !string.IsNullOrEmpty(tileset.Image.Source))
        {
            return Path.GetFileNameWithoutExtension(tileset.Image.Source);
        }

        // Fallback to tileset name
        return tileset.Name ?? "default-tileset";
    }

    private void LoadTilesetTexture(TmxTileset tileset, string mapPath, string tilesetId)
    {
        if (tileset.Image == null || string.IsNullOrEmpty(tileset.Image.Source))
        {
            throw new InvalidOperationException($"Tileset has no image source");
        }

        // Resolve relative path from map directory
        var mapDirectory = Path.GetDirectoryName(mapPath) ?? string.Empty;
        var tilesetPath = Path.Combine(mapDirectory, tileset.Image.Source);

        // Make path relative to Assets root for AssetManager
        var assetsRoot = "Assets";
        var relativePath = Path.GetRelativePath(assetsRoot, tilesetPath);

        _assetManager.LoadTexture(tilesetId, relativePath);
    }
}
