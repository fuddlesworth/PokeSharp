# PokeSharp.Game.Data Tests

Comprehensive test suite for SQLite database migration and Entity Framework Core integration.

## Test Coverage

### DatabaseMigrationTests.cs

Unit tests covering all aspects of the SQLite database migration:

#### 1. Database Creation Tests
- ✅ `DatabaseCreation_OnFirstRun_CreatesDatabaseFile` - Verifies database file is created
- ✅ `DatabaseCreation_OnFirstRun_CreatesAllTables` - Ensures all required tables exist
- ✅ `DatabaseCreation_OnFirstRun_CreatesIndexes` - Validates indexes are created for performance

#### 2. Data Loading Tests
- ✅ `DataLoading_MapDefinition_StoresAndRetrievesCorrectly` - Tests MapDefinition CRUD operations
- ✅ `DataLoading_NpcDefinition_StoresAndRetrievesCorrectly` - Tests NpcDefinition CRUD operations
- ✅ `DataLoading_TrainerDefinition_StoresAndRetrievesCorrectly` - Tests TrainerDefinition CRUD operations
- ✅ `DataLoading_BulkInsert_HandlesLargeDatasets` - Tests bulk insertion of 500+ maps
- ✅ `DataLoading_TiledDataJson_DeserializesCorrectly` - Validates JSON serialization/deserialization

#### 3. Subsequent Startup Tests
- ✅ `SubsequentStartup_ExistingDatabase_LoadsCorrectly` - Tests loading from existing database
- ✅ `SubsequentStartup_NoRecreation_PreservesData` - Ensures data persists across restarts

#### 4. EF Query Tests
- ✅ `EfQueries_MapByRegion_ReturnsCorrectResults` - Tests region-based filtering
- ✅ `EfQueries_MapByType_UsesIndex` - Validates indexed queries
- ✅ `EfQueries_NpcByType_ReturnsFiltered` - Tests NPC filtering
- ✅ `EfQueries_TrainerByClass_ReturnsCorrect` - Tests trainer class queries
- ✅ `EfQueries_ComplexQuery_PerformsEfficiently` - Tests multi-filter queries with performance

#### 5. Memory Usage Tests
- ✅ `MemoryUsage_DatabaseCreation_RemainsLow` - Ensures database creation uses <100MB
- ✅ `MemoryUsage_QueryExecution_DoesNotLeak` - Validates no memory leaks during queries
- ✅ `MemoryUsage_LargeJsonField_DoesNotExplode` - Tests handling of large JSON fields

## Running the Tests

### Using .NET CLI

```bash
# Run all tests
cd tests/PokeSharp.Game.Data.Tests
dotnet test

# Run with detailed output
dotnet test --verbosity normal

# Run specific test
dotnet test --filter "DatabaseCreation_OnFirstRun_CreatesDatabaseFile"

# Run with code coverage
dotnet test /p:CollectCoverage=true
```

### Using Visual Studio / Rider

1. Open Test Explorer
2. Right-click on `PokeSharp.Game.Data.Tests`
3. Select "Run Tests"

### Expected Results

All tests should pass with:
- ✅ 20/20 tests passing
- ⏱️ Total execution time: <10 seconds
- 💾 Memory usage: <100MB
- 📊 Code coverage: >80%

## Test Data

Tests use realistic test data:
- **Maps**: 500+ test maps with varying sizes
- **NPCs**: 1000+ test NPCs with different types
- **Trainers**: 200+ test trainers with different classes
- **JSON Data**: Realistic Tiled JSON structures (5-10KB per map)

## Integration with CI/CD

Add to your CI pipeline:

```yaml
# .github/workflows/tests.yml
- name: Run Database Tests
  run: |
    cd tests/PokeSharp.Game.Data.Tests
    dotnet test --logger "trx;LogFileName=test-results.xml"
```

## Troubleshooting

### Test Database Cleanup

Tests automatically clean up temporary database files. If you encounter issues:

```bash
# Clean temp directory
rm -rf /tmp/pokesharp_test_*.db
```

### Memory Test Failures

If memory tests fail:
1. Close other applications
2. Run tests individually
3. Check for memory leaks in production code
4. Verify GC is collecting properly

### JSON Serialization Issues

If TiledDataJson tests fail:
1. Check System.Text.Json version
2. Verify JSON structure matches schema
3. Ensure special characters are escaped

## Performance Benchmarks

Expected performance metrics:
- Database creation: <500ms
- 500 map inserts: <2 seconds
- Complex query: <100ms
- Memory usage: <100MB with 500 maps

## Dependencies

- Microsoft.EntityFrameworkCore: 8.0.0
- Microsoft.EntityFrameworkCore.Sqlite: 8.0.0
- xUnit: 2.9.3
- FluentAssertions: 6.12.2
- Moq: 4.20.72

## Contributing

When adding new tests:
1. Follow existing naming conventions
2. Use FluentAssertions for readable assertions
3. Clean up resources in Dispose()
4. Add descriptive test names and comments
5. Ensure tests are isolated and repeatable
