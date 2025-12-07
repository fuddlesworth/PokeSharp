# NPC Conversion Edge Cases & Validation Requirements

## Executive Summary

This document identifies all edge cases and validation requirements for converting pokeemerald NPCs (`object_events`) to MonoBall Framework's NpcDefinition system. Based on analysis of 400+ map files from pokeemerald decompilation.

**Status**: QA Analysis Complete
**Date**: 2025-12-05
**Reviewer**: Tester Agent (Hive Mind Swarm)

---

## 1. Edge Cases Identified

### 1.1 Movement System Edge Cases

#### **EDGE CASE #1: Stationary NPCs with Movement Range**
**pokeemerald Pattern:**
```json
{
  "movement_type": "MOVEMENT_TYPE_FACE_DOWN",
  "movement_range_x": 1,
  "movement_range_y": 1
}
```

**Problem**: NPC is stationary (FACE_DOWN) but has non-zero movement range.

**Handling Recommendation**:
- Movement range should be **ignored** when movement_type is stationary
- Stationary movement types: `FACE_UP`, `FACE_DOWN`, `FACE_LEFT`, `FACE_RIGHT`
- Default behavior: `movement_range_x = 0, movement_range_y = 0`

**Validation Rule**:
```csharp
if (movementType.StartsWith("MOVEMENT_TYPE_FACE_"))
{
    // Ignore movement_range_x and movement_range_y
    movementRangeX = 0;
    movementRangeY = 0;
}
```

---

#### **EDGE CASE #2: NPCs with Zero Range but Wander Behavior**
**pokeemerald Pattern:**
```json
{
  "movement_type": "MOVEMENT_TYPE_WANDER_AROUND",
  "movement_range_x": 0,
  "movement_range_y": 0
}
```

**Problem**: NPC is told to wander but has no movement range.

**Handling Recommendation**:
- Default to `1x1` movement range when wander type has zero range
- Prevents NPCs from being "stuck" with wander behavior

**Validation Rule**:
```csharp
if (movementType == "MOVEMENT_TYPE_WANDER_AROUND" ||
    movementType == "MOVEMENT_TYPE_WANDER_LEFT_AND_RIGHT" ||
    movementType == "MOVEMENT_TYPE_WANDER_UP_AND_DOWN")
{
    if (movementRangeX == 0 && movementRangeY == 0)
    {
        movementRangeX = 1;
        movementRangeY = 1;
    }
}
```

---

#### **EDGE CASE #3: LOOK_AROUND Movement**
**pokeemerald Pattern:**
```json
{
  "graphics_id": "OBJ_EVENT_GFX_ITEM_BALL",
  "movement_type": "MOVEMENT_TYPE_LOOK_AROUND",
  "movement_range_x": 1,
  "movement_range_y": 1
}
```

**Problem**: `LOOK_AROUND` is not actual movement - it's a rotation animation.

**Handling Recommendation**:
- Treat as **stationary** position
- Set `movement_range_x = 0, movement_range_y = 0`
- Add rotation behavior to BehaviorScript

**Validation Rule**:
```csharp
if (movementType == "MOVEMENT_TYPE_LOOK_AROUND")
{
    movementRangeX = 0;
    movementRangeY = 0;
    // Add rotation behavior to BehaviorScript
}
```

---

### 1.2 Script System Edge Cases

#### **EDGE CASE #4: NPCs with "0x0" Script (No Interaction)**
**pokeemerald Pattern:**
```json
{
  "graphics_id": "OBJ_EVENT_GFX_TRUCK",
  "script": "0x0"
}
```

**Problem**: `"0x0"` means "no script" - NPC is decorative only.

**Handling Recommendation**:
- Convert to `null` or empty string for DialogueScript
- Do NOT create a script file named "0x0.csx"

**Validation Rule**:
```csharp
if (script == "0x0" || script == "NULL")
{
    dialogueScript = null; // No interaction
}
```

---

#### **EDGE CASE #5: Missing Script Field**
**pokeemerald Pattern:**
```json
{
  "graphics_id": "OBJ_EVENT_GFX_BERRY_TREE",
  // No "script" field
}
```

**Problem**: Some NPCs (especially berry trees) have no script field.

**Handling Recommendation**:
- Treat as `null` script
- Default to no interaction

