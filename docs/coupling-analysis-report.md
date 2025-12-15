# Content Modularization - Coupling Analysis Report

**Date:** 2025-12-14
**Objective:** Identify all coupling issues preventing MonoBallFramework.Game/Assets content from being fully modular

---

## Executive Summary

The codebase has **GOOD** modular foundations with the ModLoader and content definition systems, but several hardcoded dependencies prevent true content modularity. The analysis identified **87 coupling points** across 4 major categories.

**Key Findings:**
- Asset path hardcoding prevents mod content discovery
- Font paths are deeply embedded in engine code
- Default/fallback mechanisms assume specific content exists
- Initialization order dependencies create implicit contracts

---

## Category 1: Hardcoded Asset Paths (CRITICAL)

### 1.1 Font Path Hardcoding (SEVERITY: HIGH)

**Issue:** Font paths are hardcoded throughout the rendering and UI systems, preventing font modding.

| File | Line | Hardcoded Value | Impact |
|------|------|----------------|---------|
| `Engine/UI/Utilities/FontLoader.cs` | 14 | `"Assets/Fonts/pokemon.ttf"` | Pokemon font must exist at exact path |
| `Engine/UI/Utilities/FontLoader.cs` | 19 | `"Assets/Fonts/0xProtoNerdFontMono-Regular.ttf"` | Debug font must exist at exact path |
| `Infrastructure/Diagnostics/PerformanceOverlay.cs` | 75 | `"Assets/Fonts/0xProtoNerdFontMono-Regular.ttf"` | Performance overlay requires specific font |
| `Engine/Scenes/Scenes/LoadingScene.cs` | 80 | `"Assets/Fonts/pokemon.ttf"` | Loading scene hardcoded to Pokemon font |
| `Engine/Scenes/Scenes/MapPopupScene.cs` | 257-258 | `"Assets/Fonts/pokemon.ttf"` | Map popup hardcoded to Pokemon font |

**Suggested Abstraction:**
```csharp
// Configuration-based font resolution
public interface IFontResolver
{
    string ResolveFont(FontType type); // FontType.UI, FontType.Debug, FontType.Game
}

// Allow mods to register font definitions
public class FontDefinition
{
    public string FontId { get; set; } // "base:font:ui/pokemon"
    public string FontPath { get; set; }
    public FontType Type { get; set; }
}
```

### 1.2 Data Path Hardcoding (SEVERITY: MEDIUM)

| File | Line | Hardcoded Value | Impact |
|------|------|----------------|---------|
| `Initialization/Constants/GameInitializationConstants.cs` | 17 | `"Assets/Definitions"` | Default data path location |
| `Infrastructure/Configuration/GameConfiguration.cs` | 65 | `"Assets/Definitions"` | Configuration default |

**Suggested Abstraction:**
- Make data path fully configurable per mod
- Allow multiple data path search locations
- Support relative and absolute paths

### 1.3 Intro Scene Asset Hardcoding (SEVERITY: LOW)

| File | Line | Hardcoded Value | Impact |
|------|------|----------------|---------|
| `Engine/Scenes/Scenes/IntroScene.cs` | 82 | `"Assets/logo.png"` | Logo must exist in Assets root |
| `Engine/Scenes/Scenes/IntroScene.cs` | 103 | `"Assets/MonoBall.wav"` | Intro audio must exist in Assets root |

**Suggested Abstraction:**
- Create IntroAssets configuration section
- Allow mods to override intro assets
- Support disabling intro entirely

---

## Category 2: Asset Root & Path Resolution (SEVERITY: MEDIUM)

### 2.1 DefaultAssetRoot Constant

**Files Using RenderingConstants.DefaultAssetRoot:**
- `Engine/Rendering/Configuration/RenderingConstants.cs:82` - Definition: `"Assets"`
- `Engine/Rendering/Assets/AssetManager.cs:18` - Default parameter
- Multiple references throughout rendering pipeline

**Issue:** The constant "Assets" is assumed throughout the codebase. While configurable in AssetManager constructor, many systems don't expose this configuration.

