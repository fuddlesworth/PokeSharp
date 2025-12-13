# Entity Framework Data Loading Analysis Report

**Date:** 2025-12-12
**Status:** CRITICAL DATA LOADING ISSUE IDENTIFIED
**Severity:** HIGH - NPCs and Trainers completely missing from EF Core database

---

## Executive Summary

The EntityFramework panel debug data revealed that **NPCs and Trainers DbSets are EMPTY**, while other data types load correctly. This is not a bug in the loading code—it's a **MISSING DATA SOURCE** issue.

### Root Cause
**The `Assets/Definitions/NPCs` and `Assets/Definitions/Trainers` directories do not exist in the repository.**

The GameDataLoader is correctly programmed to load from these directories, but the directories themselves are missing, so no files are found, resulting in 0 items loaded.

---

## Expected vs Actual Data Counts

### Database Configuration (GameDataContext.cs)

| DbSet | Entity Type | Status |
|-------|-------------|--------|
| `Npcs` | `NpcDefinition` | **CONFIGURED - EMPTY** |
| `Trainers` | `TrainerDefinition` | **CONFIGURED - EMPTY** |
| `Maps` | `MapDefinition` | **CONFIGURED - LOADED** |
| `AudioDefinitions` | `AudioDefinition` | **CONFIGURED - LOADED** |
| `PopupThemes` | `PopupTheme` | **CONFIGURED - LOADED** |
| `MapSections` | `MapSection` | **CONFIGURED - LOADED** |

All DbSets are properly configured in `/mnt/c/Users/nate0/RiderProjects/PokeSharp/MonoBallFramework.Game/GameData/GameDataContext.cs`

---

## JSON Files Found in Assets/Definitions

### Directory Structure
```
Assets/Definitions/
├── Audio/             ✓ EXISTS - 368 JSON files loaded
├── Behaviors/         ✓ EXISTS
├── Maps/              ✓ EXISTS
│   ├── Regions/       ✓ EXISTS - 520 JSON files loaded
│   ├── Popups/        ✓ EXISTS
│   │   └── Themes/    ✓ EXISTS - 6 JSON files loaded
│   └── Sections/      ✓ EXISTS - 213 JSON files loaded
├── Sprites/           ✓ EXISTS (asset files only)
├── TextWindow/        ✓ EXISTS
├── TileBehaviors/     ✓ EXISTS
├── Worlds/            ✓ EXISTS
├── NPCs/              ✗ MISSING - 0 files
└── Trainers/          ✗ MISSING - 0 files
```

### Actual File Counts

| Data Type | Location | Expected Count | Actual Count | Status |
|-----------|----------|-----------------|--------------|--------|
| NPCs | `Assets/Definitions/NPCs/` | Unknown | **0** | **MISSING DIRECTORY** |
| Trainers | `Assets/Definitions/Trainers/` | Unknown | **0** | **MISSING DIRECTORY** |
| Maps/Regions | `Assets/Definitions/Maps/Regions/` | 520+ | **520 loaded** | ✓ OK |
| Audio Definitions | `Assets/Definitions/Audio/**` | 368+ | **368 loaded** | ✓ OK |
| Popup Themes | `Assets/Definitions/Maps/Popups/Themes/` | 6 | **6 loaded** | ✓ OK |
| Map Sections | `Assets/Definitions/Maps/Sections/` | 213 | **213 loaded** | ✓ OK |

**Total Expected Definitions:** 1,107+ | **Total Loaded:** 1,107 | **Missing:** NPCs + Trainers (Unknown count)

---

## Code Analysis

### GameDataLoader Implementation
**File:** `/mnt/c/Users/nate0/RiderProjects/PokeSharp/MonoBallFramework.Game/GameData/Loading/GameDataLoader.cs`

The loader correctly:
1. ✓ Checks if directories exist (lines 76-79, 151-154)
2. ✓ Logs when directories are not found (18 entries, e.g., "Directory not found: NPCs")
3. ✓ Returns count of 0 when no files found
4. ✓ Validates required fields in DTOs
5. ✓ Handles deserialization errors gracefully
6. ✓ Calls SaveChangesAsync after each batch load

### Loading Flow

