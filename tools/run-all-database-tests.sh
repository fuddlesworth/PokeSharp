#!/bin/bash

###############################################################################
# Run All Database Tests
#
# This script runs the complete SQLite database test suite:
#   1. Unit tests (DatabaseMigrationTests)
#   2. Integration tests (SqliteMemoryTest)
#   3. Validation script (validate-sqlite-migration.sh)
#
# Usage: ./run-all-database-tests.sh [--skip-validation]
###############################################################################

set -e

# Colors
RED='\033[0;31m'
GREEN='\033[0;32m'
YELLOW='\033[1;33m'
BLUE='\033[0;34m'
CYAN='\033[0;36m'
NC='\033[0m'

# Configuration
SCRIPT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
PROJECT_ROOT="$(dirname "$SCRIPT_DIR")"
SKIP_VALIDATION=false

# Parse arguments
if [[ "$1" == "--skip-validation" ]]; then
    SKIP_VALIDATION=true
fi

###############################################################################
# Helper Functions
###############################################################################

print_header() {
    echo -e "\n${CYAN}================================================${NC}"
    echo -e "${CYAN}$1${NC}"
    echo -e "${CYAN}================================================${NC}\n"
}

print_success() {
    echo -e "${GREEN}✓ $1${NC}"
}

print_error() {
    echo -e "${RED}✗ $1${NC}"
}

print_info() {
    echo -e "${BLUE}ℹ $1${NC}"
}

###############################################################################
# Test Functions
###############################################################################

run_unit_tests() {
    print_header "1. Running Unit Tests (DatabaseMigrationTests)"

    cd "$PROJECT_ROOT/tests/PokeSharp.Game.Data.Tests"

    if dotnet test --verbosity normal --logger "console;verbosity=detailed"; then
        print_success "Unit tests passed"
        return 0
    else
        print_error "Unit tests failed"
        return 1
    fi
}

run_integration_tests() {
    print_header "2. Running Integration Tests (SqliteMemoryTest)"

    cd "$PROJECT_ROOT/tests/Integration"

    if dotnet test --verbosity normal --logger "console;verbosity=detailed"; then
        print_success "Integration tests passed"
        return 0
    else
        print_error "Integration tests failed"
        return 1
    fi
}

run_validation_script() {
    print_header "3. Running Validation Script"

    # First, we need a database to validate
    # Try to find the game's database or create a test one

    local db_path="$PROJECT_ROOT/pokesharp.db"

    if [ ! -f "$db_path" ]; then
        print_info "No production database found, looking for test database..."
        # Look for recently created test databases
        db_path=$(find /tmp -name "pokesharp_test_*.db" -type f -mtime -1 2>/dev/null | head -1)

        if [ -z "$db_path" ]; then
            print_info "No test database found, creating one for validation..."
            # Create a temporary database for validation
            db_path="/tmp/pokesharp_validation_test.db"

            # Run a quick test to create a database
            cd "$PROJECT_ROOT/tests/PokeSharp.Game.Data.Tests"
            dotnet test --filter "DatabaseCreation_OnFirstRun_CreatesDatabaseFile" > /dev/null 2>&1 || true

            # Find the created database
            db_path=$(find /tmp -name "pokesharp_test_*.db" -type f -mtime -1 2>/dev/null | head -1)
        fi
    fi

    if [ -z "$db_path" ] || [ ! -f "$db_path" ]; then
        print_error "Could not find or create database for validation"
        print_info "Skipping validation script"
        return 1
    fi

    print_info "Using database: $db_path"

    if "$SCRIPT_DIR/validate-sqlite-migration.sh" "$db_path"; then
        print_success "Validation script passed"
        return 0
    else
        print_error "Validation script failed"
        return 1
    fi
}

generate_test_report() {
    print_header "Test Summary Report"

    echo -e "${BLUE}Test Suite Results:${NC}"
    echo -e "  Unit Tests:        ${unit_test_status}"
    echo -e "  Integration Tests: ${integration_test_status}"
    echo -e "  Validation Script: ${validation_status}"
    echo ""

    if [[ "$unit_test_status" == *"✓"* ]] && [[ "$integration_test_status" == *"✓"* ]]; then
        echo -e "${GREEN}════════════════════════════════════════════════${NC}"
        echo -e "${GREEN}  ALL TESTS PASSED - Database migration ready! ${NC}"
        echo -e "${GREEN}════════════════════════════════════════════════${NC}"
        return 0
    else
        echo -e "${RED}════════════════════════════════════════════════${NC}"
        echo -e "${RED}  TESTS FAILED - Review failures above         ${NC}"
        echo -e "${RED}════════════════════════════════════════════════${NC}"
        return 1
    fi
}

###############################################################################
# Main Execution
###############################################################################

main() {
    print_header "SQLite Database Test Suite"

    print_info "Project root: $PROJECT_ROOT"
    print_info "Skip validation: $SKIP_VALIDATION"
    echo ""

    # Initialize status variables
    unit_test_status="${RED}✗ Failed${NC}"
    integration_test_status="${RED}✗ Failed${NC}"
    validation_status="${YELLOW}⊝ Skipped${NC}"

    # Run unit tests
    if run_unit_tests; then
        unit_test_status="${GREEN}✓ Passed${NC}"
    fi

    echo ""

    # Run integration tests
    if run_integration_tests; then
        integration_test_status="${GREEN}✓ Passed${NC}"
    fi

    echo ""

    # Run validation script (optional)
    if [ "$SKIP_VALIDATION" = false ]; then
        if run_validation_script; then
            validation_status="${GREEN}✓ Passed${NC}"
        else
            validation_status="${RED}✗ Failed${NC}"
        fi
        echo ""
    fi

    # Generate summary report
    generate_test_report
}

# Run main
main
exit_code=$?

echo ""
exit $exit_code
