# Porycon Per-Tileset Refactoring Checklist

## Quick Reference: Code to Change

### File 1: converter.py

#### Change 1: Remove tile tracking (Lines 217-221)
**DELETE**:
```python
# Track used tiles for tileset building
used_primary_tiles = set()
used_secondary_tiles = set()
used_primary_tiles_with_palettes = set()  # (tile_id, palette_index)
used_secondary_tiles_with_palettes = set()  # (tile_id, palette_index)
```

**KEEP**: Palette tracking arrays (lines 223-226) - these are still needed for remapping

---

#### Change 2: Remove tile collection (Lines 284-354)
**DELETE** the helper function and all calls to it:
```python
def add_used_tile_with_palette(tile_id: int, palette_index: int, tileset_name_for_tile: str):
    """Add (tile_id, palette) to the correct used_tiles set..."""
    # [Lines 284-305] - DELETE THIS ENTIRE FUNCTION
```

**DELETE** all calls (lines 322, 338, 354):
```python
add_used_tile_with_palette(tile_id, palette_index, tileset_name)  # DELETE
```

---

#### Change 3: Remove tileset builder registration (Lines 391-403)
**DELETE**:
```python
# Record used tiles (for backward compatibility)
self.tileset_builder.add_tiles(primary_tileset, list(used_primary_tiles))
self.tileset_builder.add_tiles(secondary_tileset, list(used_secondary_tiles))

# Record used tiles with palettes (for palette-aware tileset building)
if used_primary_tiles_with_palettes:
    unique_palettes = set(p[1] for p in used_primary_tiles_with_palettes)
    logger.debug(f"Recording {len(used_primary_tiles_with_palettes)} tiles...")
if used_secondary_tiles_with_palettes:
    unique_palettes = set(p[1] for p in used_secondary_tiles_with_palettes)
    logger.debug(f"Recording {len(used_secondary_tiles_with_palettes)} tiles...")

self.tileset_builder.add_tiles_with_palettes(primary_tileset, list(used_primary_tiles_with_palettes))
self.tileset_builder.add_tiles_with_palettes(secondary_tileset, list(used_secondary_tiles_with_palettes))
```

**No replacement needed** - tilesets will be built before map conversion

---

#### Change 4: Update tileset references (Lines 493-506)
**REPLACE**:
```python
# Primary tileset
tilesets.append({
    "firstgid": first_gid,
    "source": self.tileset_builder.get_tileset_path(primary_tileset, region)
})

# Secondary tileset (if different and used)
if secondary_tileset != primary_tileset and used_secondary_tiles:
    # Calculate firstgid for secondary (after primary)
    primary_tilecount = len(used_primary_tiles) if used_primary_tiles else 1
    first_gid += primary_tilecount
    tilesets.append({
        "firstgid": first_gid,
        "source": self.tileset_builder.get_tileset_path(secondary_tileset, region)
    })
```

**WITH**:
```python
# Primary tileset (always include)
tilesets.append({
    "firstgid": 1,
    "source": self.tileset_builder.get_tileset_path(primary_tileset, region)
})

# Secondary tileset (if different from primary)
if secondary_tileset != primary_tileset:
    # firstgid = 513 (after 512 primary tiles)
    # This matches pokeemerald's VRAM layout
    tilesets.append({
        "firstgid": 513,
        "source": self.tileset_builder.get_tileset_path(secondary_tileset, region)
    })
```

**Why**: We always include both tilesets (if different), with fixed firstgid values matching pokeemerald structure

---

#### Change 5: Remove or refactor _create_tileset_for_map (Lines 1168-1317)

**Option A (Recommended): DELETE ENTIRE METHOD**
```python
def _create_tileset_for_map(...):  # Lines 1168-1317
    # DELETE ALL 150 LINES
```

**And UPDATE the call site** (around line 1890):
```python
# OLD:
tileset_info = self._create_tileset_for_map(
    map_id, region, used_metatiles, metatile_to_gid, used_gids,
    tileset_data, tile_id_to_gids, metatile_composition
)

# NEW:
# No tileset creation needed - they're pre-built
# Just get tileset names from tileset_data
primary_tileset = tileset_data["primary_tileset"]
secondary_tileset = tileset_data["secondary_tileset"]
map_name = f"{primary_tileset.lower()}_{secondary_tileset.lower()}"
```