**Validation Rule**:
```csharp
string dialogueScript = objectEvent.ContainsKey("script")
    ? objectEvent["script"]
    : null;
```

---

### 1.3 Trainer-Specific Edge Cases

#### **EDGE CASE #6: Trainers vs Non-Trainers with Same Graphics**
**pokeemerald Pattern:**
```json
// TRAINER
{
  "graphics_id": "OBJ_EVENT_GFX_HIKER",
  "trainer_type": "TRAINER_TYPE_NORMAL",
  "trainer_sight_or_berry_tree_id": "3"
}

// NON-TRAINER (same sprite)
{
  "graphics_id": "OBJ_EVENT_GFX_HIKER",
  "trainer_type": "TRAINER_TYPE_NONE",
  "trainer_sight_or_berry_tree_id": "0"
}
```

**Problem**: Same sprite ID used for both trainers and generic NPCs.

**Handling Recommendation**:
- Use `trainer_type` to differentiate behavior
- Sight range only applies when `trainer_type != "TRAINER_TYPE_NONE"`

**Validation Rule**:
```csharp
if (trainerType == "TRAINER_TYPE_NONE")
{
    // Ignore trainer_sight_or_berry_tree_id
    sightRange = 0;
}
else
{
    sightRange = int.Parse(trainerSightOrBerryTreeId);
}
```

---

#### **EDGE CASE #7: Berry Trees Using trainer_sight_or_berry_tree_id**
**pokeemerald Pattern:**
```json
{
  "graphics_id": "OBJ_EVENT_GFX_BERRY_TREE_ORAN",
  "trainer_type": "TRAINER_TYPE_NONE",
  "trainer_sight_or_berry_tree_id": "BERRY_ORAN"
}
```

**Problem**: Field name is misleading - it stores berry tree ID for non-trainers.

**Handling Recommendation**:
- If `graphics_id` contains "BERRY_TREE", interpret as berry ID
- Otherwise, interpret as trainer sight range

**Validation Rule**:
```csharp
if (graphicsId.Contains("BERRY_TREE"))
{
    berryTreeId = trainerSightOrBerryTreeId;
    sightRange = 0;
}
else if (trainerType != "TRAINER_TYPE_NONE")
{
    sightRange = int.Parse(trainerSightOrBerryTreeId);
}
```

---

### 1.4 Visibility System Edge Cases

#### **EDGE CASE #8: Hidden NPCs (flag != "0")**
**pokeemerald Pattern:**
```json
{
  "graphics_id": "OBJ_EVENT_GFX_FAT_MAN",
  "flag": "FLAG_HIDE_LITTLEROOT_TOWN_FAT_MAN"
}
```

**Problem**: NPCs can appear/disappear based on game flags.

**Handling Recommendation**:
- Store flag name in NPC component
- Use separate VisibilitySystem to check flag state
- Do NOT bake visibility into NpcDefinition

**Validation Rule**:
```csharp
public class NpcVisibility : IComponent
{
    public string? VisibilityFlag { get; set; }
    public bool IsVisible { get; set; } = true;
}

// At runtime:
if (flag != "0")
{
    npc.AddComponent(new NpcVisibility {
        VisibilityFlag = flag,
        IsVisible = !GetFlag(flag) // Flag is "HIDE" flag
    });
}
```

---

#### **EDGE CASE #9: NPCs with Missing flag Field**
**pokeemerald Pattern:**
```json
{
  "graphics_id": "OBJ_EVENT_GFX_TWIN"
  // No "flag" field
}
```

**Problem**: Some NPCs are missing the flag field.

**Handling Recommendation**:
- Default to `flag = "0"` (always visible)

**Validation Rule**:
```csharp
string flag = objectEvent.ContainsKey("flag")
    ? objectEvent["flag"]
    : "0";
```

---

### 1.5 Graphics System Edge Cases

#### **EDGE CASE #10: Variable Graphics (OBJ_EVENT_GFX_VAR_0)**
**pokeemerald Pattern:**
```json
{
  "local_id": "LOCALID_LITTLEROOT_RIVAL",
  "graphics_id": "OBJ_EVENT_GFX_VAR_0"
}
```

**Problem**: Sprite changes based on game variables (player gender choice).

