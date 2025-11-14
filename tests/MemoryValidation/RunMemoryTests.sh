#!/bin/bash

# Memory Validation Test Runner
# Quick script to run memory tests and generate report

echo "╔══════════════════════════════════════════════════════╗"
echo "║     PokeSharp Memory Validation Test Suite          ║"
echo "╚══════════════════════════════════════════════════════╝"
echo ""

# Check if dotnet is available
if ! command -v dotnet &> /dev/null
then
    echo "❌ dotnet CLI not found. Please install .NET SDK."
    exit 1
fi

echo "🔍 Discovering memory validation tests..."
echo ""

# Run tests with detailed output
echo "🧪 Running memory validation tests..."
echo "━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━"
echo ""

dotnet test \
    --filter "Category=Memory" \
    --logger "console;verbosity=detailed" \
    --results-directory ./tests/MemoryValidation/TestResults \
    --collect:"XPlat Code Coverage"

TEST_EXIT_CODE=$?

echo ""
echo "━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━"
echo ""

# Check results
if [ $TEST_EXIT_CODE -eq 0 ]; then
    echo "✅ ALL MEMORY TESTS PASSED!"
    echo ""
    echo "Memory optimization successful:"
    echo "  ✅ Baseline memory <500MB"
    echo "  ✅ Map loads <100MB increase"
    echo "  ✅ Map transitions cleanup properly"
    echo "  ✅ Stress test stable <500MB"
    echo "  ✅ LRU cache eviction working"
    echo ""
    echo "🎉 Memory validation complete. Ready for deployment!"
else
    echo "❌ SOME MEMORY TESTS FAILED!"
    echo ""
    echo "Please review the test output above for details."
    echo "Common issues:"
    echo "  🔧 Baseline >500MB - Reduce initialization allocations"
    echo "  🔧 Map load >100MB - Optimize texture sizes"
    echo "  🔧 Stress test fails - Memory leak in map loading"
    echo "  🔧 LRU cache fails - Eviction logic not working"
    echo ""
    echo "See tests/MemoryValidation/MemoryTestResults.md for troubleshooting."
fi

echo ""
echo "📊 Test results saved to: ./tests/MemoryValidation/TestResults"
echo ""

exit $TEST_EXIT_CODE
