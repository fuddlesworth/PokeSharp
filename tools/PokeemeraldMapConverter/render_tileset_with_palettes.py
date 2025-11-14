#!/usr/bin/env python3
"""
Render pokeemerald indexed tiles.png with proper palettes per tile.
Pokemon Emerald tiles reference which of 16 palettes to use (stored in tile data bits 12-15).
"""

import struct
import sys
from pathlib import Path

def load_jasc_palette(pal_path: Path) -> list:
    """Load JASC-PAL palette file."""
    with open(pal_path) as f:
        lines = [line.strip() for line in f if line.strip()]

    if lines[0] != "JASC-PAL":
        raise ValueError(f"Not a JASC-PAL file: {pal_path}")

    num_colors = int(lines[2])
    palette = []

    for i in range(num_colors):
        line_idx = 3 + i
        if line_idx < len(lines):
            r, g, b = map(int, lines[line_idx].split())
            palette.append((r, g, b))

    return palette

def load_all_palettes(palettes_dir: Path) -> dict:
    """Load all .pal files from directory."""
    palettes = {}
    for pal_file in sorted(palettes_dir.glob('*.pal')):
        pal_num = int(pal_file.stem)
        palettes[pal_num] = load_jasc_palette(pal_file)
    return palettes

def read_png_indexed_data(png_path: Path):
    """
    Read indexed PNG data and palette.
    Returns: (width, height, pixel_data, embedded_palette)
    """
    # Read PNG chunks
    with open(png_path, 'rb') as f:
        png_data = f.read()

    if png_data[:8] != b'\x89PNG\r\n\x1a\n':
        raise ValueError("Not a valid PNG")

    width = struct.unpack('>I', png_data[16:20])[0]
    height = struct.unpack('>I', png_data[20:24])[0]

    # Find PLTE and IDAT chunks
    palette_data = None
    idat_data = bytearray()

    pos = 8
    while pos < len(png_data):
        length = struct.unpack('>I', png_data[pos:pos+4])[0]
        chunk_type = png_data[pos+4:pos+8]
        chunk_data = png_data[pos+8:pos+8+length]

        if chunk_type == b'PLTE':
            palette_data = chunk_data
        elif chunk_type == b'IDAT':
            idat_data.extend(chunk_data)

        pos += 12 + length

    # Decompress IDAT
    import zlib
    raw_data = zlib.decompress(bytes(idat_data))

    # Parse scanlines (each scanline has 1 filter byte + pixel data)
    # For 4-bit indexed: width/2 bytes per scanline (2 pixels per byte)
    pixels = []
    bytes_per_scanline = (width + 1) // 2  # 4-bit = 2 pixels per byte

    for y in range(height):
        scanline_start = y * (bytes_per_scanline + 1) + 1  # +1 for filter byte
        scanline_data = raw_data[scanline_start:scanline_start + bytes_per_scanline]

        row_pixels = []
        for byte_val in scanline_data:
            # Each byte contains 2 pixels (4 bits each)
            pixel1 = (byte_val >> 4) & 0x0F
            pixel2 = byte_val & 0x0F
            row_pixels.extend([pixel1, pixel2])

        pixels.append(row_pixels[:width])  # Trim to exact width

    return width, height, pixels, palette_data

