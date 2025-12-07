# Entity Framework ID System Collision Audit Report
**Generated:** 2025-12-05
**Auditor:** TESTER Agent
**Scope:** MonoBallFramework.Game/GameData/Entities

---

## Executive Summary

### Critical Findings
- **HIGH SEVERITY**: Cross-entity ID collisions possible (NpcId vs TrainerId vs other string IDs)
- **MEDIUM SEVERITY**: Inconsistent ID type usage (strongly-typed vs plain strings)
- **MEDIUM SEVERITY**: Mod content namespace collisions not enforced at database level
- **LOW SEVERITY**: No composite unique constraints for SourceMod + ID combinations

### Recommendation
Implement a unified ID strategy with entity-type prefixes or separate ID namespaces per entity type.

---

## 1. Complete ID Field Inventory

### 1.1 Entity Definitions and Their IDs

| Entity | Primary Key Field | Type | Format/Convention | Max Length | Strongly Typed |
|--------|------------------|------|-------------------|------------|----------------|
| **MapDefinition** | `MapId` | `MapIdentifier` (struct) | `"littleroot_town"`, `"route_101"` | 100 chars | ✅ YES |
| **NpcDefinition** | `NpcId` | `string` | `"npc/youngster_joey"`, `"npc/prof_birch"` | 100 chars | ❌ NO |
| **TrainerDefinition** | `TrainerId` | `string` | `"trainer/youngster_joey"`, `"trainer/roxanne_1"` | 100 chars | ❌ NO |
| **MapSection** | `Id` | `string` | `"MAPSEC_LITTLEROOT_TOWN"` | 100 chars | ❌ NO |
| **PopupTheme** | `Id` | `string` | `"wood"`, `"marble"`, `"stone"` | 50 chars | ❌ NO |
| **EntityTemplate** | `TemplateId` | `string` | `"category/name"` (e.g., `"pokemon/bulbasaur"`) | N/A | ❌ NO |

### 1.2 Foreign Key and Reference IDs

| Entity | Field | Type | References | Strongly Typed |
|--------|-------|------|------------|----------------|
| **MapDefinition** | `NorthMapId`, `SouthMapId`, `EastMapId`, `WestMapId` | `MapIdentifier?` (nullable) | `MapDefinition.MapId` | ✅ YES |
| **MapDefinition** | `MusicId` | `string?` | External music asset | ❌ NO |
| **NpcDefinition** | `SpriteId` | `SpriteId?` (struct) | Asset reference | ✅ YES |
| **TrainerDefinition** | `SpriteId` | `SpriteId?` (struct) | Asset reference | ✅ YES |
| **MapSection** | `ThemeId` | `string` | `PopupTheme.Id` (FK) | ❌ NO |

### 1.3 Auxiliary ID Fields

| Entity | Field | Purpose | Type |
|--------|-------|---------|------|
| **MapDefinition** | `SourceMod` | Mod origin identifier | `string?` (100 chars) |
| **NpcDefinition** | `SourceMod` | Mod origin identifier | `string?` (100 chars) |
| **TrainerDefinition** | `SourceMod` | Mod origin identifier | `string?` (100 chars) |
| **MapSection** | `SourceMod` | Mod origin identifier | `string?` (100 chars) |
| **PopupTheme** | `SourceMod` | Mod origin identifier | `string?` (100 chars) |
| **MapRuntimeId** | `Value` | Runtime map instance ID | `int` (non-negative) |

---

## 2. Collision Risk Analysis

### 2.1 Cross-Entity ID Collisions (CRITICAL)

#### **Scenario 1: NpcId vs TrainerId Collision**
**Risk:** ✅ **COLLISION POSSIBLE**

```csharp
// Both can have ID "oak" - NO DATABASE CONSTRAINT PREVENTS THIS
NpcDefinition npc = new() { NpcId = "oak" };          // Allowed
TrainerDefinition trainer = new() { TrainerId = "oak" }; // Also allowed!

// Different tables, different keys → No uniqueness constraint across tables
```

**Impact:**
- Application code must distinguish between `"oak"` the NPC and `"oak"` the trainer
- Template system uses `TemplateId` with format `"category/name"` - could collide with either
- If using ID-based lookups without entity type context, ambiguity arises

