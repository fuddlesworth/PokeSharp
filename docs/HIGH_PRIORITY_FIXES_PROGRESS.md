# High Priority Fixes - Progress Report

**Date:** December 2, 2025  
**Status:** In Progress

---

## Completed ✅

### 1. PoolNames Constants Class ✅
**File Created:** `PokeSharp.Engine.Systems/Pooling/PoolNames.cs`

Created centralized constants class with:
- `Player`, `Npc`, `Tile` - Currently used
- `Default` - Fallback pool
- `Projectile`, `Particle`, `UI`, `Item`, `Pokemon` - For future use

### 2. Pool Name String Literals Replaced ✅
**Files Updated:**
- `CoreServicesExtensions.cs` - Pool registration
- `EntityPoolManager.cs` - Default values and fallbacks
- `EntityPool.cs` - Constructor default
- `PoolConfiguration.cs` - Configuration defaults  
- `EntityFactoryServicePooling.cs` - Method defaults

**Impact:** All pool names now use type-safe constants

### 3. Theme Layout Constants Added ✅
**File Updated:** `PokeSharp.Engine.UI.Debug/Core/UITheme.cs`

Added new properties to all 8 themes:
- `PanelMaxWidth = 800f`
- `PanelMaxHeight = 600f`
- `DropdownMaxHeight = 300f`
- `DocumentationMinWidth = 400f`
- `DocumentationMaxWidth = 600f`
- `ScrollSpeed = 30`
- `TableColumnWidth = 80`
- `SectionSpacing = 8`

All themes now have consistent layout constants!

---

## In Progress 🔄

### 4. Update UI Components to Use Theme Constants
**Files to Update:**
- `ConsolePanelBuilder.cs` - Replace magic numbers (800, 300, 30)
- `DebugPanelBase.cs` - Remove hardcoded constants (8, 5)
- `StatsContent.cs` - Use theme for layout values

---

## Pending ⏳

### 5. Create Centralized EcsQueries Class
Create `PokeSharp.Engine.Systems/Queries/EcsQueries.cs` with static readonly queries

### 6. Move WarpSystem Queries to Static Class
Update `WarpSystem.cs` to use centralized queries instead of instance fields

---

## Summary

**Progress:** 3/6 tasks completed (50%)

**Next Steps:**
1. Update ConsolePanelBuilder to use theme constants
2. Update DebugPanelBase to use theme constants
3. Update StatsContent to use theme constants
4. Create EcsQueries static class
5. Move WarpSystem queries

**Estimated Time Remaining:** 1-2 hours

