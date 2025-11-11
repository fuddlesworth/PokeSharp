# Elevation System Refactor - Summary

## Overview

Successfully refactored the entire rendering pipeline from a **layer-based system** (Ground/Object/Overhead) to a **Pokemon Emerald-style elevation system**. This is a clean break - no backwards compatibility, no fallbacks.

## Key Changes

### 1. **New Elevation Component** (`PokeSharp.Game.Components/Components/Rendering/Elevation.cs`)
- Represents elevation levels from 0-15 (Pokemon Emerald standard)
- Predefined constants:
  - `Ground` (0): Water, pits, lower terrain
  - `Default` (3): Most tiles and objects
  - `Bridge` (6): Elevated platforms, bridges
  - `Overhead` (9): Tall structures, roofs
  - `Max` (15): Maximum elevation
- Used for both **render order** and **collision detection**

### 2. **Removed Old Layer System**
- ✅ Deleted `TileLayer.cs` enum
- ✅ Deleted `LayerTags.cs` (GroundLayerTag, ObjectLayerTag, OverheadLayerTag)
- ✅ Removed `Layer` property from `TileSprite` component
- No fallbacks, no backwards compatibility

### 3. **Renamed System: ZOrderRenderSystem → ElevationRenderSystem**
- File: `PokeSharp.Engine.Rendering/Systems/ElevationRenderSystem.cs`
- New render depth formula (Pokemon Emerald model):
  ```csharp
  layerDepth = 1.0 - ((elevation * 16) + (y / mapHeight)) / 241.0
  ```
- Allows **16 Y-sorted positions per elevation level**
- Simplified queries: One query for all tiles/sprites with `Elevation` component
- Removed all layer-specific query logic

### 4. **Collision System Now Checks Elevation**
- File: `PokeSharp.Game.Systems/Movement/CollisionSystem.cs`
- `IsPositionWalkable()` now accepts `entityElevation` parameter (default: 3)
- **Entities only collide with objects at the SAME elevation**
  - Bridge at elevation 6 doesn't collide with water at elevation 0
  - Player at elevation 3 walks under overhead structures at elevation 9+
- Updated `ICollisionService` interface to match

### 5. **Map Loader: Elevation from Tiled**
- File: `PokeSharp.Game.Data/MapLoading/Tiled/MapLoader.cs`
- `DetermineElevation()` method reads elevation from:
  1. **Tile properties** (`elevation` custom property) - highest priority
  2. **Layer name** (case-insensitive):
     - "ground", "water" → elevation 0
     - "overhead", "roof" → elevation 9
     - "bridge" → elevation 6
  3. **Layer index** (fallback):
     - Index 0 → elevation 0 (Ground)
     - Index 1 → elevation 3 (Default)
     - Index 2+ → elevation 9 (Overhead)
- Every tile entity gets an `Elevation` component

### 6. **Templates Updated**
- File: `PokeSharp.Game/Templates/TemplateRegistry.cs`
- All tile templates now include `Elevation` component:
  - `tile/base`: elevation 3 (Default)
  - `tile/ground`: elevation 0 (Ground)
  - `tile/wall`: elevation 3 (Default)

## Benefits

### **Matches Pokemon Emerald Architecture**
- Direct implementation of pokeemerald decompilation elevation system
- Proper multi-level map support (caves, bridges, multi-story buildings)

### **More Flexible Rendering**
- 16 Y-sorted positions per elevation level (vs. 3 fixed layers)
- Fine-grained control over render order
- Easier to implement complex scenarios (walking under bridges, elevated platforms)

### **Collision Improvements**
- Elevation-aware collision detection
- Walk under overhead structures naturally
- Bridges no longer collide with ground/water below

### **Cleaner Codebase**
- Removed 3 tag component types
- Removed enum with 3 values
- Single unified query for all tiles
- Simplified rendering logic (one pass instead of three)

## How to Use in Tiled

