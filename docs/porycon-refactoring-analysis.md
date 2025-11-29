# Porycon Per-Tileset Generation Refactoring Analysis

## Code Quality Analysis Report

### Summary
- **Overall Quality Score**: 7/10
- **Files Analyzed**: 3 (converter.py, __main__.py, tileset_builder.py)
- **Issues Found**: 1 Critical architectural issue
- **Technical Debt Estimate**: 8-12 hours

---

## Current Architecture Problems

### Critical Issue: Per-Map vs Per-Tileset Generation

**Current Behavior (WRONG):**
1. Each map tracks ONLY the tiles it uses from tilesets
2. Tileset images are built containing ONLY those used tiles
3. Results in duplicate tile data across maps
4. Breaks the tileset reusability model

**Desired Behavior (CORRECT):**
1. Generate ONE complete tileset per tileset NAME (General, Mauville, etc.)
2. Include ALL metatiles/tiles from the source tileset
3. All maps reference the same shared tileset files
4. Sprites/TileAnimations export once per tileset

---

## Exact Problem Locations

### 1. Tile Collection (Lines 217-403 in converter.py)

**File**: `/mnt/c/Users/nate0/RiderProjects/PokeSharp/porycon/porycon/converter.py`

**Problem Code** (Lines 217-221):
```python
# Track used tiles for tileset building
used_primary_tiles = set()
used_secondary_tiles = set()
used_primary_tiles_with_palettes = set()  # (tile_id, palette_index)
used_secondary_tiles_with_palettes = set()  # (tile_id, palette_index)
```

**What's Wrong**:
- Only tracks tiles USED by the current map
- Should track ALL tiles from the tileset definition

**Impact**: Tilesets are incomplete and map-specific

---

### 2. Tileset Builder Registration (Lines 391-403)

**Problem Code**:
```python
# Record used tiles (for backward compatibility)
self.tileset_builder.add_tiles(primary_tileset, list(used_primary_tiles))
self.tileset_builder.add_tiles(secondary_tileset, list(used_secondary_tiles))

# Record used tiles with palettes (for palette-aware tileset building)
self.tileset_builder.add_tiles_with_palettes(primary_tileset, list(used_primary_tiles_with_palettes))
self.tileset_builder.add_tiles_with_palettes(secondary_tileset, list(used_secondary_tiles_with_palettes))
```

**What's Wrong**:
- Passes only used tiles to tileset builder
- Should pass ALL tiles from tileset once at initialization

**Line Numbers**: 391-403

---

### 3. TilesetBuilder.build_tileset_image (Lines 74-260 in tileset_builder.py)

**File**: `/mnt/c/Users/nate0/RiderProjects/PokeSharp/porycon/porycon/tileset_builder.py`

**Problem Code** (Lines 119-131):
```python
# Get used tiles with palettes, or fall back to tiles without palettes
used_with_palettes = sorted(self.used_tiles_with_palettes.get(tileset_name, set()))
if not used_with_palettes:
    # Fallback: use tiles without palette info (assume palette 0)
    used_tiles = sorted(self.used_tiles.get(tileset_name, set()))
    used_with_palettes = [(tile_id, 0) for tile_id in used_tiles]
```

**What's Wrong**:
- Builds image from `used_tiles_with_palettes` set
- Should build from ALL tiles in the tileset definition

**Line Numbers**: 119-131

---

### 4. Per-Map Tileset Creation (Lines 1168-1317 in converter.py)

**Problem Method**: `_create_tileset_for_map()`

**What's Wrong**:
- Creates tileset FOR EACH MAP
- Uses `used_metatiles` from that specific map
- Should be REMOVED or completely refactored

**Line Numbers**: 1168-1317

**Current Signature**:
```python
def _create_tileset_for_map(
    self,
    map_id: str,
    region: str,
    used_metatiles: Dict[Tuple[int, str, int], Tuple[Image.Image, Image.Image]],
    metatile_to_gid: Dict[Tuple[int, str, int, bool], int],
    used_gids: Set[int],
    tileset_data: Dict[str, Any],
    tile_id_to_gids: Dict[Tuple[int, str], List[Tuple[int, Tuple[int, str, int], int]]],
    metatile_composition: Dict[Tuple[int, str, int], List]
) -> Dict[str, Any]:
```

**Should Be**:
- Removed entirely, or
- Refactored to generate ONCE per tileset combination

---

### 5. Main Conversion Loop (Lines 272-337 in __main__.py)

**File**: `/mnt/c/Users/nate0/RiderProjects/PokeSharp/porycon/porycon/__main__.py`

