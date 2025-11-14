#!/usr/bin/env python3
"""
Pokemon Emerald Map Converter to Tiled 8x8 Format
Converts pokeemerald metatile-based maps to Tiled JSON with 8x8 tiles and metatile properties.
"""

import json
import struct
import argparse
from dataclasses import dataclass
from pathlib import Path
from typing import Dict, List, Any, Optional

# Import border tile fallback functions
from border_tile_fix import load_border_bin, get_border_metatile

# Behavior mapping (subset of pokeemerald behavior table)
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
    0x25: "MB_SURF",
    0x27: "MB_DOOR",
    0x33: "MB_IMPASSABLE",
}


class MetatileAttributes:
    """Represents the 16-bit behavior/layer value stored in metatile_attributes.bin."""

    def __init__(self, raw_value: int):
        self.raw_value = raw_value
        self.behavior = raw_value & 0x00FF
        self.layer_type = (raw_value >> 12) & 0xF

    @classmethod
    def from_bytes(cls, data: bytes) -> 'MetatileAttributes':
        value = struct.unpack('<H', data)[0]
        return cls(value)

    def get_behavior_name(self) -> str:
        return BEHAVIOR_NAMES.get(self.behavior, f"MB_UNKNOWN_{self.behavior:02X}")


@dataclass
class TileRef:
    gid: int
    h_flip: bool
    v_flip: bool


class MetatileDefinition:
    """Represents a metatile from pokeemerald tileset."""

    def __init__(self, metatile_id: int, tiles: List[TileRef],
                 behavior: int = 0, layer_type: int = 0,
                 top_tiles: List[TileRef] = None):
        self.metatile_id = metatile_id
        self.tiles = tiles
        self.top_tiles = top_tiles or [TileRef(0, False, False) for _ in range(4)]
        self.behavior = behavior
        self.behavior_name = BEHAVIOR_NAMES.get(behavior, f"MB_UNKNOWN_{behavior:02X}")
        self.layer_type = layer_type


class TilesetData:
    """Holds tileset metatile definitions."""

    def __init__(self):
        self.metatiles: Dict[int, MetatileDefinition] = {}

    def add_metatile(self, metatile: MetatileDefinition):
        self.metatiles[metatile.metatile_id] = metatile

    def get_metatile(self, metatile_id: int) -> Optional[MetatileDefinition]:
        return self.metatiles.get(metatile_id)


def load_layout_json(layout_path: Path) -> Dict[str, Any]:
    """Load pokeemerald layout.json file."""
    # Use utf-8-sig to handle UTF-8 BOM that some layout.json files have
    with open(layout_path, 'r', encoding='utf-8-sig') as f:
        return json.load(f)


def load_map_bin(map_path: Path, width: int, height: int) -> List[List[int]]:
    """
    Load pokeemerald map.bin file containing metatile IDs.
    Format: 16-bit little-endian metatile IDs with layer info in upper bits.
    Lower 10 bits (0x3FF) = metatile ID (0-1023)
    Upper 6 bits = collision/layer flags
    """
    with open(map_path, 'rb') as f:
        data = f.read()

    cell_count = width * height
    raw_values = struct.unpack(f'<{cell_count}H', data[:cell_count * 2])

    map_rows = []
    for y in range(height):
        row = []
        for x in range(width):
            idx = y * width + x
            row.append(raw_values[idx])
        map_rows.append(row)

    return map_rows


