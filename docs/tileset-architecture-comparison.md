# Tileset Architecture: Current vs Correct

## The Problem Visualized

### CURRENT (WRONG) Implementation

```
porycon converts Pokemon Emerald maps like this:

pokeemerald/data/tilesets/
├── primary/
│   └── general/              ← 512 metatiles (SHARED by many maps)
│       ├── metatiles.bin
│       └── tiles.png
└── secondary/
    ├── mauville/             ← 510 metatiles (Mauville-specific)
    └── petalburg/            ← Different metatiles (Petalburg-specific)

    ↓ WRONG CONVERSION ↓

output/Maps/
├── mauville_city/
│   ├── mauville_city.json    ← Contains 512+510 = 1022 metatiles
│   └── mauville_city.png     ← Renders all 1022 metatiles
│
└── littleroot_town/
    ├── littleroot_town.json  ← Contains 512+XXX = YYY metatiles
    └── littleroot_town.png   ← DUPLICATES the 512 general metatiles!

PROBLEM: The 512 "general" metatiles are DUPLICATED in every map!
```

---

### CORRECT Implementation

```
pokeemerald Architecture:
─────────────────────────

TILESETS (Shared Resources):
  general.tileset   → Used by 50+ maps
  mauville.tileset  → Used by 3-5 Mauville maps
  petalburg.tileset → Used by 2-3 Petalburg maps

MAPS (References):
  MauvilleCity.map     → Uses: general + mauville
  LittlerootTown.map   → Uses: general + petalburg
  OldateTown.map       → Uses: general + petalburg
  Route101.map         → Uses: general + (none)


porycon SHOULD Convert To:
──────────────────────────

output/
├── Tilesets/hoenn/
│   ├── general.json          ← 512 metatiles (ONE file, shared)
│   ├── general.png
│   ├── mauville.json         ← 510 metatiles (ONE file)
│   ├── mauville.png
│   ├── petalburg.json        ← XXX metatiles (ONE file)
│   └── petalburg.png
│
├── Sprites/TileAnimations/
│   ├── general/
│   │   ├── water/            ← Animation frames
│   │   └── flower/
│   └── mauville/
│       └── flower_1/
│
└── Maps/hoenn/
    ├── mauville_city.json    ← References: ../Tilesets/general.json
    │                                      + ../Tilesets/mauville.json
    ├── littleroot_town.json  ← References: ../Tilesets/general.json
    │                                      + ../Tilesets/petalburg.json
    └── oldale_town.json      ← References: ../Tilesets/general.json
                                           + ../Tilesets/petalburg.json
```

---

## Size Comparison

### Current (Wrong) Output:
```
Maps/mauville_city/mauville_city.png     : ~500 KB (1022 metatiles)
Maps/littleroot_town/littleroot_town.png : ~450 KB (800 metatiles)
Maps/oldale_town/oldale_town.png         : ~450 KB (800 metatiles)
───────────────────────────────────────────────────────────────────
Total: ~1.4 MB (with massive duplication of general tileset)
```

### Correct Output:
```
Tilesets/hoenn/general.png   : ~250 KB (512 metatiles, ONE time)
Tilesets/hoenn/mauville.png  : ~250 KB (510 metatiles)
Tilesets/hoenn/petalburg.png : ~150 KB (288 metatiles)

Maps/mauville_city.json      : ~20 KB (just tile indices)
Maps/littleroot_town.json    : ~15 KB (just tile indices)
Maps/oldale_town.json        : ~15 KB (just tile indices)
───────────────────────────────────────────────────────────────────
Total: ~700 KB (NO duplication, clean architecture)
```

**Space Savings:** 50% reduction + proper reusability

---

## Data Flow Comparison

### WRONG Flow
```
1. For each map:
   ├─ Load primary tileset (general)
   ├─ Load secondary tileset (mauville/petalburg/etc)
   ├─ Merge both tilesets into ONE map-specific file
   └─ Render combined tileset to ONE map-specific image

Result: Every map file is SELF-CONTAINED but DUPLICATES shared data
```

### CORRECT Flow
```
1. For each UNIQUE tileset (general, mauville, petalburg):
   ├─ Read metatile count from metatile_attributes.bin
   ├─ Render ALL metatiles to tileset image
   ├─ Generate tileset.json with metatile definitions
   └─ Extract animations to Sprites/TileAnimations/{tileset}/

2. For each map:
   ├─ Determine primary + secondary tilesets used
   ├─ Generate map.json with:
   │  ├─ Reference to primary tileset (relative path)
   │  ├─ Reference to secondary tileset (relative path)
   │  └─ Tile placement data (which metatiles go where)
   └─ NO tileset image generation (uses shared tilesets)

Result: Maps REFERENCE shared tilesets, no duplication
```

