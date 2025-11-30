# Test Coverage Plan: EventBus Integration & Warp System Fixes

## Executive Summary

This document outlines the comprehensive test coverage needed for the EventBus integration and warp system fixes in PokeSharp. Based on analysis of existing tests and implementation code, this plan identifies gaps and prioritizes new test development.

---

## 1. EXISTING TEST COVERAGE

### 1.1 Warp System Tests
**Location**: None found
- **Status**: ❌ NO TESTS EXIST
- **Impact**: Critical - warp system is completely untested

### 1.2 EventBus Tests
**Location**: None found
- **Status**: ❌ NO TESTS EXIST
- **Impact**: Critical - event bus is completely untested despite being core infrastructure

### 1.3 Map Connection Tests
**Location**: `tests/MapConnections/`
**Existing Tests**:
- `BoundaryAlignmentTests.cs` - Boundary alignment verification (PLACEHOLDER implementations)
- `CoordinateOffsetReproductionTests.cs` - Coordinate offset testing
- `ExpectedVsActualTests.cs` - Expected vs actual behavior validation
- `MinimalReproductionTests.cs` - Minimal reproduction cases

**Status**: ⚠️ PARTIAL - Tests exist but many are placeholder implementations

### 1.4 Map Streaming Tests
**Location**: `tests/PokeSharp.Game.Tests/Systems/MapStreamingSystemTests.cs`
**Coverage**: ✅ COMPREHENSIVE (35+ tests)
- Boundary detection (all 4 directions + corners)
- Map loading/unloading
- World offset calculations
- Edge cases and transitions

**Status**: ✅ EXCELLENT

### 1.5 Map Data Tests
**Location**: `tests/PokeSharp.Game.Data.Tests/MapConnectionTests.cs`
**Coverage**: ✅ COMPREHENSIVE (20+ tests)
- Connection parsing from JSON
- Offset calculations
- Connection validation
- Real-world examples

**Status**: ✅ EXCELLENT

---

## 2. REQUIRED TEST INFRASTRUCTURE

### 2.1 New Test Projects Needed

#### Option A: Create Dedicated Projects
```
tests/PokeSharp.Engine.Core.Tests/     (for EventBus)
  └── Events/
      └── EventBusTests.cs

tests/PokeSharp.Game.Systems.Tests/    (for WarpSystem)
  └── Warps/
      ├── WarpSystemTests.cs
      ├── WarpExecutionSystemTests.cs
      └── WarpIntegrationTests.cs
```

#### Option B: Extend Existing Projects
```
tests/PokeSharp.Game.Tests/
  └── Systems/
      └── Warps/                       (NEW)
          ├── WarpSystemTests.cs
          ├── WarpExecutionSystemTests.cs
          └── WarpIntegrationTests.cs

tests/PokeSharp.Engine.Debug.Tests/
  └── Events/                          (NEW)
      └── EventBusTests.cs
```

**Recommendation**: Option B - Extend existing projects to maintain consistency

### 2.2 Required NuGet Packages
All test projects should have:
- `xUnit` (already present)
- `FluentAssertions` (already present)
- `Moq` or `NSubstitute` (for mocking)
- `Microsoft.Extensions.Logging.Abstractions` (already present)

---

## 3. EVENTBUS TEST SUITE

### 3.1 Unit Tests: EventBusTests.cs
**Location**: `tests/PokeSharp.Engine.Debug.Tests/Events/EventBusTests.cs`

#### Core Functionality Tests
```csharp
// Subscription Management
- Subscribe_ValidHandler_ShouldRegisterSuccessfully()
- Subscribe_NullHandler_ShouldThrowArgumentNullException()
- Subscribe_MultipleHandlers_ShouldAllReceiveEvents()
- Unsubscribe_ValidSubscription_ShouldRemoveHandler()
- Unsubscribe_AfterDispose_ShouldNotReceiveEvents()
- GetSubscriberCount_NoSubscribers_ShouldReturnZero()
- GetSubscriberCount_MultipleSubscribers_ShouldReturnCorrectCount()

// Event Publishing
- Publish_WithSubscribers_ShouldInvokeAllHandlers()
- Publish_WithoutSubscribers_ShouldNotThrow()
- Publish_NullEvent_ShouldThrowArgumentNullException()
- Publish_HandlerThrows_ShouldIsolateErrorAndContinue()
- Publish_MultipleEventTypes_ShouldOnlyNotifyCorrectSubscribers()

// Priority/Ordering Tests
- Publish_MultipleHandlers_ShouldExecuteInRegistrationOrder()

// Cleanup Tests
- ClearSubscriptions_SpecificEventType_ShouldRemoveOnlyThatType()
- ClearAllSubscriptions_ShouldRemoveAllHandlers()
- Dispose_Subscription_ShouldBeIdempotent()
```

