# Architecture Design: Base Game Content as Mod

## Executive Summary

This document defines the architecture for transforming PokeSharp's base game content into a mod-based system where almost all content in `MonoBallFramework.Game/Assets` becomes a mod. The base game content will follow the same patterns as user mods, enabling unified content resolution, hot-reload support, and seamless mod overrides.

**Status**: Design Phase
**Author**: System Architecture Designer
**Date**: 2025-12-14

---

## 1. Current State Analysis

### 1.1 Existing Architecture

#### Content Loading System
- **AssetManager**: Loads textures and fonts from hardcoded paths relative to `AssetRoot`
- **GameDataLoader**: Loads JSON definitions from `Assets/Definitions/` into EF Core
- **TypeRegistry<T>**: Generic registry for type definitions loaded from JSON
- **ModLoader**: Loads mods from `/Mods/{modId}/` with mod.json manifests

#### Current Content Structure
```
MonoBallFramework.Game/
├── Assets/
│   ├── Audio/              # Music and SFX files
│   ├── Definitions/        # JSON type definitions
│   │   ├── Audio/
│   │   ├── Behaviors/
│   │   ├── Maps/
│   │   ├── Sprites/
│   │   ├── TextWindow/
│   │   ├── TileBehaviors/
│   │   └── Worlds/
│   ├── Fonts/              # TTF font files
│   ├── Graphics/           # PNG textures
│   │   ├── Maps/
│   │   └── Sprites/
│   ├── Scripts/            # CSX behavior scripts
│   │   ├── Behaviors/
│   │   └── TileBehaviors/
│   ├── Tiled/              # Tiled map files
│   └── Tilesets/           # Tileset images and JSON
└── Mods/
    └── {modId}/
        ├── mod.json        # Mod manifest
        ├── patches/        # JSON Patch files
        ├── scripts/        # CSX scripts
        └── content/        # New content folders
```

#### Key Challenges
1. **Hardcoded Paths**: AssetManager loads from paths relative to game root
2. **No Content Priority**: Base game content cannot be overridden by mods
3. **Split Loading Logic**: Different systems for base content vs. mod content
4. **No Unified Resolution**: Content lookup doesn't check mod hierarchies

---

## 2. Design Goals

### 2.1 Primary Objectives

1. **Core Game as "Base Mod"**
   - Base game content follows mod structure
   - Special mod ID: `base:pokesharp-core` (highest priority by default)
   - Uses same manifest schema as user mods

2. **Content Priority System**
   - Mods can override/extend base content
   - Configurable priority chain
   - Transparent fallback to base content

3. **Unified Asset Resolution**
   - Single lookup mechanism for all content
   - Priority-based search across mods
   - Efficient caching of resolved paths

4. **Backward Compatibility**
   - Existing game code continues to work
   - Gradual migration path
   - No breaking changes to public APIs

5. **Hot Reload Support**
   - Content changes without restart
   - Mod enable/disable at runtime
   - Development-friendly workflow

### 2.2 Non-Functional Requirements

- **Performance**: O(1) cached lookups after initial resolution
- **Memory**: Minimal overhead from content provider layer
- **Maintainability**: Clean separation of concerns
- **Extensibility**: Easy to add new content types

---

## 3. Architectural Components

### 3.1 Content Provider Architecture

#### IContentProvider Interface
```csharp
/// <summary>
/// Unified content provider with mod-aware resolution.
/// Replaces hardcoded path lookups with priority-based search.
/// </summary>
public interface IContentProvider
{
    /// <summary>
    /// Resolves a content path by searching mods in priority order.
    /// Returns the absolute path to the highest-priority version.
    /// </summary>
    string? ResolveContentPath(string contentType, string relativePath);

    /// <summary>
    /// Gets all content paths for a type (e.g., all sprites across all mods).
    /// </summary>
    IEnumerable<string> GetAllContentPaths(string contentType, string pattern = "*.json");

    /// <summary>
    /// Checks if content exists in any loaded mod.
    /// </summary>
    bool ContentExists(string contentType, string relativePath);

    /// <summary>
    /// Gets the mod ID that provides specific content.
    /// </summary>
    string? GetContentSource(string contentType, string relativePath);
}
```

