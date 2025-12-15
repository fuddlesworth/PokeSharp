# ContentProvider Usage Analysis

## Executive Summary

This document provides a comprehensive analysis of all `ContentProvider.GetAllContentPaths()` and `ContentProvider.ResolveContentPath()` usages throughout the codebase, documenting the actual content types used vs. what's defined in `ContentProviderOptions`.

**Analysis Date:** 2025-12-15

---

## Method Usage Overview

### `GetAllContentPaths()` Usages

| File | Line | Content Type | Pattern | Definition Type | Purpose |
|------|------|--------------|---------|----------------|---------|
| `GameDataLoader.cs` | 929 | `"Behaviors"` | `"*.json"` | `BehaviorDefinition` | Load all NPC behavior definitions with mod override support |
| `GameDataLoader.cs` | 1022 | `"TileBehaviors"` | `"*.json"` | `TileBehaviorDefinition` | Load all tile behavior definitions with mod override support |

**Total Usages:** 2

### `ResolveContentPath()` Usages

| File | Line | Content Type | Relative Path | Purpose |
|------|------|--------------|---------------|---------|
| **TilesetLoader.cs** | 503 | `"Tilesets"` | `tileset.Image.Source` | Resolve tileset image path (fallback) |
| **TilesetLoader.cs** | 665 | `"Graphics"` | `Path.GetFileName(absolutePath)` | Convert absolute path for AssetManager |
| **TilesetLoader.cs** | 734 | `"Graphics"` | `Path.GetFileName(absoluteImagePath)` | Preload tileset textures |
| **PerformanceOverlay.cs** | 78 | `"Fonts"` | `"0xProtoNerdFontMono-Regular.ttf"` | Load debug font for performance overlay |
| **IntroScene.cs** | 86 | `"Root"` | `"logo.png"` | Load game logo from root assets |
| **TrackCache.cs** | 183 | `"Root"` | `definition.AudioPath` | Resolve audio file paths (includes prefix) |
| **StreamingMusicPlayerHelper.cs** | 64 | `"Root"` | `definition.AudioPath` | Resolve streaming audio file paths |
| **AssetManager.cs** | 118 | `"Root"` | `normalizedPath` | Load textures with content type prefix |
| **AssetManager.cs** | 231 | `"Fonts"` | `normalizedRelative` | Load font files |
| **AssetManager.cs** | 313 | `"Root"` | `normalizedPath` | Async texture preloading |
| **ImageLayerProcessor.cs** | 91 | `"Graphics"` | `fullImagePath` | Load image layer textures from Tiled |
| **FontLoader.cs** | 44 | `"Fonts"` | `fontFileName` | Resolve font file paths |

**Total Usages:** 12

---

## Content Types Defined vs. Used

### Defined in ContentProviderOptions (17 types)

```csharp
public Dictionary<string, string> BaseContentFolders { get; set; } = new()
{
    ["Root"] = "",  // ✅ USED (5 usages)
    ["Definitions"] = "Definitions",  // ❌ NOT DIRECTLY USED (but subdirectories used)
    ["Graphics"] = "Graphics",  // ✅ USED (3 usages)
    ["Audio"] = "Audio",  // ❌ NOT DIRECTLY USED
    ["Scripts"] = "Scripts",  // ❌ NOT USED
    ["Fonts"] = "Fonts",  // ✅ USED (3 usages)
    ["Tiled"] = "Tiled",  // ❌ NOT USED
    ["Tilesets"] = "Tilesets",  // ✅ USED (1 usage)

    // Definition subdirectories
    ["TileBehaviors"] = "Definitions/TileBehaviors",  // ✅ USED (1 usage - GetAllContentPaths)
    ["Behaviors"] = "Definitions/Behaviors",  // ✅ USED (1 usage - GetAllContentPaths)
    ["Sprites"] = "Definitions/Sprites",  // ❌ NOT USED
    ["MapDefinitions"] = "Definitions/Maps/Regions",  // ❌ NOT USED
    ["AudioDefinitions"] = "Definitions/Audio",  // ❌ NOT USED
    ["PopupBackgrounds"] = "Definitions/Maps/Popups/Backgrounds",  // ❌ NOT USED
    ["PopupOutlines"] = "Definitions/Maps/Popups/Outlines",  // ❌ NOT USED
    ["PopupThemes"] = "Definitions/Maps/Popups/Themes",  // ❌ NOT USED
    ["MapSections"] = "Definitions/Maps/Sections"  // ❌ NOT USED
};
```