#### Concurrency Tests
```csharp
// Thread Safety
- Subscribe_Concurrent_ShouldBeThreadSafe()
- Publish_Concurrent_ShouldBeThreadSafe()
- Unsubscribe_ConcurrentWithPublish_ShouldNotCauseDeadlock()
- MultipleThreads_SubscribePublishUnsubscribe_ShouldBeConsistent()
```

#### Memory Leak Tests
```csharp
// Memory Management
- Unsubscribe_ShouldReleaseHandlerReference()
- ClearSubscriptions_ShouldReleaseAllReferences()
- DisposedSubscription_ShouldNotPreventGarbageCollection()
```

**Total EventBus Tests**: ~25 tests

---

## 4. WARP SYSTEM TEST SUITE

### 4.1 Unit Tests: WarpSystemTests.cs
**Location**: `tests/PokeSharp.Game.Tests/Systems/Warps/WarpSystemTests.cs`

#### Warp Detection Tests
```csharp
// Basic Warp Detection
- ProcessPlayerWarp_OnWarpTile_ShouldCreatePendingWarp()
- ProcessPlayerWarp_NotOnWarpTile_ShouldNotCreateWarp()
- ProcessPlayerWarp_PlayerMoving_ShouldNotDetectWarp()
- ProcessPlayerWarp_MovementLocked_ShouldNotDetectWarp()

// Re-Warp Prevention
- ProcessPlayerWarp_AtLastDestination_ShouldNotReWarp()
- ProcessPlayerWarp_MovedAwayFromDestination_ShouldClearTracking()
- ProcessPlayerWarp_SamePositionDifferentMap_ShouldAllowWarp()

// State Management
- ProcessPlayerWarp_AlreadyWarping_ShouldSkip()
- ProcessPlayerWarp_FoundWarp_ShouldLockMovement()
- ProcessPlayerWarp_FoundWarp_ShouldSetIsWarpingFlag()
- ProcessPlayerWarp_FoundWarp_ShouldRecordWarpStartTime()

// Spatial Lookup
- BuildMapWarpCache_MultipleMapEntities_ShouldCacheAll()
- TryFindWarp_ValidPosition_ShouldReturnTrue()
- TryFindWarp_InvalidPosition_ShouldReturnFalse()
- TryFindWarp_MapNotLoaded_ShouldReturnFalse()
- TryFindWarp_WarpEntityMissingComponent_ShouldReturnFalse()
```

**Total WarpSystem Tests**: ~15 tests

### 4.2 Unit Tests: WarpExecutionSystemTests.cs
**Location**: `tests/PokeSharp.Game.Tests/Systems/Warps/WarpExecutionSystemTests.cs`

#### Async Map Loading Tests
```csharp
// Map Loading
- ExecuteWarp_ValidMap_ShouldLoadSuccessfully()
- ExecuteWarp_InvalidMap_ShouldHandleGracefully()
- ExecuteWarp_MapLoadFails_ShouldResetPlayerState()
- ExecuteWarp_UnloadsCurrentMap_BeforeLoadingNew()

// Player Teleportation
- TeleportPlayer_ShouldUpdatePosition()
- TeleportPlayer_ShouldSetCorrectMapId()
- TeleportPlayer_ShouldSyncPixelsToGrid()
- TeleportPlayer_ShouldSetElevationTo3()
- TeleportPlayer_ShouldUpdateMapStreaming()
- TeleportPlayer_ShouldCompleteMovement()
- TeleportPlayer_ShouldUnlockMovement()

// Camera Behavior
- TeleportPlayer_ShouldSnapCameraToNewPosition()
- TeleportPlayer_CameraShouldBeCenteredOnPlayer()
- TeleportPlayer_CameraShouldBypassSmoothing()
- TeleportPlayer_CameraShouldBeMarkedDirty()

// Timeout Handling
- ExecuteWarp_Timeout_ShouldCancelWarp()
- ExecuteWarp_Timeout_ShouldUnlockMovement()
- ExecuteWarp_Timeout_ShouldLogWarning()

// Error Handling
- ExecuteWarp_Exception_ShouldResetState()
- ExecuteWarp_Exception_ShouldNotCrashSystem()
- ExecuteWarp_ServicesNull_ShouldNotExecute()

// Duplicate Prevention
- ExecuteWarp_AlreadyExecuting_ShouldSkip()
- ExecuteWarp_CompletesBeforeNext_ShouldAllowNext()
```

