# Tile Flip Flag Support in Porycon

## Overview

This document describes the tile flip flag infrastructure added to porycon for potential future integration with Tiled's GID flip bit system.

## Background

In Pokemon Emerald's metatile system:
- Each metatile (16x16) is composed of 8 tiles (8x8 each)
- Each individual tile can have horizontal and/or vertical flip flags
- Flip flags are stored in metatiles.bin as part of the tile definition

Currently, porycon renders these flips by applying transformations when compositing tiles into metatiles. This produces correct visual output but doesn't expose the flip information to Tiled.

## Implementation

### Constants (constants.py)

Added Tiled GID flip flag constants:
- `TILED_FLIP_HORIZONTAL = 0x80000000` - Horizontal flip bit
- `TILED_FLIP_VERTICAL = 0x40000000` - Vertical flip bit
- `TILED_FLIP_DIAGONAL = 0x20000000` - Diagonal flip bit (not used for Pokemon)
- `TILED_FLIP_MASK = 0xE0000000` - Combined mask for all flip bits

### Utility Functions (tiled_utils.py)

Created helper functions for working with Tiled GID flip flags:

```python
def apply_tiled_flip_to_gid(gid: int, flip_horizontal: bool, flip_vertical: bool) -> int:
    """Apply Tiled flip bits to a GID."""

def get_base_gid(gid: int) -> int:
    """Get the base GID without flip flags."""

def metatile_flip_to_tiled(flip_flags: int) -> tuple[bool, bool]:
    """Convert metatiles.bin flip flags to Tiled flip booleans."""
```

### Renderer Changes (metatile_renderer.py)

Modified `render_metatile()` and `_render_tile_grid()` to:
1. Track flip flags for all 8 tiles in each metatile
2. Return flip information alongside rendered images
3. **Continue applying flips for correct visual output**

**Return signature change:**
```python
# Before
def render_metatile(...) -> Tuple[Optional[Image.Image], Optional[Image.Image]]:

# After
def render_metatile(...) -> Tuple[Optional[Image.Image], Optional[Image.Image], List[Tuple[int, int]]]:
```

The third return value is a list of `(flip_flags, tile_id)` tuples for all 8 tiles.

## Current Behavior

**Flips are still applied during rendering** to ensure correct visual output. The flip tracking is for metadata and potential future use.

## Future Enhancements

### Option 1: 8x8 Tile Mode

To fully leverage Tiled's GID flip flags, porycon could be enhanced to:
1. Work at the 8x8 tile level instead of 16x16 metatiles
2. Store individual 8x8 tiles in the tileset (without flips)
3. Use Tiled GID flip flags for each tile cell
4. **Benefit**: Massive reduction in tileset size due to tile reuse

**Challenges:**
- Requires major refactor of layer building logic
- Tiled map would be 2x larger in each dimension (cells are 8x8 instead of 16x16)
- Game engine would need to understand 8x8 tile structure

### Option 2: Metadata Export

Export flip information in tile metadata for:
- Debugging tile composition issues
- Tooling that needs to understand source tile structure
- Round-trip conversion (Tiled → metatiles.bin)

### Option 3: Selective Flip Application

Detect when tiles can be deduplicated using flip flags:
- Same base tile used with/without flips → single tile + GID flags
- **Benefit**: Reduced tileset size
- **Challenge**: Complex deduplication logic at 8x8 level while maintaining 16x16 output

## Technical Notes

### Why Flips Must Be Applied

Pokemon Emerald metatiles are rendered as single 16x16 tiles in Tiled. Since Tiled's GID flip flags apply to entire tiles, we cannot use them to flip individual 8x8 sub-tiles within a 16x16 metatile.

Therefore, flips must be baked into the 16x16 metatile rendering for correct visual output.

### Deduplication

The current image-based deduplication happens at the 16x16 metatile level:
- Two metatiles that look visually identical share the same GID
- This works correctly even with different flip combinations

Adding flip tracking doesn't change deduplication behavior, but provides metadata for future optimizations.

## Files Modified

- `porycon/constants.py` - Added Tiled flip constants
- `porycon/tiled_utils.py` - New utility module for GID flip operations
- `porycon/metatile_renderer.py` - Added flip tracking to render methods
- `porycon/metatile_processor.py` - Updated to handle new return signature
- `porycon/converter.py` - Updated to handle new return signature

## Backward Compatibility

All changes are backward compatible:
- Existing deduplication logic unchanged
- Visual output remains identical
- New flip tracking data is optional (can be ignored)