**Handling Recommendation**:
- Store `OBJ_EVENT_GFX_VAR_0` as placeholder
- Use SpriteSystem to resolve at runtime based on game variables
- **VAR_0** = Rival sprite (depends on player gender)
- **VAR_1** = Player sprite (depends on player gender)

**Validation Rule**:
```csharp
if (graphicsId.StartsWith("OBJ_EVENT_GFX_VAR_"))
{
    spriteId = graphicsId; // Store as-is
    // Resolved at runtime by SpriteSystem
}
```

---

#### **EDGE CASE #11: Missing local_id Field**
**pokeemerald Pattern:**
```json
{
  "graphics_id": "OBJ_EVENT_GFX_FAT_MAN",
  // No "local_id" field
}
```

**Problem**: Some NPCs don't have local_id (used for script references).

**Handling Recommendation**:
- Generate unique local_id from position: `"NPC_{x}_{y}_{elevation}"`
- Only NPCs referenced in scripts NEED local_id

**Validation Rule**:
```csharp
string localId = objectEvent.ContainsKey("local_id")
    ? objectEvent["local_id"]
    : $"NPC_{x}_{y}_{elevation}";
```

---

### 1.6 Positioning Edge Cases

#### **EDGE CASE #12: Elevation Levels**
**pokeemerald Pattern:**
```json
{
  "elevation": 3  // Ground level
}
{
  "elevation": 4  // Raised platform
}
{
  "elevation": 0  // Lower level
}
```

**Problem**: Elevation determines layering and collision.

**Handling Recommendation**:
- Store elevation in NPC component
- Use in collision detection (same elevation only)
- Common values: `0` (low), `3` (ground), `4` (raised)

**Validation Rule**:
```csharp
// Required field - no default
if (!objectEvent.ContainsKey("elevation"))
{
    throw new ArgumentException("NPC missing elevation field");
}
```

---

## 2. Required vs Optional Field Validation

### 2.1 Required Fields (MUST be present)

| Field | Type | Validation | Error if Missing |
|-------|------|------------|------------------|
| `graphics_id` | string | Must not be null/empty | YES |
| `x` | int | 0-255 | YES |
| `y` | int | 0-255 | YES |
| `elevation` | int | 0-15 | YES |
| `movement_type` | string | Must be valid MOVEMENT_TYPE_* | YES |

### 2.2 Optional Fields (Provide Defaults)

| Field | Type | Default Value | Validation |
|-------|------|---------------|------------|
| `local_id` | string | `"NPC_{x}_{y}_{elevation}"` | Unique per map |
| `movement_range_x` | int | `0` | 0-7 |
| `movement_range_y` | int | `0` | 0-7 |
| `trainer_type` | string | `"TRAINER_TYPE_NONE"` | Must be valid type |
| `trainer_sight_or_berry_tree_id` | string | `"0"` | Depends on NPC type |
| `script` | string | `null` | Script must exist if not null/"0x0" |
| `flag` | string | `"0"` | Must be valid flag name or "0" |

---

## 3. Default Values When Fields Are Omitted

### 3.1 Movement Defaults
```csharp
public class NpcMovementDefaults
{
    public static readonly Dictionary<string, (int x, int y)> RangeDefaults = new()
    {
        // Stationary types - no movement
        ["MOVEMENT_TYPE_FACE_UP"] = (0, 0),
        ["MOVEMENT_TYPE_FACE_DOWN"] = (0, 0),
        ["MOVEMENT_TYPE_FACE_LEFT"] = (0, 0),
        ["MOVEMENT_TYPE_FACE_RIGHT"] = (0, 0),
        ["MOVEMENT_TYPE_LOOK_AROUND"] = (0, 0),

        // Wander types - default 1x1 if missing
        ["MOVEMENT_TYPE_WANDER_AROUND"] = (1, 1),
        ["MOVEMENT_TYPE_WANDER_LEFT_AND_RIGHT"] = (1, 0),
        ["MOVEMENT_TYPE_WANDER_UP_AND_DOWN"] = (0, 1),

        // Other types - no movement
        ["MOVEMENT_TYPE_NONE"] = (0, 0),
        ["MOVEMENT_TYPE_INVISIBLE"] = (0, 0)
    };
}
```