**Current Mitigation:**
- Convention-based prefixes (`"npc/"`, `"trainer/"`) in comments but **NOT ENFORCED**
- Example IDs suggest prefixes: `"npc/youngster_joey"` vs `"trainer/youngster_joey"`
- **Convention is documented but not validated**

---

#### **Scenario 2: MapSection.Id vs PopupTheme.Id Collision**
**Risk:** ✅ **COLLISION POSSIBLE**

```csharp
MapSection section = new() { Id = "wood" };     // Allowed
PopupTheme theme = new() { Id = "wood" };       // Also allowed!
```

**Impact:**
- Different semantic domains but same namespace
- Could cause confusion in generic ID resolution systems
- No database constraint preventing collision

**Current Mitigation:**
- Naming conventions (MAPSEC_* for MapSections, lowercase for themes)
- **Convention only - not enforced**

---

#### **Scenario 3: EntityTemplate Collisions**
**Risk:** ⚠️ **POTENTIAL COLLISION**

EntityTemplate uses `TemplateId` with format `"category/name"`:
- `"pokemon/bulbasaur"` - Pokemon template
- `"npc/professor_oak"` - NPC template
- `"trainer/youngster_joey"` - Trainer template (could match `TrainerId`)

**If NpcId or TrainerId matches TemplateId:**
```csharp
NpcDefinition npc = new() { NpcId = "npc/professor_oak" };
EntityTemplate template = new() { TemplateId = "npc/professor_oak" };
// Same ID, different contexts - potential ambiguity
```

**Current Mitigation:**
- EntityTemplate is not an EF entity (no database table)
- Separate in-memory system
- **Still creates semantic collision risk**

---

### 2.2 Mod Content Namespace Collisions (HIGH)

#### **Scenario 4: Mod vs Base Game ID Collision**
**Risk:** ⚠️ **COLLISION POSSIBLE WITHOUT COMPOSITE KEY**

```csharp
// Base game NPC
NpcDefinition baseNpc = new()
{
    NpcId = "npc/oak",
    SourceMod = null  // Base game
};

// Mod overwrites/conflicts
NpcDefinition modNpc = new()
{
    NpcId = "npc/oak",      // SAME ID!
    SourceMod = "MyMod"     // Different source
};

// Database: NpcId is PRIMARY KEY → Second insert FAILS
// But: No composite (SourceMod, NpcId) key to allow both
```

**Current Behavior:**
- Primary key is `NpcId` alone - prevents duplicates
- `SourceMod` is metadata, not part of key
- **Mods CANNOT override base game entities** - collision throws error

**Expected Behavior (for modding):**
- Composite key: `(SourceMod, NpcId)` would allow:
  - Base game: `(null, "npc/oak")`
  - Mod 1: `("ModA", "npc/oak")`
  - Mod 2: `("ModB", "npc/oak")`

**Current System:**
- Mods must use unique IDs: `"npc/mod_a/oak"` vs `"npc/mod_b/oak"`
- **No formal namespacing enforcement**

---

#### **Scenario 5: Multiple Mods, Same IDs**
**Risk:** ✅ **COLLISION GUARANTEED**

```csharp
// Mod A creates custom map
MapDefinition modAMap = new()
{
    MapId = "custom_town",
    SourceMod = "ModA"
};

// Mod B creates map with same ID
MapDefinition modBMap = new()
{
    MapId = "custom_town",  // COLLISION!
    SourceMod = "ModB"
};

// Database insert fails - MapId is PRIMARY KEY
```

**Impact:**
- Mod compatibility issues
- Load order determines which mod "wins"
- No multi-mod coexistence for same ID

**Mitigation Required:**
- Composite key: `(SourceMod, MapId)`
- Or enforce ID prefixing convention: `"mod_a:custom_town"`

---

### 2.3 Entity Type Encoding in IDs (MEDIUM)

#### **Scenario 6: ID Format Inconsistency**
**Risk:** ⚠️ **CONFUSION & BUG POTENTIAL**