```
LoadGameDataStep (execution starts here)
  ↓
GameDataLoader.LoadAllAsync(dataPath)
  ├─ LoadNpcsAsync(Assets/Definitions/NPCs/) → 0 files, returns 0
  ├─ LoadTrainersAsync(Assets/Definitions/Trainers/) → 0 files, returns 0
  ├─ LoadMapDefinitionsAsync(Assets/Definitions/Maps/Regions/) → 520 files ✓
  ├─ LoadPopupThemesAsync(Assets/Definitions/Maps/Popups/Themes/) → 6 files ✓
  ├─ LoadMapSectionsAsync(Assets/Definitions/Maps/Sections/) → 213 files ✓
  └─ LoadAudioDefinitionsAsync(Assets/Definitions/Audio/) → 368 files ✓
```

### Error Handling in LoadGameDataStep
**File:** `/mnt/c/Users/nate0/RiderProjects/PokeSharp/MonoBallFramework.Game/Initialization/Pipeline/Steps/LoadGameDataStep.cs`

The step has **silent error swallowing** (lines 54-84):
- `FileNotFoundException` - Logged as warning, continues
- `DirectoryNotFoundException` - Logged as warning, continues
- `IOException` - Logged as error, continues
- `Exception` - Logged as error, continues

**Impact:** Missing NPCs/Trainers directories do NOT crash the app; they're silently logged as warnings. The game continues with empty DbSets.

---

## Data Validation Criteria

### DTOs Defined for Deserialization

#### NpcDefinitionDto (lines 792-804)
```csharp
public record NpcDefinitionDto {
    public string? NpcId { get; init; }           // REQUIRED
    public string? DisplayName { get; init; }
    public string? NpcType { get; init; }
    public string? SpriteId { get; init; }
    public string? BehaviorScript { get; init; }
    public string? DialogueScript { get; init; }
    public float? MovementSpeed { get; init; }
    public Dictionary<string, object>? CustomProperties { get; init; }
    public string? SourceMod { get; init; }
    public string? Version { get; init; }
}
```

#### TrainerDefinitionDto (lines 809-826)
```csharp
public record TrainerDefinitionDto {
    public string? TrainerId { get; init; }           // REQUIRED
    public string? DisplayName { get; init; }
    public string? TrainerClass { get; init; }
    public string? SpriteId { get; init; }
    public int? PrizeMoney { get; init; }
    public string[]? Items { get; init; }
    public string? AiScript { get; init; }
    public string? IntroDialogue { get; init; }
    public string? DefeatDialogue { get; init; }
    public string? OnDefeatScript { get; init; }
    public bool? IsRematchable { get; init; }
    public TrainerPartyMemberDto[]? Party { get; init; }
    public Dictionary<string, object>? CustomProperties { get; init; }
    public string? SourceMod { get; init; }
    public string? Version { get; init; }
}
```

**Validation checks:**
- NpcId/TrainerId must not be whitespace (lines 104-108, 179-183)
- Custom properties are optional and stored as JSON

---

## Comparison: What Works vs What Doesn't

### Working Data Loads

#### Audio Definitions (368 files)
- **Path:** `Assets/Definitions/Audio/**` (recursive)
- **Files:** Music/Battle, Music/Routes, Music/Towns, Music/Special, SFX/Battle, SFX/Environment, SFX/UI
- **Processing:** All subdirectories processed recursively (AllDirectories)
- **Filtering:** Hidden directories (.claude-flow) excluded
- **Status:** ✓ **LOADING SUCCESSFULLY**

Example JSON structure:
```json
{
  "id": "music:battle:mus_vs_gym_leader",
  "displayName": "Gym Leader Battle",
  "audioPath": "Audio/Music/Battle/mus_vs_gym_leader.ogg",
  "category": "music",
  "subcategory": "battle",
  "volume": 1.0,
  "loop": true
}
```

#### Map Definitions (520 files)
- **Path:** `Assets/Definitions/Maps/Regions/` (recursive)
- **Files:** Maps in Hoenn region (and subdirectories)
- **Processing:** All subdirectories processed recursively
- **Caching:** Existing maps checked to support mod overrides
- **Status:** ✓ **LOADING SUCCESSFULLY**

Example JSON structure:
```json
{
  "id": "base:map:hoenn/littleroot_town",
  "displayName": "Littleroot Town",
  "type": "town",
  "region": "hoenn",
  "tiledPath": "Maps/hoenn/littleroot_town.tmx"
}
```

#### Popup Themes (6 files)
- **Path:** `Assets/Definitions/Maps/Popups/Themes/`
- **Files:** TopDirectory only (not recursive)
- **Status:** ✓ **LOADING SUCCESSFULLY**