### **Method 1: Layer Names (Automatic)**
Name your layers to automatically assign elevation:
- `Ground`, `Water` → elevation 0
- `Objects` (default layer) → elevation 3
- `Overhead`, `Roof` → elevation 9
- `Bridge` → elevation 6

### **Method 2: Custom Tile Properties**
Add an `elevation` custom property to individual tiles in Tiled:
1. Select tile in tileset
2. Add custom property: `elevation` (integer)
3. Set value: 0-15

### **Method 3: Let Layer Index Decide**
- First layer → elevation 0
- Second layer → elevation 3
- Third+ layers → elevation 9

## Migration Notes

### **Breaking Changes**
- ❌ No `TileLayer` enum
- ❌ No `GroundLayerTag`, `ObjectLayerTag`, `OverheadLayerTag`
- ❌ `TileSprite` constructor signature changed (no `layer` parameter)
- ❌ `ZOrderRenderSystem` renamed to `ElevationRenderSystem`

### **If you have existing code:**
1. Replace `TileLayer.Ground` → `Elevation.Ground` (0)
2. Replace `TileLayer.Object` → `Elevation.Default` (3)
3. Replace `TileLayer.Overhead` → `Elevation.Overhead` (9)
4. Replace queries for `GroundLayerTag` with queries for `Elevation`
5. Replace `ZOrderRenderSystem` → `ElevationRenderSystem`

## Files Changed

### **Created:**
- `PokeSharp.Game.Components/Components/Rendering/Elevation.cs`

### **Deleted:**
- `PokeSharp.Game.Components/Components/Tiles/TileLayer.cs`
- `PokeSharp.Game.Components/Components/Tiles/LayerTags.cs`

### **Renamed:**
- `PokeSharp.Engine.Rendering/Systems/ZOrderRenderSystem.cs` → `ElevationRenderSystem.cs`

### **Modified:**
- `PokeSharp.Game.Components/Components/Tiles/TileSprite.cs` (removed Layer property)
- `PokeSharp.Engine.Rendering/Systems/ElevationRenderSystem.cs` (complete refactor)
- `PokeSharp.Game.Systems/Movement/CollisionSystem.cs` (elevation-aware collision)
- `PokeSharp.Game.Systems/Services/ICollisionService.cs` (added elevation parameter)
- `PokeSharp.Game.Data/MapLoading/Tiled/MapLoader.cs` (elevation from Tiled)
- `PokeSharp.Game/Templates/TemplateRegistry.cs` (updated templates)
- `PokeSharp.Game/Initialization/GameInitializer.cs` (system rename)
- `PokeSharp.Game/Input/InputManager.cs` (system rename)
- `PokeSharp.Game/Initialization/MapInitializer.cs` (system rename)

## Build Status

✅ **Build Successful** (0 errors, 5 warnings)
- All warnings are pre-existing TODOs, unrelated to elevation refactor

## Testing Recommendations

1. **Render Order**: Create a test map with tiles at different elevations (0, 3, 6, 9)
2. **Collision**: Create a bridge at elevation 6 over water at elevation 0, verify player can walk on bridge without colliding with water
3. **Y-Sorting**: Place multiple entities at the same elevation but different Y positions, verify correct sorting
4. **Overhead Structures**: Create overhead tiles at elevation 9+, verify player walks under them

## Performance Impact

- **Positive**: Simplified rendering (one pass instead of three layer passes)
- **Neutral**: Elevation check in collision is negligible (single byte comparison)
- **Positive**: Removed 3 tag components from archetype queries

## Future Enhancements

1. **Elevation Transitions**: Special tiles that change player elevation (stairs, ramps)
2. **Elevation-Based Visibility**: Hide/show layers based on player elevation
3. **Ledges with Elevation**: Ledges that work across elevation changes
4. **Camera Elevation**: Camera follows player elevation for proper rendering

---

**Refactor Date:** November 11, 2025
**Status:** ✅ Complete, Build Passing