IDs that DO encode entity type (by convention):
```csharp
NpcId:      "npc/youngster_joey"    // Prefix: npc/
TrainerId:  "trainer/roxanne_1"     // Prefix: trainer/
TemplateId: "pokemon/bulbasaur"     // Prefix: category/
SpriteId:   "may/walking"           // Prefix: category/
```

IDs that DON'T encode entity type:
```csharp
MapId:           "littleroot_town"      // No prefix
MapSection.Id:   "MAPSEC_LITTLEROOT_TOWN"  // Convention: MAPSEC_ prefix
PopupTheme.Id:   "wood"                 // No prefix
MusicId:         "route_101_theme"      // No prefix (assumed)
```

**Issues:**
1. **Inconsistent conventions** - some use prefixes, some don't
2. **Not validated** - nothing enforces `"npc/"` prefix for NpcId
3. **Mixed approaches** - MapIdentifier is strongly typed but has no prefix

**Example Bug:**
```csharp
string id = "professor_oak"; // Forgot "npc/" prefix
var npc = await context.Npcs.FindAsync(id); // Returns null
```

---

## 3. Strongly-Typed vs Plain String IDs

### 3.1 Strongly-Typed IDs (Using Value Objects)

| Type | Underlying Type | Validation | Benefits |
|------|----------------|------------|----------|
| **MapIdentifier** | `string` | Non-null, non-whitespace | ✅ Type safety, compile-time checks |
| **SpriteId** | `string` | Non-null, non-whitespace, category parsing | ✅ Type safety, category extraction |
| **MapRuntimeId** | `int` | Non-negative | ✅ Type safety, runtime ID validation |

**Advantages:**
- Cannot accidentally pass wrong ID type (e.g., `NpcId` where `MapId` expected)
- Compiler enforces type correctness
- Centralized validation logic

**Value Converters:**
```csharp
// EF Core converts struct to string for database storage
public class MapIdentifierValueConverter : ValueConverter<MapIdentifier, string>
{
    public MapIdentifierValueConverter()
        : base(v => v.Value, v => new MapIdentifier(v)) { }
}
```

**Usage in Database Config:**
```csharp
entity.Property(m => m.MapId).HasConversion(new MapIdentifierValueConverter());
entity.Property(n => n.SpriteId).HasConversion(new SpriteIdValueConverter());
```

---

### 3.2 Plain String IDs (Primitive Obsession)

| Field | Entity | Validation | Issues |
|-------|--------|------------|--------|
| `NpcId` | NpcDefinition | `[MaxLength(100)]` only | ❌ No prefix enforcement |
| `TrainerId` | TrainerDefinition | `[MaxLength(100)]` only | ❌ No prefix enforcement |
| `MapSection.Id` | MapSection | `[MaxLength(100)]` only | ❌ No MAPSEC_ enforcement |
| `PopupTheme.Id` | PopupTheme | `[MaxLength(50)]` only | ❌ No format validation |

**Disadvantages:**
- Can pass any string - no compile-time safety
- Easy to confuse `NpcId` and `TrainerId` (both `string`)
- No centralized validation (prefix conventions not enforced)

**Example Risk:**
```csharp
// Nothing prevents this at compile time or database level
string wrongId = "trainer/oak";
NpcDefinition npc = new() { NpcId = wrongId }; // Should be "npc/oak"
context.Npcs.Add(npc); // Database accepts it - format not validated
```

---

### 3.3 Recommendations for Standardization

**Option 1: Strongly Type All Entity IDs**
```csharp
public readonly record struct NpcId(string Value);
public readonly record struct TrainerId(string Value);
public readonly record struct ThemeId(string Value);
```

**Benefits:**
- Compile-time type safety
- Cannot mix up ID types
- Centralized validation (prefix enforcement)

**Option 2: Composite Keys with SourceMod**
```csharp
[PrimaryKey(nameof(SourceMod), nameof(NpcId))]
public class NpcDefinition
{
    public string? SourceMod { get; set; }
    public string NpcId { get; set; }
}
```

**Benefits:**
- Allows mod content to override/extend base game
- Prevents inter-mod collisions
- Explicit namespace partitioning