**Problem Code** (Lines 281-315):
```python
# For tileset building, we need the used_tiles from the converter
tileset_names = list(set(converter.tileset_builder.used_tiles.keys()) |
                     set(converter.tileset_builder.used_tiles_with_palettes.keys()))

with ThreadPoolExecutor(max_workers=min(4, len(tileset_names))) as executor:
    for tileset_name in tileset_names:
        future = executor.submit(
            converter.tileset_builder.create_tiled_tileset,
            tileset_name,
            str(output_dir),
            tileset_region
        )
```

**What's Wrong**:
- Builds tilesets AFTER map conversion
- Only includes tiles that were used
- Happens sequentially per map

**Should Be**:
- Build ALL tilesets BEFORE map conversion
- Include ALL tiles from tileset definition
- Build once, reference many times

**Line Numbers**: 272-337

---

## Data Flow Analysis

### Current Flow (BROKEN):
```
1. For each map:
   ├─ Load map data
   ├─ Parse metatiles used by THIS map
   ├─ Collect used tiles (used_primary_tiles, used_secondary_tiles)
   ├─ Register used tiles with TilesetBuilder
   └─ Create per-map tileset with only those tiles

2. After all maps:
   └─ Build tileset images from accumulated used_tiles
      (Still incomplete - only tiles used by at least one map)
```

### Desired Flow (CORRECT):
```
1. Initialize phase:
   ├─ Discover all unique tileset names
   ├─ For each tileset:
   │  ├─ Load complete tileset definition
   │  ├─ Extract ALL metatiles
   │  ├─ Export Sprites/TileAnimations
   │  └─ Build complete tileset image + JSON
   └─ Store tileset registry

2. For each map:
   ├─ Load map data
   ├─ Reference pre-built tilesets by name
   └─ Create map JSON with tileset references
```

---

## Required Changes

### Phase 1: Tileset Discovery and Initialization

**New Method Needed**:
```python
def discover_all_tilesets(input_dir: Path) -> Set[str]:
    """
    Scan pokeemerald data to find all unique tileset names.

    Looks in:
    - data/tilesets/primary/*/
    - data/tilesets/secondary/*/

    Returns: Set of tileset names (e.g., {"General", "Mauville", ...})
    """
```

**Location**: Add to `utils.py` or create new `tileset_discovery.py`

**Line Estimate**: ~50-80 lines

---

### Phase 2: Complete Tileset Loading

**New Method Needed**:
```python
def load_complete_tileset(
    tileset_name: str,
    input_dir: Path
) -> Dict[str, Any]:
    """
    Load ALL metatiles and tiles from a tileset.

    Returns:
    {
        "name": str,
        "all_metatiles": List[Tuple[int, int, int]],  # All metatiles, not just used
        "all_tiles_with_palettes": Set[Tuple[int, int]],  # All (tile_id, palette) pairs
        "max_metatile_id": int,
        "animations": List[Dict],  # Animation definitions
    }
    """
```

**Location**: Add to `tileset_builder.py` or `metatile.py`

**Line Estimate**: ~100-150 lines

---

### Phase 3: Refactor TilesetBuilder

**Changes to `tileset_builder.py`**:

1. **Remove** `used_tiles` and `used_tiles_with_palettes` sets
2. **Add** complete tileset storage:
   ```python
   class TilesetBuilder:
       def __init__(self, input_dir: str):
           self.input_dir = Path(input_dir)
           self.tilesets: Dict[str, Dict[str, Any]] = {}  # tileset_name -> complete data
           self.animation_scanner = AnimationScanner(input_dir)
   ```

3. **Replace** `add_tiles()` and `add_tiles_with_palettes()` with:
   ```python
   def load_tileset(self, tileset_name: str):
       """Load and store complete tileset definition."""
       tileset_data = load_complete_tileset(tileset_name, self.input_dir)
       self.tilesets[tileset_name] = tileset_data
   ```

4. **Update** `build_tileset_image()` to use ALL tiles:
   ```python
   def build_tileset_image(self, tileset_name: str, ...):
       tileset_data = self.tilesets[tileset_name]
       all_tiles_with_palettes = tileset_data["all_tiles_with_palettes"]
       # Build image from ALL tiles, not just used ones
   ```

**Line Changes**: ~200-300 lines modified/added

---

### Phase 4: Refactor Main Conversion Flow

**Changes to `__main__.py`**:

