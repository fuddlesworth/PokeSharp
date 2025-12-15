# Mod Loading and Content System Consistency Analysis

**Analysis Date**: 2025-12-15
**Analyst**: Code Analyzer Agent (Hive Mind)
**Scope**: Mod loading, content resolution, definition loading, and TypeId reference handling

---

## Executive Summary

The codebase exhibits **critical inconsistencies** in how behavior definitions are loaded and registered. The system is in a **transitional state** between two architectural approaches:

1. **Legacy System**: `TypeRegistry<T>` with `ITypeDefinition` using simple string IDs
2. **New System**: EF Core entities with strongly-typed IDs (e.g., `GameBehaviorId`, `GameTileBehaviorId`)

These two systems operate in parallel, creating **registration mismatches**, **duplicate loading paths**, and **ID format inconsistencies**.

---

## Critical Inconsistencies Found

### 1. Dual Loading Systems for Behavior Definitions

**Location**: `GameDataLoader.cs` vs `TypeRegistry<T>`

#### Problem
Behavior definitions are loaded through TWO separate mechanisms:

**Path A: EF Core Entity Loading** (NEW)
- File: `/MonoBallFramework.Game/GameData/Loading/GameDataLoader.cs:920-1000`
- Method: `LoadBehaviorDefinitionsAsync()`
- Output: `BehaviorEntity` objects stored in EF Core in-memory database
- ID Type: `GameBehaviorId` (strongly-typed value object)
- ID Creation: `GameBehaviorId.Create(behaviorId)` at line 973

**Path B: TypeRegistry Loading** (LEGACY)
- File: `/MonoBallFramework.Game/Engine/Core/Types/TypeRegistry.cs:98-128`
- Method: `LoadAllAsync()` → `RegisterFromJsonAsync()`
- Output: `BehaviorDefinition` records stored in `ConcurrentDictionary<string, T>`
- ID Type: Simple `string` property
- ID Creation: Direct JSON deserialization into `Id` property

#### Consequence
- Behavior definitions may be loaded **twice** into different data stores
- No synchronization between `GameDataContext.Behaviors` and `TypeRegistry<BehaviorDefinition>`
- Lookups may succeed in one system but fail in another

---

### 2. ID Format Extraction Inconsistency

**Location**: `GameDataLoader.cs:956-968` (BehaviorEntity loading)

#### Code Analysis
```csharp
// Lines 956-968: Extract the behavior ID from the id
// Format: "base:behavior:movement/patrol" -> "patrol"
string behaviorId = dto.Id;
int lastSlash = behaviorId.LastIndexOf('/');
if (lastSlash >= 0)
{
    behaviorId = behaviorId[(lastSlash + 1)..];
}
// Also handle colon-based format
int lastColon = behaviorId.LastIndexOf(':');
if (lastColon >= 0)
{
    behaviorId = behaviorId[(lastColon + 1)..];
}
```

#### Problem
This extraction logic **strips namespace prefixes** from fully-qualified IDs:
- Input: `"base:behavior:movement/patrol"`
- Output: `"patrol"`

However, the same extraction logic is **NOT applied** to:
1. **TileBehaviorEntity loading** (line 1075): Uses `TryCreate` which preserves full format
2. **TypeRegistry loading**: Preserves full ID from JSON
3. **NpcSpawner** (external usage): Creates IDs with full namespace

#### Consequence
- BehaviorEntity IDs are **short names only** (`"patrol"`)
- TileBehaviorEntity IDs **may be fully-qualified** (`"base:tile_behavior:movement/ice"`)
- Database lookups will fail when using fully-qualified IDs for behaviors
- Inconsistent ID format across entity types

---

### 3. TileBehavior vs Behavior Entity Handling Asymmetry

**Comparison**:

| Aspect | BehaviorEntity | TileBehaviorEntity |
|--------|----------------|---------------------|
| **File** | GameDataLoader.cs:920-1000 | GameDataLoader.cs:1007-1110 |
| **ID Creation** | `GameBehaviorId.Create(behaviorId)` after stripping namespace | `GameTileBehaviorId.TryCreate(dto.Id) ?? GameTileBehaviorId.Create(dto.Id)` |
| **ID Format** | Short name only (`"patrol"`) | Full format supported (`"base:tile_behavior:movement/ice"`) |
| **ContentProvider Use** | No | **Yes** (line 1011-1015) |
| **Mod Override Support** | No explicit handling | **Yes** - detects source mod from path (line 1062) |
| **Extension Data** | No | **Yes** - stored in `ExtensionData` column (line 1066-1069) |

