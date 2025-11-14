# Pokemon Emerald Map Converter to Tiled 8x8

Converts Pokemon Emerald metatile-based maps to Tiled Editor format using 8x8 tiles with metatile properties.

## Features

- ✅ Converts 16x16 metatiles to 2x2 grids of 8x8 tiles
- ✅ Preserves metatile properties (collision, elevation, behavior)
- ✅ Creates Tiled-compatible JSON maps
- ✅ Maintains editability in Tiled Editor
- ✅ Supports property-based metatile system

## Installation

Requires Python 3.7+. No additional dependencies needed.

```bash
chmod +x convert_map_8x8.py
```

## Usage

### Basic Conversion

```bash
./convert_map_8x8.py layout.json tileset.json output_map.json
```

### With Custom Tileset Image

```bash
./convert_map_8x8.py layout.json tileset.json output_map.json \
    --tileset-image "assets/tileset.png" \
    --tileset-name "general"
```

## Input Format

### layout.json (Pokemon Emerald Format)

```json
{
  "width": 20,
  "height": 18,
  "blockdata_filepath": "map.bin",
  "primary_tileset": "general",
  "border_filepath": "border.bin"
}
```

### tileset.json (Metatile Definitions)

```json
{
  "metatiles": [
    {
      "id": 0,
      "tiles": [1, 2, 17, 18],
      "behavior": 0,
      "elevation": 0,
      "layer_type": 0
    }
  ]
}
```

**Tile Order:** `[top_left, top_right, bottom_left, bottom_right]`

## Output Format

Each 8x8 tile includes metatile properties:

```json
{
  "id": 42,
  "properties": [
    {"name": "metatile_id", "type": "int", "value": 123},
    {"name": "metatile_pos", "type": "string", "value": "top_left"},
    {"name": "collision", "type": "bool", "value": true},
    {"name": "elevation", "type": "int", "value": 3},
    {"name": "behavior", "type": "string", "value": "MB_TALL_GRASS"}
  ]
}
```

## Applying Palettes to Tilesets

Pokemon Emerald stores tiles as indexed grayscale PNGs with separate palette files. You need to apply these palettes to create true-color tilesets.

**Note:** When using `convert_all_maps.ps1` or `convert_all_maps.sh`, palettes are automatically applied during tileset extraction. The following methods are for manual single-tileset conversion.

### Apply Palettes to All Tilesets

Use the batch script to apply palettes to all tilesets at once:

**Windows (PowerShell):**
```powershell
.\apply_palettes_all.ps1 -PokeemeraldDir "C:\path\to\pokeemerald" -OutputDir "..\..\PokeSharp.Game\Assets\Tilesets"
```

**Linux/Mac (Bash):**
```bash
./apply_palettes_all.sh /path/to/pokeemerald ../../PokeSharp.Game/Assets/Tilesets
```

### Apply Palette to Single Tileset

You can also use `convert_tileset_8x8.py` with the `--apply-palettes` flag:
```bash
python convert_tileset_8x8.py \
    pokeemerald/data/tilesets/primary/general \
    output/general.json \
    --apply-palettes \
    --tile-offset 0
```

For secondary tilesets, use `--tile-offset 512`:
```bash
python convert_tileset_8x8.py \
    pokeemerald/data/tilesets/secondary/lab \
    output/lab.json \
    --apply-palettes \
    --tile-offset 512
```

### Manual Palette Application (Alternative Methods)

**Using `render_tileset_with_palettes.py` (recommended - handles per-tile palettes):**
```bash
python render_tileset_with_palettes.py \
    pokeemerald/data/tilesets/primary/building/tiles.png \
    pokeemerald/data/tilesets/primary/building/palettes \
    pokeemerald/data/tilesets/primary/building/metatiles.bin \
    PokeSharp.Game/Assets/Tilesets/building_tiles.png
```

**Using `apply_palettes.py` (simple - uses palette 00 only):**
```bash
python apply_palettes.py \
    pokeemerald/data/tilesets/primary/building/tiles.png \
    pokeemerald/data/tilesets/primary/building/palettes \
    PokeSharp.Game/Assets/Tilesets/building_tiles.png
```

**Note:** Secondary tilesets need a tile offset of 512:
```bash
python render_tileset_with_palettes.py \
    pokeemerald/data/tilesets/secondary/lab/tiles.png \
    pokeemerald/data/tilesets/secondary/lab/palettes \
    pokeemerald/data/tilesets/secondary/lab/metatiles.bin \
    PokeSharp.Game/Assets/Tilesets/lab_tiles.png \
    512
```

## Batch Converting All Maps

To convert all maps from pokeemerald at once, use the batch conversion script. The script automatically extracts tilesets directly into map-specific folders with palettes applied, and processes maps in parallel for improved performance.

**Windows (PowerShell):**
```powershell
.\convert_all_maps.ps1 `
    -PokeemeraldDir "C:\path\to\pokeemerald" `
    -MapOutputDir "..\..\PokeSharp.Game\Assets\Data\Maps" `
    -TilesetOutputDir "..\..\PokeSharp.Game\Assets\Tilesets" `
    -MaxParallelJobs 4
```

**Linux/Mac (Bash):**
```bash
./convert_all_maps.sh \
    ~/pokeemerald \
    ~/PokeSharp/PokeSharp.Game/Assets/Data/Maps \
    ~/PokeSharp/PokeSharp.Game/Assets/Tilesets \
    --max-jobs 4
```

**Parameters:**
- `PokeemeraldDir` / `<pokeemerald_dir>`: Path to pokeemerald repository
- `MapOutputDir` / `<map_output_dir>`: Output directory for converted maps
- `TilesetOutputDir` / `<tileset_output_dir>`: Output directory for tilesets (organized by tileset name, e.g., `general/`, `petalburg/`)
- `-MaxParallelJobs` / `--max-jobs`: Maximum number of parallel jobs (default: 4)

**Workflow:**
The script will:
1. Find all `layout.json` files in `pokeemerald/data/layouts/`
2. For each map (processed in parallel):
   - Read the layout to determine primary and secondary tilesets
   - Extract tilesets to shared locations organized by tileset name: `{TilesetOutputDir}/{tilesetName}/` (e.g., `Tilesets/general/`, `Tilesets/petalburg/`)
   - Skip extraction if tileset already exists (avoids duplicates)
   - Apply palettes during extraction
   - Convert the map to Tiled JSON format
   - Generate relative paths for tileset references
3. Provide a summary of successful/failed conversions

**Note:** Tilesets are organized by tileset name (not map name) to avoid duplicates. Multiple maps sharing the same tileset will reference the same tileset files, reducing storage and improving organization.

## References

- [PokeSharp Metatile System](../../docs/research/metatile-8x8-approach.md)
