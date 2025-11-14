#!/bin/bash

###############################################################################
# SQLite Migration Validation Script
#
# This script validates that the SQLite database migration is working correctly
# by checking:
#   1. Database file exists and is accessible
#   2. All required tables are present
#   3. Tables contain expected data
#   4. TiledDataJson field is populated correctly
#   5. Memory usage is within acceptable limits
#
# Usage: ./validate-sqlite-migration.sh [database-path]
# If no path is provided, searches for common locations
###############################################################################

set -e  # Exit on error

# Colors for output
RED='\033[0;31m'
GREEN='\033[0;32m'
YELLOW='\033[1;33m'
BLUE='\033[0;34m'
NC='\033[0m' # No Color

# Configuration
DEFAULT_DB_PATHS=(
    "./pokesharp.db"
    "./PokeSharp.Game/bin/Debug/net9.0/pokesharp.db"
    "./PokeSharp.Game/bin/Release/net9.0/pokesharp.db"
    "$HOME/.pokesharp/pokesharp.db"
)

MIN_EXPECTED_MAPS=10
MIN_EXPECTED_NPCS=5
MIN_EXPECTED_TRAINERS=3
MAX_MEMORY_MB=100

###############################################################################
# Helper Functions
###############################################################################

print_header() {
    echo -e "\n${BLUE}========================================${NC}"
    echo -e "${BLUE}$1${NC}"
    echo -e "${BLUE}========================================${NC}\n"
}

print_success() {
    echo -e "${GREEN}✓ $1${NC}"
}

print_error() {
    echo -e "${RED}✗ $1${NC}"
}

print_warning() {
    echo -e "${YELLOW}⚠ $1${NC}"
}

print_info() {
    echo -e "${BLUE}ℹ $1${NC}"
}

###############################################################################
# Validation Functions
###############################################################################

find_database() {
    local db_path="$1"

    # If path provided, use it
    if [ -n "$db_path" ]; then
        if [ -f "$db_path" ]; then
            echo "$db_path"
            return 0
        else
            print_error "Database not found at: $db_path"
            return 1
        fi
    fi

    # Search default locations
    print_info "Searching for database in default locations..."
    for path in "${DEFAULT_DB_PATHS[@]}"; do
        if [ -f "$path" ]; then
            print_success "Found database at: $path"
            echo "$path"
            return 0
        fi
    done

    print_error "Database not found in any default location"
    print_info "Default search paths:"
    for path in "${DEFAULT_DB_PATHS[@]}"; do
        echo "  - $path"
    done
    return 1
}

check_sqlite_installed() {
    if ! command -v sqlite3 &> /dev/null; then
        print_error "sqlite3 is not installed"
        print_info "Install with: sudo apt-get install sqlite3 (Linux) or brew install sqlite3 (Mac)"
        return 1
    fi
    print_success "sqlite3 is installed"
    return 0
}

validate_database_file() {
    local db_path="$1"

    print_header "1. Database File Validation"

    # Check file exists
    if [ ! -f "$db_path" ]; then
        print_error "Database file does not exist: $db_path"
        return 1
    fi
    print_success "Database file exists: $db_path"

    # Check file size
    local size=$(stat -f%z "$db_path" 2>/dev/null || stat -c%s "$db_path" 2>/dev/null)
    local size_mb=$((size / 1024 / 1024))
    print_info "Database size: ${size_mb}MB (${size} bytes)"

    # Check file is readable
    if [ ! -r "$db_path" ]; then
        print_error "Database file is not readable"
        return 1
    fi
    print_success "Database file is readable"

    # Check if it's a valid SQLite database
    if ! file "$db_path" | grep -q "SQLite"; then
        print_error "File is not a valid SQLite database"
        return 1
    fi
    print_success "File is a valid SQLite database"

    return 0
}

validate_database_schema() {
    local db_path="$1"

    print_header "2. Database Schema Validation"

    # Check tables exist
    local tables=$(sqlite3 "$db_path" "SELECT name FROM sqlite_master WHERE type='table';")

    print_info "Tables found:"
    echo "$tables" | while read table; do
        if [ -n "$table" ]; then
            echo "  - $table"
        fi
    done

    # Check for required tables
    local required_tables=("Maps" "Npcs" "Trainers")
    for table in "${required_tables[@]}"; do
        if echo "$tables" | grep -q "^${table}$"; then
            print_success "Table '$table' exists"
        else
            print_error "Required table '$table' is missing"
            return 1
        fi
    done

    # Check indexes
    print_info "Checking indexes..."
    local indexes=$(sqlite3 "$db_path" "SELECT name FROM sqlite_master WHERE type='index';")
    local index_count=$(echo "$indexes" | grep -c "IX_" || true)

    if [ "$index_count" -gt 0 ]; then
        print_success "Found $index_count indexes"
    else
        print_warning "No indexes found - queries may be slow"
    fi

    return 0
}

validate_table_data() {
    local db_path="$1"

    print_header "3. Table Data Validation"

    # Count records in each table
    local map_count=$(sqlite3 "$db_path" "SELECT COUNT(*) FROM Maps;")
    local npc_count=$(sqlite3 "$db_path" "SELECT COUNT(*) FROM Npcs;")
    local trainer_count=$(sqlite3 "$db_path" "SELECT COUNT(*) FROM Trainers;")

    print_info "Record counts:"
    echo "  - Maps: $map_count"
    echo "  - NPCs: $npc_count"
    echo "  - Trainers: $trainer_count"

    # Validate minimum records
    if [ "$map_count" -ge "$MIN_EXPECTED_MAPS" ]; then
        print_success "Maps table has sufficient data ($map_count >= $MIN_EXPECTED_MAPS)"
    else
        print_warning "Maps table has fewer records than expected ($map_count < $MIN_EXPECTED_MAPS)"
    fi

    if [ "$npc_count" -ge "$MIN_EXPECTED_NPCS" ]; then
        print_success "NPCs table has sufficient data ($npc_count >= $MIN_EXPECTED_NPCS)"
    else
        print_warning "NPCs table has fewer records than expected ($npc_count < $MIN_EXPECTED_NPCS)"
    fi

    if [ "$trainer_count" -ge "$MIN_EXPECTED_TRAINERS" ]; then
        print_success "Trainers table has sufficient data ($trainer_count >= $MIN_EXPECTED_TRAINERS)"
    else
        print_warning "Trainers table has fewer records than expected ($trainer_count < $MIN_EXPECTED_TRAINERS)"
    fi

    return 0
}

