#!/usr/bin/env python3
"""
Apply JASC .pal palette files to indexed-color tiles.png to create true-color output.
Pokemon Emerald stores tiles as indexed grayscale PNGs, with separate palette files.
"""

import sys
from pathlib import Path
from PIL import Image

def load_jasc_palette(pal_path: Path) -> list:
    """
    Load a JASC-PAL palette file.
    Format:
      JASC-PAL
      0100
      16
      R G B  (one color per line, 16 colors total)
    """
    with open(pal_path) as f:
        lines = [line.strip() for line in f if line.strip()]

    if lines[0] != "JASC-PAL":
        raise ValueError(f"Not a JASC-PAL file: {pal_path}")

    version = lines[1]
    num_colors = int(lines[2])

    palette = []
    for i in range(num_colors):
        line_idx = 3 + i
        if line_idx < len(lines):
            r, g, b = map(int, lines[line_idx].split())
            palette.append((r, g, b))

    return palette

def apply_palette_to_png(input_png: Path, palettes_dir: Path, output_png: Path):
    """
    Apply JASC palettes to an indexed PNG to create a true-color PNG.

    Args:
        input_png: Path to indexed grayscale tiles.png
        palettes_dir: Directory containing .pal palette files (00.pal, 01.pal, etc.)
        output_png: Path for output true-color PNG
    """
    # Load the indexed PNG
    img = Image.open(input_png)

    if img.mode not in ['P', 'L']:
        print(f"Warning: Image is already {img.mode} mode, not indexed. Copying as-is.")
        img.save(output_png)
        return

    # Load all palettes
    palettes = {}
    for pal_file in sorted(palettes_dir.glob('*.pal')):
        pal_num = int(pal_file.stem)
        palettes[pal_num] = load_jasc_palette(pal_file)

    if not palettes:
        raise ValueError(f"No .pal files found in {palettes_dir}")

    # Use palette 00 as the default
    default_palette = palettes.get(0, palettes[min(palettes.keys())])

    # Create a full 256-color palette (PIL expects 768 bytes: R,G,B for each of 256 colors)
    pil_palette = []
    for i in range(256):
        if i < len(default_palette):
            r, g, b = default_palette[i]
            pil_palette.extend([r, g, b])
        else:
            # Fill remaining slots with black
            pil_palette.extend([0, 0, 0])

    # Apply the palette
    img.putpalette(pil_palette)

    # Convert to RGB mode for Tiled compatibility
    rgb_img = img.convert('RGB')

    # Save as true-color PNG
    rgb_img.save(output_png, 'PNG')

    print(f"✅ Applied palette to {input_png.name}")
    print(f"   → Saved true-color PNG to {output_png}")
    print(f"   → Used palette 00.pal with {len(default_palette)} colors")

def main():
    if len(sys.argv) < 4:
        print("Usage: python apply_palettes.py <input_png> <palettes_dir> <output_png>")
        print()
        print("Example:")
        print("  python apply_palettes.py \\")
        print("    pokeemerald/data/tilesets/primary/general/tiles.png \\")
        print("    pokeemerald/data/tilesets/primary/general/palettes \\")
        print("    PokeSharp.Game/Assets/Tilesets/general_tiles.png")
        sys.exit(1)

    input_png = Path(sys.argv[1])
    palettes_dir = Path(sys.argv[2])
    output_png = Path(sys.argv[3])

    if not input_png.exists():
        print(f"❌ Error: Input PNG not found: {input_png}")
        sys.exit(1)

    if not palettes_dir.exists():
        print(f"❌ Error: Palettes directory not found: {palettes_dir}")
        sys.exit(1)

    output_png.parent.mkdir(parents=True, exist_ok=True)

    try:
        apply_palette_to_png(input_png, palettes_dir, output_png)
    except Exception as e:
        print(f"❌ Failed to apply palette: {e}")
        import traceback
        traceback.print_exc()
        sys.exit(1)

if __name__ == '__main__':
    main()
