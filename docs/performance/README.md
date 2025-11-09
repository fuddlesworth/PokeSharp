# PokeSharp Performance Documentation

This directory contains performance benchmarks, analysis, and optimization reports for the PokeSharp ECS implementation.

## 📊 Reports

### Phase 1: Baseline Performance

**File:** [PHASE1_BASELINE.md](./PHASE1_BASELINE.md)

Establishes baseline performance metrics before Phase 2 optimizations:
- Entity creation and destruction performance
- Query execution times
- System update frame times
- Memory allocation patterns
- GC collection frequency

**Status:** ⚠️ Partially complete - Some benchmarks have validation errors

### Phase 2: Performance Optimizations

**File:** [PHASE2_PERFORMANCE_REPORT.md](./PHASE2_PERFORMANCE_REPORT.md)

Compares Phase 1 vs Phase 2 performance after implementing:
1. **Entity Pooling System** - Reduces allocation overhead
2. **Bulk Entity Operations** - Batches operations for efficiency
3. **Parallel Query Execution** - Leverages multi-core CPUs

**Status:** ⏳ Template created, waiting for Phase 2 implementation completion

**Targets:**
- ✅ 2-3x faster entity spawning
- ✅ 50%+ reduction in GC allocations
- ✅ 20-30% reduction in frame time
- ✅ Near-linear CPU core scaling for parallel queries

### Benchmark Guide

**File:** [BENCHMARK_GUIDE.md](./BENCHMARK_GUIDE.md)

Comprehensive guide for running and interpreting benchmarks.

## 🚀 Quick Start

### Running Benchmarks

```bash
# From project root
cd scripts
./run_benchmarks.sh phase1    # Run Phase 1 baseline
./run_benchmarks.sh phase2    # Run Phase 2 optimizations
./run_benchmarks.sh all       # Run everything
./run_benchmarks.sh compare   # Check comparison status
```

Or manually:

```bash
cd tests/PokeSharp.Benchmarks

# Phase 1 Benchmarks
dotnet run -c Release -- entity    # Entity creation
dotnet run -c Release -- query     # Query performance
dotnet run -c Release -- system    # System updates
dotnet run -c Release -- memory    # Memory allocation
dotnet run -c Release -- spatial   # Spatial hashing

# Phase 2 Benchmarks (requires Phase 2 implementation)
dotnet run -c Release -- pooling   # Entity pooling
dotnet run -c Release -- bulk      # Bulk operations
dotnet run -c Release -- parallel  # Parallel execution
dotnet run -c Release -- phase2    # Integration scenarios
```

### Running Performance Tests

```bash
cd tests/Integration
dotnet test --filter "FullyQualifiedName~Phase2PerformanceValidation"
```

## 📁 File Structure

```
docs/performance/
├── README.md                          # This file
├── PHASE1_BASELINE.md                 # Phase 1 baseline metrics
├── PHASE2_PERFORMANCE_REPORT.md       # Phase 2 comparison report
└── BENCHMARK_GUIDE.md                 # Benchmark guide

tests/PokeSharp.Benchmarks/
├── BenchmarkBase.cs                   # Base benchmark class
├── EntityCreationBenchmarks.cs        # Phase 1: Entity creation
├── QueryBenchmarks.cs                 # Phase 1: Query performance
├── SystemBenchmarks.cs                # Phase 1: System updates
├── MemoryAllocationBenchmarks.cs      # Phase 1: Memory patterns
├── SpatialHashBenchmarks.cs           # Phase 1: Spatial hashing
├── PoolingBenchmarks.cs               # Phase 2: Entity pooling
├── BulkOperationsBenchmarks.cs        # Phase 2: Bulk operations
├── ParallelExecutionBenchmarks.cs     # Phase 2: Parallel queries
├── Phase2IntegrationBenchmarks.cs     # Phase 2: Real-world scenarios
└── Program.cs                         # Benchmark runner

tests/Integration/
└── Phase2PerformanceValidation.cs     # Performance regression tests

scripts/
└── run_benchmarks.sh                  # Automated benchmark runner
```

## 🎯 Performance Targets

### Phase 2 Optimization Goals

