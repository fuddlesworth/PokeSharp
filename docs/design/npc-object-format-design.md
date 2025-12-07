# NPC Object Format Design
## Simplified, Moddable Tiled Object System

**Version:** 1.0.0
**Status:** Design Specification
**Author:** Hive Mind Swarm - Coder Agent
**Date:** 2025-12-05

---

## Executive Summary

This document defines a clean-slate, moddable object format for NPCs in Tiled maps that bridges pokeemerald's `object_event` structure to the MonoBall Framework game engine. The design follows the principle of **minimal instance data** in Tiled maps, with **maximum reusability** through NPC and Behavior definitions.

### Design Principles

1. **Tiled objects store ONLY per-instance data** (position, direction, instance overrides)
2. **All shared data lives in NPC definitions** (appearance, dialogue, default behavior)
3. **Behaviors are separate, reusable patterns** (movement AI, scripts)
4. **Modders can override defaults at any level** (definition, instance, or create new)
5. **No backward compatibility constraints** (clean slate design)

---

## 1. Tiled Object Format (Minimal Instance Data)

### 1.1 Basic NPC Object

Tiled objects use the **type** field to indicate they reference an NPC definition:

```json
{
  "id": 1,
  "name": "Prof Birch",
  "type": "npc",
  "x": 128,
  "y": 192,
  "width": 16,
  "height": 16,
  "visible": true,
  "properties": [
    {
      "name": "npcId",
      "type": "string",
      "value": "base:npc:hoenn/prof_birch"
    },
    {
      "name": "direction",
      "type": "string",
      "value": "south"
    },
    {
      "name": "elevation",
      "type": "int",
      "value": 0
    },
    {
      "name": "localId",
      "type": "string",
      "value": "LITTLEROOT_BIRCH"
    }
  ]
}
```

### 1.2 Object with Instance Overrides

Override specific properties for THIS instance only:

```json
{
  "id": 2,
  "name": "Wandering Youngster",
  "type": "npc",
  "x": 256,
  "y": 320,
  "width": 16,
  "height": 16,
  "visible": true,
  "properties": [
    {
      "name": "npcId",
      "type": "string",
      "value": "base:npc:generic/youngster"
    },
    {
      "name": "direction",
      "type": "string",
      "value": "east"
    },
    {
      "name": "elevation",
      "type": "int",
      "value": 0
    },
    {
      "name": "behaviorOverride",
      "type": "string",
      "value": "base:behavior:patrol_horizontal"
    },
    {
      "name": "movementRange",
      "type": "class",
      "value": {
        "x": 5,
        "y": 0
      }
    },
    {
      "name": "waypoints",
      "type": "string",
      "value": "10,15;20,15"
    },
    {
      "name": "waypointWaitTime",
      "type": "float",
      "value": 2.0
    }
  ]
}
```

### 1.3 Trainer Object

Trainers use a different type and reference trainer definitions:

```json
{
  "id": 3,
  "name": "Bug Catcher Rick",
  "type": "trainer",
  "x": 384,
  "y": 256,
  "width": 16,
  "height": 16,
  "visible": true,
  "properties": [
    {
      "name": "trainerId",
      "type": "string",
      "value": "base:trainer:route102/rick"
    },
    {
      "name": "direction",
      "type": "string",
      "value": "north"
    },
    {
      "name": "elevation",
      "type": "int",
      "value": 0
    },
    {
      "name": "sightRange",
      "type": "int",
      "value": 4
    },
    {
      "name": "trainerType",
      "type": "string",
      "value": "standard"
    },
    {
      "name": "localId",
      "type": "string",
      "value": "ROUTE102_RICK"
    }
  ]
}
```

### 1.4 Conditional Visibility

Objects that appear/disappear based on game flags:

```json
{
  "id": 4,
  "name": "Rival (Post-Battle)",
  "type": "npc",
  "x": 160,
  "y": 224,
  "width": 16,
  "height": 16,
  "visible": true,
  "properties": [
    {
      "name": "npcId",
      "type": "string",
      "value": "base:npc:rival/may"
    },
    {
      "name": "direction",
      "type": "string",
      "value": "west"
    },
    {
      "name": "visibilityFlag",
      "type": "string",
      "value": "FLAG_RIVAL_BATTLE_ROUTE103_COMPLETE"
    },
    {
      "name": "localId",
      "type": "string",
      "value": "ROUTE103_RIVAL"
    }
  ]
}
```

