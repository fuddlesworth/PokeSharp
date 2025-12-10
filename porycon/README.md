# Porycon - Pokemon Emerald to Tiled Converter

A Python tool to convert Pokemon Emerald decompilation maps to Tiled JSON format, replacing metatiles with individual tile layers for easier editing.

## Features

- Converts pokeemerald map.json files to Tiled format
- Splits metatiles into individual tiles across separate BG layers
- Creates complete tilesets (no metatiles) for Tiled editing
- Generates Tiled world files from map connections
- **Tile animations**: Converts automatic and trigger-based animations (see [Animation Guide](docs/animations.md))
- **Map popup graphics**: Extracts and processes region map popup backgrounds and outlines
- Organizes output: Maps in region folders, Worlds at root, Tilesets in region folders

## Project Structure

```
porycon/
├── porycon/
│   ├── __init__.py
│   ├── converter.py          # Main conversion logic
│   ├── metatile.py          # Metatile to tile conversion
│   ├── tileset_builder.py   # Complete tileset generation
│   ├── world_builder.py     # World file generation
│   ├── popup_extractor.py   # Map popup graphics extractor
│   └── utils.py             # Utility functions
├── tests/
├── requirements.txt
└── README.md
```

## Installation

```bash
cd porycon
pip install -e .
```

Or install dependencies directly:
```bash
pip install -r requirements.txt
```

## Usage

### Convert Maps

Convert all maps:
```bash
python -m porycon --input /path/to/pokeemerald --output /path/to/output
```

Convert specific region:
```bash
python -m porycon --input /path/to/pokeemerald --output /path/to/output --region hoenn
```

### Extract Map Popup Graphics

Extract popup backgrounds and outlines from pokeemerald:
```bash
python -m porycon --input /path/to/pokeemerald --output /path/to/PokeSharp --extract-popups
```

This will:
- Find all popup graphics in `pokeemerald/graphics/map_popup/`
- Copy backgrounds to `MonoBallFramework.Game/Assets/Graphics/Maps/Popups/Backgrounds/`
- Copy outlines to `MonoBallFramework.Game/Assets/Graphics/Maps/Popups/Outlines/`
- Convert outline tile sheets with palette transparency

### Extract Map Section Definitions

Extract map sections (MAPSEC) and popup theme mappings:
```bash
python -m porycon --input /path/to/pokeemerald --output /path/to/PokeSharp/MonoBallFramework.Game/Assets --extract-sections
```

This will:
- Parse `pokeemerald/src/data/region_map/region_map_sections.json` for section definitions
- Parse `pokeemerald/src/map_name_popup.c` for popup theme mappings
- Generate individual section JSON files in `MonoBallFramework.Game/Assets/Definitions/Maps/Sections/`
- Create `section_registry.json` (master list of all sections)
- Create `theme_summary.json` (theme usage statistics)
- Copy and process outlines to `MonoBallFramework.Game/Assets/Graphics/Maps/Popups/Outlines/`
- **Apply transparency** to outline sprite sheets for 9-slice rendering
- Create JSON definition files in `Assets/Definitions/Maps/Popups/`

**Example:**
```bash
python -m porycon --input C:/pokeemerald --output C:/Users/nate0/RiderProjects/PokeSharp --extract-popups
```

### Output Structure

The converter creates the following structure:

```
output/
├── Maps/
│   └── hoenn/
│       ├── mauvillecity.json
│       ├── littleroottown.json
│       └── ...
├── Worlds/
│   ├── hoenn.world
│   └── ...
├── Tilesets/
│   └── hoenn/
│       ├── general.json
│       ├── general.png
│       ├── mauville.json
│       ├── mauville.png
│       └── ...
└── MonoBallFramework.Game/
    └── Assets/
        ├── Graphics/Maps/Popups/
        │   ├── Backgrounds/
        │   │   ├── wood.png
        │   │   └── ...
        │   └── Outlines/
        │       ├── wood_outline.png  (transparency applied)
        │       └── ...
        └── Definitions/Maps/Popups/
            ├── Backgrounds/
            │   ├── wood.json
            │   └── ...
            └── Outlines/
                ├── wood_outline.json
                └── ...
```

### How It Works

1. **Metatile Conversion**: Each 2x2 metatile (8 tiles) is split into individual tiles
2. **Layer Distribution**: Tiles are distributed across 3 BG layers based on metatile layer type:
   - **NORMAL**: Bottom tiles → Objects layer, Top tiles → Overhead layer
   - **COVERED**: Bottom tiles → Ground layer, Top tiles → Objects layer
   - **SPLIT**: Bottom tiles → Ground layer, Top tiles → Overhead layer
3. **Tileset Building**: Complete tilesets are created containing only tiles actually used in maps
4. **World Files**: Tiled world files are generated from map connections
5. **Popup Graphics**: Backgrounds and outlines are extracted, with outlines getting 9-slice transparency processing

### Map Popup Graphics

Popup graphics are processed specially:

- **Backgrounds**: Simple textures, copied as-is
- **Outlines**: Sprite sheets for 9-slice rendering
  - Center region made fully transparent (so background shows through)
  - White/background colors removed from border regions
  - Corners and edges preserved for pixel-perfect rendering

This ensures popups render correctly with proper transparency and no distortion.

## Requirements

- Python 3.8+
- Pillow (for image processing)
- See requirements.txt for full dependencies

## Documentation

- **[Animation Guide](docs/animations.md)** - Complete guide to tile animations (automatic & trigger-based)
- **Project Structure** - See directory layout above

## Notes

- The converter creates tilesets with only used tiles (not all tiles from source)
- Tile IDs are remapped to be sequential (1-based for Tiled)
- Maps reference tilesets via relative paths
- World files use a simple grid layout (can be improved with graph algorithms)
- Animation support includes water, flowers, waterfalls, and more (see Animation Guide)
- Popup outlines are automatically processed with transparency for 9-slice rendering

## Command Line Options

- `--input <path>`: Input directory (pokeemerald root) [required]
- `--output <path>`: Output directory for Tiled files [required]
- `--region <name>`: Region name for organizing output folders
- `--extract-popups`: Extract map popup graphics instead of converting maps
- `--verbose, -v`: Show detailed progress information
- `--debug, -d`: Show debug information (implies verbose)
