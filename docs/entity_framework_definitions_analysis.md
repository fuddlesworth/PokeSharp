# Research Analysis: Entity Framework Definition Loading in PokeSharp

## CRITICAL FINDING: Definitions NOT Being Loaded into EF Core

### Summary
The GameDataContext has 6 DbSets (Npcs, Trainers, Maps, AudioDefinitions, PopupThemes, MapSections), but there are **6 additional Definition types that are BYPASSED and loaded via separate registry systems**:

1. **SpriteDefinition** - JSON registry only (SpriteRegistry)
2. **PopupBackgroundDefinition** - JSON registry only (PopupRegistry)
3. **PopupOutlineDefinition** - JSON registry only (PopupRegistry)
4. **PopupTileDefinition** - Embedded in PopupOutlineDefinition (not standalone)
5. **BehaviorDefinition** - JSON registry only (TypeRegistry<BehaviorDefinition>)
6. **TileBehaviorDefinition** - JSON registry only (TypeRegistry<TileBehaviorDefinition>)
7. **AnimationDefinition** - No registry at all (only used inline within SpriteAnimationSystem)

---

## Detailed Definition Type Analysis

### LOADED INTO EF CORE (6 Types)

#### 1. NpcDefinition
- **Location**: `/mnt/c/Users/nate0/RiderProjects/PokeSharp/MonoBallFramework.Game/GameData/Entities/NpcDefinition.cs`
- **EF DbSet**: `GameDataContext.Npcs`
- **Loading Method**: `GameDataLoader.LoadNpcsAsync()`
- **Configuration**: GameDataContext.cs:72-88 (ConfigureNpcDefinition)
- **DTO**: NpcDefinitionDto (GameDataLoader.cs:792-804)
- **Files Created**: GameDataLoader.cs lines 74-144

#### 2. TrainerDefinition
- **Location**: `/mnt/c/Users/nate0/RiderProjects/PokeSharp/MonoBallFramework.Game/GameData/Entities/TrainerDefinition.cs`
- **EF DbSet**: `GameDataContext.Trainers`
- **Loading Method**: `GameDataLoader.LoadTrainersAsync()`
- **Configuration**: GameDataContext.cs:93-111 (ConfigureTrainerDefinition)
- **DTO**: TrainerDefinitionDto (GameDataLoader.cs:809-826)
- **Files Created**: GameDataLoader.cs lines 149-227

#### 3. MapDefinition
- **Location**: `/mnt/c/Users/nate0/RiderProjects/PokeSharp/MonoBallFramework.Game/GameData/Entities/MapDefinition.cs`
- **EF DbSet**: `GameDataContext.Maps`
- **Loading Method**: `GameDataLoader.LoadMapDefinitionsAsync()`
- **Configuration**: GameDataContext.cs:116-145 (ConfigureMapDefinition)
- **DTO**: MapDefinitionDto (GameDataLoader.cs:883-893)
- **Files Created**: GameDataLoader.cs lines 234-319

#### 4. AudioDefinition
- **Location**: `/mnt/c/Users/nate0/RiderProjects/PokeSharp/MonoBallFramework.Game/GameData/Entities/AudioDefinition.cs`
- **EF DbSet**: `GameDataContext.AudioDefinitions`
- **Loading Method**: `GameDataLoader.LoadAudioDefinitionsAsync()`
- **Configuration**: GameDataContext.cs:176-193 (ConfigureAudioDefinition)
- **DTO**: AudioDefinitionDto (GameDataLoader.cs:899-914)
- **Files Created**: GameDataLoader.cs lines 701-784

#### 5. PopupTheme
- **Location**: `/mnt/c/Users/nate0/RiderProjects/PokeSharp/MonoBallFramework.Game/GameData/Entities/PopupTheme.cs`
- **EF DbSet**: `GameDataContext.PopupThemes`
- **Loading Method**: `GameDataLoader.LoadPopupThemesAsync()`
- **Configuration**: GameDataContext.cs:150-171 (ConfigurePopupTheme)
- **DTO**: PopupThemeDto (GameDataLoader.cs:851-861)
- **Files Created**: GameDataLoader.cs lines 561-625