---

## Code Example: Loading a Map

### Current (Wrong) - Self-Contained Map
```csharp
// Load map (everything embedded)
var map = MapLoader.Load("Maps/hoenn/mauville_city.json");

// Access tileset (embedded in map file)
var tileset = map.Tileset;  // Contains all 1022 metatiles
var image = map.TilesetImage;  // "mauville_city.png"
```

### Correct - Referenced Tilesets
```csharp
// Load map (references external tilesets)
var map = MapLoader.Load("Maps/hoenn/mauville_city.json");

// Access tilesets (loaded from referenced files)
var primaryTileset = TilesetLoader.Load(map.PrimaryTilesetPath);
  // → Loads "Tilesets/hoenn/general.json"
  // → Contains 512 metatiles

var secondaryTileset = TilesetLoader.Load(map.SecondaryTilesetPath);
  // → Loads "Tilesets/hoenn/mauville.json"
  // → Contains 510 metatiles

// Get specific metatile from map
var tileIndex = map.GetTileAt(x, y);

if (tileIndex < 512)
{
    // Tile from primary tileset
    var metatile = primaryTileset.Metatiles[tileIndex];
}
else
{
    // Tile from secondary tileset
    var metatile = secondaryTileset.Metatiles[tileIndex - 512];
}
```

---

## Tileset Reuse Statistics

Based on pokeemerald analysis:

| Tileset  | Type      | Maps Using It | Metatiles |
|----------|-----------|---------------|-----------|
| general  | Primary   | ~60+ maps     | 512       |
| building | Primary   | ~40+ maps     | 8         |
| mauville | Secondary | 5 maps        | 510       |
| rustboro | Secondary | 4 maps        | 350       |
| petalburg| Secondary | 3 maps        | 288       |

**Impact of Current Bug:**
- general (512 metatiles) duplicated in 60+ map files
- building (8 metatiles) duplicated in 40+ map files

**With Correct Architecture:**
- general: ONE file, referenced by 60+ maps
- building: ONE file, referenced by 40+ maps

---

## Animation Handling

### Current (Wrong):
```
Maps/mauville_city/
└── animations/           ← Animations embedded per map?
    └── ??? (not clear where these go)
```

### Correct:
```
Sprites/TileAnimations/
├── general/
│   ├── water/            ← Water animation (8 frames)
│   │   ├── 0.png
│   │   ├── 1.png
│   │   └── ... 7.png
│   └── flower/           ← Flower animation (8 frames)
│       └── ...
└── mauville/
    ├── flower_1/         ← Mauville flower variant (5 frames)
    └── flower_2/
```

**Key Points:**
1. Animations organized by TILESET, not by map
2. Each animation in its own subdirectory
3. Frames numbered: 0.png, 1.png, 2.png, ...
4. Multiple maps can reference the same animation set

---

## Validation Steps

### How to Verify Correct Implementation:

1. **Check tileset count:**
   ```bash
   ls Tilesets/hoenn/*.json | wc -l
   # Should match unique tileset count (NOT map count)
   ```

2. **Check for duplication:**
   ```bash
   md5sum Tilesets/hoenn/general.png
   # Run on ALL maps - general.png should exist ONLY ONCE
   ```

3. **Verify references:**
   ```bash
   grep -r "primaryTileset" Maps/hoenn/*.json
   # All should reference: "../../Tilesets/hoenn/general.json"
   ```

4. **Check metatile counts:**
   ```bash
   # general.json should have exactly 512 metatiles
   jq '.metatiles | length' Tilesets/hoenn/general.json
   ```

---

## Migration Path

If you have existing wrong output:

```bash
# 1. Extract unique tilesets from all maps
find output/Maps -name "*.json" | xargs grep -h "tileset" | sort -u

# 2. Create Tilesets directory
mkdir -p output/Tilesets/hoenn

# 3. Move first occurrence of each tileset
# (manual process - identify duplicates)

# 4. Update map JSON files to reference shared tilesets

# 5. Delete duplicate tileset files from Maps/
```

---

## Summary

| Aspect           | Current (Wrong)         | Correct                    |
|------------------|-------------------------|----------------------------|
| Tileset location | Per map                 | Shared directory           |
| Duplication      | Massive (60x+ general)  | None                       |
| File size        | Large                   | 50% smaller                |
| Reusability      | None                    | Full reuse                 |
| Animations       | Unclear/embedded        | Sprites/TileAnimations/    |
| References       | Self-contained          | Relative paths             |
| Scalability      | Poor (N maps = N tilesets) | Good (shared resources) |

**Bottom Line:** The current implementation treats each map as isolated, duplicating shared tileset data. The correct implementation extracts tilesets as shared resources that multiple maps reference.
