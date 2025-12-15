# C4 Context Diagram: Content Provider System

## Level 1: System Context

```
┌─────────────────────────────────────────────────────────────────────┐
│                         PokeSharp Game                              │
│                                                                     │
│  ┌──────────────┐                                                  │
│  │   Player     │                                                  │
│  │  (Person)    │                                                  │
│  └──────┬───────┘                                                  │
│         │ Plays game, installs mods                                │
│         │                                                           │
│  ┌──────▼───────────────────────────────────────────────────────┐ │
│  │                                                               │ │
│  │         PokeSharp Game Engine                                │ │
│  │                                                               │ │
│  │  [MonoGame Application]                                       │ │
│  │  Loads and renders game content from base game and mods      │ │
│  │                                                               │ │
│  └───────┬───────────────────────────────────┬──────────────────┘ │
│          │                                   │                     │
│          │                                   │                     │
│  ┌───────▼──────────┐              ┌─────────▼──────────┐         │
│  │                  │              │                     │         │
│  │  Base Game       │              │  User Mods         │         │
│  │  Content         │              │                     │         │
│  │                  │              │  [File System]      │         │
│  │  [File System]   │              │  Custom content     │         │
│  │  Core assets,    │              │  that overrides or  │         │
│  │  definitions,    │              │  extends base game  │         │
│  │  graphics        │              │                     │         │
│  │                  │              │                     │         │
│  └──────────────────┘              └─────────────────────┘         │
│                                                                     │
└─────────────────────────────────────────────────────────────────────┘
```

## Level 2: Container Diagram

```
┌──────────────────────────────────────────────────────────────────────────┐
│                          PokeSharp Game Engine                           │
│                                                                          │
│  ┌────────────────────────────────────────────────────────────────────┐ │
│  │                     Game Application Layer                         │ │
│  │                                                                    │ │
│  │  ┌──────────────┐  ┌──────────────┐  ┌──────────────┐            │ │
│  │  │              │  │              │  │              │            │ │
│  │  │   Rendering  │  │   Gameplay   │  │   Audio      │            │ │
│  │  │   System     │  │   Systems    │  │   System     │            │ │
│  │  │              │  │              │  │              │            │ │
│  │  └──────┬───────┘  └──────┬───────┘  └──────┬───────┘            │ │
│  │         │                 │                 │                     │ │
│  └─────────┼─────────────────┼─────────────────┼─────────────────────┘ │
│            │                 │                 │                       │
│            │   Uses          │   Uses          │   Uses                │
│            │                 │                 │                       │
│  ┌─────────▼─────────────────▼─────────────────▼─────────────────────┐ │
│  │                                                                    │ │
│  │               Content Management Layer                            │ │
│  │                                                                    │ │
│  │  ┌────────────────┐  ┌──────────────────┐  ┌──────────────────┐  │ │
│  │  │                │  │                  │  │                  │  │ │
│  │  │  AssetManager  │  │  GameDataLoader  │  │  TypeRegistry<T> │  │ │
│  │  │                │  │                  │  │                  │  │ │
│  │  │  [Component]   │  │  [Component]     │  │  [Component]     │  │ │
│  │  │  Loads and     │  │  Loads JSON      │  │  Type definition │  │ │
│  │  │  caches        │  │  definitions     │  │  registry        │  │ │
│  │  │  textures,     │  │  into EF Core    │  │                  │  │ │
│  │  │  fonts         │  │                  │  │                  │  │ │
│  │  │                │  │                  │  │                  │  │ │
│  │  └────────┬───────┘  └────────┬─────────┘  └────────┬─────────┘  │ │
│  │           │                   │                     │             │ │
│  │           │ Uses              │ Uses                │ Uses        │ │
│  │           │                   │                     │             │ │
│  │  ┌────────▼───────────────────▼─────────────────────▼─────────┐  │ │
│  │  │                                                             │  │ │
│  │  │              ContentProvider                                │  │ │
│  │  │                                                             │  │ │
│  │  │              [Component]                                    │  │ │
│  │  │              Resolves content paths across mods with        │  │ │
│  │  │              priority-based lookup and LRU caching          │  │ │
│  │  │                                                             │  │ │
│  │  └────────┬──────────────────────────────────────────┬─────────┘  │ │
│  │           │                                          │             │ │
│  └───────────┼──────────────────────────────────────────┼─────────────┘ │
│              │                                          │               │
│              │ Uses                                     │ Uses          │
│              │                                          │               │
│  ┌───────────▼────────────────────────┐  ┌─────────────▼────────────┐ │
│  │                                    │  │                          │ │
│  │         ModLoader                  │  │    File System           │ │
│  │                                    │  │                          │ │
│  │         [Component]                │  │    [External]            │ │
│  │         Discovers, validates,      │  │    Base game Assets/     │ │
│  │         and loads mods with        │  │    and Mods/ directories │ │
│  │         dependency resolution      │  │                          │ │
│  │                                    │  │                          │ │
│  └────────────────────────────────────┘  └──────────────────────────┘ │
│                                                                        │
└────────────────────────────────────────────────────────────────────────┘
```