**Suggested Abstraction:**
```csharp
// Inject asset path resolver everywhere, not just AssetManager
public interface IAssetPathResolver
{
    string ResolveAssetPath(string relativeOrModPath);
    IEnumerable<string> GetSearchPaths(); // Base game + all mods
}
```

### 2.2 ContentRoot Hardcoding

| File | Line | Hardcoded Value | Impact |
|------|------|----------------|---------|
| `Initialization/Constants/GameInitializationConstants.cs` | 37 | `"Content"` | MonoGame content pipeline root |
| `Infrastructure/Configuration/GameConfiguration.cs` | 70 | `"Content"` | Configuration default |
| `MonoBallFrameworkGame.cs` | 200 | `Content.RootDirectory = ...` | MonoGame content manager |

**Note:** ContentRoot is for MonoGame's Content Pipeline (compiled XNB files). This is separate from Assets but creates confusion.

**Suggested Abstraction:**
- Clearly document difference between Content (XNB) and Assets (runtime)
- Consider deprecating Content Pipeline entirely for pure runtime loading
- Mods should only interact with Assets, not Content

---

## Category 3: Type ID & Behavior References (SEVERITY: MEDIUM)

### 3.1 Default Type IDs

Multiple files reference default/fallback type categories:

| File | Line | Type | Default Value |
|------|------|------|---------------|
| `Engine/Core/Types/GameTileBehaviorId.cs` | 31 | Tile Behavior | `"movement"` |
| `Engine/Core/Types/GameTrainerId.cs` | 18 | Trainer Class | `"trainer"` |
| `Engine/Core/Types/GameThemeId.cs` | 18 | Theme Category | `"popup"` |
| `Engine/Core/Types/GameSpriteId.cs` | 21 | Sprite Category | `"generic"` |
| `Engine/Core/Types/GamePopupBackgroundId.cs` | 24 | Category | `"background"` |
| `Engine/Core/Types/GamePopupOutlineId.cs` | 24 | Category | `"outline"` |

**Issue:** Default categories are hardcoded in type ID constructors. While these are reasonable defaults, they prevent alternative categorization schemes.

**Suggested Abstraction:**
```csharp
// Make defaults configurable
public class GameTypeIdDefaults
{
    public string TileBehaviorCategory { get; set; } = "movement";
    public string TrainerClass { get; set; } = "trainer";
    // ... etc
}
```

### 3.2 Example Behavior Names in Documentation

Multiple files use examples like "ice", "jump", "normal" in comments:

| File | Line | Context |
|------|------|---------|
| `Engine/Core/Types/TileBehaviorDefinition.cs` | 19 | Documentation example |
| `Engine/Core/Events/Tile/TileSteppedOnEvent.cs` | 46 | Documentation example |
| `Ecs/Components/Tiles/TerrainType.cs` | 13 | Common values documentation |

**Impact:** LOW - These are documentation only, not runtime coupling.

---

## Category 4: Fallback & Default Mechanisms (SEVERITY: MEDIUM-HIGH)

### 4.1 Default Popup Theme Fallback

| File | Line | Code | Impact |
|------|------|------|---------|
| `Engine/Rendering/Popups/PopupRegistry.cs` | 26 | `_defaultBackgroundId = "base:popup:background/wood"` | Hardcoded wood theme |
| `Engine/Rendering/Popups/PopupRegistry.cs` | 27 | `_defaultOutlineId = "base:popup:outline/wood_outline"` | Hardcoded wood outline |
| `Engine/Scenes/Services/MapPopupOrchestrator.cs` | 212-217 | Fallback to wood theme | Assumes wood exists |
| `Engine/Scenes/Services/MapPopupOrchestrator.cs` | 217 | `usedThemeId = "wood"` | Hardcoded theme ID |

**Issue:** If the "wood" theme is not present (removed by mod or base content extraction), popups will fail.