### Usage Summary

| Category | Count | Status |
|----------|-------|--------|
| **Defined content types** | 17 | Total available |
| **Actually used** | 6 | Root, Graphics, Fonts, Tilesets, Behaviors, TileBehaviors |
| **Not used** | 11 | See below |

### Unused Content Types (Candidates for Cleanup)

1. ❌ `"Definitions"` - Parent directory, subdirectories used instead
2. ❌ `"Audio"` - Audio paths use `"Root"` with full prefix
3. ❌ `"Scripts"` - No script loading implementation yet
4. ❌ `"Tiled"` - No direct Tiled file loading via ContentProvider
5. ❌ `"Sprites"` - Sprite definitions loaded via direct file access
6. ❌ `"MapDefinitions"` - Maps loaded via direct file access
7. ❌ `"AudioDefinitions"` - Audio loaded via direct file access
8. ❌ `"PopupBackgrounds"` - Loaded via direct file access
9. ❌ `"PopupOutlines"` - Loaded via direct file access
10. ❌ `"PopupThemes"` - Loaded via direct file access
11. ❌ `"MapSections"` - Loaded via direct file access

---

## Detailed Analysis by File

### 1. GameDataLoader.cs

**Purpose:** Loads game data definitions from JSON files into EF Core database.

#### GetAllContentPaths Usages

**Line 929 - Behaviors:**
```csharp
files = _contentProvider.GetAllContentPaths("Behaviors", "*.json");
```
- **Content Type:** `"Behaviors"` → `"Definitions/Behaviors"`
- **Pattern:** `"*.json"`
- **Definition Type:** `BehaviorDefinition` (NPC behaviors)
- **Mod Support:** ✅ Yes - Mods can override base game behaviors
- **Deduplication:** ✅ Yes - Same relative path = mod wins over base
- **Fallback:** Falls back to `Directory.GetFiles()` if `_contentProvider` is null

**Line 1022 - TileBehaviors:**
```csharp
files = _contentProvider.GetAllContentPaths("TileBehaviors", "*.json");
```
- **Content Type:** `"TileBehaviors"` → `"Definitions/TileBehaviors"`
- **Pattern:** `"*.json"`
- **Definition Type:** `TileBehaviorDefinition` (ice, jump, impassable, etc.)
- **Mod Support:** ✅ Yes - Mods can override base game tile behaviors
- **Deduplication:** ✅ Yes - Same relative path = mod wins over base
- **Fallback:** Falls back to `Directory.GetFiles()` if `_contentProvider` is null

**Notes:**
- Both usages follow the same pattern: try ContentProvider first, fallback to direct file access
- Source mod detection via `DetectSourceModFromPath()` for logging
- Extension data support for mod-added properties (e.g., `testProperty`, `modded`)

---

### 2. TilesetLoader.cs

**Purpose:** Loads tileset textures and external tileset JSON files for Tiled maps.

#### ResolveContentPath Usages

**Line 503 - Tilesets (Fallback):**
```csharp
string? resolvedPath = _contentProvider.ResolveContentPath("Tilesets", relativeTilesetPath);
```
- **Content Type:** `"Tilesets"` → `"Tilesets"`
- **Context:** Fallback path resolution when direct file existence check fails
- **Purpose:** Resolve tileset image paths relative to tileset JSON
- **Mod Support:** ✅ Yes - Mods can provide custom tilesets

