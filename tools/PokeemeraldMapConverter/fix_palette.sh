#!/bin/bash
# Replace PNG embedded palette with JASC .pal colors

INPUT_PNG="$1"
PAL_FILE="$2"
OUTPUT_PNG="$3"

# Extract palette colors (skip first 3 lines of JASC file)
# Create a 1x16 pixel gradient image with the palette colors
tail -n +4 "$PAL_FILE" | awk '
{
    printf "xc:\"rgb(%d,%d,%d)\" ", $1, $2, $3
}' | xargs convert +append /tmp/gradient_$$.png

# Now apply this gradient as the palette to the indexed PNG
convert "$INPUT_PNG" -remap /tmp/gradient_$$.png -type Palette "$OUTPUT_PNG"

rm -f /tmp/gradient_$$.png
echo "✅ Applied palette from $PAL_FILE"
echo "   → Saved to $OUTPUT_PNG"