## Level 3: Component Diagram - ContentProvider Detail

```
┌─────────────────────────────────────────────────────────────────────────┐
│                         ContentProvider Component                       │
│                                                                         │
│  ┌───────────────────────────────────────────────────────────────────┐ │
│  │                    IContentProvider Interface                     │ │
│  │                                                                   │ │
│  │  + ResolveContentPath(contentType, relativePath): string?        │ │
│  │  + GetAllContentPaths(contentType, pattern): IEnumerable<string> │ │
│  │  + ContentExists(contentType, relativePath): bool                │ │
│  │  + GetContentSource(contentType, relativePath): string?          │ │
│  │  + InvalidateCache(contentType?): void                           │ │
│  │                                                                   │ │
│  └───────────────────────────────────┬───────────────────────────────┘ │
│                                      │                                 │
│                                      │ implements                      │
│                                      │                                 │
│  ┌───────────────────────────────────▼───────────────────────────────┐ │
│  │                    ContentProvider Implementation                 │ │
│  │                                                                   │ │
│  │  ┌─────────────────────────────────────────────────────────────┐ │ │
│  │  │  Resolution Engine                                          │ │ │
│  │  │                                                             │ │ │
│  │  │  - GetModsByPriority(): IEnumerable<ModManifest>           │ │ │
│  │  │  - TryResolveInMod(mod, type, path): string?               │ │ │
│  │  │  - TryResolveInBaseGame(type, path): string?               │ │ │
│  │  │  - SanitizeRelativePath(path): string                      │ │ │
│  │  │                                                             │ │ │
│  │  └─────────────────────────────────────────────────────────────┘ │ │
│  │                                                                   │ │
│  │  ┌─────────────────────────────────────────────────────────────┐ │ │
│  │  │  LRU Cache                                                  │ │ │
│  │  │                                                             │ │ │
│  │  │  Dictionary<(string, string), string> _resolvedPaths        │ │ │
│  │  │                                                             │ │ │
│  │  │  - TryGetValue(key, out value): bool                       │ │ │
│  │  │  - AddOrUpdate(key, value): void                           │ │ │
│  │  │  - Clear(): void                                            │ │ │
│  │  │                                                             │ │ │
│  │  └─────────────────────────────────────────────────────────────┘ │ │
│  │                                                                   │ │
│  │  ┌─────────────────────────────────────────────────────────────┐ │ │
│  │  │  Content Type Mappings                                      │ │ │
│  │  │                                                             │ │ │
│  │  │  static Dictionary<string, string> ContentTypeFolders       │ │ │
│  │  │                                                             │ │ │
│  │  │  - "Definitions" -> "Definitions"                           │ │ │
│  │  │  - "Graphics" -> "Graphics"                                 │ │ │
│  │  │  - "Audio" -> "Audio"                                       │ │ │
│  │  │  - "Scripts" -> "Scripts"                                   │ │ │
│  │  │  - etc.                                                     │ │ │
│  │  │                                                             │ │ │
│  │  └─────────────────────────────────────────────────────────────┘ │ │
│  │                                                                   │ │
│  │  Dependencies:                                                    │ │
│  │  - ModLoader _modLoader                                          │ │
│  │  - ILogger<ContentProvider> _logger                              │ │
│  │  - string _baseGameRoot                                          │ │
│  │                                                                   │ │
│  └───────────────────────────────────────────────────────────────────┘ │
│                                                                         │
└─────────────────────────────────────────────────────────────────────────┘
```

