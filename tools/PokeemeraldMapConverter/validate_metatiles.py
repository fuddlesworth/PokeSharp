#!/usr/bin/env python3
"""
Metatile Validation Tool for Tiled Maps

Validates that all metatile groups in a Tiled map are complete and properly aligned.
This tool checks:
1. All metatile tiles have required properties (metatile_id, metatile_pos)
2. Each metatile has all 4 positions present (top_left, top_right, bottom_left, bottom_right)
3. Tiles within a metatile are properly aligned in a 2x2 grid
4. No orphaned or incomplete metatiles exist

Usage:
    python validate_metatiles.py <map_file.json>
    python validate_metatiles.py <map_file.json> --verbose
    python validate_metatiles.py <map_file.json> --fix

Exit codes:
    0 - All metatiles valid
    1 - Validation errors found
    2 - File not found or invalid JSON
"""

import json
import sys
from collections import defaultdict
from dataclasses import dataclass
from pathlib import Path
from typing import Dict, List, Optional, Set, Tuple


@dataclass
class TileInfo:
    """Information about a single tile in the map."""
    x: int
    y: int
    gid: int
    metatile_id: Optional[int] = None
    metatile_pos: Optional[str] = None
    layer_name: str = ""


@dataclass
class MetatileGroup:
    """A collection of tiles forming a metatile."""
    metatile_id: int
    tiles: Dict[str, TileInfo]

    def is_complete(self) -> bool:
        """Check if all 4 positions are present."""
        required = {"top_left", "top_right", "bottom_left", "bottom_right"}
        return required.issubset(self.tiles.keys())

    def get_missing_positions(self) -> List[str]:
        """Get list of missing positions."""
        required = {"top_left", "top_right", "bottom_left", "bottom_right"}
        return sorted(required - self.tiles.keys())

    def is_aligned(self) -> Tuple[bool, Optional[str]]:
        """
        Check if tiles form a proper 2x2 grid.
        Returns (is_aligned, error_message).
        """
        if not self.is_complete():
            return False, "Incomplete metatile"

        tl = self.tiles["top_left"]
        tr = self.tiles["top_right"]
        bl = self.tiles["bottom_left"]
        br = self.tiles["bottom_right"]

        # Check top_right is one tile to the right
        if tr.x != tl.x + 1 or tr.y != tl.y:
            return False, f"top_right at ({tr.x}, {tr.y}) should be at ({tl.x + 1}, {tl.y})"

        # Check bottom_left is one tile below
        if bl.x != tl.x or bl.y != tl.y + 1:
            return False, f"bottom_left at ({bl.x}, {bl.y}) should be at ({tl.x}, {tl.y + 1})"

        # Check bottom_right is diagonal from top_left
        if br.x != tl.x + 1 or br.y != tl.y + 1:
            return False, f"bottom_right at ({br.x}, {br.y}) should be at ({tl.x + 1}, {tl.y + 1})"

        return True, None