| Optimization | Target | Measurement |
|-------------|--------|-------------|
| **Entity Pooling** | 2-3x faster | Entity create/destroy time |
| **GC Reduction** | 50%+ less | Total bytes allocated |
| **Frame Time** | 20-30% faster | System update time |
| **Multi-core** | Near-linear scaling | Query speedup vs cores |

### 60 FPS Targets

| Entity Count | Target Frame Time | Phase 1 | Phase 2 |
|--------------|------------------|---------|---------|
| 100 entities | < 16.6ms | TBD | TBD |
| 1000 entities | < 16.6ms | TBD | TBD |
| 10000 entities | < 16.6ms | TBD | TBD |

## 🛠️ Benchmark Infrastructure

### Current Status

#### ✅ Completed

1. Created all Phase 2 benchmark files
2. Updated Program.cs with Phase 2 commands
3. Created performance validation tests
4. Created automated benchmark runner script
5. Established baseline report template
6. Created Phase 2 comparison report template

#### ⏳ In Progress

1. **Phase 2 Implementations** - Being developed by other agents:
   - Entity Pooling System (Coder agent)
   - Bulk Entity Operations (Coder agent)
   - Parallel Query Executor (System Architect agent)

#### 🐛 Known Issues

1. **Benchmark Validation Errors** - Several benchmarks have duplicate `[GlobalSetup]` attributes:
   - QueryBenchmarks
   - SystemBenchmarks
   - MemoryAllocationBenchmarks
   - SpatialHashBenchmarks

   **Fix:** Remove duplicate attributes or use `Target` parameter

2. **ParallelQueryExecutor Compilation Errors** - 38 errors related to `ref` keywords in lambdas
   - Being fixed by System Architect agent

3. **EntityCreationBenchmarks Build Error** - Failed to complete due to core library compilation errors

### Next Steps

1. **Immediate:**
   - Fix benchmark validation errors
   - Wait for Phase 2 implementations to complete
   - Verify all benchmarks compile and run

2. **After Phase 2 Implementation:**
   - Uncomment Phase 2 benchmark code
   - Run complete benchmark suite
   - Capture actual performance numbers
   - Generate comparison visualizations
   - Validate performance targets met

3. **Future:**
   - Add more real-world scenario benchmarks
   - Create performance regression CI/CD checks
   - Add memory profiling analysis
   - Create interactive performance dashboard

## 📚 Resources

### BenchmarkDotNet

- [Official Documentation](https://benchmarkdotnet.org/)
- [Best Practices](https://benchmarkdotnet.org/articles/guides/how-it-works.html)
- [Memory Diagnostics](https://benchmarkdotnet.org/articles/configs/diagnosers.html)

### ECS Performance

- [Arch ECS Documentation](https://github.com/genaray/Arch)
- [ECS Performance Patterns](https://github.com/SanderMertens/ecs-faq)
- [Data-Oriented Design](https://www.dataorienteddesign.com/)

### Phase 2 Optimization Resources

- **Entity Pooling:** Object pool pattern for reusing entities
- **Bulk Operations:** Batching to reduce archetype changes
- **Parallel Queries:** Multi-threading with Parallel.ForEach

## 🤝 Contributing

### Adding New Benchmarks

1. Create benchmark class inheriting from `BenchmarkBase`
2. Add `[MemoryDiagnoser]` attribute
3. Use `[Benchmark(Baseline = true)]` for baseline method
4. Add command to `Program.cs`
5. Update this README

### Running Benchmarks Locally

```bash
# Ensure you're in Release mode
dotnet build -c Release

# Run specific benchmark
cd tests/PokeSharp.Benchmarks
dotnet run -c Release -- <command>

# Results are saved to:
# BenchmarkDotNet.Artifacts/results/
```

### Interpreting Results

- **Mean:** Average execution time
- **Error:** Standard error of the mean
- **Ratio:** Relative to baseline (lower is better)
- **Allocated:** Memory allocated per operation
- **Gen 0/1/2:** Garbage collection counts

## 📞 Support

For questions or issues with benchmarks:
1. Check [BENCHMARK_GUIDE.md](./BENCHMARK_GUIDE.md)
2. Review BenchmarkDotNet [documentation](https://benchmarkdotnet.org/)
3. Check for compilation errors in Phase 2 implementations

---

**Last Updated:** November 9, 2024
**PokeSharp Version:** Phase 1 Complete, Phase 2 In Progress
**Performance Analyzer Agent:** Active