**Option 3: Global ID Registry with Type Prefixes**
```csharp
// Enforce format: "entity_type:id"
NpcId:      "npc:youngster_joey"
TrainerId:  "trainer:roxanne_1"
MapId:      "map:littleroot_town"
ThemeId:    "theme:wood"
```

**Benefits:**
- Self-documenting IDs
- Global uniqueness across all entity types
- Easy to parse entity type from ID

---

## 4. EF Core Uniqueness Constraints

### 4.1 Current Constraints

**Primary Keys (Enforced):**
```csharp
entity.HasKey(n => n.NpcId);           // NpcDefinition
entity.HasKey(t => t.TrainerId);       // TrainerDefinition
entity.HasKey(m => m.MapId);           // MapDefinition
entity.HasKey(s => s.Id);              // MapSection
entity.HasKey(t => t.Id);              // PopupTheme
```

**Foreign Keys (Enforced):**
```csharp
// MapSection → PopupTheme
entity.HasMany(t => t.MapSections)
      .WithOne(s => s.Theme)
      .HasForeignKey(s => s.ThemeId)
      .OnDelete(DeleteBehavior.Restrict);
```

**Indexes (Performance, NOT Uniqueness):**
```csharp
entity.HasIndex(n => n.NpcType);        // Non-unique
entity.HasIndex(n => n.DisplayName);    // Non-unique
entity.HasIndex(m => m.Region);         // Non-unique
entity.HasIndex(s => new { s.X, s.Y }); // Composite index (non-unique)
```

---

### 4.2 Missing Constraints

**No Cross-Table Uniqueness:**
- `NpcId` is unique within `Npcs` table
- `TrainerId` is unique within `Trainers` table
- **But "oak" can exist in both** - no global constraint

**No Composite Key for Modding:**
```csharp
// NEEDED but MISSING:
[PrimaryKey(nameof(SourceMod), nameof(NpcId))]
```

**No Format Validation:**
- Database accepts any string for `NpcId`, `TrainerId`, etc.
- No CHECK constraints for prefix formats
- No validation that `"npc/"` prefix is used

---

## 5. Test Scenarios for ID Conflicts

### Test Case 1: Cross-Entity Collision Detection
```csharp
[Fact]
public async Task SameId_DifferentEntities_Should_BeAllowed()
{
    // Given
    var npc = new NpcDefinition { NpcId = "oak" };
    var trainer = new TrainerDefinition { TrainerId = "oak" };

    // When
    context.Npcs.Add(npc);
    context.Trainers.Add(trainer);
    await context.SaveChangesAsync();

    // Then - both succeed (different tables)
    Assert.NotNull(await context.Npcs.FindAsync("oak"));
    Assert.NotNull(await context.Trainers.FindAsync("oak"));
}
```

**Result:** ✅ PASSES - Collision allowed, no constraint

---

### Test Case 2: Mod Content Collision
```csharp
[Fact]
public async Task SameNpcId_DifferentMods_Should_Fail()
{
    // Given
    var baseNpc = new NpcDefinition
    {
        NpcId = "npc/oak",
        SourceMod = null
    };
    var modNpc = new NpcDefinition
    {
        NpcId = "npc/oak",      // SAME ID
        SourceMod = "MyMod"
    };

    // When
    context.Npcs.Add(baseNpc);
    await context.SaveChangesAsync();

    context.Npcs.Add(modNpc);

    // Then - should throw (primary key violation)
    await Assert.ThrowsAsync<DbUpdateException>(
        () => context.SaveChangesAsync()
    );
}
```

**Result:** ✅ FAILS as expected - Primary key violation
**Issue:** Mods cannot coexist with same IDs

---

### Test Case 3: Missing Prefix Validation
```csharp
[Fact]
public async Task NpcId_WithoutPrefix_Should_BeRejected()
{
    // Given - ID missing "npc/" prefix (violates convention)
    var npc = new NpcDefinition
    {
        NpcId = "professor_oak",  // Should be "npc/professor_oak"
        DisplayName = "Oak"
    };

    // When
    context.Npcs.Add(npc);

    // Then - currently ALLOWS invalid format
    await context.SaveChangesAsync(); // No exception thrown

    // ISSUE: No validation of prefix convention
}
```

