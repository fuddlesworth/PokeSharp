# JSON Loading Patterns Analysis - PokeSharp

## Executive Summary

The codebase has **4 parallel JSON loading pathways** that operate independently with **significant redundancy in JsonSerializerOptions creation**. This analysis maps all JSON operations and identifies optimization opportunities.

---

## 1. JSON Loading Pathways

### Pathway 1: GameDataLoader - EF Core Database Loading

**File**: `/mnt/c/Users/nate0/RiderProjects/PokeSharp/MonoBallFramework.Game/GameData/Loading/GameDataLoader.cs`

**Purpose**: Loads game entity definitions into EF Core in-memory database

**What it loads**:
- NPCs (JSON → NpcDefinition → EF Core)
- Trainers (JSON → TrainerDefinition → EF Core)
- Maps (JSON → MapDefinition → EF Core)
- Popup Themes (JSON → PopupTheme → EF Core)
- Map Sections (JSON → MapSection → EF Core)
- Audio Definitions (JSON → AudioDefinition → EF Core)

**Methods**:
- `LoadAllAsync()` - Entry point
- `LoadNpcsAsync()` - Lines 74-144
- `LoadTrainersAsync()` - Lines 149-227
- `LoadMapDefinitionsAsync()` - Lines 234-319
- `LoadPopupThemesAsync()` - Lines 561-625
- `LoadMapSectionsAsync()` - Lines 630-695
- `LoadAudioDefinitionsAsync()` - Lines 701-784

**JsonSerializerOptions**:
```csharp
// Lines 25-31: Single instance cached in constructor
_jsonOptions = new JsonSerializerOptions
{
    PropertyNameCaseInsensitive = true,
    ReadCommentHandling = JsonCommentHandling.Skip,
    AllowTrailingCommas = true,
    WriteIndented = true,
};
```
✅ **GOOD**: Options cached as instance field, reused for all deserialization

**Deserialization Calls** (Lines 92-94, 167-170, 260, 581, 650, 721):
- 6 different JsonSerializer.Deserialize calls
- All reuse `_jsonOptions` - **No redundancy here**

**File Paths Scanned**:
- `{dataPath}/NPCs/**/*.json`
- `{dataPath}/Trainers/**/*.json`
- `{dataPath}/Maps/Regions/**/*.json`
- `{dataPath}/Maps/Popups/Themes/*.json`
- `{dataPath}/Maps/Sections/*.json`
- `{dataPath}/Audio/**/*.json`

---

### Pathway 2: SpriteRegistry - Sprite Definition Loading

**File**: `/mnt/c/Users/nate0/RiderProjects/PokeSharp/MonoBallFramework.Game/GameData/Sprites/SpriteRegistry.cs`

**Purpose**: Loads sprite definitions from JSON for animation system

**What it loads**:
- Sprite metadata (ID, animation paths, dimensions, etc.)
- Stores in ConcurrentDictionary for O(1) lookup

**Methods**:
- `LoadDefinitionsAsync()` - Async entry point (Lines 155-201)
- `LoadDefinitions()` - Sync entry point (Lines 136-141)
- `LoadSpritesRecursiveAsync()` - Parallel recursive loading (Lines 206-246)
- `LoadDefinitionsFromJson()` - Sync loading (Lines 251-293)

**JsonSerializerOptions - ASYNC PATH** (Lines 185-189):
```csharp
var options = new JsonSerializerOptions
{
    PropertyNameCaseInsensitive = true,
    ReadCommentHandling = JsonCommentHandling.Skip
};
```
❌ **PROBLEM**: Created fresh for EACH async load (line 185)
- Reused across all recursive file loads (line 192 parameter)
- But recreated if LoadDefinitionsAsync called multiple times

**JsonSerializerOptions - SYNC PATH** (Lines 261-265):
```csharp
var options = new JsonSerializerOptions
{
    PropertyNameCaseInsensitive = true,
    ReadCommentHandling = JsonCommentHandling.Skip
};
```
❌ **PROBLEM**: Created fresh for each sync load
- Not reused across loop iterations (lines 272-292)
- **REDUNDANT**: Options recreated inside foreach loop

**Deserialization Calls**:
- Async: Line 224 - Inside parallel task lambda
- Sync: Line 277 - Inside foreach loop
- Both reuse options parameter

**File Path Scanned**:
- `{AssetPath}/Sprites/**/*.json` (recursive)

---

### Pathway 3: PopupRegistry - Popup UI Definition Loading

