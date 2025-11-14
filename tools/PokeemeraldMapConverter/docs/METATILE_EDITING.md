# Metatile Editing in Tiled

## Overview

The `general.json` tileset now includes **Wang Tile** definitions that allow you to paint complete 2x2 metatiles using Tiled's built-in terrain brush, replacing the need for individual `.tx` stamp files.

## What are Metatiles?

In Pokemon ROM hacking, a **metatile** is a 2x2 grid of 8x8 pixel tiles (16x16 pixels total) that represents a logical map element like grass, water, a building corner, etc. Each metatile consists of 4 tiles:

```
┌─────────┬─────────┐
│ Top-Left│Top-Right│
├─────────┼─────────┤
│Bottom-  │Bottom-  │
│  Left   │  Right  │
└─────────┴─────────┘
```

## Using Wang Tiles for Metatile Editing

### Setup in Tiled

1. **Open your map** in Tiled (e.g., `petalburg.tmx`)

2. **Open the Terrains View**:
   - Go to `View` → `Views and Toolbars` → `Terrains`
   - Or press the Terrains panel icon in the toolbar

3. **Select the general tileset** in the Tilesets panel

4. **View available metatiles**:
   - In the Terrains view, you'll see "Metatiles" terrain set
   - Each metatile appears as a distinct terrain type

### Painting Metatiles

1. **Select the Terrain Brush** tool (shortcut: `T`)

2. **Choose a metatile terrain** from the Terrains view

3. **Paint on your map**:
   - Click and drag to paint
   - The terrain brush automatically places the correct 2x2 tile configuration
   - Adjacent metatiles blend seamlessly if they share edge patterns

### Benefits over Stamp Files

| Feature | Stamp Files (.tx) | Wang Tiles |
|---------|------------------|------------|
| **Compatibility** | Broken in newer Tiled versions | Built-in Tiled feature |
| **Organization** | 256 separate files | Single tileset definition |
| **Performance** | Slower to load | Fast native rendering |
| **Editing** | Manual updates to each file | Update tileset once |
| **Visibility** | Hidden in stamps folder | Visible in Terrains panel |

## Technical Details

### Wang Tile Format

Each metatile is defined as a Wang tile with a unique terrain ID:

```json
{
  "wangsets": [{
    "name": "Metatiles",
    "type": "corner",
    "colors": [
      {"name": "Metatile_0", "color": "#ff0000", ...},
      {"name": "Metatile_1", "color": "#00ff00", ...},
      ...
    ],
    "wangtiles": [
      {
        "tileid": 0,  // Top-left tile ID of the metatile
        "wangid": [1,1,1,1,1,1,1,1]  // All edges = terrain ID 1
      },
      ...
    ]
  }]
}
```

### Tile Properties

Each tile in the tileset maintains these properties for compatibility:

- `metatile_id` (int): Which metatile group this tile belongs to (0-255)
- `metatile_pos` (string): Position within metatile (`top_left`, `top_right`, `bottom_left`, `bottom_right`)
- `collision` (bool): Whether this tile has collision
- `elevation` (int): Height level
- `behavior` (string): Tile behavior type
- `terrain` (int): Terrain type for encounters

## Regenerating Wang Tiles

If you need to regenerate the Wang tile definitions (e.g., after adding new metatiles):

```bash
cd tools/PokeemeraldMapConverter
python3 scripts/add_wang_tiles.py ../../Assets/Tilesets/general.json
```

This script:
1. Reads all tile properties to identify metatiles
2. Groups tiles by `metatile_id`
3. Creates Wang tile entries for each complete 2x2 metatile group
4. Adds the `wangsets` array to the tileset JSON

## Alternative: Simple Metadata Approach

If Wang tiles don't work well for your workflow, use the simpler metadata approach:

```bash
python3 scripts/add_wang_tiles.py --simple ../../Assets/Tilesets/general.json
```

This adds a `metatile_group` property to each tile showing all 4 tile IDs in the group:

```json
{
  "name": "metatile_group",
  "type": "string",
  "value": "0,1|16,17"  // Format: TL,TR|BL,BR
}
```

You can then search/filter by this property in Tiled.

## Troubleshooting

### Wang tiles not showing up

- **Check Tiled version**: Wang tiles require Tiled 1.9+
- **Verify tileset**: Make sure `general.json` has a `wangsets` array
- **Reload tileset**: Right-click tileset → Reload

### Metatiles painting incorrectly

- **Check tile properties**: Each tile must have valid `metatile_id` and `metatile_pos`
- **Verify completeness**: Each metatile must have all 4 positions defined
- **Regenerate Wang tiles**: Run the script again to rebuild definitions

### Performance issues

- **Use smaller tilesets**: Consider splitting into multiple tilesets by area
- **Optimize PNG**: Compress `general_tiles.png` with tools like `pngquant`
- **Disable unused tilesets**: Only load tilesets needed for current map

## Migration from Stamp Files

Existing `.tx` stamp files in `Assets/Stamps/General/` are no longer needed but are kept for backward compatibility. You can safely:

1. Continue using them in older Tiled versions
2. Delete them once you've migrated to Wang tiles
3. Keep them as reference for metatile layouts

The Wang tile approach is **recommended** for all new editing work.

## See Also

- [Tiled Wang Tiles Documentation](https://doc.mapeditor.org/en/stable/manual/using-wang-tiles/)
- [Tiled Terrain Documentation](https://doc.mapeditor.org/en/stable/manual/using-terrains/)
- Original stamp files: `Assets/Stamps/General/metatile_*.tx`
