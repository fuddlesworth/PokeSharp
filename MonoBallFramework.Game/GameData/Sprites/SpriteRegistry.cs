using System.Collections.Concurrent;
using System.Text.Json;
using Microsoft.Extensions.Logging;
using MonoBallFramework.Game.Engine.Core.Types;
using MonoBallFramework.Game.Infrastructure.Services;

namespace MonoBallFramework.Game.GameData.Sprites;

/// <summary>
///     Registry for sprite definitions.
///     Manages available sprite assets from the Data/Sprites directory.
///     Provides thread-safe registration and lookup of sprite definitions by ID or path.
/// </summary>
/// <remarks>
///     <para>
///         <b>Thread Safety:</b>
///         - ConcurrentDictionary for thread-safe registration during parallel loading
///         - Async loading with parallel file I/O for improved performance
///         - SemaphoreSlim to prevent concurrent LoadDefinitionsAsync calls
///         - CancellationToken support for graceful shutdown
///     </para>
/// </remarks>
public class SpriteRegistry
{
    private readonly ConcurrentDictionary<GameSpriteId, SpriteDefinition> _sprites = new();
    private readonly SemaphoreSlim _loadLock = new(1, 1);
    private readonly IAssetPathResolver _pathResolver;
    private readonly ILogger<SpriteRegistry> _logger;
    private volatile bool _isLoaded = false;