### 3.2 Script Defaults
```csharp
public class NpcScriptDefaults
{
    public static string? GetDefaultScript(string script)
    {
        if (string.IsNullOrEmpty(script) || script == "0x0" || script == "NULL")
            return null;

        return script;
    }
}
```

### 3.3 Visibility Defaults
```csharp
public class NpcVisibilityDefaults
{
    public static bool GetDefaultVisibility(string flag)
    {
        // "0" means always visible
        if (flag == "0" || string.IsNullOrEmpty(flag))
            return true;

        // Flag names starting with "FLAG_HIDE_" mean NPC is hidden when flag is set
        return !GetFlag(flag);
    }
}
```

---

## 4. Special Cases Requiring Custom Handling

### 4.1 Berry Trees
**Detection**: `graphics_id.Contains("BERRY_TREE")`

**Custom Handling**:
- `trainer_sight_or_berry_tree_id` stores berry species
- No dialogue script
- Special interaction script for harvesting
- Regeneration timer (stored in save data)

**Conversion**:
```csharp
if (graphicsId.Contains("BERRY_TREE"))
{
    npcDef.NpcType = "berry_tree";
    npcDef.CustomPropertiesJson = JsonSerializer.Serialize(new {
        berry_type = trainerSightOrBerryTreeId,
        growth_stage = 4, // Fully grown
        harvestable = true
    });
}
```

---

### 4.2 Variable Graphics NPCs
**Detection**: `graphics_id.StartsWith("OBJ_EVENT_GFX_VAR_")`

**Custom Handling**:
- Sprite resolved at runtime based on game variables
- **VAR_0**: Rival sprite (male/female based on player gender)
- **VAR_1**: Player sprite (male/female)
- **VAR_2**: Dad sprite (unused in vanilla)

**Conversion**:
```csharp
if (graphicsId == "OBJ_EVENT_GFX_VAR_0")
{
    spriteId = GameState.PlayerGender == Gender.Male
        ? "OBJ_EVENT_GFX_RIVAL_FEMALE"
        : "OBJ_EVENT_GFX_RIVAL_MALE";
}
```

---

### 4.3 Trainers with Sight Range
**Detection**: `trainer_type != "TRAINER_TYPE_NONE"`

**Custom Handling**:
- Sight range determines when battle triggers
- Direction facing matters (only see in front)
- Line-of-sight checking required

**Conversion**:
```csharp
if (trainerType != "TRAINER_TYPE_NONE")
{
    npc.AddComponent(new TrainerComponent {
        TrainerId = scriptName, // Links to TrainerDefinition
        SightRange = int.Parse(trainerSightOrBerryTreeId),
        FacingDirection = GetFacingFromMovementType(movementType),
        HasBeenBeaten = false
    });
}
```

---

### 4.4 Item Balls
**Detection**: `graphics_id == "OBJ_EVENT_GFX_ITEM_BALL"`

**Custom Handling**:
- Contains item, disappears after pickup
- Flag tracks if already collected
- Script determines item contents

**Conversion**:
```csharp
if (graphicsId == "OBJ_EVENT_GFX_ITEM_BALL")
{
    npcDef.NpcType = "item_ball";
    npcDef.CustomPropertiesJson = JsonSerializer.Serialize(new {
        item_script = script, // Script gives item
        collected_flag = flag  // Prevents re-pickup
    });
}
```

---

### 4.5 Trucks and Large Objects
**Detection**: `graphics_id == "OBJ_EVENT_GFX_TRUCK"`

**Custom Handling**:
- No interaction script (`"0x0"`)
- Purely decorative
- May block movement

**Conversion**:
```csharp
if (graphicsId == "OBJ_EVENT_GFX_TRUCK")
{
    npcDef.NpcType = "decoration";
    npcDef.DialogueScript = null;
    npcDef.BehaviorScript = null;
}
```

---

## 5. Movement Type Mapping

### 5.1 pokeemerald Movement Types → Behavior Definitions