#### Problem
- **BehaviorEntity** loading is simpler but **less mod-aware**
- **TileBehaviorEntity** loading is **ContentProvider-aware** and properly handles mod overrides
- Inconsistent feature parity between similar entity types

---

### 4. ContentProvider Integration Inconsistency

**Location**: `GameDataLoader.cs` constructor and loading methods

#### Analysis

**ContentProvider Usage**:
```csharp
// Line 21: Optional dependency
private readonly IContentProvider? _contentProvider;

// Line 1011-1015: TileBehavior loading uses ContentProvider
if (_contentProvider != null)
{
    files = _contentProvider.GetAllContentPaths("TileBehaviors", "*.json");
}
```

**Problem**:
- **TileBehaviorEntity** loading uses `ContentProvider.GetAllContentPaths()` for mod-aware file discovery
- **BehaviorEntity** loading uses `Directory.GetFiles()` for direct filesystem access (line 928)
- Other definition types (Sprites, PopupBackgrounds, etc.) also use `Directory.GetFiles()`

#### Consequence
- TileBehaviors support mod content overrides via ContentProvider priority system
- Behaviors and other definitions do **NOT** support mod overrides during loading
- Inconsistent mod support across definition types

---

### 5. TypeRegistry ID vs Entity ID Mismatch

**Location**: TypeRegistry lookups vs Entity lookups

#### Problem

**TypeRegistry Pattern**:
```csharp
// TypeRegistry.cs:196-204
public T? Get(string typeId) {
    return _definitions.TryGetValue(typeId, out T? def) ? def : default;
}
```

**Entity Lookup Pattern** (from ValueConverters):
```csharp
// GameTileBehaviorIdValueConverter.cs:19
return GameTileBehaviorId.TryCreate(value) ?? GameTileBehaviorId.CreateMovement(value);
```

**Conflict**:
- TypeRegistry uses **simple string keys** (`"patrol"`)
- Entity system uses **strongly-typed IDs** (`GameBehaviorId.Create("patrol")`)
- BehaviorDefinition.Id is a **string property**
- BehaviorEntity.BehaviorId is a **GameBehaviorId value object**

#### Consequence
- Cannot query TypeRegistry and EF Core with the same ID value
- Need conversion layer between systems
- No clear migration path from TypeRegistry to Entity system

---

### 6. Mod Content Discovery Inconsistency

**Location**: ModLoader vs GameDataLoader

#### ModLoader Pattern (line 125-138):
```csharp
// Registers manifests (content folders become available to ContentProvider)
foreach (ModManifest manifest in orderedManifests)
{
    _loadedMods[manifest.Id] = manifest;
    _logger.LogInformation(
        "📁 Registered mod: {Name} v{Version} (priority {Priority}, {ContentCount} content folders)",
        manifest.Name, manifest.Version, manifest.Priority, manifest.ContentFolders.Count);
}
```

#### GameDataLoader Pattern (line 928):
```csharp
string[] files = Directory.GetFiles(path, "*.json", SearchOption.AllDirectories)
```

**Problem**:
- ModLoader correctly registers content folders with ContentProvider
- **MOST** GameDataLoader methods bypass ContentProvider and access filesystem directly
- **ONLY** TileBehaviorEntity loading uses ContentProvider (exception to the rule)

#### Consequence
- Mod content overrides work for TileBehaviors
- Mod content overrides **DO NOT WORK** for Behaviors, Sprites, PopupBackgrounds, etc.
- Inconsistent mod support across asset types

---

## TypeId Reference Analysis

### ID Format Journey Through System

**1. JSON File Format**:
```json
{
  "id": "base:behavior:movement/patrol",
  "displayName": "Patrol Behavior",
  "behaviorScript": "behaviors/patrol.csx"
}
```

**2. GameDataLoader Processing**:
```csharp
// Lines 956-968: Strips to "patrol"
string behaviorId = dto.Id;  // "base:behavior:movement/patrol"
int lastSlash = behaviorId.LastIndexOf('/');
if (lastSlash >= 0) {
    behaviorId = behaviorId[(lastSlash + 1)..];  // "patrol"
}
```

**3. Entity Creation**:
```csharp
// Line 973: Creates GameBehaviorId with short name
BehaviorId = GameBehaviorId.Create(behaviorId),  // GameBehaviorId("patrol")
```