**File**: `/mnt/c/Users/nate0/RiderProjects/PokeSharp/MonoBallFramework.Game/Engine/Rendering/Popups/PopupRegistry.cs`

**Purpose**: Loads popup background and outline definitions

**What it loads**:
- Popup background definitions (texture, border styles)
- Popup outline definitions (decorative outlines)
- Stores in separate ConcurrentDictionaries

**Methods**:
- `LoadDefinitionsAsync()` - Async entry point (Lines 172-213)
- `LoadDefinitions()` - Sync entry point (Lines 153-160)
- `LoadBackgroundsAsync()` - Background parallel loading (Lines 218-241)
- `LoadOutlinesAsync()` - Outline parallel loading (Lines 246-269)
- `LoadDefinitionsFromJson()` - Sync loading (Lines 271-350)

**JsonSerializerOptions - ASYNC PATH** (Lines 192-196):
```csharp
var options = new JsonSerializerOptions
{
    PropertyNameCaseInsensitive = true,
    ReadCommentHandling = JsonCommentHandling.Skip
};
```
✅ **GOOD**: Created once, passed to both background and outline loaders
- Line 202: `Task backgroundsTask = LoadBackgroundsAsync(backgroundsPath, options, cancellationToken);`
- Line 203: `Task outlinesTask = LoadOutlinesAsync(outlinesPath, options, cancellationToken);`

**JsonSerializerOptions - SYNC PATH** (Lines 295-299, 327-331):
```csharp
var options = new JsonSerializerOptions
{
    PropertyNameCaseInsensitive = true,
    ReadCommentHandling = JsonCommentHandling.Skip
};
```
❌ **CRITICAL PROBLEM**: Options created TWICE in sync path
- Line 295-299: Inside first foreach loop (backgrounds)
- Line 327-331: Inside second foreach loop (outlines)
- **Both recreated for each file**

**File Paths Scanned**:
- `{AppContext.BaseDirectory}/Assets/Definitions/Maps/Popups/Backgrounds/*.json`
- `{AppContext.BaseDirectory}/Assets/Definitions/Maps/Popups/Outlines/*.json`

---

### Pathway 4: TypeRegistry - Behavior Script Definition Loading

**File**: `/mnt/c/Users/nate0/RiderProjects/PokeSharp/MonoBallFramework.Game/Engine/Core/Types/TypeRegistry.cs`

**Purpose**: Loads type definitions for moddable behavior scripts

**What it loads**:
- Behavior definitions (NPC types, item types, status types, etc.)
- Stores definitions + compiled script instances

**Methods**:
- `LoadAllAsync()` - Async entry point (Lines 98-128)
- `RegisterFromJsonAsync()` - Single file loader (Lines 136-168)

**JsonSerializerOptions** (Lines 139-144):
```csharp
var options = new JsonSerializerOptions
{
    PropertyNameCaseInsensitive = true,
    ReadCommentHandling = JsonCommentHandling.Skip,
    AllowTrailingCommas = true,
};
```
❌ **PROBLEM**: Created fresh for EACH JSON file deserialization
- Lines 109-120: Loop through jsonFiles
- Line 113: `await RegisterFromJsonAsync(jsonPath);`
- Line 146: Options created INSIDE RegisterFromJsonAsync
- **Recreated for every file** in LoadAllAsync

**Deserialization Call**:
- Line 146: `T? definition = JsonSerializer.Deserialize<T>(json, options);`

**File Path Scanned**:
- `{dataPath}/**/*.json` (recursive)

---

## 2. Initialization Order & Race Conditions

### Pipeline Execution Order

**File**: `/mnt/c/Users/nate0/RiderProjects/PokeSharp/MonoBallFramework.Game/MonoBallFrameworkGame.cs`

```
1. LoadGameDataStep (async)
   ├─ GameDataLoader.LoadAllAsync()
   │  ├─ Loads: NPCs, Trainers, Maps, Popup Themes, Sections, Audio
   │  └─ Stores: EF Core in-memory database
   └─ Preloads popup cache

2. LoadSpriteDefinitionsStep (async)
   └─ SpriteRegistry.LoadDefinitionsAsync()
      └─ Loads: Sprite definitions from JSON files

3. LoadSpriteTexturesStep
   └─ Loads texture assets (not JSON-related)

4. InitializeMapPopupStep (async)
   ├─ PopupRegistry.LoadDefinitionsAsync()
   │  ├─ Loads: Popup backgrounds + outlines (parallel)
   │  └─ Creates new PopupRegistry instance
   ├─ Preloads pokemon font
   └─ Creates MapPopupOrchestrator
```

