#!/bin/bash
# Bash script to convert all pokeemerald maps to Tiled format
# Usage: ./convert_all_maps.sh <pokeemerald_dir> <map_output_dir> <tileset_output_dir> [--max-jobs N]

set -e

if [ "$#" -lt 3 ]; then
    echo "Usage: $0 <pokeemerald_dir> <map_output_dir> <tileset_output_dir> [--max-jobs N]"
    echo ""
    echo "Example:"
    echo "  $0 ~/pokeemerald ~/PokeSharp/PokeSharp.Game/Assets/Data/Maps ~/PokeSharp/PokeSharp.Game/Assets/Tilesets"
    echo "  $0 ~/pokeemerald ~/PokeSharp/PokeSharp.Game/Assets/Data/Maps ~/PokeSharp/PokeSharp.Game/Assets/Tilesets --max-jobs 8"
    exit 1
fi

POKEEMERALD_DIR="$1"
MAP_OUTPUT_DIR="$2"
TILESET_OUTPUT_DIR="$3"
MAX_JOBS=4

# Parse optional flags
for arg in "${@:4}"; do
    case $arg in
        --max-jobs)
            shift
            MAX_JOBS="$1"
            ;;
        *)
            echo "Unknown option: $arg"
            exit 1
            ;;
    esac
done

SCRIPT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
CONVERT_MAP_SCRIPT="$SCRIPT_DIR/convert_map_8x8.py"
CONVERT_TILESET_SCRIPT="$SCRIPT_DIR/convert_tileset_8x8.py"

# Validate paths
if [ ! -d "$POKEEMERALD_DIR" ]; then
    echo "[ERROR] Pokeemerald directory not found: $POKEEMERALD_DIR"
    exit 1
fi

LAYOUTS_DIR="$POKEEMERALD_DIR/data/layouts"
if [ ! -d "$LAYOUTS_DIR" ]; then
    echo "[ERROR] Layouts directory not found: $LAYOUTS_DIR"
    exit 1
fi

# Create output directories
mkdir -p "$MAP_OUTPUT_DIR"
mkdir -p "$TILESET_OUTPUT_DIR"

echo ""
echo "========================================"
echo "  Pokeemerald Map Converter"
echo "========================================"
echo ""
echo "Pokeemerald dir: $POKEEMERALD_DIR"
echo "Map output dir:  $MAP_OUTPUT_DIR"
echo "Tileset output:  $TILESET_OUTPUT_DIR"
echo "Max parallel jobs: $MAX_JOBS"
echo ""

