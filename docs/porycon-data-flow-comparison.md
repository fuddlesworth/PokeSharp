# Porycon Data Flow Comparison

## Current Architecture (BROKEN)

```
┌─────────────────────────────────────────────────────────────────────────┐
│                         CURRENT WORKFLOW (WRONG)                        │
└─────────────────────────────────────────────────────────────────────────┘

Step 1: Load Map Data
┌────────────────┐
│ Map: Route101  │
│ Primary: Gen   │
│ Secondary: Rte │
└────────┬───────┘
         │
         v
┌────────────────────────────────┐
│ Parse Map Entries              │
│ - Metatile 0x001              │
│ - Metatile 0x015              │
│ - Metatile 0x200              │
└────────┬───────────────────────┘
         │
         v
┌────────────────────────────────┐
│ Extract USED Tiles Only        │
│                                │
│ used_primary_tiles = {        │
│   0, 1, 2, 5, 8, 12, 15, 20   │  ← Only tiles used by THIS map
│ }                              │
│                                │
│ used_secondary_tiles = {      │
│   0, 3, 7, 10                 │  ← Only tiles used by THIS map
│ }                              │
└────────┬───────────────────────┘
         │
         v
┌────────────────────────────────┐
│ Register with TilesetBuilder   │
│                                │
│ builder.add_tiles(             │
│   "General",                   │
│   [0,1,2,5,8,12,15,20]        │  ← Incomplete!
│ )                              │
└────────┬───────────────────────┘
         │
         v
┌────────────────────────────────┐
│ Create Per-Map Tileset         │
│                                │
│ File: route101_general.png     │  ← Map-specific tileset
│ Contains: 8 tiles only         │  ← Missing most tiles!
└────────┬───────────────────────┘
         │
         v
┌────────────────────────────────┐
│ Save Map JSON                  │
│                                │
│ "tilesets": [{                 │
│   "source": "route101_gen.json"│  ← Unique per map
│ }]                             │
└────────────────────────────────┘

Step 2: Next Map (Route102)
┌────────────────┐
│ Map: Route102  │
│ Primary: Gen   │  ← SAME tileset
│ Secondary: Rte │  ← SAME tileset
└────────┬───────┘
         │
         v
┌────────────────────────────────┐
│ Extract USED Tiles (again!)    │
│                                │
│ used_primary_tiles = {        │
│   0, 2, 3, 7, 9, 18, 22       │  ← Different subset!
│ }                              │
└────────┬───────────────────────┘
         │
         v
┌────────────────────────────────┐
│ Create ANOTHER Tileset         │
│                                │
│ File: route102_general.png     │  ← DUPLICATE data!
│ Contains: 7 different tiles    │  ← Overlaps with Route101
└────────────────────────────────┘

Result After 150 Maps:
┌────────────────────────────────────────────┐
│ Tilesets/ (OUTPUT)                         │
│ ├─ route101_general.png       (8 tiles)   │
│ ├─ route102_general.png       (7 tiles)   │  } 150 maps ×
│ ├─ route103_general.png       (12 tiles)  │  } 2 tilesets =
│ ├─ ...                                     │  } 300 FILES!
│ ├─ littleroot_general.png     (15 tiles)  │
│ └─ oldale_general.png         (9 tiles)   │
│                                            │
│ PROBLEMS:                                  │
│ - 300 tileset files (should be ~20)       │
│ - Each has incomplete tile set            │
│ - Cannot edit maps in Tiled freely        │
│ - Massive file duplication                │
└────────────────────────────────────────────┘
```

---

## Desired Architecture (CORRECT)