def load_metatiles_from_pokeemerald(metatiles_bin_path: Path,
                                    attributes_bin_path: Path) -> TilesetData:
    """
    Load metatile definitions from pokeemerald binary files.

    Args:
        metatiles_bin_path: Path to metatiles.bin
        attributes_bin_path: Path to metatile_attributes.bin

    Returns:
        TilesetData with metatile definitions
    """
    tileset = TilesetData()

    if not metatiles_bin_path.exists():
        return tileset

    with open(metatiles_bin_path, 'rb') as f:
        metatiles_data = f.read()

    attributes = []
    if attributes_bin_path.exists():
        with open(attributes_bin_path, 'rb') as f:
            attributes_data = f.read()

        for i in range(0, len(attributes_data), 2):
            attr_bytes = attributes_data[i:i+2]
            if len(attr_bytes) == 2:
                attributes.append(MetatileAttributes.from_bytes(attr_bytes))

    # Each metatile is 16 bytes: bottom layer (8 bytes) + top layer (8 bytes)
    metatile_count = len(metatiles_data) // 16
    for metatile_id in range(metatile_count):
        offset = metatile_id * 16
        if offset + 16 > len(metatiles_data):
            break

        raw_tiles = struct.unpack('<8H', metatiles_data[offset:offset+16])

        def parse_tile(raw: int) -> TileRef:
            tile_idx = raw & 0x3FF
            gid = tile_idx + 1  # Tiled GIDs are 1-based
            h_flip = bool(raw & 0x400)
            v_flip = bool(raw & 0x800)
            return TileRef(gid=gid, h_flip=h_flip, v_flip=v_flip)

        bottom_refs = [parse_tile(tile) for tile in raw_tiles[0:4]]
        top_refs = [parse_tile(tile) for tile in raw_tiles[4:8]]

        attr = attributes[metatile_id] if metatile_id < len(attributes) else MetatileAttributes(0)

        tileset.add_metatile(MetatileDefinition(
            metatile_id=metatile_id,
            tiles=bottom_refs,
            top_tiles=top_refs,
            behavior=attr.behavior,
            layer_type=attr.layer_type
        ))

    return tileset


