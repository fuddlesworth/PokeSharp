using System.Diagnostics;
using System.Text.Json;
using System.Linq;
using Microsoft.Extensions.Logging;
using Microsoft.Xna.Framework.Graphics;
using PokeSharp.Engine.Common.Logging;

namespace PokeSharp.Engine.Rendering.Assets;

/// <summary>
///     Manages runtime asset loading for textures and resources.
///     Provides PNG loading without MonoGame Content Pipeline.
/// </summary>
public class AssetManager(
    GraphicsDevice graphicsDevice,
    string assetRoot = RenderingConstants.DefaultAssetRoot,
    ILogger<AssetManager>? logger = null
) : IAssetProvider, IDisposable
{
    private readonly string _assetRoot = assetRoot;

    private readonly GraphicsDevice _graphicsDevice =
        graphicsDevice ?? throw new ArgumentNullException(nameof(graphicsDevice));

    private readonly ILogger<AssetManager>? _logger = logger;

    // LRU cache with 50MB budget for texture memory management
    private readonly LruCache<string, Texture2D> _textures = new(
        maxSizeBytes: 50_000_000, // 50MB budget
        sizeCalculator: texture => texture.Width * texture.Height * 4L, // RGBA = 4 bytes/pixel
        logger: logger
    );

    private bool _disposed;

    /// <summary>
    ///     Gets the root directory path where assets are stored.
    /// </summary>
    public string AssetRoot => _assetRoot;

    /// <summary>
    ///     Gets the number of loaded textures.
    /// </summary>
    public int LoadedTextureCount => _textures.Count;

    /// <summary>
    ///     Gets the current texture cache memory usage in bytes.
    /// </summary>
    public long TextureCacheSizeBytes => _textures.CurrentSize;

    /// <summary>
    ///     Disposes all loaded textures.
    /// </summary>
    public void Dispose()
    {
        if (_disposed)
            return;

        _textures.Clear(); // LruCache.Clear() disposes all textures
        _disposed = true;

        GC.SuppressFinalize(this);
    }

    // REMOVED: LoadManifest() and LoadManifestInternal() - obsolete
    // manifest.json has been replaced by EF Core MapDefinition and on-demand texture loading

    /// <summary>
    ///     Loads a texture from a PNG file and caches it.
    /// </summary>
    /// <param name="id">Unique identifier for the texture.</param>
    /// <param name="relativePath">Path relative to asset root.</param>
    public void LoadTexture(string id, string relativePath)
    {
        ArgumentException.ThrowIfNullOrEmpty(id);
        ArgumentException.ThrowIfNullOrEmpty(relativePath);

        var normalizedRelative = relativePath.Replace('/', Path.DirectorySeparatorChar);
        var fullPath = Path.Combine(_assetRoot, normalizedRelative);

        if (!File.Exists(fullPath))
        {
            var fallbackPath = ResolveFallbackTexturePath(id, normalizedRelative);
            if (fallbackPath is not null && File.Exists(fallbackPath))
            {
                _logger?.LogWarning(
                    "Texture '{TextureId}' not found at '{OriginalPath}'. Using fallback '{FallbackPath}'.",
                    id,
                    fullPath,
                    fallbackPath
                );
                fullPath = fallbackPath;
            }
            else
            {
                throw new FileNotFoundException($"Texture file not found: {fullPath}");
            }
        }

        var sw = Stopwatch.StartNew();

        using var fileStream = File.OpenRead(fullPath);
        var texture = Texture2D.FromStream(_graphicsDevice, fileStream);

        sw.Stop();
        var elapsedMs = sw.Elapsed.TotalMilliseconds;

        _textures.AddOrUpdate(id, texture); // LRU cache auto-evicts if needed

        // Log texture loading with timing
        _logger?.LogTextureLoaded(id, elapsedMs, texture.Width, texture.Height);

        // Warn about slow texture loads (>100ms)
        if (elapsedMs > 100.0)
            _logger?.LogSlowTextureLoad(id, elapsedMs);
    }

    private string? ResolveFallbackTexturePath(string id, string normalizedRelativePath)
    {
        var normalized = normalizedRelativePath.Replace('\\', '/');

        if (normalized.StartsWith("Tilesets/", StringComparison.OrdinalIgnoreCase))
        {
            var fileName = Path.GetFileName(normalizedRelativePath);
            var tilesetsRoot = Path.Combine(_assetRoot, "Tilesets");
            var tilesetDir = Path.Combine(tilesetsRoot, id);

            if (!string.IsNullOrEmpty(fileName))
            {
                var nestedPath = Path.Combine(tilesetDir, fileName);
                if (File.Exists(nestedPath))
                    return nestedPath;
            }

            // Try reading the tileset JSON for the actual image name
            var tilesetJson = Path.Combine(tilesetDir, $"{id}.json");
            if (File.Exists(tilesetJson))
            {
                var imageName = TryGetTilesetImageName(tilesetJson);
                if (!string.IsNullOrEmpty(imageName))
                {
                    var jsonImagePath = Path.Combine(tilesetDir, imageName);
                    if (File.Exists(jsonImagePath))
                        return jsonImagePath;
                }
            }

            // Fallback: pick the first PNG found in the tileset directory
            if (Directory.Exists(tilesetDir))
            {
                var pngMatch = Directory
                    .EnumerateFiles(tilesetDir, "*.png", SearchOption.TopDirectoryOnly)
                    .FirstOrDefault();
                if (pngMatch is not null)
                    return pngMatch;
            }
        }

        return null;
    }

    private string? TryGetTilesetImageName(string tilesetJsonPath)
    {
        try
        {
            using var doc = JsonDocument.Parse(File.ReadAllText(tilesetJsonPath));
            if (doc.RootElement.TryGetProperty("image", out var imageProperty))
                return imageProperty.GetString();
        }
        catch (Exception ex)
        {
            _logger?.LogWarning(
                ex,
                "Failed to read tileset JSON '{TilesetJson}' while resolving fallback texture path.",
                tilesetJsonPath
            );
        }

        return null;
    }

    /// <summary>
    ///     Gets a cached texture by its identifier.
    /// </summary>
    /// <param name="id">The texture identifier.</param>
    /// <returns>The cached texture.</returns>
    /// <exception cref="KeyNotFoundException">Thrown if texture not found.</exception>
    public Texture2D GetTexture(string id)
    {
        if (_textures.TryGetValue(id, out var texture) && texture != null)
            return texture;

        throw new KeyNotFoundException(
            $"Texture '{id}' not loaded or was evicted from cache."
        );
    }

    /// <summary>
    ///     Checks if a texture is loaded in cache.
    /// </summary>
    /// <param name="id">The texture identifier.</param>
    /// <returns>True if texture is loaded.</returns>
    public bool HasTexture(string id)
    {
        return _textures.TryGetValue(id, out _);
    }

    // REMOVED: HotReloadTexture() - obsolete (depended on manifest.json)
    // For hot-reloading during development, use UnregisterTexture() + LoadTexture() manually

    /// <summary>
    ///     Registers a pre-loaded texture with the asset manager.
    ///     Useful for runtime-loaded textures (e.g., sprite sheets).
    /// </summary>
    /// <param name="id">Unique identifier for the texture.</param>
    /// <param name="texture">The pre-loaded texture to register.</param>
    public void RegisterTexture(string id, Texture2D texture)
    {
        ArgumentException.ThrowIfNullOrEmpty(id);
        ArgumentNullException.ThrowIfNull(texture);

        // LruCache will handle old texture disposal automatically
        _textures.AddOrUpdate(id, texture);
        _logger?.LogDebug(
            "Registered texture: {TextureId} ({Width}x{Height})",
            id,
            texture.Width,
            texture.Height);
    }

    /// <summary>
    ///     Unregisters and disposes a texture.
    /// </summary>
    /// <param name="id">The texture identifier to remove.</param>
    /// <returns>True if texture was found and removed.</returns>
    public bool UnregisterTexture(string id)
    {
        var removed = _textures.Remove(id); // LruCache handles disposal
        if (removed)
        {
            _logger?.LogDebug("Unregistered texture: {TextureId}", id);
        }
        return removed;
    }
}

