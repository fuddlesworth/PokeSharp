# ECS Event System Test Specification

**Project**: PokeSharp Pokemon Engine
**System**: ECS Event Integration
**Test Framework**: xUnit 2.9.3
**Performance Framework**: BenchmarkDotNet 0.13.12
**ECS**: Arch ECS 2.1.0
**Date**: 2025-01-28

---

## Executive Summary

This document defines comprehensive test coverage for the ECS event system integration, covering unit tests, integration tests, performance benchmarks, and edge cases. Tests are prioritized by risk and impact, with performance thresholds based on game requirements (60 FPS target).

---

## 1. Test Categories & Priorities

### Priority Levels
- **P0 (Critical)**: Core functionality, breaks system if failing
- **P1 (High)**: Important features, degrades experience if failing
- **P2 (Medium)**: Nice-to-have features, minor impact
- **P3 (Low)**: Edge cases, documentation validation

### Test Distribution
```
Unit Tests:          ~40 tests (60% of coverage)
Integration Tests:   ~25 tests (30% of coverage)
Performance Tests:   ~10 benchmarks (10% of coverage)
Total:              ~75 tests
```

---

## 2. Unit Tests - Event Infrastructure (P0/P1)

### 2.1 Event Publishing & Subscription

**File**: `tests/PokeSharp.Game.Tests/Events/EventBusTests.cs`

```csharp
public class EventBusTests
{
    // P0: Basic publishing
    [Fact]
    public void Publish_WithValidEvent_ShouldNotifySubscribers();

    [Fact]
    public void Publish_WithNoSubscribers_ShouldNotThrow();

    // P0: Basic subscription
    [Fact]
    public void Subscribe_WithValidHandler_ShouldReceiveEvents();

    [Fact]
    public void Subscribe_MultipleHandlers_ShouldAllReceiveEvents();

    [Fact]
    public void Unsubscribe_ExistingHandler_ShouldStopReceivingEvents();

    // P1: Type safety
    [Fact]
    public void Subscribe_GenericType_ShouldOnlyReceiveMatchingEventType();

    [Theory]
    [InlineData(typeof(MovementEvent))]
    [InlineData(typeof(TileInteractionEvent))]
    [InlineData(typeof(MapTransitionEvent))]
    public void Publish_DifferentEventTypes_ShouldIsolateSubscribers(Type eventType);

    // P1: Memory management
    [Fact]
    public void Unsubscribe_AllHandlers_ShouldAllowGarbageCollection();

    [Fact]
    public void EventBus_DisposedInstance_ShouldClearAllSubscriptions();
}
```

**Fixtures Required**:
- `EventBusFixture`: Creates isolated EventBus instance
- `MockEventHandler<T>`: Tracks invocations and parameters

---

### 2.2 Event Ordering & Priority

**File**: `tests/PokeSharp.Game.Tests/Events/EventPriorityTests.cs`

```csharp
public class EventPriorityTests
{
    // P0: Priority ordering
    [Fact]
    public void Publish_WithPrioritizedHandlers_ShouldExecuteInPriorityOrder();

    [Theory]
    [InlineData(10, 5, 1)]  // High to low
    [InlineData(1, 1, 1)]   // Same priority (insertion order)
    [InlineData(-10, 0, 10)] // Negative priorities
    public void Subscribe_DifferentPriorities_ShouldOrderCorrectly(
        int priority1, int priority2, int priority3);

    // P1: Dynamic priority changes
    [Fact]
    public void ChangePriority_ExistingSubscription_ShouldReorderHandlers();

    // P1: Priority edge cases
    [Fact]
    public void Subscribe_IntMaxPriority_ShouldHandleCorrectly();

    [Fact]
    public void Subscribe_IntMinPriority_ShouldHandleCorrectly();
}
```

**Performance Threshold**:
- Priority sorting: < 1ms for 1000 subscribers

---

### 2.3 Event Cancellation

**File**: `tests/PokeSharp.Game.Tests/Events/EventCancellationTests.cs`