## Level 4: Code Diagram - Resolution Algorithm

```
ResolveContentPath(contentType, relativePath)
│
├─▶ 1. Sanitize Input
│   └─▶ SanitizeRelativePath(relativePath)
│       ├─▶ Check for ".." (directory traversal)
│       ├─▶ Check for rooted paths
│       └─▶ Normalize separators
│
├─▶ 2. Check Cache
│   └─▶ _resolvedPaths.TryGetValue((contentType, relativePath), out cached)
│       ├─▶ [Cache Hit] → Return cached
│       └─▶ [Cache Miss] → Continue
│
├─▶ 3. Search User Mods (High to Low Priority)
│   └─▶ foreach (mod in GetModsByPriority())
│       │
│       ├─▶ TryResolveInMod(mod, contentType, relativePath)
│       │   │
│       │   ├─▶ mod.ContentFolders.TryGetValue(contentType, out folder)
│       │   ├─▶ fullPath = Path.Combine(mod.DirectoryPath, folder, relativePath)
│       │   └─▶ File.Exists(fullPath)
│       │       ├─▶ [Found] → Cache & Return
│       │       └─▶ [Not Found] → Continue
│       │
│       └─▶ Next mod...
│
├─▶ 4. Fallback to Base Game
│   └─▶ TryResolveInBaseGame(contentType, relativePath)
│       │
│       ├─▶ ContentTypeFolders.TryGetValue(contentType, out folder)
│       ├─▶ fullPath = Path.Combine(_baseGameRoot, folder, relativePath)
│       └─▶ File.Exists(fullPath)
│           ├─▶ [Found] → Cache & Return
│           └─▶ [Not Found] → Return null
│
└─▶ 5. Return Result
    ├─▶ [Found] → Cache result and return absolute path
    └─▶ [Not Found] → Log warning and return null
```

## Component Responsibilities

### ContentProvider
- **Primary Responsibility**: Resolve content paths across mods and base game
- **Key Behaviors**:
  - Priority-based search across loaded mods
  - LRU caching for performance
  - Path sanitization for security
  - Logging for debugging

### ModLoader
- **Primary Responsibility**: Discover, validate, and load mods
- **Key Behaviors**:
  - Scan `/Mods/` directory
  - Parse `mod.json` manifests
  - Resolve dependencies
  - Provide loaded mod metadata

### AssetManager
- **Primary Responsibility**: Load and cache runtime assets
- **Key Behaviors**:
  - Texture loading with GPU upload
  - Font loading with FontStashSharp
  - Async preloading
  - LRU cache management

### GameDataLoader
- **Primary Responsibility**: Load JSON definitions into EF Core
- **Key Behaviors**:
  - Scan definition directories
  - Deserialize JSON to entities
  - Handle mod overrides
  - Populate in-memory database

## Data Flow

```
1. User Request
   ↓
2. AssetManager.LoadTexture("sprites/player.png")
   ↓
3. ContentProvider.ResolveContentPath("Graphics", "sprites/player.png")
   ↓
4. Search Priority Chain:
   - Cache lookup
   - Mod: custom-sprites (priority 2000)
   - Mod: enhanced-graphics (priority 1500)
   - Base: pokesharp-core (priority 1000)
   ↓
5. Return: "/Mods/custom-sprites/Graphics/sprites/player.png"
   ↓
6. AssetManager loads texture from resolved path
   ↓
7. Texture cached in AssetManager LRU cache
   ↓
8. Return Texture2D to caller
```

## Key Design Patterns

1. **Strategy Pattern**: IContentProvider allows different resolution strategies
2. **Cache-Aside Pattern**: LRU cache for resolved paths
3. **Chain of Responsibility**: Priority-based mod search chain
4. **Dependency Injection**: ContentProvider injected into consumers
5. **Facade Pattern**: ContentProvider simplifies mod system complexity

## Technology Stack

- **Language**: C# 12 (.NET 9)
- **Framework**: MonoGame (XNA-based)
- **Data**: Entity Framework Core (In-Memory)
- **Logging**: Microsoft.Extensions.Logging
- **Serialization**: System.Text.Json
- **Scripting**: Roslyn (CSX scripts)
