# Map Popup Rendering System Updates

## Overview

Updated the map popup rendering system to support GBA-accurate tile-based rendering while maintaining backwards compatibility with legacy 9-slice rendering.

## Summary of Changes

### New Files Created

#### 1. **PopupTileDefinition.cs**
Defines individual tiles in a tile sheet:
```csharp
public class PopupTileDefinition
{
    public int Index { get; init; }      // Tile number (0-29)
    public int X { get; init; }          // X position in sheet
    public int Y { get; init; }          // Y position in sheet
    public int Width { get; init; }      // Tile width (8px)
    public int Height { get; init; }     // Tile height (8px)
}
```

#### 2. **PopupTileUsage.cs**
Maps tile indices to their usage in the frame:
```csharp
public class PopupTileUsage
{
    public List<int> TopEdge { get; init; }        // Tiles 0-11
    public int LeftTopCorner { get; init; }        // Tile 12
    public int RightTopCorner { get; init; }       // Tile 13
    public int LeftMiddle { get; init; }           // Tile 14
    public int RightMiddle { get; init; }          // Tile 15
    public int LeftBottomCorner { get; init; }     // Tile 16
    public int RightBottomCorner { get; init; }    // Tile 17
    public List<int> BottomEdge { get; init; }     // Tiles 18-29
}
```

### Updated Files

#### 3. **PopupOutlineDefinition.cs**
- Added `Type` property to distinguish "TileSheet" vs "9Slice" rendering
- Added tile sheet properties: `TileWidth`, `TileHeight`, `TileCount`, `Tiles`, `TileUsage`
- Maintained backwards compatibility with legacy `cornerWidth`, `cornerHeight` properties
- Added `IsTileSheet` property for easy mode detection
- Supports both PascalCase (new) and camelCase (old) JSON property names

#### 4. **PopupBackgroundDefinition.cs**
- Updated property names to PascalCase (with camelCase fallbacks)
- Added `Type` property (always "Bitmap")
- Renamed `defaultWidth/defaultHeight` to `Width/Height`
- Added optional `Description` property

#### 5. **MapPopupScene.cs**
- Split `DrawNineSliceBorder()` into three methods:
  - `DrawNineSliceBorder()` - Router that detects render mode
  - `DrawTileSheetBorder()` - **NEW**: GBA-accurate tile assembly
  - `DrawLegacyNineSliceBorder()` - Legacy 9-slice rendering
- Tile sheet rendering assembles frames from individual 8×8 tiles
- No changes to animation, positioning, or text rendering

## Rendering Modes Comparison

### Tile Sheet Mode (GBA-Accurate)

**When Used**: `Type: "TileSheet"` with tile data present

**How It Works**:
1. Loads 80×24 pixel tile sheet (10×3 tiles)
2. Uses `TileUsage` to select tiles for each frame section
3. Draws individual 8×8 tiles at calculated positions
4. Repeats edge tiles as needed to fill the frame width/height

**Benefits**:
- ✅ Pixel-perfect match to pokeemerald
- ✅ No scaling or stretching artifacts
- ✅ Authentic GBA look and feel

**Considerations**:
- More draw calls (one per tile)
- Fixed tile size (8×8 pixels)

### Legacy 9-Slice Mode

**When Used**: No `Type` field or `Type: "9Slice"`

**How It Works**:
1. Divides texture into 9 regions (corners, edges, center)
2. Corners are never stretched
3. Edges are stretched along one axis
4. Center is transparent

**Benefits**:
- ✅ Fewer draw calls (9 total)
- ✅ More flexible for varying sizes
- ✅ Works with any texture layout

**Considerations**:
- Can show stretching artifacts
- Not accurate to original GBA

## Tile Sheet Assembly Algorithm

### Frame Structure

```
┌────────┬────────┬────────┬────────┬────────┐
│  TL    │  Top   │  Top   │  Top   │  TR    │
│ (12)   │ (0-11) │ (0-11) │ (0-11) │ (13)   │
├────────┼────────┼────────┼────────┼────────┤
│ Left   │                           │ Right  │
│ (14)   │     Background            │ (15)   │
│ (14)   │     (stretched)           │ (15)   │
├────────┼────────┼────────┼────────┼────────┤
│  BL    │ Bottom │ Bottom │ Bottom │  BR    │
│ (16)   │(18-29) │(18-29) │(18-29) │ (17)   │
└────────┴────────┴────────┴────────┴────────┘
```