**Option B: Keep for metatile animations** (if animations need per-map handling)
- Refactor to only handle animations, not tileset building
- This requires deeper analysis of animation requirements

---

### File 2: __main__.py

#### Change 1: Add tileset building BEFORE maps (Insert after line 82)

**INSERT** after `converter = MapConverter(...)`:
```python
# Build all tilesets BEFORE converting maps
logger.info("Discovering tilesets...")
# Get unique tileset names from layouts
all_tileset_names = set()
for layout_id, layout in layouts.items():
    if 'tileset_primary' in layout:
        all_tileset_names.add(layout['tileset_primary'])
    if 'tileset_secondary' in layout:
        all_tileset_names.add(layout['tileset_secondary'])

logger.info(f"Found {len(all_tileset_names)} unique tilesets")

# Build each tileset once with ALL tiles
logger.info("Building tilesets...")
tileset_region = args.region if args.region else "hoenn"

for tileset_name in sorted(all_tileset_names):
    try:
        # Load complete tileset (all metatiles)
        converter.tileset_builder.load_complete_tileset(tileset_name)

        # Build tileset image and JSON
        converter.tileset_builder.create_tiled_tileset(
            tileset_name,
            str(output_dir),
            tileset_region
        )
        logger.info(f"  Built {tileset_name}")
    except Exception as e:
        logger.error(f"  Failed to build {tileset_name}: {e}")

logger.info(f"Built {len(all_tileset_names)} complete tilesets")
```

---

#### Change 2: Remove old tileset building (DELETE lines 272-337)

**DELETE** entire section:
```python
# Build tilesets and collect tile mappings (parallelized)
logger.info("Building tilesets...")
tile_mappings = {}  # tileset_name -> {(old_tile_id, palette_index): new_tile_id}
# ... [Lines 272-337] ...
# DELETE ALL OF THIS
```

**Why**: Tilesets are now built BEFORE map conversion, not after

---

### File 3: tileset_builder.py

#### Change 1: Refactor data structures (Lines 23-28)

**REPLACE**:
```python
def __init__(self, input_dir: str):
    self.input_dir = Path(input_dir)
    self.used_tiles: Dict[str, Set[int]] = {}  # tileset_name -> set of tile IDs
    self.used_tiles_with_palettes: Dict[str, Set[Tuple[int, int]]] = {}
    self.tileset_info: Dict[str, Dict] = {}  # tileset_name -> tileset metadata
    self.animation_scanner = AnimationScanner(input_dir)
```

**WITH**:
```python
def __init__(self, input_dir: str):
    self.input_dir = Path(input_dir)
    # Store complete tileset data (all tiles, not just used ones)
    self.tilesets: Dict[str, Dict[str, Any]] = {}  # tileset_name -> complete tileset data
    self.animation_scanner = AnimationScanner(input_dir)
```

---

#### Change 2: Add load_complete_tileset method (NEW)

**ADD** after `__init__`:
```python
def load_complete_tileset(self, tileset_name: str):
    """
    Load complete tileset with ALL metatiles and tiles.

    This replaces the old "used tiles only" approach.
    """
    from .metatile_loader import load_complete_metatiles

    # Load ALL metatiles from this tileset
    tileset_data = load_complete_metatiles(tileset_name, self.input_dir)

    # Store for later use
    self.tilesets[tileset_name] = tileset_data

    logger.info(f"Loaded {tileset_name}: {tileset_data['metatile_count']} metatiles, "
                f"{len(tileset_data['all_tiles_with_palettes'])} tile+palette combinations")
```

---

#### Change 3: Remove old methods

**DELETE**:
```python
def add_tiles(self, tileset_name: str, tile_ids: List[int]):
    # Lines 30-34 - DELETE

def add_tiles_with_palettes(self, tileset_name: str, tile_palette_pairs: List[Tuple[int, int]]):
    # Lines 36-46 - DELETE
```

---

#### Change 4: Update build_tileset_image (Lines 74-260)