**4. TypeRegistry Processing** (IF used):
```csharp
// TypeRegistry.cs:146-163: Preserves full ID
T? definition = JsonSerializer.Deserialize<T>(json, options);
_definitions[definition.Id] = definition;  // Stores "base:behavior:movement/patrol"
```

**5. Lookup Mismatch**:
```csharp
// NpcSpawner.cs:126 creates full ID
GameBehaviorId behaviorId = GameBehaviorId.Create(behaviorIdStr);

// But database only has short name "patrol" stored
// Lookup will FAIL if behaviorIdStr is "base:behavior:movement/patrol"
```

---

## Root Cause Analysis

### Architectural Debt: Mid-Migration State

The codebase is **partially migrated** from:
- **OLD**: `TypeRegistry<ITypeDefinition>` with string IDs
- **NEW**: EF Core entities with strongly-typed IDs

### Evidence:
1. **BehaviorDefinition.cs** (line 27): Still has `required string Id` property
2. **BehaviorEntity.cs** (line 21): Uses `GameBehaviorId BehaviorId` property
3. **TypeRegistry.cs** still exists and is functional
4. **GameDataLoader.cs** loads into EF Core but doesn't replace TypeRegistry usage

### Migration Not Completed:
- TypeRegistry is still referenced in multiple places
- No deprecation warnings or migration guide
- Both systems load definitions independently
- No data synchronization between systems

---

## Impact Assessment

### High Severity Issues

1. **Data Inconsistency**: Definitions exist in two separate data stores with no synchronization
2. **Lookup Failures**: ID format mismatches cause runtime lookup failures
3. **Mod Override Failures**: Most asset types don't support mod content overrides despite ModLoader registering them
4. **Memory Waste**: Definitions loaded twice into separate systems

### Medium Severity Issues

1. **Inconsistent Mod Support**: Only TileBehaviors properly integrate with ContentProvider
2. **ID Format Confusion**: Developers must know when to use short vs full IDs
3. **Code Duplication**: Two parallel loading paths with different capabilities

### Low Severity Issues

1. **Missing Extension Data**: BehaviorEntity doesn't support extension data like TileBehaviorEntity does
2. **Inconsistent Logging**: Different loading methods have different log formats

---

## Recommendations

### Immediate Actions (Priority 1)

1. **Standardize ID Handling in GameDataLoader**:
   - File: `/MonoBallFramework.Game/GameData/Loading/GameDataLoader.cs:956-968`
   - **ISSUE**: Remove ID stripping logic for BehaviorEntity
   - **FIX**: Use same pattern as TileBehaviorEntity (line 1075):
     ```csharp
     BehaviorId = GameBehaviorId.TryCreate(dto.Id) ?? GameBehaviorId.Create(dto.Id)
     ```

2. **Implement ContentProvider for All Definitions**:
   - File: `/MonoBallFramework.Game/GameData/Loading/GameDataLoader.cs:920-1000`
   - **ISSUE**: BehaviorEntity loading bypasses ContentProvider
   - **FIX**: Apply same ContentProvider pattern used in TileBehaviorEntity loading (lines 1011-1015)

3. **Add Extension Data Support to BehaviorEntity**:
   - File: `/MonoBallFramework.Game/GameData/Entities/BehaviorEntity.cs`
   - **ISSUE**: Missing `ExtensionData` column
   - **FIX**: Add `ExtensionData` property matching TileBehaviorEntity pattern (line 68)

### Medium-Term Actions (Priority 2)

4. **Deprecate TypeRegistry**:
   - Add `[Obsolete]` attributes to TypeRegistry
   - Update all consumers to use EF Core entities
   - Remove TypeRegistry after migration complete

5. **Standardize Loading Patterns**:
   - Create `BaseDefinitionLoader<TEntity, TDto>` abstract class
   - Enforce ContentProvider usage for all definition types
   - Unify extension data handling

6. **Add Integration Tests**:
   - Test mod content override for all definition types
   - Test ID format consistency across lookups
   - Test TypeRegistry vs Entity synchronization

### Long-Term Actions (Priority 3)

7. **Document Architecture Decision**:
   - Create ADR for TypeRegistry deprecation
   - Document migration guide for consumers
   - Update API documentation

8. **Create Migration Tool**:
   - Build tool to migrate TypeRegistry usage to EF Core
   - Automated refactoring for ID format changes

---

## File-Specific Issues

### `/MonoBallFramework.Game/GameData/Loading/GameDataLoader.cs`