```
┌─────────────────────────────────────────────────────────────────────────┐
│                      DESIRED WORKFLOW (CORRECT)                         │
└─────────────────────────────────────────────────────────────────────────┘

PHASE 1: TILESET INITIALIZATION (Once, before maps)
─────────────────────────────────────────────────

Step 1: Discover All Tilesets
┌────────────────────────────────┐
│ Scan pokeemerald/data/tilesets │
│                                │
│ Found:                         │
│ - General                      │
│ - Mauville                     │
│ - Petalburg                    │
│ - Rustboro                     │
│ - ...                          │
│                                │
│ Total: 15-20 unique tilesets   │
└────────┬───────────────────────┘
         │
         v

Step 2: Load Complete Tileset Definitions
┌────────────────────────────────────────┐
│ For tileset "General":                 │
│                                        │
│ Load ALL metatiles from:               │
│ - metatiles.bin (512 metatiles)        │
│ - tiles.png (all source tiles)         │
│ - 00.pal - 15.pal (all palettes)       │
│                                        │
│ Extract ALL tiles:                     │
│ all_tiles_with_palettes = {           │
│   (0,0), (0,1), ..., (511,15)         │  ← ALL possible combinations
│ }                                      │
│ Count: ~8000 tile+palette pairs        │
└────────┬───────────────────────────────┘
         │
         v

Step 3: Build Complete Tileset Image
┌────────────────────────────────────────┐
│ Build: general.png                     │
│                                        │
│ Contains: ALL 8000 tiles               │  ← Complete tileset!
│ Dimensions: 16×500 tiles               │
│ Size: 128×4000 pixels                  │
│                                        │
│ Mapping: (tile_id, palette) → new_gid │
│   (0, 0) → 1                           │
│   (0, 1) → 2                           │
│   ...                                  │
│   (511, 15) → 8000                     │
└────────┬───────────────────────────────┘
         │
         v

Step 4: Export Animations
┌────────────────────────────────────────┐
│ Export to Sprites/TileAnimations/      │
│                                        │
│ - General/water_anim/frame_0.png       │
│ - General/water_anim/frame_1.png       │
│ - General/flower_anim/frame_0.png      │
│                                        │
│ (Once per tileset)                     │
└────────┬───────────────────────────────┘
         │
         v

Step 5: Save Tileset Files
┌────────────────────────────────────────┐
│ Tilesets/hoenn/general.json            │
│ {                                      │
│   "tilecount": 8000,                   │  ← ALL tiles
│   "image": "general.png",              │
│   "tiles": [/* animations */]          │
│ }                                      │
│                                        │
│ Tilesets/hoenn/general.png             │
│ (Complete 128×4000px image)            │
└────────┬───────────────────────────────┘
         │
         v

REPEAT for Mauville, Petalburg, etc.

Result After Phase 1:
┌────────────────────────────────────────┐
│ Tilesets/hoenn/ (OUTPUT)               │
│ ├─ general.json      (complete)        │
│ ├─ general.png       (8000 tiles)      │
│ ├─ mauville.json     (complete)        │
│ ├─ mauville.png      (8000 tiles)      │
│ ├─ ...                                 │
│                                        │
│ Total: ~20 tileset files               │  ← Shared by all maps!
└────────────────────────────────────────┘

─────────────────────────────────────────
PHASE 2: MAP CONVERSION (Uses pre-built tilesets)
─────────────────────────────────────────

Step 1: Load Map Data
┌────────────────┐
│ Map: Route101  │
│ Primary: Gen   │  ← Tileset already built!
│ Secondary: Rte │  ← Tileset already built!
└────────┬───────┘
         │
         v

Step 2: Reference Pre-Built Tilesets
┌────────────────────────────────────────┐
│ NO TILESET BUILDING                    │
│                                        │
│ Just create map JSON with references:  │
│                                        │
│ "tilesets": [                          │
│   {                                    │
│     "firstgid": 1,                     │
│     "source": "../../Tilesets/hoenn/   │
│               general.json"            │  ← Reference shared file
│   },                                   │
│   {                                    │
│     "firstgid": 513,                   │
│     "source": "../../Tilesets/hoenn/   │
│               route.json"              │  ← Reference shared file
│   }                                    │
│ ]                                      │
└────────┬───────────────────────────────┘
         │
         v

Step 3: Save Map JSON Only
┌────────────────────────────────────────┐
│ Data/Maps/hoenn/route_101.json         │
│                                        │
│ (No tileset image created)             │
│ (Just references shared tilesets)      │
└────────────────────────────────────────┘

Step 4: Next Map (Route102)
┌────────────────┐
│ Map: Route102  │
│ Primary: Gen   │  ← References SAME general.json
│ Secondary: Rte │  ← References SAME route.json
└────────┬───────┘
         │
         v
┌────────────────────────────────────────┐
│ "tilesets": [                          │
│   { "source": "../../general.json" },  │  ← SAME file as Route101!
│   { "source": "../../route.json" }     │  ← SAME file as Route101!
│ ]                                      │
└────────────────────────────────────────┘

Result After 150 Maps:
┌────────────────────────────────────────┐
│ OUTPUT STRUCTURE                       │
│                                        │
│ Tilesets/hoenn/                        │
│ ├─ general.json      (shared)          │  }
│ ├─ general.png       (shared)          │  } ~20 files
│ ├─ mauville.json     (shared)          │  } (not 300!)
│ └─ mauville.png      (shared)          │  }
│                                        │
│ Data/Maps/hoenn/                       │
│ ├─ route_101.json    (refs general)    │
│ ├─ route_102.json    (refs general)    │  } All reference
│ ├─ route_103.json    (refs general)    │  } same tileset
│ ├─ littleroot.json   (refs general)    │  } files
│ └─ oldale.json       (refs general)    │
│                                        │
│ Sprites/TileAnimations/                │
│ ├─ General/                            │  } Exported
│ │  └─ water_anim/                      │  } once per
│ └─ Mauville/                           │  } tileset
│    └─ fountain_anim/                   │
│                                        │
│ BENEFITS:                              │
│ ✓ 20 tileset files (not 300)          │
│ ✓ Each tileset is COMPLETE            │
│ ✓ Full editing capability in Tiled    │
│ ✓ No file duplication                 │
│ ✓ 40-60% smaller output               │
└────────────────────────────────────────┘
```