**Total WarpExecutionSystem Tests**: ~20 tests

### 4.3 Integration Tests: WarpIntegrationTests.cs
**Location**: `tests/Integration/WarpIntegrationTests.cs`

#### End-to-End Warp Tests
```csharp
// Complete Warp Flow
- CompleteWarpFlow_PlayerEntersWarp_ShouldTeleportToDestination()
- CompleteWarpFlow_LandOnWarpTile_ShouldNotImmediatelyReWarp()
- CompleteWarpFlow_WarpToNewMap_ShouldUnloadOldMap()
- CompleteWarpFlow_WarpToNewMap_ShouldLoadNewMap()
- CompleteWarpFlow_CameraFollowsPlayer_ThroughWarp()

// Event Bus Integration
- WarpFlow_ShouldPublishWarpStartedEvent()
- WarpFlow_ShouldPublishMapUnloadedEvent()
- WarpFlow_ShouldPublishMapLoadedEvent()
- WarpFlow_ShouldPublishWarpCompletedEvent()
- WarpFlow_EventsShouldFireInCorrectOrder()

// Multi-Map Scenarios
- WarpFlow_BetweenAdjacentMaps_ShouldMaintainState()
- WarpFlow_LongDistance_ShouldHandleCorrectly()
- WarpFlow_BackAndForth_ShouldAllowBidirectional()

// Error Recovery
- WarpFlow_MapLoadFailure_PlayerShouldRecoverGracefully()
- WarpFlow_Interrupted_ShouldCleanupProperly()

// Memory Cleanup
- WarpFlow_AfterMultipleWarps_ShouldNotLeakMemory()
- WarpFlow_OldMapUnloaded_EntitiesShouldBeDestroyed()
```

**Total Integration Tests**: ~15 tests

---

## 5. EVENT BUS WARP INTEGRATION TESTS

### 5.1 Event Publishing Tests
**Location**: `tests/Integration/EventBusWarpIntegrationTests.cs`

```csharp
// Warp Events
- WarpSystem_DetectsWarp_ShouldPublishWarpTriggeredEvent()
- WarpExecutionSystem_BeforeMapLoad_ShouldPublishWarpStartedEvent()
- WarpExecutionSystem_AfterMapUnload_ShouldPublishMapUnloadedEvent()
- WarpExecutionSystem_AfterMapLoad_ShouldPublishMapLoadedEvent()
- WarpExecutionSystem_AfterTeleport_ShouldPublishWarpCompletedEvent()
- WarpExecutionSystem_OnError_ShouldPublishWarpFailedEvent()

// Event Data Validation
- WarpEvents_ShouldContainCorrectSourceMap()
- WarpEvents_ShouldContainCorrectDestinationMap()
- WarpEvents_ShouldContainCorrectPlayerPosition()
- WarpEvents_ShouldContainCorrectTimestamp()

// Cancellable Events
- WarpTriggeredEvent_Cancelled_ShouldPreventWarp()
- MapUnloadedEvent_Cancelled_ShouldAbortWarp()

// Event Ordering
- WarpFlow_Events_ShouldFireInCorrectOrder()
- WarpFlow_Events_ShouldBeAtomic()
```

**Total Event Integration Tests**: ~15 tests

---

## 6. TEST PRIORITY MATRIX

### Priority 1 (CRITICAL - Implement First)
These tests validate core functionality and prevent regressions:

1. **EventBus Core Tests** (Week 1)
   - `EventBusTests.cs`: Subscribe, Publish, Unsubscribe (12 tests)
   - Priority: CRITICAL - No existing coverage

2. **WarpSystem Detection Tests** (Week 1)
   - `WarpSystemTests.cs`: Warp detection, re-warp prevention (10 tests)
   - Priority: CRITICAL - Core warp logic

3. **WarpExecutionSystem Tests** (Week 2)
   - `WarpExecutionSystemTests.cs`: Map loading, teleportation (15 tests)
   - Priority: CRITICAL - Async behavior needs validation

### Priority 2 (HIGH - Implement Second)
These tests validate integration and edge cases:

4. **EventBus Thread Safety Tests** (Week 2)
   - `EventBusTests.cs`: Concurrency tests (5 tests)
   - Priority: HIGH - Thread safety is important

