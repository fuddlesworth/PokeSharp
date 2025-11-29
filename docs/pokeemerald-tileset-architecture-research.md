# Pokemon Emerald Tileset Architecture Research

**Date:** 2025-11-28
**Purpose:** Understand the correct tileset architecture for porycon conversion

---

## Executive Summary

The current porycon implementation is INCORRECT because it creates per-map tilesets instead of shared tilesets. This research analyzes pokeemerald's tileset system to define the correct architecture.

## Critical Findings

### 1. Tileset Sharing Architecture

**pokeemerald uses SHARED tilesets, not per-map tilesets.**

Example from `/pokeemerald/data/layouts/layouts.json`:

```json
{
  "id": "LAYOUT_MAUVILLE_CITY",
  "primary_tileset": "gTileset_General",
  "secondary_tileset": "gTileset_Mauville"
},
{
  "id": "LAYOUT_LITTLEROOT_TOWN",
  "primary_tileset": "gTileset_General",
  "secondary_tileset": "gTileset_Petalburg"
}
```

**Key Insight:** Both maps reference the SAME primary tileset `gTileset_General`.

---

## Metatile Counts

### Formula
```
metatile_count = metatile_attributes.bin file size / 2
```

Each metatile attribute entry is 2 bytes:
- Byte 1: Behavior flags
- Byte 2: Terrain/encounter/layer flags

### Primary Tilesets

| Tileset     | Metatiles | File Size (attributes.bin) |
|-------------|-----------|----------------------------|
| general     | 512       | 1024 bytes                 |
| building    | 8         | 16 bytes                   |
| secret_base | 2         | 4 bytes                    |

### Secondary Tilesets (Sample)

| Tileset   | Metatiles | File Size (attributes.bin) |
|-----------|-----------|----------------------------|
| mauville  | 510       | 1020 bytes                 |
| rustboro  | 350       | 700 bytes                  |
| dewford   | 379       | 758 bytes                  |
| slateport | 406       | 812 bytes                  |
| lavaridge | 441       | 882 bytes                  |
| fortree   | 280       | 560 bytes                  |

---

## Metatile Binary Structure

### metatiles.bin
- **Size per metatile:** 16 bytes
- **Structure:**
  - 8 tile references (4 tiles × 2 layers)
  - Each tile reference = 2 bytes (tile index + flip flags)

### metatile_attributes.bin
- **Size per metatile:** 2 bytes
- **Structure:**
  - Byte 0: Behavior (collision, elevation, etc.)
  - Byte 1: Terrain/encounter type + layer type

### Example Calculation (general tileset):
```
metatile_attributes.bin = 1024 bytes
metatile_count = 1024 / 2 = 512 metatiles
metatiles.bin = 512 × 16 = 8192 bytes ✓ (matches actual file)
```

---

## Tileset Animation Structure

Animations are stored in subdirectories under `anim/`:

### Primary Tileset: general
```
primary/general/anim/
├── flower/          (8 frames: 0.png - 7.png)
├── land_water_edge/ (animation frames)
├── sand_water_edge/ (animation frames)
├── water/           (8 frames: 0.png - 7.png)
└── waterfall/       (animation frames)
```

### Secondary Tileset: mauville
```
secondary/mauville/anim/
├── flower_1/        (5 frames: 0.png - 4.png)
└── flower_2/        (5 frames: 0.png - 4.png)
```

**Animation Pattern:**
- Each animation = separate directory
- Frames numbered sequentially: 0.png, 1.png, 2.png, ...
- Frame count varies by animation type

---

## Correct Output Structure for porycon

### WRONG (Current Implementation)
```
output/
└── Maps/
    ├── mauville_city/
    │   ├── mauville_city.json     ❌ Map-specific tileset
    │   └── mauville_city.png      ❌ Duplicate tile data
    └── littleroot_town/
        ├── littleroot_town.json   ❌ Map-specific tileset
        └── littleroot_town.png    ❌ Duplicate tile data (same as mauville!)
```

### CORRECT (Required Implementation)
```
output/
├── Tilesets/
│   └── hoenn/
│       ├── general.json          ✓ ONE file - 512 metatiles
│       ├── general.png           ✓ ONE image - all 512 metatiles rendered
│       ├── mauville.json         ✓ ONE file - 510 metatiles
│       ├── mauville.png          ✓ ONE image
│       ├── rustboro.json         ✓ 350 metatiles
│       └── rustboro.png
│
├── Sprites/
│   └── TileAnimations/
│       ├── general/
│       │   ├── water/
│       │   │   ├── 0.png
│       │   │   └── ... (8 frames)
│       │   └── flower/
│       │       └── ... (8 frames)
│       └── mauville/
│           └── flower_1/
│               └── ... (5 frames)
│
└── Maps/
    └── hoenn/
        ├── mauville_city.json    ✓ References: "../../Tilesets/hoenn/general.json"
        │                                    + "../../Tilesets/hoenn/mauville.json"
        └── littleroot_town.json  ✓ References: "../../Tilesets/hoenn/general.json"
                                                + "../../Tilesets/hoenn/petalburg.json"
```

