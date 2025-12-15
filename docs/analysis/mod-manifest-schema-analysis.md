# Mod Manifest Schema Analysis Report

## Code Quality Analysis Report

### Summary
- **Overall Quality Score**: 7/10
- **Files Analyzed**: 6 core files
- **Issues Found**: 3 critical inconsistencies
- **Technical Debt Estimate**: 2-3 hours

---

## Critical Inconsistencies Found

### 1. **CRITICAL: contentFolders Key Mismatch**

**Issue**: Mods declare `contentFolders` keys that don't match what `ContentProvider` expects.

#### Where the Problem Occurs

**ModManifest.cs** (Lines 90-96):
```csharp
/// <summary>
///     Relative paths to folders containing new content (templates, definitions, etc.).
///     Key is the content type, value is the relative folder path.
///     Example: { "Templates": "content/templates", "Sprites": "content/sprites" }
/// </summary>
[JsonPropertyName("contentFolders")]
public Dictionary<string, string> ContentFolders { get; init; } = new();
```

**Documentation says**: Use generic keys like `"Templates"`, `"Sprites"`

**BUT ContentProviderOptions.cs** (Lines 36-57):
```csharp
public Dictionary<string, string> BaseContentFolders { get; set; } = new()
{
    ["Root"] = "",  // Root-level assets (logo.png, MonoBall.wav, etc.)
    ["Definitions"] = "Definitions",
    ["Graphics"] = "Graphics",
    ["Audio"] = "Audio",
    ["Scripts"] = "Scripts",
    ["Fonts"] = "Fonts",
    ["Tiled"] = "Tiled",
    ["Tilesets"] = "Tilesets",

    // Definition subdirectories - allows mods to override specific definition types
    ["TileBehaviors"] = "Definitions/TileBehaviors",
    ["Behaviors"] = "Definitions/Behaviors",
    ["Sprites"] = "Definitions/Sprites",
    ["MapDefinitions"] = "Definitions/Maps/Regions",
    ["AudioDefinitions"] = "Definitions/Audio",
    ["PopupBackgrounds"] = "Definitions/Maps/Popups/Backgrounds",
    ["PopupOutlines"] = "Definitions/Maps/Popups/Outlines",
    ["PopupThemes"] = "Definitions/Maps/Popups/Themes",
    ["MapSections"] = "Definitions/Maps/Sections"
};
```

**Reality**: ContentProvider expects these 16 specific keys, NOT generic ones!

#### Examples of the Confusion

**test-override/mod.json** (Lines 8-10) - ✅ CORRECT:
```json
{
  "contentFolders": {
    "TileBehaviors": "content/Definitions/TileBehaviors"
  }
}
```
This works because `"TileBehaviors"` matches a key in `BaseContentFolders`.

**base:pokesharp-core/mod.json** (Lines 8-16) - ⚠️ INCOMPLETE:
```json
{
  "contentFolders": {
    "Definitions": "Definitions",
    "Graphics": "Graphics",
    "Audio": "Audio",
    "Scripts": "Scripts",
    "Fonts": "Fonts",
    "Tiled": "Tiled",
    "Tilesets": "Tilesets"
  }
}
```
Missing the 9 subdirectory keys: `TileBehaviors`, `Behaviors`, `Sprites`, etc.

**Documentation in base-game-as-mod-architecture.md** (Lines 146-155) - ❌ WRONG:
```csharp
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
```
This shows only 7 types, but the actual implementation has 16!

---

### 2. **MEDIUM: ModLoader Default Manifest Creation Incomplete**

**Issue**: When ModLoader auto-creates a base game manifest, it doesn't include subdirectory keys.

**Location**: ModLoader.cs (Lines 609-632)

```csharp
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
    // ...
}
```

**Problem**: This creates a manifest with only 7 content folder keys, missing:
- `TileBehaviors`
- `Behaviors`
- `Sprites`
- `MapDefinitions`
- `AudioDefinitions`
- `PopupBackgrounds`
- `PopupOutlines`
- `PopupThemes`
- `MapSections`