```csharp
public class EventCancellationTests
{
    // P0: Cancellation support
    [Fact]
    public void Event_WithCancellableInterface_ShouldSupportCancellation();

    [Fact]
    public void Cancel_BeforeHandlerExecution_ShouldStopPropagation();

    [Fact]
    public void Cancel_MidExecution_ShouldStopRemainingHandlers();

    // P1: Cancellation order
    [Fact]
    public void Cancel_InHighPriorityHandler_ShouldPreventLowerPriorityExecution();

    [Fact]
    public void Cancel_AfterNonCancellableHandlers_ShouldStillExecuteThem();

    // P1: Cancellation state
    [Fact]
    public void IsCancelled_AfterCancellation_ShouldReturnTrue();

    [Fact]
    public void CancelledBy_ShouldTrackOriginalCanceller();
}
```

**Mock Objects**:
- `CancellableTestEvent : ICancellable`
- `OrderTrackingHandler`: Records execution order

---

### 2.4 Thread Safety

**File**: `tests/PokeSharp.Game.Tests/Events/EventThreadSafetyTests.cs`

```csharp
public class EventThreadSafetyTests
{
    // P0: Concurrent publishing
    [Fact]
    public async Task Publish_FromMultipleThreads_ShouldNotCorruptState();

    [Fact]
    public async Task Subscribe_DuringPublish_ShouldNotCauseDeadlock();

    [Fact]
    public async Task Unsubscribe_DuringPublish_ShouldNotCauseExceptions();

    // P1: Race conditions
    [Theory]
    [InlineData(10)]   // 10 concurrent publishers
    [InlineData(100)]  // 100 concurrent publishers
    [InlineData(1000)] // 1000 concurrent publishers
    public async Task Publish_HighConcurrency_ShouldMaintainCorrectCount(
        int publisherCount);

    // P1: Memory barriers
    [Fact]
    public async Task Subscribe_CrossThread_ShouldBeVisibleImmediately();

    // P2: Stress test
    [Fact(Skip = "Stress test - run manually")]
    public async Task StressTest_10000ConcurrentOperations_ShouldNotFail();
}
```

**Performance Threshold**:
- Thread contention: < 10% overhead vs single-threaded
- No deadlocks under 1000 concurrent operations

---

### 2.5 Event Payload & Immutability

**File**: `tests/PokeSharp.Game.Tests/Events/EventPayloadTests.cs`

```csharp
public class EventPayloadTests
{
    // P0: Payload delivery
    [Fact]
    public void Event_WithComplexPayload_ShouldPreserveAllData();

    [Theory]
    [MemberData(nameof(GetEventPayloads))]
    public void Event_DifferentPayloadTypes_ShouldSerializeCorrectly(
        object payload);

    // P1: Immutability validation
    [Fact]
    public void Event_PublishedInstance_ShouldNotBeModifiableByHandlers();

    [Fact]
    public void Event_WithReadonlyStruct_ShouldPreventMutation();

    // P2: Large payloads
    [Fact]
    public void Event_WithLargePayload_ShouldNotCauseStackOverflow();

    public static IEnumerable<object[]> GetEventPayloads() => new[]
    {
        new object[] { new MovementEvent { Entity = 1, Direction = Direction.North } },
        new object[] { new TileInteractionEvent { TileX = 10, TileY = 20 } },
        new object[] { new MapTransitionEvent { SourceMap = "Route1", TargetMap = "Route2" } }
    };
}
```

---

## 3. Integration Tests - Script Events (P0/P1)

### 3.1 ScriptContext Integration

**File**: `tests/PokeSharp.Game.Tests/Integration/ScriptEventIntegrationTests.cs`

