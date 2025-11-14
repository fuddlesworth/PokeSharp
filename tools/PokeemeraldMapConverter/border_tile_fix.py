#!/usr/bin/env python3
"""Border tile handling for pokeemerald map conversion."""

import struct
from pathlib import Path
from typing import List, Optional


def load_border_bin(border_path: Path) -> List[int]:
    """Load 2x2 border metatile pattern from border.bin."""
    if not border_path.exists():
        raise FileNotFoundError(f"Border file not found: {border_path}")

    with open(border_path, 'rb') as f:
        data = f.read(8)

    if len(data) < 8:
        raise ValueError(f"Border file too small: expected 8 bytes, got {len(data)}")

    border_raw = struct.unpack('<4H', data)
    return [raw & 0x3FF for raw in border_raw]


def get_border_metatile(border_tiles: List[int], x: int, y: int) -> int:
    """Get border metatile for position using pokeemerald's checkerboard pattern."""
    if len(border_tiles) != 4:
        raise ValueError(f"Border tiles must have 4 elements, got {len(border_tiles)}")

    i = ((x + 1) & 1) + (((y + 1) & 1) * 2)
    return border_tiles[i]


def apply_border_fallback(map_data: List[List[int]],
                          border_tiles: Optional[List[int]] = None,
                          default_metatile: int = 2) -> List[List[int]]:
    """Apply border tile fallback for empty metatiles (ID 0 or 1)."""
    height = len(map_data)
    width = len(map_data[0]) if height > 0 else 0

    result = []
    for y in range(height):
        row = []
        for x in range(width):
            metatile_id = map_data[y][x]
            if metatile_id <= 1:
                metatile_id = get_border_metatile(border_tiles, x, y) if border_tiles else default_metatile
            row.append(metatile_id)
        result.append(row)

    return result


def get_statistics(map_data: List[List[int]]) -> dict:
    """Get statistics about metatile usage in map."""
    total = empty = 0
    unique_ids = set()

    for row in map_data:
        for metatile_id in row:
            total += 1
            unique_ids.add(metatile_id)
            if metatile_id <= 1:
                empty += 1

    return {
        'total_metatiles': total,
        'empty_count': empty,
        'empty_percent': (empty / total * 100) if total > 0 else 0,
        'unique_ids': unique_ids
    }