### Race Condition Analysis

**POTENTIAL ISSUE**: PopupRegistry instantiated in InitializeMapPopupStep (line 40):
```csharp
var popupRegistry = new PopupRegistry();
await popupRegistry.LoadDefinitionsAsync(cancellationToken);
```

**Risk**: If `InitializeMapPopupStep` runs concurrently with `LoadGameDataStep`:
- GameDataLoader loads popup themes into EF Core (different storage)
- PopupRegistry loads popup backgrounds/outlines separately
- **No direct conflict** (different data sources)
- But popup themes in EF Core are NOT the same as PopupRegistry backgrounds/outlines

**ARCHITECTURAL ISSUE**:
- Popup data loaded in TWO different ways:
  1. GameDataLoader → EF Core (PopupTheme entity)
  2. PopupRegistry → In-memory ConcurrentDictionary (PopupBackground/Outline)
- These are separate systems without synchronization

---

## 3. JsonSerializerOptions Redundancy Summary

### Options Creation Locations

| Registry | Location | Creation Count | Caching | Issue |
|----------|----------|-----------------|---------|-------|
| **GameDataLoader** | Constructor (lines 25-31) | 1 instance | Yes ✅ | None - reused across all 6 data types |
| **SpriteRegistry async** | LoadDefinitionsAsync (lines 185-189) | 1 per call | Partial | Recreated if method called multiple times |
| **SpriteRegistry sync** | LoadDefinitionsFromJson (lines 261-265) | 1 per call | No ❌ | Created once, used across all sprites |
| **PopupRegistry async** | LoadDefinitionsAsync (lines 192-196) | 1 per call | Yes ✅ | Passed to both background/outline loaders |
| **PopupRegistry sync** | LoadDefinitionsFromJson (lines 295-299, 327-331) | 2 per call | No ❌ | **CRITICAL**: Created twice (once per loop) |
| **TypeRegistry** | RegisterFromJsonAsync (lines 139-144) | 1 per file | No ❌ | **WORST**: Recreated for every single JSON file |

### Configuration Differences

All registries use nearly identical options:
```csharp
PropertyNameCaseInsensitive = true
ReadCommentHandling = JsonCommentHandling.Skip
```

Only variations:
- GameDataLoader: `AllowTrailingCommas = true`, `WriteIndented = true`
- TypeRegistry: `AllowTrailingCommas = true`

---

## 4. Redundant JSON File Reads

### Files Read Multiple Times

**NONE DETECTED** - Each registry reads from different directories:

| Registry | Directory | Overlap | Notes |
|----------|-----------|---------|-------|
| GameDataLoader | `{dataPath}/NPCs/**` | None | NPC definitions only |
| GameDataLoader | `{dataPath}/Trainers/**` | None | Trainer definitions only |
| GameDataLoader | `{dataPath}/Maps/Regions/**` | None | Map definitions only |
| GameDataLoader | `{dataPath}/Maps/Popups/Themes/**` | None | Popup themes only |
| GameDataLoader | `{dataPath}/Maps/Sections/**` | None | Map sections only |
| GameDataLoader | `{dataPath}/Audio/**` | None | Audio definitions only |
| SpriteRegistry | `{AssetPath}/Sprites/**` | None | Sprite definitions |
| PopupRegistry | `{AppBase}/Assets/Definitions/Maps/Popups/Backgrounds/**` | None | Popup backgrounds |
| PopupRegistry | `{AppBase}/Assets/Definitions/Maps/Popups/Outlines/**` | None | Popup outlines |
| TypeRegistry | `{dataPath}/**` | **POTENTIAL** | Generic recursive scan |

**TypeRegistry Issue**: Uses `SearchOption.AllDirectories` on dataPath (line 106)
- Could include NPC, Trainer, Map, Popup JSON files if mixed in same directory
- No filename filtering (reads all *.json)
- **Could cause duplicate loading if behavior definitions are in same folder as NPCs**

---

## 5. Deserialization Patterns

### Deserialization Call Sites

**GameDataLoader** (Best practice):
```csharp
// Line 92-95: Reuses cached _jsonOptions
NpcDefinitionDto? dto = JsonSerializer.Deserialize<NpcDefinitionDto>(json, _jsonOptions);

// Same pattern repeated for Trainers (line 167), Maps (line 260), Themes (line 581), etc.
```

