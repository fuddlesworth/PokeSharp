# ContentProvider Usage Summary

## Quick Reference

### Content Types: Active vs Defined

| Content Type | Status | Usages | Purpose |
|--------------|--------|--------|---------|
| **Root** | ✅ Active | 5 | Paths with content type prefix (Audio/Music/..., Graphics/...) |
| **Graphics** | ✅ Active | 3 | Graphics files (tilesets, image layers) |
| **Fonts** | ✅ Active | 3 | Font files (.ttf) |
| **Tilesets** | ✅ Active | 1 | Tileset images (fallback) |
| **Behaviors** | ✅ Active | 1 | NPC behavior definitions (*.json) |
| **TileBehaviors** | ✅ Active | 1 | Tile behavior definitions (*.json) |
| **Definitions** | ❌ Unused | 0 | Parent directory (subdirs used instead) |
| **Audio** | ❌ Unused | 0 | Audio uses "Root" instead |
| **Scripts** | ❌ Unused | 0 | No implementation |
| **Tiled** | ❌ Unused | 0 | No implementation |
| **Sprites** | ❌ Unused | 0 | Direct file access |
| **MapDefinitions** | ❌ Unused | 0 | Direct file access |
| **AudioDefinitions** | ❌ Unused | 0 | Direct file access |
| **PopupBackgrounds** | ❌ Unused | 0 | Direct file access |
| **PopupOutlines** | ❌ Unused | 0 | Direct file access |
| **PopupThemes** | ❌ Unused | 0 | Direct file access |
| **MapSections** | ❌ Unused | 0 | Direct file access |

**Total:** 6 active / 17 defined (35% utilization)

---

## Method Usage Breakdown

### GetAllContentPaths() - 2 calls

Used for loading ALL files of a type with mod support:

```csharp
// Load all NPC behaviors (with mod overrides)
_contentProvider.GetAllContentPaths("Behaviors", "*.json")

// Load all tile behaviors (with mod overrides)
_contentProvider.GetAllContentPaths("TileBehaviors", "*.json")
```

**Files:**
- `GameDataLoader.cs` (2 usages)

---

### ResolveContentPath() - 12 calls

Used for resolving individual file paths:

```csharp
// Fonts (3 calls)
_contentProvider.ResolveContentPath("Fonts", "pokemon.ttf")
_contentProvider.ResolveContentPath("Fonts", "0xProtoNerdFontMono-Regular.ttf")
// + AssetManager font loading

// Root - prefixed paths (5 calls)
_contentProvider.ResolveContentPath("Root", "logo.png")
_contentProvider.ResolveContentPath("Root", "Audio/Music/wild.ogg")
// + AssetManager texture loading (2x)
// + Audio streaming

// Graphics (3 calls)
_contentProvider.ResolveContentPath("Graphics", "tileset.png")
// + TilesetLoader (2x)

// Tilesets (1 call)
_contentProvider.ResolveContentPath("Tilesets", "tileset_image.png")
```

**Files:**
- `AssetManager.cs` (3 usages)
- `TilesetLoader.cs` (3 usages)
- `FontLoader.cs` (1 usage)
- `PerformanceOverlay.cs` (1 usage)
- `IntroScene.cs` (1 usage)
- `TrackCache.cs` (1 usage)
- `StreamingMusicPlayerHelper.cs` (1 usage)
- `ImageLayerProcessor.cs` (1 usage)

---

## Usage Patterns

### Pattern 1: "Root" for Prefixed Paths

**When:** Database definitions include content type prefix

```csharp
// Database stores: "Audio/Music/Battle/wild.ogg"
_contentProvider.ResolveContentPath("Root", definition.AudioPath)
// Resolves from Assets root, finds: Assets/Audio/Music/Battle/wild.ogg

// Database stores: "Graphics/Maps/tileset.png"
_contentProvider.ResolveContentPath("Root", texturePath)
// Resolves from Assets root, finds: Assets/Graphics/Maps/tileset.png
```

**Files:** AssetManager, TrackCache, StreamingMusicPlayerHelper, IntroScene

---

### Pattern 2: Specific Type for Unprefixed Paths

**When:** Path is relative to content type directory

```csharp
// Just the filename, no prefix
_contentProvider.ResolveContentPath("Fonts", "pokemon.ttf")
// Resolves to: Assets/Fonts/pokemon.ttf

// Relative to Graphics directory
_contentProvider.ResolveContentPath("Graphics", "Tiled/tileset.png")
// Resolves to: Assets/Graphics/Tiled/tileset.png
```

**Files:** FontLoader, ImageLayerProcessor, TilesetLoader

---

### Pattern 3: Load All with Mod Support

**When:** Need to load all files of a type, supporting mod overrides

```csharp
// Get ALL behavior files (base + mods, mods override base)
var files = _contentProvider.GetAllContentPaths("Behaviors", "*.json");

// Returns:
// Mods/my-mod/content/Definitions/Behaviors/custom.json
// Mods/my-mod/content/Definitions/Behaviors/wander.json  // Overrides base
// Assets/Definitions/Behaviors/wander.json                // Skipped (overridden)
// Assets/Definitions/Behaviors/stationary.json
```