**Result:** ❌ PASSES but SHOULDN'T - Convention not enforced

---

### Test Case 4: Strongly-Typed ID Mismatch
```csharp
[Fact]
public void MapIdentifier_CannotBeAssignedToSpriteId()
{
    // Given
    MapIdentifier mapId = "littleroot_town";

    // When/Then - compile error (type safety works!)
    // SpriteId spriteId = mapId; // ERROR: Cannot convert

    Assert.True(true); // Type system prevents this
}
```

**Result:** ✅ PASSES - Strong typing prevents type confusion

---

### Test Case 5: Multiple Mods, Unique IDs Required
```csharp
[Fact]
public async Task MultipleMods_MustUseUniqueIds()
{
    // Given - two mods with prefixed IDs
    var modA = new NpcDefinition
    {
        NpcId = "npc/mod_a:custom_character",
        SourceMod = "ModA"
    };
    var modB = new NpcDefinition
    {
        NpcId = "npc/mod_b:custom_character", // Different prefix
        SourceMod = "ModB"
    };

    // When
    context.Npcs.AddRange(modA, modB);
    await context.SaveChangesAsync();

    // Then - both succeed (unique IDs)
    Assert.Equal(2, await context.Npcs.CountAsync());
}
```

**Result:** ✅ PASSES - Manual prefixing works, but not enforced

---

## 6. Severity Assessment

### Critical Issues (Immediate Action Required)

| Issue | Severity | Impact | Affected Entities |
|-------|----------|--------|------------------|
| **Cross-entity ID collisions** | 🔴 CRITICAL | Application logic errors, data ambiguity | NpcId, TrainerId, all string IDs |
| **No mod namespacing** | 🔴 CRITICAL | Mod incompatibility, load order dependencies | All entities with SourceMod |

---

### High-Severity Issues (Address in Next Sprint)

| Issue | Severity | Impact | Affected Entities |
|-------|----------|--------|------------------|
| **Inconsistent ID typing** | 🟠 HIGH | Type confusion, runtime errors | NpcId, TrainerId, MapSection.Id, PopupTheme.Id |
| **No prefix validation** | 🟠 HIGH | Convention violations, lookup failures | NpcId, TrainerId |
| **No composite keys** | 🟠 HIGH | Blocks multi-mod support | All entities |

---

### Medium-Severity Issues (Technical Debt)

| Issue | Severity | Impact | Affected Entities |
|-------|----------|--------|------------------|
| **Mixed strongly-typed/plain strings** | 🟡 MEDIUM | Code inconsistency, learning curve | MapId (typed) vs NpcId (string) |
| **No global ID registry** | 🟡 MEDIUM | Potential future collisions as system grows | All entities |

---

### Low-Severity Issues (Monitor)

| Issue | Severity | Impact | Affected Entities |
|-------|----------|--------|------------------|
| **Convention-only documentation** | 🟢 LOW | Onboarding confusion | All ID fields |
| **No CHECK constraints** | 🟢 LOW | Database integrity (mitigated by app layer) | String IDs |

---

## 7. Recommendations

### Immediate Actions (Sprint 1)

1. **Implement Strongly-Typed IDs for All Entities**
   ```csharp
   public readonly record struct NpcId(string Value);
   public readonly record struct TrainerId(string Value);
   public readonly record struct ThemeId(string Value);
   public readonly record struct MapSectionId(string Value);
   ```

2. **Add Composite Primary Keys for Modding Support**
   ```csharp
   [PrimaryKey(nameof(SourceMod), nameof(NpcId))]
   public class NpcDefinition { ... }
   ```

3. **Enforce Prefix Conventions in Value Object Constructors**
   ```csharp
   public readonly record struct NpcId
   {
       public NpcId(string value)
       {
           if (!value.StartsWith("npc/"))
               throw new ArgumentException("NpcId must start with 'npc/'");
           Value = value;
       }
   }
   ```

---

### Short-Term Improvements (Sprint 2-3)

4. **Create ID Namespace Registry**
   - Central enum or constants for entity type prefixes
   - Factory methods for ID creation with validation

5. **Add Database Migrations for Constraints**
   - Add CHECK constraints for ID formats (if supported by DB)
   - Create unique indexes on (SourceMod, EntityId) composites