#### ContentProvider Implementation
```csharp
public sealed class ContentProvider : IContentProvider
{
    private readonly ModLoader _modLoader;
    private readonly ILogger<ContentProvider> _logger;
    private readonly string _baseGameRoot; // Assets/ directory

    // LRU cache: (contentType, relativePath) -> absolute path
    private readonly LruCache<(string, string), string> _resolvedPaths;

    // Content type mappings to folder names
    private static readonly Dictionary<string, string> ContentTypeFolders = new()
    {
        ["Definitions"] = "Definitions",
        ["Graphics"] = "Graphics",
        ["Audio"] = "Audio",
        ["Scripts"] = "Scripts",
        ["Fonts"] = "Fonts",
        ["Tiled"] = "Tiled",
        ["Tilesets"] = "Tilesets"
    };

    public string? ResolveContentPath(string contentType, string relativePath)
    {
        // Check cache first (O(1) lookup)
        if (_resolvedPaths.TryGetValue((contentType, relativePath), out string? cached))
            return cached;

        // Search mods in priority order (high to low)
        foreach (var mod in GetModsByPriority())
        {
            string? path = TryResolveInMod(mod, contentType, relativePath);
            if (path != null)
            {
                _resolvedPaths.AddOrUpdate((contentType, relativePath), path);
                _logger.LogDebug("Resolved {Type}/{Path} -> {Mod}",
                    contentType, relativePath, mod.Id);
                return path;
            }
        }

        // Fallback to base game content
        string? basePath = TryResolveInBaseGame(contentType, relativePath);
        if (basePath != null)
        {
            _resolvedPaths.AddOrUpdate((contentType, relativePath), basePath);
        }

        return basePath;
    }

    private IEnumerable<ModManifest> GetModsByPriority()
    {
        // Return mods sorted by priority (higher = loads first)
        return _modLoader.LoadedMods.Values
            .OrderByDescending(m => m.Priority)
            .ThenBy(m => m.Id);
    }

    private string? TryResolveInMod(ModManifest mod, string contentType, string relativePath)
    {
        // Check if mod declares this content type in contentFolders
        if (!mod.ContentFolders.TryGetValue(contentType, out string? contentFolder))
            return null;

        string fullPath = Path.Combine(mod.DirectoryPath, contentFolder, relativePath);
        return File.Exists(fullPath) ? fullPath : null;
    }

    private string? TryResolveInBaseGame(string contentType, string relativePath)
    {
        if (!ContentTypeFolders.TryGetValue(contentType, out string? folder))
            return null;

        string fullPath = Path.Combine(_baseGameRoot, folder, relativePath);
        return File.Exists(fullPath) ? fullPath : null;
    }
}
```

### 3.2 Base Game Mod Structure

#### Base Mod Manifest
Location: `MonoBallFramework.Game/Assets/mod.json`

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

### 3.3 Enhanced ModManifest Schema

**Changes to ModManifest.cs**:
- Add `isBaseGame: bool` field (automatically set for `base:*` IDs)
- Support inheritance: mods can declare `extendsBase: true` to merge with base content
- Add `contentPriority: int` per content type for fine-grained control

**No Breaking Changes**: All new fields are optional with sensible defaults.

### 3.4 Asset Resolution Chain

```
User Request: LoadTexture("sprites/player_walk.png")
    ↓
ContentProvider.ResolveContentPath("Graphics", "sprites/player_walk.png")
    ↓
    [Check Cache] → Cache Hit? → Return cached path
    ↓
    [Priority Search]
    1. Mod: awesome-sprites (priority: 2000) → Found? → Cache & Return
    2. Mod: enhanced-graphics (priority: 1500) → Found? → Cache & Return
    3. Base: pokesharp-core (priority: 1000) → Found? → Cache & Return
    ↓
    [Not Found] → Log warning & return null
    ↓
AssetManager.LoadTexture(id, resolvedPath)
```