# Function to process a single map
process_map() {
    local layout_file="$1"
    local pokeemerald_dir="$2"
    local map_output_dir="$3"
    local tileset_output_dir="$4"
    local convert_map_script="$5"
    local convert_tileset_script="$6"
    
    local layout_dir=$(dirname "$layout_file")
    local map_name=$(basename "$layout_dir")
    
    # Read layout.json to get tileset info
    local primary_tileset=$(python -c "import json; t=json.load(open('$layout_file')).get('primary_tileset', ''); print(t.replace('gTileset_', '') if t.startswith('gTileset_') else t)" 2>/dev/null || echo "")
    local secondary_tileset=$(python -c "import json; t=json.load(open('$layout_file')).get('secondary_tileset', ''); print(t.replace('gTileset_', '') if t and t.startswith('gTileset_') else t)" 2>/dev/null || echo "")
    
    if [ -z "$primary_tileset" ]; then
        echo "[SKIP] $map_name - No primary tileset specified"
        return 1
    fi
    
    # Determine tileset directories (case-insensitive matching)
    local primary_tileset_dir="$pokeemerald_dir/data/tilesets/primary/$primary_tileset"
    # Try lowercase if not found
    if [ ! -d "$primary_tileset_dir" ]; then
        primary_tileset_dir="$pokeemerald_dir/data/tilesets/primary/$(echo "$primary_tileset" | tr '[:upper:]' '[:lower:]')"
    fi
    if [ ! -d "$primary_tileset_dir" ]; then
        echo "[SKIP] $map_name - Primary tileset directory not found: $primary_tileset_dir"
        return 1
    fi
    
    local secondary_tileset_dir=""
    if [ -n "$secondary_tileset" ]; then
        secondary_tileset_dir="$pokeemerald_dir/data/tilesets/secondary/$secondary_tileset"
        # Try lowercase if not found
        if [ ! -d "$secondary_tileset_dir" ]; then
            secondary_tileset_dir="$pokeemerald_dir/data/tilesets/secondary/$(echo "$secondary_tileset" | tr '[:upper:]' '[:lower:]')"
        fi
        if [ ! -d "$secondary_tileset_dir" ]; then
            secondary_tileset_dir=""
        fi
    fi
    
    # Extract primary tileset to shared location (organized by tileset name, lowercase for consistency)
    local primary_tileset_lower=$(echo "$primary_tileset" | tr '[:upper:]' '[:lower:]')
    local primary_tileset_output_dir="$tileset_output_dir/$primary_tileset_lower"
    mkdir -p "$primary_tileset_output_dir"
    local primary_tileset_json_dest="$primary_tileset_output_dir/$primary_tileset_lower.json"
    
    # Only extract if not already exists (avoid duplicates)
    if [ ! -f "$primary_tileset_json_dest" ]; then
        if ! python "$convert_tileset_script" "$primary_tileset_dir" "$primary_tileset_json_dest" --apply-palettes --tile-offset 0 2>&1; then
            echo "[ERROR] $map_name - Failed to convert primary tileset"
            return 2
        fi
    fi
    
    # Extract secondary tileset if present
    local secondary_tileset_json=""
    if [ -n "$secondary_tileset_dir" ]; then
        local secondary_tileset_lower=$(echo "$secondary_tileset" | tr '[:upper:]' '[:lower:]')
        local secondary_tileset_output_dir="$tileset_output_dir/$secondary_tileset_lower"
        mkdir -p "$secondary_tileset_output_dir"
        local secondary_tileset_json_dest="$secondary_tileset_output_dir/$secondary_tileset_lower.json"
        
        # Only extract if not already exists (avoid duplicates)
        if [ ! -f "$secondary_tileset_json_dest" ]; then
            if ! python "$convert_tileset_script" "$secondary_tileset_dir" "$secondary_tileset_json_dest" --apply-palettes --tile-offset 512 2>&1; then
                echo "[ERROR] $map_name - Failed to convert secondary tileset"
                return 2
            fi
        fi
        
        # Calculate relative path from Maps directory to secondary tileset
        local relative_to_map_output=$(realpath --relative-to="$map_output_dir" "$secondary_tileset_output_dir" 2>/dev/null || echo "../Tilesets/$secondary_tileset_lower")
        secondary_tileset_json="$relative_to_map_output/$secondary_tileset_lower.json"
    fi
    
    # Calculate relative paths for tileset JSON references (from Maps directory to tileset directory)
    local map_output_path="$map_output_dir/$map_name.json"
    local relative_to_map_output=$(realpath --relative-to="$map_output_dir" "$primary_tileset_output_dir" 2>/dev/null || echo "../Tilesets/$primary_tileset_lower")
    local primary_tileset_json="$relative_to_map_output/$primary_tileset_lower.json"
    
    # Build Python command for map conversion
    local python_args=(
        "$convert_map_script"
        "$layout_file"
        "$primary_tileset_dir"
        "$map_output_path"
        "--primary-tileset-json" "$primary_tileset_json"
    )
    
    if [ -n "$secondary_tileset_dir" ]; then
        python_args+=("--secondary-tileset-dir" "$secondary_tileset_dir")
        if [ -n "$secondary_tileset_json" ]; then
            python_args+=("--secondary-tileset-json" "$secondary_tileset_json")
        fi
    fi
    
    if python "${python_args[@]}" 2>&1; then
        echo "[OK] $map_name - Converted successfully"
        return 0
    else
        echo "[ERROR] $map_name - Map conversion failed"
        return 2
    fi
}

# Export function for parallel execution
export -f process_map

# Read master layouts.json file
echo "[STEP 1] Reading layouts from master file..."
MASTER_LAYOUTS_FILE="$LAYOUTS_DIR/layouts.json"
if [ ! -f "$MASTER_LAYOUTS_FILE" ]; then
    echo "[ERROR] Master layouts.json not found: $MASTER_LAYOUTS_FILE"
    exit 1
fi

# Extract layouts array using Python
LAYOUT_COUNT=$(python -c "import json; f=open('$MASTER_LAYOUTS_FILE', 'r', encoding='utf-8-sig'); d=json.load(f); print(len(d.get('layouts', [])))" 2>/dev/null || echo "0")
echo "Found $LAYOUT_COUNT layouts in master file"
echo ""

# Process maps in parallel
echo "[STEP 2] Converting maps (parallel, max $MAX_JOBS jobs)..."
echo ""

SUCCESS_COUNT=0
FAIL_COUNT=0
SKIP_COUNT=0

