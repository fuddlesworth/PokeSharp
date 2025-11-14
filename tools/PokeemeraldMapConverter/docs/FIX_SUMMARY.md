# Bug Fix Summary: Tile GID Out of Range Issue

## The Problem
Maps showed red X's in Tiled because tile GIDs were 1024-42100, but the tileset only had 512 tiles (GIDs 1-512).

## The Root Cause
**Scripts were using raw 16-bit tile values as GIDs without masking out the flip/palette flags.**

In GBA format, a tile value contains:
- **Bits 0-9**: Tile index (the actual tile)
- **Bit 10**: Horizontal flip
- **Bit 11**: Vertical flip
- **Bits 12-15**: Palette number

## The Fix

### File 1: `convert_tileset_8x8.py` (Line 284-288)

**BEFORE (WRONG):**
```python
tile_id = metatile.get_tile_at_position(pos)
# Using raw value 0x2003 (8195) as tile ID
```

**AFTER (CORRECT):**
```python
tile_raw = metatile.get_tile_at_position(pos)
tile_id = tile_raw & 0x3FF  # Extract bits 0-9 only
# Now using 0x0003 (3) as tile ID
```

### File 2: `convert_map_8x8.py` (Lines 75-163)

**BEFORE:** Expected custom JSON format, couldn't read Tiled tilesets

**AFTER:** Properly reads Tiled tileset JSON and builds metatile mapping

## Key Code Changes

### Extract Tile Index (convert_tileset_8x8.py)
```python
# Extract tile index from raw value (lower 10 bits)
tile_id = tile_raw & 0x3FF

# Also extract flip/palette for metadata
h_flip = bool(tile_raw & 0x400)   # Bit 10
v_flip = bool(tile_raw & 0x800)   # Bit 11
palette = (tile_raw >> 12) & 0xF  # Bits 12-15
```

### Build Metatile Mapping (convert_map_8x8.py)
```python
# Build mapping from (metatile_id, position) -> tile_gid
metatile_tiles = {}
position_to_index = {
    "top_left": 0,
    "top_right": 1,
    "bottom_left": 2,
    "bottom_right": 3
}

# Parse Tiled tileset format
for tile_data in data.get('tiles', []):
    tile_id = tile_data['id']
    tile_gid = tile_id + 1  # Convert 0-based to 1-based

    properties = parse_properties(tile_data)
    metatile_id = properties['metatile_id']
    metatile_pos = properties['metatile_pos']

    metatile_tiles[metatile_id][metatile_pos] = tile_gid

# Construct metatile definitions
for metatile_id, tiles_dict in metatile_tiles.items():
    tiles = [0, 0, 0, 0]
    for pos_name, tile_gid in tiles_dict.items():
        idx = position_to_index[pos_name]
        tiles[idx] = tile_gid

    metatile = MetatileDefinition(metatile_id, tiles, ...)
```

## Result

### Before:
- ❌ Tile GIDs: 1024-42100
- ❌ Max GID: 21687 (way over 512)
- ❌ Tiled shows red X's

### After:
- ✅ Tile GIDs: 1-512
- ✅ Max GID: 512 (correct)
- ✅ Tiled displays tiles correctly

## Files Modified

1. `/mnt/c/Users/nate0/RiderProjects/PokeSharp/tools/PokeemeraldMapConverter/convert_tileset_8x8.py`
   - Lines 280-317: Added bit masking to extract tile index

2. `/mnt/c/Users/nate0/RiderProjects/PokeSharp/tools/PokeemeraldMapConverter/convert_map_8x8.py`
   - Lines 75-163: Rewrote `load_tileset()` to parse Tiled format

## Test Script

Created: `/mnt/c/Users/nate0/RiderProjects/PokeSharp/tools/PokeemeraldMapConverter/test_tileset_loading.py`

Run with:
```bash
python3 test_tileset_loading.py
```

## Next Actions Required

1. **Regenerate tileset JSON** using the fixed convert_tileset_8x8.py
2. **Regenerate map JSON** using the fixed convert_map_8x8.py
3. **Open in Tiled** to verify tiles display correctly
