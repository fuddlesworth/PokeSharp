# Content Provider Implementation Guide

**Project:** PokeSharp - Base Game as Mod Architecture
**Status:** Ready for Implementation
**Estimated Effort:** 4 weeks (20 working days)

---

## Table of Contents

1. [Problem Statement](#1-problem-statement)
2. [Solution Overview](#2-solution-overview)
3. [Phase 1: Core Infrastructure](#3-phase-1-core-infrastructure-week-1)
4. [Phase 2: System Integration](#4-phase-2-system-integration-week-2)
5. [Phase 3: Content Migration](#5-phase-3-content-migration-week-3)
6. [Phase 4: Polish & Release](#6-phase-4-polish--release-week-4)
7. [Code Reference](#7-code-reference)
8. [Testing Checklist](#8-testing-checklist)
9. [Success Criteria](#9-success-criteria)

---

## 1. Problem Statement

### The Critical Gap

ModLoader registers content folders but GameDataLoader never queries them:

```
Current Flow (BROKEN):
┌─────────────────┐     ┌──────────────────┐     ┌─────────────────┐
│   ModLoader     │     │  Content Folders │     │  GameDataLoader │
│                 │────▶│   (registered)   │  ✗  │                 │
│ LoadModsAsync() │     │                  │     │ LoadAllAsync()  │
└─────────────────┘     └──────────────────┘     └─────────────────┘
                              NOT CONNECTED!
                                    │
                                    ▼
                        GameDataLoader only reads from
                        hardcoded "Assets/Definitions/"
```

### Impact

- **88% of content** (~3,500 files) cannot be loaded from mods
- Mods can register content folders but they're never used
- Font paths, theme defaults are hardcoded
- No way to override base game content

### Root Causes (87 coupling issues identified)

| Category | Count | Severity |
|----------|-------|----------|
| Font path hardcoding | 5 | HIGH |
| Asset root hardcoding | 11 | MEDIUM |
| Theme fallback hardcoding | 4 | MEDIUM |
| ModLoader↔GameDataLoader disconnect | 1 | CRITICAL |
| Type ID defaults | 6 | LOW |
| Other | 60 | LOW |

---

## 2. Solution Overview

### The Fix: IContentProvider Interface

Create a bridge between ModLoader and all content consumers:

```
Fixed Flow:
┌─────────────────┐     ┌──────────────────┐     ┌─────────────────┐
│   ModLoader     │────▶│ IContentProvider │◀────│  GameDataLoader │
│                 │     │                  │     │                 │
│ LoadModsAsync() │     │ ResolveContent() │     │ LoadAllAsync()  │
└─────────────────┘     └──────────────────┘     └─────────────────┘
                               │
                               ▼
                        ┌──────────────────┐
                        │   AssetManager   │
                        │   FontLoader     │
                        │   ScriptService  │
                        └──────────────────┘
```

### Key Benefits

- **Single point of content resolution** with priority-based lookup
- **LRU caching** for performance (target: >90% hit rate)
- **Hot reload support** via cache invalidation
- **Backward compatible** with existing code

---

## 3. Phase 1: Core Infrastructure (Week 1)

### Task 1.1: Create IContentProvider Interface

**File:** `MonoBallFramework.Game/Engine/Content/IContentProvider.cs`

```csharp
namespace MonoBallFramework.Game.Engine.Content;

/// <summary>
/// Provides unified content resolution across base game and mods.
/// Resolves content paths using priority-based lookup with LRU caching.
/// </summary>
public interface IContentProvider
{
    /// <summary>
    /// Resolves a content path, checking mods (by priority) then base game.
    /// </summary>
    /// <param name="contentType">Content category (e.g., "Definitions", "Graphics")</param>
    /// <param name="relativePath">Relative path within content folder</param>
    /// <returns>Full resolved path, or null if not found</returns>
    string? ResolveContentPath(string contentType, string relativePath);

    /// <summary>
    /// Gets all content paths matching a pattern across all sources.
    /// Results are ordered by priority (highest first, base game last).
    /// </summary>
    /// <param name="contentType">Content category</param>
    /// <param name="pattern">Glob pattern (e.g., "*.json", "Maps/**/*.json")</param>
    /// <returns>All matching paths with duplicates removed (highest priority wins)</returns>
    IEnumerable<string> GetAllContentPaths(string contentType, string pattern = "*.json");

    /// <summary>
    /// Checks if content exists in any source.
    /// </summary>
    bool ContentExists(string contentType, string relativePath);

    /// <summary>
    /// Gets the source (mod ID or "base") that provides the content.
    /// </summary>
    string? GetContentSource(string contentType, string relativePath);

    /// <summary>
    /// Invalidates cached paths. Call when mods are loaded/unloaded.
    /// </summary>
    /// <param name="contentType">Specific type to invalidate, or null for all</param>
    void InvalidateCache(string? contentType = null);

    /// <summary>
    /// Gets cache statistics for monitoring.
    /// </summary>
    ContentProviderStats GetStats();
}

/// <summary>
/// Cache and resolution statistics.
/// </summary>
public sealed class ContentProviderStats
{
    public int CacheHits { get; init; }
    public int CacheMisses { get; init; }
    public int TotalResolutions { get; init; }
    public int CachedEntries { get; init; }
    public double HitRate => TotalResolutions > 0 ? (double)CacheHits / TotalResolutions : 0;
}
```

**Estimated Time:** 1 hour

---

### Task 1.2: Create ContentProviderOptions

**File:** `MonoBallFramework.Game/Engine/Content/ContentProviderOptions.cs`

```csharp
namespace MonoBallFramework.Game.Engine.Content;

/// <summary>
/// Configuration options for ContentProvider.
/// </summary>
public sealed class ContentProviderOptions
{
    /// <summary>
    /// Maximum number of cached path resolutions.
    /// Default: 10,000 entries (~1MB memory)
    /// </summary>
    public int MaxCacheSize { get; set; } = 10_000;

    /// <summary>
    /// Base game content root path.
    /// Default: "Assets"
    /// </summary>
    public string BaseGameRoot { get; set; } = "Assets";

    /// <summary>
    /// Whether to log cache misses (useful for debugging).
    /// </summary>
    public bool LogCacheMisses { get; set; } = false;

    /// <summary>
    /// Whether to throw on path traversal attempts (../).
    /// If false, returns null instead.
    /// </summary>
    public bool ThrowOnPathTraversal { get; set; } = true;

    /// <summary>
    /// Content type to folder mapping for base game.
    /// Loaded from Assets/mod.json at startup.
    /// </summary>
    public Dictionary<string, string> BaseContentFolders { get; set; } = new()
    {
        ["Definitions"] = "Definitions",
        ["Graphics"] = "Graphics",
        ["Audio"] = "Audio",
        ["Scripts"] = "Scripts",
        ["Fonts"] = "Fonts",
        ["Tiled"] = "Tiled",
        ["Tilesets"] = "Tilesets"
    };
}
```

**Estimated Time:** 30 minutes

---

### Task 1.3: Implement ContentProvider

**File:** `MonoBallFramework.Game/Engine/Content/ContentProvider.cs`

```csharp
using System.Collections.Concurrent;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using MonoBallFramework.Game.Engine.Core.Modding;

namespace MonoBallFramework.Game.Engine.Content;

/// <summary>
/// Default implementation of IContentProvider with LRU caching.
/// </summary>
public sealed class ContentProvider : IContentProvider
{
    private readonly ModLoader _modLoader;
    private readonly ILogger<ContentProvider> _logger;
    private readonly ContentProviderOptions _options;
    private readonly LruCache<string, string?> _cache;

    // Statistics
    private int _cacheHits;
    private int _cacheMisses;

    public ContentProvider(
        ModLoader modLoader,
        ILogger<ContentProvider> logger,
        IOptions<ContentProviderOptions> options)
    {
        _modLoader = modLoader ?? throw new ArgumentNullException(nameof(modLoader));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        _options = options?.Value ?? new ContentProviderOptions();
        _cache = new LruCache<string, string?>(_options.MaxCacheSize);
    }

    public string? ResolveContentPath(string contentType, string relativePath)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(contentType);
        ArgumentException.ThrowIfNullOrWhiteSpace(relativePath);

        // Security: Prevent path traversal
        if (!IsPathSafe(relativePath))
        {
            _logger.LogWarning("Path traversal attempt blocked: {Path}", relativePath);
            if (_options.ThrowOnPathTraversal)
                throw new ArgumentException($"Invalid path: {relativePath}");
            return null;
        }

        // Check cache first
        string cacheKey = $"{contentType}:{relativePath}";
        if (_cache.TryGet(cacheKey, out string? cachedPath))
        {
            Interlocked.Increment(ref _cacheHits);
            return cachedPath;
        }

        Interlocked.Increment(ref _cacheMisses);

        // Resolve path
        string? resolvedPath = ResolvePathInternal(contentType, relativePath);

        // Cache result (even null for negative caching)
        _cache.Set(cacheKey, resolvedPath);

        if (_options.LogCacheMisses && resolvedPath != null)
        {
            _logger.LogDebug("Resolved {Type}/{Path} -> {FullPath}",
                contentType, relativePath, resolvedPath);
        }

        return resolvedPath;
    }

    private string? ResolvePathInternal(string contentType, string relativePath)
    {
        // 1. Check mods in priority order (highest first)
        var modsOrderedByPriority = _modLoader.LoadedMods.Values
            .OrderByDescending(m => m.Priority)
            .ToList();

        foreach (var mod in modsOrderedByPriority)
        {
            string? modPath = TryResolveInMod(mod, contentType, relativePath);
            if (modPath != null)
            {
                _logger.LogDebug("Content resolved from mod '{ModId}': {Path}",
                    mod.Id, modPath);
                return modPath;
            }
        }

        // 2. Check base game
        string? basePath = TryResolveInBaseGame(contentType, relativePath);
        if (basePath != null)
        {
            _logger.LogDebug("Content resolved from base game: {Path}", basePath);
            return basePath;
        }

        _logger.LogDebug("Content not found: {Type}/{Path}", contentType, relativePath);
        return null;
    }

    private string? TryResolveInMod(ModManifest mod, string contentType, string relativePath)
    {
        if (!mod.ContentFolders.TryGetValue(contentType, out string? contentFolder))
            return null;

        string fullPath = Path.Combine(mod.DirectoryPath, contentFolder, relativePath);
        return File.Exists(fullPath) ? fullPath : null;
    }

    private string? TryResolveInBaseGame(string contentType, string relativePath)
    {
        if (!_options.BaseContentFolders.TryGetValue(contentType, out string? contentFolder))
            return null;

        string fullPath = Path.Combine(_options.BaseGameRoot, contentFolder, relativePath);
        return File.Exists(fullPath) ? fullPath : null;
    }

    public IEnumerable<string> GetAllContentPaths(string contentType, string pattern = "*.json")
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(contentType);

        var seenRelativePaths = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var results = new List<string>();

        // 1. Collect from mods (highest priority first)
        var modsOrderedByPriority = _modLoader.LoadedMods.Values
            .OrderByDescending(m => m.Priority)
            .ToList();

        foreach (var mod in modsOrderedByPriority)
        {
            if (!mod.ContentFolders.TryGetValue(contentType, out string? contentFolder))
                continue;

            string searchPath = Path.Combine(mod.DirectoryPath, contentFolder);
            if (!Directory.Exists(searchPath))
                continue;

            foreach (string file in Directory.EnumerateFiles(searchPath, pattern,
                SearchOption.AllDirectories))
            {
                string relativePath = Path.GetRelativePath(searchPath, file);
                if (seenRelativePaths.Add(relativePath))
                {
                    results.Add(file);
                }
            }
        }

        // 2. Collect from base game (lowest priority - only add if not overridden)
        if (_options.BaseContentFolders.TryGetValue(contentType, out string? baseFolder))
        {
            string basePath = Path.Combine(_options.BaseGameRoot, baseFolder);
            if (Directory.Exists(basePath))
            {
                foreach (string file in Directory.EnumerateFiles(basePath, pattern,
                    SearchOption.AllDirectories))
                {
                    string relativePath = Path.GetRelativePath(basePath, file);
                    if (seenRelativePaths.Add(relativePath))
                    {
                        results.Add(file);
                    }
                }
            }
        }

        return results;
    }

    public bool ContentExists(string contentType, string relativePath)
    {
        return ResolveContentPath(contentType, relativePath) != null;
    }

    public string? GetContentSource(string contentType, string relativePath)
    {
        if (!IsPathSafe(relativePath))
            return null;

        // Check mods first
        var modsOrderedByPriority = _modLoader.LoadedMods.Values
            .OrderByDescending(m => m.Priority)
            .ToList();

        foreach (var mod in modsOrderedByPriority)
        {
            if (TryResolveInMod(mod, contentType, relativePath) != null)
                return mod.Id;
        }

        // Check base game
        if (TryResolveInBaseGame(contentType, relativePath) != null)
            return "base";

        return null;
    }

    public void InvalidateCache(string? contentType = null)
    {
        if (contentType == null)
        {
            _cache.Clear();
            _logger.LogInformation("Content cache fully invalidated");
        }
        else
        {
            // Partial invalidation - remove entries starting with contentType
            _cache.RemoveWhere(key => key.StartsWith($"{contentType}:"));
            _logger.LogInformation("Content cache invalidated for type: {Type}", contentType);
        }
    }

    public ContentProviderStats GetStats()
    {
        return new ContentProviderStats
        {
            CacheHits = _cacheHits,
            CacheMisses = _cacheMisses,
            TotalResolutions = _cacheHits + _cacheMisses,
            CachedEntries = _cache.Count
        };
    }

    private static bool IsPathSafe(string path)
    {
        // Block path traversal and rooted paths
        if (path.Contains("..") || Path.IsPathRooted(path))
            return false;

        // Block null bytes
        if (path.Contains('\0'))
            return false;

        return true;
    }
}
```

**Estimated Time:** 4 hours

---

### Task 1.4: Implement LRU Cache

**File:** `MonoBallFramework.Game/Engine/Content/LruCache.cs`

```csharp
using System.Collections.Concurrent;

namespace MonoBallFramework.Game.Engine.Content;

/// <summary>
/// Thread-safe LRU (Least Recently Used) cache.
/// </summary>
public sealed class LruCache<TKey, TValue> where TKey : notnull
{
    private readonly int _capacity;
    private readonly ConcurrentDictionary<TKey, LinkedListNode<CacheEntry>> _cache;
    private readonly LinkedList<CacheEntry> _lruList;
    private readonly object _lock = new();

    private sealed class CacheEntry
    {
        public required TKey Key { get; init; }
        public required TValue Value { get; init; }
    }

    public LruCache(int capacity)
    {
        if (capacity <= 0)
            throw new ArgumentOutOfRangeException(nameof(capacity));

        _capacity = capacity;
        _cache = new ConcurrentDictionary<TKey, LinkedListNode<CacheEntry>>();
        _lruList = new LinkedList<CacheEntry>();
    }

    public int Count => _cache.Count;

    public bool TryGet(TKey key, out TValue value)
    {
        if (_cache.TryGetValue(key, out var node))
        {
            lock (_lock)
            {
                // Move to front (most recently used)
                _lruList.Remove(node);
                _lruList.AddFirst(node);
            }
            value = node.Value.Value;
            return true;
        }

        value = default!;
        return false;
    }

    public void Set(TKey key, TValue value)
    {
        lock (_lock)
        {
            if (_cache.TryGetValue(key, out var existingNode))
            {
                // Update existing - move to front
                _lruList.Remove(existingNode);
                var newNode = new LinkedListNode<CacheEntry>(
                    new CacheEntry { Key = key, Value = value });
                _lruList.AddFirst(newNode);
                _cache[key] = newNode;
                return;
            }

            // Evict if at capacity
            while (_cache.Count >= _capacity && _lruList.Last != null)
            {
                var lru = _lruList.Last;
                _lruList.RemoveLast();
                _cache.TryRemove(lru.Value.Key, out _);
            }

            // Add new entry
            var entry = new CacheEntry { Key = key, Value = value };
            var cacheNode = new LinkedListNode<CacheEntry>(entry);
            _lruList.AddFirst(cacheNode);
            _cache[key] = cacheNode;
        }
    }

    public void Clear()
    {
        lock (_lock)
        {
            _cache.Clear();
            _lruList.Clear();
        }
    }

    public void RemoveWhere(Func<TKey, bool> predicate)
    {
        lock (_lock)
        {
            var keysToRemove = _cache.Keys.Where(predicate).ToList();
            foreach (var key in keysToRemove)
            {
                if (_cache.TryRemove(key, out var node))
                {
                    _lruList.Remove(node);
                }
            }
        }
    }
}
```

**Estimated Time:** 2 hours

---

### Task 1.5: Create Base Game Manifest

**File:** `MonoBallFramework.Game/Assets/mod.json`

```json
{
  "id": "base:pokesharp-core",
  "name": "PokeSharp Core Content",
  "author": "PokeSharp Team",
  "version": "1.0.0",
  "description": "Base game content for PokeSharp - Pokemon-style game framework",
  "priority": 1000,
  "contentFolders": {
    "Definitions": "Definitions",
    "Graphics": "Graphics",
    "Audio": "Audio",
    "Scripts": "Scripts",
    "Fonts": "Fonts",
    "Tiled": "Tiled",
    "Tilesets": "Tilesets"
  },
  "scripts": [],
  "patches": [],
  "dependencies": []
}
```

**Estimated Time:** 15 minutes

---

### Task 1.6: Register Services

**File:** `MonoBallFramework.Game/Engine/Content/ContentExtensions.cs`

```csharp
using Microsoft.Extensions.DependencyInjection;

namespace MonoBallFramework.Game.Engine.Content;

/// <summary>
/// Extension methods for registering content services.
/// </summary>
public static class ContentExtensions
{
    /// <summary>
    /// Registers IContentProvider and related services.
    /// </summary>
    public static IServiceCollection AddContentProvider(
        this IServiceCollection services,
        Action<ContentProviderOptions>? configure = null)
    {
        services.Configure<ContentProviderOptions>(options =>
        {
            configure?.Invoke(options);
        });

        services.AddSingleton<IContentProvider, ContentProvider>();

        return services;
    }
}
```

**Estimated Time:** 30 minutes

---

### Phase 1 Checklist

- [ ] Create `Engine/Content/` directory
- [ ] Implement `IContentProvider.cs`
- [ ] Implement `ContentProviderOptions.cs`
- [ ] Implement `ContentProvider.cs`
- [ ] Implement `LruCache.cs`
- [ ] Implement `ContentExtensions.cs`
- [ ] Create `Assets/mod.json`
- [ ] Write unit tests for ContentProvider
- [ ] Write unit tests for LruCache
- [ ] Verify all tests pass

---

## 4. Phase 2: System Integration (Week 2)

### Task 2.1: Update ModLoader

**File:** `MonoBallFramework.Game/Engine/Core/Modding/ModLoader.cs`

Add method to load base game as a mod:

```csharp
/// <summary>
/// Loads the base game content as a special mod.
/// Should be called before LoadModsAsync().
/// </summary>
public async Task LoadBaseGameAsync(string baseGameRoot)
{
    string manifestPath = Path.Combine(baseGameRoot, "mod.json");

    if (!File.Exists(manifestPath))
    {
        _logger.LogWarning(
            "Base game mod.json not found at {Path}. Creating default manifest.",
            manifestPath);
        await CreateDefaultBaseManifestAsync(manifestPath, baseGameRoot);
    }

    ModManifest? manifest = ParseManifest(manifestPath, baseGameRoot);
    if (manifest != null)
    {
        // Ensure base game has highest priority
        if (manifest.Priority < 1000)
        {
            _logger.LogWarning(
                "Base game priority ({Priority}) is low. Setting to 1000.",
                manifest.Priority);
        }

        _loadedMods[manifest.Id] = manifest;
        _logger.LogInformation(
            "✅ Loaded base game: {Name} v{Version}",
            manifest.Name, manifest.Version);
    }
}

private async Task CreateDefaultBaseManifestAsync(string path, string baseGameRoot)
{
    var manifest = new
    {
        id = "base:pokesharp-core",
        name = "PokeSharp Core Content",
        author = "PokeSharp Team",
        version = "1.0.0",
        description = "Base game content",
        priority = 1000,
        contentFolders = new Dictionary<string, string>
        {
            ["Definitions"] = "Definitions",
            ["Graphics"] = "Graphics",
            ["Audio"] = "Audio",
            ["Scripts"] = "Scripts",
            ["Fonts"] = "Fonts",
            ["Tiled"] = "Tiled",
            ["Tilesets"] = "Tilesets"
        },
        scripts = Array.Empty<string>(),
        patches = Array.Empty<string>(),
        dependencies = Array.Empty<string>()
    };

    string json = JsonSerializer.Serialize(manifest, new JsonSerializerOptions
    {
        WriteIndented = true
    });

    await File.WriteAllTextAsync(path, json);
    _logger.LogInformation("Created default base game manifest at {Path}", path);
}
```

**Estimated Time:** 2 hours

---

### Task 2.2: Update GameDataLoader

**File:** `MonoBallFramework.Game/GameData/Loading/GameDataLoader.cs`

Inject IContentProvider and use it for loading:

```csharp
// Add to constructor parameters
private readonly IContentProvider _contentProvider;

public GameDataLoader(
    GameDataContext context,
    IContentProvider contentProvider,  // NEW
    ILogger<GameDataLoader> logger)
{
    _context = context;
    _contentProvider = contentProvider;  // NEW
    _logger = logger;
}

// Update LoadAllAsync to use ContentProvider
public async Task LoadAllAsync(CancellationToken cancellationToken = default)
{
    _logger.LogInformation("Loading game data from all content sources...");

    // Load maps from all sources (mods + base game)
    var mapFiles = _contentProvider.GetAllContentPaths("Definitions", "Maps/**/*.json");
    foreach (string mapFile in mapFiles)
    {
        await LoadMapEntityFromFileAsync(mapFile, cancellationToken);
    }

    // Load audio definitions
    var audioFiles = _contentProvider.GetAllContentPaths("Definitions", "Audio/**/*.json");
    foreach (string audioFile in audioFiles)
    {
        await LoadAudioEntityFromFileAsync(audioFile, cancellationToken);
    }

    // Load sprite definitions
    var spriteFiles = _contentProvider.GetAllContentPaths("Definitions", "Sprites/**/*.json");
    foreach (string spriteFile in spriteFiles)
    {
        await LoadSpriteDefinitionFromFileAsync(spriteFile, cancellationToken);
    }

    // Load behavior definitions
    var behaviorFiles = _contentProvider.GetAllContentPaths("Definitions", "Behaviors/*.json");
    foreach (string behaviorFile in behaviorFiles)
    {
        await LoadBehaviorDefinitionFromFileAsync(behaviorFile, cancellationToken);
    }

    // Load tile behavior definitions
    var tileBehaviorFiles = _contentProvider.GetAllContentPaths("Definitions", "TileBehaviors/*.json");
    foreach (string tileBehaviorFile in tileBehaviorFiles)
    {
        await LoadTileBehaviorDefinitionFromFileAsync(tileBehaviorFile, cancellationToken);
    }

    await _context.SaveChangesAsync(cancellationToken);

    _logger.LogInformation("Game data loading complete");
}

// Update individual load methods to track source
private async Task LoadMapEntityFromFileAsync(string filePath, CancellationToken ct)
{
    try
    {
        string json = await File.ReadAllTextAsync(filePath, ct);
        var dto = JsonSerializer.Deserialize<MapEntityDto>(json, _jsonOptions);

        if (dto == null) return;

        // Track which mod/source provided this content
        string? source = GetSourceFromPath(filePath);

        var entity = new MapEntity
        {
            Id = dto.Id,
            DisplayName = dto.DisplayName,
            Region = dto.Region ?? "hoenn",
            TiledPath = dto.TiledPath,
            SourceMod = source  // Track the source!
        };

        // Upsert - mod content overrides base
        var existing = await _context.Maps.FindAsync(new object[] { entity.Id }, ct);
        if (existing != null)
        {
            _context.Entry(existing).CurrentValues.SetValues(entity);
        }
        else
        {
            _context.Maps.Add(entity);
        }
    }
    catch (Exception ex)
    {
        _logger.LogError(ex, "Failed to load map from {Path}", filePath);
    }
}

private string? GetSourceFromPath(string filePath)
{
    // Determine source by checking if path is in a mod folder
    if (filePath.Contains("/Mods/") || filePath.Contains("\\Mods\\"))
    {
        // Extract mod folder name
        var parts = filePath.Split(new[] { "/Mods/", "\\Mods\\" },
            StringSplitOptions.None);
        if (parts.Length > 1)
        {
            var modFolder = parts[1].Split(new[] { '/', '\\' })[0];
            return modFolder;
        }
    }
    return "base";
}
```

**Estimated Time:** 4 hours

---

### Task 2.3: Update AssetManager

**File:** `MonoBallFramework.Game/Engine/Rendering/Assets/AssetManager.cs`

Add IContentProvider support:

```csharp
private readonly IContentProvider? _contentProvider;

public AssetManager(
    GraphicsDevice graphicsDevice,
    IContentProvider? contentProvider = null,  // NEW (optional for backward compat)
    string assetRoot = "Assets",
    ILogger<AssetManager>? logger = null)
{
    _graphicsDevice = graphicsDevice;
    _contentProvider = contentProvider;
    AssetRoot = assetRoot;
    _logger = logger;
}

public void LoadTexture(string id, string relativePath)
{
    if (HasTexture(id)) return;

    string? fullPath = null;

    // Try ContentProvider first (if available)
    if (_contentProvider != null)
    {
        fullPath = _contentProvider.ResolveContentPath("Graphics", relativePath);
    }

    // Fallback to legacy path resolution
    if (fullPath == null)
    {
        fullPath = Path.Combine(AssetRoot, relativePath);
        _logger?.LogDebug(
            "Using legacy path for texture {Id}: {Path}",
            id, fullPath);
    }

    if (!File.Exists(fullPath))
    {
        _logger?.LogError("Texture not found: {Path}", fullPath);
        throw new FileNotFoundException($"Texture not found: {relativePath}", fullPath);
    }

    using var stream = File.OpenRead(fullPath);
    var texture = Texture2D.FromStream(_graphicsDevice, stream);
    _textures[id] = texture;

    _logger?.LogDebug("Loaded texture {Id} from {Path}", id, fullPath);
}
```

**Estimated Time:** 2 hours

---

### Task 2.4: Update FontLoader

**File:** `MonoBallFramework.Game/Engine/UI/Utilities/FontLoader.cs`

Replace hardcoded paths:

```csharp
public sealed class FontLoader
{
    private readonly IContentProvider _contentProvider;
    private readonly ILogger<FontLoader> _logger;

    // Font type constants
    public const string GameFont = "pokemon.ttf";
    public const string DebugFont = "0xProtoNerdFontMono-Regular.ttf";

    public FontLoader(
        IContentProvider contentProvider,
        ILogger<FontLoader> logger)
    {
        _contentProvider = contentProvider;
        _logger = logger;
    }

    public string? ResolveFontPath(string fontFileName)
    {
        string? path = _contentProvider.ResolveContentPath("Fonts", fontFileName);

        if (path == null)
        {
            _logger.LogWarning("Font not found: {Font}", fontFileName);
        }

        return path;
    }

    public string GetGameFontPath() => ResolveFontPath(GameFont)
        ?? throw new FileNotFoundException($"Game font not found: {GameFont}");

    public string GetDebugFontPath() => ResolveFontPath(DebugFont)
        ?? throw new FileNotFoundException($"Debug font not found: {DebugFont}");
}
```

**Estimated Time:** 1 hour

---

### Task 2.5: Update Initialization Pipeline

**File:** `MonoBallFramework.Game/Initialization/Pipeline/Steps/LoadGameDataStep.cs`

Ensure proper initialization order:

```csharp
protected override async Task ExecuteStepAsync(
    InitializationContext context,
    LoadingProgress progress,
    CancellationToken cancellationToken)
{
    var logger = context.LoggerFactory.CreateLogger<LoadGameDataStep>();

    // 1. Get ModLoader (already loaded base game + user mods)
    var modLoader = context.Services.GetRequiredService<ModLoader>();

    // 2. Create ContentProvider with loaded mods
    var contentProvider = context.Services.GetRequiredService<IContentProvider>();

    // 3. Load game data using ContentProvider
    var gameDataLoader = context.Services.GetRequiredService<GameDataLoader>();
    await gameDataLoader.LoadAllAsync(cancellationToken);

    logger.LogInformation("Game data loaded successfully");
}
```

**Estimated Time:** 1 hour

---

### Phase 2 Checklist

- [ ] Update ModLoader with LoadBaseGameAsync()
- [ ] Update GameDataLoader to use IContentProvider
- [ ] Update AssetManager to use IContentProvider
- [ ] Update FontLoader to use IContentProvider
- [ ] Update LoadGameDataStep
- [ ] Update DI registration order
- [ ] Write integration tests
- [ ] Verify existing functionality still works

---

## 5. Phase 3: Content Migration (Week 3)

### Task 3.1: Create Test Mod Structure

Create a minimal test mod to validate the system:

**Directory:** `Mods/test-override/`

```
Mods/
└── test-override/
    ├── mod.json
    └── content/
        └── Definitions/
            └── TileBehaviors/
                └── ice.json  (overrides base ice behavior)
```

**File:** `Mods/test-override/mod.json`

```json
{
  "id": "test.override",
  "name": "Test Override Mod",
  "author": "Test",
  "version": "1.0.0",
  "description": "Tests content override functionality",
  "priority": 100,
  "contentFolders": {
    "Definitions": "content/Definitions"
  },
  "dependencies": ["base:pokesharp-core >= 1.0.0"]
}
```

**Estimated Time:** 1 hour

---

### Task 3.2: Validate Content Override

Write test to verify mod content overrides base:

```csharp
[Fact]
public void ContentProvider_ModOverridesBase_ReturnsModPath()
{
    // Arrange
    var modLoader = CreateModLoaderWithTestMod();
    var contentProvider = new ContentProvider(modLoader, _logger, _options);

    // Act
    string? path = contentProvider.ResolveContentPath(
        "Definitions",
        "TileBehaviors/ice.json");

    // Assert
    Assert.NotNull(path);
    Assert.Contains("test-override", path);
    Assert.DoesNotContain("Assets", path);
}

[Fact]
public void ContentProvider_NoModOverride_ReturnsBasePath()
{
    // Arrange
    var modLoader = CreateModLoaderWithTestMod();
    var contentProvider = new ContentProvider(modLoader, _logger, _options);

    // Act - request content that mod doesn't override
    string? path = contentProvider.ResolveContentPath(
        "Definitions",
        "TileBehaviors/normal.json");

    // Assert
    Assert.NotNull(path);
    Assert.Contains("Assets", path);
}
```

**Estimated Time:** 2 hours

---

### Task 3.3: Performance Benchmarking

Create benchmarks to ensure performance targets:

```csharp
[MemoryDiagnoser]
public class ContentProviderBenchmarks
{
    private ContentProvider _provider = null!;

    [GlobalSetup]
    public void Setup()
    {
        var modLoader = CreateModLoaderWith10Mods();
        _provider = new ContentProvider(modLoader, NullLogger.Instance, DefaultOptions);

        // Warm up cache
        for (int i = 0; i < 100; i++)
        {
            _provider.ResolveContentPath("Definitions", $"test{i}.json");
        }
    }

    [Benchmark]
    public string? ResolveContentPath_CacheHit()
    {
        return _provider.ResolveContentPath("Definitions", "test0.json");
    }

    [Benchmark]
    public string? ResolveContentPath_CacheMiss()
    {
        _provider.InvalidateCache();
        return _provider.ResolveContentPath("Definitions", "test0.json");
    }

    [Benchmark]
    public IEnumerable<string> GetAllContentPaths()
    {
        return _provider.GetAllContentPaths("Definitions", "*.json").ToList();
    }
}
```

**Target Metrics:**
- Cache hit: < 0.1ms
- Cache miss: < 10ms
- GetAllContentPaths (1000 files): < 100ms

**Estimated Time:** 2 hours

---

### Phase 3 Checklist

- [ ] Create test mod structure
- [ ] Validate content override works
- [ ] Write performance benchmarks
- [ ] Ensure cache hit rate > 90%
- [ ] Test with 0, 5, 10 mods loaded
- [ ] Memory profiling (< 10MB overhead)
- [ ] Security audit (path traversal tests)
- [ ] Fix any issues found

---

## 6. Phase 4: Polish & Release (Week 4)

### Task 4.1: Remove Hardcoded Paths

Search and replace all hardcoded paths:

```bash
# Find all hardcoded font paths
grep -r "Assets/Fonts" --include="*.cs" .

# Find all hardcoded asset paths
grep -r '"Assets/' --include="*.cs" .
```

**Files to update:**
- `Engine/UI/Utilities/FontLoader.cs` - ✅ Done in Phase 2
- `Engine/Scenes/Scenes/LoadingScene.cs`
- `Engine/Scenes/Scenes/MapPopupScene.cs`
- `Engine/Scenes/Scenes/IntroScene.cs`
- `Infrastructure/Diagnostics/PerformanceOverlay.cs`

**Estimated Time:** 4 hours

---

### Task 4.2: Update Default Fallbacks

Make theme defaults configurable:

**File:** `MonoBallFramework.Game/Engine/Rendering/Popups/PopupRegistryOptions.cs`

```csharp
public sealed class PopupRegistryOptions
{
    public string DefaultBackgroundId { get; set; } = "base:popup:background/stone";
    public string DefaultOutlineId { get; set; } = "base:popup:outline/stone_outline";
    public bool UseFirstAvailableIfDefaultMissing { get; set; } = true;
}
```

**Estimated Time:** 2 hours

---

### Task 4.3: Documentation

Create mod developer documentation:

**File:** `docs/modding-guide.md`

```markdown
# PokeSharp Modding Guide

## Creating a Mod

1. Create a folder in `Mods/` with your mod ID
2. Create `mod.json` manifest
3. Add content folders
4. Test with game

## mod.json Reference

{
  "id": "yourname.modname",       // Unique ID
  "name": "Display Name",         // Shown in mod list
  "version": "1.0.0",            // Semantic version
  "priority": 100,               // Higher = loads first
  "contentFolders": {            // Map content types to folders
    "Definitions": "content/Definitions",
    "Graphics": "content/Graphics"
  },
  "dependencies": [              // Required mods
    "base:pokesharp-core >= 1.0.0"
  ]
}

## Content Override

To override base game content, create a file with the same relative path:

Base: Assets/Definitions/TileBehaviors/ice.json
Mod:  Mods/mymod/content/Definitions/TileBehaviors/ice.json

The mod version will be used (higher priority wins).
```

**Estimated Time:** 4 hours

---

### Phase 4 Checklist

- [ ] Remove all hardcoded paths
- [ ] Make fallback defaults configurable
- [ ] Write modding guide documentation
- [ ] Update CHANGELOG
- [ ] Final testing pass
- [ ] Performance verification
- [ ] Release!

---

## 7. Code Reference

### Files to Create

| File | Purpose |
|------|---------|
| `Engine/Content/IContentProvider.cs` | Interface definition |
| `Engine/Content/ContentProvider.cs` | Implementation |
| `Engine/Content/ContentProviderOptions.cs` | Configuration |
| `Engine/Content/ContentExtensions.cs` | DI registration |
| `Engine/Content/LruCache.cs` | Caching utility |
| `Assets/mod.json` | Base game manifest |

### Files to Modify

| File | Changes |
|------|---------|
| `Engine/Core/Modding/ModLoader.cs` | Add LoadBaseGameAsync() |
| `GameData/Loading/GameDataLoader.cs` | Use IContentProvider |
| `Engine/Rendering/Assets/AssetManager.cs` | Use IContentProvider |
| `Engine/UI/Utilities/FontLoader.cs` | Use IContentProvider |
| `Initialization/Pipeline/Steps/LoadGameDataStep.cs` | Update init order |
| `Engine/Scenes/Scenes/LoadingScene.cs` | Remove hardcoded paths |
| `Engine/Scenes/Scenes/MapPopupScene.cs` | Remove hardcoded paths |
| `Engine/Rendering/Popups/PopupRegistry.cs` | Configurable defaults |

---

## 8. Testing Checklist

### Unit Tests

- [ ] ContentProvider.ResolveContentPath - base game only
- [ ] ContentProvider.ResolveContentPath - mod override
- [ ] ContentProvider.ResolveContentPath - multiple mods (priority)
- [ ] ContentProvider.ResolveContentPath - cache hit
- [ ] ContentProvider.ResolveContentPath - cache miss
- [ ] ContentProvider.GetAllContentPaths - no duplicates
- [ ] ContentProvider.ContentExists - true/false cases
- [ ] ContentProvider.GetContentSource - correct source
- [ ] ContentProvider.InvalidateCache - clears entries
- [ ] LruCache - eviction when full
- [ ] LruCache - thread safety
- [ ] Path traversal prevention

### Integration Tests

- [ ] Game starts with no mods
- [ ] Game starts with test mod
- [ ] Mod content overrides base content
- [ ] Multiple mods load in priority order
- [ ] Hot reload works (mod unload/reload)
- [ ] Missing content handled gracefully

### Manual Tests

- [ ] Play through intro scene
- [ ] Enter a map
- [ ] Trigger tile behaviors (ice, jump)
- [ ] Open map popup
- [ ] Load/save game
- [ ] Performance acceptable

---

## 9. Success Criteria

### Must Have (P0)

- [x] IContentProvider interface implemented
- [ ] ContentProvider with LRU cache implemented
- [ ] GameDataLoader uses ContentProvider
- [ ] AssetManager uses ContentProvider
- [ ] Base game loads as mod
- [ ] Mods can override base content
- [ ] All existing tests pass
- [ ] No performance regression > 10%

### Should Have (P1)

- [ ] Font paths use ContentProvider
- [ ] All hardcoded paths removed
- [ ] Hot reload works
- [ ] Modding documentation complete
- [ ] Cache hit rate > 90%

### Nice to Have (P2)

- [ ] Theme fallbacks configurable
- [ ] Content source tracking in UI
- [ ] Mod conflict detection
- [ ] Performance dashboard

---

## Quick Start

```bash
# 1. Create the Content directory
mkdir -p MonoBallFramework.Game/Engine/Content

# 2. Create files in order:
#    - IContentProvider.cs
#    - ContentProviderOptions.cs
#    - LruCache.cs
#    - ContentProvider.cs
#    - ContentExtensions.cs

# 3. Create base game manifest
#    - Assets/mod.json

# 4. Run tests
dotnet test

# 5. Run game and verify
dotnet run --project MonoBallFramework.Game
```

---

*Document Version: 1.0*
*Generated: 2025-12-14*
*Based on Hive Mind Analysis (4 workers, 87 issues, 10 SOLID violations)*
