# Content Resolution Sequence Diagrams

## Scenario 1: Texture Loading with Mod Override

```
Player          Game          AssetManager     ContentProvider    ModLoader      FileSystem
  │               │                 │                 │               │              │
  │ Move to       │                 │                 │               │              │
  │ new area      │                 │                 │               │              │
  │───────────────▶                 │                 │               │              │
  │               │                 │                 │               │              │
  │               │ LoadTexture     │                 │               │              │
  │               │ ("tileset",     │                 │               │              │
  │               │  "tilesets/     │                 │               │              │
  │               │   hoenn.png")   │                 │               │              │
  │               │─────────────────▶                 │               │              │
  │               │                 │                 │               │              │
  │               │                 │ ResolveContentPath              │              │
  │               │                 │ ("Graphics",                    │              │
  │               │                 │  "tilesets/hoenn.png")          │              │
  │               │                 │─────────────────▶               │              │
  │               │                 │                 │               │              │
  │               │                 │                 │ Check Cache   │              │
  │               │                 │                 │───────┐       │              │
  │               │                 │                 │       │       │              │
  │               │                 │                 │◀──────┘       │              │
  │               │                 │                 │ [Cache Miss]  │              │
  │               │                 │                 │               │              │
  │               │                 │                 │ GetModsByPriority()          │
  │               │                 │                 │───────────────▶              │
  │               │                 │                 │               │              │
  │               │                 │                 │ [Mod List]    │              │
  │               │                 │                 │◀──────────────┘              │
  │               │                 │                 │               │              │
  │               │                 │                 │ TryResolveInMod              │
  │               │                 │                 │ (custom-tiles, ...)          │
  │               │                 │                 │──────────────────────────────▶
  │               │                 │                 │               │              │
  │               │                 │                 │         File.Exists()        │
  │               │                 │                 │         "/Mods/custom-tiles/ │
  │               │                 │                 │          Graphics/tilesets/  │
  │               │                 │                 │          hoenn.png"          │
  │               │                 │                 │               │              │
  │               │                 │                 │               │   [Found]    │
  │               │                 │                 │◀──────────────────────────────
  │               │                 │                 │               │              │
  │               │                 │                 │ Cache Result  │              │
  │               │                 │                 │───────┐       │              │
  │               │                 │                 │       │       │              │
  │               │                 │                 │◀──────┘       │              │
  │               │                 │                 │               │              │
  │               │                 │ [Path]          │               │              │
  │               │                 │◀────────────────┘               │              │
  │               │                 │                 │               │              │
  │               │                 │ LoadFromPath    │               │              │
  │               │                 │ ("/Mods/custom-tiles/...")      │              │
  │               │                 │─────────────────────────────────────────────────▶
  │               │                 │                 │               │              │
  │               │                 │                 │               │ [Texture2D]  │
  │               │                 │◀──────────────────────────────────────────────────
  │               │                 │                 │               │              │
  │               │                 │ Cache Texture   │               │              │
  │               │                 │───────┐         │               │              │
  │               │                 │       │         │               │              │
  │               │                 │◀──────┘         │               │              │
  │               │                 │                 │               │              │
  │               │ [Success]       │                 │               │              │
  │               │◀────────────────┘                 │               │              │
  │               │                 │                 │               │              │
  │◀──────────────┘                 │                 │               │              │
  │ [Renders with                   │                 │               │              │
  │  custom tiles]                  │                 │               │              │
```

## Scenario 2: Base Game Content (No Mod Override)