5. **Warp Integration Tests** (Week 3)
   - `WarpIntegrationTests.cs`: End-to-end flows (10 tests)
   - Priority: HIGH - Validates complete scenarios

6. **Event Bus Warp Integration** (Week 3)
   - `EventBusWarpIntegrationTests.cs`: Event publishing (10 tests)
   - Priority: HIGH - New feature validation

### Priority 3 (MEDIUM - Implement Third)
These tests validate performance and edge cases:

7. **EventBus Memory Tests** (Week 4)
   - `EventBusTests.cs`: Memory leak detection (3 tests)
   - Priority: MEDIUM - Important but not blocking

8. **Camera Snap Tests** (Week 4)
   - `WarpExecutionSystemTests.cs`: Camera behavior (4 tests)
   - Priority: MEDIUM - Visual quality

9. **Timeout & Error Tests** (Week 4)
   - `WarpExecutionSystemTests.cs`: Timeout handling (5 tests)
   - Priority: MEDIUM - Edge case coverage

---

## 7. IMPLEMENTATION ROADMAP

### Week 1: Foundation
**Days 1-2**: EventBus Core Tests
- Set up test project structure
- Implement subscription/publish tests
- Implement cleanup tests
- **Deliverable**: 12 passing EventBus tests

**Days 3-5**: WarpSystem Detection Tests
- Set up warp test infrastructure
- Implement warp detection tests
- Implement re-warp prevention tests
- **Deliverable**: 10 passing WarpSystem tests

### Week 2: Execution & Concurrency
**Days 1-3**: WarpExecutionSystem Tests
- Implement async map loading tests
- Implement teleportation tests
- Implement camera snap tests
- **Deliverable**: 15 passing WarpExecutionSystem tests

**Days 4-5**: EventBus Concurrency Tests
- Implement thread safety tests
- Implement concurrent publish tests
- **Deliverable**: 5 passing concurrency tests

### Week 3: Integration
**Days 1-3**: Warp Integration Tests
- Implement end-to-end warp flows
- Implement multi-map scenarios
- Implement error recovery tests
- **Deliverable**: 10 passing integration tests

**Days 4-5**: Event Bus Integration
- Implement event publishing tests
- Implement event ordering tests
- Implement cancellable event tests
- **Deliverable**: 10 passing event integration tests

### Week 4: Polish & Edge Cases
**Days 1-2**: Memory & Performance Tests
- Implement memory leak tests
- Implement performance benchmarks
- **Deliverable**: 5 passing performance tests

**Days 3-5**: Documentation & Cleanup
- Document test patterns
- Update README files
- Code review and refinement
- **Deliverable**: Complete test documentation

---

## 8. TEST NAMING CONVENTIONS

Follow existing PokeSharp patterns:

### Test Method Names
```csharp
[MethodUnderTest]_[Scenario]_[ExpectedBehavior]

Examples:
- Subscribe_ValidHandler_ShouldRegisterSuccessfully()
- ProcessPlayerWarp_OnWarpTile_ShouldCreatePendingWarp()
- ExecuteWarp_MapLoadFails_ShouldResetPlayerState()
```

### Test Class Organization
```csharp
public class EventBusTests
{
    #region Subscription Management Tests
    // All subscription tests here
    #endregion

    #region Event Publishing Tests
    // All publishing tests here
    #endregion

    #region Helper Methods
    // Shared test utilities
    #endregion
}
```

---

## 9. REQUIRED MOCKING PATTERNS

### EventBus Tests
```csharp
// Mock event handler
var handlerCalled = false;
var subscription = eventBus.Subscribe<TestEvent>(e => handlerCalled = true);

// Assert handler was called
eventBus.Publish(new TestEvent());
handlerCalled.Should().BeTrue();
```

### Warp Tests
```csharp
// Mock IMapInitializer
var mockMapInitializer = new Mock<IMapInitializer>();
mockMapInitializer
    .Setup(m => m.LoadMap(It.IsAny<MapIdentifier>()))
    .ReturnsAsync(mapEntity);

// Mock MapLifecycleManager
var mockLifecycle = new Mock<MapLifecycleManager>();
mockLifecycle.Setup(m => m.UnloadAllMaps()).Verifiable();

// Set services
warpExecutionSystem.SetServices(mockMapInitializer.Object, mockLifecycle.Object);
```

---

## 10. SUCCESS CRITERIA