---

## 2. Tiled Object Properties Reference

### 2.1 Required Properties

| Property | Type | Description |
|----------|------|-------------|
| `npcId` or `trainerId` | string | ID referencing NPC/Trainer definition (unified format: `namespace:type:path/name`) |
| `direction` | string | Initial facing direction: `north`, `south`, `east`, `west` |

### 2.2 Optional Instance Properties

| Property | Type | Description | Example |
|----------|------|-------------|---------|
| `elevation` | int | Vertical layer for bridge/overlap logic | `0`, `3`, `6` |
| `localId` | string | Script reference ID for this instance | `LITTLEROOT_BIRCH` |
| `visibilityFlag` | string | Game flag controlling visibility | `FLAG_DEFEATED_GYM_1` |
| `behaviorOverride` | string | Override default behavior for this instance | `base:behavior:guard_area` |
| `movementRange` | class | Movement bounds (x, y in tiles) | `{"x": 3, "y": 2}` |
| `waypoints` | string | Patrol waypoints `x1,y1;x2,y2;...` | `10,15;20,15;10,15` |
| `waypointWaitTime` | float | Seconds to wait at each waypoint | `2.0` |
| `sightRange` | int | Trainer sight distance (trainers only) | `4` |
| `trainerType` | string | Trainer battle type (trainers only) | `standard`, `boss`, `double` |
| `customProperties` | class | Free-form JSON for mod data | `{"quest_giver": true}` |

### 2.3 Coordinate System

- **x, y**: Pixel coordinates (Tiled default)
  - Converted to tile coordinates: `tileX = floor(x / tileWidth)`, `tileY = floor(y / tileHeight)`
  - Tiled Y coordinate is from **top of object**, uses top-left corner for positioning
- **elevation**: Integer value (0, 3, 6, 9, etc.)
  - Used for layering/collision logic (bridges, elevated areas)
  - Not used in Position struct, but stored in Elevation component

---

## 3. NPC Definition Format (Shared/Reusable Data)

NPC definitions are stored in the database (EF Core entities) and loaded at runtime.

### 3.1 NpcDefinition Schema

```csharp
public class NpcDefinition
{
    /// <summary>
    /// Unique identifier in unified format (e.g., "base:npc:hoenn/prof_birch")
    /// </summary>
    [Key]
    public GameNpcId NpcId { get; set; }

    /// <summary>
    /// Display name shown in-game (e.g., "PROF. BIRCH")
    /// </summary>
    [Required]
    public string DisplayName { get; set; }

    /// <summary>
    /// NPC category (e.g., "professor", "rival", "generic", "shopkeeper")
    /// </summary>
    public string? NpcType { get; set; }

    /// <summary>
    /// Sprite/appearance ID (references sprite in AssetManager)
    /// </summary>
    public SpriteId? SpriteId { get; set; }

    /// <summary>
    /// Default behavior ID (references behavior definition)
    /// </summary>
    public string? DefaultBehaviorId { get; set; }

    /// <summary>
    /// Dialogue script path (e.g., "Dialogue/hoenn/prof_birch.csx")
    /// </summary>
    public string? DialogueScript { get; set; }

    /// <summary>
    /// Interaction script path (e.g., "Scripts/hoenn/birch_interaction.csx")
    /// </summary>
    public string? InteractionScript { get; set; }

    /// <summary>
    /// Default movement speed (tiles per second)
    /// Matches pokeemerald's MOVE_SPEED_NORMAL: 3.75 tiles/second
    /// </summary>
    public float MovementSpeed { get; set; } = 3.75f;

    /// <summary>
    /// Custom properties as JSON (extensible for modding)
    /// </summary>
    public string? CustomPropertiesJson { get; set; }

    /// <summary>
    /// Source mod ID (null for base game)
    /// </summary>
    public string? SourceMod { get; set; }

    /// <summary>
    /// Version for compatibility tracking
    /// </summary>
    public string Version { get; set; } = "1.0.0";
}
```

### 3.2 Example NPC Definition (JSON)

