# PokeSharp Registry Architecture - Visual Analysis

## Current Registry Topology

```
┌─────────────────────────────────────────────────────────────────────────┐
│                        POKESHARP DATA LOADING                           │
└─────────────────────────────────────────────────────────────────────────┘

                          INITIALIZATION PIPELINE
                                    │
                ┌───────────────────┼───────────────────┐
                │                   │                   │
        [Phase 1]           [Phase 2]           [Phase 3]
      GameDataLoader    SpriteRegistry      PopupRegistry
          │                   │                   │
          ├─ Load NPCs        ├─ Parse JSON      ├─ Parse Backgrounds
          ├─ Load Trainers    ├─ Deserialize    ├─ Parse Outlines
          ├─ Load Maps        ├─ Register       └─ Register
          ├─ Load Themes      └─ Cache (dual)
          ├─ Load Sections
          └─ Load Audio
             │
             ▼
        [Save to EF Core]
             │
             ▼
        GameDataContext
        (In-Memory DB)
             │
             ├─ DbSet<Npc>
             ├─ DbSet<Trainer>
             ├─ DbSet<Map>
             ├─ DbSet<PopupTheme>
             ├─ DbSet<MapSection>
             └─ DbSet<AudioDefinition>


        RUNTIME REGISTRIES (Lazy-Loaded)
                    │
        ┌───────────┼───────────────┐
        │           │               │
   AudioRegistry  TypeRegistry   TypeRegistry
   (EF Core       (BehaviorDef)  (NpcDef)
    wrapper)          │             │
        │             │             │
        ├─ _cache     ├─ _defs      ├─ _defs
        ├─ _trackId   ├─ _scripts   ├─ _scripts
        │   Cache     └─ LoadAll()  └─ LoadAll()
        └─            (JSON)        (JSON)
           LoadDefs()
           (from EF)
```

---

## Data Flow: Single Sprite Load

### Current (Normal Case)

```
User calls: SpriteRegistry.GetSpriteByPath("npcs/generic/prof_birch")
                    │
                    ▼
            ┌──────────────────┐
            │ _isLoaded == true │  [Phase 1: Fast Load]
            └──────────────────┘
                    │
                    ▼
        LoadDefinitionsAsync()
                    │
                    ├─→ _loadLock.WaitAsync()
                    │
                    ├─→ Directory.GetFiles()
                    │   └─→ "Data/Sprites/**/*.json"
                    │
                    └─→ foreach jsonFile:
                        ├─→ File.ReadAllTextAsync(jsonFile)
                        │       [READ 1: "npcs/generic/prof_birch.json"]
                        │
                        ├─→ var options = new JsonSerializerOptions
                        │       {
                        │           PropertyNameCaseInsensitive = true,
                        │           ReadCommentHandling = JsonCommentHandling.Skip
                        │       }
                        │       [OPTIONS ALLOCATION 1]
                        │
                        ├─→ JsonSerializer.Deserialize<SpriteDefinition>()
                        │       [PARSE 1: JSON → SpriteDefinition object]
                        │
                        └─→ RegisterSprite(definition)
                            ├─→ _sprites.TryAdd(spriteId, definition)
                            │   [ADD TO PRIMARY CACHE]
                            │
                            └─→ _pathCache.TryAdd(spriteId.LocalId, definition)
                                [ADD TO PATH CACHE]

        ▼ LOAD COMPLETE ▼

        Later call: GetSpriteByPath("npcs/generic/prof_birch")
                    │
                    ▼
        string normalizedPath = "npcs/generic/prof_birch"
                    │
                    ▼
        _pathCache.TryGetValue(normalizedPath, out var definition)
                    │
                    ▼
        return definition  [O(1) lookup - FAST]
```

### Problem: PopupRegistry Inefficiency

