# Elevation System Fix - Player & NPCs Not Rendering

## Problem
After refactoring to the elevation system, **player and NPCs were not rendering** because:
1. `ElevationRenderSystem` queries **require** `Elevation` component on all sprites
2. Player and NPC templates did not have `Elevation` components
3. Map was loading tiles, but no sprites were visible

## Root Cause
The render queries were changed from:
```csharp
// OLD (layer-based)
QueryCache.Get<Position, Sprite, GridMovement>()

// NEW (elevation-based) - REQUIRES Elevation!
QueryCache.Get<Position, Sprite, GridMovement, Elevation>()
```

But the templates didn't add `Elevation` components.

## Solution

### 1. **Player Template** (`TemplateRegistry.cs`)
Added `Elevation.Default` (3) to player template:
```csharp
template.WithComponent(new Elevation(Elevation.Default)); // Standard elevation (3)
```

### 2. **NPC Base Template** (`TemplateRegistry.cs`)
Added `Elevation.Default` (3) to NPC base template (inherited by all NPCs):
```csharp
baseNpc.WithComponent(new Elevation(Elevation.Default)); // Standard elevation (3)
```

### 3. **Map Object Spawning** (`MapLoader.cs`)
Added support for custom `elevation` property on Tiled objects:
```csharp
// Apply custom elevation if specified (Pokemon Emerald style)
if (obj.Properties.TryGetValue("elevation", out var elevProp))
{
    var elevValue = Convert.ToByte(elevProp);
    builder.OverrideComponent(new Elevation(elevValue));
}
```

## Pokemon Emerald Reference
In Pokemon Emerald, **all objects (player, NPCs) default to elevation 3**, which matches the standard ground tile elevation. This allows proper rendering and collision detection.

## Current Map Elevation Setup

**test-map.json** now has automatic elevation assignment:

| Layer | Name | Index | Elevation | Reason |
|-------|------|-------|-----------|--------|
| 0 | "Ground" | 0 | **0** | Layer name contains "ground" |
| 1 | "Objects" | 1 | **3** | Default for index 1 |
| 2 | "Overhead" | 2 | **9** | Layer name contains "overhead" |

**NPCs in test-map.json**:
- Generic NPC → elevation **3** (from base template)
- Stationary NPC → elevation **3** (from base template)
- Trainer → elevation **3** (from base template)
- Patrol Guard → elevation **3** (from base template)

**Player**:
- Player → elevation **3** (from player template)

## Testing

### ✅ **What Should Work Now:**
1. **Player renders** at elevation 3
2. **All NPCs render** at elevation 3
3. **All tiles render** with their assigned elevations
4. **Collision works** - entities at elevation 3 collide with tiles at elevation 3
5. **Overhead tiles** (elevation 9) render on top of everything

### 🧪 **Test Scenarios:**
1. **Walk around** - Player should be visible
2. **Walk near NPCs** - NPCs should be visible and block movement
3. **Walk under overhead tiles** - You should see the overhead layer (tile 6) rendering above you

### 🔧 **Advanced Testing (Optional):**
Add elevation to specific NPCs in Tiled:
1. Open `test-map.json` in Tiled
2. Select an NPC object
3. Add custom property: `elevation` (int) = `6`
4. Save and run - that NPC will be on a "bridge" at elevation 6

## Files Changed
- ✅ `PokeSharp.Game/Templates/TemplateRegistry.cs` (added Elevation to player + NPC base)
- ✅ `PokeSharp.Game.Data/MapLoading/Tiled/MapLoader.cs` (added elevation override for objects)

## Build Status
✅ **Build Successful** (0 errors)

---

**Fix Date:** November 11, 2025
**Status:** ✅ Complete - Player & NPCs Now Rendering