```json
{
  "NpcId": "base:npc:hoenn/prof_birch",
  "DisplayName": "PROF. BIRCH",
  "NpcType": "professor",
  "SpriteId": "base:sprite:overworld/birch",
  "DefaultBehaviorId": "base:behavior:stationary",
  "DialogueScript": "Dialogue/hoenn/prof_birch.csx",
  "InteractionScript": "Scripts/hoenn/birch_interaction.csx",
  "MovementSpeed": 2.0,
  "CustomPropertiesJson": "{\"gives_starter\": true, \"lab_owner\": true}",
  "SourceMod": null,
  "Version": "1.0.0"
}
```

### 3.3 Example Generic NPC Definition

```json
{
  "NpcId": "base:npc:generic/youngster",
  "DisplayName": "YOUNGSTER",
  "NpcType": "generic",
  "SpriteId": "base:sprite:overworld/youngster",
  "DefaultBehaviorId": "base:behavior:wander",
  "DialogueScript": "Dialogue/generic/youngster.csx",
  "InteractionScript": null,
  "MovementSpeed": 1.5,
  "CustomPropertiesJson": null,
  "SourceMod": null,
  "Version": "1.0.0"
}
```

---

## 4. Behavior Definition Format (Movement/AI Patterns)

Behaviors are separate, reusable patterns that control NPC movement and AI.

### 4.1 BehaviorDefinition Schema

```csharp
public class BehaviorDefinition
{
    /// <summary>
    /// Unique behavior ID (e.g., "base:behavior:wander")
    /// </summary>
    [Key]
    public string BehaviorId { get; set; }

    /// <summary>
    /// Display name for editor/debugging
    /// </summary>
    public string DisplayName { get; set; }

    /// <summary>
    /// Behavior category (e.g., "movement", "combat", "interaction")
    /// </summary>
    public string Category { get; set; }

    /// <summary>
    /// Behavior script path (e.g., "Behaviors/wander_behavior.csx")
    /// </summary>
    public string ScriptPath { get; set; }

    /// <summary>
    /// Default parameters as JSON
    /// </summary>
    public string? DefaultParametersJson { get; set; }

    /// <summary>
    /// Description for documentation
    /// </summary>
    public string? Description { get; set; }

    /// <summary>
    /// Source mod ID (null for base game)
    /// </summary>
    public string? SourceMod { get; set; }

    /// <summary>
    /// Version for compatibility tracking
    /// </summary>
    public string Version { get; set; } = "1.0.0";
}
```

### 4.2 Example Behavior Definitions (JSON)

**Stationary Behavior:**
```json
{
  "BehaviorId": "base:behavior:stationary",
  "DisplayName": "Stationary",
  "Category": "movement",
  "ScriptPath": "Behaviors/stationary_behavior.csx",
  "DefaultParametersJson": "{\"can_turn\": true, \"turn_frequency\": 5.0}",
  "Description": "NPC stays in place, occasionally turning to look around",
  "SourceMod": null,
  "Version": "1.0.0"
}
```

**Wander Behavior:**
```json
{
  "BehaviorId": "base:behavior:wander",
  "DisplayName": "Wander Around",
  "Category": "movement",
  "ScriptPath": "Behaviors/wander_behavior.csx",
  "DefaultParametersJson": "{\"wander_radius\": 3, \"wander_delay\": 2.0, \"return_to_origin\": false}",
  "Description": "NPC wanders randomly within a specified radius",
  "SourceMod": null,
  "Version": "1.0.0"
}
```

**Patrol Behavior:**
```json
{
  "BehaviorId": "base:behavior:patrol_horizontal",
  "DisplayName": "Patrol Horizontal",
  "Category": "movement",
  "ScriptPath": "Behaviors/patrol_behavior.csx",
  "DefaultParametersJson": "{\"patrol_axis\": \"horizontal\", \"patrol_distance\": 5, \"wait_time\": 1.0}",
  "Description": "NPC patrols back and forth horizontally",
  "SourceMod": null,
  "Version": "1.0.0"
}
```

**Guard Behavior:**
```json
{
  "BehaviorId": "base:behavior:guard_area",
  "DisplayName": "Guard Area",
  "Category": "movement",
  "ScriptPath": "Behaviors/guard_behavior.csx",
  "DefaultParametersJson": "{\"guard_range\": 3, \"return_speed\": 3.0, \"alert_on_player\": true}",
  "Description": "NPC guards an area and returns to position if moved",
  "SourceMod": null,
  "Version": "1.0.0"
}
```