```csharp
public class ScriptEventIntegrationTests : IClassFixture<ScriptEngineFixture>
{
    private readonly ScriptEngineFixture _fixture;

    public ScriptEventIntegrationTests(ScriptEngineFixture fixture)
    {
        _fixture = fixture;
    }

    // P0: Basic script subscription
    [Fact]
    public async Task Script_SubscribeToEvent_ShouldReceivePublishedEvents();

    [Fact]
    public async Task Script_UnsubscribeFromEvent_ShouldStopReceivingEvents();

    // P0: Event handler execution
    [Fact]
    public async Task Script_EventHandler_ShouldExecuteInScriptContext();

    [Fact]
    public async Task Script_MultipleHandlers_ShouldAllExecute();

    // P1: Error handling
    [Fact]
    public async Task Script_HandlerThrowsException_ShouldNotCrashEventBus();

    [Fact]
    public async Task Script_HandlerThrowsException_ShouldLogError();

    // P1: Async handlers
    [Fact]
    public async Task Script_AsyncHandler_ShouldAwaitCompletion();

    [Fact]
    public async Task Script_AsyncHandler_ShouldNotBlockOtherHandlers();
}
```

**Script Sample** (`test-scripts/event-handler.csx`):
```csharp
// Subscribe to movement events
Context.Events.Subscribe<MovementEvent>(e =>
{
    Console.WriteLine($"Entity {e.Entity} moved {e.Direction}");
    TestResults.RecordExecution("MovementHandler");
});

// Subscribe with priority
Context.Events.Subscribe<TileInteractionEvent>(
    handler: e => TestResults.RecordExecution("TileHandler"),
    priority: 10
);
```

---

### 3.2 Hot Reload Event Re-subscription

**File**: `tests/PokeSharp.Game.Tests/Integration/ScriptHotReloadEventTests.cs`

```csharp
public class ScriptHotReloadEventTests : IClassFixture<ScriptEngineFixture>
{
    // P0: Hot reload basic
    [Fact]
    public async Task HotReload_WithEventSubscriptions_ShouldResubscribe();

    [Fact]
    public async Task HotReload_ShouldRemoveOldSubscriptions();

    // P1: Hot reload edge cases
    [Fact]
    public async Task HotReload_DuringEventPublish_ShouldNotCorrupt();

    [Fact]
    public async Task HotReload_ChangedHandlerPriority_ShouldUpdateOrder();

    [Fact]
    public async Task HotReload_AddedEventSubscription_ShouldRegisterNew();

    [Fact]
    public async Task HotReload_RemovedEventSubscription_ShouldUnregister();

    // P1: Memory leaks
    [Fact]
    public async Task HotReload_RepeatedReloads_ShouldNotLeakMemory();

    [Fact]
    public async Task HotReload_100Times_ShouldNotDegradePerformance();
}
```

**Performance Threshold**:
- Hot reload with 100 subscriptions: < 100ms
- Memory leak: 0 bytes after 1000 reloads (GC collected)

---

## 4. Integration Tests - Mod Events (P0/P1)

### 4.1 Mod Event Handler Registration

**File**: `tests/PokeSharp.Game.Tests/Integration/ModEventIntegrationTests.cs`

```csharp
public class ModEventIntegrationTests : IClassFixture<ModSystemFixture>
{
    // P0: Mod registration
    [Fact]
    public void Mod_RegisterEventHandler_ShouldReceiveEvents();

    [Fact]
    public void Mod_UnloadWithHandlers_ShouldUnregisterAll();

    // P0: Multiple mods
    [Fact]
    public void MultipleMods_SameEvent_ShouldAllReceiveEvent();

    [Fact]
    public void Mod_ConflictingHandlers_ShouldExecuteBoth();

    // P1: Mod isolation
    [Fact]
    public void Mod_EventHandler_ShouldNotAccessOtherModData();

    [Fact]
    public void Mod_ThrowsException_ShouldNotAffectOtherMods();
}
```

---

### 4.2 Mod Priority Ordering

**File**: `tests/PokeSharp.Game.Tests/Integration/ModEventPriorityTests.cs`

