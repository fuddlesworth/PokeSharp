#!/usr/bin/env python3
"""
Replace PNG embedded palette (PLTE chunk) with colors from JASC .pal file.
Uses only stdlib - no PIL/Pillow required!
"""

import struct
import zlib
from pathlib import Path

def load_jasc_palette(pal_path: Path) -> list:
    """Load JASC-PAL palette file, return list of (R,G,B) tuples."""
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

def replace_png_palette(input_png: Path, pal_file: Path, output_png: Path):
    """
    Replace PNG PLTE (palette) chunk with colors from JASC .pal file.
    """
    # Load JASC palette
    palette = load_jasc_palette(pal_file)
    print(f"Loaded {len(palette)} colors from {pal_file.name}")

    # Read input PNG
    with open(input_png, 'rb') as f:
        png_data = f.read()

    # Verify PNG signature
    if png_data[:8] != b'\x89PNG\r\n\x1a\n':
        raise ValueError(f"Not a valid PNG file: {input_png}")

    # Parse chunks and replace PLTE
    output_chunks = bytearray()
    output_chunks.extend(png_data[:8])  # Copy PNG signature

    pos = 8
    plte_replaced = False

    while pos < len(png_data):
        # Read chunk length and type
        length = struct.unpack('>I', png_data[pos:pos+4])[0]
        chunk_type = png_data[pos+4:pos+8]
        chunk_data = png_data[pos+8:pos+8+length]
        chunk_crc = png_data[pos+8+length:pos+12+length]

        if chunk_type == b'PLTE':
            # Replace PLTE chunk with new palette
            new_plte_data = bytearray()
            for r, g, b in palette:
                new_plte_data.extend([r, g, b])

            # Calculate new CRC
            crc_data = b'PLTE' + new_plte_data
            new_crc = struct.pack('>I', zlib.crc32(crc_data) & 0xffffffff)

            # Write new PLTE chunk
            output_chunks.extend(struct.pack('>I', len(new_plte_data)))
            output_chunks.extend(b'PLTE')
            output_chunks.extend(new_plte_data)
            output_chunks.extend(new_crc)

            plte_replaced = True
            print(f"✅ Replaced PLTE chunk ({len(new_plte_data)} bytes, {len(palette)} colors)")
        else:
            # Copy chunk as-is
            output_chunks.extend(png_data[pos:pos+12+length])

        pos += 12 + length

    if not plte_replaced:
        print("⚠️  Warning: No PLTE chunk found in PNG")

    # Write output PNG
    with open(output_png, 'wb') as f:
        f.write(output_chunks)

    print(f"✅ Saved to {output_png}")

def main():
    import sys

    if len(sys.argv) < 4:
        print("Usage: python replace_png_palette.py <input.png> <palette.pal> <output.png>")
        sys.exit(1)

    input_png = Path(sys.argv[1])
    pal_file = Path(sys.argv[2])
    output_png = Path(sys.argv[3])

    if not input_png.exists():
        print(f"❌ Error: Input PNG not found: {input_png}")
        sys.exit(1)

    if not pal_file.exists():
        print(f"❌ Error: Palette file not found: {pal_file}")
        sys.exit(1)

    try:
        replace_png_palette(input_png, pal_file, output_png)
    except Exception as e:
        print(f"❌ Error: {e}")
        import traceback
        traceback.print_exc()
        sys.exit(1)

if __name__ == '__main__':
    main()