```
User calls: PopupRegistry.LoadDefinitionsAsync()
                    │
                    ▼
        LoadDefinitionsAsync()
            │
            ├─→ LoadBackgroundsAsync(backgroundsPath, options, ct)
            │       │
            │       ├─→ Directory.GetFiles(backgroundsPath, "*.json")
            │       │   └─→ ["bg1.json", "bg2.json", ..., "bg10.json"]
            │       │
            │       └─→ foreach jsonFile in jsonFiles:
            │           ├─→ File.ReadAllTextAsync(jsonFile)
            │           │
            │           ├─→ JsonSerializer.Deserialize<PopupBackgroundDefinition>(...)
            │           │
            │           └─→ RegisterBackground()
            │
            └─→ LoadOutlinesAsync(outlinesPath, options, ct)
                    │
                    ├─→ Directory.GetFiles(outlinesPath, "*.json")
                    │   └─→ ["outline1.json", "outline2.json", ..., "outline10.json"]
                    │
                    └─→ foreach jsonFile in jsonFiles:
                        ├─→ File.ReadAllTextAsync(jsonFile)
                        │
                        ├─→ JsonSerializer.Deserialize<PopupOutlineDefinition>(...)
                        │
                        └─→ RegisterOutline()

        COMPARISON: Sync Method (LoadDefinitionsFromJson)

        LoadDefinitionsFromJson()
            │
            ├─→ Load Backgrounds:
            │   └─→ foreach jsonFile in backgroundsPath/*.json:  [10 iterations]
            │       ├─→ File.ReadAllText(jsonFile)
            │       ├─→ var options = new JsonSerializerOptions {...}  ← ALLOCATION 1
            │       ├─→ var options = new JsonSerializerOptions {...}  ← ALLOCATION 2
            │       └─→ ... (allocate 10 times!)
            │
            └─→ Load Outlines:
                └─→ foreach jsonFile in outlinesPath/*.json:  [10 iterations]
                    ├─→ File.ReadAllText(jsonFile)
                    ├─→ var options = new JsonSerializerOptions {...}  ← ALLOCATION 11
                    └─→ ... (allocate 10 more times!)

        TOTAL ALLOCATIONS: 20 × JsonSerializerOptions
```

---

## Lookup Performance: AudioRegistry Worst Case

```
Scenario: Cache not loaded, looking for TrackId "mus_dewford"

GetByTrackId("mus_dewford")
    │
    ├─→ _trackIdCache.TryGetValue("mus_dewford")
    │   └─→ MISS (not cached yet)
    │
    └─→ if (!_isCacheLoaded)  ← TRUE
        │
        ├─→ [SEARCH 1] _cache.Values.FirstOrDefault(d => d.TrackId == "mus_dewford")
        │   │
        │   ├─→ Scan all items in _cache
        │   │   Item 1: TrackId == "mus_battle_01" → NO
        │   │   Item 2: TrackId == "mus_battle_02" → NO
        │   │   ...
        │   │   Item 50: TrackId == "mus_town_01" → NO
        │   │
        │   └─→ MISS (item not in partial cache)
        │       └─→ Performance: O(50) string comparisons
        │
        ├─→ [DATABASE LOAD] context.AudioDefinitions.AsNoTracking().ToList()
        │   │
        │   ├─→ SELECT * FROM AudioDefinitions
        │   │   └─→ Returns 150 records
        │   │
        │   └─→ ToList() materializes all into memory
        │       └─→ Allocation: ~50KB
        │
        └─→ [SEARCH 2] allDefinitions.FirstOrDefault(d => d.TrackId == "mus_dewford")
            │
            ├─→ Scan all 150 loaded items
            │   Item 1: TrackId == "mus_battle_01" → NO
            │   Item 2: TrackId == "mus_battle_02" → NO
            │   ...
            │   Item 47: TrackId == "mus_dewford" → YES
            │
            └─→ HIT
                └─→ Performance: O(150) string comparisons

TOTAL COST:
- O(50) in-memory scans
- O(150) database load + memory allocation
- O(150) in-memory scans
- = O(350) operations + 50KB allocation

NEXT CALL (if different TrackId not found before):
- Repeat entire database load + scan
```

### Fixed Version

```
GetByTrackId("mus_dewford")  [WITH INDEX]
    │
    ├─→ _trackIdCache.TryGetValue("mus_dewford")
    │   └─→ MISS
    │
    └─→ if (!_isCacheLoaded)
        │
        └─→ [DATABASE QUERY WITH WHERE]
            │
            ├─→ SELECT * FROM AudioDefinitions
            │   WHERE TrackId = 'mus_dewford'  ← INDEX SEEK
            │   LIMIT 1
            │
            ├─→ Returns 1 record (via index)
            │   └─→ Allocation: ~300 bytes
            │
            └─→ Cache and return

TOTAL COST:
- O(1) index lookup on database
- O(300 bytes) allocation
- = O(1) operation

IMPROVEMENT: 100-350x faster for cache misses
```

---