---

## Key Differences Summary

| Aspect | Current (Wrong) | Desired (Correct) |
|--------|----------------|-------------------|
| **When tilesets built** | During each map conversion | Once, before any maps |
| **Tiles included** | Only tiles used by that map | ALL tiles from tileset |
| **Number of files** | ~300 (150 maps × 2) | ~20 (unique tilesets) |
| **File reuse** | None (each map has own) | 100% (all maps share) |
| **Editing in Tiled** | Limited (missing tiles) | Full (all tiles available) |
| **Animation export** | Once per tileset (correct) | Once per tileset (same) |
| **Conversion speed** | Slower (builds 300 files) | Faster (builds 20 files) |
| **Disk usage** | High (duplicates) | Low (shared) |

---

## Code Mapping

### Current Architecture Code Locations

```
converter.py:217-221
┌─────────────────────────────────┐
│ used_primary_tiles = set()      │  ← WRONG: Only tracks used tiles
│ used_secondary_tiles = set()    │
└─────────────────────────────────┘

converter.py:391-403
┌─────────────────────────────────────────────────┐
│ self.tileset_builder.add_tiles(                │  ← WRONG: Adds only
│   primary_tileset,                              │          used tiles
│   list(used_primary_tiles)                      │
│ )                                               │
└─────────────────────────────────────────────────┘

converter.py:1168-1317
┌─────────────────────────────────────────────────┐
│ def _create_tileset_for_map(...):              │  ← WRONG: Per-map
│     # Creates tileset FOR THIS MAP             │          tilesets
│     # Uses only used_metatiles                 │
└─────────────────────────────────────────────────┘

__main__.py:272-337
┌─────────────────────────────────────────────────┐
│ # Build tilesets AFTER maps                    │  ← WRONG: After maps
│ for tileset_name in tileset_names:             │
│     create_tiled_tileset(...)                  │
└─────────────────────────────────────────────────┘

tileset_builder.py:119-131
┌─────────────────────────────────────────────────┐
│ used_with_palettes = sorted(                   │  ← WRONG: Uses
│     self.used_tiles_with_palettes.get(...)     │          "used" tiles
│ )                                               │
└─────────────────────────────────────────────────┘
```

### Desired Architecture Code Locations