**Impact**: If a mod tries to override content in these subdirectories, the ContentProvider won't find them because the base game manifest doesn't declare them.

---

### 3. **LOW: Documentation Example Mismatch**

**Issue**: ModManifest.cs documentation example uses generic keys, not the actual required ones.

**Location**: ModManifest.cs (Line 93)

```csharp
///     Example: { "Templates": "content/templates", "Sprites": "content/sprites" }
```

**Problem**:
- `"Templates"` is NOT a valid key in `BaseContentFolders`
- `"Sprites"` IS valid, but the example suggests it's at the same level as Templates

**Correct Example Should Be**:
```csharp
///     Example: { "TileBehaviors": "content/Definitions/TileBehaviors", "Graphics": "content/Graphics" }
```

---

## How Content Resolution Actually Works

### The Resolution Chain

**ContentProvider.ResolveContentPath()** (Lines 53-172):

```csharp
public string? ResolveContentPath(string contentType, string relativePath)
{
    // 1. Check cache
    string cacheKey = $"{contentType}:{relativePath}";
    if (_cache.TryGet(cacheKey, out string? cachedPath))
        return cachedPath;

    // 2. Search mods by priority (highest to lowest)
    var modsOrderedByPriority = _modLoader.LoadedMods.Values
        .OrderByDescending(m => m.Priority)
        .ToList();

    foreach (var mod in modsOrderedByPriority)
    {
        // ⚠️ THIS IS THE KEY LOOKUP
        if (!mod.ContentFolders.TryGetValue(contentType, out string? contentFolder))
            continue;  // Skip if mod doesn't declare this contentType

        string candidatePath = Path.Combine(mod.DirectoryPath, contentFolder, relativePath);
        if (File.Exists(candidatePath))
        {
            _cache.Set(cacheKey, candidatePath);
            return candidatePath;
        }
    }

    // 3. Fallback to base game
    if (_options.BaseContentFolders.TryGetValue(contentType, out string? baseContentFolder))
    {
        string basePath = Path.Combine(_options.BaseGameRoot, baseContentFolder, relativePath);
        if (File.Exists(basePath))
        {
            _cache.Set(cacheKey, basePath);
            return basePath;
        }
    }

    return null;
}
```

### Example Resolution Flow

**Request**: `ResolveContentPath("TileBehaviors", "ice.json")`

**Step 1**: Check if any high-priority mod declares `"TileBehaviors"` key
```json
// test-override/mod.json (priority: 100)
{
  "contentFolders": {
    "TileBehaviors": "content/Definitions/TileBehaviors"
  }
}
```
✅ Match! Checks `/Mods/test-override/content/Definitions/TileBehaviors/ice.json`

**Step 2**: If not found in mods, check base game
```csharp
// ContentProviderOptions.BaseContentFolders
["TileBehaviors"] = "Definitions/TileBehaviors"
```
✅ Checks `/Assets/Definitions/TileBehaviors/ice.json`

---

## The 16 Valid contentFolders Keys

| Key | Base Game Path | Purpose |
|-----|----------------|---------|
| `Root` | `` (empty) | Root-level assets (logo.png, etc.) |
| `Definitions` | `Definitions` | All definition files |
| `Graphics` | `Graphics` | Textures and sprites |
| `Audio` | `Audio` | Music and sound effects |
| `Scripts` | `Scripts` | CSX behavior scripts |
| `Fonts` | `Fonts` | TTF font files |
| `Tiled` | `Tiled` | Tiled map files |
| `Tilesets` | `Tilesets` | Tileset images and JSON |
| **Subdirectories** | | |
| `TileBehaviors` | `Definitions/TileBehaviors` | Tile behavior definitions |
| `Behaviors` | `Definitions/Behaviors` | Entity behavior definitions |
| `Sprites` | `Definitions/Sprites` | Sprite definitions |
| `MapDefinitions` | `Definitions/Maps/Regions` | Map region definitions |
| `AudioDefinitions` | `Definitions/Audio` | Audio definitions |
| `PopupBackgrounds` | `Definitions/Maps/Popups/Backgrounds` | Popup background definitions |
| `PopupOutlines` | `Definitions/Maps/Popups/Outlines` | Popup outline definitions |
| `PopupThemes` | `Definitions/Maps/Popups/Themes` | Popup theme definitions |
| `MapSections` | `Definitions/Maps/Sections` | Map section definitions |

