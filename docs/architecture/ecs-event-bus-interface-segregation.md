# ECS Event Bus Interface Segregation

## Overview

The `IEcsEventBus` interface has been refactored following the Interface Segregation Principle (ISP), providing focused interfaces for different use cases while maintaining full backward compatibility.

## Problem Statement

The original `IEcsEventBus` interface was too broad:
- Clients that only needed to publish events had to depend on subscription and admin methods
- Clients that only needed to subscribe had access to unnecessary publishing and admin methods
- Testing required mocking all methods, even unused ones
- The API surface was unclear about component intentions

## Solution: Segregated Interfaces

The interface has been split into four focused interfaces:

### 1. `IEventPublisher`
**Purpose**: Publishing events to subscribers

**Methods**:
- `void Publish<TEvent>(TEvent evt)`
- `bool PublishCancellable<TEvent>(ref TEvent evt)`

**Use Cases**:
- Systems that generate events (movement, combat, inventory)
- Services that broadcast state changes
- Game logic that triggers actions

**Example**:
```csharp
public class MovementSystem
{
    private readonly IEventPublisher _eventPublisher;

    public MovementSystem(IEventPublisher eventPublisher)
    {
        _eventPublisher = eventPublisher;
    }

    public void MovePlayer(Position to)
    {
        _eventPublisher.Publish(new PlayerMovedEvent { Position = to });
    }
}
```

### 2. `IEventSubscriber`
**Purpose**: Subscribing to events

**Methods**:
- `IDisposable Subscribe<TEvent>(IEventHandler<TEvent> handler)`
- `IDisposable Subscribe<TEvent>(Action<TEvent> handler, EventPriority priority)`

**Use Cases**:
- Systems that react to events (animation, audio, UI)
- Components that listen for state changes
- Mods that intercept game events

**Example**:
```csharp
public class AnimationSystem : IDisposable
{
    private readonly IEventSubscriber _eventSubscriber;
    private IDisposable? _subscription;

    public AnimationSystem(IEventSubscriber eventSubscriber)
    {
        _eventSubscriber = eventSubscriber;
        _subscription = _eventSubscriber.Subscribe<PlayerMovedEvent>(OnPlayerMoved);
    }

    private void OnPlayerMoved(PlayerMovedEvent evt)
    {
        PlayWalkAnimation(evt.Position);
    }

    public void Dispose() => _subscription?.Dispose();
}
```

### 3. `IEventBusDiagnostics`
**Purpose**: Inspecting event bus state

**Methods**:
- `int GetSubscriberCount<TEvent>()`

**Use Cases**:
- Debug tools and UI
- Performance monitoring
- Development utilities
- Testing assertions

**Example**:
```csharp
public class EventDebugger
{
    private readonly IEventBusDiagnostics _diagnostics;

    public EventDebugger(IEventBusDiagnostics diagnostics)
    {
        _diagnostics = diagnostics;
    }

    public void PrintStats()
    {
        Console.WriteLine($"PlayerMovedEvent: {_diagnostics.GetSubscriberCount<PlayerMovedEvent>()} subscribers");
    }
}
```

### 4. `IEventBusAdmin`
**Purpose**: Managing event subscriptions

**Methods**:
- `void ClearSubscriptions<TEvent>()`
- `void ClearAllSubscriptions()`

**Use Cases**:
- Test harnesses (cleanup between tests)
- Hot-reload scenarios
- Shutdown/cleanup logic
- Administrative tools

**Example**:
```csharp
public class EventTestHarness
{
    private readonly IEventBusAdmin _admin;

    public EventTestHarness(IEventBusAdmin admin)
    {
        _admin = admin;
    }

    public void CleanupBeforeTest()
    {
        _admin.ClearAllSubscriptions();
    }
}
```

### 5. `IEcsEventBus` (Composite Interface)
**Purpose**: Full event bus capabilities

**Inherits**: `IEventPublisher`, `IEventSubscriber`, `IEventBusDiagnostics`, `IEventBusAdmin`, `IDisposable`

**Use Cases**:
- Legacy code (backward compatibility)
- Components needing multiple capabilities
- Central event bus services

**Example**:
```csharp
// Backward compatible - existing code continues to work
public class LegacySystem
{
    private readonly IEcsEventBus _eventBus;

    public LegacySystem(IEcsEventBus eventBus)
    {
        _eventBus = eventBus;
        _eventBus.Subscribe<SomeEvent>(OnEvent);
    }
}
```

## Implementation Details

### Interface Hierarchy
```
IEventPublisher
    ├─ Publish<TEvent>()
    └─ PublishCancellable<TEvent>()

IEventSubscriber
    ├─ Subscribe<TEvent>(IEventHandler)
    └─ Subscribe<TEvent>(Action, priority)

IEventBusDiagnostics
    └─ GetSubscriberCount<TEvent>()

IEventBusAdmin
    ├─ ClearSubscriptions<TEvent>()
    └─ ClearAllSubscriptions()

IDisposable
    └─ Dispose()

IEcsEventBus : IEventPublisher, IEventSubscriber, IEventBusDiagnostics, IEventBusAdmin, IDisposable
    └─ (No additional members - all inherited)
```