    /// <summary>
    ///     Initializes a new instance of the <see cref="SpriteRegistry"/> class.
    /// </summary>
    /// <param name="pathResolver">Asset path resolver for locating sprite data files.</param>
    /// <param name="logger">Logger for diagnostic information.</param>
    public SpriteRegistry(IAssetPathResolver pathResolver, ILogger<SpriteRegistry> logger)
    {
        _pathResolver = pathResolver ?? throw new ArgumentNullException(nameof(pathResolver));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    /// <summary>
    ///     Gets whether sprite definitions have been loaded.
    /// </summary>
    public bool IsLoaded => _isLoaded;

    /// <summary>
    ///     Gets the total number of registered sprites.
    /// </summary>
    public int Count => _sprites.Count;

    /// <summary>
    ///     Registers a sprite definition.
    ///     Thread-safe for parallel loading.
    /// </summary>
    /// <param name="definition">The sprite definition to register.</param>
    /// <exception cref="ArgumentNullException">Thrown if definition is null.</exception>
    /// <exception cref="ArgumentException">Thrown if definition.Id is null or empty.</exception>
    public void RegisterSprite(SpriteDefinition definition)
    {
        ArgumentNullException.ThrowIfNull(definition);
        ArgumentException.ThrowIfNullOrEmpty(definition.Id);

        // Parse the ID string directly - format: base:sprite:category/name
        var spriteId = new GameSpriteId(definition.Id);
        if (_sprites.TryAdd(spriteId, definition))
        {
            _logger.LogDebug("Registered sprite: {SpriteId}", definition.Id);
        }
        else
        {
            _logger.LogWarning("Sprite {SpriteId} already registered, skipping", definition.Id);
        }
    }

    /// <summary>
    ///     Gets a sprite definition by its full ID.
    /// </summary>
    /// <param name="spriteId">The full sprite ID (e.g., "base:sprite:npcs/generic/prof_birch").</param>
    /// <returns>The sprite definition if found; otherwise, null.</returns>
    public SpriteDefinition? GetSprite(GameSpriteId spriteId)
    {
        return _sprites.TryGetValue(spriteId, out SpriteDefinition? definition) ? definition : null;
    }

    /// <summary>
    ///     Gets a sprite definition by its path.
    ///     Format: {type}/{name} (e.g., "npcs/generic_twin", "players/may_normal")
    /// </summary>
    /// <param name="path">The sprite path (e.g., "npcs/generic_prof_birch").</param>
    /// <returns>The sprite definition if found; otherwise, null.</returns>
    public SpriteDefinition? GetSpriteByPath(string path)
    {
        if (string.IsNullOrWhiteSpace(path))
        {
            return null;
        }

        // Normalize path separators
        string normalizedPath = path.Replace('\\', '/').Trim('/');

        // Search for sprite with matching path
        var match = _sprites.FirstOrDefault(kvp =>
        {
            var parts = kvp.Key.Value.Split(':');
            if (parts.Length >= 3)
            {
                string spritePath = parts[2]; // Extract the path portion (e.g., "npcs/generic_twin")
                return spritePath.Equals(normalizedPath, StringComparison.OrdinalIgnoreCase);
            }
            return false;
        }).Value;

        if (match == null)
        {
            _logger.LogDebug("Sprite not found by path: {Path}", path);
        }

        return match;
    }

    /// <summary>
    ///     Tries to get a sprite definition by its full ID.
    /// </summary>
    /// <param name="spriteId">The full sprite ID.</param>
    /// <param name="definition">The sprite definition if found; otherwise, null.</param>
    /// <returns>True if the sprite was found; otherwise, false.</returns>
    public bool TryGetSprite(GameSpriteId spriteId, out SpriteDefinition? definition)
    {
        return _sprites.TryGetValue(spriteId, out definition);
    }

    /// <summary>
    ///     Gets all registered sprite IDs.
    /// </summary>
    /// <returns>An enumerable collection of all sprite IDs.</returns>
    public IEnumerable<GameSpriteId> GetAllSpriteIds()
    {
        return _sprites.Keys;
    }

    /// <summary>
    ///     Loads all sprite definitions from the Data/Sprites folder synchronously.
    /// </summary>
    public void LoadDefinitions()
    {
        LoadDefinitionsFromJson();
        _isLoaded = true;
        _logger.LogInformation("Loaded {Count} sprite definitions synchronously", _sprites.Count);
    }

    /// <summary>
    ///     Loads sprite definitions asynchronously with parallel file I/O.
    ///     Thread-safe - concurrent calls will wait for the first load to complete.
    /// </summary>
    /// <param name="cancellationToken">Cancellation token for graceful shutdown.</param>
    /// <remarks>
    ///     <para>
    ///         <b>Performance:</b> Sprite files are loaded in parallel using Task.WhenAll,
    ///         reducing total load time significantly compared to sequential loading.
    ///         Recursively scans all subdirectories under Data/Sprites.
    ///     </para>
    /// </remarks>
    public async Task LoadDefinitionsAsync(CancellationToken cancellationToken = default)
    {
        // Fast path: already loaded
        if (_isLoaded)
        {
            _logger.LogDebug("Sprite definitions already loaded, skipping");
            return;
        }

        // Prevent concurrent loads
        await _loadLock.WaitAsync(cancellationToken);
        try
        {
            // Double-check after acquiring lock
            if (_isLoaded)
            {
                return;
            }

            _logger.LogInformation("Loading sprite definitions asynchronously...");

            string spritesPath = _pathResolver.ResolveData("Sprites");

            if (!Directory.Exists(spritesPath))
            {
                _logger.LogWarning("Sprites directory not found at {Path}", spritesPath);
                _isLoaded = true;
                return;
            }

            var options = new JsonSerializerOptions
            {
                PropertyNameCaseInsensitive = true,
                ReadCommentHandling = JsonCommentHandling.Skip
            };

            // Load all sprite definition files recursively in parallel
            await LoadSpritesRecursiveAsync(spritesPath, options, cancellationToken);

            _isLoaded = true;
            _logger.LogInformation("Loaded {Count} sprite definitions asynchronously", _sprites.Count);
        }
        finally
        {
            _loadLock.Release();
        }
    }

    /// <summary>
    ///     Recursively loads sprite definitions from a directory and its subdirectories.
    /// </summary>
    private async Task LoadSpritesRecursiveAsync(string path, JsonSerializerOptions options, CancellationToken ct)
    {
        if (!Directory.Exists(path))
        {
            return;
        }

        // Get all JSON files in the current directory
        string[] jsonFiles = Directory.GetFiles(path, "*.json", SearchOption.AllDirectories);

        _logger.LogDebug("Found {Count} sprite definition files in {Path}", jsonFiles.Length, path);

        // Load all sprite files in parallel
        var tasks = jsonFiles.Select(async jsonFile =>
        {
            try
            {
                string json = await File.ReadAllTextAsync(jsonFile, ct);
                var definition = JsonSerializer.Deserialize<SpriteDefinition>(json, options);
                if (definition != null)
                {
                    RegisterSprite(definition);
                }
                else
                {
                    _logger.LogWarning("Failed to deserialize sprite definition from {File}", jsonFile);
                }
            }
            catch (OperationCanceledException)
            {
                _logger.LogInformation("Sprite loading cancelled for {File}", jsonFile);
                throw;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error loading sprite definition from {File}", jsonFile);
            }
        });

        await Task.WhenAll(tasks);
    }

    /// <summary>
    ///     Loads sprite definitions from JSON files synchronously.
    /// </summary>
    private void LoadDefinitionsFromJson()
    {
        string spritesPath = _pathResolver.ResolveData("Sprites");

        if (!Directory.Exists(spritesPath))
        {
            _logger.LogWarning("Sprites directory not found at {Path}", spritesPath);
            return;
        }

        var options = new JsonSerializerOptions
        {
            PropertyNameCaseInsensitive = true,
            ReadCommentHandling = JsonCommentHandling.Skip
        };

        // Load all JSON files recursively
        string[] jsonFiles = Directory.GetFiles(spritesPath, "*.json", SearchOption.AllDirectories);

        _logger.LogDebug("Found {Count} sprite definition files in {Path}", jsonFiles.Length, spritesPath);

        foreach (string jsonFile in jsonFiles)
        {
            try
            {
                string json = File.ReadAllText(jsonFile);
                SpriteDefinition? definition = JsonSerializer.Deserialize<SpriteDefinition>(json, options);

                if (definition != null)
                {
                    RegisterSprite(definition);
                }
                else
                {
                    _logger.LogWarning("Failed to deserialize sprite definition from {File}", jsonFile);
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error loading sprite definition from {File}", jsonFile);
            }
        }
    }
}