---

## Refactoring Opportunities

### 1. **Add Validation to ModManifest**

**Suggestion**: Validate contentFolders keys against known types

```csharp
// In ModManifest.Validate()
public void Validate()
{
    // ... existing validation ...

    // Validate contentFolders keys
    var validKeys = new HashSet<string>
    {
        "Root", "Definitions", "Graphics", "Audio", "Scripts", "Fonts", "Tiled", "Tilesets",
        "TileBehaviors", "Behaviors", "Sprites", "MapDefinitions", "AudioDefinitions",
        "PopupBackgrounds", "PopupOutlines", "PopupThemes", "MapSections"
    };

    foreach (var key in ContentFolders.Keys)
    {
        if (!validKeys.Contains(key))
        {
            _logger?.LogWarning(
                "Mod '{Id}' declares unknown contentFolder key: '{Key}'. " +
                "This key will be ignored during content resolution.",
                Id, key);
        }
    }
}
```

**Benefit**: Catch configuration errors early with clear warnings.

---

### 2. **Centralize Valid Keys in ContentProviderOptions**

**Suggestion**: Make valid keys discoverable

```csharp
public class ContentProviderOptions
{
    /// <summary>
    /// Gets all valid content type keys that can be used in mod.json contentFolders.
    /// </summary>
    public IReadOnlySet<string> GetValidContentTypes()
        => BaseContentFolders.Keys.ToHashSet();

    /// <summary>
    /// Checks if a content type key is valid.
    /// </summary>
    public bool IsValidContentType(string contentType)
        => BaseContentFolders.ContainsKey(contentType);
}
```

**Benefit**: Single source of truth for valid keys.

---

### 3. **Fix ModLoader Default Manifest**

**Current** (Lines 619-628):
```csharp
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
```

**Fixed**:
```csharp
contentFolders = new Dictionary<string, string>
{
    // Top-level folders
    ["Definitions"] = "Definitions",
    ["Graphics"] = "Graphics",
    ["Audio"] = "Audio",
    ["Scripts"] = "Scripts",
    ["Fonts"] = "Fonts",
    ["Tiled"] = "Tiled",
    ["Tilesets"] = "Tilesets",

    // Definition subdirectories for fine-grained overrides
    ["TileBehaviors"] = "Definitions/TileBehaviors",
    ["Behaviors"] = "Definitions/Behaviors",
    ["Sprites"] = "Definitions/Sprites",
    ["MapDefinitions"] = "Definitions/Maps/Regions",
    ["AudioDefinitions"] = "Definitions/Audio",
    ["PopupBackgrounds"] = "Definitions/Maps/Popups/Backgrounds",
    ["PopupOutlines"] = "Definitions/Maps/Popups/Outlines",
    ["PopupThemes"] = "Definitions/Maps/Popups/Themes",
    ["MapSections"] = "Definitions/Maps/Sections"
},
```

**Benefit**: Auto-generated base manifests will support all content types.

---

### 4. **Update Documentation Examples**

**Files to Update**:
1. `ModManifest.cs` (Line 93)
2. `docs/architecture/base-game-as-mod-architecture.md` (Lines 146-155, 228-237)
3. `docs/IMPLEMENTATION-GUIDE.md` (Lines 200-217)