```csharp
public class ModEventPriorityTests
{
    // P0: Priority ordering
    [Fact]
    public void Mods_DifferentPriorities_ShouldExecuteInOrder();

    [Theory]
    [InlineData("CoreMod", 1000, "ExtensionMod", 500)]
    [InlineData("UIMod", 100, "DataMod", 50)]
    public void Mods_WithPriorityMetadata_ShouldRespectOrder(
        string mod1Name, int priority1, string mod2Name, int priority2);

    // P1: Dependency-based ordering
    [Fact]
    public void Mods_WithDependencies_ShouldExecuteDependenciesFirst();

    [Fact]
    public void Mods_CircularDependencies_ShouldDetectAndFail();

    [Fact]
    public void Mods_DependencyChain_ShouldResolveCorrectly();
}
```

**Mock Objects**:
- `TestMod`: Configurable priority and dependencies
- `ModDependencyGraph`: Validates dependency resolution

---

## 5. Performance Tests (P0/P1)

### 5.1 High-Frequency Event Publishing

**File**: `tests/PerformanceBenchmarks/EventSystemBenchmarks.cs`

```csharp
[MemoryDiagnoser]
[SimpleJob(RunStrategy.Throughput, warmupCount: 3, iterationCount: 10)]
public class EventSystemBenchmarks
{
    private EventBus _eventBus;
    private const int EventsPerFrame = 1000;

    [GlobalSetup]
    public void Setup()
    {
        _eventBus = new EventBus();
        // Setup 100 subscribers
        for (int i = 0; i < 100; i++)
        {
            _eventBus.Subscribe<MovementEvent>(e => { });
        }
    }

    // P0: Frame budget compliance
    [Benchmark]
    public void PublishEvents_1000PerFrame_60FPS()
    {
        // Target: < 16.67ms total (60 FPS)
        for (int i = 0; i < EventsPerFrame; i++)
        {
            _eventBus.Publish(new MovementEvent
            {
                Entity = i,
                Direction = Direction.North
            });
        }
    }

    [Benchmark]
    public void PublishEvents_10000PerFrame_Stress()
    {
        // Stress test: 10x normal load
        for (int i = 0; i < 10000; i++)
        {
            _eventBus.Publish(new MovementEvent
            {
                Entity = i,
                Direction = Direction.North
            });
        }
    }
}
```

**Performance Thresholds**:
- 1000 events/frame with 100 subscribers: < 16.67ms (60 FPS)
- Memory allocation per event: < 200 bytes
- GC collections per 1000 frames: < 10 Gen0, 0 Gen1/Gen2

---

### 5.2 Memory Allocation Patterns

**File**: `tests/PerformanceBenchmarks/EventAllocationBenchmarks.cs`

```csharp
[MemoryDiagnoser]
public class EventAllocationBenchmarks
{
    // P0: Zero-allocation event publishing
    [Benchmark]
    public void Publish_StructEvent_ShouldNotAllocate()
    {
        var eventBus = new EventBus();
        var evt = new MovementEvent { Entity = 1, Direction = Direction.North };

        for (int i = 0; i < 1000; i++)
        {
            eventBus.Publish(evt); // Struct copy, no heap allocation
        }
    }

    // P1: Subscription allocation
    [Benchmark]
    public void Subscribe_100Handlers_MeasureAllocation()
    {
        var eventBus = new EventBus();

        for (int i = 0; i < 100; i++)
        {
            eventBus.Subscribe<MovementEvent>(e => { });
        }
    }

    // P1: Pooled event objects
    [Benchmark]
    public void Publish_PooledEvents_VsNewEvents()
    {
        var eventBus = new EventBus();
        var pool = new EventPool<MovementEvent>();

        for (int i = 0; i < 1000; i++)
        {
            var evt = pool.Rent();
            evt.Entity = i;
            evt.Direction = Direction.North;
            eventBus.Publish(evt);
            pool.Return(evt);
        }
    }
}
```

**Performance Thresholds**:
- Struct event publish: 0 bytes allocated
- Handler subscription: < 100 bytes per subscription
- Pooled events: 90% reduction vs new allocations

---

### 5.3 Cache Efficiency

**File**: `tests/PerformanceBenchmarks/EventCacheBenchmarks.cs`

