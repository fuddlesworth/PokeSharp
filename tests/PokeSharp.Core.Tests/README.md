# PokeSharp.Core.Tests

Comprehensive test suite for PokeSharp.Core ECS framework, focusing on performance-critical components.

## Test Coverage

### 1. CommandBuffer Tests (`Commands/CommandBufferTests.cs`)
- **Recording Operations**: Tests for Create/Destroy/AddComponent/RemoveComponent
- **Playback Order**: Validates FIFO execution order
- **Thread Safety**: Concurrent recording from multiple threads
- **Exception Safety**: Ensures playback continues even if commands fail
- **Pooling**: Tests Rent/Return pattern for buffer reuse
- **Performance**: Large-scale command execution benchmarks

### 2. QueryResultCache Tests (`Cache/QueryResultCacheTests.cs`)
- **Cache Hit/Miss**: Validates caching behavior
- **Invalidation**: Tests cache invalidation on entity changes
- **Thread Safety**: Concurrent access from multiple threads
- **LRU Eviction**: Tests least-recently-used eviction policy
- **Statistics**: Validates hit rate tracking
- **Performance**: Large-scale query caching benchmarks

### 3. Integration Tests

#### CommandBuffer Integration (`Integration/CommandBufferIntegrationTests.cs`)
- System integration with real ECS systems
- Deferred entity spawning/destruction
- Parallel system execution with separate buffers
- System chain execution
- Performance under load (10,000 entities)
- Memory pooling effectiveness

#### QueryResultCache Integration (`Integration/QueryResultCacheIntegrationTests.cs`)
- ParallelQueryExecutor integration
- Multi-frame caching scenarios
- Dynamic world changes (entities added/removed)
- Complex query patterns
- Parallel thread execution
- Real-world game loop simulation
- Performance regression testing

## Running Tests

```bash
# Run all tests
dotnet test

# Run with detailed output
dotnet test -v detailed

# Run specific test class
dotnet test --filter "FullyQualifiedName~CommandBufferTests"

# Run in release mode (performance tests)
dotnet test -c Release

# Generate coverage report
dotnet test /p:CollectCoverage=true
```

## Test Structure

```
tests/PokeSharp.Core.Tests/
├── Commands/
│   └── CommandBufferTests.cs          # 13 unit tests
├── Cache/
│   └── QueryResultCacheTests.cs       # 12 unit tests
├── Integration/
│   ├── CommandBufferIntegrationTests.cs       # 7 integration tests
│   └── QueryResultCacheIntegrationTests.cs    # 10 integration tests
└── PokeSharp.Core.Tests.csproj
```

## Performance Targets

### CommandBuffer
- **Throughput**: 10,000 commands in <1 second
- **Frame Budget**: System update + playback <16ms (60 FPS)
- **Memory**: <1MB increase over 1000 frames with pooling

### QueryResultCache
- **Cache Hit Speed**: <10ms for 100 cache hits
- **Hit Rate**: >95% in steady-state game loop
- **Speedup**: >2x faster than uncached queries
- **Frame Time**: <16.67ms per frame for typical game scenario

## Dependencies

- **xUnit**: Test framework
- **FluentAssertions**: Readable assertions
- **Moq**: Mocking framework (for future use)

## Notes

**IMPORTANT**: These tests are designed to work with the implementations created by the coder agents. If implementations don't exist yet, tests will fail to compile until:

1. `/PokeSharp.Core/Commands/CommandBuffer.cs` is created
2. `/PokeSharp.Core/Cache/QueryResultCache.cs` is created
3. Supporting classes (World, Query, ParallelQueryExecutor) are available

The tests serve as both **validation** and **specification** for the implementations.