**Example Fix for ModManifest.cs**:
```csharp
/// <summary>
///     Relative paths to folders containing new content.
///     Key must match a valid content type from ContentProviderOptions.BaseContentFolders.
///
///     Valid keys include:
///     - Top-level: "Definitions", "Graphics", "Audio", "Scripts", "Fonts", "Tiled", "Tilesets"
///     - Subdirectories: "TileBehaviors", "Behaviors", "Sprites", "MapDefinitions", etc.
///
///     Example: { "TileBehaviors": "content/tiles", "Graphics": "content/graphics" }
/// </summary>
[JsonPropertyName("contentFolders")]
public Dictionary<string, string> ContentFolders { get; init; } = new();
```

---

## Positive Findings

### 1. **Well-Designed Architecture**

✅ **ContentProvider** correctly implements priority-based resolution
✅ **LRU caching** for performance optimization
✅ **Security validation** (path traversal checks)
✅ **Clear separation of concerns** (ModLoader ↔ ContentProvider ↔ ContentProviderOptions)

### 2. **Comprehensive Content Type Support**

✅ Supports both coarse-grained (`Definitions`) and fine-grained (`TileBehaviors`) overrides
✅ Allows mods to override specific subdirectories without duplicating entire trees

### 3. **Good Error Handling**

✅ Null-safe path resolution
✅ File existence checks at every step
✅ Detailed logging for debugging

---

## Recommended Actions (Priority Order)

| Priority | Action | File | Effort | Impact |
|----------|--------|------|--------|--------|
| **HIGH** | Fix CreateDefaultBaseManifestAsync | ModLoader.cs:619-628 | 10 min | Ensures base game supports all content types |
| **HIGH** | Update ModManifest.cs documentation | ModManifest.cs:90-94 | 5 min | Prevents mod developer confusion |
| **MEDIUM** | Add contentFolders validation | ModManifest.cs:Validate() | 30 min | Early error detection |
| **MEDIUM** | Update architecture docs | base-game-as-mod-architecture.md | 20 min | Accurate documentation |
| **LOW** | Add GetValidContentTypes() | ContentProviderOptions.cs | 15 min | Developer convenience |

**Total Estimated Effort**: 1.5 hours

---

## Testing Recommendations

### Unit Tests Needed

1. **Test contentFolders key validation**
   ```csharp
   [Fact]
   public void Validate_InvalidContentFolderKey_LogsWarning()
   {
       var manifest = new ModManifest
       {
           Id = "test.mod",
           Name = "Test",
           ContentFolders = new() { ["InvalidKey"] = "path" }
       };

       // Assert: Should log warning about unknown key
   }
   ```

2. **Test subdirectory resolution**
   ```csharp
   [Fact]
   public void ResolveContentPath_TileBehaviors_FindsInSubdirectory()
   {
       // Arrange: Create mod with TileBehaviors folder
       // Act: Resolve "TileBehaviors" content type
       // Assert: Correct path returned
   }
   ```

3. **Test base game manifest auto-generation**
   ```csharp
   [Fact]
   public async Task LoadBaseGameAsync_CreatesManifestWithAllContentTypes()
   {
       // Act: Load base game without existing mod.json
       // Assert: Generated manifest has all 16 content type keys
   }
   ```

---

## Conclusion

The modding system architecture is fundamentally sound, but there are **3 critical documentation/implementation inconsistencies** that could confuse mod developers:

1. **Mismatch between documentation examples and valid keys**
2. **Incomplete auto-generated base game manifests**
3. **Missing validation for invalid contentFolders keys**

These issues can be resolved in approximately **1.5 hours** of focused development work with minimal risk of breaking changes.

### Quality Metrics

- **Code Readability**: 8/10 (clear naming, good comments)
- **Maintainability**: 7/10 (slight coupling between ContentProviderOptions and docs)
- **Security**: 9/10 (excellent path traversal protection)
- **Performance**: 9/10 (LRU caching, O(1) cache lookups)
- **Documentation Accuracy**: 5/10 ⚠️ (critical mismatches found)

**Overall Assessment**: The implementation is production-ready but needs documentation updates and validation improvements before external mod developers use the system.