---

## 4. Integration Points

### 4.1 AssetManager Changes

**Current**:
```csharp
public void LoadTexture(string id, string relativePath)
{
    string fullPath = Path.Combine(AssetRoot, relativePath);
    // Load texture...
}
```

**Enhanced**:
```csharp
private readonly IContentProvider _contentProvider;

public void LoadTexture(string id, string relativePath)
{
    // Resolve path through mod system
    string? fullPath = _contentProvider.ResolveContentPath("Graphics", relativePath);

    if (fullPath == null)
    {
        // Fallback to legacy path for backward compatibility
        fullPath = Path.Combine(AssetRoot, relativePath);
    }

    if (!File.Exists(fullPath))
    {
        throw new FileNotFoundException($"Texture not found: {relativePath}");
    }

    // Load texture...
}
```

### 4.2 GameDataLoader Changes

**Current**:
```csharp
public async Task LoadAllAsync(string dataPath, CancellationToken ct)
{
    string mapsPath = Path.Combine(dataPath, "Maps", "Regions");
    await LoadMapEntitysAsync(mapsPath, ct);
    // ...
}
```

**Enhanced**:
```csharp
private readonly IContentProvider _contentProvider;

public async Task LoadAllAsync(string dataPath, CancellationToken ct)
{
    // Load from all mods in priority order, then base game
    var allMapFiles = _contentProvider.GetAllContentPaths("Definitions", "Maps/Regions/*.json");

    foreach (string file in allMapFiles)
    {
        await LoadMapEntityAsync(file, ct);
    }
    // ...
}
```

### 4.3 ModLoader Integration

**Initialization Sequence**:
1. Create ContentProvider with base game root path
2. ModLoader.LoadModsAsync()
   - Loads user mods from `/Mods/`
   - Registers them with ContentProvider
3. GameDataLoader.LoadAllAsync()
   - Uses ContentProvider for all content resolution
4. AssetManager operations use ContentProvider

**New Method in ModLoader**:
```csharp
/// <summary>
/// Loads the base game as a special mod.
/// Called before loading user mods.
/// </summary>
public async Task LoadBaseGameModAsync(string baseGameRoot)
{
    string manifestPath = Path.Combine(baseGameRoot, "mod.json");

    if (!File.Exists(manifestPath))
    {
        _logger.LogWarning("Base game mod.json not found. Creating default...");
        await CreateDefaultBaseManifestAsync(manifestPath);
    }

    ModManifest? manifest = ParseManifest(manifestPath, baseGameRoot);
    if (manifest != null)
    {
        _loadedMods[manifest.Id] = manifest;
        _logger.LogInformation("Loaded base game mod: {Id}", manifest.Id);
    }
}
```

---

## 5. Content Override Scenarios

### 5.1 Texture Override Example

**Base Game** (`base:pokesharp-core`):
```
Assets/Graphics/Sprites/player_walk.png
```

**User Mod** (`user:custom-sprites`, priority: 2000):
```
Mods/custom-sprites/
├── mod.json
└── Graphics/
    └── Sprites/
        └── player_walk.png  # Overrides base texture
```

**Resolution**: ContentProvider returns mod version due to higher priority.

### 5.2 Definition Merge Example

**Base Game**:
```json
// Assets/Definitions/TileBehaviors/ice.json
{
  "typeId": "ice",
  "flags": "ForcesMovement",
  "behaviorScript": "TileBehaviors/ice.csx"
}
```

**User Mod** (using JSON Patch):
```json
// Mods/better-ice/patches/ice_behavior.patch.json
{
  "target": "Definitions/TileBehaviors/ice.json",
  "operations": [
    {
      "op": "replace",
      "path": "/flags",
      "value": "ForcesMovement, DisablesRunning"
    }
  ]
}
```

