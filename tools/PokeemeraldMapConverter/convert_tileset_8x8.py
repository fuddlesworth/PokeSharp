#!/usr/bin/env python3
"""
Pokemon Emerald to Tiled 8x8 Tileset Converter
Converts pokeemerald tileset files to Tiled JSON format with metatile properties.

Usage:
    python convert_tileset_8x8.py <input_dir> <output_json>

Input files (pokeemerald format):
    - metatiles.bin: Metatile definitions (4 tile indices per metatile)
    - metatile_attributes.bin: Behavior, collision, elevation data
    - tiles.png: 8x8 tile graphics sprite sheet

Output:
    - Tiled JSON tileset with 8x8 tiles and metatile properties
"""

import struct
import json
import sys
import shutil
import subprocess
from pathlib import Path
from typing import List, Dict, Any, Tuple, Optional
from PIL import Image


# Metatile behavior constants (from pokeemerald metatile_behaviors.h)
BEHAVIOR_NAMES = {
    0x00: "MB_NORMAL",
    0x01: "MB_TALL_GRASS",
    0x02: "MB_LONG_GRASS",
    0x03: "MB_PUDDLE",
    0x04: "MB_SAND",
    0x05: "MB_DEEP_WATER",
    0x06: "MB_SHORT_GRASS",
    0x07: "MB_CAVE",
    0x08: "MB_SPIN_RIGHT",
    0x09: "MB_SPIN_LEFT",
    0x0A: "MB_SPIN_UP",
    0x0B: "MB_SPIN_DOWN",
    0x0C: "MB_SAND_WALL",
    0x0D: "MB_CRACKED_FLOOR",
    0x0E: "MB_SHOAL_CAVE_ENTRANCE",
    0x0F: "MB_ICE",
    0x10: "MB_WALKWAY_OVER_WATER",
    0x11: "MB_WATERFALL",
    0x12: "MB_NO_RUNNING",
    0x13: "MB_BRIDGE_OVER_WATER",
    0x14: "MB_BRIDGE_OVER_OCEAN",
    0x15: "MB_IMPASSABLE_NORTH",
    0x16: "MB_IMPASSABLE_SOUTH",
    0x17: "MB_IMPASSABLE_WEST",
    0x18: "MB_IMPASSABLE_EAST",
    0x19: "MB_IMPASSABLE_NORTHEAST",
    0x1A: "MB_IMPASSABLE_NORTHWEST",
    0x1B: "MB_IMPASSABLE_SOUTHEAST",
    0x1C: "MB_IMPASSABLE_SOUTHWEST",
    0x1D: "MB_JUMP_NORTH",
    0x1E: "MB_JUMP_SOUTH",
    0x1F: "MB_JUMP_WEST",
    0x20: "MB_JUMP_EAST",
    0x21: "MB_JUMP_NORTHEAST",
    0x22: "MB_JUMP_NORTHWEST",
    0x23: "MB_JUMP_SOUTHEAST",
    0x24: "MB_JUMP_SOUTHWEST",
    0x25: "MB_SURF",
    0x26: "MB_SLIDE_ICE",
    0x27: "MB_DOOR",
    0x28: "MB_COUNTER",
    0x29: "MB_SECRET_BASE_WALL",
    0x2A: "MB_SECRET_BASE_PC",
    0x2B: "MB_SECRET_BASE_REGISTER",
    0x2C: "MB_SECRET_BASE_DECORATION",
    0x2D: "MB_SECRET_BASE_GLITTER_MAT",
    0x2E: "MB_SECRET_BASE_JUMP_MAT",
    0x2F: "MB_SECRET_BASE_SPIN_MAT",
    0x30: "MB_STAIRS_OUTSIDE",
    0x31: "MB_STAIRS",
    0x32: "MB_SECRET_BASE_BREAKABLE_DOOR",
    0x33: "MB_IMPASSABLE",
}

# Collision/Passability flags
PASSABILITY_SOLID = 0xC
PASSABILITY_WALKABLE = 0x0