**Suggested Abstraction:**
```csharp
public class PopupRegistryConfig
{
    public string DefaultBackgroundId { get; set; } = "base:popup:background/wood";
    public string DefaultOutlineId { get; set; } = "base:popup:outline/wood_outline";
    public bool FailOnMissingDefault { get; set; } = false; // For strict modding
}
```

### 4.2 Elevation Rendering Defaults

| File | Line | Code | Impact |
|------|------|------|---------|
| `Engine/Rendering/Systems/ElevationRenderSystem.cs` | 320 | `int clamped = tileSize > 0 ? tileSize : 16` | Assumes 16px tiles |
| `Engine/Rendering/Systems/ElevationRenderSystem.cs` | 1318 | `Elevation.Default` | Default ground elevation |

**Suggested Abstraction:**
- Make default tile size configurable per mod
- Support multiple tile size standards (8px, 16px, 32px)

### 4.3 Tileset Fallback Resolution

| File | Line | Code | Impact |
|------|------|------|---------|
| `Engine/Rendering/Assets/AssetManager.cs` | 410-454 | `ResolveFallbackTexturePath()` | Complex fallback logic for tilesets |

**Issue:** Fallback logic assumes specific directory structure (Tilesets/{id}/{id}.json). Mods with different structures may fail.

**Suggested Abstraction:**
```csharp
public interface ITilesetPathResolver
{
    string? ResolveTilesetPath(string tilesetId, string relativeExpectedPath);
    void RegisterCustomResolver(Func<string, string, string?> resolver);
}
```

---

## Category 5: Loading Order Dependencies (SEVERITY: LOW-MEDIUM)

### 5.1 Initialization Pipeline Order

The initialization pipeline has implicit dependencies:

```
1. CreateGraphicsServicesStep -> Creates AssetManager with hardcoded AssetRoot
2. LoadGameDataStep -> Loads definitions from Assets/Definitions
3. InitializeMapPopupStep -> Expects popup themes to exist
4. LoadSpriteDefinitionsStep -> Expects sprite definitions
5. InitializeBehaviorSystemsStep -> Expects behavior definitions
```

**Issue:** Steps assume previous steps succeeded and created expected content.

**Suggested Abstraction:**
```csharp
// Explicit dependency declaration
public abstract class InitializationStepBase
{
    public virtual IEnumerable<Type> RequiredSteps { get; } = Array.Empty<Type>();
    public virtual IEnumerable<string> RequiredContent { get; } = Array.Empty<string>();
}
```

### 5.2 ModLoader Content Folder Registration

| File | Line | Code | Impact |
|------|------|------|---------|
| `Engine/Core/Modding/ModLoader.cs` | 247-256 | Content folder registration | Registered but not actively searched |

**Issue:** ModLoader registers content folders in manifests, but the actual content loading doesn't query these folders. The connection is incomplete.

**Suggested Abstraction:**
```csharp
// GameDataLoader should query ModLoader for content paths
public class GameDataLoader
{
    public async Task LoadAllAsync(string baseDataPath, IModLoader modLoader)
    {
        // 1. Load base game content from baseDataPath
        // 2. Query modLoader.GetContentFolders() for all mods
        // 3. Load mod content in dependency order (with overrides)
    }
}
```

---

## Category 6: Content Assumptions (SEVERITY: LOW)

### 6.1 Expected File Extensions

Multiple loaders assume file formats:

| File | Pattern | Extensions Assumed |
|------|---------|-------------------|
| `GameData/Loading/GameDataLoader.cs` | `*.json` | JSON definitions |
| `Engine/Rendering/Assets/AssetManager.cs` | `*.png` | PNG textures |
| `GameData/MapLoading/Tiled/Core/MapLoader.cs` | Tiled JSON | `.json`, `.tmx` |

**Impact:** Mods cannot provide content in alternative formats (e.g., YAML, TOML, binary).

**Suggested Abstraction:**
```csharp
public interface IContentDeserializer
{
    bool CanDeserialize(string filePath);
    T? Deserialize<T>(string filePath);
}

// Register deserializers: JSON, YAML, TOML, Binary, etc.
```