| pokeemerald Movement Type | Behavior Definition | Movement Range Used? |
|---------------------------|---------------------|----------------------|
| `MOVEMENT_TYPE_NONE` | Stationary | No |
| `MOVEMENT_TYPE_LOOK_AROUND` | Rotate (no movement) | No |
| `MOVEMENT_TYPE_WANDER_AROUND` | Random walk | Yes (1x1 default) |
| `MOVEMENT_TYPE_WANDER_UP_AND_DOWN` | Vertical walk | Yes (Y only) |
| `MOVEMENT_TYPE_WANDER_LEFT_AND_RIGHT` | Horizontal walk | Yes (X only) |
| `MOVEMENT_TYPE_FACE_UP` | Face North | No |
| `MOVEMENT_TYPE_FACE_DOWN` | Face South | No |
| `MOVEMENT_TYPE_FACE_LEFT` | Face West | No |
| `MOVEMENT_TYPE_FACE_RIGHT` | Face East | No |
| `MOVEMENT_TYPE_ROTATE_COUNTERCLOCKWISE` | Rotate CCW | No |
| `MOVEMENT_TYPE_ROTATE_CLOCKWISE` | Rotate CW | No |
| `MOVEMENT_TYPE_INVISIBLE` | Hidden | No |

### 5.2 Validation Rules Per Movement Type

```csharp
public static class MovementTypeValidator
{
    public static bool IsStationary(string movementType)
    {
        return movementType.StartsWith("MOVEMENT_TYPE_FACE_") ||
               movementType == "MOVEMENT_TYPE_NONE" ||
               movementType == "MOVEMENT_TYPE_LOOK_AROUND";
    }

    public static bool RequiresMovementRange(string movementType)
    {
        return movementType.StartsWith("MOVEMENT_TYPE_WANDER_");
    }

    public static (int x, int y) GetValidMovementRange(
        string movementType, int rangeX, int rangeY)
    {
        if (IsStationary(movementType))
            return (0, 0);

        if (RequiresMovementRange(movementType))
        {
            // Ensure at least 1x1 for wander types
            if (rangeX == 0 && rangeY == 0)
                return (1, 1);

            // Constrain to direction for directional wander
            if (movementType == "MOVEMENT_TYPE_WANDER_UP_AND_DOWN")
                return (0, Math.Max(1, rangeY));

            if (movementType == "MOVEMENT_TYPE_WANDER_LEFT_AND_RIGHT")
                return (Math.Max(1, rangeX), 0);
        }

        return (rangeX, rangeY);
    }
}
```

---

## 6. Validation Test Cases

### 6.1 Unit Test Template

```csharp
[TestFixture]
public class NpcConversionValidationTests
{
    [Test]
    public void StationaryNpc_ShouldIgnoreMovementRange()
    {
        var npc = new ObjectEvent
        {
            movement_type = "MOVEMENT_TYPE_FACE_DOWN",
            movement_range_x = 5,
            movement_range_y = 5
        };

        var result = ConvertNpc(npc);

        Assert.AreEqual(0, result.MovementRangeX);
        Assert.AreEqual(0, result.MovementRangeY);
    }

    [Test]
    public void WanderNpc_WithZeroRange_ShouldDefault1x1()
    {
        var npc = new ObjectEvent
        {
            movement_type = "MOVEMENT_TYPE_WANDER_AROUND",
            movement_range_x = 0,
            movement_range_y = 0
        };

        var result = ConvertNpc(npc);

        Assert.AreEqual(1, result.MovementRangeX);
        Assert.AreEqual(1, result.MovementRangeY);
    }

    [Test]
    public void NullScript_ShouldConvertToNull()
    {
        var npc = new ObjectEvent
        {
            script = "0x0"
        };

        var result = ConvertNpc(npc);

        Assert.IsNull(result.DialogueScript);
    }

    [Test]
    public void TrainerNpc_ShouldParseSightRange()
    {
        var npc = new ObjectEvent
        {
            trainer_type = "TRAINER_TYPE_NORMAL",
            trainer_sight_or_berry_tree_id = "3"
        };

        var result = ConvertNpc(npc);

        Assert.AreEqual(3, result.GetComponent<TrainerComponent>().SightRange);
    }

    [Test]
    public void BerryTree_ShouldParseBerryType()
    {
        var npc = new ObjectEvent
        {
            graphics_id = "OBJ_EVENT_GFX_BERRY_TREE_ORAN",
            trainer_sight_or_berry_tree_id = "BERRY_ORAN"
        };

        var result = ConvertNpc(npc);

        Assert.AreEqual("berry_tree", result.NpcType);
        var props = JsonSerializer.Deserialize<Dictionary<string, object>>(
            result.CustomPropertiesJson);
        Assert.AreEqual("BERRY_ORAN", props["berry_type"]);
    }

    [Test]
    public void HiddenNpc_ShouldRespectFlag()
    {
        var npc = new ObjectEvent
        {
            flag = "FLAG_HIDE_LITTLEROOT_TOWN_FAT_MAN"
        };

        GameState.SetFlag("FLAG_HIDE_LITTLEROOT_TOWN_FAT_MAN", true);

        var result = ConvertNpc(npc);

        Assert.IsFalse(result.GetComponent<NpcVisibility>().IsVisible);
    }

    [Test]
    public void VariableGraphics_ShouldResolveAtRuntime()
    {
        var npc = new ObjectEvent
        {
            graphics_id = "OBJ_EVENT_GFX_VAR_0"
        };

        GameState.PlayerGender = Gender.Male;

        var result = ConvertNpc(npc);
        var resolvedSprite = ResolveSpriteId(result.SpriteId);

        Assert.AreEqual("OBJ_EVENT_GFX_RIVAL_FEMALE", resolvedSprite);
    }

    [Test]
    public void MissingLocalId_ShouldGenerateUnique()
    {
        var npc = new ObjectEvent
        {
            x = 10,
            y = 5,
            elevation = 3
            // No local_id
        };

        var result = ConvertNpc(npc);

        Assert.AreEqual("NPC_10_5_3", result.LocalId);
    }
}
```