# Use Python to extract layouts and process them
# We'll use a Python script to handle the parallel processing since bash has limitations with complex data structures
python3 << 'PYTHON_SCRIPT'
import json
import subprocess
import sys
from pathlib import Path
import multiprocessing
from functools import partial

def process_layout(layout, pokeemerald_dir, map_output_dir, tileset_output_dir, convert_map_script, convert_tileset_script, layouts_dir):
    """Process a single layout from the master layouts.json"""
    try:
        # Extract map name from layout name (e.g., "LittlerootTown_Layout" -> "LittlerootTown")
        map_name = layout['name'].replace('_Layout', '')
        
        primary_tileset = layout.get('primary_tileset', '')
        secondary_tileset = layout.get('secondary_tileset', '')
        
        # Strip "gTileset_" prefix if present
        if primary_tileset.startswith('gTileset_'):
            primary_tileset = primary_tileset.replace('gTileset_', '')
        if secondary_tileset and secondary_tileset.startswith('gTileset_'):
            secondary_tileset = secondary_tileset.replace('gTileset_', '')
        
        if not primary_tileset:
            print(f"[SKIP] {map_name} - No primary tileset specified")
            return {'success': False, 'skipped': True, 'map_name': map_name}
        
        # Determine tileset directories (case-insensitive)
        primary_tileset_dir = Path(pokeemerald_dir) / "data" / "tilesets" / "primary" / primary_tileset.lower()
        if not primary_tileset_dir.exists():
            primary_tileset_dir = Path(pokeemerald_dir) / "data" / "tilesets" / "primary" / primary_tileset
        if not primary_tileset_dir.exists():
            print(f"[SKIP] {map_name} - Primary tileset directory not found: {primary_tileset_dir}")
            return {'success': False, 'skipped': True, 'map_name': map_name}
        
        secondary_tileset_dir = None
        if secondary_tileset:
            secondary_tileset_dir = Path(pokeemerald_dir) / "data" / "tilesets" / "secondary" / secondary_tileset.lower()
            if not secondary_tileset_dir.exists():
                secondary_tileset_dir = Path(pokeemerald_dir) / "data" / "tilesets" / "secondary" / secondary_tileset
            if not secondary_tileset_dir.exists():
                secondary_tileset_dir = None
        
        # Extract primary tileset to shared location
        primary_tileset_lower = primary_tileset.lower()
        primary_tileset_output_dir = Path(tileset_output_dir) / primary_tileset_lower
        primary_tileset_output_dir.mkdir(parents=True, exist_ok=True)
        primary_tileset_json_dest = primary_tileset_output_dir / f"{primary_tileset_lower}.json"
        
        # Only extract if not already exists
        if not primary_tileset_json_dest.exists():
            cmd = [
                sys.executable,
                convert_tileset_script,
                str(primary_tileset_dir),
                str(primary_tileset_json_dest),
                "--apply-palettes",
                "--tile-offset", "0"
            ]
            result = subprocess.run(cmd, capture_output=True, text=True)
            if result.returncode != 0:
                print(f"[ERROR] {map_name} - Failed to convert primary tileset: {result.stderr}")
                return {'success': False, 'skipped': False, 'map_name': map_name}
        
        # Extract secondary tileset if present
        secondary_tileset_json = None
        if secondary_tileset_dir:
            secondary_tileset_lower = secondary_tileset.lower()
            secondary_tileset_output_dir = Path(tileset_output_dir) / secondary_tileset_lower
            secondary_tileset_output_dir.mkdir(parents=True, exist_ok=True)
            secondary_tileset_json_dest = secondary_tileset_output_dir / f"{secondary_tileset_lower}.json"
            
            if not secondary_tileset_json_dest.exists():
                cmd = [
                    sys.executable,
                    convert_tileset_script,
                    str(secondary_tileset_dir),
                    str(secondary_tileset_json_dest),
                    "--apply-palettes",
                    "--tile-offset", "512"
                ]
                result = subprocess.run(cmd, capture_output=True, text=True)
                if result.returncode != 0:
                    print(f"[ERROR] {map_name} - Failed to convert secondary tileset: {result.stderr}")
                    return {'success': False, 'skipped': False, 'map_name': map_name}
            
            # Calculate relative path for secondary tileset
            map_output_path_obj = Path(map_output_dir)
            secondary_tileset_path_obj = secondary_tileset_output_dir
            try:
                relative_path = str(secondary_tileset_path_obj.relative_to(map_output_path_obj)).replace('\\', '/')
                secondary_tileset_json = f"{relative_path}/{secondary_tileset_lower}.json"
            except ValueError:
                secondary_tileset_json = f"../Tilesets/{secondary_tileset_lower}/{secondary_tileset_lower}.json"
        
        # Calculate relative path for primary tileset
        map_output_path_obj = Path(map_output_dir)
        primary_tileset_path_obj = primary_tileset_output_dir
        try:
            relative_path = str(primary_tileset_path_obj.relative_to(map_output_path_obj)).replace('\\', '/')
            primary_tileset_json = f"{relative_path}/{primary_tileset_lower}.json"
        except ValueError:
            primary_tileset_json = f"../Tilesets/{primary_tileset_lower}/{primary_tileset_lower}.json"
        
        # Create temporary layout.json file
        blockdata_path = layout['blockdata_filepath']
        layout_dir_name = Path(blockdata_path).parent.name
        layout_dir = Path(layouts_dir) / layout_dir_name
        temp_layout_file = layout_dir / "layout.json"
        
        if not temp_layout_file.exists():
            layout_dir.mkdir(parents=True, exist_ok=True)
            layout_json_data = {
                'width': layout['width'],
                'height': layout['height'],
                'primary_tileset': layout['primary_tileset'],
                'secondary_tileset': layout.get('secondary_tileset'),
                'border_filepath': layout['border_filepath'],
                'blockdata_filepath': layout['blockdata_filepath']
            }
            with open(temp_layout_file, 'w', encoding='utf-8') as f:
                json.dump(layout_json_data, f, indent=2)
        
        # Build Python command for map conversion
        map_output_path = Path(map_output_dir) / f"{map_name}.json"
        cmd = [
            sys.executable,
            convert_map_script,
            str(temp_layout_file),
            str(primary_tileset_dir),
            str(map_output_path),
            "--primary-tileset-json", primary_tileset_json
        ]
        
        if secondary_tileset_dir:
            cmd.extend(["--secondary-tileset-dir", str(secondary_tileset_dir)])
            if secondary_tileset_json:
                cmd.extend(["--secondary-tileset-json", secondary_tileset_json])
        
        result = subprocess.run(cmd, capture_output=True, text=True)
        if result.returncode == 0:
            print(f"[OK] {map_name} - Converted successfully")
            return {'success': True, 'skipped': False, 'map_name': map_name}
        else:
            print(f"[ERROR] {map_name} - Map conversion failed: {result.stderr}")
            return {'success': False, 'skipped': False, 'map_name': map_name}
            
    except Exception as e:
        print(f"[ERROR] {map_name} - Exception: {str(e)}")
        return {'success': False, 'skipped': False, 'map_name': map_name}

