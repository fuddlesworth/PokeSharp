#!/bin/bash
# Bash script to apply palettes to all tilesets
# Usage: ./apply_palettes_all.sh /path/to/pokeemerald [output_dir]

set -e

POKEEMERALD_DIR="$1"
OUTPUT_DIR="${2:-../../PokeSharp.Game/Assets/Tilesets}"

if [ -z "$POKEEMERALD_DIR" ]; then
    echo "Usage: $0 <pokeemerald_dir> [output_dir]"
    echo ""
    echo "Example:"
    echo "  $0 ~/pokeemerald"
    echo "  $0 ~/pokeemerald ../../PokeSharp.Game/Assets/Tilesets"
    exit 1
fi

if [ ! -d "$POKEEMERALD_DIR" ]; then
    echo "❌ Error: Pokeemerald directory not found: $POKEEMERALD_DIR"
    exit 1
fi

SCRIPT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
RENDER_SCRIPT="$SCRIPT_DIR/render_tileset_with_palettes.py"

if [ ! -f "$RENDER_SCRIPT" ]; then
    echo "❌ Error: render_tileset_with_palettes.py not found at $RENDER_SCRIPT"
    exit 1
fi

mkdir -p "$OUTPUT_DIR"

echo ""
echo "🎨 Applying palettes to all tilesets..."
echo "   Pokeemerald dir: $POKEEMERALD_DIR"
echo "   Output dir: $OUTPUT_DIR"
echo ""

SUCCESS_COUNT=0
FAIL_COUNT=0

# Process primary tilesets (offset 0)
process_tileset() {
    local TYPE=$1
    local NAME=$2
    local OFFSET=${3:-0}
    
    local SOURCE_DIR="$POKEEMERALD_DIR/data/tilesets/$TYPE/$NAME"
    local TILES_PNG="$SOURCE_DIR/tiles.png"
    local PALETTES_DIR="$SOURCE_DIR/palettes"
    local METATILES_BIN="$SOURCE_DIR/metatiles.bin"
    local OUTPUT_PNG="$OUTPUT_DIR/${NAME}_tiles.png"
    
    echo "Processing: $NAME ($TYPE)"
    
    # Check if source files exist
    if [ ! -f "$TILES_PNG" ]; then
        echo "  ⚠️  Skipping: tiles.png not found at $TILES_PNG"
        FAIL_COUNT=$((FAIL_COUNT + 1))
        return
    fi
    
    if [ ! -d "$PALETTES_DIR" ]; then
        echo "  ⚠️  Skipping: palettes directory not found at $PALETTES_DIR"
        FAIL_COUNT=$((FAIL_COUNT + 1))
        return
    fi
    
    if [ ! -f "$METATILES_BIN" ]; then
        echo "  ⚠️  Skipping: metatiles.bin not found at $METATILES_BIN"
        FAIL_COUNT=$((FAIL_COUNT + 1))
        return
    fi
    
    # Run Python script
    if python "$RENDER_SCRIPT" "$TILES_PNG" "$PALETTES_DIR" "$METATILES_BIN" "$OUTPUT_PNG" "$OFFSET"; then
        echo "  ✅ Applied palette to $NAME"
        SUCCESS_COUNT=$((SUCCESS_COUNT + 1))
    else
        echo "  ❌ Failed to apply palette to $NAME"
        FAIL_COUNT=$((FAIL_COUNT + 1))
    fi
    
    echo ""
}

# Primary tilesets (offset 0)
process_tileset "primary" "general" 0
process_tileset "primary" "building" 0

# Secondary tilesets (offset 512)
process_tileset "secondary" "petalburg" 512
process_tileset "secondary" "brendans_mays_house" 512
process_tileset "secondary" "lab" 512

echo "📊 Summary:"
echo "   ✅ Success: $SUCCESS_COUNT"
echo "   ❌ Failed:  $FAIL_COUNT"
echo ""

if [ $FAIL_COUNT -eq 0 ]; then
    echo "🎉 All palettes applied successfully!"
else
    echo "⚠️  Some tilesets failed. Check the errors above."
fi


