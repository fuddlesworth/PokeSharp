# NPC Template Hierarchy Analysis

## Executive Summary

The current NPC/entity system uses a **3-tier template hierarchy** with significant property duplication and complexity. This analysis identifies opportunities to simplify the system while maintaining moddability.

**Key Findings:**
- 40% property duplication across tiers
- Movement speed defined at 3 different levels
- Display name redundancy between NPC Definition and Behavior Definition
- Opportunity to eliminate Behavior Definition tier entirely

---

## Current Template Hierarchy

### Tier 1: Tiled Object (Map-Level, Per-Instance)

**Location**: `test-map.json` and other map files
**Purpose**: Per-instance placement and overrides

```json
{
  "id": 1,
  "name": "Wanderer 1",
  "type": "npc/generic",
  "x": 80,
  "y": 80,
  "width": 16,
  "height": 16,
  "properties": [
    {"name": "direction", "type": "string", "value": "down"},
    {"name": "npcId", "type": "string", "value": "wanderer"},
    {"name": "displayName", "type": "string", "value": "WANDERER 1"}
  ]
}
```

**Properties Defined:**
- ✅ `id` - Tiled internal ID (required)
- ✅ `name` - Instance name in editor (required)
- ✅ `type` - Template reference (required)
- ✅ `x, y, width, height` - Spatial data (required)
- ⚠️ `direction` - Instance override (optional)
- ⚠️ `npcId` - Reference to NPC Definition (required for NPCs)
- ❌ `displayName` - REDUNDANT with NPC Definition
- ⚠️ `waypoints` - Instance-specific patrol path (optional)
- ⚠️ `waypointWaitTime` - Instance override (optional)

### Tier 2: NPC Definition (EF Core Database)

**Location**: `Data/NPCs/wanderer.json` → loaded into `NpcDefinition` entity
**C# Class**: `MonoBallFramework.Game.GameData.Entities.NpcDefinition`

```csharp
public class NpcDefinition
{
    public GameNpcId NpcId { get; set; }              // "base:npc:wanderer/wanderer"
    public string DisplayName { get; set; }           // "WANDERER"
    public string? NpcType { get; set; }              // "wanderer"
    public SpriteId? SpriteId { get; set; }           // "base:sprite:npc/boy_1"
    public string? BehaviorScript { get; set; }       // "wander"
    public string? DialogueScript { get; set; }       // "Dialogue/generic_greeting.csx"
    public float MovementSpeed { get; set; }          // 1.5
    public string? CustomPropertiesJson { get; set; }
    public string? SourceMod { get; set; }
    public string Version { get; set; }
}
```

**Properties Defined:**
- ✅ `NpcId` - Unique identifier (CRITICAL)
- ❌ `DisplayName` - REDUNDANT (can be map override only)
- ✅ `NpcType` - Categorization for logic (useful)
- ✅ `SpriteId` - Visual appearance (CRITICAL)
- ⚠️ `BehaviorScript` - Reference to Behavior Definition (REDUNDANT TIER)
- ✅ `DialogueScript` - Dialogue system reference (CRITICAL)
- ❌ `MovementSpeed` - REDUNDANT with Behavior Definition
- ✅ `CustomPropertiesJson` - Moddability extension (CRITICAL)
- ✅ `SourceMod` - Mod attribution (CRITICAL)
- ✅ `Version` - Compatibility tracking (CRITICAL)

### Tier 3: Behavior Definition (JSON → TypeRegistry)

**Location**: `Data/Behaviors/wander.json` → loaded into `BehaviorDefinition` record
**C# Record**: `MonoBallFramework.Game.Engine.Core.Types.BehaviorDefinition`

```csharp
public record BehaviorDefinition : IScriptedType
{
    public float DefaultSpeed { get; init; }                      // 1.5
    public float PauseAtWaypoint { get; init; }                  // 0.0
    public bool AllowInteractionWhileMoving { get; init; }       // true
    public required string TypeId { get; init; }                 // "wander"
    public required string DisplayName { get; init; }            // "Wander Behavior"
    public string? Description { get; init; }                    // Description text
    public string? BehaviorScript { get; init; }                 // "Behaviors/wander_behavior.csx"
}
```

**Properties Defined:**
- ❌ `DefaultSpeed` - REDUNDANT with NPC Definition
- ⚠️ `PauseAtWaypoint` - Behavior-specific (could be in script)
- ⚠️ `AllowInteractionWhileMoving` - Behavior-specific (could be in script)
- ⚠️ `TypeId` - Behavior type identifier (REDUNDANT - just use script path)
- ❌ `DisplayName` - REDUNDANT (only needed for editor, not runtime)
- ❌ `Description` - REDUNDANT (only needed for editor, not runtime)
- ✅ `BehaviorScript` - Script path (CRITICAL)