**REPLACE** (Lines 119-140):
```python
# Get used tiles with palettes, or fall back to tiles without palettes
used_with_palettes = sorted(self.used_tiles_with_palettes.get(tileset_name, set()))
if not used_with_palettes:
    # ... fallback logic ...
    used_tiles = sorted(self.used_tiles.get(tileset_name, set()))
    used_with_palettes = [(tile_id, 0) for tile_id in used_tiles]
    # ... error logging ...

if not used_with_palettes:
    # No tiles used, return empty
    image = Image.new('RGBA', (tile_size, tile_size), (0, 0, 0, 0))
    return image, {}
```

**WITH**:
```python
# Get complete tileset data (all tiles)
if tileset_name not in self.tilesets:
    logger.error(f"Tileset {tileset_name} not loaded! Call load_complete_tileset() first.")
    # Return empty tileset
    image = Image.new('RGBA', (tile_size, tile_size), (0, 0, 0, 0))
    return image, {}

tileset_data = self.tilesets[tileset_name]
all_tiles_with_palettes = sorted(tileset_data['all_tiles_with_palettes'])

if not all_tiles_with_palettes:
    logger.warning(f"Tileset {tileset_name} has no tiles!")
    image = Image.new('RGBA', (tile_size, tile_size), (0, 0, 0, 0))
    return image, {}

# Use ALL tiles, not just used ones
used_with_palettes = all_tiles_with_palettes
```

**Why**: Build from ALL tiles in the tileset, not just those used by maps

---

### File 4: metatile_loader.py (NEW FILE)

**CREATE** new file: `/mnt/c/Users/nate0/RiderProjects/PokeSharp/porycon/porycon/metatile_loader.py`

```python
"""
Complete tileset loading - loads ALL metatiles and tiles from a tileset.
"""

from pathlib import Path
from typing import Dict, Set, Tuple, Any, List
from .utils import TilesetPathResolver
from .metatile import load_metatiles, load_metatile_attributes
from .logging_config import get_logger

logger = get_logger('metatile_loader')


def load_complete_metatiles(
    tileset_name: str,
    input_dir: Path
) -> Dict[str, Any]:
    """
    Load complete tileset with ALL metatiles and tiles.

    Args:
        tileset_name: Name of tileset (e.g., "General", "Mauville")
        input_dir: Path to pokeemerald root

    Returns:
        {
            "name": str,
            "metatile_count": int,
            "all_metatiles": List[Tuple[int, int, int]],  # All metatiles from .bin
            "all_tiles_with_palettes": Set[Tuple[int, int]],  # All (tile_id, palette)
            "max_metatile_id": int,
            "is_primary": bool,
        }
    """
    resolver = TilesetPathResolver(input_dir)
    result = resolver.find_tileset_path(tileset_name)

    if not result:
        logger.error(f"Tileset {tileset_name} not found!")
        return {
            "name": tileset_name,
            "metatile_count": 0,
            "all_metatiles": [],
            "all_tiles_with_palettes": set(),
            "max_metatile_id": 0,
            "is_primary": False,
        }

    is_primary, tileset_path = result

    # Load metatiles.bin (contains ALL metatiles in this tileset)
    metatiles_bin = tileset_path / "metatiles.bin"
    if not metatiles_bin.exists():
        logger.error(f"metatiles.bin not found for {tileset_name}")
        return {
            "name": tileset_name,
            "metatile_count": 0,
            "all_metatiles": [],
            "all_tiles_with_palettes": set(),
            "max_metatile_id": 0,
            "is_primary": is_primary,
        }

    # Load ALL metatiles with attributes
    all_metatiles = load_metatiles(str(metatiles_bin))
    attributes = load_metatile_attributes(str(tileset_path / "metatile_attributes.bin"))

    # Extract ALL unique (tile_id, palette) pairs from ALL metatiles
    all_tiles_with_palettes: Set[Tuple[int, int]] = set()

    for tile_id, palette_id, v_flip in all_metatiles:
        # Each metatile entry is (tile_id, palette_id, v_flip)
        all_tiles_with_palettes.add((tile_id, palette_id))

    logger.info(f"Loaded {tileset_name}: {len(all_metatiles)} metatile entries, "
                f"{len(all_tiles_with_palettes)} unique tile+palette pairs")

    return {
        "name": tileset_name,
        "metatile_count": len(all_metatiles) // 8,  # 8 tiles per metatile
        "all_metatiles": all_metatiles,
        "all_tiles_with_palettes": all_tiles_with_palettes,
        "max_metatile_id": (len(all_metatiles) // 8) - 1,
        "is_primary": is_primary,
    }
```