def get_tile_palette_index(metatiles_bin: Path, tile_idx: int) -> int:
    """
    Find which palette a tile uses by scanning metatiles.bin.
    Returns palette index (0-15).

    IMPORTANT: Each metatile is 16 bytes (8 tiles), not 8 bytes!
    """
    with open(metatiles_bin, 'rb') as f:
        data = f.read()

    # Scan all metatiles to find this tile (16 bytes per metatile)
    for i in range(len(data) // 16):
        offset = i * 16
        raw_tiles = struct.unpack('<8H', data[offset:offset+16])  # 8 tiles per metatile

        for raw_tile in raw_tiles:
            tile_id = raw_tile & 0x3FF
            palette = (raw_tile >> 12) & 0xF

            if tile_id == tile_idx:
                return palette

    return 0  # Default to palette 0

def render_tileset_with_palettes(tiles_png: Path, palettes_dir: Path, metatiles_bin: Path, output_png: Path, tile_offset: int = 0):
    """
    Render indexed tiles.png as true-color PNG with correct palette per tile.

    Args:
        tile_offset: For secondary tilesets, set to 512 (tiles in metatiles.bin use absolute indices)
    """
    print(f"Loading indexed PNG: {tiles_png}")
    width, height, pixels, _ = read_png_indexed_data(tiles_png)

    print(f"Loading palettes from: {palettes_dir}")
    palettes = load_all_palettes(palettes_dir)
    print(f"Loaded {len(palettes)} palettes")

    if tile_offset > 0:
        print(f"Using tile offset: {tile_offset} (secondary tileset)")

    # Create output RGBA pixels (with transparency)
    rgba_pixels = []

    # Process each 8x8 tile
    tile_size = 8
    tiles_per_row = width // tile_size
    tiles_per_col = height // tile_size

    for tile_y in range(tiles_per_col):
        for row_in_tile in range(tile_size):
            y = tile_y * tile_size + row_in_tile
            row_rgba = []

            for tile_x in range(tiles_per_row):
                # Get tile index in the PNG
                tile_idx_in_png = tile_y * tiles_per_row + tile_x

                # For secondary tilesets, add offset to get absolute index
                tile_idx_absolute = tile_idx_in_png + tile_offset

                # Get which palette this tile uses (using absolute index)
                palette_idx = get_tile_palette_index(metatiles_bin, tile_idx_absolute)
                palette = palettes.get(palette_idx, palettes[0])

                # Render 8 pixels of this tile
                for col_in_tile in range(tile_size):
                    x = tile_x * tile_size + col_in_tile
                    color_idx = pixels[y][x]

                    # Color index 0 is TRANSPARENT in Pokemon GBA
                    # Render as fully transparent (alpha = 0)
                    if color_idx == 0:
                        r, g, b, a = 0, 0, 0, 0  # Fully transparent
                    elif color_idx < len(palette):
                        r, g, b = palette[color_idx]
                        a = 255  # Fully opaque
                    else:
                        r, g, b, a = 0, 0, 0, 255  # Black, opaque

                    row_rgba.extend([r, g, b, a])

            rgba_pixels.append(row_rgba)

    # Write PNG with alpha channel
    write_rgba_png(output_png, width, height, rgba_pixels)
    print(f"[OK] Rendered tileset to {output_png}")

def write_rgba_png(output_path: Path, width: int, height: int, rgba_pixels):
    """Write RGBA pixels as PNG with transparency."""
    import zlib

    # Create PNG signature
    png_data = bytearray(b'\x89PNG\r\n\x1a\n')

    # IHDR chunk - color type 6 = RGBA (with alpha channel)
    ihdr_data = struct.pack('>IIBBBBB', width, height, 8, 6, 0, 0, 0)  # 8-bit RGBA
    png_data.extend(make_chunk(b'IHDR', ihdr_data))

    # IDAT chunk
    raw_data = bytearray()
    for row in rgba_pixels:
        raw_data.append(0)  # Filter type: None
        raw_data.extend(row)

    compressed = zlib.compress(bytes(raw_data), 9)
    png_data.extend(make_chunk(b'IDAT', compressed))

    # IEND chunk
    png_data.extend(make_chunk(b'IEND', b''))

    with open(output_path, 'wb') as f:
        f.write(png_data)

def make_chunk(chunk_type: bytes, data: bytes) -> bytes:
    """Create PNG chunk with CRC."""
    import zlib
    length = struct.pack('>I', len(data))
    crc = struct.pack('>I', zlib.crc32(chunk_type + data) & 0xffffffff)
    return length + chunk_type + data + crc

def main():
    if len(sys.argv) < 5:
        print("Usage: python render_tileset_with_palettes.py <tiles.png> <palettes_dir> <metatiles.bin> <output.png> [tile_offset]")
        print()
        print("Example (primary tileset):")
        print("  python render_tileset_with_palettes.py \\")
        print("    pokeemerald/data/tilesets/primary/general/tiles.png \\")
        print("    pokeemerald/data/tilesets/primary/general/palettes \\")
        print("    pokeemerald/data/tilesets/primary/general/metatiles.bin \\")
        print("    PokeSharp.Game/Assets/Tilesets/general_tiles.png")
        print()
        print("Example (secondary tileset - needs offset 512):")
        print("  python render_tileset_with_palettes.py \\")
        print("    pokeemerald/data/tilesets/secondary/petalburg/tiles.png \\")
        print("    pokeemerald/data/tilesets/secondary/petalburg/palettes \\")
        print("    pokeemerald/data/tilesets/secondary/petalburg/metatiles.bin \\")
        print("    PokeSharp.Game/Assets/Tilesets/petalburg_tiles.png 512")
        sys.exit(1)

    tiles_png = Path(sys.argv[1])
    palettes_dir = Path(sys.argv[2])
    metatiles_bin = Path(sys.argv[3])
    output_png = Path(sys.argv[4])
    tile_offset = int(sys.argv[5]) if len(sys.argv) > 5 else 0

    if not tiles_png.exists():
        print(f"[ERROR] tiles.png not found: {tiles_png}")
        sys.exit(1)

    if not palettes_dir.exists():
        print(f"[ERROR] palettes directory not found: {palettes_dir}")
        sys.exit(1)

    if not metatiles_bin.exists():
        print(f"[ERROR] metatiles.bin not found: {metatiles_bin}")
        sys.exit(1)

    try:
        render_tileset_with_palettes(tiles_png, palettes_dir, metatiles_bin, output_png, tile_offset)
    except Exception as e:
        print(f"[ERROR] Error: {e}")
        import traceback
        traceback.print_exc()
        sys.exit(1)

if __name__ == '__main__':
    main()