```csharp
[HardwareCounters(HardwareCounter.CacheMisses, HardwareCounter.BranchMispredictions)]
public class EventCacheBenchmarks
{
    // P1: Cache-friendly subscriber iteration
    [Benchmark]
    public void IterateSubscribers_SequentialAccess()
    {
        // Measure cache misses for subscriber list iteration
        var eventBus = CreateEventBusWithSubscribers(1000);

        for (int i = 0; i < 100; i++)
        {
            eventBus.Publish(new MovementEvent { Entity = i });
        }
    }

    // P1: Event type dispatch cache
    [Benchmark]
    public void EventDispatch_TypeCache_HitRate()
    {
        var eventBus = new EventBus();
        RegisterMultipleEventTypes(eventBus);

        // Publish same event types repeatedly (should hit cache)
        for (int i = 0; i < 1000; i++)
        {
            eventBus.Publish(new MovementEvent { Entity = i });
            eventBus.Publish(new TileInteractionEvent { TileX = i, TileY = i });
        }
    }
}
```

**Performance Thresholds**:
- Cache miss rate: < 5% for subscriber iteration
- Type dispatch cache hit rate: > 95%

---

## 6. Integration Tests - Game Systems (P0/P1)

### 6.1 Movement System Events

**File**: `tests/PokeSharp.Game.Tests/Integration/MovementSystemEventTests.cs`

```csharp
public class MovementSystemEventTests : IClassFixture<EcsWorldFixture>
{
    // P0: Movement event publishing
    [Fact]
    public void MovementSystem_EntityMoves_ShouldPublishEvent();

    [Fact]
    public void MovementSystem_CollisionOccurs_ShouldPublishCollisionEvent();

    [Fact]
    public void MovementSystem_TileTransition_ShouldPublishTileChangeEvent();

    // P1: Movement event ordering
    [Fact]
    public void MovementSystem_MultipleEntities_ShouldPublishInUpdateOrder();

    [Fact]
    public void MovementSystem_CancelledMovement_ShouldNotUpdatePosition();

    // P1: Integration with scripts
    [Fact]
    public async Task MovementSystem_ScriptCancelsMovement_ShouldRespectCancellation();
}
```

---

### 6.2 Tile Behavior Events

**File**: `tests/PokeSharp.Game.Tests/Integration/TileBehaviorEventTests.cs`

```csharp
public class TileBehaviorEventTests
{
    // P0: Tile interaction
    [Theory]
    [InlineData(TileType.Grass, "GrassEncounter")]
    [InlineData(TileType.Water, "WaterSplash")]
    [InlineData(TileType.LedgeJump, "LedgeJump")]
    public void TileBehavior_DifferentTypes_ShouldPublishCorrectEvents(
        TileType tileType, string expectedEventName);

    // P1: Custom tile behaviors
    [Fact]
    public void CustomTileBehavior_ModRegistered_ShouldExecute();

    [Fact]
    public void CustomTileBehavior_ScriptRegistered_ShouldExecute();
}
```

---

### 6.3 Map Streaming Events

**File**: `tests/PokeSharp.Game.Tests/Integration/MapStreamingEventTests.cs`

```csharp
public class MapStreamingEventTests
{
    // P0: Map lifecycle events
    [Fact]
    public async Task MapStreaming_LoadStart_ShouldPublishLoadStartEvent();

    [Fact]
    public async Task MapStreaming_LoadComplete_ShouldPublishLoadCompleteEvent();

    [Fact]
    public async Task MapStreaming_Unload_ShouldPublishUnloadEvent();

    // P1: Event timing
    [Fact]
    public async Task MapStreaming_Events_ShouldFireInCorrectOrder();

    [Fact]
    public async Task MapStreaming_CancelLoad_ShouldPublishCancelEvent();
}
```

---

## 7. Edge Cases & Error Handling (P1/P2)

### 7.1 Error Scenarios

**File**: `tests/PokeSharp.Game.Tests/Events/EventErrorHandlingTests.cs`