```
GameDataLoader  ContentProvider    ModLoader      FileSystem
     │                │               │              │
     │ LoadAllAsync   │               │              │
     │ ("Assets/      │               │              │
     │  Definitions") │               │              │
     │────────┐       │               │              │
     │        │       │               │              │
     │        │       │               │              │
     │        │ GetAllContentPaths    │              │
     │        │ ("Definitions",       │              │
     │        │  "TileBehaviors/*.json")             │
     │        │───────▶               │              │
     │        │       │               │              │
     │        │       │ GetModsByPriority()          │
     │        │       │───────────────▶              │
     │        │       │               │              │
     │        │       │ [Empty List - No Mods]       │
     │        │       │◀──────────────┘              │
     │        │       │               │              │
     │        │       │ SearchInBaseGame             │
     │        │       │ ("Assets/Definitions/        │
     │        │       │  TileBehaviors/*.json")      │
     │        │       │──────────────────────────────▶
     │        │       │               │              │
     │        │       │               │ [File List]  │
     │        │       │◀──────────────────────────────
     │        │       │               │              │
     │        │ [Paths]               │              │
     │        │◀──────┘               │              │
     │        │       │               │              │
     │◀───────┘       │               │              │
     │                │               │              │
     │ foreach path   │               │              │
     │────────┐       │               │              │
     │        │       │               │              │
     │        │ LoadFromPath          │              │
     │        │ ("Assets/Definitions/ │              │
     │        │  TileBehaviors/ice.json")            │
     │        │──────────────────────────────────────▶
     │        │       │               │              │
     │        │       │               │ [JSON Data]  │
     │        │◀──────────────────────────────────────
     │        │       │               │              │
     │        │ ParseToEntity         │              │
     │        │───────┐               │              │
     │        │       │               │              │
     │        │◀──────┘               │              │
     │        │       │               │              │
     │        │ SaveToEFCore          │              │
     │        │───────┐               │              │
     │        │       │               │              │
     │        │◀──────┘               │              │
     │        │       │               │              │
     │◀───────┘       │               │              │
     │                │               │              │
```

## Scenario 3: Mod Initialization Sequence

```
GameEngine    InitializationPipeline   ModLoader     ContentProvider   GameDataLoader
    │                │                    │                │                │
    │ Start()        │                    │                │                │
    │────────────────▶                    │                │                │
    │                │                    │                │                │
    │                │ LoadBaseGameModAsync                │                │
    │                │ ("Assets/")        │                │                │
    │                │────────────────────▶                │                │
    │                │                    │                │                │
    │                │                    │ ParseManifest  │                │
    │                │                    │ ("Assets/      │                │
    │                │                    │  mod.json")    │                │
    │                │                    │────────┐       │                │
    │                │                    │        │       │                │
    │                │                    │◀───────┘       │                │
    │                │                    │                │                │
    │                │                    │ RegisterMod    │                │
    │                │                    │ (base:pokesharp-core)           │
    │                │                    │───────┐        │                │
    │                │                    │       │        │                │
    │                │                    │◀──────┘        │                │
    │                │                    │                │                │
    │                │ [Success]          │                │                │
    │                │◀───────────────────┘                │                │
    │                │                    │                │                │
    │                │ LoadModsAsync      │                │                │
    │                │ ("/Mods/")         │                │                │
    │                │────────────────────▶                │                │
    │                │                    │                │                │
    │                │                    │ DiscoverMods   │                │
    │                │                    │────────┐       │                │
    │                │                    │        │       │                │
    │                │                    │◀───────┘       │                │
    │                │                    │                │                │
    │                │                    │ ResolveDependencies             │
    │                │                    │────────┐       │                │
    │                │                    │        │       │                │
    │                │                    │◀───────┘       │                │
    │                │                    │                │                │
    │                │                    │ foreach mod    │                │
    │                │                    │────────┐       │                │
    │                │                    │        │       │                │
    │                │                    │        │ LoadModAsync           │
    │                │                    │        │───────┐                │
    │                │                    │        │       │                │
    │                │                    │        │◀──────┘                │
    │                │                    │        │       │                │
    │                │                    │◀───────┘       │                │
    │                │                    │                │                │
    │                │ [Success]          │                │                │
    │                │◀───────────────────┘                │                │
    │                │                    │                │                │
    │                │ CreateContentProvider               │                │
    │                │ (modLoader, "Assets/")              │                │
    │                │────────────────────────────────────▶                │
    │                │                    │                │                │
    │                │                    │                │ [Ready]        │
    │                │◀────────────────────────────────────┘                │
    │                │                    │                │                │
    │                │ LoadGameDataStep   │                │                │
    │                │────────────────────────────────────────────────────▶ │
    │                │                    │                │                │
    │                │                    │                │ LoadAllAsync   │
    │                │                    │                │────────┐       │
    │                │                    │                │        │       │
    │                │                    │                │        │ Uses  │
    │                │                    │                │        │ ContentProvider
    │                │                    │                │        │ for   │
    │                │                    │                │        │ path  │
    │                │                    │                │        │ resolution
    │                │                    │                │        │       │
    │                │                    │                │◀───────┘       │
    │                │                    │                │                │
    │                │                    │                │ [Success]      │
    │                │◀────────────────────────────────────────────────────┘
    │                │                    │                │                │
    │ [Game Ready]   │                    │                │                │
    │◀───────────────┘                    │                │                │
    │                │                    │                │                │
```