**SpriteRegistry Async** (Good):
```csharp
// Lines 185-189: Options created once
var options = new JsonSerializerOptions { ... };

// Lines 218-245: Options passed to all parallel tasks
var definition = JsonSerializer.Deserialize<SpriteDefinition>(json, options);
```

**SpriteRegistry Sync** (Inefficient):
```csharp
// Lines 261-265: Options created once
var options = new JsonSerializerOptions { ... };

// Lines 272-292: Reused in foreach, but inefficient placement
// Correct placement would be outside loop
SpriteDefinition? definition = JsonSerializer.Deserialize<SpriteDefinition>(json, options);
```

**PopupRegistry Sync** (Critical issue):
```csharp
// Lines 290-291, 295-299: Background loop
foreach (string jsonFile in Directory.GetFiles(backgroundsPath, "*.json"))
{
    var options = new JsonSerializerOptions { ... };  // ❌ RECREATED EACH ITERATION
    var definition = JsonSerializer.Deserialize<PopupBackgroundDefinition>(json, options);
}

// Lines 322-331: Outline loop (same pattern)
foreach (string jsonFile in Directory.GetFiles(outlinesPath, "*.json"))
{
    var options = new JsonSerializerOptions { ... };  // ❌ RECREATED EACH ITERATION
    var definition = JsonSerializer.Deserialize<PopupOutlineDefinition>(json, options);
}
```

**TypeRegistry** (Worst case):
```csharp
// Lines 109-120: Loop through all JSON files
foreach (string jsonPath in jsonFiles)
{
    await RegisterFromJsonAsync(jsonPath);  // Calls method below
}

// Lines 136-168: Inside RegisterFromJsonAsync
private async Task RegisterFromJsonAsync(string jsonPath)
{
    var options = new JsonSerializerOptions { ... };  // ❌ RECREATED FOR EVERY FILE
    T? definition = JsonSerializer.Deserialize<T>(json, options);
}
```

---

## 6. Thread Safety Analysis

### Concurrent Access Patterns

**GameDataLoader**:
- ✅ Sequential loading (no concurrency)
- ✅ EF Core SaveChangesAsync called after each type load
- ✅ No shared mutable state between types

**SpriteRegistry**:
- ✅ SemaphoreSlim prevents concurrent LoadDefinitionsAsync calls (lines 27, 165-200)
- ✅ Parallel file I/O within single load (lines 218-245)
- ✅ ConcurrentDictionary used for registration (line 25)
- ⚠️ Race condition possible if IsLoaded flag checked before lock acquired

**PopupRegistry**:
- ✅ SemaphoreSlim prevents concurrent LoadDefinitionsAsync calls (lines 27, 178-212)
- ✅ Backgrounds and outlines loaded in parallel (lines 202-205)
- ✅ ConcurrentDictionaries used for both (lines 23-26)
- ✅ No visible race conditions

**TypeRegistry**:
- ✅ ConcurrentDictionary for definitions (line 27)
- ✅ ConcurrentDictionary for scripts (line 29)
- ⚠️ LoadAllAsync not guarded - concurrent calls could cause duplicate deserializations
- No SemaphoreSlim to prevent concurrent LoadAllAsync

---

## 7. Memory & Performance Impact

### Estimated JsonSerializerOptions Allocations

**During Initialization**:

| Registry | Scenario | Count | Total |
|----------|----------|-------|-------|
| GameDataLoader | All NPCs+Trainers+Maps+Themes+Sections+Audio | 1 | 1 object |
| SpriteRegistry async | 500 sprite files | 1 | 1 object |
| SpriteRegistry sync | 500 sprite files | 1 | 1 object |
| PopupRegistry async | 20 backgrounds + 20 outlines | 1 | 1 object |
| PopupRegistry sync | 20 backgrounds + 20 outlines | **40** | 40 objects ❌ |
| TypeRegistry | 200 behavior definitions | **200** | 200 objects ❌ |

**Total Unnecessary Allocations**: ~240 JsonSerializerOptions objects
- Each ~500-1000 bytes
- **Total waste**: 120-240 KB during initialization

**Runtime Impact**:
- GC pressure from 240 temporary objects
- CPU time creating options (reflection, property cache setup)
- Could add 10-50ms to initialization time

### Optimization Potential

**Quick wins**:
1. **PopupRegistry.LoadDefinitionsFromJson()**: Move options creation outside loops
   - Saves 40 allocations
   - Estimated: +1-2ms initialization improvement