**BEFORE map conversion** (insert after line 82):
```python
# Discover and build all tilesets BEFORE converting maps
logger.info("Discovering tilesets...")
all_tilesets = discover_all_tilesets(input_dir)
logger.info(f"Found {len(all_tilesets)} unique tilesets")

logger.info("Building tilesets...")
for tileset_name in all_tilesets:
    converter.tileset_builder.load_tileset(tileset_name)
    converter.tileset_builder.create_tiled_tileset(
        tileset_name, str(output_dir), tileset_region
    )
logger.info(f"Built {len(all_tilesets)} tilesets")
```

**REMOVE** lines 272-337 (old tileset building code)

**Line Changes**: ~80 lines removed, ~20 lines added

---

### Phase 5: Update Map Converter

**Changes to `converter.py`**:

1. **REMOVE** tile collection code (lines 217-403):
   - Delete `used_primary_tiles`, `used_secondary_tiles`, etc.
   - Delete `add_tiles()` calls

2. **REMOVE or REFACTOR** `_create_tileset_for_map()`:
   - Option A: Delete entirely (maps reference pre-built tilesets)
   - Option B: Refactor to ONLY create map-tileset associations

3. **UPDATE** tileset references in maps:
   ```python
   # OLD (lines 493-506):
   if secondary_tileset != primary_tileset and used_secondary_tiles:
       # ...

   # NEW:
   # Always reference both tilesets if they exist
   tilesets.append({
       "firstgid": 1,
       "source": f"../../Tilesets/{region}/{primary_tileset.lower()}.json"
   })
   if secondary_tileset != primary_tileset:
       tilesets.append({
           "firstgid": 513,  # After 512 primary tiles
           "source": f"../../Tilesets/{region}/{secondary_tileset.lower()}.json"
       })
   ```

**Line Changes**: ~200 lines removed, ~50 lines added

---

### Phase 6: Animation Export Location

**Current**: Animations exported during `create_tiled_tileset()` (line 352-358)

**Problem**: This is CORRECT location, but happens too late

**Fix**: Keep export in `create_tiled_tileset()`, but ensure it runs ONCE per tileset (not per map)

**Changes**: No code changes needed if Phase 4 is implemented correctly

---

## Refactoring Plan Summary

### Step 1: Add Tileset Discovery (2-3 hours)
- [ ] Create `discover_all_tilesets()` in utils.py
- [ ] Test discovery on pokeemerald data
- [ ] Verify all tilesets found (General, Mauville, etc.)

### Step 2: Add Complete Tileset Loading (3-4 hours)
- [ ] Create `load_complete_tileset()` method
- [ ] Load ALL metatiles from metatiles.bin
- [ ] Extract ALL tiles with palettes
- [ ] Test on sample tileset (General)

### Step 3: Refactor TilesetBuilder (2-3 hours)
- [ ] Remove `used_tiles` sets
- [ ] Add `tilesets` dict for complete data
- [ ] Update `build_tileset_image()` to use ALL tiles
- [ ] Test tileset building

### Step 4: Update Main Flow (1-2 hours)
- [ ] Move tileset building BEFORE map conversion
- [ ] Remove old tileset building code
- [ ] Test complete workflow

### Step 5: Clean Up Map Converter (2-3 hours)
- [ ] Remove tile tracking code
- [ ] Update tileset references in maps
- [ ] Remove/refactor `_create_tileset_for_map()`
- [ ] Test map conversion

### Step 6: Validation (1-2 hours)
- [ ] Verify each tileset built only once
- [ ] Verify Sprites/TileAnimations exported
- [ ] Verify maps reference shared tilesets
- [ ] Check file deduplication

**Total Estimate**: 11-17 hours

---

## Code Smells Detected

### 1. God Method: `convert_map()`
- **Location**: converter.py (not shown, but inferred from structure)
- **Issue**: Does too many things (load, process, build, export)
- **Recommendation**: Split into smaller methods

### 2. Duplicate Logic: Tile Routing
- **Location**: Lines 276-305 (converter.py)
- **Issue**: `get_tile_tileset()` and `add_used_tile_with_palette()` duplicate logic
- **Recommendation**: Consolidate into metatile_processor

### 3. Feature Envy: TilesetBuilder
- **Location**: Lines 391-403 (converter.py)
- **Issue**: Converter reaches into TilesetBuilder internals
- **Recommendation**: Use proper encapsulation

### 4. Magic Numbers: Tileset Boundaries
- **Location**: Throughout converter.py
- **Examples**: `512`, `NUM_TILES_IN_PRIMARY_VRAM`
- **Recommendation**: Use named constants with documentation

### 5. Dead Code: "Backward Compatibility"
- **Location**: Line 390-392 (converter.py)
- **Comment**: "# Record used tiles (for backward compatibility)"
- **Recommendation**: Remove if not needed

---

## Positive Findings

