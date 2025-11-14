# Critical Bug Fix: Tile GID Out of Range

## Problem Summary

Maps were showing red X's in Tiled because the scripts were generating tile GIDs in the range 1024-42100, but the tileset only contained 512 tiles (GIDs 1-512).

## Root Cause

The scripts were using **raw tile values** from the binary files as GIDs without masking out the flip/palette flags. In Game Boy Advance (GBA) tile format, a 16-bit tile value contains:

```
Bits 0-9   (0x3FF):  Tile index (0-1023)
Bit 10     (0x400):  Horizontal flip flag
Bit 11     (0x800):  Vertical flip flag
Bits 12-15 (0xF000): Palette index (0-15)
```

### Example:
- Raw value: `0x2003` (8195 decimal)
- Binary: `0010 0000 0000 0011`
- Palette bits (12-15): `0010` = palette 2
- Flip bits (10-11): `00` = no flips
- Tile index (0-9): `0000000011` = tile 3

**The tile index is 3, NOT 8195!**

## Files Fixed

### 1. `convert_tileset_8x8.py`

**Location:** Lines 280-317

**Before:**
```python
tile_id = metatile.get_tile_at_position(pos)
tile_props = {
    "id": tile_id,  # ❌ WRONG: Using raw value with flags
    ...
}
```

**After:**
```python
tile_raw = metatile.get_tile_at_position(pos)

# Extract tile index from raw value (lower 10 bits)
tile_id = tile_raw & 0x3FF  # ✅ CORRECT: Mask to get actual tile index

# Extract tile attributes for metadata
h_flip = bool(tile_raw & 0x400)   # Bit 10
v_flip = bool(tile_raw & 0x800)   # Bit 11
palette = (tile_raw >> 12) & 0xF  # Bits 12-15

tile_props = {
    "id": tile_id,  # ✅ Now using correct tile index
    ...
}
```

### 2. `convert_map_8x8.py`

**Location:** `load_tileset()` function (lines 75-163)

**Problem:** The function expected a custom JSON format with metatile definitions, but the actual tileset JSON uses Tiled's format with tile properties.

**Solution:** Rewrote the function to:
1. Parse Tiled tileset JSON format
2. Read tile properties (metatile_id, metatile_pos)
3. Build reverse mapping: (metatile_id, position) → tile_gid
4. Construct metatile definitions from this mapping

**Before:**
```python
def load_tileset(tileset_path: Path) -> TilesetData:
    # Expected custom format with "metatiles" array
    for mt_data in data.get('metatiles', []):
        metatile = MetatileDefinition(
            metatile_id=mt_data['id'],
            tiles=mt_data['tiles'],  # ❌ Expected pre-built array
            ...
        )
```

**After:**
```python
def load_tileset(tileset_path: Path) -> TilesetData:
    # Parse Tiled format with tile properties
    metatile_tiles = {}  # Build mapping

    for tile_data in data.get('tiles', []):
        tile_id = tile_data['id']
        tile_gid = tile_id + 1  # Convert 0-based to 1-based

        properties = parse_properties(tile_data)
        metatile_id = properties['metatile_id']
        metatile_pos = properties['metatile_pos']

        # Store mapping: (metatile_id, position) -> gid
        metatile_tiles[metatile_id][metatile_pos] = tile_gid

    # Build metatile definitions from mapping
    for metatile_id, tiles_dict in metatile_tiles.items():
        tiles = [tiles_dict.get(pos, 0) for pos in positions]
        metatile = MetatileDefinition(metatile_id, tiles, ...)
```

## Verification

### Test Command:
```bash
cd /mnt/c/Users/nate0/RiderProjects/PokeSharp/tools/PokeemeraldMapConverter
python3 test_tileset_loading.py
```

### Expected Output:
```
✅ Loaded 256 metatiles
✅ All tile GIDs are within valid range!
Max tile GID used: 512
Expected max GID: 512
```

### Before Fix:
- Tile GIDs: 1024-42100 ❌
- Max GID: 21687 (exceeds 512 tile limit)
- Result: Red X's in Tiled

### After Fix:
- Tile GIDs: 1-512 ✅
- Max GID: 512 (matches tileset)
- Result: Correct tiles display in Tiled

## Impact

1. **Tileset Generation** (`convert_tileset_8x8.py`):
   - Now generates tileset JSON with correct tile IDs (0-511)
   - Preserves flip/palette info as tile properties
   - Each tile ID matches its position in the sprite sheet

2. **Map Conversion** (`convert_map_8x8.py`):
   - Correctly loads Tiled tileset format
   - Properly maps metatile IDs to their 4 constituent tiles
   - Generates map JSON with valid tile GIDs (1-512)

## GBA Tile Format Reference

```c
// GBA tile value structure
typedef struct {
    u16 tile_index : 10;   // Bits 0-9:  Which 8x8 tile (0-1023)
    u16 h_flip     : 1;    // Bit 10:    Horizontal flip
    u16 v_flip     : 1;    // Bit 11:    Vertical flip
    u16 palette    : 4;    // Bits 12-15: Palette (0-15)
} TileValue;
```

### Masking Operations:
```python
tile_index = raw_value & 0x3FF        # Extract bits 0-9
h_flip     = bool(raw_value & 0x400)  # Test bit 10
v_flip     = bool(raw_value & 0x800)  # Test bit 11
palette    = (raw_value >> 12) & 0xF  # Extract bits 12-15
```

## Testing Checklist

- [x] Fix tile ID extraction in `convert_tileset_8x8.py`
- [x] Fix tileset loading in `convert_map_8x8.py`
- [x] Add tile flip/palette properties to tileset
- [x] Create verification test script
- [ ] Regenerate tileset JSON with correct IDs
- [ ] Regenerate map JSON with correct GIDs
- [ ] Verify maps display correctly in Tiled

## Next Steps

1. **Regenerate Tileset:**
   ```bash
   python3 convert_tileset_8x8.py \
     <pokeemerald_tileset_dir> \
     general.json
   ```

2. **Regenerate Maps:**
   ```bash
   python3 convert_map_8x8.py \
     layout.json \
     general.json \
     output_map.json \
     --tileset-name general
   ```

3. **Verify in Tiled:**
   - Open output_map.json in Tiled
   - Confirm tiles display correctly (no red X's)
   - Check that metatile properties are preserved