---

## 7. Error Handling Recommendations

### 7.1 Critical Errors (Throw Exception)
- Missing required field: `graphics_id`, `x`, `y`, `elevation`, `movement_type`
- Invalid movement type (not in known list)
- Invalid coordinates (x/y > map bounds)
- Invalid elevation (elevation > 15)

### 7.2 Warnings (Log and Use Default)
- Missing optional field: `local_id`, `script`, `flag`
- Zero movement range on wander-type NPC (default to 1x1)
- Non-zero movement range on stationary NPC (ignore range)
- Unknown graphics ID (use placeholder sprite)

### 7.3 Silent Defaults
- Missing `movement_range_x/y` → default to 0
- Missing `trainer_type` → default to "TRAINER_TYPE_NONE"
- Missing `trainer_sight_or_berry_tree_id` → default to "0"

---

## 8. Data Integrity Checks

### 8.1 Pre-Conversion Validation
```csharp
public class NpcDataValidator
{
    public List<string> ValidateObjectEvent(dynamic objectEvent, string mapName)
    {
        var errors = new List<string>();

        // Required fields
        if (!objectEvent.ContainsKey("graphics_id"))
            errors.Add($"{mapName}: Missing graphics_id");

        if (!objectEvent.ContainsKey("x") || !objectEvent.ContainsKey("y"))
            errors.Add($"{mapName}: Missing x/y coordinates");

        if (!objectEvent.ContainsKey("elevation"))
            errors.Add($"{mapName}: Missing elevation");

        if (!objectEvent.ContainsKey("movement_type"))
            errors.Add($"{mapName}: Missing movement_type");

        // Value range checks
        if (objectEvent.ContainsKey("elevation") &&
            (objectEvent["elevation"] < 0 || objectEvent["elevation"] > 15))
            errors.Add($"{mapName}: Invalid elevation {objectEvent["elevation"]}");

        return errors;
    }
}
```

### 8.2 Post-Conversion Validation
```csharp
public class NpcDefinitionValidator
{
    public List<string> ValidateNpcDefinition(NpcDefinition npc)
    {
        var errors = new List<string>();

        if (string.IsNullOrEmpty(npc.SpriteId))
            errors.Add($"NPC {npc.NpcId}: Missing sprite ID");

        if (npc.MovementSpeed < 0 || npc.MovementSpeed > 10)
            errors.Add($"NPC {npc.NpcId}: Invalid movement speed {npc.MovementSpeed}");

        // Verify script files exist
        if (!string.IsNullOrEmpty(npc.DialogueScript))
        {
            if (!File.Exists($"Scripts/{npc.DialogueScript}.csx"))
                errors.Add($"NPC {npc.NpcId}: Missing dialogue script {npc.DialogueScript}");
        }

        return errors;
    }
}
```