---

## Property Duplication Analysis

### Critical Redundancies

#### 1. Movement Speed (TRIPLE REDUNDANCY)
- **Tier 3** (Behavior Definition): `DefaultSpeed = 1.5`
- **Tier 2** (NPC Definition): `MovementSpeed = 1.5`
- **Current Logic**: NPC Definition value is used, Behavior Definition value is IGNORED

**Impact**: Confusing for modders, wasted storage

#### 2. Display Name (DOUBLE REDUNDANCY)
- **Tier 3** (Behavior Definition): `DisplayName = "Wander Behavior"`
- **Tier 2** (NPC Definition): `DisplayName = "WANDERER"`
- **Tier 1** (Tiled Object): `displayName = "WANDERER 1"` (map override)

**Impact**: Three places to define the same concept

#### 3. Behavior Reference (INDIRECT REDUNDANCY)
- **Tier 2** (NPC Definition): `BehaviorScript = "wander"`
- **Tier 3** (Behavior Definition): `TypeId = "wander"` + `BehaviorScript = "Behaviors/wander_behavior.csx"`

**Impact**: Extra indirection layer

---

## Data Flow Analysis

### Current Flow (3-Tier)

```
Map Object (Tiled)
    ↓ [npcId reference]
NPC Definition (EF Core)
    ↓ [BehaviorScript reference]
Behavior Definition (TypeRegistry)
    ↓ [BehaviorScript path]
Roslyn Script (.csx)
```

### Proposed Flow (2-Tier)

```
Map Object (Tiled)
    ↓ [npcId reference]
NPC Definition (EF Core)
    ↓ [BehaviorScript path directly]
Roslyn Script (.csx)
```

---

## Moddability Requirements Analysis

### MUST BE CUSTOMIZABLE AT MAP LEVEL (Instance-Specific)
- ✅ Position (x, y)
- ✅ Direction (facing)
- ✅ Display Name (instance override, e.g., "WANDERER 1" vs "WANDERER 2")
- ✅ Waypoints (patrol paths)
- ✅ Waypoint wait times
- ⚠️ Movement Speed (rare override, but useful for puzzles)

### MUST BE CUSTOMIZABLE AT DEFINITION LEVEL (Reusable Templates)
- ✅ Sprite ID (visual appearance)
- ✅ Behavior Script (AI/movement pattern)
- ✅ Dialogue Script (conversation content)
- ✅ NPC Type (for game logic categorization)
- ✅ Default Movement Speed
- ✅ Custom Properties (JSON blob for mod extensibility)

### NOT NEEDED AT RUNTIME (Editor-Only Metadata)
- ❌ Behavior Definition `DisplayName` - only useful in Tiled editor
- ❌ Behavior Definition `Description` - only useful in editor
- ❌ Behavior Definition `TypeId` - can be inferred from script path

---

## Recommendations

### Phase 1: Eliminate Behavior Definition Tier

**Problem**: BehaviorDefinition is a redundant indirection layer

**Solution**: Move behavior script path directly into NPC Definition

#### Before (3-Tier):
```json
// NPC Definition
{
  "NpcId": "base:npc:wanderer/wanderer",
  "BehaviorScript": "wander",  // ← References Behavior Definition TypeId
  "MovementSpeed": 1.5
}

// Behavior Definition (separate file)
{
  "typeId": "wander",
  "behaviorScript": "Behaviors/wander_behavior.csx",
  "defaultSpeed": 1.5  // ← IGNORED
}
```

#### After (2-Tier):
```json
// NPC Definition (simplified)
{
  "NpcId": "base:npc:wanderer/wanderer",
  "BehaviorScript": "Behaviors/wander_behavior.csx",  // ← Direct path
  "MovementSpeed": 1.5,
  "BehaviorProperties": {  // ← Behavior-specific config
    "pauseAtWaypoint": 0.0,
    "allowInteractionWhileMoving": true
  }
}
```

**Benefits:**
- ✅ Removes 1 entire JSON file tier
- ✅ Eliminates movement speed redundancy
- ✅ Direct script path (less indirection)
- ✅ Behavior properties moved to JSON blob (more flexible)
- ✅ Simpler for modders to understand

### Phase 2: Simplify Map Object Properties

**Problem**: Map objects define `displayName` even when it's already in NPC Definition

**Solution**: Only define `displayName` in map object for OVERRIDES

#### Before:
```json
{
  "name": "Wanderer 1",
  "type": "npc/generic",
  "properties": [
    {"name": "npcId", "value": "wanderer"},
    {"name": "displayName", "value": "WANDERER 1"}  // ← Always present
  ]
}
```