### Concrete Implementation
`ArchEcsEventBus` implements all interfaces:

```csharp
public sealed class ArchEcsEventBus :
    IEcsEventBus,
    IEventPublisher,
    IEventSubscriber,
    IEventBusDiagnostics,
    IEventBusAdmin
{
    // Implementation unchanged - all methods already present
}
```

### Dependency Injection Setup
```csharp
// Register once, use many ways
services.AddSingleton<ArchEcsEventBus>();

// Register all interface types (same instance)
services.AddSingleton<IEcsEventBus>(sp => sp.GetRequiredService<ArchEcsEventBus>());
services.AddSingleton<IEventPublisher>(sp => sp.GetRequiredService<ArchEcsEventBus>());
services.AddSingleton<IEventSubscriber>(sp => sp.GetRequiredService<ArchEcsEventBus>());
services.AddSingleton<IEventBusDiagnostics>(sp => sp.GetRequiredService<ArchEcsEventBus>());
services.AddSingleton<IEventBusAdmin>(sp => sp.GetRequiredService<ArchEcsEventBus>());
```

## Benefits for Modders

### 1. Clearer Intentions
Dependencies immediately document what a component does:
```csharp
// Clearly a publisher
public MovementSystem(IEventPublisher publisher) { }

// Clearly a subscriber
public AnimationSystem(IEventSubscriber subscriber) { }

// Clearly read-only diagnostics
public EventDebugger(IEventBusDiagnostics diagnostics) { }
```

### 2. Enhanced Safety
Components can only access what they need:
- Publishers can't accidentally clear subscriptions
- Subscribers can't accidentally publish events
- Diagnostic tools can't modify state
- Admin operations are isolated to authorized components

### 3. Improved Testability
Mock only what you use:
```csharp
// Test MovementSystem - only need to mock IEventPublisher
var mockPublisher = new Mock<IEventPublisher>();
var system = new MovementSystem(mockPublisher.Object);

// Test AnimationSystem - only need to mock IEventSubscriber
var mockSubscriber = new Mock<IEventSubscriber>();
var system = new AnimationSystem(mockSubscriber.Object);
```

### 4. Better Composition
Mix and match interfaces as needed:
```csharp
// Mod that validates AND notifies
public class ValidationMod
{
    public ValidationMod(IEventSubscriber subscriber, IEventPublisher publisher)
    {
        subscriber.Subscribe<PlayerMovingEvent>(evt =>
        {
            if (!IsValid(evt))
            {
                evt.Cancel();
                publisher.Publish(new ValidationFailedEvent());
            }
        });
    }
}
```

### 5. Gradual Migration
Existing code continues to work:
```csharp
// Old code - still works perfectly
public OldSystem(IEcsEventBus eventBus) { }

// New code - use focused interfaces
public NewSystem(IEventPublisher publisher) { }
```

## Migration Guide

### For Existing Code
No changes required! Existing code using `IEcsEventBus` continues to work:
```csharp
// This still works exactly as before
private readonly IEcsEventBus _eventBus;

public MySystem(IEcsEventBus eventBus)
{
    _eventBus = eventBus;
}
```

### For New Code
Use the most specific interface that matches your needs:

**If you only publish events**:
```csharp
private readonly IEventPublisher _publisher;

public MySystem(IEventPublisher publisher)
{
    _publisher = publisher;
}
```

**If you only subscribe to events**:
```csharp
private readonly IEventSubscriber _subscriber;

public MySystem(IEventSubscriber subscriber)
{
    _subscriber = subscriber;
}
```

**If you need both publishing and subscribing**:
```csharp
// Option 1: Inject both (explicit)
public MySystem(IEventPublisher publisher, IEventSubscriber subscriber)
{
    // ...
}

// Option 2: Use composite interface (simpler)
public MySystem(IEcsEventBus eventBus)
{
    // ...
}
```

### Refactoring Existing Code
To take advantage of segregated interfaces:

1. Identify what the component actually uses
2. Replace `IEcsEventBus` with the appropriate interface(s)
3. Update constructor and field types
4. Verify tests still pass

**Before**:
```csharp
public class AudioSystem
{
    private readonly IEcsEventBus _eventBus;

    public AudioSystem(IEcsEventBus eventBus)
    {
        _eventBus = eventBus;
        _eventBus.Subscribe<SoundEvent>(PlaySound);
    }
}
```

**After**:
```csharp
public class AudioSystem
{
    private readonly IEventSubscriber _subscriber;

    public AudioSystem(IEventSubscriber subscriber)
    {
        _subscriber = subscriber;
        _subscriber.Subscribe<SoundEvent>(PlaySound);
    }
}
```

## Design Rationale

### Why Interface Segregation?
Following the SOLID principles, specifically the Interface Segregation Principle:
> "No client should be forced to depend on methods it does not use"