**Line 665 - Graphics (AssetManager conversion):**
```csharp
string? resolvedPath = _contentProvider.ResolveContentPath("Graphics", Path.GetFileName(absolutePath));
```
- **Content Type:** `"Graphics"` → `"Graphics"`
- **Context:** Convert absolute path to path suitable for AssetManager
- **Purpose:** Resolve graphics files by filename only
- **Pattern:** Takes filename from absolute path, resolves via ContentProvider

**Line 734 - Graphics (Texture preloading):**
```csharp
string? resolvedPath = _contentProvider.ResolveContentPath("Graphics", Path.GetFileName(absoluteImagePath));
```
- **Content Type:** `"Graphics"` → `"Graphics"`
- **Context:** Async texture preloading for external tilesets
- **Purpose:** Resolve tileset texture paths for preloading
- **Pattern:** Same as line 665 - filename-based resolution

**Notes:**
- Tileset loading uses both direct file access AND ContentProvider
- ContentProvider used as fallback when direct path doesn't exist
- Graphics resolution always uses filename only (not full path)

---

### 3. PerformanceOverlay.cs

**Purpose:** Renders debug performance overlay (F3 key).

#### ResolveContentPath Usage

**Line 78 - Fonts:**
```csharp
string? debugFontPath = _contentProvider.ResolveContentPath("Fonts", "0xProtoNerdFontMono-Regular.ttf");
```
- **Content Type:** `"Fonts"` → `"Fonts"`
- **Relative Path:** `"0xProtoNerdFontMono-Regular.ttf"`
- **Purpose:** Load debug font for performance metrics display
- **Error Handling:** Throws `FileNotFoundException` if font not found
- **Mod Support:** ✅ Yes - Mods can override debug font

---

### 4. IntroScene.cs

**Purpose:** Displays game intro with logo animation and audio.

#### ResolveContentPath Usage

**Line 86 - Root:**
```csharp
string? logoPath = _contentProvider.ResolveContentPath("Root", "logo.png");
```
- **Content Type:** `"Root"` → `""` (root-level assets)
- **Relative Path:** `"logo.png"`
- **Purpose:** Load game logo texture for intro animation
- **Error Handling:** Throws `FileNotFoundException` if logo not found
- **Mod Support:** ✅ Yes - Mods can override game logo

**Note:** Intro scene also loads `"MonoBall.wav"` audio file using similar pattern (line ~105-115, truncated in read).

---

### 5. TrackCache.cs

**Purpose:** Caches audio tracks with LRU eviction for music playback.

#### ResolveContentPath Usage

**Line 183 - Root:**
```csharp
string? fullPath = _contentProvider.ResolveContentPath("Root", definition.AudioPath);
```
- **Content Type:** `"Root"` → `""` (root-level)
- **Relative Path:** `definition.AudioPath` (e.g., `"Audio/Music/Battle/wild.ogg"`)
- **Purpose:** Resolve audio file paths for caching
- **Error Handling:** Throws `FileNotFoundException` if audio not found
- **Why Root?** Audio paths from database include content type prefix (e.g., `"Audio/Music/..."`), so we resolve from root
- **Mod Support:** ✅ Yes - Mods can override audio files

---

### 6. StreamingMusicPlayerHelper.cs