### 4.3 Behavior Script Interface

All behavior scripts implement this pattern:

```csharp
public class WanderBehavior : ScriptBase
{
    // Per-entity state
    private Vector2 _originPosition;
    private float _wanderTimer;
    private int _wanderRadius;

    public override void Initialize(ScriptContext context)
    {
        // Get entity's starting position
        var position = context.Entity.Get<Position>();
        _originPosition = new Vector2(position.X, position.Y);

        // Parse parameters from behavior definition or instance override
        var paramsJson = context.GetProperty<string>("behaviorParams") ?? "{}";
        var parameters = JsonSerializer.Deserialize<WanderParameters>(paramsJson);
        _wanderRadius = parameters?.WanderRadius ?? 3;
    }

    public override void RegisterEventHandlers(ScriptContext context)
    {
        context.EventBus.Subscribe<TickEvent>(OnTick);
    }

    private void OnTick(TickEvent evt)
    {
        // Behavior logic here
        _wanderTimer -= evt.DeltaTime;
        if (_wanderTimer <= 0)
        {
            // Choose random direction and move
            MoveRandomly();
            _wanderTimer = Random.Shared.NextSingle() * 4.0f + 1.0f;
        }
    }
}
```

---

## 5. Field Mapping Table

### 5.1 PokEmerald to New System Mapping

| PokEmerald Field | Type | New Location | New Format | Notes |
|------------------|------|--------------|------------|-------|
| `graphics_id` | string | **NPC Definition** → `SpriteId` | `SpriteId` object | Shared sprite reference |
| `x` | int | **Tiled Object** → `x` | float (pixels) | Converted to tile coords at runtime |
| `y` | int | **Tiled Object** → `y` | float (pixels) | Converted to tile coords at runtime |
| `elevation` | int | **Tiled Object** → `properties.elevation` | int | Instance-specific elevation |
| `movement_type` | string | **Behavior Definition** → `BehaviorId` | string | E.g., `base:behavior:wander` |
| `movement_range_x` | int | **Tiled Object** → `properties.movementRange.x` | int | Instance-specific override |
| `movement_range_y` | int | **Tiled Object** → `properties.movementRange.y` | int | Instance-specific override |
| `trainer_type` | string | **Tiled Object** → `properties.trainerType` | string | Trainer-specific property |
| `trainer_sight_or_berry_tree_id` | int | **Tiled Object** → `properties.sightRange` or `berryTreeId` | int | Context-dependent |
| `script` | string | **NPC Definition** → `InteractionScript` or `DialogueScript` | string | Shared script paths |
| `flag` | string | **Tiled Object** → `properties.visibilityFlag` | string | Instance-specific visibility |
| `local_id` | string | **Tiled Object** → `properties.localId` | string | Script reference ID |

### 5.2 Data Flow Diagram

```
┌─────────────────────────────────────────────────────────┐
│                    Tiled Map (.json)                    │
│                                                         │
│  Object {                                               │
│    type: "npc"                                          │
│    x: 128, y: 192           ◄─── INSTANCE DATA         │
│    properties: {                                        │
│      npcId: "base:npc:hoenn/birch"  ────────┐          │
│      direction: "south"                      │          │
│      elevation: 0                            │          │
│      localId: "LITTLEROOT_BIRCH"            │          │
│    }                                          │          │
│  }                                            │          │
└───────────────────────────────────────────────┼──────────┘
                                                │
                                                │
                                                ▼
┌─────────────────────────────────────────────────────────┐
│              NPC Definition (Database)                  │
│                                                         │
│  NpcId: "base:npc:hoenn/birch"                         │
│  DisplayName: "PROF. BIRCH"      ◄─── SHARED DATA      │
│  SpriteId: "base:sprite:overworld/birch"               │
│  DefaultBehaviorId: "base:behavior:stationary" ────┐   │
│  DialogueScript: "Dialogue/hoenn/prof_birch.csx"   │   │
│  MovementSpeed: 2.0                                 │   │
└─────────────────────────────────────────────────────┼───┘
                                                      │
                                                      │
                                                      ▼
┌─────────────────────────────────────────────────────────┐
│           Behavior Definition (Database)                │
│                                                         │
│  BehaviorId: "base:behavior:stationary"                │
│  ScriptPath: "Behaviors/stationary_behavior.csx"       │
│  DefaultParametersJson: {"can_turn": true}             │
└─────────────────────────────────────────────────────────┘
                                                      │
                                                      │
                                                      ▼
┌─────────────────────────────────────────────────────────┐
│                    ECS Entity                           │
│                                                         │
│  Components:                                            │
│    - Position(tileX: 8, tileY: 12, mapId, elevation)   │
│    - Direction(South)                                   │
│    - Npc(npcId: "base:npc:hoenn/birch")                │
│    - Name("PROF. BIRCH")                                │
│    - Sprite("birch", "overworld")                       │
│    - Behavior("base:behavior:stationary")               │
│    - GridMovement(speed: 2.0)                           │
└─────────────────────────────────────────────────────────┘
```

