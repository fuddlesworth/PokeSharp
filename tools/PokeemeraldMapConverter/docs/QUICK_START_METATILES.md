# Quick Start: Metatile Editing

## TL;DR

Use Tiled's **Terrain Brush** to paint complete 2x2 metatiles instead of individual tiles.

## 3 Steps to Get Started

### 1. Open Terrains Panel

In Tiled: `View` → `Views and Toolbars` → `Terrains`

### 2. Select Terrain Brush

Press `T` or click the Terrain Brush tool

### 3. Paint Metatiles

- Choose a terrain from "Metatiles" set
- Click/drag on your map
- Complete 2x2 metatiles are painted automatically

## What Changed?

### Before (Old Method)
- Used 256 individual `.tx` stamp files
- Stamps not loading in newer Tiled versions
- Manual placement required

### After (New Method)
- Wang tiles built into `general.json`
- Native Tiled terrain feature
- Automatic 2x2 metatile placement
- Works in all modern Tiled versions

## Example Workflow

```
1. Open petalburg.tmx in Tiled
2. Open Terrains panel (View → Terrains)
3. Select "general" tileset
4. Press T for Terrain Brush
5. Choose "Metatile_42" (grass)
6. Paint on map → automatic 2x2 placement!
```

## Key Benefits

✅ **Compatible**: Works in Tiled 1.9+
✅ **Fast**: Paint entire metatiles in one click
✅ **Organized**: All definitions in one JSON file
✅ **Automatic**: Correct tile placement guaranteed

## Troubleshooting

**Don't see Terrains panel?**
- Go to `View` → `Views and Toolbars` → Check "Terrains"

**Wang tiles not appearing?**
- Make sure you're using Tiled 1.9 or newer
- Right-click tileset → "Reload"

**Need old stamp files?**
- They're still in `Assets/Stamps/General/` for backward compatibility
- Can be deleted once you've migrated to Wang tiles

## Full Documentation

See [METATILE_EDITING.md](./METATILE_EDITING.md) for complete details.