| Line | Issue | Severity | Fix |
|------|-------|----------|-----|
| 956-968 | ID format stripping inconsistent with TileBehaviorEntity | HIGH | Remove stripping, use TryCreate pattern |
| 928 | Direct filesystem access bypasses ContentProvider | HIGH | Use `_contentProvider.GetAllContentPaths()` |
| 973 | Creates short ID when full ID available in DTO | HIGH | Use `dto.Id` directly with TryCreate |
| 920-1000 | No extension data handling | MEDIUM | Add ExtensionData serialization |
| 920-1000 | No source mod detection | MEDIUM | Add `DetectSourceModFromPath()` call |

### `/MonoBallFramework.Game/GameData/Entities/BehaviorEntity.cs`

| Line | Issue | Severity | Fix |
|------|-------|----------|-----|
| N/A | Missing `ExtensionData` property | MEDIUM | Add column matching TileBehaviorEntity.cs:67-68 |
| N/A | Missing computed properties for mod detection | LOW | Add `IsFromMod` property |
| N/A | Missing extension data parsing helpers | LOW | Add `GetExtensionProperty<T>()` method |

### `/MonoBallFramework.Game/Engine/Core/Types/BehaviorDefinition.cs`

| Line | Issue | Severity | Fix |
|------|-------|----------|-----|
| 27 | String ID conflicts with GameBehaviorId system | HIGH | Mark as obsolete or add conversion methods |
| 7 | IScriptedType still in use despite Entity system | MEDIUM | Document deprecation path |

### `/MonoBallFramework.Game/Engine/Core/Modding/ModLoader.cs`

| Line | Issue | Severity | Fix |
|------|-------|----------|-----|
| 125-138 | Registers content folders but GameDataLoader doesn't use them | HIGH | Document that only TileBehaviors use ContentProvider |
| 358-365 | Comment says content folders handled by data loader but most types don't | MEDIUM | Update comment to reflect reality |

### `/MonoBallFramework.Game/Engine/Content/ContentProvider.cs`

| Line | Issue | Severity | Fix |
|------|-------|----------|-----|
| N/A | No issues found | N/A | ContentProvider implementation is solid |

---

## Testing Gaps

### Missing Test Coverage

1. **Mod Content Override Tests**:
   - Test that BehaviorEntity loading respects mod priority
   - Test that mod definitions override base game definitions
   - Test extension data from mods is preserved

2. **ID Format Tests**:
   - Test full ID format (`"base:behavior:movement/patrol"`) works for database lookup
   - Test short ID format (`"patrol"`) works for database lookup
   - Test conversion between formats

3. **ContentProvider Integration Tests**:
   - Test all definition types use ContentProvider
   - Test priority ordering (mods over base game)
   - Test negative caching works

---

## Conclusion

The mod loading and content system has **critical inconsistencies** stemming from an **incomplete architectural migration**. The system is caught between two paradigms (TypeRegistry vs EF Core Entities) with no clear completion path.

**Immediate action required**:
1. Standardize BehaviorEntity loading to match TileBehaviorEntity pattern
2. Make ContentProvider usage consistent across all definition types
3. Remove ID format stripping to preserve full IDs in database

**Strategic recommendation**:
Complete the migration to EF Core entities and deprecate TypeRegistry to eliminate dual loading paths and synchronization issues.

---

## Appendix: Key Code References

### BehaviorEntity Loading (Inconsistent)
- File: `/MonoBallFramework.Game/GameData/Loading/GameDataLoader.cs`
- Lines: 920-1000
- ID Creation: Line 973 - `GameBehaviorId.Create(behaviorId)` after stripping
- ContentProvider: Not used (line 928 uses `Directory.GetFiles()`)

### TileBehaviorEntity Loading (Reference Implementation)
- File: `/MonoBallFramework.Game/GameData/Loading/GameDataLoader.cs`
- Lines: 1007-1110
- ID Creation: Line 1075 - `TryCreate(dto.Id) ?? Create(dto.Id)` preserves format
- ContentProvider: Used (lines 1011-1015)
- Extension Data: Supported (lines 1066-1069)
- Mod Detection: Supported (line 1062)

### ModLoader Content Registration
- File: `/MonoBallFramework.Game/Engine/Core/Modding/ModLoader.cs`
- Lines: 125-138 - Registers content folders with ContentProvider
- Lines: 358-365 - Comment indicates data loader should handle content folders

### ContentProvider Implementation
- File: `/MonoBallFramework.Game/Engine/Content/ContentProvider.cs`
- Lines: 110-139 - Mod priority resolution logic
- Lines: 175-287 - GetAllContentPaths with deduplication

---

**End of Analysis Report**