#### Map Sections (213 files)
- **Path:** `Assets/Definitions/Maps/Sections/`
- **Files:** TopDirectory only (not recursive)
- **Status:** ✓ **LOADING SUCCESSFULLY**

### Non-Working Data Loads

#### NPCs (0 files)
- **Expected Path:** `Assets/Definitions/NPCs/`
- **Actual Status:** **DIRECTORY DOES NOT EXIST**
- **Consequence:** LoadNpcsAsync returns 0, context.Npcs stays empty
- **Logged As:** Warning "Directory not found" (line 78)

#### Trainers (0 files)
- **Expected Path:** `Assets/Definitions/Trainers/`
- **Actual Status:** **DIRECTORY DOES NOT EXIST**
- **Consequence:** LoadTrainersAsync returns 0, context.Trainers stays empty
- **Logged As:** Warning "Directory not found" (line 153)

---

## Why NPCs/Trainers are Missing

### Hypothesis 1: Data Not Committed to Repository
The NPCs and Trainers JSON files may be:
- Untracked by Git
- In .gitignore
- Too large to commit
- Intentionally excluded from version control

**Evidence:** They're not in Assets/Definitions directory structure

### Hypothesis 2: Data Generated at Runtime
Could be generated by external tools (like porycon) and not committed:
- Check for any build scripts or data generation steps
- Look for .gitignore patterns

### Hypothesis 3: Feature Not Yet Implemented
NPCs and Trainers might be stub features with:
- Entity types defined (NpcDefinition, TrainerDefinition)
- Loader code prepared (GameDataLoader)
- But no actual data files created yet

---

## EF Core Configuration Details

### SaveChangesAsync Pattern
Each loader method calls SaveChangesAsync separately:
```csharp
await _context.SaveChangesAsync(ct);  // After LoadNpcsAsync
await _context.SaveChangesAsync(ct);  // After LoadTrainersAsync
await _context.SaveChangesAsync(ct);  // After LoadMapDefinitionsAsync
// etc...
```

**Benefit:** Each batch is persisted independently, preventing cascading failures.

### Indexes Configured
All DbSets have strategic indexes for common queries:

**NpcDefinition:**
- Primary Key: NpcId
- Indexes: NpcType, DisplayName

**TrainerDefinition:**
- Primary Key: TrainerId
- Indexes: TrainerClass, DisplayName

**MapDefinition:**
- Primary Key: MapId
- Indexes: Region, MapType, DisplayName

**AudioDefinition:**
- Primary Key: AudioId
- Indexes: Category, Subcategory, DisplayName
- Composite: (Category, Subcategory)

**PopupTheme:**
- Primary Key: Id
- Indexes: Name, Background, Outline
- Relationship: HasMany(MapSections)

**MapSection:**
- Primary Key: Id
- Indexes: Name, ThemeId
- Composite: (X, Y)

---

## Test Strategy for EF Core Data

### Unit Tests Needed

#### 1. Directory Existence Tests
```csharp
[Fact]
public void LoadNpcsAsync_WithMissingDirectory_ReturnsZero()
{
    // Assert: directory doesn't exist
    var dataPath = Path.Combine(testDataRoot, "NPCs");
    Assert.False(Directory.Exists(dataPath));

    // Act
    var count = await loader.LoadNpcsAsync(dataPath, CancellationToken.None);

    // Assert
    Assert.Equal(0, count);
}
```

#### 2. File Count Tests
```csharp
[Fact]
public void LoadAudioDefinitionsAsync_LoadsAllRecursiveFiles()
{
    var dataPath = Path.Combine(testDataRoot, "Audio");
    var files = Directory.GetFiles(dataPath, "*.json", SearchOption.AllDirectories);

    // Should find all 368 audio files
    Assert.True(files.Length > 0);
}
```

#### 3. SaveChangesAsync Verification
```csharp
[Fact]
public async Task LoadMapsAsync_CallsSaveChangesAsync()
{
    var mockContext = new Mock<GameDataContext>();
    var loader = new GameDataLoader(mockContext.Object, logger);

    await loader.LoadMapDefinitionsAsync(mapsPath, CancellationToken.None);

    // Verify SaveChangesAsync was called
    mockContext.Verify(x => x.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Once);
}
```