This principle leads to:
- **Smaller, focused interfaces** - easier to understand and implement
- **Reduced coupling** - clients depend only on what they need
- **Better testability** - smaller mock surfaces
- **Clearer contracts** - interface name documents intent
- **Easier maintenance** - changes to one interface don't affect unrelated clients

### Why Keep `IEcsEventBus`?
Backward compatibility and convenience:
- Existing code continues to work without changes
- Simpler DI when you need multiple capabilities
- Familiar interface for developers
- Gradual migration path

### Performance Impact
**None**. All interfaces point to the same `ArchEcsEventBus` instance:
- Same method implementations
- Same memory layout
- Same performance characteristics
- Zero runtime overhead

## Example: Complete Mod System

```csharp
// 1. Movement validation (Subscriber + Publisher)
public class MovementValidator : IDisposable
{
    private readonly IEventSubscriber _subscriber;
    private readonly IEventPublisher _publisher;
    private IDisposable? _subscription;

    public MovementValidator(IEventSubscriber subscriber, IEventPublisher publisher)
    {
        _subscriber = subscriber;
        _publisher = publisher;

        _subscription = _subscriber.Subscribe<PlayerMovingEvent>(ValidateMovement, EventPriority.High);
    }

    private void ValidateMovement(PlayerMovingEvent evt)
    {
        if (!IsValidTile(evt.ToPosition))
        {
            evt.Cancel();
            _publisher.Publish(new MovementDeniedEvent { Reason = "Invalid tile" });
        }
    }

    public void Dispose() => _subscription?.Dispose();
}

// 2. Audio system (Subscriber only)
public class AudioSystem : IDisposable
{
    private readonly IEventSubscriber _subscriber;
    private readonly List<IDisposable> _subscriptions = new();

    public AudioSystem(IEventSubscriber subscriber)
    {
        _subscriber = subscriber;

        _subscriptions.Add(_subscriber.Subscribe<PlayerMovedEvent>(evt => PlayFootstep()));
        _subscriptions.Add(_subscriber.Subscribe<MovementDeniedEvent>(evt => PlayBonkSound()));
    }

    public void Dispose()
    {
        foreach (var sub in _subscriptions)
            sub.Dispose();
    }
}

// 3. Debug overlay (Diagnostics only)
public class EventDebugOverlay
{
    private readonly IEventBusDiagnostics _diagnostics;

    public EventDebugOverlay(IEventBusDiagnostics diagnostics)
    {
        _diagnostics = diagnostics;
    }

    public void Render()
    {
        Console.WriteLine($"Active Events:");
        Console.WriteLine($"  PlayerMovedEvent: {_diagnostics.GetSubscriberCount<PlayerMovedEvent>()} handlers");
        Console.WriteLine($"  PlayerMovingEvent: {_diagnostics.GetSubscriberCount<PlayerMovingEvent>()} handlers");
    }
}
```

## Files Changed

### Modified Files
1. `/PokeSharp.Engine.Core/Events/ECS/IEcsEventBus.cs`
   - Added `IEventPublisher` interface
   - Added `IEventSubscriber` interface
   - Added `IEventBusDiagnostics` interface
   - Added `IEventBusAdmin` interface
   - Modified `IEcsEventBus` to inherit from all interfaces
   - Removed duplicate method declarations from composite interface

2. `/PokeSharp.Engine.Core/Events/ECS/ArchEcsEventBus.cs`
   - Updated class declaration to explicitly implement all interfaces
   - Added documentation about interface segregation
   - Added dependency injection example

### New Files
1. `/docs/examples/InterfaceSegregationExample.cs`
   - Comprehensive examples of all interface usages
   - Demonstrates best practices for different scenarios
   - Shows DI setup and testing approaches

2. `/docs/architecture/ecs-event-bus-interface-segregation.md` (this file)
   - Complete documentation of the segregation
   - Migration guide
   - Design rationale

## Backward Compatibility

✅ **100% Backward Compatible**

All existing code using `IEcsEventBus` continues to work without any changes:
- Same method signatures
- Same behavior
- Same performance
- Same DI registration patterns

The segregated interfaces are additive - they provide new ways to use the event bus without breaking existing usage.

## Testing

All existing tests pass without modification. The segregated interfaces can be tested independently:

```csharp
// Test with focused interfaces
[Fact]
public void MovementSystem_Should_PublishEvent()
{
    var mockPublisher = new Mock<IEventPublisher>();
    var system = new MovementSystem(mockPublisher.Object);

    system.MovePlayer(new Position(0, 0));

    mockPublisher.Verify(p => p.Publish(It.IsAny<PlayerMovedEvent>()), Times.Once);
}
```

## Conclusion

The interface segregation provides:
- ✅ Clearer, more focused APIs for modders
- ✅ Better testability with smaller mock surfaces
- ✅ Enhanced safety by limiting access to needed operations
- ✅ Self-documenting code through explicit dependencies
- ✅ 100% backward compatibility with existing code
- ✅ Zero performance overhead

This refactoring follows SOLID principles while maintaining the high-performance characteristics of the ECS event system, making it both more powerful and easier to use for mod developers.