**Purpose:** Helper for streaming audio playback (large files that shouldn't be fully cached).

#### ResolveContentPath Usage

**Line 64 - Root:**
```csharp
string? fullPath = _contentProvider.ResolveContentPath("Root", definition.AudioPath);
```
- **Content Type:** `"Root"` → `""` (root-level)
- **Relative Path:** `definition.AudioPath` (e.g., `"Audio/Music/Route/route101.ogg"`)
- **Purpose:** Resolve streaming audio file paths
- **Error Handling:** Throws `FileNotFoundException` if audio not found
- **Why Root?** Same reason as TrackCache - paths include prefix
- **Mod Support:** ✅ Yes - Mods can override streaming audio

---

### 7. AssetManager.cs

**Purpose:** Manages runtime texture and font loading with LRU caching.

#### ResolveContentPath Usages

**Line 118 - Root (Texture loading):**
```csharp
string? resolvedPath = _contentProvider.ResolveContentPath("Root", normalizedPath);
```
- **Content Type:** `"Root"` → `""` (root-level)
- **Context:** Synchronous texture loading
- **Purpose:** Resolve texture paths that include content type prefix
- **Why Root?** Paths from definitions already include type (e.g., `"Graphics/Maps/..."`)
- **Fallback:** If path is absolute and exists, use directly (bypasses ContentProvider)

**Line 231 - Fonts:**
```csharp
string? fullPath = _contentProvider.ResolveContentPath("Fonts", normalizedRelative);
```
- **Content Type:** `"Fonts"` → `"Fonts"`
- **Context:** Font loading
- **Purpose:** Resolve font file paths
- **Error Handling:** Throws `FileNotFoundException` if font not found

**Line 313 - Root (Async texture preloading):**
```csharp
fullPath = _contentProvider.ResolveContentPath("Root", normalizedPath);
```
- **Content Type:** `"Root"` → `""` (root-level)
- **Context:** Asynchronous texture preloading
- **Purpose:** Same as line 118 - resolve texture paths for background loading
- **Pattern:** Identical to synchronous loading path

**Notes:**
- AssetManager has dual-mode path resolution:
  - **Absolute paths:** Use directly if file exists (bypasses ContentProvider)
  - **Relative paths:** Resolve via ContentProvider
- Most textures use `"Root"` because paths include content type prefix

---

### 8. ImageLayerProcessor.cs

**Purpose:** Creates image layer entities from Tiled maps.

#### ResolveContentPath Usage

**Line 91 - Graphics:**
```csharp
string? resolvedPath = _contentProvider.ResolveContentPath("Graphics", fullImagePath);
```
- **Content Type:** `"Graphics"` → `"Graphics"`
- **Context:** Loading image layer textures from Tiled
- **Relative Path:** `fullImagePath` (combined map directory + relative image path)
- **Purpose:** Resolve Tiled image layer texture paths
- **Error Handling:** Throws `FileNotFoundException` if texture not found
- **Mod Support:** ✅ Yes - Mods can override image layer graphics

**Note:** This is one of the few places that uses `"Graphics"` content type directly rather than `"Root"`.

---

### 9. FontLoader.cs

**Purpose:** Utility for loading fonts with ContentProvider integration.

#### ResolveContentPath Usage

**Line 44 - Fonts:**
```csharp
string? path = _contentProvider.ResolveContentPath("Fonts", fontFileName);
```
- **Content Type:** `"Fonts"` → `"Fonts"`
- **Relative Path:** `fontFileName` (e.g., `"pokemon.ttf"`, `"0xProtoNerdFontMono-Regular.ttf"`)
- **Purpose:** Generic font path resolution utility
- **Error Handling:** Logs warning if font not found, returns null
- **Public Methods:**
  - `GetGameFontPath()` - Resolves `"pokemon.ttf"`
  - `GetDebugFontPath()` - Resolves `"0xProtoNerdFontMono-Regular.ttf"`
  - `FontExists()` - Uses `ContentExists()` to check font availability

---

## Pattern Analysis

### Content Type Usage Patterns

#### 1. **Root** (5 usages)
**Usage Pattern:** Paths that already include content type prefix

```csharp
// Audio files - path includes "Audio/Music/..." prefix
_contentProvider.ResolveContentPath("Root", "Audio/Music/Battle/wild.ogg")

// Root-level assets - no prefix needed
_contentProvider.ResolveContentPath("Root", "logo.png")

// Textures - path includes "Graphics/..." prefix
_contentProvider.ResolveContentPath("Root", "Graphics/Maps/tileset.png")
```

**Why?** Database definitions store full paths with content type prefix, so we resolve from root.

#### 2. **Graphics** (3 usages)
**Usage Pattern:** Filename-only or partial path resolution

```csharp
// Resolve by filename only
_contentProvider.ResolveContentPath("Graphics", Path.GetFileName(absolutePath))

// Tiled image layers - already in Graphics directory
_contentProvider.ResolveContentPath("Graphics", fullImagePath)
```

**Why?** When we have just a filename or know it's in Graphics directory.

#### 3. **Fonts** (3 usages)
**Usage Pattern:** Direct font filename resolution

```csharp
_contentProvider.ResolveContentPath("Fonts", "pokemon.ttf")
_contentProvider.ResolveContentPath("Fonts", "0xProtoNerdFontMono-Regular.ttf")
```

**Why?** Fonts are always in Fonts directory, no prefix needed.

#### 4. **Behaviors** (1 usage - GetAllContentPaths)
**Usage Pattern:** Load all behavior definitions

```csharp
_contentProvider.GetAllContentPaths("Behaviors", "*.json")
```

**Why?** Mod system requires loading ALL behaviors to support overrides.

#### 5. **TileBehaviors** (1 usage - GetAllContentPaths)
**Usage Pattern:** Load all tile behavior definitions

```csharp
_contentProvider.GetAllContentPaths("TileBehaviors", "*.json")
```

**Why?** Mod system requires loading ALL tile behaviors to support overrides.

#### 6. **Tilesets** (1 usage)
**Usage Pattern:** Fallback resolution for tileset images

```csharp
_contentProvider.ResolveContentPath("Tilesets", relativeTilesetPath)
```

**Why?** Only used when direct file path doesn't exist.

---

## Anti-Patterns and Issues

### Issue 1: Inconsistent "Root" vs Specific Type Usage

**Problem:** Some code uses `"Root"` for paths that should use specific types.

**Example:**
```csharp
// AssetManager.cs:118 - Uses Root for all textures
string? resolvedPath = _contentProvider.ResolveContentPath("Root", normalizedPath);

// ImageLayerProcessor.cs:91 - Uses Graphics for textures
string? resolvedPath = _contentProvider.ResolveContentPath("Graphics", fullImagePath);
```

**Impact:** Inconsistent - both work, but pattern is unclear.

**Root Cause:** Paths from database include content type prefix (e.g., `"Graphics/Maps/..."`) so they must use `"Root"` type. Tiled paths don't include prefix, so they use specific type.

**Recommendation:** Document this pattern clearly in code comments.

---

### Issue 2: Many Unused Content Types

**Problem:** 11 of 17 content types are defined but never used.

**Defined but Unused:**
- `"Definitions"` - Parent directory
- `"Audio"` - Audio uses `"Root"` instead
- `"Scripts"` - No implementation
- `"Tiled"` - No implementation
- `"Sprites"` - Direct file access
- `"MapDefinitions"` - Direct file access
- `"AudioDefinitions"` - Direct file access
- `"PopupBackgrounds"` - Direct file access
- `"PopupOutlines"` - Direct file access
- `"PopupThemes"` - Direct file access
- `"MapSections"` - Direct file access

**Impact:** Configuration bloat, unclear which types are active.

**Recommendation:** Either:
1. Migrate unused definition types to use ContentProvider (preferred)
2. Remove unused types from ContentProviderOptions
3. Document which types are active vs. planned

---

### Issue 3: Direct File Access vs ContentProvider

**Problem:** Some loaders use direct file access instead of ContentProvider.

**Examples:**
- `GameDataLoader.LoadMapEntitysAsync()` - Uses `Directory.GetFiles()`
- `GameDataLoader.LoadPopupThemesAsync()` - Uses `Directory.GetFiles()`
- `GameDataLoader.LoadAudioEntitysAsync()` - Uses `Directory.GetFiles()`

**Why This Happens:** These methods have optional `IContentProvider` parameter and fallback to direct access.

**Impact:** Inconsistent - mod support works for Behaviors/TileBehaviors but not Maps/Audio/Popups.

**Recommendation:** Make ContentProvider required (not optional) for GameDataLoader.

---

## Code Quality Issues

### 1. Null-Conditional Pattern Inconsistency

**Good Pattern (FontLoader.cs):**
```csharp
string? path = _contentProvider.ResolveContentPath("Fonts", fontFileName);
if (path == null)
{
    _logger.LogWarning("Font not found: {Font}", fontFileName);
}
return path;
```

**Risky Pattern (AssetManager.cs):**
```csharp
string? resolvedPath = _contentProvider.ResolveContentPath("Root", normalizedPath);
if (resolvedPath == null)
{
    throw new FileNotFoundException($"Texture not found: {relativePath}");
}
fullPath = resolvedPath; // Will never be null here
```

**Issue:** The throw ensures non-null, but pattern could be clearer with null-forgiving operator or `ArgumentNullException.ThrowIfNull()`.

---

### 2. Magic String Content Types

**Problem:** Content types are magic strings scattered throughout code.

**Example:**
```csharp
_contentProvider.ResolveContentPath("Graphics", ...)
_contentProvider.ResolveContentPath("Fonts", ...)
_contentProvider.ResolveContentPath("Root", ...)
```

**Recommendation:** Create string constants or enum for content types:

```csharp
public static class ContentTypes
{
    public const string Root = "Root";
    public const string Graphics = "Graphics";
    public const string Fonts = "Fonts";
    public const string Tilesets = "Tilesets";
    public const string Behaviors = "Behaviors";
    public const string TileBehaviors = "TileBehaviors";
}
```

---

### 3. Fallback Pattern Duplication

**Problem:** GameDataLoader has duplicate fallback pattern in multiple methods:

```csharp
IEnumerable<string> files;
if (_contentProvider != null)
{
    files = _contentProvider.GetAllContentPaths("Behaviors", "*.json");
}
else
{
    if (!Directory.Exists(path))
        return 0;
    files = Directory.GetFiles(path, "*.json", SearchOption.AllDirectories)
        .Where(f => !IsHiddenOrSystemDirectory(f));
}
```

**Recommendation:** Extract to helper method or make ContentProvider required (preferred).

---

## Performance Considerations

### ContentProvider Cache Hit Analysis

Based on usage patterns, ContentProvider cache should perform well:

**High Cache Hit Scenarios:**
1. ✅ Repeated font loads (same fonts used across scenes)
2. ✅ Texture reloads (LRU cache may evict/reload)
3. ✅ Audio track resolution (same tracks played multiple times)

**Low Cache Hit Scenarios:**
1. ❌ `GetAllContentPaths()` calls - Only happen once at startup
2. ❌ Tileset loading - Each map has unique tilesets

**Recommendation:** Current LRU cache size (10,000 entries) is appropriate.

---

## Recommendations

### Priority 1: High Impact

1. **Document Root vs Specific Type Pattern**
   - Add code comments explaining when to use `"Root"` vs specific types
   - Document that database paths include content type prefix

2. **Make ContentProvider Required**
   - Remove optional `IContentProvider?` from GameDataLoader constructor
   - Remove all fallback to `Directory.GetFiles()` code paths

3. **Migrate Remaining Loaders to ContentProvider**
   - Convert Maps, PopupThemes, MapSections, Audio to use `GetAllContentPaths()`
   - Enable full mod support for all definition types

### Priority 2: Medium Impact

4. **Create ContentTypes Constants**
   - Add `ContentTypes` static class with string constants
   - Replace all magic strings with constants

5. **Remove Unused Content Types**
   - Remove from `ContentProviderOptions.BaseContentFolders`
   - Or document as "planned" with TODO comments

### Priority 3: Low Impact (Code Quality)

6. **Standardize Null-Handling Pattern**
   - Use consistent pattern for `ResolveContentPath()` null checks
   - Consider using `ArgumentNullException.ThrowIfNull()` where appropriate

7. **Extract Fallback Pattern**
   - Create helper method for "ContentProvider or fallback" pattern
   - Reduce code duplication in GameDataLoader

---

## Testing Coverage Gaps

### Missing Test Coverage

1. ❌ `GetAllContentPaths()` with mod overrides
   - Need test: Base game + mod override same behavior
   - Need test: Mod adds new behavior

2. ❌ Content type case sensitivity
   - Need test: `"Graphics"` vs `"graphics"` vs `"GRAPHICS"`

3. ❌ Path traversal attacks
   - Need test: `ResolveContentPath("Graphics", "../../../etc/passwd")`
   - Need test: ThrowOnPathTraversal option

4. ❌ Content type not in BaseContentFolders
   - Need test: What happens with undefined content type?

### Existing Test Coverage

✅ `ContentProviderTests.cs` has good coverage for:
- Basic path resolution
- Mod priority ordering
- Cache behavior
- LRU eviction

---

## Conclusion

### Summary Statistics

- **Total ContentProvider API calls:** 14 (2 GetAllContentPaths, 12 ResolveContentPath)
- **Content types actively used:** 6 of 17 (35%)
- **Files using ContentProvider:** 9
- **Mod support coverage:** ~35% of definition types (Behaviors, TileBehaviors only)

### Key Findings

1. **`"Root"` is the most-used content type** (5 usages) because database paths include content type prefix
2. **Most definition types don't use ContentProvider** - they use direct `Directory.GetFiles()`
3. **Mod support is incomplete** - Only Behaviors and TileBehaviors support overrides
4. **Many content types defined but unused** - 11 of 17 types never called

### Next Steps

1. Complete ContentProvider migration for all definition types
2. Remove or document unused content types
3. Create constants for content type strings
4. Add test coverage for gaps identified above

---

## Appendix: All Usage Locations

### GetAllContentPaths() Calls (2)

```
MonoBallFramework.Game/GameData/Loading/GameDataLoader.cs:929
    _contentProvider.GetAllContentPaths("Behaviors", "*.json")

MonoBallFramework.Game/GameData/Loading/GameDataLoader.cs:1022
    _contentProvider.GetAllContentPaths("TileBehaviors", "*.json")
```

### ResolveContentPath() Calls (12)

```
MonoBallFramework.Game/GameData/MapLoading/Tiled/Services/TilesetLoader.cs:503
    _contentProvider.ResolveContentPath("Tilesets", relativeTilesetPath)

MonoBallFramework.Game/GameData/MapLoading/Tiled/Services/TilesetLoader.cs:665
    _contentProvider.ResolveContentPath("Graphics", Path.GetFileName(absolutePath))

MonoBallFramework.Game/GameData/MapLoading/Tiled/Services/TilesetLoader.cs:734
    _contentProvider.ResolveContentPath("Graphics", Path.GetFileName(absoluteImagePath))

MonoBallFramework.Game/Infrastructure/Diagnostics/PerformanceOverlay.cs:78
    _contentProvider.ResolveContentPath("Fonts", "0xProtoNerdFontMono-Regular.ttf")

MonoBallFramework.Game/Engine/Scenes/Scenes/IntroScene.cs:86
    _contentProvider.ResolveContentPath("Root", "logo.png")

MonoBallFramework.Game/Engine/Audio/Services/TrackCache.cs:183
    _contentProvider.ResolveContentPath("Root", definition.AudioPath)

MonoBallFramework.Game/Engine/Audio/Services/Streaming/StreamingMusicPlayerHelper.cs:64
    _contentProvider.ResolveContentPath("Root", definition.AudioPath)

MonoBallFramework.Game/Engine/Rendering/Assets/AssetManager.cs:118
    _contentProvider.ResolveContentPath("Root", normalizedPath)

MonoBallFramework.Game/Engine/Rendering/Assets/AssetManager.cs:231
    _contentProvider.ResolveContentPath("Fonts", normalizedRelative)

MonoBallFramework.Game/Engine/Rendering/Assets/AssetManager.cs:313
    _contentProvider.ResolveContentPath("Root", normalizedPath)

MonoBallFramework.Game/GameData/MapLoading/Tiled/Processors/ImageLayerProcessor.cs:91
    _contentProvider.ResolveContentPath("Graphics", fullImagePath)

MonoBallFramework.Game/Engine/UI/Utilities/FontLoader.cs:44
    _contentProvider.ResolveContentPath("Fonts", fontFileName)
```

---

**Document Version:** 1.0
**Last Updated:** 2025-12-15
**Analyzed By:** Code Quality Analyzer (Claude Sonnet 4.5)