---

## Map-to-Tileset Reference Pattern

Maps use **layout definitions** that reference tilesets by name:

```json
{
  "id": "LAYOUT_MAUVILLE_CITY",
  "width": 40,
  "height": 20,
  "primary_tileset": "gTileset_General",
  "secondary_tileset": "gTileset_Mauville",
  "blockdata_filepath": "data/layouts/MauvilleCity/map.bin"
}
```

### Conversion Rule:
```
pokeemerald name      →  porycon output path
─────────────────────────────────────────────────
gTileset_General      →  Tilesets/hoenn/general.json
gTileset_Mauville     →  Tilesets/hoenn/mauville.json
gTileset_Building     →  Tilesets/hoenn/building.json
```

---

## Implementation Requirements

### 1. Tileset Generation (ONE per tileset name)

```csharp
// CORRECT approach
void GenerateTileset(string tilesetName, string tilesetPath)
{
    // Read metatile count from attributes file
    var attributesPath = $"{tilesetPath}/metatile_attributes.bin";
    var attributesSize = File.ReadAllBytes(attributesPath).Length;
    var metatileCount = attributesSize / 2;

    // Render ALL metatiles to single image
    var outputImage = RenderAllMetatiles(metatileCount, tilesetPath);
    outputImage.Save($"Tilesets/hoenn/{tilesetName}.png");

    // Generate tileset JSON with ALL metatiles
    var tilesetJson = new TilesetDefinition
    {
        Name = tilesetName,
        MetatileCount = metatileCount,
        Metatiles = LoadAllMetatiles(metatileCount, tilesetPath)
    };
    File.WriteAllText($"Tilesets/hoenn/{tilesetName}.json",
                      JsonSerializer.Serialize(tilesetJson));
}

// Run ONCE per unique tileset
GenerateTileset("general", "pokeemerald/data/tilesets/primary/general");
GenerateTileset("mauville", "pokeemerald/data/tilesets/secondary/mauville");
```

### 2. Map Reference Generation

```csharp
// Map files reference shared tilesets
var mapJson = new MapDefinition
{
    Name = "MauvilleCity",
    Width = 40,
    Height = 20,
    PrimaryTileset = "../../Tilesets/hoenn/general.json",  // Relative path
    SecondaryTileset = "../../Tilesets/hoenn/mauville.json",
    TileData = blockData  // Map-specific tile placement
};
```

### 3. Animation Extraction

```csharp
// Extract animations to Sprites/ directory
void ExtractAnimations(string tilesetName, string animPath)
{
    var animations = Directory.GetDirectories(animPath);

    foreach (var animDir in animations)
    {
        var animName = Path.GetFileName(animDir);
        var frames = Directory.GetFiles(animDir, "*.png")
                             .OrderBy(f => f);

        var outputDir = $"Sprites/TileAnimations/{tilesetName}/{animName}";
        Directory.CreateDirectory(outputDir);

        foreach (var frame in frames)
        {
            File.Copy(frame,
                     Path.Combine(outputDir, Path.GetFileName(frame)));
        }
    }
}
```

---

## Validation Checklist

- [ ] ONE tileset file per tileset name (not per map)
- [ ] general.json contains exactly 512 metatiles
- [ ] mauville.json contains exactly 510 metatiles
- [ ] Multiple maps reference the SAME general.json file
- [ ] Animations extracted to Sprites/TileAnimations/{tileset}/{anim}/
- [ ] No duplicate tile data across maps
- [ ] Map JSON files use relative paths to reference tilesets

---

## File Size Reference

Use these sizes to validate correct extraction:

| File                                  | Expected Size      |
|---------------------------------------|-------------------|
| primary/general/metatiles.bin         | 8192 bytes        |
| primary/general/metatile_attributes.bin | 1024 bytes      |
| secondary/mauville/metatiles.bin      | 8160 bytes        |
| secondary/mauville/metatile_attributes.bin | 1020 bytes   |

---

## Common Mistakes to Avoid

1. ❌ Creating one tileset per map
2. ❌ Duplicating general tileset data in every map folder
3. ❌ Not extracting animations to Sprites/ directory
4. ❌ Hardcoding absolute paths instead of relative paths
5. ❌ Miscalculating metatile count (must use attributes.bin / 2)

---

## References

- pokeemerald source: `/pokeemerald/data/tilesets/`
- Layout definitions: `/pokeemerald/data/layouts/layouts.json`
- Tileset headers: `/pokeemerald/src/data/tilesets/headers.h`
- Map definitions: `/pokeemerald/data/maps/*/map.json`