### Rendering Steps

1. **Corners** (4 tiles): Fixed at frame corners
2. **Top/Bottom Edges**: Cycle through edge tiles (0-11, 18-29) until width is filled
3. **Left/Right Sides**: Repeat middle tiles (14, 15) until height is filled

### Example: 200px Wide Frame

```
Frame width: 200px
Tile width: 8px
Corner space: 8px (left) + 8px (right) = 16px
Edge space: 200px - 16px = 184px
Tiles needed: 184px / 8px = 23 tiles

Top edge rendering:
[0][1][2][3][4][5][6][7][8][9][10][11][0][1][2][3][4][5][6][7][8][9][10]
(Cycles through tiles 0-11, 23 times total)
```

## Backwards Compatibility

### JSON Property Names

Both formats are supported:

```json
// New format (PascalCase)
{
  "Id": "wood",
  "DisplayName": "Wood",
  "Type": "TileSheet",
  "TexturePath": "Graphics/Maps/Popups/Outlines/wood_outline.png"
}

// Old format (camelCase) - still works
{
  "id": "wood",
  "displayName": "Wood",
  "texturePath": "Graphics/Maps/Popups/Outlines/wood_outline.png",
  "cornerWidth": 8
}
```

### Type Detection

```csharp
if (outline.IsTileSheet) {
    // Has Type="TileSheet" AND has Tiles AND has TileUsage
    DrawTileSheetBorder();
} else {
    // Missing tile data or Type != "TileSheet"
    DrawLegacyNineSliceBorder();
}
```

## Performance Considerations

### Tile Sheet Mode
- **Draw calls**: ~40-60 per popup (depends on size)
- **Memory**: Minimal (one texture, ~2KB)
- **GPU load**: Low (small textures, simple blits)

### Legacy 9-Slice Mode
- **Draw calls**: 9 per popup
- **Memory**: Minimal (one texture)
- **GPU load**: Low

**Conclusion**: Both modes are performant. Tile sheet mode uses more draw calls but they're batched by SpriteBatch, making the overhead negligible.

## Testing

### Verify Tile Sheet Rendering

1. Run game and trigger map transition
2. Popup should appear with wood frame
3. Check that corners are crisp (not stretched)
4. Check that edges have repeating pattern
5. Verify no scaling artifacts

### Verify Backwards Compatibility

1. Create a legacy outline definition (no `Type` field)
2. Should render using 9-slice mode
3. No errors or warnings in log

## Future Enhancements

- [ ] Tile caching for improved performance
- [ ] Support for animated tiles (pokeemerald doesn't have this, but we could)
- [ ] Custom tile arrangements per style
- [ ] Runtime switching between render modes
- [ ] Tile sheet editor/preview tool

## Migration Guide

### For Existing Popups

**No action required!** Legacy popups will continue to work using 9-slice rendering.

### For New Popups

1. Extract from pokeemerald using `porycon --extract-popups`
2. Manifests are automatically created with correct format
3. Will automatically use tile sheet rendering

### For Custom Popups

Create a tile sheet manifest:

```json
{
  "Id": "custom",
  "DisplayName": "Custom Style",
  "Type": "TileSheet",
  "TexturePath": "Graphics/Maps/Popups/Outlines/custom_outline.png",
  "TileWidth": 8,
  "TileHeight": 8,
  "TileCount": 30,
  "Tiles": [ /* ... 30 tile definitions ... */ ],
  "TileUsage": { /* ... tile usage mapping ... */ }
}
```

Tile sheet texture should be 80×24 pixels (10×3 tiles of 8×8 each).

## Benefits Summary

1. **GBA Accuracy**: Pixel-perfect match to pokeemerald
2. **Backwards Compatible**: Legacy popups still work
3. **Flexible**: Supports both rendering modes
4. **Documented**: Comprehensive docs for maintenance
5. **Extensible**: Easy to add new styles
6. **Performant**: Minimal overhead for both modes