**Result**: PatchApplicator merges changes after base content loads.

### 5.3 Content Addition Example

**User Mod** adds new content:
```
Mods/new-region/
├── mod.json
└── Definitions/
    └── Maps/
        └── Regions/
            └── johto.json  # New region, doesn't exist in base
```

**Resolution**: ContentProvider finds it when scanning all content paths.

---

## 6. Migration Path

### Phase 1: Foundation (Week 1)
1. Create `IContentProvider` interface
2. Implement `ContentProvider` with cache
3. Add base game `mod.json` manifest
4. Unit tests for content resolution

### Phase 2: Integration (Week 2)
1. Integrate ContentProvider into AssetManager
2. Update GameDataLoader to use ContentProvider
3. Add LoadBaseGameModAsync to ModLoader
4. Update initialization sequence

### Phase 3: Testing & Refinement (Week 3)
1. Test mod override scenarios
2. Test hot-reload with mod enable/disable
3. Performance profiling of resolution cache
4. Documentation and examples

### Phase 4: Deprecation (Week 4)
1. Mark old hardcoded path methods as obsolete
2. Provide migration guide for mod developers
3. Update all internal code to use ContentProvider
4. Remove deprecated code after deprecation period

---

## 7. Architecture Decision Records

### ADR-001: Use Composition Over Inheritance for ContentProvider

**Context**: Need to integrate mod-aware resolution into AssetManager and GameDataLoader.

**Decision**: Pass IContentProvider as dependency rather than subclassing existing loaders.

**Rationale**:
- Non-invasive: minimal changes to existing code
- Testable: can mock IContentProvider
- Flexible: easy to swap implementations
- Single Responsibility: each class has clear purpose

**Consequences**:
- Requires constructor changes for AssetManager and GameDataLoader
- Dependency injection configuration updates needed
- Clear separation between content resolution and content loading

### ADR-002: LRU Cache for Resolved Paths

**Context**: Content resolution involves file system checks across multiple mods.

**Decision**: Cache resolved paths with LRU eviction policy.

**Rationale**:
- File system operations are slow (especially on WSL)
- Content paths rarely change during gameplay
- LRU prevents unbounded memory growth
- Same pattern already used in AssetManager for textures

**Consequences**:
- O(1) lookups after initial resolution
- Small memory overhead (~10KB for 1000 cached paths)
- Cache invalidation needed for hot-reload
- Slightly more complex implementation

### ADR-003: Base Game as Special Mod with ID Prefix

**Context**: Need to distinguish base game from user mods.

**Decision**: Use mod ID prefix `base:` for base game content (e.g., `base:pokesharp-core`).

**Rationale**:
- Follows existing pattern (mods use `username:modname`)
- Easy to identify base game in logs and debugging
- Prevents naming conflicts with user mods
- Can have multiple base game modules if needed (DLC, expansions)

**Consequences**:
- ID validation must allow `base:` prefix
- Priority system ensures base game loads with correct precedence
- Clear semantic distinction in codebase

### ADR-004: Content Type Folder Mapping

**Context**: Different content types stored in different folder structures.

**Decision**: Use dictionary to map content type names to folder paths.

**Rationale**:
- Centralized configuration
- Easy to add new content types
- Supports both flat and nested structures
- Backwards compatible with existing folder layout

**Consequences**:
- Content types must be pre-registered
- Folder structure consistency enforced
- Dynamic content types require code changes

---

## 8. API Design

### 8.1 Public Interfaces