#### 6. MapSection
- **Location**: `/mnt/c/Users/nate0/RiderProjects/PokeSharp/MonoBallFramework.Game/GameData/Entities/MapSection.cs`
- **EF DbSet**: `GameDataContext.MapSections`
- **Loading Method**: `GameDataLoader.LoadMapSectionsAsync()`
- **Configuration**: GameDataContext.cs:198-215 (ConfigureMapSection)
- **DTO**: MapSectionDto (GameDataLoader.cs:866-877)
- **Files Created**: GameDataLoader.cs lines 630-695

---

### NOT LOADED INTO EF CORE (7 Types)

#### 1. SpriteDefinition
- **Location**: `/mnt/c/Users/nate0/RiderProjects/PokeSharp/MonoBallFramework.Game/GameData/Sprites/SpriteDefinition.cs`
- **Loading Mechanism**: `SpriteRegistry` (JSON-based registry)
- **Registry File**: `/mnt/c/Users/nate0/RiderProjects/PokeSharp/MonoBallFramework.Game/GameData/Sprites/SpriteRegistry.cs`
- **Registry Type**: ConcurrentDictionary<GameSpriteId, SpriteDefinition>
- **Loading Methods**:
  - Sync: `SpriteRegistry.LoadDefinitions()` (lines 136-141)
  - Async: `SpriteRegistry.LoadDefinitionsAsync()` (lines 155-201)
  - Core: `LoadSpritesRecursiveAsync()` (lines 206-246)
- **Load Location**: `/Definitions/Sprites/` directory (recursive)
- **Initialization Step**: `/mnt/c/Users/nate0/RiderProjects/PokeSharp/MonoBallFramework.Game/Initialization/Pipeline/Steps/LoadSpriteDefinitionsStep.cs` (line 30)
- **Status**: Uses custom registry - completely bypasses EF Core
- **Sub-types**:
  - `SpriteFrameDefinition` (lines 104-120) - nested data
  - `SpriteAnimationDefinition` (lines 125-142) - nested data

#### 2. PopupBackgroundDefinition
- **Location**: `/mnt/c/Users/nate0/RiderProjects/PokeSharp/MonoBallFramework.Game/Engine/Rendering/Popups/PopupBackgroundDefinition.cs`
- **Loading Mechanism**: `PopupRegistry` (JSON-based registry)
- **Registry File**: `/mnt/c/Users/nate0/RiderProjects/PokeSharp/MonoBallFramework.Game/Engine/Rendering/Popups/PopupRegistry.cs`
- **Registry Type**: ConcurrentDictionary<string, PopupBackgroundDefinition>
- **Loading Methods**:
  - Async: `PopupRegistry.LoadBackgroundsAsync()` (lines 218-241)
  - Loaded in parallel with outlines via `LoadDefinitionsAsync()` (line 202)
- **Load Location**: `/Assets/Definitions/Maps/Popups/Backgrounds/`
- **Initialization Step**: `/mnt/c/Users/nate0/RiderProjects/PokeSharp/MonoBallFramework.Game/Initialization/Pipeline/Steps/InitializeMapPopupStep.cs` (lines 40-41)
- **Status**: Uses custom registry - completely bypasses EF Core
- **Thread Safety**: ConcurrentDictionary with SemaphoreSlim load lock (lines 23, 27)

#### 3. PopupOutlineDefinition
- **Location**: `/mnt/c/Users/nate0/RiderProjects/PokeSharp/MonoBallFramework.Game/Engine/Rendering/Popups/PopupOutlineDefinition.cs`
- **Loading Mechanism**: `PopupRegistry` (JSON-based registry)
- **Registry File**: `/mnt/c/Users/nate0/RiderProjects/PokeSharp/MonoBallFramework.Game/Engine/Rendering/Popups/PopupRegistry.cs`
- **Registry Type**: ConcurrentDictionary<string, PopupOutlineDefinition>
- **Loading Methods**:
  - Async: `PopupRegistry.LoadOutlinesAsync()` (lines 246-269)
  - Loaded in parallel with backgrounds via `LoadDefinitionsAsync()` (line 203)
- **Load Location**: `/Assets/Definitions/Maps/Popups/Outlines/`
- **Initialization Step**: `/mnt/c/Users/nate0/RiderProjects/PokeSharp/MonoBallFramework.Game/Initialization/Pipeline/Steps/InitializeMapPopupStep.cs` (lines 40-41)
- **Status**: Uses custom registry - completely bypasses EF Core
- **Nested Type**: Contains `PopupTileDefinition` (line 62)