```csharp
public class EventErrorHandlingTests
{
    // P1: Handler exceptions
    [Fact]
    public void Handler_ThrowsException_ShouldCatchAndLog();

    [Fact]
    public void Handler_ThrowsException_ShouldContinueOtherHandlers();

    [Fact]
    public void Handler_ThrowsOutOfMemory_ShouldPropagate();

    // P1: Invalid operations
    [Fact]
    public void Publish_NullEvent_ShouldThrowArgumentNullException();

    [Fact]
    public void Subscribe_NullHandler_ShouldThrowArgumentNullException();

    [Fact]
    public void Unsubscribe_NonExistentHandler_ShouldNotThrow();

    // P2: Resource exhaustion
    [Fact]
    public void Subscribe_10000Handlers_ShouldNotCauseOOM();

    [Fact]
    public void Publish_DuringDisposal_ShouldHandleGracefully();
}
```

---

### 7.2 Boundary Conditions

**File**: `tests/PokeSharp.Game.Tests/Events/EventBoundaryTests.cs`

```csharp
public class EventBoundaryTests
{
    // P2: Empty scenarios
    [Fact]
    public void Publish_NoSubscribers_ShouldCompleteQuickly();

    [Fact]
    public void EventBus_NoEvents_ShouldNotConsumeMemory();

    // P2: Maximum scenarios
    [Theory]
    [InlineData(1_000)]
    [InlineData(10_000)]
    [InlineData(100_000)]
    public void Subscribe_ManyHandlers_ShouldScaleLinearly(int handlerCount);

    [Fact]
    public void Event_MaxPayloadSize_ShouldHandle();

    // P2: Timing edge cases
    [Fact]
    public async Task Publish_ImmediatelyAfterSubscribe_ShouldReceive();

    [Fact]
    public async Task Unsubscribe_DuringEventHandling_ShouldNotCrash();
}
```

---

## 8. Test Infrastructure

### 8.1 Shared Fixtures

**File**: `tests/PokeSharp.Game.Tests/Fixtures/EventTestFixtures.cs`

```csharp
public class EventBusFixture : IDisposable
{
    public EventBus EventBus { get; }

    public EventBusFixture()
    {
        EventBus = new EventBus();
    }

    public void Dispose()
    {
        EventBus.Dispose();
    }
}

public class EcsWorldFixture : IDisposable
{
    public World World { get; }
    public EventBus EventBus { get; }

    public EcsWorldFixture()
    {
        World = World.Create();
        EventBus = new EventBus();
    }

    public void Dispose()
    {
        World.Dispose();
        EventBus.Dispose();
    }
}

public class ScriptEngineFixture : IAsyncLifetime
{
    public ScriptEngine Engine { get; private set; }
    public EventBus EventBus { get; private set; }

    public async Task InitializeAsync()
    {
        EventBus = new EventBus();
        Engine = await ScriptEngine.CreateAsync(new ScriptEngineOptions
        {
            EventBus = EventBus
        });
    }

    public async Task DisposeAsync()
    {
        await Engine.DisposeAsync();
        EventBus.Dispose();
    }
}
```

---

### 8.2 Mock Objects & Helpers

**File**: `tests/PokeSharp.Game.Tests/Mocks/EventMocks.cs`

```csharp
public class MockEventHandler<TEvent> where TEvent : struct
{
    private readonly List<TEvent> _receivedEvents = new();
    private readonly List<DateTime> _timestamps = new();

    public IReadOnlyList<TEvent> ReceivedEvents => _receivedEvents;
    public int CallCount => _receivedEvents.Count;

    public void Handle(TEvent evt)
    {
        _receivedEvents.Add(evt);
        _timestamps.Add(DateTime.UtcNow);
    }

    public void Reset()
    {
        _receivedEvents.Clear();
        _timestamps.Clear();
    }

    public void AssertReceivedEvent(TEvent expectedEvent)
    {
        Assert.Contains(expectedEvent, _receivedEvents);
    }

    public void AssertCallCount(int expectedCount)
    {
        Assert.Equal(expectedCount, CallCount);
    }
}

public class OrderTrackingHandler
{
    private static int _globalCounter = 0;
    private readonly List<(int Order, string HandlerName)> _executionOrder = new();

    public IReadOnlyList<(int Order, string HandlerName)> ExecutionOrder => _executionOrder;

    public Action<MovementEvent> CreateHandler(string name)
    {
        return evt =>
        {
            var order = Interlocked.Increment(ref _globalCounter);
            _executionOrder.Add((order, name));
        };
    }

    public void AssertExecutionOrder(params string[] expectedOrder)
    {
        var actualOrder = _executionOrder
            .OrderBy(x => x.Order)
            .Select(x => x.HandlerName)
            .ToArray();

        Assert.Equal(expectedOrder, actualOrder);
    }
}
```