---

## 6. Modding Support & Override System

### 6.1 Override Hierarchy

The system supports a **3-level override hierarchy**:

1. **Behavior Definition Defaults** (lowest priority)
   - Default parameters in `BehaviorDefinition.DefaultParametersJson`

2. **NPC Definition Defaults** (medium priority)
   - Default behavior in `NpcDefinition.DefaultBehaviorId`
   - Default appearance in `NpcDefinition.SpriteId`

3. **Tiled Object Instance** (highest priority)
   - Instance-specific overrides in Tiled object properties
   - E.g., `behaviorOverride`, `movementRange`, `waypoints`

### 6.2 Example: Override Chain

**Scenario:** Modder wants a generic youngster to patrol instead of wander

**Base Game:**
```json
// NPC Definition: base:npc:generic/youngster
{
  "DefaultBehaviorId": "base:behavior:wander",
  "MovementSpeed": 1.5
}
```

**Mod Override (Tiled Object):**
```json
{
  "type": "npc",
  "properties": [
    {"name": "npcId", "value": "base:npc:generic/youngster"},
    {"name": "behaviorOverride", "value": "base:behavior:patrol_horizontal"},
    {"name": "movementRange", "value": {"x": 5, "y": 0}}
  ]
}
```

**Result:** NPC uses patrol behavior instead of wander, but keeps other youngster properties.

### 6.3 Mod: Create New NPC Definition

Modders can create entirely new NPCs:

```json
{
  "NpcId": "mymod:npc:custom/quest_giver",
  "DisplayName": "ELDER SAGE",
  "NpcType": "quest_giver",
  "SpriteId": "mymod:sprite:overworld/sage",
  "DefaultBehaviorId": "base:behavior:stationary",
  "DialogueScript": "Mods/mymod/Dialogue/sage_quest.csx",
  "InteractionScript": "Mods/mymod/Scripts/sage_interaction.csx",
  "MovementSpeed": 2.0,
  "CustomPropertiesJson": "{\"quest_id\": \"mymod:quest:ancient_artifact\"}",
  "SourceMod": "mymod",
  "Version": "1.0.0"
}
```

### 6.4 Mod: Create New Behavior

Modders can create new behaviors:

```json
{
  "BehaviorId": "mymod:behavior:flee_from_player",
  "DisplayName": "Flee from Player",
  "Category": "movement",
  "ScriptPath": "Mods/mymod/Behaviors/flee_behavior.csx",
  "DefaultParametersJson": "{\"flee_distance\": 5, \"detection_range\": 3}",
  "Description": "NPC runs away when player gets close",
  "SourceMod": "mymod",
  "Version": "1.0.0"
}
```

---

## 7. Implementation Notes

### 7.1 Tiled Class Properties

For complex properties like `movementRange`, use Tiled's **Class** property type:

**Tiled Editor Setup:**
1. Create custom class: `MovementRange`
2. Add properties: `x` (int), `y` (int)
3. Use class type in object property

**JSON Output:**
```json
{
  "name": "movementRange",
  "type": "class",
  "value": {
    "x": 5,
    "y": 3
  }
}
```

### 7.2 MapObjectSpawner Integration

The `MapObjectSpawner` class already supports this pattern:

```csharp
// 1. Check for NPC definition reference
if (obj.Properties.TryGetValue("npcId", out object? npcIdProp))
{
    NpcDefinition? npcDef = _npcDefinitionService.GetNpc(npcId);

    // 2. Apply definition data
    builder.OverrideComponent(new Npc(npcId));
    builder.OverrideComponent(new Name(npcDef.DisplayName));
    builder.OverrideComponent(new Sprite(npcDef.SpriteId));
    builder.OverrideComponent(new Behavior(npcDef.DefaultBehaviorId));

    // 3. Apply instance overrides
    if (obj.Properties.TryGetValue("behaviorOverride", out object? behaviorOverride))
    {
        builder.OverrideComponent(new Behavior(behaviorOverride.ToString()));
    }
}
```

### 7.3 Migration Strategy

**For New Maps:**
- Use this format from the start
- No migration needed

**For Existing pokeemerald Maps:**
1. Extract unique NPCs → Create NPC definitions
2. Extract movement patterns → Create Behavior definitions
3. Convert object_events → Minimal Tiled objects
4. Map-specific overrides → Instance properties

---

## 8. Example Scenarios

### 8.1 Simple Stationary NPC

**Tiled Object:**
```json
{
  "type": "npc",
  "x": 128, "y": 192,
  "properties": [
    {"name": "npcId", "value": "base:npc:hoenn/prof_birch"},
    {"name": "direction", "value": "south"},
    {"name": "localId", "value": "LITTLEROOT_BIRCH"}
  ]
}
```

**NPC Definition provides:**
- Sprite: Prof Birch appearance
- Behavior: Stationary (default)
- Dialogue: Prof Birch script
- Movement speed: 2.0 tiles/sec

**Result:** Prof Birch stands at (128, 192) facing south, can be talked to via script reference `LITTLEROOT_BIRCH`.

---

### 8.2 Wandering NPC with Custom Range

**Tiled Object:**
```json
{
  "type": "npc",
  "x": 256, "y": 320,
  "properties": [
    {"name": "npcId", "value": "base:npc:generic/youngster"},
    {"name": "direction", "value": "east"},
    {"name": "movementRange", "value": {"x": 5, "y": 3}}
  ]
}
```

**NPC Definition provides:**
- Sprite: Generic youngster
- Behavior: Wander (default)
- Dialogue: Generic youngster dialogue
- Movement speed: 1.5 tiles/sec