# Read master layouts.json
with open('$MASTER_LAYOUTS_FILE', 'r', encoding='utf-8-sig') as f:
    master_layouts = json.load(f)

layouts = master_layouts.get('layouts', [])

# Process in parallel
with multiprocessing.Pool(processes=$MAX_JOBS) as pool:
    process_func = partial(
        process_layout,
        pokeemerald_dir='$POKEEMERALD_DIR',
        map_output_dir='$MAP_OUTPUT_DIR',
        tileset_output_dir='$TILESET_OUTPUT_DIR',
        convert_map_script='$CONVERT_MAP_SCRIPT',
        convert_tileset_script='$CONVERT_TILESET_SCRIPT',
        layouts_dir='$LAYOUTS_DIR'
    )
    results = pool.map(process_func, layouts)

# Count results
success_count = sum(1 for r in results if r['success'])
fail_count = sum(1 for r in results if not r['success'] and not r['skipped'])
skip_count = sum(1 for r in results if r['skipped'])

print("")
print("========================================")
print("  Conversion Summary")
print("========================================")
print(f"  [OK] Success:    {success_count}")
print(f"  [ERROR] Failed:  {fail_count}")
print(f"  [SKIP] Skipped:  {skip_count}")
print("")

if fail_count == 0:
    print("[OK] All maps converted successfully!")
else:
    print("[WARN] Some maps failed to convert. Check errors above.")

sys.exit(0 if fail_count == 0 else 1)
PYTHON_SCRIPT

echo ""

# Summary
echo "========================================"
echo "  Conversion Summary"
echo "========================================"
echo "  [OK] Success:    $SUCCESS_COUNT"
echo "  [ERROR] Failed:  $FAIL_COUNT"
echo "  [SKIP] Skipped:  $SKIP_COUNT"
echo ""

if [ $FAIL_COUNT -eq 0 ]; then
    echo "[OK] All maps converted successfully!"
else
    echo "[WARN] Some maps failed to convert. Check errors above."
fi
