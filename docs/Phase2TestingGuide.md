# Phase 2 Testing Guide

## 🎯 Quick Start

### Run All Phase 2 Tests
```bash
cd /Users/ntomsic/Documents/PokeSharp
dotnet test --filter "FullyQualifiedName~Pooling|FullyQualifiedName~BulkOperations|FullyQualifiedName~Parallel|FullyQualifiedName~Phase2Integration"
```

### Run Individual Test Suites
```bash
# Entity Pooling Tests
dotnet test --filter "FullyQualifiedName~Pooling"

# Bulk Operations Tests
dotnet test --filter "FullyQualifiedName~BulkOperations"

# Parallel Execution Tests
dotnet test --filter "FullyQualifiedName~Parallel"

# Integration Tests
dotnet test --filter "FullyQualifiedName~Phase2Integration"
```

---

## 📊 Test Suite Overview

| Suite | File | Tests | Focus |
|-------|------|-------|-------|
| Entity Pool | `EntityPoolTests.cs` | 35+ | Pool behavior, reuse, thread safety |
| Pool Manager | `EntityPoolManagerTests.cs` | 25+ | Multi-pool management, statistics |
| Bulk Operations | `BulkEntityOperationsTests.cs` | 20+ | Batch operations, performance |
| Parallel Execution | `ParallelQueryExecutorTests.cs` | 18+ | Concurrent processing, speedup |
| Integration | `Phase2IntegrationTests.cs` | 11+ | Combined features, workflows |

**Total:** 89+ comprehensive tests

---

## 🔍 Test Categories

### 1. Entity Pooling Tests (`EntityPoolTests.cs`)

**Location:** `/tests/ECS/Pooling/EntityPoolTests.cs`

**Test Categories:**
- Basic operations (acquire, release, reuse)
- Pool initialization and warmup
- Pool size limits
- Component management
- Statistics tracking
- Thread safety
- Error handling
- Performance benchmarks

**Key Tests:**
```bash
# Run specific test
dotnet test --filter "EntityPoolTests.Acquire_ReturnsValidEntity"
dotnet test --filter "EntityPoolTests.ThreadSafety_ConcurrentAcquireRelease"
dotnet test --filter "EntityPoolTests.Performance_AcquireReleaseCycle"
```

**Expected Behavior:**
- ✅ Entities reused after release
- ✅ Components cleaned on release
- ✅ Thread-safe concurrent access
- ✅ <1ms acquire/release cycle

---

### 2. Entity Pool Manager Tests (`EntityPoolManagerTests.cs`)

**Location:** `/tests/ECS/Pooling/EntityPoolManagerTests.cs`

**Test Categories:**
- Pool registration
- Pool access and retrieval
- Default pool behavior
- Bulk acquire/release
- Statistics aggregation
- Pool lifecycle
- Multiple pool coordination

**Key Tests:**
```bash
# Run specific test
dotnet test --filter "EntityPoolManagerTests.RegisterPool_CreatesNamedPool"
dotnet test --filter "EntityPoolManagerTests.AcquireMultiple_ReturnsRequestedCount"
dotnet test --filter "EntityPoolManagerTests.Scenario_MultiplePoolsWorkIndependently"
```

**Expected Behavior:**
- ✅ Named pools work independently
- ✅ Default pool always available
- ✅ Bulk operations efficient
- ✅ Accurate statistics across pools

---

### 3. Bulk Operations Tests (`BulkEntityOperationsTests.cs`)

**Location:** `/tests/ECS/BulkOperations/BulkEntityOperationsTests.cs`

**Test Categories:**
- Bulk entity creation
- Bulk entity destruction
- Bulk component addition
- Bulk component removal
- Bulk component modification
- Performance comparisons
- Edge case handling

**Key Tests:**
```bash
# Run specific test
dotnet test --filter "BulkEntityOperationsTests.CreateEntities_CreatesCorrectCount"
dotnet test --filter "BulkEntityOperationsTests.BulkCreate_IsFasterThanIndividual"
dotnet test --filter "BulkEntityOperationsTests.ModifyComponent_UpdatesAllEntities"
```

**Expected Behavior:**
- ✅ 2-10x faster than individual operations
- ✅ Factory functions applied correctly
- ✅ Efficient for 100+ entities
- ✅ Proper error handling

---

### 4. Parallel Execution Tests (`ParallelQueryExecutorTests.cs`)

**Location:** `/tests/ECS/Parallel/ParallelQueryExecutorTests.cs`

**Test Categories:**
- Single component parallel processing
- Multi-component parallel processing
- Performance vs sequential
- Thread safety validation
- Batch size configuration
- Parallel algorithm correctness