## Scenario 4: Hot Reload Sequence

```
Developer      FileWatcher     ModLoader      ContentProvider    AssetManager
    │              │               │                │                │
    │ Edit         │               │                │                │
    │ mod file     │               │                │                │
    │──────────────▶               │                │                │
    │              │               │                │                │
    │              │ OnFileChanged │                │                │
    │              │───────────────▶                │                │
    │              │               │                │                │
    │              │               │ ReloadModAsync │                │
    │              │               │ ("user:my-mod")                 │
    │              │               │────────┐       │                │
    │              │               │        │       │                │
    │              │               │        │ UnloadModAsync         │
    │              │               │        │───────┐                │
    │              │               │        │       │                │
    │              │               │        │       │ DisposeScripts │
    │              │               │        │       │────────┐       │
    │              │               │        │       │        │       │
    │              │               │        │       │◀───────┘       │
    │              │               │        │       │                │
    │              │               │        │◀──────┘                │
    │              │               │        │       │                │
    │              │               │        │ LoadModAsync           │
    │              │               │        │───────┐                │
    │              │               │        │       │                │
    │              │               │        │       │ ParseManifest  │
    │              │               │        │       │────────┐       │
    │              │               │        │       │        │       │
    │              │               │        │       │◀───────┘       │
    │              │               │        │       │                │
    │              │               │        │       │ LoadScripts    │
    │              │               │        │       │────────┐       │
    │              │               │        │       │        │       │
    │              │               │        │       │◀───────┘       │
    │              │               │        │       │                │
    │              │               │        │◀──────┘                │
    │              │               │        │       │                │
    │              │               │◀───────┘       │                │
    │              │               │                │                │
    │              │               │ InvalidateCache()               │
    │              │               │────────────────▶                │
    │              │               │                │                │
    │              │               │                │ Clear()        │
    │              │               │                │────────┐       │
    │              │               │                │        │       │
    │              │               │                │◀───────┘       │
    │              │               │                │                │
    │              │               │                │ UnregisterTexture
    │              │               │                │ (affected textures)
    │              │               │                │────────────────▶
    │              │               │                │                │
    │              │               │                │ Dispose old    │
    │              │               │                │ textures       │
    │              │               │                │────────┐       │
    │              │               │                │        │       │
    │              │               │                │◀───────┘       │
    │              │               │                │                │
    │              │               │ [Success]      │                │
    │              │◀──────────────┘                │                │
    │              │               │                │                │
    │ [Notification]               │                │                │
    │◀─────────────┘               │                │                │
    │ "Mod reloaded"               │                │                │
    │              │               │                │                │
```

## Scenario 5: Cache Performance

