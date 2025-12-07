# NPC System Refactor Plan

## Overview

Simplify the NPC object system by eliminating the NPC Definition tier and allowing map objects to directly compose sprites and behaviors.

**Current System (3-tier):**
```
Tiled Object → npcId → NPC Definition → behaviorScript → Behavior Definition
```

**New System (2-tier):**
```
Tiled Object → spriteId + behaviorId → Behavior Definition
```

## Design Principles

1. **Sprites and behaviors are independent** - same sprite can use different behaviors on different maps
2. **Map objects are the source of truth** - all instance data lives in the Tiled object
3. **Behavior definitions provide defaults** - map can override any parameter
4. **Modder-friendly** - add sprites or behaviors independently

---

## Phase 1: Behavior Template Expansion

### 1.1 Create Behavior Definitions for pokeemerald Movement Types

Create behavior JSON files in `Assets/Data/Behaviors/` for the most common movement types:

| Behavior ID | pokeemerald Movement Type | Parameters |
|-------------|---------------------------|------------|
| `stationary` | FACE_DOWN, FACE_UP, FACE_LEFT, FACE_RIGHT | direction |
| `look_around` | LOOK_AROUND | directions[], interval |
| `wander` | WANDER_AROUND | rangeX, rangeY |
| `wander_horizontal` | WANDER_LEFT_AND_RIGHT | range |
| `wander_vertical` | WANDER_UP_AND_DOWN | range |
| `walk_in_place` | WALK_IN_PLACE_* | direction |
| `rotate_clockwise` | ROTATE_CLOCKWISE | interval |
| `rotate_counterclockwise` | ROTATE_COUNTERCLOCKWISE | interval |
| `patrol_square` | WALK_SEQUENCE_* | waypoints (auto-generated) |
| `copy_player` | COPY_PLAYER_* | mode (normal, opposite, clockwise) |
| `hidden` | INVISIBLE | - |
| `berry_tree` | BERRY_TREE_GROWTH | - |
| `disguise` | TREE_DISGUISE, MOUNTAIN_DISGUISE, BURIED | disguiseType |

**Files to create:** ~15 new behavior definition JSONs

### 1.2 Create Behavior Scripts

For each behavior definition, create corresponding `.csx` scripts in `Assets/Scripts/Behaviors/`:

- `stationary_behavior.csx` - No movement, just face direction
- `look_around_behavior.csx` - Periodically change facing direction
- `wander_behavior.csx` - Random movement within range (already exists)
- `patrol_behavior.csx` - Follow waypoints (already exists)
- etc.

**Priority:** Focus on the top 10 most used (covers 95% of NPCs):
1. FACE_DOWN (914 uses)
2. LOOK_AROUND (526 uses)
3. FACE_RIGHT (269 uses)
4. FACE_UP (227 uses)
5. FACE_LEFT (220 uses)
6. WANDER_AROUND (159 uses)
7. BERRY_TREE_GROWTH (88 uses)
8. WANDER_LEFT_AND_RIGHT (42 uses)
9. FACE_DOWN_AND_LEFT (32 uses)
10. WANDER_UP_AND_DOWN (30 uses)

---

## Phase 2: porycon Converter Updates

### 2.1 Add Movement Type Mapping

In `porycon/porycon/constants.py`, add mapping:

```python
MOVEMENT_TYPE_TO_BEHAVIOR = {
    "MOVEMENT_TYPE_FACE_DOWN": ("stationary", {"direction": "down"}),
    "MOVEMENT_TYPE_FACE_UP": ("stationary", {"direction": "up"}),
    "MOVEMENT_TYPE_FACE_LEFT": ("stationary", {"direction": "left"}),
    "MOVEMENT_TYPE_FACE_RIGHT": ("stationary", {"direction": "right"}),
    "MOVEMENT_TYPE_LOOK_AROUND": ("look_around", {}),
    "MOVEMENT_TYPE_WANDER_AROUND": ("wander", {}),
    # ... etc
}
```

### 2.2 Add Graphics ID to Sprite Mapping

Extend `porycon/porycon/id_transformer.py`:

```python
def sprite_id_from_graphics(graphics_id: str) -> str:
    """Convert OBJ_EVENT_GFX_* to sprite ID."""
    # Already partially implemented - extend for full coverage
    # OBJ_EVENT_GFX_BOY_1 → "npc/boy_1"
    # OBJ_EVENT_GFX_BIRCH → "character/birch"
```

### 2.3 Add object_events Conversion

In `porycon/porycon/converter.py`, add new section in `_create_tiled_map_structure()` after bg_events (~line 1314):