#### 4. PopupTileDefinition
- **Location**: `/mnt/c/Users/nate0/RiderProjects/PokeSharp/MonoBallFramework.Game/Engine/Rendering/Popups/PopupTileDefinition.cs`
- **Loading Mechanism**: Embedded within `PopupOutlineDefinition` JSON
- **Registry**: Not registered independently - only loaded as nested property
- **Parent Definition**: `PopupOutlineDefinition.Tiles` property (line 62)
- **Status**: No independent registry or EF loading - purely nested data structure
- **Data**: Contains Index, X, Y, Width, Height (lines 13-38)

#### 5. BehaviorDefinition
- **Location**: `/mnt/c/Users/nate0/RiderProjects/PokeSharp/MonoBallFramework.Game/Engine/Core/Types/BehaviorDefinition.cs`
- **Loading Mechanism**: `TypeRegistry<BehaviorDefinition>` (generic registry)
- **Registry File**: `/mnt/c/Users/nate0/RiderProjects/PokeSharp/MonoBallFramework.Game/Engine/Core/Types/TypeRegistry.cs`
- **Registry Type**: ConcurrentDictionary<string, BehaviorDefinition>
- **Loading Methods**:
  - `TypeRegistry<T>.LoadAllAsync()` (lines 98-128)
  - `TypeRegistry<T>.RegisterFromJsonAsync()` (lines 136-168)
- **Load Location**: `/Definitions/Behaviors/` directory (recursive)
- **Initialization**: Created in `CoreServicesExtensions.cs` via Dependency Injection
- **Status**: Uses generic type registry - completely bypasses EF Core
- **Features**: Implements `IScriptedType` for Roslyn script compilation
- **Record Type**: C# record with required properties (TypeId, DisplayName)
- **Script Support**: BehaviorScript property for C# scripts (line 43)

#### 6. TileBehaviorDefinition
- **Location**: `/mnt/c/Users/nate0/RiderProjects/PokeSharp/MonoBallFramework.Game/Engine/Core/Types/TileBehaviorDefinition.cs`
- **Loading Mechanism**: `TypeRegistry<TileBehaviorDefinition>` (generic registry)
- **Registry File**: `/mnt/c/Users/nate0/RiderProjects/PokeSharp/MonoBallFramework.Game/Engine/Core/Types/TypeRegistry.cs`
- **Registry Type**: ConcurrentDictionary<string, TileBehaviorDefinition>
- **Loading Methods**:
  - `TypeRegistry<T>.LoadAllAsync()` (lines 98-128)
  - `TypeRegistry<T>.RegisterFromJsonAsync()` (lines 136-168)
- **Load Location**: `/Definitions/TileBehaviors/` directory (recursive)
- **Initialization**: Created in `CoreServicesExtensions.cs` via Dependency Injection
- **Status**: Uses generic type registry - completely bypasses EF Core
- **Features**: Implements `IScriptedType` for Roslyn script compilation
- **Record Type**: C# record with required properties (TypeId, DisplayName)
- **Flags Support**: TileBehaviorFlags enum (lines 44-75) with bit flags for HasEncounters, Surfable, BlocksMovement, ForcesMovement, DisablesRunning

#### 7. AnimationDefinition
- **Location**: `/mnt/c/Users/nate0/RiderProjects/PokeSharp/MonoBallFramework.Game/Engine/Rendering/Animation/AnimationDefinition.cs`
- **Loading Mechanism**: **NO REGISTRY AT ALL**
- **Used By**: Inline in code, embedded in `SpriteAnimationDefinition` from SpriteRegistry
- **Usage Context**:
  - `SpriteAnimationSystem.cs` - creates instances dynamically from cached sprite definitions
  - No JSON file loading directly
  - No registry lookup (derived from sprite data)
  - Pure in-memory construction with full event support
- **Status**: Not loaded from data files - constructed programmatically from sprite animation data
- **Features**: Full animation system with (lines 9-221):
  - Frame timing support (uniform and per-frame)
  - Animation events (dictionary of frame→events)
  - Static factory methods (CreateSingleFrame, CreateFromGrid)
  - Event chaining API
  - Helper methods for frame lookup and validation