---

## Step-by-Step Implementation Order

### Step 1: Create Infrastructure
- [ ] Create `metatile_loader.py` with `load_complete_metatiles()`
- [ ] Test loading General tileset
- [ ] Verify ALL tiles extracted (should be ~8000 pairs)

### Step 2: Refactor TilesetBuilder
- [ ] Update `__init__` to use `self.tilesets` dict
- [ ] Add `load_complete_tileset()` method
- [ ] Remove `add_tiles()` and `add_tiles_with_palettes()`
- [ ] Update `build_tileset_image()` to use `all_tiles_with_palettes`
- [ ] Test building General tileset

### Step 3: Update Main Flow
- [ ] Add tileset building BEFORE maps in `__main__.py`
- [ ] Remove old tileset building code (lines 272-337)
- [ ] Test that tilesets build before maps

### Step 4: Clean Up Converter
- [ ] Remove tile tracking code (lines 217-221)
- [ ] Remove `add_used_tile_with_palette()` function
- [ ] Remove tileset builder calls (lines 391-403)
- [ ] Update tileset references (lines 493-506)
- [ ] Delete or refactor `_create_tileset_for_map()`
- [ ] Test map conversion

### Step 5: Validate
- [ ] Run full conversion on pokeemerald
- [ ] Verify ~20 tileset files created (not 300)
- [ ] Verify each tileset has ~8000 tiles
- [ ] Verify Sprites/TileAnimations exported
- [ ] Open maps in Tiled and verify all tiles available

---

## Testing Checklist

### Unit Tests
- [ ] `test_load_complete_metatiles()` - Loads all metatiles
- [ ] `test_tileset_builder_complete()` - Builds with all tiles
- [ ] `test_tileset_count()` - Correct number of unique tilesets

### Integration Tests
- [ ] Convert 5 maps, verify same tileset referenced
- [ ] Check tileset file count (should be ~20, not 300)
- [ ] Verify tile count in General.json (~8000)
- [ ] Confirm Sprites/TileAnimations exported once per tileset

### Visual Tests
- [ ] Open Route 101 in Tiled
- [ ] Verify all tiles from General tileset available
- [ ] Edit map - add tiles that weren't in original map
- [ ] Verify no "missing tile" errors

---

## Rollback Plan

If refactoring fails:

1. **Git branch**: Create `feature/per-tileset-gen` branch
2. **Keep main working**: Don't commit to main until validated
3. **Rollback command**: `git checkout main` (if issues found)
4. **Incremental commits**: Commit after each step passes tests

---

## Success Criteria

- ✓ Tilesets built exactly once (before maps)
- ✓ ~20 tileset files created (not 300)
- ✓ Each tileset contains ALL tiles (~8000 per tileset)
- ✓ Maps reference shared tileset files
- ✓ Sprites/TileAnimations exported once per tileset
- ✓ Maps load correctly in Tiled
- ✓ All tiles available for editing in Tiled
- ✓ File size reduced by 40-60%
- ✓ Conversion speed improved by 20-30%

---

## Time Estimates

| Step | Task | Estimate |
|------|------|----------|
| 1 | Create metatile_loader.py | 2-3 hours |
| 2 | Refactor TilesetBuilder | 2-3 hours |
| 3 | Update main flow | 1-2 hours |
| 4 | Clean up converter | 2-3 hours |
| 5 | Testing and validation | 2-3 hours |
| 6 | Documentation | 1 hour |
| **Total** | | **10-15 hours** |

---

## Notes

- Keep commits small and focused
- Test after each major change
- Use debug logging to verify tile counts
- Compare output against current version
- Document any deviations from plan