```csharp
namespace MonoBallFramework.Game.Engine.Content;

/// <summary>
/// Provides unified content resolution across base game and mods.
/// </summary>
public interface IContentProvider
{
    string? ResolveContentPath(string contentType, string relativePath);
    IEnumerable<string> GetAllContentPaths(string contentType, string pattern = "*.json");
    bool ContentExists(string contentType, string relativePath);
    string? GetContentSource(string contentType, string relativePath);
    void InvalidateCache(string? contentType = null);
}

/// <summary>
/// Content resolution result with metadata.
/// </summary>
public sealed class ContentResolution
{
    public required string AbsolutePath { get; init; }
    public required string SourceModId { get; init; }
    public required int Priority { get; init; }
    public required bool IsBaseGame { get; init; }
}
```

### 8.2 Configuration

```csharp
public sealed class ContentProviderOptions
{
    /// <summary>
    /// Root directory for base game content (default: "Assets")
    /// </summary>
    public string BaseGameRoot { get; set; } = "Assets";

    /// <summary>
    /// Enable content resolution caching (default: true)
    /// </summary>
    public bool EnableCache { get; set; } = true;

    /// <summary>
    /// LRU cache size in entries (default: 1000)
    /// </summary>
    public int CacheSize { get; set; } = 1000;

    /// <summary>
    /// Log resolved paths for debugging (default: false)
    /// </summary>
    public bool VerboseLogging { get; set; } = false;
}
```

---

## 9. Testing Strategy

### 9.1 Unit Tests

```csharp
[Fact]
public void ResolveContentPath_BaseGameOnly_ReturnsBasePath()
{
    // Arrange
    var provider = CreateContentProvider(baseGameOnly: true);

    // Act
    string? path = provider.ResolveContentPath("Graphics", "sprites/player.png");

    // Assert
    Assert.NotNull(path);
    Assert.Contains("Assets/Graphics/sprites/player.png", path);
}

[Fact]
public void ResolveContentPath_ModOverridesBase_ReturnsModPath()
{
    // Arrange
    var provider = CreateContentProviderWithMod(
        modId: "user:custom-sprites",
        priority: 2000,
        contentFolders: new() { ["Graphics"] = "Graphics" }
    );

    // Act
    string? path = provider.ResolveContentPath("Graphics", "sprites/player.png");

    // Assert
    Assert.Contains("Mods/custom-sprites/Graphics/sprites/player.png", path);
}

[Fact]
public void ResolveContentPath_CacheHit_DoesNotAccessFileSystem()
{
    // Arrange
    var provider = CreateContentProvider();
    provider.ResolveContentPath("Graphics", "sprites/player.png"); // Prime cache

    // Act
    var stopwatch = Stopwatch.StartNew();
    string? path = provider.ResolveContentPath("Graphics", "sprites/player.png");
    stopwatch.Stop();

    // Assert
    Assert.True(stopwatch.ElapsedMilliseconds < 1, "Cache lookup should be <1ms");
}
```

### 9.2 Integration Tests

```csharp
[Fact]
public async Task AssetManager_LoadTextureWithMod_UsesModVersion()
{
    // Arrange
    var (assetManager, modLoader) = CreateIntegrationTestContext();
    await modLoader.LoadModsAsync();

    // Act
    assetManager.LoadTexture("player", "sprites/player.png");

    // Assert
    var texture = assetManager.GetTexture("player");
    Assert.NotNull(texture);
    // Verify texture is from mod, not base game
}
```

### 9.3 Performance Tests

```csharp
[Fact]
public void ContentProvider_1000Resolutions_CompletesUnder100ms()
{
    // Arrange
    var provider = CreateContentProviderWithManyMods(count: 10);

    // Act
    var stopwatch = Stopwatch.StartNew();
    for (int i = 0; i < 1000; i++)
    {
        provider.ResolveContentPath("Graphics", $"sprite_{i % 100}.png");
    }
    stopwatch.Stop();

    // Assert
    Assert.True(stopwatch.ElapsedMilliseconds < 100);
}
```

---

## 10. Security Considerations

### 10.1 Path Traversal Prevention