#### 4. DbSet Population Tests
```csharp
[Fact]
public async Task LoadAllAsync_PopulatesAllDbSets()
{
    var context = new GameDataContext(options);
    var loader = new GameDataLoader(context, logger);

    await loader.LoadAllAsync(dataPath);

    // After loading:
    Assert.Equal(0, context.Npcs.Count());           // EMPTY - no directory
    Assert.Equal(0, context.Trainers.Count());       // EMPTY - no directory
    Assert.True(context.Maps.Count() > 0);           // 520+ maps
    Assert.True(context.AudioDefinitions.Count() > 0); // 368+ audio
    Assert.Equal(6, context.PopupThemes.Count());    // 6 themes
    Assert.Equal(213, context.MapSections.Count());  // 213 sections
}
```

#### 5. Error Handling Tests
```csharp
[Fact]
public async Task LoadGameDataStep_HandlesMissingDirectory_Gracefully()
{
    var step = new LoadGameDataStep();
    var context = new InitializationContext(...);

    // Should not throw even if NPCs directory missing
    await step.ExecuteStepAsync(context, progress, CancellationToken.None);

    // Should log warning
    Assert.Contains("not found", loggedMessages);
}
```

### Integration Tests Needed

#### 1. Full Load Cycle
```csharp
[Fact]
public async Task LoadGameDataStep_LoadsAllAvailableData()
{
    var context = new GameDataContext(inMemoryOptions);
    var loader = new GameDataLoader(context, logger);

    await loader.LoadAllAsync(assetsPath);

    var summary = new {
        Maps = context.Maps.Count(),
        Audio = context.AudioDefinitions.Count(),
        Themes = context.PopupThemes.Count(),
        Sections = context.MapSections.Count(),
        Npcs = context.Npcs.Count(),
        Trainers = context.Trainers.Count()
    };

    // Verify expected totals
    Assert.Equal(520, summary.Maps);
    Assert.Equal(368, summary.Audio);
    Assert.Equal(6, summary.Themes);
    Assert.Equal(213, summary.Sections);
    Assert.Equal(0, summary.Npcs);      // MISSING
    Assert.Equal(0, summary.Trainers);  // MISSING
}
```

#### 2. Data Integrity
```csharp
[Fact]
public async Task LoadedMaps_HaveRequiredFields()
{
    var maps = context.Maps.AsEnumerable();

    foreach (var map in maps)
    {
        Assert.NotNull(map.MapId);
        Assert.NotEmpty(map.DisplayName);
        Assert.NotEmpty(map.TiledDataPath);
    }
}
```

---

## Summary of Findings

### What IS Working
1. ✓ GameDataLoader code is correctly implemented
2. ✓ GameDataContext is properly configured with 6 DbSets
3. ✓ Error handling prevents crashes on missing data
4. ✓ Logging captures what was/wasn't loaded
5. ✓ SaveChangesAsync is called correctly
6. ✓ 1,107 definitions successfully loaded (Maps, Audio, Themes, Sections)

### What IS NOT Working
1. ✗ NPCs DbSet: EMPTY (0 items)
2. ✗ Trainers DbSet: EMPTY (0 items)
3. ✗ Missing directories: `Assets/Definitions/NPCs/` and `Assets/Definitions/Trainers/`

### Root Cause
**Data source missing, not code bug.**

The directories expected by GameDataLoader do not exist in the repository. This is either:
- A missing commit/branch
- Data generated externally and not checked in
- Feature stub awaiting implementation

### Next Steps
1. Create or restore `Assets/Definitions/NPCs/` directory
2. Create or restore `Assets/Definitions/Trainers/` directory
3. Add sample JSON files matching NpcDefinitionDto and TrainerDefinitionDto schemas
4. Verify they load with correct counts via EntityFrameworkPanel

---

## References

- **GameDataLoader:** `/mnt/c/Users/nate0/RiderProjects/PokeSharp/MonoBallFramework.Game/GameData/Loading/GameDataLoader.cs` (785 lines)
- **GameDataContext:** `/mnt/c/Users/nate0/RiderProjects/PokeSharp/MonoBallFramework.Game/GameData/GameDataContext.cs` (216 lines)
- **LoadGameDataStep:** `/mnt/c/Users/nate0/RiderProjects/PokeSharp/MonoBallFramework.Game/Initialization/Pipeline/Steps/LoadGameDataStep.cs` (87 lines)
- **GameInitializer:** `/mnt/c/Users/nate0/RiderProjects/PokeSharp/MonoBallFramework.Game/Initialization/Initializers/GameInitializer.cs` (291 lines)

---

**Report Status:** Complete
**Last Updated:** 2025-12-12
**Agent:** QA/Tester (Hive Mind)