---

## Redundant Loading Patterns Identified

### Pattern 1: Parallel JSON-Based Registry Systems
**Location**: `/mnt/c/Users/nate0/RiderProjects/PokeSharp/MonoBallFramework.Game/Engine/Rendering/Popups/PopupRegistry.cs` lines 172-213
```
- Backgrounds and Outlines loaded IN PARALLEL using Task.WhenAll
- Both could theoretically be in EF but use separate registry instead
- Doubles the infrastructure overhead
- Parallel loading at line 202-205
```

### Pattern 2: Generic TypeRegistry with Script Compilation
**Location**: `/mnt/c/Users/nate0/RiderProjects/PokeSharp/MonoBallFramework.Game/Engine/Core/Types/TypeRegistry.cs` lines 1-323
```
- BehaviorDefinition and TileBehaviorDefinition both use same registry
- TypeRegistry is generic but only used for these two types
- Scripts are compiled separately via Roslyn after registration
- Over-engineered for current use case (only 2 consumers)
- Full async disposal support for script instances (lines 40-69)
```

### Pattern 3: Async Loading Orchestration (Multiple Loading Mechanisms)
**Locations**:
- `/mnt/c/Users/nate0/RiderProjects/PokeSharp/MonoBallFramework.Game/Initialization/Pipeline/Steps/LoadGameDataStep.cs` line 41 (EF-based)
- `/mnt/c/Users/nate0/RiderProjects/PokeSharp/MonoBallFramework.Game/Initialization/Pipeline/Steps/LoadSpriteDefinitionsStep.cs` line 30 (Registry-based)
- `/mnt/c/Users/nate0/RiderProjects/PokeSharp/MonoBallFramework.Game/Initialization/Pipeline/Steps/InitializeMapPopupStep.cs` lines 40-41 (Registry-based)

```
Three separate async loading phases:
- LoadGameDataStep: EF Core loader (GameDataLoader)
- LoadSpriteDefinitionsStep: Sprite registry loader
- InitializeMapPopupStep: Popup registry loader
No unified loading strategy or coordination
```

---

## Architecture Impact Summary

| Definition Type | Mechanism | In EF? | Async? | Script Support? | Thread Safe? | Location |
|---|---|---|---|---|---|---|
| NpcDefinition | GameDataLoader → EF | YES | YES | NO | YES (EF) | GameData/Entities |
| TrainerDefinition | GameDataLoader → EF | YES | YES | NO | YES (EF) | GameData/Entities |
| MapDefinition | GameDataLoader → EF | YES | YES | NO | YES (EF) | GameData/Entities |
| AudioDefinition | GameDataLoader → EF | YES | YES | NO | YES (EF) | GameData/Entities |
| PopupTheme | GameDataLoader → EF | YES | YES | NO | YES (EF) | GameData/Entities |
| MapSection | GameDataLoader → EF | YES | YES | NO | YES (EF) | GameData/Entities |
| **SpriteDefinition** | **SpriteRegistry** | **NO** | YES | NO | YES (ConcurrentDict) | GameData/Sprites |
| **PopupBackgroundDefinition** | **PopupRegistry** | **NO** | YES | NO | YES (ConcurrentDict) | Engine/Rendering/Popups |
| **PopupOutlineDefinition** | **PopupRegistry** | **NO** | YES | NO | YES (ConcurrentDict) | Engine/Rendering/Popups |
| **PopupTileDefinition** | Nested in Outline | **NO** | N/A | NO | N/A | Engine/Rendering/Popups |
| **BehaviorDefinition** | **TypeRegistry<T>** | **NO** | YES | YES | YES (ConcurrentDict) | Engine/Core/Types |
| **TileBehaviorDefinition** | **TypeRegistry<T>** | **NO** | YES | YES | YES (ConcurrentDict) | Engine/Core/Types |
| **AnimationDefinition** | Inline/Dynamic | **NO** | N/A | NO | YES (code-created) | Engine/Rendering/Animation |

---

## Key Insights for Queen Seraphina

### 1. EF Core is Underutilized
- Only 6 of 13 definition types use EF Core
- The other 7 use custom registries or inline construction
- Missed opportunity for unified data access patterns