validate_tiled_data() {
    local db_path="$1"

    print_header "4. TiledDataJson Field Validation"

    # Check if TiledDataJson is populated
    local empty_json_count=$(sqlite3 "$db_path" "SELECT COUNT(*) FROM Maps WHERE TiledDataJson = '{}' OR TiledDataJson = '' OR TiledDataJson IS NULL;")
    local total_maps=$(sqlite3 "$db_path" "SELECT COUNT(*) FROM Maps;")
    local populated_count=$((total_maps - empty_json_count))

    print_info "TiledDataJson status:"
    echo "  - Total maps: $total_maps"
    echo "  - Populated: $populated_count"
    echo "  - Empty: $empty_json_count"

    if [ "$populated_count" -gt 0 ]; then
        print_success "TiledDataJson is populated for $populated_count maps"
    else
        print_error "No maps have TiledDataJson populated"
        return 1
    fi

    # Sample a few TiledDataJson entries
    print_info "Sample TiledDataJson entries (first 3 maps):"
    sqlite3 "$db_path" "SELECT MapId, LENGTH(TiledDataJson) as JsonSize FROM Maps LIMIT 3;" | while read line; do
        echo "  - $line"
    done

    # Check for valid JSON structure
    print_info "Validating JSON structure..."
    local sample_json=$(sqlite3 "$db_path" "SELECT TiledDataJson FROM Maps WHERE TiledDataJson != '{}' LIMIT 1;")

    if echo "$sample_json" | python3 -m json.tool > /dev/null 2>&1; then
        print_success "Sample TiledDataJson is valid JSON"
    else
        print_warning "Sample TiledDataJson may not be valid JSON"
    fi

    return 0
}

estimate_memory_usage() {
    local db_path="$1"

    print_header "5. Memory Usage Estimation"

    # Get database file size
    local db_size=$(stat -f%z "$db_path" 2>/dev/null || stat -c%s "$db_path" 2>/dev/null)
    local db_size_mb=$((db_size / 1024 / 1024))

    print_info "Database file size: ${db_size_mb}MB"

    # Estimate in-memory footprint (rough approximation)
    # SQLite typically uses ~2-3x the database size when fully loaded
    local estimated_memory=$((db_size_mb * 2))

    print_info "Estimated memory usage (worst case): ${estimated_memory}MB"

    if [ "$estimated_memory" -le "$MAX_MEMORY_MB" ]; then
        print_success "Estimated memory usage is within limits ($estimated_memory MB <= $MAX_MEMORY_MB MB)"
    else
        print_warning "Estimated memory usage may exceed limits ($estimated_memory MB > $MAX_MEMORY_MB MB)"
    fi

    # Show table sizes
    print_info "Table size breakdown:"
    sqlite3 "$db_path" <<EOF
.mode column
SELECT
    name as TableName,
    ROUND(SUM(pgsize) / 1024.0 / 1024.0, 2) as SizeMB
FROM dbstat
WHERE name IN ('Maps', 'Npcs', 'Trainers')
GROUP BY name
ORDER BY SizeMB DESC;
EOF

    return 0
}

generate_summary_report() {
    local db_path="$1"

    print_header "Summary Report"

    # Get statistics
    local map_count=$(sqlite3 "$db_path" "SELECT COUNT(*) FROM Maps;")
    local npc_count=$(sqlite3 "$db_path" "SELECT COUNT(*) FROM Npcs;")
    local trainer_count=$(sqlite3 "$db_path" "SELECT COUNT(*) FROM Trainers;")
    local db_size=$(stat -f%z "$db_path" 2>/dev/null || stat -c%s "$db_path" 2>/dev/null)
    local db_size_mb=$((db_size / 1024 / 1024))

    echo "Database: $db_path"
    echo "Size: ${db_size_mb}MB"
    echo ""
    echo "Data Summary:"
    echo "  - Maps: $map_count"
    echo "  - NPCs: $npc_count"
    echo "  - Trainers: $trainer_count"
    echo "  - Total Entities: $((map_count + npc_count + trainer_count))"
    echo ""

    # Sample data
    echo "Sample Maps:"
    sqlite3 -column -header "$db_path" "SELECT MapId, DisplayName, Region, MapType FROM Maps LIMIT 5;"
    echo ""

    print_success "Validation complete!"
}

###############################################################################
# Main Execution
###############################################################################

main() {
    local db_path="$1"

    print_header "SQLite Database Migration Validator"

    # Check prerequisites
    check_sqlite_installed || exit 1

    # Find database
    db_path=$(find_database "$db_path") || exit 1

    print_info "Using database: $db_path"
    echo ""

    # Run validations
    validate_database_file "$db_path" || exit 1
    validate_database_schema "$db_path" || exit 1
    validate_table_data "$db_path" || exit 1
    validate_tiled_data "$db_path" || exit 1
    estimate_memory_usage "$db_path" || exit 1

    # Generate summary
    generate_summary_report "$db_path"

    exit 0
}

# Run main with all arguments
main "$@"