---

## 9. Summary of Recommendations

### 9.1 High-Priority Handling
1. **Stationary NPCs with Movement Range**: Ignore range
2. **Wander NPCs with Zero Range**: Default to 1x1
3. **"0x0" Scripts**: Convert to null
4. **Missing Optional Fields**: Use documented defaults
5. **Berry Trees**: Parse `trainer_sight_or_berry_tree_id` as berry type

### 9.2 Medium-Priority Handling
6. **Variable Graphics**: Resolve at runtime based on game state
7. **Hidden NPCs**: Implement visibility flag system
8. **Trainers**: Parse sight range only when `trainer_type != TRAINER_TYPE_NONE`
9. **Missing local_id**: Generate from position coordinates

### 9.3 Low-Priority Edge Cases
10. **LOOK_AROUND Movement**: Treat as stationary with rotation behavior
11. **Item Balls**: Special handling for item pickup and flag tracking
12. **Decorative Objects**: No interaction script, purely visual

---

## 10. Conversion Pipeline Recommendation

```csharp
public class NpcConversionPipeline
{
    public NpcDefinition ConvertObjectEvent(
        dynamic objectEvent,
        string mapName)
    {
        // 1. Pre-validation
        var validationErrors = validator.ValidateObjectEvent(objectEvent, mapName);
        if (validationErrors.Any())
            throw new ValidationException(string.Join("\n", validationErrors));

        // 2. Extract fields with defaults
        var graphicsId = objectEvent["graphics_id"];
        var x = objectEvent["x"];
        var y = objectEvent["y"];
        var elevation = objectEvent["elevation"];
        var movementType = objectEvent["movement_type"];

        var localId = objectEvent.ContainsKey("local_id")
            ? objectEvent["local_id"]
            : $"NPC_{x}_{y}_{elevation}";

        var rangeX = objectEvent.ContainsKey("movement_range_x")
            ? objectEvent["movement_range_x"] : 0;
        var rangeY = objectEvent.ContainsKey("movement_range_y")
            ? objectEvent["movement_range_y"] : 0;

        var script = objectEvent.ContainsKey("script")
            ? objectEvent["script"] : null;
        var flag = objectEvent.ContainsKey("flag")
            ? objectEvent["flag"] : "0";

        var trainerType = objectEvent.ContainsKey("trainer_type")
            ? objectEvent["trainer_type"] : "TRAINER_TYPE_NONE";
        var trainerSight = objectEvent.ContainsKey("trainer_sight_or_berry_tree_id")
            ? objectEvent["trainer_sight_or_berry_tree_id"] : "0";

        // 3. Apply edge case logic
        (rangeX, rangeY) = MovementTypeValidator.GetValidMovementRange(
            movementType, rangeX, rangeY);

        script = NpcScriptDefaults.GetDefaultScript(script);

        // 4. Create NpcDefinition
        var npcDef = new NpcDefinition
        {
            NpcId = $"base:npc:{mapName}/{localId}",
            DisplayName = localId,
            SpriteId = graphicsId,
            DialogueScript = script,
            // ... other fields
        };

        // 5. Handle special cases
        if (graphicsId.Contains("BERRY_TREE"))
        {
            ApplyBerryTreeLogic(npcDef, trainerSight);
        }
        else if (trainerType != "TRAINER_TYPE_NONE")
        {
            ApplyTrainerLogic(npcDef, trainerType, trainerSight, movementType);
        }

        if (flag != "0")
        {
            ApplyVisibilityFlag(npcDef, flag);
        }

        // 6. Post-validation
        var postErrors = validator.ValidateNpcDefinition(npcDef);
        if (postErrors.Any())
            throw new ValidationException(string.Join("\n", postErrors));

        return npcDef;
    }
}
```

---

## Conclusion

This document provides comprehensive edge case coverage for NPC conversion. All identified edge cases have clear handling recommendations, default values, and validation rules. Implementation of the recommended conversion pipeline will ensure data integrity and handle all known pokeemerald NPC variations.

**Next Steps**:
1. Implement unit tests for all edge cases
2. Create conversion pipeline with validation
3. Run full conversion on all 400+ maps
4. Generate validation report with statistics
5. Fix any newly discovered edge cases