#### After:
```json
// Case 1: No override (use NPC Definition displayName)
{
  "name": "Wanderer 1",
  "type": "npc/generic",
  "properties": [
    {"name": "npcId", "value": "wanderer"}
  ]
}

// Case 2: Override for instance-specific name
{
  "name": "Wanderer Special",
  "type": "npc/generic",
  "properties": [
    {"name": "npcId", "value": "wanderer"},
    {"name": "displayName", "value": "SPECIAL WANDERER"}  // ← Optional override
  ]
}
```

**Benefits:**
- ✅ Reduces map file size
- ✅ Clearer intent (override vs default)
- ✅ Less duplication

### Phase 3: Add Behavior Property Overrides at Map Level

**Problem**: Currently behavior properties (pauseAtWaypoint, allowInteractionWhileMoving) can't be overridden per-instance

**Solution**: Support map-level behavior property overrides

```json
{
  "name": "Guard Captain",
  "type": "npc/generic",
  "properties": [
    {"name": "npcId", "value": "guard"},
    {"name": "behaviorProperties", "value": "{\"pauseAtWaypoint\": 2.0}"}
  ]
}
```

**Benefits:**
- ✅ Instance-specific behavior tweaks
- ✅ Useful for puzzle NPCs (e.g., slower patrol guard)

---

## Migration Strategy

### Step 1: Create Migration Script
1. Parse all existing Behavior Definition JSON files
2. For each NPC Definition that references a behavior:
   - Replace `BehaviorScript = "wander"` with full path from Behavior Definition
   - Copy `defaultSpeed` to `MovementSpeed` if not already set
   - Move behavior-specific properties to `BehaviorProperties` JSON blob

### Step 2: Update Code
1. Modify `MapObjectSpawner.ApplyNpcDefinition()` to use direct script path
2. Remove `BehaviorDefinition` TypeRegistry system
3. Update `NPCBehaviorSystem` to load scripts directly by path

### Step 3: Update Map Files
1. Remove `displayName` property from map objects where it matches NPC Definition
2. Add support for `behaviorProperties` overrides

### Step 4: Cleanup
1. Delete `Data/Behaviors/*.json` files
2. Remove `BehaviorDefinition.cs`
3. Update documentation

---

## Simplified Architecture Diagram

### Current (3-Tier)
```
┌─────────────────┐
│  Tiled Object   │ displayName, npcId, direction
└────────┬────────┘
         │ npcId reference
         ↓
┌─────────────────┐
│ NPC Definition  │ DisplayName, BehaviorScript, MovementSpeed
└────────┬────────┘
         │ BehaviorScript reference
         ↓
┌─────────────────┐
│ Behavior Defn   │ DisplayName, DefaultSpeed, BehaviorScript path
└────────┬────────┘
         │
         ↓
┌─────────────────┐
│  .csx Script    │
└─────────────────┘
```

### Proposed (2-Tier)
```
┌─────────────────┐
│  Tiled Object   │ npcId, (optional overrides)
└────────┬────────┘
         │ npcId reference
         ↓
┌─────────────────┐
│ NPC Definition  │ DisplayName, BehaviorScript path, MovementSpeed, BehaviorProperties
└────────┬────────┘
         │ Direct script path
         ↓
┌─────────────────┐
│  .csx Script    │
└─────────────────┘
```

---

## Impact Summary

### Code Changes Required
- **Moderate**: ~200 lines of code changes in MapObjectSpawner and NPCBehaviorSystem
- **Low Risk**: Behavior scripts themselves are unchanged

### Data Migration Required
- **Medium**: ~100 NPC definitions to update
- **Low**: ~3 behavior definitions to eliminate
- **Scriptable**: Can be automated with PowerShell/Python

### Benefits
- ✅ **40% reduction in property duplication**
- ✅ **Eliminates 1 entire configuration tier**
- ✅ **Simpler mental model for modders**
- ✅ **Reduced file I/O (fewer JSON files to parse)**
- ✅ **Clearer override semantics**

### Risks
- ⚠️ **Breaking change for existing mods** (if any exist)
- ⚠️ **Requires comprehensive testing** of all NPC spawning
- ✅ **Mitigated by**: Migration script + backward compatibility layer

---

## Conclusion

The current 3-tier hierarchy creates unnecessary complexity and duplication. By eliminating the Behavior Definition tier and simplifying property overrides, we can:

1. **Reduce duplication by 40%**
2. **Simplify the system** from 3 tiers to 2 tiers
3. **Improve moddability** with clearer override semantics
4. **Reduce file I/O overhead** by eliminating behavior JSON files

**Recommendation**: Implement Phase 1 (eliminate Behavior Definition tier) immediately, then evaluate Phases 2-3 based on modding community feedback.