### Test Coverage Goals
- **EventBus**: ≥90% code coverage
- **WarpSystem**: ≥85% code coverage
- **WarpExecutionSystem**: ≥80% code coverage (async code is harder)
- **Integration**: All critical paths covered

### Quality Metrics
- All tests pass on CI/CD
- No flaky tests (≥99.5% reliability)
- Test execution time <30 seconds total
- Zero memory leaks detected

### Documentation
- All test classes have XML doc comments
- README files updated
- Test patterns documented
- Examples provided for common scenarios

---

## 11. MAINTENANCE PLAN

### Ongoing
- Run tests on every PR
- Update tests when features change
- Add regression tests for bugs
- Monitor test execution time

### Quarterly
- Review test coverage reports
- Identify gaps in coverage
- Refactor slow tests
- Update documentation

---

## APPENDIX A: TEST FILE TEMPLATES

### EventBus Test Template
```csharp
using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using PokeSharp.Engine.Core.Events;
using PokeSharp.Engine.Core.Types.Events;
using Xunit;

namespace PokeSharp.Engine.Debug.Tests.Events;

public class EventBusTests : IDisposable
{
    private readonly EventBus _eventBus;

    public EventBusTests()
    {
        _eventBus = new EventBus(NullLogger<EventBus>.Instance);
    }

    public void Dispose()
    {
        _eventBus.ClearAllSubscriptions();
    }

    #region Subscription Management Tests

    [Fact]
    public void Subscribe_ValidHandler_ShouldRegisterSuccessfully()
    {
        // Arrange
        var handlerCalled = false;

        // Act
        var subscription = _eventBus.Subscribe<TestEvent>(e => handlerCalled = true);
        _eventBus.Publish(new TestEvent());

        // Assert
        handlerCalled.Should().BeTrue();
        _eventBus.GetSubscriberCount<TestEvent>().Should().Be(1);
    }

    #endregion

    #region Helper Classes

    private class TestEvent : TypeEventBase { }

    #endregion
}
```

### Warp Test Template
```csharp
using Arch.Core;
using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using PokeSharp.Engine.Core.Types;
using PokeSharp.Game.Components.Warps;
using PokeSharp.Game.Systems.Warps;
using Xunit;

namespace PokeSharp.Game.Tests.Systems.Warps;

public class WarpSystemTests : IDisposable
{
    private readonly World _world;
    private readonly WarpSystem _warpSystem;

    public WarpSystemTests()
    {
        _world = World.Create();
        _warpSystem = new WarpSystem(NullLogger<WarpSystem>.Instance);
        _warpSystem.Initialize(_world);
    }

    public void Dispose()
    {
        _world?.Dispose();
    }

    [Fact]
    public void ProcessPlayerWarp_OnWarpTile_ShouldCreatePendingWarp()
    {
        // Arrange
        var player = CreatePlayerOnWarpTile();

        // Act
        _warpSystem.Update(_world, 0.016f);

        // Assert
        var warpState = player.Get<WarpState>();
        warpState.PendingWarp.Should().NotBeNull();
        warpState.IsWarping.Should().BeTrue();
    }

    private Entity CreatePlayerOnWarpTile()
    {
        // Test helper implementation
        throw new NotImplementedException();
    }
}
```

---

## APPENDIX B: QUICK REFERENCE

### Run All Tests
```bash
# From project root
dotnet test

# Specific test project
dotnet test tests/PokeSharp.Game.Tests/

# With coverage
dotnet test --collect:"XPlat Code Coverage"

# Verbose output
dotnet test --verbosity detailed
```

### Test Project Structure
```
tests/
├── PokeSharp.Engine.Debug.Tests/
│   └── Events/
│       └── EventBusTests.cs                    (NEW - 25 tests)
├── PokeSharp.Game.Tests/
│   └── Systems/
│       ├── MapStreamingSystemTests.cs          (EXISTING - 35 tests)
│       └── Warps/                              (NEW)
│           ├── WarpSystemTests.cs              (NEW - 15 tests)
│           └── WarpExecutionSystemTests.cs     (NEW - 20 tests)
└── Integration/
    ├── WarpIntegrationTests.cs                 (NEW - 15 tests)
    └── EventBusWarpIntegrationTests.cs         (NEW - 15 tests)
```

### Total New Tests: ~105 tests

---

**Document Version**: 1.0
**Created**: 2025-11-29
**Author**: Tester Agent (Hive Mind Swarm)
**Status**: Ready for Implementation