```
RenderSystem    AssetManager    ContentProvider    Cache      FileSystem
     │              │                 │              │            │
     │ GetTexture   │                 │              │            │
     │ ("player")   │                 │              │            │
     │──────────────▶                 │              │            │
     │              │                 │              │            │
     │              │ [First Request] │              │            │
     │              │                 │              │            │
     │              │ ResolveContentPath             │            │
     │              │ ("Graphics",                   │            │
     │              │  "sprites/player.png")         │            │
     │              │─────────────────▶              │            │
     │              │                 │              │            │
     │              │                 │ TryGetValue  │            │
     │              │                 │──────────────▶            │
     │              │                 │              │            │
     │              │                 │ [Cache Miss] │            │
     │              │                 │◀─────────────┘            │
     │              │                 │              │            │
     │              │                 │ Search Mods  │            │
     │              │                 │──────────────────────────▶
     │              │                 │              │            │
     │              │                 │              │ [10ms]     │
     │              │                 │              │            │
     │              │                 │◀──────────────────────────
     │              │                 │              │            │
     │              │                 │ AddOrUpdate  │            │
     │              │                 │──────────────▶            │
     │              │                 │              │ [Cached]   │
     │              │                 │◀─────────────┘            │
     │              │                 │              │            │
     │              │ [Path]          │              │            │
     │              │◀────────────────┘              │            │
     │              │                 │              │            │
     │ [Texture2D]  │                 │              │            │
     │◀─────────────┘                 │              │            │
     │              │                 │              │            │
     │ [Time: 12ms] │                 │              │            │
     │              │                 │              │            │
     │              │                 │              │            │
     │ GetTexture   │                 │              │            │
     │ ("player")   │                 │              │            │
     │──────────────▶                 │              │            │
     │              │                 │              │            │
     │              │ [Subsequent     │              │            │
     │              │  Request]       │              │            │
     │              │                 │              │            │
     │              │ ResolveContentPath             │            │
     │              │─────────────────▶              │            │
     │              │                 │              │            │
     │              │                 │ TryGetValue  │            │
     │              │                 │──────────────▶            │
     │              │                 │              │            │
     │              │                 │ [Cache Hit!] │            │
     │              │                 │◀─────────────┘            │
     │              │                 │              │            │
     │              │ [Path]          │              │            │
     │              │◀────────────────┘              │            │
     │              │                 │              │            │
     │ [Texture2D]  │                 │              │            │
     │◀─────────────┘                 │              │            │
     │              │                 │              │            │
     │ [Time: 0.1ms]│                 │              │            │
     │ [120x faster]│                 │              │            │
     │              │                 │              │            │
```

## Key Observations

1. **Lazy Resolution**: Paths are only resolved when first requested
2. **Cache Effectiveness**: Subsequent lookups are O(1) and ~100x faster
3. **Mod Priority**: Higher priority mods are checked first
4. **Fallback Chain**: Always falls back to base game if no mod override
5. **Hot Reload**: Cache invalidation enables runtime mod updates
6. **Security**: Path sanitization prevents directory traversal attacks

## Performance Metrics

| Operation | First Call | Cached Call | Improvement |
|-----------|-----------|-------------|-------------|
| ResolveContentPath | 5-10ms | 0.05-0.1ms | 100-200x |
| GetAllContentPaths | 50-100ms | N/A | Uncached |
| LoadTexture | 10-15ms | 0.1-0.2ms | 50-150x |
| ModLoader init | 100-500ms | N/A | One-time |

## Error Handling Flows

### File Not Found
```
AssetManager → ContentProvider.ResolveContentPath()
    → Search mods: [Not Found]
    → Search base game: [Not Found]
    → Log Warning: "Content not found: Graphics/sprites/missing.png"
    → Return null
AssetManager → Check for fallback texture
    → Load placeholder/default texture
    → Continue gameplay
```

### Invalid Path
```
ContentProvider.ResolveContentPath("Graphics", "../../../etc/passwd")
    → SanitizeRelativePath()
        → Detect ".."
        → throw SecurityException("Invalid content path")
    → Log Security Warning
    → Return null
```

### Mod Dependency Failure
```
ModLoader.LoadModsAsync()
    → ResolveDependencies()
        → Detect circular dependency
        → throw ModDependencyException
    → Log Error
    → Skip problematic mod
    → Continue with valid mods
```