2. **TypeRegistry.RegisterFromJsonAsync()**: Create single options instance in LoadAllAsync
   - Saves 200 allocations
   - Estimated: +5-10ms initialization improvement
   - Also prevents concurrent LoadAllAsync race condition

3. **SpriteRegistry.LoadDefinitionsFromJson()**: Already good, just document

**Large-scale optimization**:
4. **Shared static JsonSerializerOptions**: Create once globally
   - Could reduce to 1-2 instances total
   - Requires careful design for conflicting options
   - Estimated: +20-30ms improvement if hundreds of files

---

## 8. Current Issues Summary

### Critical Issues

1. **TypeRegistry redundancy** (Lines 139-144 of TypeRegistry.cs)
   - Creates new options for EVERY JSON file
   - Example: 200 behavior files = 200 JsonSerializerOptions allocations
   - **Fix**: Cache options in LoadAllAsync, pass to RegisterFromJsonAsync

2. **PopupRegistry sync redundancy** (Lines 295-299, 327-331 of PopupRegistry.cs)
   - Creates options twice per LoadDefinitionsFromJson() call
   - Inside foreach loops (recreated for EACH file)
   - **Fix**: Create once before loops

3. **TypeRegistry missing concurrency guard** (Lines 98-128)
   - LoadAllAsync can be called concurrently
   - No SemaphoreSlim like SpriteRegistry/PopupRegistry
   - Could cause duplicate deserializations and race conditions
   - **Fix**: Add SemaphoreSlim guard (add _loadLock field)

### Medium Issues

4. **Data model duplication**: Popup data stored in TWO places
   - PopupTheme entities in EF Core (GameDataLoader)
   - PopupBackground/Outline in PopupRegistry
   - These are independent - no synchronization
   - **Risk**: If popup definitions updated, only one system sees changes

### Minor Issues

5. **SpriteRegistry has both sync and async paths** (Lines 136-141, 155-201)
   - Sync version has redundant options creation
   - Async version is fine
   - **Risk**: Developers might use sync path unintentionally

6. **Path construction inconsistencies**:
   - GameDataLoader: Uses `Path.Combine(dataPath, ...)`
   - PopupRegistry: Uses `Path.Combine(AppContext.BaseDirectory, "Assets", ...)`
   - SpriteRegistry: Uses `_pathResolver.ResolveData("Sprites")`
   - TypeRegistry: Uses constructor-provided dataPath
   - **Risk**: Confusing path resolution, hard to find data files

---

## 9. File Locations Reference

### Source Files Analyzed

```
MonoBallFramework.Game/GameData/Loading/
├── GameDataLoader.cs
│  └─ 6 Deserialize calls, 1 options instance ✅

MonoBallFramework.Game/GameData/Sprites/
├── SpriteRegistry.cs
   ├─ Async: 1 options (lines 185-189) ✅
   └─ Sync: 1 options (lines 261-265) ⚠️ Inefficient placement

MonoBallFramework.Game/Engine/Rendering/Popups/
├── PopupRegistry.cs
   ├─ Async: 1 options (lines 192-196) ✅
   └─ Sync: 2 options in loops (lines 295-299, 327-331) ❌

MonoBallFramework.Game/Engine/Core/Types/
├── TypeRegistry.cs
   └─ Options in RegisterFromJsonAsync (lines 139-144) ❌

MonoBallFramework.Game/Initialization/Pipeline/
├── Steps/LoadGameDataStep.cs
├── Steps/LoadSpriteDefinitionsStep.cs
├── Steps/InitializeMapPopupStep.cs
└── InitializationPipeline.cs (executes in order)
```

### Data Directory Structure

```
Assets/
├── Definitions/
│  ├── Maps/
│  │  ├── Popups/
│  │  │  ├── Themes/ (loaded by GameDataLoader)
│  │  │  ├── Backgrounds/ (loaded by PopupRegistry)
│  │  │  └── Outlines/ (loaded by PopupRegistry)
│  │  ├── Regions/ (loaded by GameDataLoader)
│  │  └── Sections/ (loaded by GameDataLoader)
│  ├── NPCs/ (loaded by GameDataLoader)
│  ├── Trainers/ (loaded by GameDataLoader)
│  ├── Audio/ (loaded by GameDataLoader)
│  └── [Behavior types]/ (loaded by TypeRegistry)
└── Sprites/ (loaded by SpriteRegistry)
```

---

## 10. Recommendations

### Priority 1: Critical Fixes (10 minutes each)