```python
# Add NPCs object layer from object_events
object_events = map_data.get("object_events", [])
if object_events:
    npc_objects = []
    npc_object_id = tiled_map.get("nextobjectid", 1)

    for obj_event in object_events:
        npc_obj = self._convert_object_event(obj_event, npc_object_id, map_name)
        if npc_obj:
            npc_objects.append(npc_obj)
            npc_object_id += 1

    if npc_objects:
        npc_layer = {
            "id": len(tiled_map["layers"]) + 1,
            "name": "NPCs",
            "type": "objectgroup",
            "visible": True,
            "opacity": 1,
            "x": 0, "y": 0,
            "objects": npc_objects
        }
        tiled_map["layers"].append(npc_layer)
        tiled_map["nextobjectid"] = npc_object_id
```

### 2.4 Implement _convert_object_event Method

```python
def _convert_object_event(self, obj_event: dict, object_id: int, map_name: str) -> dict:
    """Convert pokeemerald object_event to Tiled NPC object."""

    graphics_id = obj_event.get("graphics_id", "")
    movement_type = obj_event.get("movement_type", "MOVEMENT_TYPE_FACE_DOWN")

    # Get behavior and default params from movement type
    behavior_id, behavior_params = MOVEMENT_TYPE_TO_BEHAVIOR.get(
        movement_type, ("stationary", {"direction": "down"})
    )

    # Build properties list
    properties = [
        {"name": "spriteId", "type": "string", "value": sprite_id_from_graphics(graphics_id)},
        {"name": "behaviorId", "type": "string", "value": behavior_id},
        {"name": "elevation", "type": "int", "value": obj_event.get("elevation", 0)},
    ]

    # Add movement range for wander behaviors
    if "wander" in behavior_id:
        properties.extend([
            {"name": "rangeX", "type": "int", "value": obj_event.get("movement_range_x", 1)},
            {"name": "rangeY", "type": "int", "value": obj_event.get("movement_range_y", 1)},
        ])

    # Add direction for stationary behaviors
    if behavior_id == "stationary":
        properties.append({
            "name": "direction", "type": "string",
            "value": behavior_params.get("direction", "down")
        })

    # Add script reference if present
    script = obj_event.get("script", "")
    if script:
        properties.append({"name": "script", "type": "string", "value": script})

    # Add flag if present
    flag = obj_event.get("flag", "0")
    if flag and flag != "0":
        properties.append({"name": "flag", "type": "string", "value": flag})

    # Add trainer data if applicable
    trainer_type = obj_event.get("trainer_type", "TRAINER_TYPE_NONE")
    if trainer_type != "TRAINER_TYPE_NONE":
        properties.append({"name": "trainerType", "type": "string", "value": trainer_type})
        sight_range = obj_event.get("trainer_sight_or_berry_tree_id", "0")
        if sight_range != "0":
            properties.append({"name": "sightRange", "type": "int", "value": int(sight_range)})

    return {
        "id": object_id,
        "name": obj_event.get("local_id", f"NPC_{object_id}"),
        "type": "npc",
        "x": obj_event.get("x", 0) * METATILE_SIZE,
        "y": obj_event.get("y", 0) * METATILE_SIZE,
        "width": METATILE_SIZE,
        "height": METATILE_SIZE,
        "visible": True,
        "properties": properties
    }
```

---

## Phase 3: Engine Updates

### 3.1 Update MapObjectSpawner

Modify `MapObjectSpawner.cs` to handle the new format:

**Current flow:**
```
npcId → NpcDefinitionService.GetNpc() → Apply definition
```

**New flow:**
```
spriteId + behaviorId → Direct component creation
```

Update `ApplyNpcDefinition()` to prioritize direct properties:

```csharp
private void ApplyNpcDefinition(EntityBuilder builder, TmxObject obj, ...)
{
    // NEW: Direct sprite specification (preferred)
    if (obj.Properties.TryGetValue("spriteId", out object? spriteIdProp))
    {
        var spriteId = new SpriteId(spriteIdProp.ToString());
        requiredSpriteIds?.Add(spriteId);
        builder.OverrideComponent(new Sprite(spriteId.SpriteName, spriteId.Category));
    }

    // NEW: Direct behavior specification (preferred)
    if (obj.Properties.TryGetValue("behaviorId", out object? behaviorIdProp))
    {
        builder.OverrideComponent(new Behavior(behaviorIdProp.ToString()));

        // Apply behavior parameters from map object
        ApplyBehaviorParameters(builder, obj);
    }

    // LEGACY: Fall back to npcId lookup (for existing maps)
    else if (obj.Properties.TryGetValue("npcId", out object? npcIdProp))
    {
        // ... existing NpcDefinitionService logic (keep for migration)
    }
}

private void ApplyBehaviorParameters(EntityBuilder builder, TmxObject obj)
{
    // Movement range for wander behaviors
    if (obj.Properties.TryGetValue("rangeX", out object? rangeX) &&
        obj.Properties.TryGetValue("rangeY", out object? rangeY))
    {
        builder.OverrideComponent(new MovementRange(
            Convert.ToInt32(rangeX),
            Convert.ToInt32(rangeY)
        ));
    }

    // Movement speed override
    if (obj.Properties.TryGetValue("movementSpeed", out object? speed))
    {
        builder.OverrideComponent(new GridMovement(Convert.ToSingle(speed)));
    }
}
```

