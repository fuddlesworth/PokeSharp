# Map Popup Extraction Updates

## Summary of Changes

Updated the map popup extraction system to properly handle pokeemerald's tile-based rendering and generate comprehensive manifest files.

## Key Fixes

### 1. **Corrected Output Paths**
- **Before**: Hardcoded `"MonoBallFramework.Game/Assets"` in extractor, causing duplication
- **After**: Uses `output_dir` parameter directly, matching other converters
- **Impact**: Files now extract to correct paths without duplication

### 2. **Fixed Transparency Handling**
- **Before**: Treated outlines as 9-slice sprites, auto-detected and removed "background colors"
- **After**: Recognizes outlines as **tile sheets**, converts palette index 0 to transparent
- **Impact**: Preserves all 30 tiles in correct structure for tile-based rendering

### 3. **Added Comprehensive Manifests**

#### Background Manifests
Created JSON definitions for background bitmaps:

```json
{
  "Id": "wood",
  "DisplayName": "Wood",
  "Type": "Bitmap",
  "TexturePath": "Graphics/Maps/Popups/Backgrounds/wood.png",
  "Width": 80,
  "Height": 24,
  "Description": "Background bitmap for map popup"
}
```

#### Outline Tile Sheet Manifests
Created detailed tile sheet definitions similar to NPC sprite manifests:

```json
{
  "Id": "wood_outline",
  "DisplayName": "Wood Outline",
  "Type": "TileSheet",
  "TexturePath": "Graphics/Maps/Popups/Outlines/wood_outline.png",
  "TileWidth": 8,
  "TileHeight": 8,
  "TileCount": 30,
  "Tiles": [
    { "Index": 0, "X": 0, "Y": 0, "Width": 8, "Height": 8 },
    { "Index": 1, "X": 8, "Y": 0, "Width": 8, "Height": 8 },
    ...
  ],
  "TileUsage": {
    "TopEdge": [0-11],
    "LeftTopCorner": 12,
    "RightTopCorner": 13,
    "LeftMiddle": 14,
    "RightMiddle": 15,
    "LeftBottomCorner": 16,
    "RightBottomCorner": 17,
    "BottomEdge": [18-29]
  },
  "Description": "9-patch frame tile sheet for map popup (GBA tile-based rendering)"
}
```

### 4. **Documentation**

Added comprehensive documentation:
- **`docs/analysis/map-popup-tiles.md`**: Deep dive into pokeemerald's tile structure
- **`MonoBallFramework.Game/Assets/Data/Maps/Popups/README.md`**: Manifest format reference

## Files Modified

### porycon/porycon/popup_extractor.py
- Removed hardcoded `MonoBallFramework.Game/Assets` paths
- Removed incorrect 9-slice transparency processing
- Added proper palette index 0 transparency conversion
- Updated to generate tile sheet manifests with all 30 tiles
- Added `TileUsage` mapping for frame assembly

### porycon/porycon/__main__.py
- Updated log message from "9-slice rendering" to "palette transparency"

### extract_popups.sh / extract_popups.bat
- Updated to pass `MonoBallFramework.Game/Assets` as output directory
- Fixed messaging

## Output Structure

```
MonoBallFramework.Game/Assets/
├── Graphics/Maps/Popups/
│   ├── Backgrounds/
│   │   ├── wood.png           (80×24 bitmap)
│   │   ├── stone.png
│   │   └── ...
│   └── Outlines/
│       ├── wood_outline.png   (80×24 tile sheet, 30 tiles)
│       ├── stone_outline.png
│       └── ...
└── Data/Maps/Popups/
    ├── README.md              (Format documentation)
    ├── Backgrounds/
    │   ├── wood.json          (Bitmap manifest)
    │   ├── stone.json
    │   └── ...
    └── Outlines/
        ├── wood_outline.json  (Tile sheet manifest)
        ├── stone_outline.json
        └── ...
```

## Benefits

1. **Accurate Representation**: Manifests reflect actual GBA tile-based rendering
2. **Complete Metadata**: All 30 tiles defined with positions and usage
3. **Consistent Format**: Follows NPC sprite manifest pattern
4. **Extensible**: Easy to add support for rendering these in PokeSharp
5. **Documented**: Clear documentation of structure and usage

## Implementation Notes

The tile sheet manifests enable PokeSharp to:
1. Load the tile sheet texture
2. Extract individual 8×8 tiles using `Tiles` array
3. Use `TileUsage` to select appropriate tiles for frame sections
4. Assemble popup frames matching pokeemerald's rendering

This matches the GBA's tile-based hardware constraints while providing a modern, data-driven approach for the engine.