### 2. Mixed Loading Strategies
- **EF Core + GameDataLoader** (6 types) - standard data loading pattern
- **Custom registry + JSON** (SpriteRegistry, PopupRegistry, TypeRegistry) - parallel loading with ConcurrentDictionary
- **No registry at all** (AnimationDefinition) - pure in-memory construction

### 3. Initialization Complexity
Three separate async loading phases with different mechanisms and timing:
1. LoadGameDataStep: Loads 6 definition types into EF Core
2. LoadSpriteDefinitionsStep: Loads sprites into SpriteRegistry
3. InitializeMapPopupStep: Loads popup definitions into PopupRegistry

### 4. The Real Redundancy
- **PopupBackgroundDefinition and PopupOutlineDefinition** COULD be in EF but use PopupRegistry instead
  - They're complex objects with structure but still JSON-based
  - Could support full EF relationships and querying
  - Current approach duplicates some PopupTheme functionality

- **BehaviorDefinition and TileBehaviorDefinition** have their own generic registry
  - Only used by these two types
  - TypeRegistry is over-engineered for this use case
  - Could be consolidated into a single registry pattern

- **SpriteDefinition** has specialized registry
  - Similar pattern to popup definitions
  - Could theoretically be EF-based like other sprite types
  - Maintains separate caches by ID and path

### 5. No Unified Pattern
Each definition type evolved independently:
- Old definitions (NPC, Trainer, Map, Audio) → EF Core path
- Mid-tier definitions (Popup backgrounds/outlines) → Custom PopupRegistry
- Newer definitions (Behaviors) → Generic TypeRegistry
- UI/Rendering definitions (Animation) → Inline construction

---

## Specific File References for Implementation

### EF-Based Definitions (GameDataContext)
```
File: /mnt/c/Users/nate0/RiderProjects/PokeSharp/MonoBallFramework.Game/GameData/GameDataContext.cs
- DbSets declared: lines 44-55
- Configuration methods: lines 57-215
- NpcDefinition config: lines 72-88
- TrainerDefinition config: lines 93-111
- MapDefinition config: lines 116-145
- AudioDefinition config: lines 176-193
- PopupTheme config: lines 150-171
- MapSection config: lines 198-215
```

### Registry-Based Definitions
```
File: /mnt/c/Users/nate0/RiderProjects/PokeSharp/MonoBallFramework.Game/GameData/Sprites/SpriteRegistry.cs
- SpriteDefinition loading: lines 136-201

File: /mnt/c/Users/nate0/RiderProjects/PokeSharp/MonoBallFramework.Game/Engine/Rendering/Popups/PopupRegistry.cs
- PopupBackgroundDefinition loading: lines 218-241
- PopupOutlineDefinition loading: lines 246-269
- Parallel loading: lines 202-205

File: /mnt/c/Users/nate0/RiderProjects/PokeSharp/MonoBallFramework.Game/Engine/Core/Types/TypeRegistry.cs
- Generic registry implementation: lines 1-323
- Load all definitions: lines 98-128
- Register from JSON: lines 136-168
```

### Initialization Steps
```
File: /mnt/c/Users/nate0/RiderProjects/PokeSharp/MonoBallFramework.Game/Initialization/Pipeline/Steps/LoadGameDataStep.cs
- Loads 6 EF-based definitions: lines 24-85
- Preloads popup cache: lines 44-51

File: /mnt/c/Users/nate0/RiderProjects/PokeSharp/MonoBallFramework.Game/Initialization/Pipeline/Steps/LoadSpriteDefinitionsStep.cs
- Loads sprite definitions: line 30

File: /mnt/c/Users/nate0/RiderProjects/PokeSharp/MonoBallFramework.Game/Initialization/Pipeline/Steps/InitializeMapPopupStep.cs
- Loads popup definitions: lines 40-41
```

---

## Recommendations for Unification

1. **Standardize on EF Core** for all persistent definitions (would require rearchitecting PopupRegistry and TypeRegistry)
2. **Create unified definition loader** that handles all 13 types consistently
3. **Consolidate registries** - use one generic registry pattern for all non-EF types
4. **Align initialization timing** - all definitions should follow same loading phases
5. **Document the pattern** - make it clear why certain types choose certain mechanisms