1. **TypeRegistry - Add concurrency guard**
   ```csharp
   private readonly SemaphoreSlim _loadLock = new(1, 1);

   public async Task<int> LoadAllAsync()
   {
       await _loadLock.WaitAsync();
       try
       {
           // existing code
       }
       finally
       {
           _loadLock.Release();
       }
   }
   ```

2. **TypeRegistry - Cache options**
   ```csharp
   public async Task<int> LoadAllAsync()
   {
       var options = new JsonSerializerOptions { ... };

       foreach (string jsonPath in jsonFiles)
       {
           await RegisterFromJsonAsync(jsonPath, options);
       }
   }

   private async Task RegisterFromJsonAsync(string jsonPath, JsonSerializerOptions options)
   {
       // Remove options creation here, use parameter instead
   }
   ```

3. **PopupRegistry - Move options outside loops**
   ```csharp
   private void LoadDefinitionsFromJson()
   {
       var options = new JsonSerializerOptions { ... };  // Move here

       string backgroundsPath = Path.Combine(...);
       if (Directory.Exists(backgroundsPath))
       {
           foreach (string jsonFile in Directory.GetFiles(...))
           {
               // Remove options creation from inside loop
               var definition = JsonSerializer.Deserialize<PopupBackgroundDefinition>(json, options);
           }
       }

       // Same for outlines...
   }
   ```

### Priority 2: Design Improvements (1-2 hours)

4. **Create shared JsonSerializerOptions factory**
   ```csharp
   public static class JsonSerializerOptionsFactory
   {
       public static readonly JsonSerializerOptions Default = new()
       {
           PropertyNameCaseInsensitive = true,
           ReadCommentHandling = JsonCommentHandling.Skip,
           AllowTrailingCommas = true,
       };

       public static readonly JsonSerializerOptions Pretty = new()
       {
           PropertyNameCaseInsensitive = true,
           ReadCommentHandling = JsonCommentHandling.Skip,
           AllowTrailingCommas = true,
           WriteIndented = true,
       };
   }
   ```

5. **Consolidate popup data models**
   - Consider: Should PopupTheme and PopupBackground/Outline be the same entity?
   - Or: Create sync between EF Core and PopupRegistry at initialization?

6. **Standardize path resolution**
   - Inject IAssetPathResolver into all registries
   - Remove hardcoded AppContext.BaseDirectory usage
   - Consistent "Data" vs "Assets" directory naming

### Priority 3: Long-term Refactoring (4-8 hours)

7. **Unified data loading system**
   - Consider: Single registry that loads all types
   - Benefits: Consistent options, single concurrency guard, better coordination
   - Risk: Would require major refactoring

8. **Async-only registries**
   - Remove sync loading paths (LoadDefinitions, LoadDefinitionsFromJson)
   - Make all registries async-only
   - Simplify code, remove duplicate paths

---

## 11. Verification Checklist

After applying fixes, verify:

- [ ] TypeRegistry has SemaphoreSlim preventing concurrent LoadAllAsync
- [ ] PopupRegistry options created once per LoadDefinitionsFromJson call
- [ ] No new JsonSerializerOptions created inside loops
- [ ] All registries use same options configuration (or justified differences)
- [ ] Popup data consistency between EF Core and PopupRegistry
- [ ] No files read multiple times during initialization
- [ ] Initialization time improved by 10-20ms (especially with many definition files)
- [ ] All tests still pass
- [ ] No new race conditions introduced

---

## 12. Appendix: JsonSerializerOptions Diffs

### Expected Identical Configurations

**GameDataLoader** (Line 25-31):
```json
{
  "PropertyNameCaseInsensitive": true,
  "ReadCommentHandling": "Skip",
  "AllowTrailingCommas": true,
  "WriteIndented": true
}
```

**SpriteRegistry** (Lines 185-189, 261-265):
```json
{
  "PropertyNameCaseInsensitive": true,
  "ReadCommentHandling": "Skip"
}
```

**PopupRegistry** (Lines 192-196, 295-299, 327-331):
```json
{
  "PropertyNameCaseInsensitive": true,
  "ReadCommentHandling": "Skip"
}
```

**TypeRegistry** (Lines 139-144):
```json
{
  "PropertyNameCaseInsensitive": true,
  "ReadCommentHandling": "Skip",
  "AllowTrailingCommas": true
}
```

**Recommendation**: Consolidate to single shared factory with variants for WriteIndented

---

End of Analysis
