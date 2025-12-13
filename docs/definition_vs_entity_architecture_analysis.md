# Definition vs Entity Architecture Analysis

## Executive Summary

This codebase uses a **dual-layer architecture** for game data:

1. **Runtime "Definition" classes** (`Engine/Core/Types/`) - Lightweight, immutable data structures loaded from JSON for runtime game logic
2. **EF Core "Entity" classes** (`GameData/Entities/`) - Database-backed entities with EF Core annotations for persistence and querying

The architecture is currently in **migration transition** from JSON-based TypeRegistry to EF Core-based persistence.

---

## Core Concepts

### What is a "Definition"?

**Location**: `/MonoBallFramework.Game/Engine/Core/Types/`

**Purpose**: Runtime data structures for game logic

**Key Characteristics**:
- Implements `ITypeDefinition` interface
- Immutable `record` types (C# records)
- Designed for O(1) lookup via `TypeRegistry<T>`
- Loaded from JSON files
- Pure data - no behavior methods
- Used by game systems at runtime

**Base Interface**:
```csharp
public interface ITypeDefinition
{
    string TypeId { get; }        // e.g., "patrol", "ice", "tall_grass"
    string DisplayName { get; }   // e.g., "Patrol Behavior"
    string? Description { get; }  // Optional documentation
}
```

**Extended Interface for Scripting**:
```csharp
public interface IScriptedType : ITypeDefinition
{
    string? BehaviorScript { get; }  // Path to .csx Roslyn script
}
```

---

### What is an "Entity"?

**Location**: `/MonoBallFramework.Game/GameData/Entities/`

**Purpose**: Database persistence layer using Entity Framework Core

**Key Characteristics**:
- Uses EF Core annotations (`[Table]`, `[Key]`, `[MaxLength]`, etc.)
- Mutable `class` types
- Designed for CRUD operations and querying
- Stored in EF Core in-memory database
- Supports relationships and foreign keys
- Used for data loading and persistence

**Naming Convention**:
- Pattern: `{Name}DefinitionEntity` or just `{Name}Definition`
- Examples: `BehaviorDefinitionEntity`, `SpriteDefinitionEntity`, `MapDefinition`

---

## Architecture Pattern Analysis

### Pattern 1: Mirrored Definition + Entity (Scripted Types)

**Use Case**: Types that need both runtime lookup AND database persistence with scripting

**Examples**: `BehaviorDefinition` ↔ `BehaviorDefinitionEntity`, `TileBehaviorDefinition` ↔ `TileBehaviorDefinitionEntity`

#### Runtime Definition (Record)
```csharp
// Engine/Core/Types/BehaviorDefinition.cs
public record BehaviorDefinition : IScriptedType
{
    public float DefaultSpeed { get; init; } = 4.0f;
    public float PauseAtWaypoint { get; init; } = 1.0f;
    public bool AllowInteractionWhileMoving { get; init; } = false;

    // IScriptedType properties
    public required string TypeId { get; init; }
    public required string DisplayName { get; init; }
    public string? Description { get; init; }
    public string? BehaviorScript { get; init; }
}
```

#### EF Core Entity (Class)
```csharp
// GameData/Entities/BehaviorDefinitionEntity.cs
[Table("BehaviorDefinitions")]
public class BehaviorDefinitionEntity
{
    [Key]
    [MaxLength(100)]
    public string BehaviorId { get; set; } = string.Empty;  // Maps to TypeId

    [Required]
    [MaxLength(100)]
    public string DisplayName { get; set; } = string.Empty;

    [MaxLength(1000)]
    public string? Description { get; set; }

    public float DefaultSpeed { get; set; } = 4.0f;
    public float PauseAtWaypoint { get; set; } = 1.0f;
    public bool AllowInteractionWhileMoving { get; set; } = false;

    [MaxLength(500)]
    public string? BehaviorScript { get; set; }

    // EF Core-specific metadata
    [MaxLength(100)]
    public string? SourceMod { get; set; }

    [MaxLength(20)]
    public string Version { get; set; } = "1.0.0";
}
```

**Mapping**:
- `TypeId` → `BehaviorId` (primary key)
- All properties mirrored
- Entity adds: `SourceMod`, `Version` (metadata for modding)

---

### Pattern 2: Entity-Only (No Runtime Definition)

**Use Case**: Data that only needs database persistence, no runtime TypeRegistry lookup

**Examples**: `MapDefinition`, `NpcDefinition`, `TrainerDefinition`, `AudioDefinition`

**Why No Runtime Definition?**
- These are accessed via EF Core queries, not TypeRegistry
- They use strongly-typed IDs (`GameMapId`, `GameNpcId`, etc.) for direct lookups
- They don't need Roslyn scripting support

```csharp
// GameData/Entities/MapDefinition.cs
[Table("Maps")]
public class MapDefinition
{
    [Key]
    [MaxLength(100)]
    [Column(TypeName = "nvarchar(100)")]
    public GameMapId MapId { get; set; } = null!;  // Strongly-typed ID

    [Required]
    [MaxLength(100)]
    public string DisplayName { get; set; } = string.Empty;

    // ... map-specific properties ...
}
```

---

### Pattern 3: Legacy Deleted Definitions (Replaced by Entities)

**Examples**:
- `SpriteDefinition.cs` (deleted) → `SpriteDefinitionEntity`
- `PopupBackgroundDefinition.cs` (deleted) → `PopupBackgroundEntity`
- `PopupOutlineDefinition.cs` (deleted) → `PopupOutlineEntity`
- `PopupTileDefinition.cs` (deleted) → Owned entity in `PopupOutlineEntity`

**Old Pattern (JSON-based)**:
```csharp
// GameData/Sprites/SpriteDefinition.cs (DELETED)
public class SpriteDefinition
{
    [JsonPropertyName("Id")]
    public string Id { get; set; } = string.Empty;

    [JsonPropertyName("DisplayName")]
    public string DisplayName { get; set; } = string.Empty;

    // Direct JSON loading into class
}
```

**New Pattern (EF Core-based)**:
```csharp
// GameData/Entities/SpriteDefinitionEntity.cs
[Table("SpriteDefinitions")]
public class SpriteDefinitionEntity
{
    [Key]
    [MaxLength(150)]
    [Column(TypeName = "nvarchar(150)")]
    public GameSpriteId SpriteId { get; set; } = null!;  // Strongly-typed ID

    [Required]
    [MaxLength(100)]
    public string DisplayName { get; set; } = string.Empty;

    // Owned collections (JSON columns in DB)
    public List<SpriteFrame> Frames { get; set; } = new();
    public List<SpriteAnimation> Animations { get; set; } = new();

    // EF Core metadata
    [MaxLength(100)]
    public string? SourceMod { get; set; }

    [MaxLength(20)]
    public string Version { get; set; } = "1.0.0";
}
```

---

## Naming Convention Summary

### Consistent Patterns:

| Category | Runtime Definition | EF Core Entity | Primary Key |
|----------|-------------------|----------------|-------------|
| **Behaviors** | `BehaviorDefinition` | `BehaviorDefinitionEntity` | `BehaviorId` (string) |
| **Tile Behaviors** | `TileBehaviorDefinition` | `TileBehaviorDefinitionEntity` | `TileBehaviorId` (string) |
| **Sprites** | *(deleted)* | `SpriteDefinitionEntity` | `SpriteId` (GameSpriteId) |
| **Popup Backgrounds** | *(deleted)* | `PopupBackgroundEntity` | `BackgroundId` (string) |
| **Popup Outlines** | *(deleted)* | `PopupOutlineEntity` | `OutlineId` (string) |

### Entity-Only Patterns:

| Category | EF Core Entity | Primary Key |
|----------|----------------|-------------|
| **Maps** | `MapDefinition` | `MapId` (GameMapId) |
| **NPCs** | `NpcDefinition` | `NpcId` (GameNpcId) |
| **Trainers** | `TrainerDefinition` | `TrainerId` (GameTrainerId) |
| **Audio** | `AudioDefinition` | `AudioId` (GameAudioId) |
| **Map Sections** | `MapSection` | `Id` (GameMapSectionId) |
| **Popup Themes** | `PopupTheme` | `Id` (GameThemeId) |

---

## Pattern Breakdown

### ✅ Consistent Naming (Entity Suffix)
- `BehaviorDefinitionEntity`
- `TileBehaviorDefinitionEntity`
- `SpriteDefinitionEntity`
- `PopupBackgroundEntity`
- `PopupOutlineEntity`

### ⚠️ Inconsistent Naming (No Entity Suffix)
- `MapDefinition` (should be `MapDefinitionEntity` or `MapEntity`)
- `NpcDefinition` (should be `NpcDefinitionEntity` or `NpcEntity`)
- `TrainerDefinition` (should be `TrainerDefinitionEntity`)
- `AudioDefinition` (should be `AudioDefinitionEntity`)
- `MapSection` (consistent - not a definition)
- `PopupTheme` (consistent - not a definition)

---

## Migration Status

### ✅ Fully Migrated (EF Core + No TypeRegistry)
- `SpriteDefinition` → `SpriteDefinitionEntity`
- `PopupBackgroundDefinition` → `PopupBackgroundEntity`
- `PopupOutlineDefinition` → `PopupOutlineEntity`

### 🔄 Dual System (Runtime Definition + Entity)
- `BehaviorDefinition` + `BehaviorDefinitionEntity`
- `TileBehaviorDefinition` + `TileBehaviorDefinitionEntity`

**Why Dual System?**
- Behavior types need `TypeRegistry<T>` for O(1) lookup by `TypeId`
- Roslyn scripts are compiled and cached in `TypeRegistry`
- EF Core entities used for initial data loading and persistence

### ⚠️ Entity-Only (No Runtime Definition Needed)
- `MapDefinition`, `NpcDefinition`, `TrainerDefinition`, `AudioDefinition`
- These use strongly-typed IDs for direct EF Core queries
- No need for TypeRegistry pattern

---

## Key Differences: Definition vs Entity

| Aspect | Runtime Definition | EF Core Entity |
|--------|-------------------|----------------|
| **Type** | `record` (immutable) | `class` (mutable) |
| **Location** | `Engine/Core/Types/` | `GameData/Entities/` |
| **Interface** | `ITypeDefinition` / `IScriptedType` | None (uses EF attributes) |
| **Lookup** | `TypeRegistry<T>.Get(typeId)` | `DbSet<T>.Find(id)` or LINQ |
| **Primary Key** | `TypeId` (string) | Varies (`{Name}Id`, typed IDs) |
| **Mutability** | Immutable (`init` properties) | Mutable (`set` properties) |
| **Attributes** | None (JSON serialization) | `[Table]`, `[Key]`, `[MaxLength]`, etc. |
| **Metadata** | Minimal (TypeId, DisplayName, Description) | Rich (SourceMod, Version, etc.) |
| **Scripting** | `BehaviorScript` property | Same property, stored in DB |
| **Usage** | Runtime game logic | Data loading, persistence, queries |

---

## TypeRegistry Pattern

**Purpose**: O(1) in-memory lookup for runtime definitions

```csharp
public class TypeRegistry<T> where T : ITypeDefinition
{
    private readonly ConcurrentDictionary<string, T> _definitions;
    private readonly ConcurrentDictionary<string, object> _scripts;

    public T? Get(string typeId);              // Get definition
    public object? GetScript(string typeId);   // Get compiled Roslyn script
    public void Register(T definition);        // Add definition
    public void RegisterScript(string typeId, object scriptInstance);
}
```

**Current Usage**:
- `TypeRegistry<BehaviorDefinition>` - NPC behavior types
- `TypeRegistry<TileBehaviorDefinition>` - Tile behavior types

**Migration Path**:
1. Load from EF Core → `BehaviorDefinitionEntity`
2. Convert to runtime → `BehaviorDefinition`
3. Register in → `TypeRegistry<BehaviorDefinition>`
4. Compile Roslyn scripts → Store in `TypeRegistry`

---

## ID System Analysis

### String-Based TypeIds (TypeRegistry)
- Used by: `BehaviorDefinition`, `TileBehaviorDefinition`
- Format: Simple strings (`"patrol"`, `"ice"`, `"tall_grass"`)
- Lookup: `TypeRegistry<T>.Get("patrol")`

### Strongly-Typed IDs (EF Core)
- Used by: `MapDefinition`, `NpcDefinition`, `TrainerDefinition`, etc.
- Types: `GameMapId`, `GameNpcId`, `GameSpriteId`, `GameAudioId`, etc.
- Format: Unified format (`"base:map:hoenn/littleroot_town"`)
- Lookup: `DbContext.Maps.Find(GameMapId.Parse("..."))`

### Hybrid Approach (Behavior Entities)
- Entity uses `BehaviorId` (string) as primary key
- Maps to `TypeId` from runtime definition
- Maintains compatibility with TypeRegistry lookups

---

## Recommendations

### ✅ Architecture is Sound
The dual-layer approach is appropriate:
- **Definitions**: Fast runtime lookups for game logic
- **Entities**: Rich querying and persistence for data management

### 🔧 Naming Consistency Issues

**Inconsistent Entity Naming**:
- `MapDefinition` vs `BehaviorDefinitionEntity` vs `PopupBackgroundEntity`
- Consider standardizing: Either all `*Entity` suffix or all `*Definition` suffix

**Recommended Fix Options**:

**Option 1: Add Entity Suffix to All**
```csharp
MapDefinition → MapEntity
NpcDefinition → NpcEntity
TrainerDefinition → TrainerEntity
AudioDefinition → AudioEntity
```

**Option 2: Remove Entity Suffix from All**
```csharp
BehaviorDefinitionEntity → BehaviorDefinition (rename runtime to BehaviorType)
TileBehaviorDefinitionEntity → TileBehaviorDefinition
SpriteDefinitionEntity → SpriteDefinition
PopupBackgroundEntity → PopupBackground
PopupOutlineEntity → PopupOutline
```

**Option 3: Use Definition for Entities, Type for Runtime**
```csharp
// Runtime (TypeRegistry)
BehaviorDefinition → BehaviorType
TileBehaviorDefinition → TileBehaviorType

// Entities (keep as-is)
BehaviorDefinitionEntity → BehaviorDefinition
MapDefinition (no change)
```

### 🎯 Recommended Pattern

For **new definitions**, use this decision tree:

1. **Does it need TypeRegistry + Roslyn scripting?**
   - YES → Create both `{Name}Type` (record) and `{Name}DefinitionEntity` (class)
   - NO → Go to step 2

2. **Is it primarily for data persistence?**
   - YES → Create only `{Name}Entity` or `{Name}Definition` (class)
   - Use strongly-typed ID (`Game{Name}Id`)

3. **Does it have complex relationships?**
   - YES → Use EF Core navigation properties
   - NO → Keep simple with foreign key properties

---

## Files Analyzed

### Runtime Definitions (`Engine/Core/Types/`)
- ✅ `ITypeDefinition.cs` - Base interface
- ✅ `IScriptedType.cs` - Scripting interface
- ✅ `BehaviorDefinition.cs` - NPC behavior record
- ✅ `TileBehaviorDefinition.cs` - Tile behavior record
- ✅ `TypeRegistry.cs` - Generic registry implementation

### EF Core Entities (`GameData/Entities/`)
- ✅ `BehaviorDefinitionEntity.cs` - Mirrors `BehaviorDefinition`
- ✅ `TileBehaviorDefinitionEntity.cs` - Mirrors `TileBehaviorDefinition`
- ✅ `SpriteDefinitionEntity.cs` - Replaces deleted `SpriteDefinition`
- ✅ `PopupBackgroundEntity.cs` - Replaces deleted `PopupBackgroundDefinition`
- ✅ `PopupOutlineEntity.cs` - Replaces deleted `PopupOutlineDefinition`
- ✅ `MapDefinition.cs` - Entity-only (no runtime definition)
- ✅ `NpcDefinition.cs` - Entity-only
- ✅ `TrainerDefinition.cs` - Entity-only
- ✅ `AudioDefinition.cs` - Entity-only
- ✅ `PopupTheme.cs` - Entity-only
- ✅ `MapSection.cs` - Entity-only

### Configuration
- ✅ `GameDataContext.cs` - EF Core DbContext with all entity configurations

### Deleted Files (from git history)
- ❌ `GameData/Sprites/SpriteDefinition.cs` (commit d10ee628)
- ❌ `Engine/Rendering/Popups/PopupBackgroundDefinition.cs` (commit ac284739)
- ❌ `Engine/Rendering/Popups/PopupOutlineDefinition.cs` (commit ac284739)
- ❌ `Engine/Rendering/Popups/PopupTileDefinition.cs` (commit ac284739)

---

## Conclusion

The codebase follows a **well-designed dual-layer architecture**:
1. **Runtime layer**: Immutable records with TypeRegistry for fast lookups
2. **Persistence layer**: Mutable EF Core entities with rich metadata

**Current state**: Mid-migration from JSON-based loading to EF Core

**Inconsistencies**: Naming conventions vary (`Entity` suffix not consistently applied)

**Recommendation**: Standardize naming to improve maintainability and developer experience.