**Key Tests:**
```bash
# Run specific test
dotnet test --filter "ParallelQueryExecutorTests.ExecuteParallel_ProcessesAllEntities"
dotnet test --filter "ParallelQueryExecutorTests.ParallelExecution_IsFaster_LargeDataset"
dotnet test --filter "ParallelQueryExecutorTests.ThreadSafety_NoDataRaces"
```

**Expected Behavior:**
- ✅ 2-4x speedup on 10K+ entities
- ✅ No data races
- ✅ All entities processed
- ✅ Configurable batch sizes

---

### 5. Integration Tests (`Phase2IntegrationTests.cs`)

**Location:** `/tests/Integration/Phase2IntegrationTests.cs`

**Test Categories:**
- Pooling + Bulk operations
- Bulk + Parallel execution
- Pooling + Parallel execution
- All three features combined
- Performance integration
- Edge cases

**Key Tests:**
```bash
# Run specific test
dotnet test --filter "Phase2IntegrationTests.AllPhase2Features_IntegrateProperly"
dotnet test --filter "Phase2IntegrationTests.ComplexGameScenario_MultipleEntityTypes"
dotnet test --filter "Phase2IntegrationTests.PerformanceTest_FullWorkflow"
```

**Expected Behavior:**
- ✅ Features work together seamlessly
- ✅ Full workflow <1000ms for 5000 entities
- ✅ Multiple entity types coordinated
- ✅ Proper cleanup and disposal

---

## 🚀 Performance Testing

### Run Performance-Focused Tests
```bash
# All performance tests
dotnet test --filter "Performance" --logger "console;verbosity=detailed"

# Specific performance tests
dotnet test --filter "BulkCreate_IsFasterThanIndividual"
dotnet test --filter "ParallelExecution_IsFaster_LargeDataset"
dotnet test --filter "PerformanceTest_FullWorkflow"
```

### Expected Performance Metrics

| Operation | Entity Count | Expected Time | Speedup |
|-----------|--------------|---------------|---------|
| Bulk Create | 1,000 | <10ms | 2-5x |
| Bulk Create | 10,000 | <100ms | 5-10x |
| Parallel Update | 10,000 | <50ms | 2-4x |
| Parallel Update | 100,000 | <500ms | 2-4x |
| Full Workflow | 5,000 | <1000ms | Combined |

---

## 🧵 Thread Safety Testing

### Run Thread Safety Tests
```bash
# All thread safety tests
dotnet test --filter "ThreadSafety"

# Specific tests
dotnet test --filter "EntityPoolTests.ThreadSafety_ConcurrentAcquireRelease"
dotnet test --filter "ParallelQueryExecutorTests.ThreadSafety_NoDataRaces"
```

### Thread Safety Validation
- ✅ 10 concurrent threads
- ✅ 1000+ operations per thread
- ✅ No data races
- ✅ Consistent statistics

---

## 📈 Coverage Analysis

### Generate Coverage Report
```bash
# Install coverage tools
dotnet tool install --global coverlet.console
dotnet tool install --global dotnet-reportgenerator-globaltool

# Run tests with coverage
dotnet test /p:CollectCoverage=true \
           /p:CoverletOutputFormat=cobertura \
           --filter "Pooling|BulkOperations|Parallel|Phase2Integration"

# Generate HTML report
reportgenerator -reports:coverage.cobertura.xml \
                -targetdir:coveragereport \
                -reporttypes:Html

# Open report
open coveragereport/index.html  # macOS
xdg-open coveragereport/index.html  # Linux
```

### Coverage Targets
- **Statement Coverage:** >85%
- **Branch Coverage:** >80%
- **Function Coverage:** >85%
- **Line Coverage:** >85%

---

## 🔧 Debugging Tests

### Run Single Test with Details
```bash
dotnet test --filter "EntityPoolTests.Acquire_ReturnsValidEntity" \
           --logger "console;verbosity=detailed"
```

### Debug Test in IDE
**Visual Studio / Rider:**
1. Open test file
2. Set breakpoint
3. Right-click test → Debug

**VS Code:**
1. Install C# extension
2. Set breakpoint
3. Use "Debug Test" CodeLens

### Common Debugging Scenarios

#### Test Failing: Entity Not Reused
```bash
dotnet test --filter "AcquireAfterRelease_ReusesEntity" --logger "console;verbosity=detailed"
```
**Check:**
- Pool release logic
- Entity cleanup
- Available pool count