class MetatileAttributes:
    """Represents metatile behavior and collision attributes."""

    def __init__(self, behavior: int, terrain: int, encounter_type: int, layer_type: int):
        self.behavior = behavior
        self.terrain = terrain  # Bits 0-3: terrain type
        self.encounter_type = encounter_type  # Bits 4-6: encounter type
        self.layer_type = layer_type  # Bits 7-9: layer type (0=normal, 1=covered, 2=split)

    @classmethod
    def from_bytes(cls, data: bytes) -> 'MetatileAttributes':
        """Parse 4-byte metatile attribute structure."""
        # Format: behavior (u16), terrain_and_layer (u16)
        behavior, terrain_and_layer = struct.unpack('<HH', data)

        terrain = terrain_and_layer & 0x000F  # Bits 0-3
        encounter_type = (terrain_and_layer >> 4) & 0x0007  # Bits 4-6
        layer_type = (terrain_and_layer >> 7) & 0x0003  # Bits 7-8

        return cls(behavior, terrain, encounter_type, layer_type)

    def get_elevation(self) -> int:
        """Extract elevation (0-15) from terrain bits."""
        return self.terrain & 0x0F

    def is_solid(self) -> bool:
        """Determine if metatile blocks movement."""
        # Check if behavior indicates impassable
        if self.behavior in range(0x15, 0x1D):  # IMPASSABLE_* behaviors
            return True
        if self.behavior == 0x33:  # MB_IMPASSABLE
            return True
        if self.behavior == 0x0C:  # MB_SAND_WALL
            return True

        # Water behaviors are solid without Surf
        if self.behavior in [0x05, 0x11]:  # DEEP_WATER, WATERFALL
            return True

        return False

    def get_behavior_name(self) -> str:
        """Get human-readable behavior name."""
        return BEHAVIOR_NAMES.get(self.behavior, f"MB_UNKNOWN_{self.behavior:02X}")

    def get_encounter_rate(self) -> int:
        """Calculate encounter rate based on behavior and encounter type."""
        # Tall grass typically has 20% encounter rate
        if self.behavior == 0x01:  # MB_TALL_GRASS
            return 20
        if self.behavior == 0x02:  # MB_LONG_GRASS
            return 25
        if self.behavior == 0x06:  # MB_SHORT_GRASS
            return 10
        if self.behavior in [0x05, 0x25]:  # DEEP_WATER, SURF
            return 5
        if self.behavior == 0x07:  # MB_CAVE
            return 10

        return 0


class Metatile:
    """Represents a 2x2 arrangement of 8x8 tiles."""

    def __init__(self, tiles: List[int], attributes: MetatileAttributes):
        """
        Args:
            tiles: List of 4 tile indices [top_left, top_right, bottom_left, bottom_right]
            attributes: Behavior and collision attributes
        """
        if len(tiles) != 4:
            raise ValueError(f"Metatile must have exactly 4 tiles, got {len(tiles)}")

        self.tiles = tiles
        self.attributes = attributes

    @classmethod
    def from_bytes(cls, data: bytes, attributes: MetatileAttributes) -> 'Metatile':
        """Parse 8-byte metatile structure (4 u16 tile indices)."""
        tiles = list(struct.unpack('<4H', data))
        return cls(tiles, attributes)

    def get_tile_at_position(self, position: str) -> int:
        """Get tile ID for specific position in 2x2 grid."""
        positions = {
            "top_left": 0,
            "top_right": 1,
            "bottom_left": 2,
            "bottom_right": 3
        }
        return self.tiles[positions[position]]