def convert_pokeemerald_map(layout_path: Path,
                           primary_tileset_dir: Path,
                           output_path: Path,
                           secondary_tileset_dir: Optional[Path] = None,
                           primary_tileset_json: str = "../Tilesets/general.json",
                           secondary_tileset_json: Optional[str] = None) -> None:
    """
    Converts pokeemerald map to Tiled format with 8x8 tiles.

    Args:
        layout_path: Path to layout.json
        primary_tileset_dir: Directory containing primary tileset binary files (metatiles.bin, etc.)
        output_path: Output path for Tiled JSON map
        secondary_tileset_dir: Optional directory containing secondary tileset binary files
        primary_tileset_json: Relative path to reference the primary tileset JSON in output
        secondary_tileset_json: Relative path to reference the secondary tileset JSON in output
    """
    layout = load_layout_json(layout_path)
    width_metatiles = layout['width']
    height_metatiles = layout['height']

    map_bin_path = Path(layout['blockdata_filepath'])
    if not map_bin_path.is_absolute():
        # blockdata_filepath is relative to pokeemerald root, not layout.json directory
        # If it starts with "data/" or "data\", resolve from pokeemerald root
        # Otherwise, resolve relative to layout_path's parent
        map_bin_str = str(map_bin_path).replace('\\', '/')
        if map_bin_str.startswith('data/'):
            # Go up from data/layouts/MapName/layout.json to pokeemerald root
            # layout_path.parent = data/layouts/MapName
            # layout_path.parent.parent = data/layouts
            # layout_path.parent.parent.parent = data
            # layout_path.parent.parent.parent.parent = pokeemerald root
            pokeemerald_root = layout_path.parent.parent.parent.parent
            map_bin_path = pokeemerald_root / map_bin_path
        else:
            map_bin_path = layout_path.parent / map_bin_path

    map_data = load_map_bin(map_bin_path, width_metatiles, height_metatiles)
    height_metatiles = len(map_data)
    width_metatiles = len(map_data[0]) if height_metatiles > 0 else 0
    border_tiles = None
    border_filepath = layout.get('border_filepath', 'border.bin')
    border_bin_path = Path(border_filepath)
    if not border_bin_path.is_absolute():
        border_bin_str = str(border_bin_path).replace('\\', '/')
        if border_bin_str.startswith('data/'):
            pokeemerald_root = layout_path.parent.parent.parent.parent
            border_bin_path = pokeemerald_root / border_bin_path
        else:
            border_bin_path = layout_path.parent / border_bin_path
    if border_bin_path.exists():
        try:
            border_tiles = load_border_bin(border_bin_path)
        except Exception as exc:
            print(f"[WARN] Failed to load border from {border_bin_path}: {exc}")
    else:
        print(f"[WARN] Border file not found for {layout_path.name}, using default border metatile")

    primary_metatiles_bin = primary_tileset_dir / "metatiles.bin"
    primary_attributes_bin = primary_tileset_dir / "metatile_attributes.bin"
    primary_tileset = load_metatiles_from_pokeemerald(
        primary_metatiles_bin,
        primary_attributes_bin
    )

    secondary_tileset = TilesetData()
    if secondary_tileset_dir and secondary_tileset_dir.exists():
        secondary_metatiles_bin = secondary_tileset_dir / "metatiles.bin"
        secondary_attributes_bin = secondary_tileset_dir / "metatile_attributes.bin"
        secondary_tileset = load_metatiles_from_pokeemerald(
            secondary_metatiles_bin,
            secondary_attributes_bin
        )

    combined_tileset = TilesetData()
    for mid, metatile in primary_tileset.metatiles.items():
        combined_tileset.add_metatile(metatile)

    for mid, metatile in secondary_tileset.metatiles.items():
        combined_tileset.add_metatile(MetatileDefinition(
            metatile_id=mid + 512,
            tiles=metatile.tiles,
            top_tiles=metatile.top_tiles,
            behavior=metatile.behavior,
            layer_type=metatile.layer_type
        ))

    tileset = combined_tileset
    width_tiles = width_metatiles * 2
    height_tiles = height_metatiles * 2
    bottom_layer_data = []
    top_layer_data = []

    H_FLIP_BIT = 0x80000000
    V_FLIP_BIT = 0x40000000

    def apply_flip(tile_ref: Optional[TileRef]) -> int:
        if not tile_ref or tile_ref.gid == 0:
            return 0
        gid = tile_ref.gid
        if tile_ref.h_flip:
            gid |= H_FLIP_BIT
        if tile_ref.v_flip:
            gid |= V_FLIP_BIT
        return gid

    for y in range(height_tiles):
        for x in range(width_tiles):
            raw_value = map_data[y // 2][x // 2]
            metatile_id = raw_value & 0x3FF
            tile_index = (y % 2) * 2 + (x % 2)

            metatile = tileset.get_metatile(metatile_id)
            if metatile:
                bottom_ref = metatile.tiles[tile_index] if len(metatile.tiles) > tile_index else None
                top_ref = metatile.top_tiles[tile_index] if len(metatile.top_tiles) > tile_index else None
            else:
                bottom_ref = top_ref = None

            bottom_layer_data.append(apply_flip(bottom_ref))
            top_layer_data.append(apply_flip(top_ref))

    # Build tileset references
    tileset_refs = [
        {
            "firstgid": 1,
            "source": primary_tileset_json
        }
    ]
    if secondary_tileset_json and len(secondary_tileset.metatiles) > 0:
        tileset_refs.append({
            "firstgid": 513,
            "source": secondary_tileset_json
        })
    tiled_map = {
        "version": "1.10",
        "tiledversion": "1.10.0",
        "type": "map",
        "orientation": "orthogonal",
        "renderorder": "right-down",
        "width": width_tiles,
        "height": height_tiles,
        "tilewidth": 8,
        "tileheight": 8,
        "infinite": False,
        "nextlayerid": 3,
        "nextobjectid": 1,
        "layers": [
            {
                "id": 1,
                "name": "Ground Layer",
                "type": "tilelayer",
                "visible": True,
                "opacity": 1,
                "x": 0,
                "y": 0,
                "width": width_tiles,
                "height": height_tiles,
                "data": bottom_layer_data
            },
            {
                "id": 2,
                "name": "Overlay Layer",
                "type": "tilelayer",
                "visible": True,
                "opacity": 1,
                "x": 0,
                "y": 0,
                "width": width_tiles,
                "height": height_tiles,
                "data": top_layer_data
            }
        ],
        "tilesets": tileset_refs
    }

    properties = []
    if border_tiles:
        properties.append({
            "name": "border_pattern_metatiles",
            "type": "string",
            "value": ",".join(str(mt) for mt in border_tiles)
        })
    if properties:
        tiled_map["properties"] = properties

    # Build metatile object layer with metadata
    metatile_objects = []
    object_id = 1
    for my in range(height_metatiles):
        for mx in range(width_metatiles):
            raw_value = map_data[my][mx]
            metatile_id = raw_value & 0x3FF
            collision_bits = (raw_value >> 10) & 0x3
            elevation = (raw_value >> 12) & 0xF
            metatile_def = tileset.get_metatile(metatile_id)
            props = [
                {"name": "metatile_id", "type": "int", "value": metatile_id},
                {"name": "collision_bits", "type": "int", "value": collision_bits},
                {"name": "is_solid", "type": "bool", "value": collision_bits == 3},
                {"name": "elevation", "type": "int", "value": elevation}
            ]
            if metatile_def:
                props.extend([
                    {"name": "behavior", "type": "string", "value": metatile_def.behavior_name},
                    {"name": "layer_type", "type": "int", "value": metatile_def.layer_type}
                ])
            metatile_objects.append({
                "id": object_id,
                "name": str(metatile_id),
                "type": "metatile",
                "x": mx * 16,
                "y": my * 16,
                "width": 16,
                "height": 16,
                "properties": props
            })
            object_id += 1

    metatile_object_layer = {
        "id": len(tiled_map["layers"]) + 1,
        "name": "MetatileData",
        "type": "objectgroup",
        "visible": False,
        "draworder": "topdown",
        "objects": metatile_objects
    }
    tiled_map["layers"].append(metatile_object_layer)

    output_path.parent.mkdir(parents=True, exist_ok=True)
    with open(output_path, 'w') as f:
        json.dump(tiled_map, f, indent=2)

    print(f"[OK] Converted {width_metatiles}x{height_metatiles} metatiles -> {width_tiles}x{height_tiles} tiles (8x8)")


def main():
    parser = argparse.ArgumentParser(
        description='Convert Pokemon Emerald maps to Tiled 8x8 format using pokeemerald binary data'
    )
    parser.add_argument('layout', type=Path, help='Path to layout.json')
    parser.add_argument('primary_tileset_dir', type=Path,
                       help='Directory with primary tileset binaries (metatiles.bin, metatile_attributes.bin)')
    parser.add_argument('output', type=Path, help='Output path for Tiled map JSON')
    parser.add_argument('--secondary-tileset-dir', type=Path, default=None,
                       help='Directory with secondary tileset binaries (optional)')
    parser.add_argument('--primary-tileset-json', default='../../Tilesets/general.json',
                       help='Relative path to reference primary tileset JSON (default: ../../Tilesets/general.json)')
    parser.add_argument('--secondary-tileset-json', default='../../Tilesets/petalburg.json',
                       help='Relative path to reference secondary tileset JSON (default: ../../Tilesets/petalburg.json)')

    args = parser.parse_args()

    convert_pokeemerald_map(
        layout_path=args.layout,
        primary_tileset_dir=args.primary_tileset_dir,
        output_path=args.output,
        secondary_tileset_dir=args.secondary_tileset_dir,
        primary_tileset_json=args.primary_tileset_json,
        secondary_tileset_json=args.secondary_tileset_json
    )


if __name__ == '__main__':
    main()