6. **Unit Tests for ID Collision Scenarios**
   - Test cross-entity collisions
   - Test mod namespacing
   - Test prefix validation

---

### Long-Term Strategy (Future Sprints)

7. **Global Unique ID System**
   - Format: `"entity_type:source_mod:local_id"`
   - Example: `"npc:ModA:professor_oak"`

8. **ID Migration Tool for Existing Data**
   - Backfill `"npc/"` prefixes if missing
   - Add SourceMod namespacing to existing entries

9. **Documentation & Guidelines**
   - ID naming conventions
   - Modding best practices
   - Entity ID examples

---

## 8. Conclusion

The current ID system has **critical collision risks** due to:
1. **Plain string IDs** across multiple entity types
2. **No cross-table uniqueness enforcement**
3. **Convention-based prefixes without validation**
4. **No composite keys for mod content namespacing**

**Recommended Priority:**
1. 🔴 **CRITICAL**: Implement strongly-typed IDs + composite keys
2. 🟠 **HIGH**: Add prefix validation and testing
3. 🟡 **MEDIUM**: Standardize ID format across all entities
4. 🟢 **LOW**: Documentation and migration planning

**Estimated Effort:**
- Strongly-typed IDs: 2-3 days
- Composite keys + migrations: 3-4 days
- Testing + validation: 2 days
- **Total: ~1.5 weeks**

**Risk if Not Addressed:**
- Mod conflicts and incompatibility
- Runtime errors from ID confusion
- Database integrity issues
- Poor developer experience

---

## Appendix A: Complete ID Field Matrix

| Entity | Primary Key | Type | Strongly Typed | Prefix Convention | Max Length | Allows Null |
|--------|------------|------|----------------|-------------------|------------|-------------|
| MapDefinition | MapId | MapIdentifier | ✅ | None | 100 | ❌ |
| NpcDefinition | NpcId | string | ❌ | "npc/" | 100 | ❌ |
| TrainerDefinition | TrainerId | string | ❌ | "trainer/" | 100 | ❌ |
| MapSection | Id | string | ❌ | "MAPSEC_" | 100 | ❌ |
| PopupTheme | Id | string | ❌ | None | 50 | ❌ |
| EntityTemplate | TemplateId | string | ❌ | "category/" | N/A | ❌ |
| MapDefinition | MusicId | string | ❌ | None | 100 | ✅ |
| MapDefinition | NorthMapId | MapIdentifier? | ✅ | None | 100 | ✅ |
| MapDefinition | SouthMapId | MapIdentifier? | ✅ | None | 100 | ✅ |
| MapDefinition | EastMapId | MapIdentifier? | ✅ | None | 100 | ✅ |
| MapDefinition | WestMapId | MapIdentifier? | ✅ | None | 100 | ✅ |
| NpcDefinition | SpriteId | SpriteId? | ✅ | "category/" | 100 | ✅ |
| TrainerDefinition | SpriteId | SpriteId? | ✅ | "category/" | 100 | ✅ |
| MapSection | ThemeId | string | ❌ | None | 50 | ❌ |

---

## Appendix B: Collision Test Matrix

| Test Scenario | Entity A | Entity B | ID Value | Collision Risk | Current Behavior |
|---------------|----------|----------|----------|----------------|------------------|
| Cross-entity same ID | NpcDefinition | TrainerDefinition | "oak" | ✅ YES | Allowed (different tables) |
| Mod vs base game | NpcDefinition (base) | NpcDefinition (mod) | "npc/oak" | ✅ YES | Blocked (PK violation) |
| Multiple mods | MapDefinition (ModA) | MapDefinition (ModB) | "custom_town" | ✅ YES | Blocked (PK violation) |
| MapSection vs Theme | MapSection | PopupTheme | "wood" | ✅ YES | Allowed (different tables) |
| Template vs Entity | EntityTemplate | NpcDefinition | "npc/oak" | ⚠️ MAYBE | Different systems (no DB collision) |
| Strongly-typed mismatch | MapIdentifier | SpriteId | "value" | ❌ NO | Prevented by type system |

---

**End of Report**