✓ **Good Separation**: TilesetBuilder is already a separate class
✓ **Logging**: Comprehensive debug logging throughout
✓ **Error Handling**: Validation of tile IDs (lines 361-388)
✓ **Documentation**: Clear comments explaining pokeemerald structure
✓ **Parallel Processing**: ThreadPoolExecutor for tileset building

---

## Risk Assessment

### High Risk
- **Used tiles logic** is deeply embedded in conversion flow
- Removing it may break map rendering if not careful
- Need thorough testing after refactoring

### Medium Risk
- **Animation export** timing must be perfect
- firstgid calculations must remain correct
- Palette handling must not regress

### Low Risk
- Tileset discovery is additive (won't break existing code)
- TilesetBuilder refactoring is isolated

---

## Testing Strategy

### Unit Tests Needed
1. `test_discover_all_tilesets()` - Finds all tilesets
2. `test_load_complete_tileset()` - Loads all metatiles
3. `test_build_complete_tileset()` - Includes all tiles
4. `test_tileset_deduplication()` - Only one file per tileset

### Integration Tests Needed
1. Convert 5 maps, verify they reference same tileset
2. Check Sprites/TileAnimations exported only once
3. Verify tile count matches source metatiles.bin
4. Confirm no tile data duplication

### Validation Tests
1. Load converted map in Tiled - should render correctly
2. Edit map in Tiled - all tiles should be available
3. Compare file sizes before/after (should be smaller)

---

## Files to Create/Modify

### New Files
- `/mnt/c/Users/nate0/RiderProjects/PokeSharp/porycon/porycon/tileset_discovery.py` (optional)

### Modified Files
1. `/mnt/c/Users/nate0/RiderProjects/PokeSharp/porycon/porycon/converter.py`
   - Remove lines 217-221 (tile tracking)
   - Remove lines 391-403 (add_tiles calls)
   - Remove/refactor lines 1168-1317 (_create_tileset_for_map)
   - Update lines 493-506 (tileset references)

2. `/mnt/c/Users/nate0/RiderProjects/PokeSharp/porycon/porycon/__main__.py`
   - Add tileset building before line 90
   - Remove lines 272-337 (old tileset building)

3. `/mnt/c/Users/nate0/RiderProjects/PokeSharp/porycon/porycon/tileset_builder.py`
   - Refactor lines 25-26 (change data structures)
   - Add `load_tileset()` method
   - Update lines 119-260 (build from all tiles)

4. `/mnt/c/Users/nate0/RiderProjects/PokeSharp/porycon/porycon/utils.py`
   - Add `discover_all_tilesets()` function

5. `/mnt/c/Users/nate0/RiderProjects/PokeSharp/porycon/porycon/metatile.py` (possibly)
   - Add `load_complete_tileset()` function

---

## Next Steps

1. **Review this analysis** with project stakeholders
2. **Create feature branch**: `feature/per-tileset-generation`
3. **Implement Step 1** (Tileset Discovery) and test
4. **Iterate through Steps 2-6** with testing after each
5. **Validate final output** against original pokeemerald data
6. **Document changes** in CHANGELOG.md

---

## Acceptance Criteria

### Must Have
- ✓ Each tileset built exactly ONCE
- ✓ Tileset includes ALL metatiles from source
- ✓ Sprites/TileAnimations exported per tileset
- ✓ Maps reference shared tileset files
- ✓ No duplicate tile data across maps

### Should Have
- ✓ File size reduction vs current approach
- ✓ Faster conversion (no per-map tileset building)
- ✓ Maintainable code structure

### Nice to Have
- ✓ Progress reporting during tileset building
- ✓ Parallel tileset building (already exists)
- ✓ Caching to skip unchanged tilesets

---

## Estimated Impact

### Before Refactoring
- ~150 maps × 2 tilesets = 300 tileset files (many duplicates)
- Each map has incomplete tileset (only used tiles)
- Cannot edit maps freely in Tiled

### After Refactoring
- ~15-20 unique tilesets × 1 file = 15-20 tileset files
- Each tileset is complete (all tiles available)
- Full editing capability in Tiled
- **Est. file size reduction**: 40-60%
- **Est. conversion speed improvement**: 20-30%

---

## Conclusion

This refactoring is **critical** for proper Tiled integration. The current approach fundamentally misunderstands the tileset reusability model. With the changes outlined above, porycon will generate correct, complete tilesets that can be shared across all maps.

**Priority**: HIGH
**Complexity**: MEDIUM
**Estimated Effort**: 11-17 hours
**Risk**: MEDIUM (thorough testing required)
**Value**: CRITICAL (enables proper Tiled editing)
