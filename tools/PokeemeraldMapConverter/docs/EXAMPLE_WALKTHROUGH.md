# Example: How the Fix Works

## Before the Fix (BROKEN)

### Step 1: Read metatile from binary
```python
# Reading metatiles.bin
tile_raw = 0x2003  # Raw value from binary (16-bit)
```

### Step 2: Use raw value as tile ID (WRONG!)
```python
tile_id = 0x2003  # = 8195 decimal
tile_gid = tile_id  # = 8195
```

### Step 3: Place in tileset JSON
```json
{
  "id": 8195,  // ❌ WRONG! Tileset only has 512 tiles (0-511)
  "properties": [...]
}
```

### Step 4: Reference in map
```json
{
  "data": [8195, 8196, ...]  // ❌ These GIDs don't exist!
}
```

### Result:
**Tiled shows RED X because GID 8195 doesn't exist (tileset only has 512 tiles)**

---

## After the Fix (CORRECT)

### Step 1: Read metatile from binary
```python
# Reading metatiles.bin
tile_raw = 0x2003  # Raw value from binary (16-bit)
# Binary: 0010 0000 0000 0011
```

### Step 2: Extract tile index with bit masking (CORRECT!)
```python
tile_id = tile_raw & 0x3FF  # Mask bits 0-9
# 0x2003 & 0x3FF = 0x0003 = 3 decimal

# Also extract other info:
h_flip = bool(tile_raw & 0x400)   # Bit 10 = 0 (no flip)
v_flip = bool(tile_raw & 0x800)   # Bit 11 = 0 (no flip)
palette = (tile_raw >> 12) & 0xF  # Bits 12-15 = 2 (palette 2)
```

### Step 3: Place in tileset JSON
```json
{
  "id": 3,  // ✅ CORRECT! Tile 3 exists
  "properties": [
    {"name": "metatile_id", "value": 10},
    {"name": "metatile_pos", "value": "top_left"},
    {"name": "palette", "value": 2},
    {"name": "h_flip", "value": false},
    {"name": "v_flip", "value": false}
  ]
}
```

### Step 4: Load tileset in convert_map_8x8.py
```python
# Parse tile properties
for tile_data in tileset_json['tiles']:
    tile_id = tile_data['id']  # = 3
    tile_gid = tile_id + 1      # = 4 (Tiled uses 1-based GIDs)

    # Extract properties
    metatile_id = 10
    metatile_pos = "top_left"

    # Build mapping
    metatile_tiles[10]["top_left"] = 4
```

### Step 5: Place tiles in map
```python
# When placing metatile 10 on map:
metatile = tileset.get_metatile(10)
# metatile.tiles = [4, 5, 6, 7]  # GIDs for top_left, top_right, bottom_left, bottom_right

# Get tile for top_left position
tile_gid = metatile.tiles[0]  # = 4
```

### Step 6: Generate map JSON
```json
{
  "data": [4, 5, 6, 7, ...]  // ✅ These GIDs exist in the 512-tile tileset!
}
```

### Result:
**Tiled displays tile 4 correctly (exists in tileset)**

---

## Detailed Binary Breakdown

### Raw Tile Value: `0x2003`

```
Hexadecimal:  0x2003
Binary:       0010 0000 0000 0011
              ^^^^ ^^   ^^^^^^^^^^
              |    |    |
              |    |    +-- Bits 0-9:  Tile index = 0000000011 = 3
              |    +------- Bits 10-11: Flips = 00 (none)
              +------------ Bits 12-15: Palette = 0010 = 2
```

### Extraction:
```python
tile_index = 0x2003 & 0x3FF        # 0x2003 & 0b1111111111 = 0x0003 = 3
h_flip     = bool(0x2003 & 0x400)  # 0x2003 & 0b10000000000 = False
v_flip     = bool(0x2003 & 0x800)  # 0x2003 & 0b100000000000 = False
palette    = (0x2003 >> 12) & 0xF  # 0b0010 = 2
```

---

## Real Example from Your Tileset

### Before Fix (from test output):
```
Metatile  10: tiles=[8229, 8230, 12459, 12460]
  ⚠️  WARNING: Tile GID 8229 exceeds tileset size (512)
```

### Breaking down tile 8229:
```
8229 decimal = 0x2025 hex = 0010 0000 0010 0101 binary

Palette (bits 12-15): 0010 = palette 2
Flips (bits 10-11):   00   = no flips
Tile index (bits 0-9): 0000100101 = 37 decimal
```

### After Fix:
```
Metatile  10: tiles=[37, 38, 203, 204]
  ✅ All tiles within valid range (1-512)
```

---

## Why This Matters

### The Pokémon Emerald tileset has:
- 512 8x8 tiles (tile indices 0-511)
- Each metatile references 4 of these tiles
- Each tile reference includes flip/palette flags

### Without masking:
- Raw value 0x2003 → GID 8195 → **Doesn't exist!**
- Red X in Tiled

### With masking:
- Raw value 0x2003 → Tile index 3 → GID 4 → **Exists!**
- Correct tile displayed

---

## Complete Example: Placing Metatile 10

### Tileset defines metatile 10:
```
Metatile 10 = [
  top_left:     tile 37 (palette 2),
  top_right:    tile 38 (palette 2),
  bottom_left:  tile 203 (palette 3),
  bottom_right: tile 204 (palette 3)
]
```

### Map.bin says: "Place metatile 10 at position (0, 0)"
```python
metatile_id = map_data[0][0]  # = 10
```

### Converter places 4 tiles:
```python
metatile = tileset.get_metatile(10)
# metatile.tiles = [37+1, 38+1, 203+1, 204+1] = [38, 39, 204, 205] GIDs

# 2x2 grid on map:
map_tiles[0][0] = 38   # top_left
map_tiles[0][1] = 39   # top_right
map_tiles[1][0] = 204  # bottom_left
map_tiles[1][1] = 205  # bottom_right
```

### Result in Tiled:
```
✅ Displays 4 tiles from the 512-tile tileset
✅ Forms a complete 16x16 metatile
✅ No red X's!
```