### 3.2 Add MovementRange Component

Create new ECS component for wander bounds:

```csharp
public struct MovementRange
{
    public int RangeX { get; init; }
    public int RangeY { get; init; }

    public MovementRange(int rangeX, int rangeY)
    {
        RangeX = rangeX;
        RangeY = rangeY;
    }
}
```

### 3.3 Update Behavior Scripts to Use MovementRange

Modify `wander_behavior.csx` to read from MovementRange component instead of hardcoded values.

---

## Phase 4: Cleanup

### 4.1 Files to Delete

After migration is complete and verified:

```
MonoBallFramework.Game/Assets/Data/NPCs/
├── guard.json          # DELETE
├── guard_001.json      # DELETE
├── generic_villager.json # DELETE
├── prof_birch.json     # DELETE
└── wanderer.json       # DELETE
```

### 4.2 Code to Remove

- `NpcDefinition.cs` - Entity class (keep TrainerDefinition for now)
- `NpcDefinitionService.GetNpc()` method - No longer needed
- References in DbContext to NpcDefinition table
- NPC definition seeding/migration code

### 4.3 Update Entity Templates

Remove `npcId` requirement from entity templates, update to expect `spriteId` + `behaviorId`:

```json
// templates/npc.json (before)
{
  "id": "npc",
  "components": ["Position", "Npc", "Name", "Sprite", "Behavior"]
}

// templates/npc.json (after)
{
  "id": "npc",
  "components": ["Position", "Sprite", "Behavior", "GridMovement"]
}
```

---

## Phase 5: Testing & Validation

### 5.1 Unit Tests

- Test MOVEMENT_TYPE → behavior mapping coverage
- Test graphics_id → spriteId mapping
- Test object_event → Tiled object conversion

### 5.2 Integration Tests

- Convert a pokeemerald map with NPCs
- Load converted map in game
- Verify NPCs spawn at correct positions
- Verify behaviors execute correctly

### 5.3 Visual Validation

- Spot check 10 maps with high NPC counts
- Verify sprite assignments look correct
- Verify movement behaviors match original

---

## Implementation Order

1. **Phase 1.1** - Create behavior definitions for top 10 movement types
2. **Phase 2.1-2.4** - Implement porycon converter changes
3. **Phase 3.1** - Update MapObjectSpawner for new format
4. **Phase 5.2** - Test with converted map
5. **Phase 1.2** - Create behavior scripts as needed
6. **Phase 3.2-3.3** - Add MovementRange component
7. **Phase 4** - Cleanup after full validation

---

## Output Format

### Example Converted NPC Object

**pokeemerald input:**
```json
{
  "local_id": "LOCALID_LITTLEROOT_TWIN",
  "graphics_id": "OBJ_EVENT_GFX_TWIN",
  "x": 16, "y": 10,
  "elevation": 3,
  "movement_type": "MOVEMENT_TYPE_WANDER_AROUND",
  "movement_range_x": 1,
  "movement_range_y": 2,
  "trainer_type": "TRAINER_TYPE_NONE",
  "script": "LittlerootTown_EventScript_Twin",
  "flag": "0"
}
```

**Tiled output:**
```json
{
  "id": 1,
  "name": "LOCALID_LITTLEROOT_TWIN",
  "type": "npc",
  "x": 256, "y": 160,
  "width": 16, "height": 16,
  "properties": [
    {"name": "spriteId", "type": "string", "value": "npc/twin"},
    {"name": "behaviorId", "type": "string", "value": "wander"},
    {"name": "rangeX", "type": "int", "value": 1},
    {"name": "rangeY", "type": "int", "value": 2},
    {"name": "elevation", "type": "int", "value": 3},
    {"name": "script", "type": "string", "value": "LittlerootTown_EventScript_Twin"}
  ]
}
```

---

## Risk Mitigation

1. **Keep legacy npcId support** during migration (Phase 3.1)
2. **Implement in porycon first** - can regenerate maps easily
3. **Test with small map first** before bulk conversion
4. **Delay cleanup (Phase 4)** until fully validated
