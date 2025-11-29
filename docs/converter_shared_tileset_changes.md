# Converter.py Changes - Shared Tileset Implementation

## Summary
Modified `converter.py` to use pre-generated shared tilesets instead of creating per-map tilesets, eliminating massive duplication.

## Changes Made

### 1. Added `complete_tileset_generator` Attribute (Line 89)
```python
self.complete_tileset_generator = None  # Set by __main__.py before conversion
```
- This will be set by the main script to provide access to tileset metadata
- Currently not used but reserved for future enhancements

### 2. Modified `convert_map_with_metatiles()` Method (Lines 1890-1920)

**REMOVED:**
- Call to `_create_tileset_for_map()` which created a new tileset for each map

**REPLACED WITH:**
```python
# Use pre-generated shared tilesets instead of creating per-map tilesets
# Get tileset info from tileset_data
primary_tileset_name = primary_tileset.replace('gTileset_', '') if primary_tileset.startswith('gTileset_') else primary_tileset
secondary_tileset_name = secondary_tileset.replace('gTileset_', '') if secondary_tileset.startswith('gTileset_') else secondary_tileset

# Normalize names to lowercase
primary_name = primary_tileset_name.lower()
secondary_name = secondary_tileset_name.lower()

# Get paths to shared tilesets (relative from Maps/region/map_group/map_name.json)
# Maps are at: output/Maps/region/map_group/map_name.json
# Tilesets are at: output/Tilesets/region/tileset_name.json
# Relative path: ../../../Tilesets/region/tileset_name.json
region_normalized = region if region else "hoenn"
primary_path = f"../../../Tilesets/{region_normalized}/{primary_name}.json"
secondary_path = f"../../../Tilesets/{region_normalized}/{secondary_name}.json"

# Register the tileset paths in the registry
self.tileset_registry.register_tileset(
    primary_tileset, secondary_tileset,
    {}, primary_path, secondary_path
)

# Use map_id for the map filename
map_name = sanitize_filename(map_id)

# Create minimal tileset_json with references to external tilesets
tileset_json = {
    "primary_tileset": primary_tileset,
    "secondary_tileset": secondary_tileset
}
```

**What This Does:**
1. Extracts tileset names from the map's tileset data
2. Normalizes names (removes 'gTileset_' prefix, converts to lowercase)
3. Constructs relative paths to shared tilesets in `output/Tilesets/region/`
4. Registers these paths in the tileset registry
5. Creates a minimal tileset_json with just tileset names (not full tileset data)

### 3. Deprecated `_create_tileset_for_map()` Method (Line 1169)

**RENAMED TO:** `_create_tileset_for_map_DEPRECATED()`

**ADDED WARNINGS:**
```python
logger.warning("_create_tileset_for_map_DEPRECATED called - this method should not be used anymore!")
logger.warning("Maps should reference pre-generated shared tilesets instead of creating per-map tilesets.")
```

**WHY:**
- Method kept for reference but should not be called
- If called accidentally, it will log warnings
- Can be fully removed in a future cleanup

### 4. Existing `_create_tiled_map_structure()` Already Compatible

The `_create_tiled_map_structure()` method (lines 1480-1509) already has logic to use external tileset references:

```python
# Use registry to get paths and firstgids for shared tilesets
if primary_tileset and secondary_tileset:
    paths = self.tileset_registry.get_paths(primary_tileset, secondary_tileset)
    firstgids = self.tileset_registry.get_firstgids(primary_tileset, secondary_tileset)

    if paths:
        primary_path, secondary_path = paths
        primary_firstgid, secondary_firstgid = firstgids
    else:
        # Fallback: construct paths manually
        primary_path = f"../../../Tilesets/{region}/{primary_tileset.lower()}.json"
        secondary_path = f"../../../Tilesets/{region}/{secondary_tileset.lower()}.json"
        primary_firstgid, secondary_firstgid = 1, NUM_TILES_IN_PRIMARY_VRAM + 1

    tiled_map["tilesets"] = [
        {"firstgid": primary_firstgid, "source": primary_path},
        {"firstgid": secondary_firstgid, "source": secondary_path}
    ]
```

This creates external tileset references in the Tiled map JSON:
```json
{
  "tilesets": [
    {"firstgid": 1, "source": "../../../Tilesets/hoenn/general.json"},
    {"firstgid": 513, "source": "../../../Tilesets/hoenn/mauville.json"}
  ]
}
```

## Expected Behavior

### Before Changes:
1. Each map called `_create_tileset_for_map()`
2. Created a NEW tileset image and JSON for EACH map
3. Resulted in hundreds of duplicate tilesets
4. Map JSON referenced per-map tilesets: `Tilesets/hoenn/map_name/map_name.json`

### After Changes:
1. Maps no longer create tilesets
2. Maps reference pre-generated shared tilesets from `CompleteTilesetGenerator`
3. Shared tilesets are in: `output/Tilesets/region/tileset_name.json`
4. Map JSON references shared tilesets: `../../../Tilesets/hoenn/general.json`
5. Massive reduction in duplication

## File Structure

```
output/
  Maps/
    hoenn/
      map_group/
        map_name.json      # References shared tilesets via relative path
  Tilesets/
    hoenn/
      general.json         # Shared tileset (generated by CompleteTilesetGenerator)
      general.png
      mauville.json        # Shared tileset
      mauville.png
      # ... other shared tilesets
```

## Next Steps

The converter now expects pre-generated tilesets to exist. The workflow is:

1. `CompleteTilesetGenerator` generates ALL tilesets FIRST
2. `MapConverter` converts maps and references those shared tilesets
3. No per-map tileset creation

## Benefits

- ✅ Eliminates tileset duplication
- ✅ Smaller output size
- ✅ Easier to manage tilesets
- ✅ Tiled Editor can properly share tilesets across maps
- ✅ Faster conversion (no redundant tileset generation)

## Validation

Python syntax validated successfully:
```bash
python3 -m py_compile porycon/porycon/converter.py
# No errors
```