---

### 8.3 Test Data Builders

**File**: `tests/PokeSharp.Game.Tests/Builders/EventBuilders.cs`

```csharp
public class MovementEventBuilder
{
    private int _entity = 1;
    private Direction _direction = Direction.North;
    private Vector2 _position = Vector2.Zero;

    public MovementEventBuilder WithEntity(int entity)
    {
        _entity = entity;
        return this;
    }

    public MovementEventBuilder WithDirection(Direction direction)
    {
        _direction = direction;
        return this;
    }

    public MovementEventBuilder WithPosition(float x, float y)
    {
        _position = new Vector2(x, y);
        return this;
    }

    public MovementEvent Build()
    {
        return new MovementEvent
        {
            Entity = _entity,
            Direction = _direction,
            Position = _position
        };
    }
}

public static class EventBuilders
{
    public static MovementEventBuilder Movement() => new();
    public static TileInteractionEventBuilder TileInteraction() => new();
    public static MapTransitionEventBuilder MapTransition() => new();
}
```

---

## 9. Performance Thresholds Summary

| Metric | Threshold | Priority |
|--------|-----------|----------|
| Event publish (1000 events, 100 subscribers) | < 16.67ms | P0 |
| Event subscribe (single handler) | < 0.1ms | P0 |
| Event unsubscribe (single handler) | < 0.1ms | P0 |
| Priority sorting (1000 subscribers) | < 1ms | P1 |
| Memory allocation per event publish | < 200 bytes | P0 |
| GC Gen0 per 1000 frames | < 10 collections | P1 |
| GC Gen1/Gen2 per 1000 frames | 0 collections | P0 |
| Thread contention overhead | < 10% | P1 |
| Cache miss rate (subscriber iteration) | < 5% | P2 |
| Type dispatch cache hit rate | > 95% | P2 |
| Hot reload with 100 subscriptions | < 100ms | P1 |
| Struct event publish allocation | 0 bytes | P0 |

---

## 10. Test Execution Strategy

### 10.1 CI/CD Integration

```yaml
# .github/workflows/test-events.yml
name: Event System Tests

on: [push, pull_request]

jobs:
  unit-tests:
    runs-on: ubuntu-latest
    steps:
      - uses: actions/checkout@v3
      - name: Setup .NET
        uses: actions/setup-dotnet@v3
        with:
          dotnet-version: '9.0.x'

      - name: Run Unit Tests
        run: |
          dotnet test tests/PokeSharp.Game.Tests/Events/ \
            --filter Category=Unit \
            --logger "console;verbosity=detailed"

      - name: Generate Coverage Report
        run: |
          dotnet test tests/PokeSharp.Game.Tests/Events/ \
            /p:CollectCoverage=true \
            /p:CoverletOutputFormat=cobertura \
            /p:Threshold=80

  integration-tests:
    runs-on: ubuntu-latest
    steps:
      - name: Run Integration Tests
        run: |
          dotnet test tests/PokeSharp.Game.Tests/Integration/ \
            --filter Category=Integration

  performance-tests:
    runs-on: ubuntu-latest
    steps:
      - name: Run Benchmarks
        run: |
          dotnet run --project tests/PerformanceBenchmarks/ \
            --configuration Release \
            --filter "*EventSystem*"

      - name: Compare to Baseline
        run: |
          dotnet run --project tests/PerformanceBenchmarks/ \
            --configuration Release \
            --filter "*EventAllocation*" \
            --compare baseline-results.json
```

