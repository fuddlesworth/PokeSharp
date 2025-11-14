#!/bin/bash
# Apply JASC .pal palette to indexed PNG using ImageMagick

if [ "$#" -ne 3 ]; then
    echo "Usage: $0 <input_png> <palette_00.pal> <output_png>"
    echo
    echo "Example:"
    echo "  $0 tiles.png palettes/00.pal output_tiles.png"
    exit 1
fi

INPUT_PNG="$1"
PALETTE_FILE="$2"
OUTPUT_PNG="$3"

if [ ! -f "$INPUT_PNG" ]; then
    echo "❌ Error: Input PNG not found: $INPUT_PNG"
    exit 1
fi

if [ ! -f "$PALETTE_FILE" ]; then
    echo "❌ Error: Palette file not found: $PALETTE_FILE"
    exit 1
fi

# Convert JASC palette to ImageMagick format
# JASC format:
#   JASC-PAL
#   0100
#   16
#   R G B  (space-separated, one per line)
#
# ImageMagick format needs: "rgb(R,G,B)" lines

TEMP_PAL="/tmp/palette_$$.txt"

# Skip first 3 lines, convert remaining to rgb(R,G,B) format
tail -n +4 "$PALETTE_FILE" | awk '{printf "rgb(%d,%d,%d)\n", $1, $2, $3}' > "$TEMP_PAL"

# Apply palette using ImageMagick
# This remaps the indexed colors to the new palette
convert "$INPUT_PNG" -remap "$TEMP_PAL" "$OUTPUT_PNG"

if [ $? -eq 0 ]; then
    echo "✅ Applied palette to $(basename $INPUT_PNG)"
    echo "   → Saved to $OUTPUT_PNG"
    rm -f "$TEMP_PAL"
else
    echo "❌ Failed to apply palette"
    rm -f "$TEMP_PAL"
    exit 1
fi