class MetatileValidator:
    """Validates metatile integrity in Tiled maps."""

    def __init__(self, verbose: bool = False):
        self.verbose = verbose
        self.errors: List[str] = []
        self.warnings: List[str] = []
        self.tileset_cache: Dict[str, dict] = {}

    def log(self, message: str) -> None:
        """Log verbose messages."""
        if self.verbose:
            print(f"[INFO] {message}")

    def load_tileset(self, tileset_path: Path) -> Optional[dict]:
        """Load a Tiled tileset file."""
        if tileset_path in self.tileset_cache:
            return self.tileset_cache[tileset_path]

        try:
            with open(tileset_path, 'r', encoding='utf-8') as f:
                tileset = json.load(f)
                self.tileset_cache[tileset_path] = tileset
                return tileset
        except Exception as e:
            self.errors.append(f"Failed to load tileset {tileset_path}: {e}")
            return None

    def get_tile_properties(self, gid: int, map_dir: Path, tilesets: List[dict]) -> Dict[str, any]:
        """
        Get properties for a tile GID.
        Handles both embedded and external tilesets.
        """
        if gid == 0:
            return {}

        # Find the tileset for this GID
        for tileset_ref in tilesets:
            first_gid = tileset_ref.get('firstgid', 1)

            # Load tileset data
            if 'source' in tileset_ref:
                # External tileset
                tileset_path = map_dir / tileset_ref['source']
                tileset = self.load_tileset(tileset_path)
                if not tileset:
                    continue
            else:
                # Embedded tileset
                tileset = tileset_ref

            tile_count = tileset.get('tilecount', 0)

            # Check if this GID belongs to this tileset
            if first_gid <= gid < first_gid + tile_count:
                local_id = gid - first_gid

                # Find tile properties
                for tile in tileset.get('tiles', []):
                    if tile.get('id') == local_id:
                        props = {}
                        for prop in tile.get('properties', []):
                            props[prop['name']] = prop['value']
                        return props

                return {}

        return {}

    def extract_tiles(self, map_data: dict, map_dir: Path) -> List[TileInfo]:
        """Extract all tiles with metatile properties from the map."""
        tiles = []
        tilesets = map_data.get('tilesets', [])

        for layer in map_data.get('layers', []):
            if layer.get('type') != 'tilelayer':
                continue

            layer_name = layer.get('name', 'unnamed')
            width = layer.get('width', 0)
            height = layer.get('height', 0)
            data = layer.get('data', [])

            self.log(f"Processing layer '{layer_name}' ({width}x{height})")

            for y in range(height):
                for x in range(width):
                    idx = y * width + x
                    if idx >= len(data):
                        continue

                    gid = data[idx]
                    if gid == 0:
                        continue

                    props = self.get_tile_properties(gid, map_dir, tilesets)

                    if 'metatile_id' in props:
                        tile = TileInfo(
                            x=x,
                            y=y,
                            gid=gid,
                            metatile_id=props.get('metatile_id'),
                            metatile_pos=props.get('metatile_pos'),
                            layer_name=layer_name
                        )
                        tiles.append(tile)

        self.log(f"Found {len(tiles)} tiles with metatile properties")
        return tiles

    def group_tiles_by_metatile(self, tiles: List[TileInfo]) -> Dict[int, MetatileGroup]:
        """Group tiles by their metatile_id."""
        metatiles = defaultdict(lambda: defaultdict(list))

        for tile in tiles:
            if tile.metatile_id is None:
                self.warnings.append(
                    f"Tile at ({tile.x}, {tile.y}) has metatile_id property but no value"
                )
                continue

            if not tile.metatile_pos:
                self.errors.append(
                    f"Tile at ({tile.x}, {tile.y}) has metatile_id {tile.metatile_id} "
                    f"but missing metatile_pos property"
                )
                continue

            # Normalize position string
            pos = tile.metatile_pos.lower().strip()
            pos_map = {
                'tl': 'top_left', 'tr': 'top_right',
                'bl': 'bottom_left', 'br': 'bottom_right'
            }
            pos = pos_map.get(pos, pos)

            if pos not in ['top_left', 'top_right', 'bottom_left', 'bottom_right']:
                self.errors.append(
                    f"Tile at ({tile.x}, {tile.y}) has invalid metatile_pos: '{tile.metatile_pos}'"
                )
                continue

            metatiles[tile.metatile_id][pos].append(tile)

        # Convert to MetatileGroup objects and check for duplicates
        result = {}
        for mid, positions in metatiles.items():
            group = MetatileGroup(metatile_id=mid, tiles={})

            for pos, tile_list in positions.items():
                if len(tile_list) > 1:
                    positions_str = ', '.join(f"({t.x}, {t.y})" for t in tile_list)
                    self.errors.append(
                        f"Metatile {mid} has duplicate {pos} tiles at: {positions_str}"
                    )
                    # Use the first one for validation
                    group.tiles[pos] = tile_list[0]
                else:
                    group.tiles[pos] = tile_list[0]

            result[mid] = group

        return result

    def validate_metatile_completeness(self, metatiles: Dict[int, MetatileGroup]) -> bool:
        """Check that all metatiles are complete (have all 4 positions)."""
        all_complete = True

        for mid, group in metatiles.items():
            if not group.is_complete():
                missing = group.get_missing_positions()
                present_positions = ', '.join(
                    f"{pos} at ({tile.x}, {tile.y})"
                    for pos, tile in group.tiles.items()
                )
                self.errors.append(
                    f"Metatile {mid} is incomplete. Missing: {', '.join(missing)}. "
                    f"Present: {present_positions}"
                )
                all_complete = False

        return all_complete

    def validate_metatile_alignment(self, metatiles: Dict[int, MetatileGroup]) -> bool:
        """Check that all metatiles are properly aligned in 2x2 grids."""
        all_aligned = True

        for mid, group in metatiles.items():
            if not group.is_complete():
                continue  # Already reported in completeness check

            is_aligned, error = group.is_aligned()
            if not is_aligned:
                tl = group.tiles["top_left"]
                self.errors.append(
                    f"Metatile {mid} at anchor ({tl.x}, {tl.y}) is not properly aligned: {error}"
                )
                all_aligned = False

        return all_aligned

    def validate_map(self, map_path: Path) -> bool:
        """
        Validate all metatiles in a Tiled map.
        Returns True if all validation passes, False otherwise.
        """
        self.errors.clear()
        self.warnings.clear()

        self.log(f"Validating map: {map_path}")

        # Load map
        try:
            with open(map_path, 'r', encoding='utf-8') as f:
                map_data = json.load(f)
        except FileNotFoundError:
            self.errors.append(f"Map file not found: {map_path}")
            return False
        except json.JSONDecodeError as e:
            self.errors.append(f"Invalid JSON in map file: {e}")
            return False

        map_dir = map_path.parent

        # Extract tiles with metatile properties
        tiles = self.extract_tiles(map_data, map_dir)

        if not tiles:
            self.log("No metatile tiles found in map")
            return True

        # Group tiles by metatile
        metatiles = self.group_tiles_by_metatile(tiles)
        self.log(f"Found {len(metatiles)} unique metatiles")

        # Validate completeness
        completeness_valid = self.validate_metatile_completeness(metatiles)

        # Validate alignment
        alignment_valid = self.validate_metatile_alignment(metatiles)

        return completeness_valid and alignment_valid and len(self.errors) == 0

    def print_report(self) -> None:
        """Print validation report."""
        if self.warnings:
            print(f"\n⚠️  {len(self.warnings)} Warning(s):")
            for warning in self.warnings:
                print(f"  - {warning}")

        if self.errors:
            print(f"\n❌ {len(self.errors)} Error(s):")
            for error in self.errors:
                print(f"  - {error}")
        else:
            print("\n✅ All metatile validation checks passed!")


def main():
    """Main entry point."""
    if len(sys.argv) < 2:
        print(__doc__)
        sys.exit(2)

    map_path = Path(sys.argv[1])
    verbose = '--verbose' in sys.argv or '-v' in sys.argv

    if not map_path.exists():
        print(f"Error: Map file not found: {map_path}")
        sys.exit(2)

    validator = MetatileValidator(verbose=verbose)
    is_valid = validator.validate_map(map_path)
    validator.print_report()

    sys.exit(0 if is_valid else 1)


if __name__ == '__main__':
    main()