---

### 10.2 Local Test Execution

```bash
# Run all event tests
dotnet test tests/PokeSharp.Game.Tests/ --filter FullyQualifiedName~Events

# Run specific category
dotnet test --filter Category=EventPriority

# Run performance benchmarks
dotnet run --project tests/PerformanceBenchmarks/ --configuration Release

# Run with coverage
dotnet test /p:CollectCoverage=true /p:CoverletOutputFormat=opencover

# Watch mode for TDD
dotnet watch test tests/PokeSharp.Game.Tests/Events/
```

---

## 11. Success Criteria

### Coverage Requirements
- **Line Coverage**: ≥ 80%
- **Branch Coverage**: ≥ 75%
- **Method Coverage**: ≥ 85%

### Performance Gates
- All P0 performance thresholds must pass
- No performance regression > 5% vs baseline
- Memory allocation < baseline + 10%

### Quality Gates
- Zero P0 test failures
- < 5% P1 test failures (with documented exceptions)
- No unhandled exceptions in test execution
- All benchmarks complete without timeout

---

## 12. Test Maintenance

### Review Schedule
- **Weekly**: Review new test failures
- **Sprint End**: Update performance baselines
- **Release**: Full benchmark comparison

### Documentation Requirements
- All P0 tests must have XML documentation
- Integration tests must document prerequisites
- Performance tests must document baseline source
- Failed tests must have GitHub issues created

---

## Appendix A: Sample Event Types

```csharp
// Movement event (struct for zero-allocation)
public readonly struct MovementEvent
{
    public int Entity { get; init; }
    public Direction Direction { get; init; }
    public Vector2 Position { get; init; }
}

// Cancellable tile interaction event
public struct TileInteractionEvent : ICancellable
{
    public int Entity { get; init; }
    public int TileX { get; init; }
    public int TileY { get; init; }
    public TileType TileType { get; init; }

    public bool IsCancelled { get; set; }
    public string? CancelReason { get; set; }
}

// Map transition event (class for complex data)
public class MapTransitionEvent
{
    public required string SourceMap { get; init; }
    public required string TargetMap { get; init; }
    public Vector2 TransitionPoint { get; init; }
    public TransitionType Type { get; init; }
}
```

---

## Appendix B: Test File Organization

```
tests/
├── PokeSharp.Game.Tests/
│   ├── Events/
│   │   ├── EventBusTests.cs                  (Unit - P0)
│   │   ├── EventPriorityTests.cs             (Unit - P0)
│   │   ├── EventCancellationTests.cs         (Unit - P0)
│   │   ├── EventThreadSafetyTests.cs         (Unit - P0)
│   │   ├── EventPayloadTests.cs              (Unit - P1)
│   │   ├── EventErrorHandlingTests.cs        (Unit - P1)
│   │   └── EventBoundaryTests.cs             (Unit - P2)
│   ├── Integration/
│   │   ├── ScriptEventIntegrationTests.cs    (Integration - P0)
│   │   ├── ScriptHotReloadEventTests.cs      (Integration - P1)
│   │   ├── ModEventIntegrationTests.cs       (Integration - P0)
│   │   ├── ModEventPriorityTests.cs          (Integration - P1)
│   │   ├── MovementSystemEventTests.cs       (Integration - P0)
│   │   ├── TileBehaviorEventTests.cs         (Integration - P1)
│   │   └── MapStreamingEventTests.cs         (Integration - P1)
│   ├── Fixtures/
│   │   └── EventTestFixtures.cs
│   ├── Mocks/
│   │   └── EventMocks.cs
│   └── Builders/
│       └── EventBuilders.cs
└── PerformanceBenchmarks/
    ├── EventSystemBenchmarks.cs              (Performance - P0)
    ├── EventAllocationBenchmarks.cs          (Performance - P0)
    └── EventCacheBenchmarks.cs               (Performance - P1)
```

---

**END OF SPECIFICATION**