**Instance override:**
- Movement range: 5x3 tiles (instead of behavior's default 3x3)

**Result:** Youngster wanders in a 5x3 area starting at (256, 320).

---

### 8.3 Patrolling Guard with Waypoints

**Tiled Object:**
```json
{
  "type": "npc",
  "x": 384, "y": 256,
  "properties": [
    {"name": "npcId", "value": "base:npc:generic/guard"},
    {"name": "direction", "value": "north"},
    {"name": "behaviorOverride", "value": "base:behavior:patrol_waypoints"},
    {"name": "waypoints", "value": "10,15;20,15;20,25;10,25"},
    {"name": "waypointWaitTime", "value": 2.0}
  ]
}
```

**NPC Definition provides:**
- Sprite: Guard appearance
- Default behavior: Stationary (overridden)
- Dialogue: Guard dialogue
- Movement speed: 2.0 tiles/sec

**Instance overrides:**
- Behavior: Patrol with waypoints
- Waypoints: Square patrol path
- Wait time: 2 seconds at each corner

**Result:** Guard patrols in a square pattern, waiting 2 seconds at each corner.

---

### 8.4 Trainer with Sight Range

**Tiled Object:**
```json
{
  "type": "trainer",
  "x": 512, "y": 384,
  "properties": [
    {"name": "trainerId", "value": "base:trainer:route102/youngster_joey"},
    {"name": "direction", "value": "south"},
    {"name": "sightRange", "value": 5},
    {"name": "trainerType", "value": "standard"},
    {"name": "localId", "value": "ROUTE102_JOEY"}
  ]
}
```

**Trainer Definition provides:**
- Sprite: Youngster Joey appearance
- Party: Joey's Pokemon team
- Dialogue: Pre/post battle dialogue
- AI: Standard trainer AI

**Instance properties:**
- Sight range: 5 tiles
- Trainer type: Standard battle

**Result:** Youngster Joey faces south, spots player within 5 tiles, initiates standard battle.

---

## 9. Benefits of This Design

### 9.1 For Map Designers

✅ **Minimal data entry** - Just reference NPC ID and set position
✅ **Visual clarity** - Tiled maps are clean and easy to read
✅ **Quick iteration** - Change NPC appearance/behavior in definition, affects all instances
✅ **Override flexibility** - Can customize specific instances without duplicating data

### 9.2 For Modders

✅ **Easy to understand** - Clear separation between instance and definition
✅ **Reusable content** - Create NPC/Behavior definitions once, use everywhere
✅ **Override system** - Can modify base game NPCs or create new ones
✅ **No conflicts** - Namespaced IDs prevent mod collisions

### 9.3 For Developers

✅ **Clean architecture** - Separation of concerns (data, behavior, instance)
✅ **EF Core integration** - Definitions in database, easy to query/update
✅ **Type safety** - Strongly-typed definitions with validation
✅ **Performance** - Shared definitions reduce memory usage
✅ **Testability** - Can test behaviors independently of NPCs

---

## 10. Future Enhancements

### 10.1 Planned Features

- **Behavior Parameters UI** - Tiled plugin for editing behavior parameters visually
- **NPC Definition Editor** - In-game tool for creating/editing NPC definitions
- **Behavior Composer** - Visual scripting for creating behaviors without code
- **Hot Reload** - Update definitions at runtime during development
- **Validation Tools** - CLI tool to validate NPC/Behavior references in maps

### 10.2 Advanced Behaviors

- **Conditional Behaviors** - Switch behaviors based on game state
- **Behavior State Machines** - Define complex multi-state behaviors
- **Behavior Inheritance** - Extend existing behaviors with new logic
- **Scripted Behaviors** - Define behaviors entirely in scripts (no C# code)

---

## 11. Appendix

### 11.1 Complete Type Reference

**Tiled Object Types:**
- `npc` - Generic NPC (references NPC definition)
- `trainer` - Trainer NPC (references Trainer definition)
- `warp_event` - Warp/door/teleport (separate from NPCs)

**Property Types:**
- `string` - Text values (IDs, scripts, flags)
- `int` - Integer values (elevation, ranges, sight)
- `float` - Decimal values (wait times, speeds)
- `class` - Structured data (movement range, custom properties)
- `bool` - True/false flags

**Direction Values:**
- `north`, `south`, `east`, `west`
- Alternative: `up`, `down`, `left`, `right`

**Trainer Types:**
- `standard` - Normal trainer battle
- `boss` - Gym leader / Elite Four
- `double` - Double battle trainer
- `partner` - Tag battle partner

### 11.2 Validation Rules

**NPC Definition:**
- ✅ NpcId must be unique
- ✅ NpcId must follow format: `namespace:type:path/name`
- ✅ DisplayName is required
- ✅ SpriteId must reference valid sprite
- ✅ DefaultBehaviorId must reference valid behavior (or null)
- ✅ Scripts must exist in file system (or null)

**Behavior Definition:**
- ✅ BehaviorId must be unique
- ✅ BehaviorId must follow format: `namespace:behavior:name`
- ✅ ScriptPath must reference valid .csx file
- ✅ DefaultParametersJson must be valid JSON (or null)

**Tiled Object:**
- ✅ Must have type: `npc` or `trainer`
- ✅ Must have npcId or trainerId property
- ✅ Referenced NPC/Trainer definition must exist
- ✅ If behaviorOverride specified, behavior must exist
- ✅ Direction must be valid value
- ✅ Elevation must be non-negative integer

---

## 12. Conclusion

This design provides a **clean, moddable, and maintainable** object format for NPCs in Tiled maps. By separating instance data from shared definitions, we achieve:

1. **Minimal duplication** - Shared data defined once
2. **Maximum flexibility** - Override at any level
3. **Easy modding** - Clear extension points
4. **Strong typing** - Validated definitions in database
5. **Clean maps** - Tiled files are concise and readable

The system is **production-ready** and can be implemented immediately with the existing `MapObjectSpawner` and EF Core infrastructure.

---

**Status:** ✅ Design Complete - Ready for Implementation
**Next Steps:** Review with team, create example maps, update map conversion tools