## Memory Allocation Waterfall

### Current State: PopupRegistry Init

```
PopupRegistry.LoadDefinitionsAsync()
    │
    ├─→ [ALLOCATION] var options = new JsonSerializerOptions
    │       Size: ~200 bytes
    │       Lifetime: Shared for all backgrounds & outlines
    │
    └─→ foreach background (10 times):
        ├─→ File.ReadAllTextAsync() → [HEAP] ~5KB per file
        ├─→ JsonDeserialize() → [HEAP] ~10KB definition object
        └─→ RegisterBackground() → [HEAP] key/value in dictionary

        Total: 10 × 15KB = 150KB

        Plus for outlines (10 times):
        ├─→ File.ReadAllTextAsync() → [HEAP] ~5KB per file
        ├─→ JsonDeserialize() → [HEAP] ~10KB definition object
        └─→ RegisterOutline() → [HEAP] key/value in dictionary

        Total: 10 × 15KB = 150KB

TOTAL MEMORY: 300KB (for definitions) + ~200 bytes (options)

                    ▼▼▼ PROBLEM ▼▼▼

Sync Method: LoadDefinitionsFromJson()
    │
    └─→ foreach background (10 times):
        ├─→ [ALLOCATION 1] var options = new JsonSerializerOptions → 200 bytes
        ├─→ [ALLOCATION 2] var options = new JsonSerializerOptions → 200 bytes
        └─→ ... repeated 20 times total

        Extra waste: 19 × 200 bytes = 3,800 bytes
        Extra GC pressure: 19 GC roots created


COMPARISON:
    Optimal:    200 bytes (1 allocation)
    Current:    4,000 bytes (20 allocations)
    Waste:      3,800 bytes + GC overhead
```

---

## Registry Initialization Order

```
TIMELINE: PokeSharp Startup

T=0ms   ├─ MonoBallFrameworkGame.Initialize()
        │
T=10ms  ├─ ServiceRegistration
        │   ├─ AddCoreServices()
        │   ├─ AddGameServices()
        │   ├─ AddAudioServices()
        │   │   └─ AudioRegistry created + LoadDefinitions() called
        │   │       ├─ Query EF Core GameDataContext
        │   │       ├─ Populate _cache from database
        │   │       └─ Time: ~50ms
        │   │
        │   └─ DI container ready
        │
T=100ms ├─ InitializationPipeline.ExecuteAsync()
        │
T=110ms ├─ Step 1: CreateGraphicsServicesStep
        │
T=200ms ├─ Step 2: LoadSpriteDefinitionsStep
        │   └─ SpriteRegistry.LoadDefinitionsAsync()
        │       ├─ Load Sprites from Data/Sprites/*.json
        │       ├─ Parallel file I/O (Task.WhenAll)
        │       ├─ Register each sprite
        │       └─ Time: ~150ms
        │
T=350ms ├─ Step 3: InitializeMapPopupStep
        │   └─ PopupRegistry.LoadDefinitionsAsync()
        │       ├─ Load Backgrounds from JSON
        │       ├─ Load Outlines from JSON
        │       └─ Time: ~50ms
        │
T=400ms ├─ Step 4: Create game systems
        │   ├─ GameInitializer.Initialize()
        │   ├─ SpriteAnimationSystem registered
        │   │   (uses SpriteRegistry for animations)
        │   └─ Time: ~100ms
        │
T=500ms ├─ Game Ready

REGISTRATION DEPENDENCIES:
    GameDataLoader
        └─→ Loads Npcs, Trainers, Maps, Audio into EF Core

    AudioRegistry
        ├─ Depends on: GameDataContext.AudioDefinitions
        └─ Loads at T=10ms, queries database

    SpriteRegistry
        ├─ Independent of GameDataLoader
        ├─ Loads JSON directly (bypasses EF Core)
        └─ Loads at T=200ms

    PopupRegistry
        ├─ Independent of GameDataLoader
        ├─ Loads JSON directly (bypasses EF Core)
        └─ Loads at T=350ms

PROBLEM: Three different loading strategies
         Three loading timestamps
         No transactional consistency across related data
```

---

## Data Consistency Issues