```
NEW: tileset_discovery.py
┌─────────────────────────────────────────────────┐
│ def discover_all_tilesets(input_dir):          │  ← NEW: Finds all
│     # Scan pokeemerald/data/tilesets           │         tilesets
│     return {"General", "Mauville", ...}        │
└─────────────────────────────────────────────────┘

NEW: tileset_builder.py (refactored)
┌─────────────────────────────────────────────────┐
│ def load_tileset(tileset_name):                │  ← NEW: Loads ALL
│     # Load ALL metatiles from tileset          │         metatiles
│     all_tiles = extract_all_tiles(...)         │
│     self.tilesets[name] = all_tiles            │
└─────────────────────────────────────────────────┘

__main__.py (refactored)
┌─────────────────────────────────────────────────┐
│ # BEFORE map conversion:                       │  ← NEW: Before maps
│ all_tilesets = discover_all_tilesets(...)      │
│ for tileset in all_tilesets:                   │
│     builder.load_tileset(tileset)              │
│     builder.create_tiled_tileset(tileset)      │
└─────────────────────────────────────────────────┘

converter.py (refactored)
┌─────────────────────────────────────────────────┐
│ # REMOVED: used_primary_tiles tracking         │  ← REMOVED
│ # REMOVED: add_tiles() calls                   │
│ # REMOVED: _create_tileset_for_map()           │
│                                                 │
│ # NEW: Just reference pre-built tilesets       │
│ tilesets = [                                   │
│   {"source": "../../general.json"},            │
│   {"source": "../../route.json"}               │
│ ]                                               │
└─────────────────────────────────────────────────┘
```

---

## Visual Comparison

### Current: Per-Map Tilesets (Wrong)

```
   Map 1           Map 2           Map 3
  ┌─────┐        ┌─────┐        ┌─────┐
  │ Rte │        │ Rte │        │ Rte │
  │ 101 │        │ 102 │        │ 103 │
  └──┬──┘        └──┬──┘        └──┬──┘
     │              │              │
     v              v              v
  ┌─────────┐   ┌─────────┐   ┌─────────┐
  │Gen:8tiles│   │Gen:7tiles│   │Gen:12tiles│  ← Duplicates!
  │Rte:4tiles│   │Rte:6tiles│   │Rte:8tiles │  ← Duplicates!
  └─────────┘   └─────────┘   └─────────┘
  Incomplete!   Incomplete!   Incomplete!
```

### Desired: Shared Tilesets (Correct)

```
   Map 1           Map 2           Map 3
  ┌─────┐        ┌─────┐        ┌─────┐
  │ Rte │        │ Rte │        │ Rte │
  │ 101 │        │ 102 │        │ 103 │
  └──┬──┘        └──┬──┘        └──┬──┘
     │              │              │
     └──────┬───────┴──────┬───────┘
            │              │
            v              v
     ┌────────────┐ ┌────────────┐
     │Gen:8000tiles│ │Rte:8000tiles│  ← Shared!
     │  Complete! │ │  Complete! │  ← Once!
     └────────────┘ └────────────┘
```

---

## Migration Path

```
Current State
     │
     v
Step 1: Add tileset discovery
     │  (discover_all_tilesets)
     v
Step 2: Add complete loading
     │  (load_complete_tileset)
     v
Step 3: Refactor TilesetBuilder
     │  (use all_tiles not used_tiles)
     v
Step 4: Move building before maps
     │  (in __main__.py)
     v
Step 5: Remove map tileset building
     │  (in converter.py)
     v
Step 6: Test & validate
     │
     v
Desired State
```

---

## Performance Impact

### Current (Per-Map)
```
For 150 maps:
  ┌──────────────────────────────┐
  │ Map 1: Build 2 tilesets      │  0.5s
  │ Map 2: Build 2 tilesets      │  0.5s
  │ Map 3: Build 2 tilesets      │  0.5s
  │ ...                          │
  │ Map 150: Build 2 tilesets    │  0.5s
  └──────────────────────────────┘
  Total: 75 seconds for tilesets
```

### Desired (Shared)
```
Once before maps:
  ┌──────────────────────────────┐
  │ Build General tileset        │  1.0s
  │ Build Mauville tileset       │  1.0s
  │ Build Petalburg tileset      │  1.0s
  │ ...                          │
  │ Build tileset #20            │  1.0s
  └──────────────────────────────┘
  Total: 20 seconds for tilesets

Maps just reference (no building):
  ┌──────────────────────────────┐
  │ Map 1: Create JSON           │  0.1s
  │ Map 2: Create JSON           │  0.1s
  │ Map 3: Create JSON           │  0.1s
  │ ...                          │
  │ Map 150: Create JSON         │  0.1s
  └──────────────────────────────┘

Total: 20s (tilesets) + 15s (maps) = 35s
Speedup: 75s → 35s = 2.1x faster!
```