def load_metatiles(metatiles_path: Path, attributes_path: Path) -> List[Metatile]:
    """Load metatiles and their attributes from binary files."""

    # Read metatile definitions (8 bytes per metatile)
    with open(metatiles_path, 'rb') as f:
        metatiles_data = f.read()

    # Read attributes (4 bytes per metatile)
    attributes_data = b''
    if attributes_path.exists():
        with open(attributes_path, 'rb') as f:
            attributes_data = f.read()
    else:
        # If attributes file doesn't exist, create default attributes
        num_metatiles = len(metatiles_data) // 8
        attributes_data = b'\x00\x00\x00\x00' * num_metatiles

    # Parse attributes (handle truncated files gracefully)
    attributes = []
    expected_metatiles = len(metatiles_data) // 8
    expected_attributes_size = expected_metatiles * 4
    
    if len(attributes_data) < expected_attributes_size:
        # File is truncated, pad with default attributes
        print(f"[WARN] Attributes file is truncated ({len(attributes_data)} bytes, expected {expected_attributes_size}), padding with defaults")
        attributes_data = attributes_data + (b'\x00\x00\x00\x00' * (expected_metatiles - len(attributes_data) // 4))
    
    for i in range(0, min(len(attributes_data), expected_attributes_size), 4):
        attr_bytes = attributes_data[i:i+4]
        if len(attr_bytes) == 4:
            attributes.append(MetatileAttributes.from_bytes(attr_bytes))
        else:
            # Pad incomplete attribute with zeros
            attr_bytes = attr_bytes + b'\x00' * (4 - len(attr_bytes))
            attributes.append(MetatileAttributes.from_bytes(attr_bytes))

    # Parse metatiles
    metatiles = []
    for i in range(0, len(metatiles_data), 8):
        metatile_bytes = metatiles_data[i:i+8]
        metatile_idx = i // 8

        if metatile_idx < len(attributes):
            metatile = Metatile.from_bytes(metatile_bytes, attributes[metatile_idx])
            metatiles.append(metatile)
        else:
            # Create metatile with default attributes if attributes are missing
            default_attrs = MetatileAttributes(0, 0, 0, 0)
            metatile = Metatile.from_bytes(metatile_bytes, default_attrs)
            metatiles.append(metatile)

    return metatiles


def get_tile_dimensions(tiles_png_path: Path) -> Tuple[int, int]:
    """Get the dimensions of the tiles.png sprite sheet."""
    try:
        img = Image.open(tiles_png_path)
        return img.size
    except Exception as e:
        print(f"Warning: Could not read {tiles_png_path}: {e}")
        # Default to reasonable size (128x128 tiles = 16x16 8x8 tiles)
        return (128, 128)


def convert_tileset_to_8x8_with_properties(
    input_dir: Path,
    output_path: Path,
    relative_image_path: str = None,
    apply_palettes: bool = False,
    tile_offset: int = 0,
    render_palettes_script: Optional[Path] = None
) -> None:
    """
    Convert pokeemerald tileset to Tiled 8x8 JSON format with metatile properties.

    Args:
        input_dir: Directory containing metatiles.bin, metatile_attributes.bin, tiles.png
        output_path: Output JSON file path
        relative_image_path: Relative path to tiles.png from the output JSON location (defaults to {tileset_name}_tiles.png)
        apply_palettes: If True, apply palettes to tiles.png using render_tileset_with_palettes.py
        tile_offset: Tile offset for secondary tilesets (default 0, use 512 for secondary)
        render_palettes_script: Path to render_tileset_with_palettes.py script (auto-detected if None)
    """

    # Load input files
    metatiles_path = input_dir / "metatiles.bin"
    attributes_path = input_dir / "metatile_attributes.bin"
    tiles_png_path = input_dir / "tiles.png"

    # Default to tileset_name_tiles.png if not specified
    if relative_image_path is None:
        tileset_name = input_dir.name
        relative_image_path = f"{tileset_name}_tiles.png"

    print(f"Loading metatiles from {input_dir}...")
    metatiles = load_metatiles(metatiles_path, attributes_path)
    print(f"Loaded {len(metatiles)} metatiles")

    # Get image dimensions
    img_width, img_height = get_tile_dimensions(tiles_png_path)
    tiles_per_row = img_width // 8
    tile_count = (img_width // 8) * (img_height // 8)

    # Create Tiled tileset structure
    tileset = {
        "columns": tiles_per_row,
        "image": relative_image_path,
        "imageheight": img_height,
        "imagewidth": img_width,
        "margin": 0,
        "name": input_dir.name,
        "spacing": 0,
        "tilecount": tile_count,
        "tiledversion": "1.10.2",
        "tileheight": 8,
        "tilewidth": 8,
        "type": "tileset",
        "version": "1.10",
        "tiles": []
    }

    # Write output JSON
    output_path.parent.mkdir(parents=True, exist_ok=True)
    with open(output_path, 'w') as f:
        json.dump(tileset, f, indent=2)

    # Handle tileset image output
    output_png_path = output_path.parent / relative_image_path
    if tiles_png_path.exists():
        if apply_palettes:
            # Apply palettes using render_tileset_with_palettes.py
            palettes_dir = input_dir / "palettes"
            metatiles_bin = input_dir / "metatiles.bin"
            
            if not palettes_dir.exists():
                print(f"[WARN] Warning: Palettes directory not found: {palettes_dir}, copying tiles.png as-is")
                shutil.copy2(tiles_png_path, output_png_path)
            elif not metatiles_bin.exists():
                print(f"[WARN] Warning: metatiles.bin not found: {metatiles_bin}, copying tiles.png as-is")
                shutil.copy2(tiles_png_path, output_png_path)
            else:
                # Find render_palettes_script if not provided
                if render_palettes_script is None:
                    script_dir = Path(__file__).parent
                    render_palettes_script = script_dir / "render_tileset_with_palettes.py"
                
                if not render_palettes_script.exists():
                    print(f"[WARN] Warning: render_tileset_with_palettes.py not found: {render_palettes_script}, copying tiles.png as-is")
                    shutil.copy2(tiles_png_path, output_png_path)
                else:
                    # Call render_tileset_with_palettes.py
                    try:
                        cmd = [
                            sys.executable,
                            str(render_palettes_script),
                            str(tiles_png_path),
                            str(palettes_dir),
                            str(metatiles_bin),
                            str(output_png_path),
                            str(tile_offset)
                        ]
                        result = subprocess.run(cmd, capture_output=True, text=True, check=True)
                        print(f"[OK] Applied palettes to tileset image: {output_png_path}")
                        if result.stdout:
                            print(result.stdout)
                    except subprocess.CalledProcessError as e:
                        print(f"[WARN] Warning: Failed to apply palettes: {e}")
                        print(f"   Error output: {e.stderr}")
                        print(f"   Copying tiles.png as-is")
                        shutil.copy2(tiles_png_path, output_png_path)
        else:
            # Just copy the tiles.png as-is
            shutil.copy2(tiles_png_path, output_png_path)
            print(f"[OK] Copied tileset image to {output_png_path}")
    else:
        print(f"[WARN] Warning: tiles.png not found at {tiles_png_path}")

    try:
        print(f"[OK] Converted tileset saved to {output_path}")
        print(f"   - {len(metatiles)} metatiles -> {len(tileset['tiles'])} tile properties")
        print(f"   - Tile count: {tile_count} (image: {img_width}x{img_height})")
        print(f"   - Image: {relative_image_path}")
    except UnicodeEncodeError:
        # Fallback for Windows console encoding issues
        print(f"[OK] Converted tileset saved to {output_path}")
        print(f"   - {len(metatiles)} metatiles -> {len(tileset['tiles'])} tile properties")
        print(f"   - Tile count: {tile_count} (image: {img_width}x{img_height})")
        print(f"   - Image: {relative_image_path}")


def main():
    """Command-line entry point."""
    import argparse
    
    parser = argparse.ArgumentParser(description='Convert pokeemerald tileset to Tiled 8x8 format')
    parser.add_argument('input_dir', type=Path, help='Input tileset directory')
    parser.add_argument('output_json', type=Path, help='Output JSON file path')
    parser.add_argument('--apply-palettes', action='store_true', help='Apply palettes to tiles.png')
    parser.add_argument('--tile-offset', type=int, default=0, help='Tile offset for secondary tilesets (default: 0, use 512 for secondary)')
    parser.add_argument('--render-palettes-script', type=Path, help='Path to render_tileset_with_palettes.py (auto-detected if not specified)')
    
    args = parser.parse_args()

    # Validate input
    if not args.input_dir.exists():
        print(f"[ERROR] Error: Input directory not found: {args.input_dir}")
        sys.exit(1)

    required_files = ["metatiles.bin", "metatile_attributes.bin", "tiles.png"]
    for filename in required_files:
        if not (args.input_dir / filename).exists():
            print(f"[ERROR] Error: Required file not found: {args.input_dir / filename}")
            sys.exit(1)

    # Convert
    try:
        convert_tileset_to_8x8_with_properties(
            args.input_dir,
            args.output_json,
            apply_palettes=args.apply_palettes,
            tile_offset=args.tile_offset,
            render_palettes_script=args.render_palettes_script
        )
    except Exception as e:
        print(f"[ERROR] Conversion failed: {e}")
        import traceback
        traceback.print_exc()
        sys.exit(1)


if __name__ == "__main__":
    main()