```
SCENARIO: User modifies audio.json while game is running

┌─ Master Data ──────────────────────┐
│ Assets/Definitions/Audio/          │
│ ├─ mus_dewford.json               │
│ └─ ... (loaded by GameDataLoader) │
└────────────────────────────────────┘
           │
           ├─→ [T=10ms] GameDataLoader reads and inserts into EF Core
           │           EF Core: AudioDefinition { Id = "...", TrackId = "mus_dewford" }
           │
           ├─→ [T=10ms] AudioRegistry.LoadDefinitions()
           │           Queries EF Core and caches
           │
           ├─→ User modifies Assets/Definitions/Audio/mus_dewford.json
           │
           ├─→ [T=200ms] SpriteRegistry loads (unaffected)
           │
           ├─→ [T=350ms] PopupRegistry loads (unaffected)
           │
           └─→ [T=500ms+] Game runs
               At runtime:
               ├─→ AudioRegistry.GetByTrackId("mus_dewford")
               │   └─→ Returns OLD version from cache
               │       (loaded at T=10ms from database)
               │
               └─→ Actual file has NEW version (not loaded)
                   Result: Version mismatch, stale data

SOLUTION: Either
1. Load all audio from JSON into AudioRegistry too
   (consistent with SpriteRegistry approach)

2. Keep AudioRegistry loading from EF Core,
   but ensure JSON → EF Core happens before
   AudioRegistry initialization

3. Implement file watching to reload on changes
```

---

## Recommended Architecture Evolution

```
PHASE 1: Current (Multiple Inconsistent Patterns)

    GameDataLoader (JSON → EF Core)
          ↓
    GameDataContext (Npcs, Trainers, Maps, Audio)
          ↓
    AudioRegistry (EF Core → Cache)

    SpriteRegistry (JSON → Cache)
    PopupRegistry (JSON → Cache)
    TypeRegistry<T> (JSON → Cache)


PHASE 2: Unified Strategy Option A (EF Core Primary)

    GameDataLoader (JSON → EF Core)
          ↓
    GameDataContext (All definitions)
          ↓
    ├─→ AudioRegistry (EF → Cache)
    ├─→ SpriteRegistry (EF → Cache)
    ├─→ PopupRegistry (EF → Cache)
    └─→ TypeRegistry<T> (EF → Cache)

    Benefits:
    ✓ Single source of truth (EF Core)
    ✓ Transactional consistency
    ✓ Mod support (database can be modified)
    ✓ Hot reload possible (query again)

    Costs:
    ✗ EF Core overhead
    ✗ More schema definitions needed


PHASE 3: Unified Strategy Option B (JSON Primary, Shared Cache)

    JSON Files (Sprites, Popups, Audio, Types, etc)
          ↓
    Unified DataLoader
    (Parse all JSON, validate, cache)
          ↓
    ├─→ SpriteRegistry (← Shared cache)
    ├─→ PopupRegistry (← Shared cache)
    ├─→ AudioRegistry (← Shared cache)
    ├─→ TypeRegistry<T> (← Shared cache)
    └─→ [Optional] GameDataContext (EF → Cache for queries)

    Benefits:
    ✓ No EF Core overhead
    ✓ Single loading point
    ✓ Faster startup
    ✓ Consistent pattern

    Costs:
    ✗ No easy mod support
    ✗ No transaction/consistency semantics
    ✗ Need manual reload mechanism


PHASE 4: Hybrid (Recommended)

    Fast Path: JSON → Memory (SpriteRegistry, PopupRegistry)
    Slow Path: JSON → EF Core (for queries/mod support)

    Initialization:
    1. Load all JSON files once
    2. Parse into memory caches
    3. Insert into EF Core for persistence/queryability
    4. All registries reference same in-memory cache

    This requires:
    - Refactor GameDataLoader to load JSON once
    - Share parsed objects across registries
    - Add EF insertion as secondary step
    - Implement cache invalidation on EF changes
```

---

## Quick Reference: Issue Locations

| Issue | File | Lines | Severity |
|-------|------|-------|----------|
| Options in loop | PopupRegistry.cs | 295-299 | CRITICAL |
| Options in loop | PopupRegistry.cs | 326-330 | CRITICAL |
| O(n) audio lookup | AudioRegistry.cs | 158 | HIGH |
| Full table load | AudioRegistry.cs | 167 | HIGH |
| Duplicate loading | SpriteRegistry.cs | 136-293 | HIGH |
| Multiple saves | GameDataLoader.cs | 140, 222, 316... | MEDIUM |
| Inconsistent patterns | Various | - | MEDIUM |
| Missing indices | AudioDefinition | - | MEDIUM |