#### Test Failing: Performance Below Target
```bash
dotnet test --filter "BulkCreate_IsFasterThanIndividual" --logger "console;verbosity=detailed"
```
**Check:**
- Entity count in test
- Warmup iterations
- Bulk operation implementation

#### Test Failing: Thread Safety
```bash
dotnet test --filter "ThreadSafety" --logger "console;verbosity=detailed"
```
**Check:**
- Lock usage
- Thread-safe collections
- Concurrent access patterns

---

## 📝 Test Patterns

### Arrange-Act-Assert Structure
```csharp
[Test]
public void TestName_Scenario_ExpectedBehavior()
{
    // Arrange - Set up test conditions
    var pool = new EntityPool(World, 10, 100);

    // Act - Perform operation
    var entity = pool.Acquire();

    // Assert - Verify result
    Assert.That(World.IsAlive(entity), Is.True);
}
```

### Parameterized Tests
```csharp
[TestCase(100)]
[TestCase(1000)]
[TestCase(10000)]
public void PerformanceTest(int entityCount)
{
    // Test with multiple entity counts
}
```

### Setup and Teardown
```csharp
[SetUp]
public override void SetUp()
{
    base.SetUp();
    // Per-test setup
}

[TearDown]
public override void TearDown()
{
    // Per-test cleanup
    base.TearDown();
}
```

---

## 🎓 Best Practices

### When Writing New Tests

1. **Inherit from `EcsTestBase`**
   ```csharp
   public class MyTests : EcsTestBase
   ```

2. **Use Descriptive Names**
   ```csharp
   [Test]
   public void MethodName_Scenario_ExpectedResult()
   ```

3. **Test One Thing**
   - Each test should verify one behavior
   - Use multiple tests for multiple scenarios

4. **Clean Up Resources**
   ```csharp
   [TearDown]
   public override void TearDown()
   {
       _disposableResource?.Dispose();
       base.TearDown();
   }
   ```

5. **Use TestContext for Diagnostics**
   ```csharp
   TestContext.WriteLine($"Performance: {time}ms");
   ```

### Common Assertions

```csharp
// Equality
Assert.That(actual, Is.EqualTo(expected));

// Null checks
Assert.That(value, Is.Not.Null);

// Collections
Assert.That(collection, Has.Count.EqualTo(5));
Assert.That(collection, Does.Contain(item));

// Ranges
Assert.That(value, Is.InRange(min, max));

// Exceptions
Assert.Throws<InvalidOperationException>(() => method());

// Performance
Assert.That(duration, Is.LessThan(targetTime));
```

---

## 🐛 Troubleshooting

### All Tests Fail
**Issue:** Base infrastructure not set up
**Fix:**
```bash
# Ensure test base exists
ls tests/ECS/Helpers/EcsTestBase.cs

# Restore dependencies
dotnet restore
```

### Performance Tests Inconsistent
**Issue:** System load affecting results
**Fix:**
- Close other applications
- Run tests multiple times
- Use larger entity counts for more stable results

### Thread Safety Tests Flaky
**Issue:** Timing-dependent failures
**Fix:**
- Increase iteration count
- Add explicit synchronization points
- Check for thread-local state issues

### Coverage Not Generated
**Issue:** Coverlet not installed
**Fix:**
```bash
dotnet tool install --global coverlet.console
dotnet add package coverlet.collector
```

---

## 📚 Related Documentation

- [Phase 2 Test Summary](/docs/Phase2TestSummary.md) - Complete test coverage details
- [Phase 2 Features](/docs/Phase2Features.md) - Feature specifications
- [ECS Architecture](/docs/ECSArchitecture.md) - System design
- [Test Infrastructure](/tests/ECS/Helpers/) - Shared test utilities

---

## ✅ Pre-Implementation Checklist

Before implementing Phase 2 features:
- [ ] Review all test files
- [ ] Understand expected behaviors
- [ ] Check performance baselines
- [ ] Verify test infrastructure works
- [ ] Run existing tests to ensure clean baseline

---

## 🎯 Success Metrics

### Test Execution
- ✅ All 89+ tests pass
- ✅ Total execution time <45s
- ✅ No flaky tests
- ✅ Clear failure messages

### Performance
- ✅ Bulk operations 2-10x faster
- ✅ Parallel execution 2-4x faster
- ✅ Pool operations <1ms
- ✅ Full workflow <1000ms for 5000 entities

### Coverage
- ✅ >85% statement coverage
- ✅ >80% branch coverage
- ✅ All public APIs tested
- ✅ Edge cases covered

---

**Status:** ✅ Test suite ready for Phase 2 implementation
**Next Step:** Implement Phase 2 features and validate with these tests