**Files:** GameDataLoader (Behaviors, TileBehaviors)

---

## Key Findings

### ✅ Working Well

1. **Mod Support for Behaviors/TileBehaviors** - Complete mod override system working
2. **Cache Performance** - LRU cache (10,000 entries) handles repeated lookups well
3. **Font Loading** - Consistent pattern across all font loads
4. **Audio Resolution** - Both cached and streaming audio use same "Root" pattern

### ❌ Issues Found

1. **Low Content Type Utilization** - Only 35% of defined types are actually used
2. **Incomplete Mod Support** - Most definition types use direct file access, not ContentProvider
3. **Inconsistent Patterns** - Some use "Root" + prefix, others use specific type
4. **No Constants** - Content type strings are magic strings throughout codebase
5. **Optional ContentProvider** - GameDataLoader makes it optional, causing dual code paths

### ⚠️ Code Smells

1. **Duplicate Fallback Pattern** - GameDataLoader has repeated "if contentProvider != null" pattern
2. **Magic Strings** - "Fonts", "Graphics", "Root", etc. scattered throughout
3. **Unused Config** - 11 content types defined but never called
4. **Mixed Paradigms** - Some loaders use ContentProvider, others use Directory.GetFiles()

---

## Recommendations

### Immediate Actions (High Priority)

1. ✅ **Make ContentProvider Required** in GameDataLoader
   - Remove optional `IContentProvider?` parameter
   - Remove all `Directory.GetFiles()` fallback code

2. ✅ **Migrate Remaining Loaders** to use GetAllContentPaths:
   - Maps → `GetAllContentPaths("MapDefinitions", "*.json")`
   - PopupThemes → `GetAllContentPaths("PopupThemes", "*.json")`
   - MapSections → `GetAllContentPaths("MapSections", "*.json")`
   - Audio → `GetAllContentPaths("AudioDefinitions", "*.json")`
   - Sprites → `GetAllContentPaths("Sprites", "*.json")`
   - Backgrounds → `GetAllContentPaths("PopupBackgrounds", "*.json")`
   - Outlines → `GetAllContentPaths("PopupOutlines", "*.json")`

3. ✅ **Create ContentTypes Constants**:
   ```csharp
   public static class ContentTypes
   {
       public const string Root = "Root";
       public const string Graphics = "Graphics";
       public const string Fonts = "Fonts";
       public const string Behaviors = "Behaviors";
       public const string TileBehaviors = "TileBehaviors";
       // etc.
   }
   ```

### Follow-Up Actions (Medium Priority)

4. ✅ **Document "Root" vs Specific Type Pattern**
   - Add XML comments explaining when to use each
   - Add code examples in ContentProvider.cs

5. ✅ **Remove Unused Content Types** from ContentProviderOptions
   - Or mark as "planned" with TODO comments

6. ✅ **Add Missing Test Coverage**:
   - Mod override tests for GetAllContentPaths
   - Path traversal security tests
   - Undefined content type handling

### Future Enhancements (Low Priority)

7. ✅ **Extract Fallback Pattern** to helper method
8. ✅ **Standardize Null-Handling** pattern across all usages
9. ✅ **Add Performance Metrics** to track cache hit rates

---

## Migration Checklist

To complete ContentProvider migration:

- [x] ✅ **Behaviors** - Using GetAllContentPaths (DONE)
- [x] ✅ **TileBehaviors** - Using GetAllContentPaths (DONE)
- [ ] ❌ **MapDefinitions** - Still using Directory.GetFiles()
- [ ] ❌ **PopupThemes** - Still using Directory.GetFiles()
- [ ] ❌ **MapSections** - Still using Directory.GetFiles()
- [ ] ❌ **AudioDefinitions** - Still using Directory.GetFiles()
- [ ] ❌ **Sprites** - Still using Directory.GetFiles()
- [ ] ❌ **PopupBackgrounds** - Still using Directory.GetFiles()
- [ ] ❌ **PopupOutlines** - Still using Directory.GetFiles()

**Progress:** 2 of 9 definition types migrated (22%)

---

## Quick Lookup Table

Need to load a specific type of content? Use this table:

| What to Load | Method | Content Type | Example |
|--------------|--------|--------------|---------|
| Font file | `ResolveContentPath` | `"Fonts"` | `("Fonts", "pokemon.ttf")` |
| Root asset (logo, etc.) | `ResolveContentPath` | `"Root"` | `("Root", "logo.png")` |
| Audio file | `ResolveContentPath` | `"Root"` | `("Root", "Audio/Music/wild.ogg")` |
| Texture (from definition) | `ResolveContentPath` | `"Root"` | `("Root", definition.TexturePath)` |
| Texture (Tiled) | `ResolveContentPath` | `"Graphics"` | `("Graphics", "tileset.png")` |
| All NPC behaviors | `GetAllContentPaths` | `"Behaviors"` | `("Behaviors", "*.json")` |
| All tile behaviors | `GetAllContentPaths` | `"TileBehaviors"` | `("TileBehaviors", "*.json")` |

---

**For full details, see:** [content-provider-usage-analysis.md](./content-provider-usage-analysis.md)