```csharp
private static string SanitizeRelativePath(string relativePath)
{
    // Prevent directory traversal attacks
    if (relativePath.Contains("..") ||
        Path.IsPathRooted(relativePath) ||
        relativePath.Contains("~"))
    {
        throw new SecurityException($"Invalid content path: {relativePath}");
    }

    return relativePath.Replace('/', Path.DirectorySeparatorChar);
}
```

### 10.2 Mod Sandboxing

- Mods can only access their own content folders
- No direct file system access outside mod directory
- ContentProvider enforces path boundaries
- Logging of all content resolution attempts

---

## 11. Performance Characteristics

### 11.1 Complexity Analysis

| Operation | Cold Cache | Warm Cache | Notes |
|-----------|-----------|-----------|-------|
| ResolveContentPath | O(n*m) | O(1) | n=mods, m=file check |
| GetAllContentPaths | O(n*m) | O(n*m) | Uncached (dynamic) |
| ContentExists | O(n*m) | O(1) | Uses ResolveContentPath |
| InvalidateCache | O(1) | O(1) | Dictionary clear |

### 11.2 Memory Footprint

```
ContentProvider Instance:
- ModLoader reference: 8 bytes (pointer)
- LRU Cache (1000 entries): ~50KB
  - Key: (string, string) → ~40 bytes
  - Value: string → ~100 bytes average path length
  - Total: ~140 bytes * 1000 = 140KB
- ContentTypeFolders: ~1KB (static)
- Logger: 8 bytes (pointer)

Total: ~142KB per instance
```

### 11.3 Benchmark Targets

- Cold resolution: <10ms for 10 mods
- Cached resolution: <0.1ms (single dictionary lookup)
- Cache invalidation: <1ms
- Startup time impact: <100ms for 50 mods

---

## 12. Open Questions & Future Work

### 12.1 Open Questions

1. **Question**: Should we support content removal (blacklisting base game content)?
   - **Proposed**: Add `contentBlacklist` to mod manifest
   - **Concern**: May break game logic expecting certain assets

2. **Question**: How to handle circular dependencies between mods?
   - **Proposed**: Detect and reject during dependency resolution
   - **Alternative**: Allow but log warnings

3. **Question**: Should content type folders be configurable per-mod?
   - **Proposed**: Yes, via contentFolders in manifest
   - **Already Supported**: Current design allows this

### 12.2 Future Enhancements

1. **Content Versioning**
   - Track content schema versions
   - Auto-migration for old content formats
   - Compatibility warnings

2. **Virtual File System**
   - In-memory content overlay
   - Hot-reload without file system changes
   - Testing and development workflows

3. **Content Compression**
   - ZIP-based mod packages
   - Reduced disk space
   - Faster distribution

4. **Content Validation**
   - Schema validation for JSON definitions
   - Asset integrity checks (checksums)
   - Mod compatibility validation

5. **Content Analytics**
   - Usage tracking per content item
   - Performance metrics per mod
   - Automatic optimization suggestions

---

## 13. Summary & Recommendations

### 13.1 Architecture Strengths

1. **Unified Content Resolution**: Single source of truth for all content lookups
2. **Backward Compatible**: Existing code continues to work with minimal changes
3. **Performance**: LRU caching ensures O(1) lookups after warmup
4. **Flexibility**: Mods can override, extend, or add new content
5. **Clean Separation**: ContentProvider decouples resolution from loading

### 13.2 Implementation Recommendations

1. **Start Small**: Implement ContentProvider and integrate with AssetManager first
2. **Gradual Migration**: Move one content type at a time to new system
3. **Comprehensive Testing**: Unit tests for all edge cases before integration
4. **Documentation**: Update mod developer guide with new patterns
5. **Monitor Performance**: Profile resolution performance in real workloads

### 13.3 Risk Mitigation

| Risk | Impact | Mitigation |
|------|--------|-----------|
| Performance regression | High | LRU cache, benchmarking |
| Breaking changes | Medium | Backward compatibility layer |
| Complex mod scenarios | Medium | Comprehensive testing |
| Cache invalidation bugs | Low | Conservative invalidation strategy |
| Security vulnerabilities | High | Path sanitization, sandboxing |