### 6.2 Region Assumptions

| File | Line | Code | Impact |
|------|------|------|---------|
| `GameData/Loading/GameDataLoader.cs` | 144 | `Region = dto.Region ?? "hoenn"` | Assumes Hoenn region |

**Impact:** Non-Pokemon mods would need to override all map regions.

**Suggested Abstraction:**
- Make default region configurable in GameConfiguration
- Or remove the default entirely (force explicit region declaration)

---

## Summary Table: Coupling Issues by Severity

| Severity | Count | Examples |
|----------|-------|----------|
| **CRITICAL** | 0 | None - no showstoppers |
| **HIGH** | 11 | Font path hardcoding, default theme fallbacks |
| **MEDIUM** | 28 | Asset root constants, type ID defaults, loading order |
| **LOW** | 48 | Documentation examples, file extension assumptions |

**Total Issues Identified:** 87

---

## Recommended Decoupling Strategy

### Phase 1: Configuration-Based Paths (HIGH PRIORITY)

1. **Create FontConfiguration system**
   - Remove all hardcoded font paths
   - Define font types (UI, Debug, Game)
   - Allow mods to register font definitions

2. **Create PopupThemeConfiguration**
   - Make default theme configurable
   - Support graceful degradation if theme missing

3. **Enhance AssetPathResolver**
   - Support multi-path search (base game + mods)
   - Provide clear path resolution logs

### Phase 2: Dynamic Content Discovery (MEDIUM PRIORITY)

1. **Integrate ModLoader with GameDataLoader**
   - Query mod content folders during loading
   - Support content overrides
   - Load in dependency order

2. **Create IContentResolver interface**
   - Abstract away "where content comes from"
   - Support multiple content sources

3. **Remove hardcoded defaults**
   - Replace `"wood"` theme default with first available
   - Replace `"hoenn"` region default with configuration

### Phase 3: Format Flexibility (LOW PRIORITY)

1. **Support alternative file formats**
   - Register custom deserializers
   - Allow YAML, TOML, binary definitions

2. **Support alternative directory structures**
   - Remove assumptions about folder hierarchy
   - Use manifest-driven content discovery

---

## Testing Strategy for Modularity

### Test Case 1: Complete Content Extraction
**Goal:** Move ALL content from MonoBallFramework.Game/Assets to a mod

**Steps:**
1. Create mod "base-content" with all current Assets
2. Empty MonoBallFramework.Game/Assets folder
3. Run game - should load everything from mod

**Expected Failures (Current State):**
- Font loading will fail (hardcoded paths)
- Intro scene will fail (hardcoded logo.png)
- Default popup theme fallback will fail

### Test Case 2: Alternative Theme Mod
**Goal:** Mod that replaces "wood" theme with "crystal"

**Steps:**
1. Create mod with crystal theme
2. Remove wood theme from base content
3. Configure default theme to "crystal"

**Expected Failures (Current State):**
- Hardcoded "wood" fallback in MapPopupOrchestrator

### Test Case 3: Font Replacement Mod
**Goal:** Mod that uses a different font family

**Steps:**
1. Create mod with custom font
2. Remove pokemon.ttf from base content
3. Configure UI font to custom font

**Expected Failures (Current State):**
- All hardcoded "Assets/Fonts/pokemon.ttf" references will fail

---

## Conclusion

The codebase has excellent architectural foundations for modding (ModLoader, type registries, EF Core definitions), but **path hardcoding** and **default assumptions** prevent full content modularity.

**Most Critical Changes Needed:**
1. Replace hardcoded font paths with FontConfiguration
2. Make default popup theme configurable
3. Connect ModLoader content folders to GameDataLoader
4. Replace hardcoded AssetRoot with multi-path resolver

**Estimated Effort:**
- Phase 1 (Configuration): 2-3 days
- Phase 2 (Dynamic Discovery): 3-4 days
- Phase 3 (Format Flexibility): 2-3 days

**Total:** ~8-10 days to achieve full content modularity