### 13.4 Success Criteria

- [ ] All base game content loadable as a mod
- [ ] Mods can override any base content
- [ ] No performance regression vs. current system
- [ ] Hot-reload works for all content types
- [ ] Comprehensive unit test coverage (>90%)
- [ ] Documentation complete for mod developers

---

## Appendices

### Appendix A: File Structure Comparison

**Before**:
```
MonoBallFramework.Game/
├── Assets/              # Hardcoded base game content
│   ├── Graphics/
│   ├── Definitions/
│   └── Scripts/
└── Mods/               # User mods (different structure)
    └── awesome-mod/
        ├── mod.json
        └── content/
```

**After**:
```
MonoBallFramework.Game/
├── Assets/              # Base game AS A MOD
│   ├── mod.json        # NEW: Base game manifest
│   ├── Graphics/       # Unchanged structure
│   ├── Definitions/    # Unchanged structure
│   └── Scripts/        # Unchanged structure
└── Mods/               # User mods (same structure as base)
    └── awesome-mod/
        ├── mod.json
        ├── Graphics/   # Can override base
        └── Definitions/
```

### Appendix B: Dependency Injection Configuration

```csharp
// Startup.cs or DI container configuration
services.AddSingleton<IContentProvider, ContentProvider>(sp =>
{
    var modLoader = sp.GetRequiredService<ModLoader>();
    var logger = sp.GetRequiredService<ILogger<ContentProvider>>();
    var options = sp.GetRequiredService<IOptions<ContentProviderOptions>>();

    return new ContentProvider(modLoader, logger, options.Value);
});

services.AddSingleton<AssetManager>(sp =>
{
    var graphicsDevice = sp.GetRequiredService<GraphicsDevice>();
    var contentProvider = sp.GetRequiredService<IContentProvider>();
    var logger = sp.GetRequiredService<ILogger<AssetManager>>();

    return new AssetManager(graphicsDevice, contentProvider, logger);
});
```

### Appendix C: Example Mod Override

**Scenario**: User wants to replace all ice tile graphics with custom versions.

**Mod Structure**:
```
Mods/better-ice/
├── mod.json
├── Graphics/
│   └── Tilesets/
│       └── ice_tiles.png      # Overrides base
└── Definitions/
    └── TileBehaviors/
        └── ice.json            # Can also override behavior
```

**mod.json**:
```json
{
  "id": "user:better-ice",
  "name": "Better Ice Graphics",
  "version": "1.0.0",
  "priority": 1500,
  "contentFolders": {
    "Graphics": "Graphics",
    "Definitions": "Definitions"
  },
  "dependencies": [
    "base:pokesharp-core >= 1.0.0"
  ]
}
```

**Result**: ContentProvider resolves `Tilesets/ice_tiles.png` to mod version.

---

## Document Metadata

- **Version**: 1.0
- **Last Updated**: 2025-12-14
- **Review Status**: Pending Technical Review
- **Reviewers**: TBD
- **Next Review**: After Phase 1 Implementation

---

## References

1. ModLoader.cs - `/MonoBallFramework.Game/Engine/Core/Modding/ModLoader.cs`
2. ModManifest.cs - `/MonoBallFramework.Game/Engine/Core/Modding/ModManifest.cs`
3. AssetManager.cs - `/MonoBallFramework.Game/Engine/Rendering/Assets/AssetManager.cs`
4. GameDataLoader.cs - `/MonoBallFramework.Game/GameData/Loading/GameDataLoader.cs`
5. TypeRegistry.cs - `/MonoBallFramework.Game/Engine/Core/Types/TypeRegistry.cs`
6. PatchApplicator.cs - `/MonoBallFramework.Game/Engine/Core/Modding/PatchApplicator.cs`
