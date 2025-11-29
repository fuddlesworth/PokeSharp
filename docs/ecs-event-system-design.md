# ECS Event System Design for PokeSharp

## Overview

This document presents the design for integrating Arch ECS 2.1.0's EventBus with PokeSharp's existing event infrastructure, providing high-performance struct-based events while maintaining compatibility with the current TypeEventBase system.

## Architecture

### Integration Points

1. **Arch.EventBus**: Source-generated event bus (struct-based, zero-allocation)
2. **Existing EventBus**: TypeEventBase record system (reference-based, established)
3. **ScriptContext**: Provides access to World, Entity, APIs
4. **ModLoader**: Handles mod discovery and loading

### Event Flow

```
ECS Events (struct) → IEcsEventBus → Arch.EventBus
                    ↓
                TypeEventBase Adapter → IEventBus → Mod/Script Handlers
```

## Design Components

### 1. Core Interfaces

```csharp
// Marker interface for ECS events
public interface IEcsEvent
{
    /// <summary>
    /// Timestamp when the event was created (game time in seconds)
    /// </summary>
    float Timestamp { get; }

    /// <summary>
    /// Event priority for ordering (higher = processed first)
    /// </summary>
    EventPriority Priority { get; }
}

// Event priority levels
public enum EventPriority
{
    Lowest = -100,
    Low = -50,
    Normal = 0,
    High = 50,
    Highest = 100,
    Critical = 1000
}

// Cancellable event pattern
public interface ICancellableEvent : IEcsEvent
{
    /// <summary>
    /// Whether this event has been cancelled
    /// </summary>
    bool IsCancelled { get; set; }
}
```

### 2. Event Lifecycle Pattern

```csharp
// Pre-event (cancellable) and Post-event (notification) pattern
public readonly struct EntityCreatingEvent : IEcsEvent, ICancellableEvent
{
    public required Entity Entity { get; init; }
    public required float Timestamp { get; init; }
    public required EventPriority Priority { get; init; }
    public bool IsCancelled { get; set; }
}

public readonly struct EntityCreatedEvent : IEcsEvent
{
    public required Entity Entity { get; init; }
    public required float Timestamp { get; init; }
    public required EventPriority Priority { get; init; }
}
```

### 3. Core ECS Events

See implementation files for complete event definitions.

### 4. IEcsEventBus Bridge

```csharp
public interface IEcsEventBus
{
    /// <summary>
    /// Subscribe to an ECS event with priority ordering
    /// </summary>
    IDisposable Subscribe<TEvent>(IEventHandler<TEvent> handler)
        where TEvent : struct, IEcsEvent;

    /// <summary>
    /// Subscribe with a delegate
    /// </summary>
    IDisposable Subscribe<TEvent>(Action<TEvent> handler, EventPriority priority = EventPriority.Normal)
        where TEvent : struct, IEcsEvent;

    /// <summary>
    /// Publish an event to all subscribers
    /// </summary>
    void Publish<TEvent>(TEvent evt)
        where TEvent : struct, IEcsEvent;

    /// <summary>
    /// Publish a cancellable event (stops if cancelled)
    /// </summary>
    bool PublishCancellable<TEvent>(ref TEvent evt)
        where TEvent : struct, ICancellableEvent;
}

public interface IEventHandler<in TEvent> where TEvent : struct, IEcsEvent
{
    EventPriority Priority { get; }
    void Handle(TEvent evt);
}
```

### 5. Arch.EventBus Integration

```csharp
// Uses Arch's source generation
[Event]
public partial class ArchEcsEventBus : IEcsEventBus
{
    private readonly Dictionary<Type, List<IEventHandlerWrapper>> _handlers = new();

    // Arch.EventBus will source-generate these methods
    // We provide wrappers for priority ordering

    public IDisposable Subscribe<TEvent>(IEventHandler<TEvent> handler)
        where TEvent : struct, IEcsEvent
    {
        // Implementation uses Arch's generated Subscribe
        // Wraps handler with priority sorting
    }

    public void Publish<TEvent>(TEvent evt)
        where TEvent : struct, IEcsEvent
    {
        // Sort handlers by priority, then invoke
        // Uses Arch's generated Publish
    }
}
```

### 6. TypeEventBase Adapter

```csharp
// Adapter to bridge struct events to existing record-based system
public class EcsEventAdapter
{
    private readonly IEcsEventBus _ecsEventBus;
    private readonly IEventBus _legacyEventBus;

    public void PublishToLegacy<TEvent>(TEvent ecsEvent)
        where TEvent : struct, IEcsEvent
    {
        // Convert struct to TypeEventBase record
        // Publish to existing EventBus for mod compatibility
    }

    public void SubscribeFromLegacy<TEvent>(Action<TEvent> legacyHandler)
        where TEvent : TypeEventBase
    {
        // Convert legacy handler to ECS handler
        // Bridge subscription
    }
}
```

## Event Categories

### Entity Lifecycle
- EntityCreating/EntityCreated
- EntityDestroying/EntityDestroyed

### Component Changes
- ComponentAdding/ComponentAdded
- ComponentRemoving/ComponentRemoved
- ComponentChanging/ComponentChanged

### Map Events
- MapLoading/MapLoaded
- MapUnloading/MapUnloaded
- TileEntering/TileEntered
- TileExiting/TileExited

### Movement Events
- PlayerMoving/PlayerMoved
- NpcMoving/NpcMoved

### Game Events
- InteractionTriggering/InteractionTriggered
- EncounterTriggering/EncounterTriggered
- BattleStarting/BattleStarted
- BattleEnding/BattleEnded

### Script/Mod Events
- ScriptLoading/ScriptLoaded
- ScriptUnloading/ScriptUnloaded
- ModLoading/ModLoaded
- ModUnloading/ModUnloaded
- HotReloadTriggering/HotReloadTriggered

## Usage Examples

### From Game Systems

```csharp
// Movement system
public class MovementSystem : ISystem
{
    private IEcsEventBus _eventBus;

    public void Update(World world)
    {
        var query = world.Query<Position, Velocity>();
        foreach (var entity in query)
        {
            ref var pos = ref world.Get<Position>(entity);
            var vel = world.Get<Velocity>(entity);

            var evt = new TileEnteringEvent
            {
                Entity = entity,
                FromPosition = pos,
                ToPosition = new Position { X = pos.X + vel.X, Y = pos.Y + vel.Y },
                Timestamp = gameTime,
                Priority = EventPriority.Normal
            };

            // Cancellable - scripts can prevent movement
            if (_eventBus.PublishCancellable(ref evt))
            {
                pos = evt.ToPosition;
                _eventBus.Publish(new TileEnteredEvent { /* ... */ });
            }
        }
    }
}
```

### From Scripts/Mods

```csharp
// Mod script handler
public class CustomMovementMod : IEventHandler<TileEnteringEvent>
{
    public EventPriority Priority => EventPriority.High;

    public void Handle(TileEnteringEvent evt)
    {
        // Check if moving into water without Surf
        if (IsWaterTile(evt.ToPosition) && !HasSurf(evt.Entity))
        {
            evt.IsCancelled = true;
            ShowMessage("You need Surf to cross water!");
        }
    }
}
```

## Performance Characteristics

- **Struct-based events**: Zero-allocation event publishing
- **Priority ordering**: O(n log n) handler sorting (cached)
- **Cancellation**: Early termination on cancel
- **Source generation**: No reflection overhead
- **Memory layout**: Cache-friendly struct layout

## Migration Strategy

1. **Phase 1**: Implement IEcsEvent infrastructure
2. **Phase 2**: Add struct event definitions
3. **Phase 3**: Integrate Arch.EventBus with source generation
4. **Phase 4**: Create adapter for TypeEventBase compatibility
5. **Phase 5**: Migrate high-frequency events to struct-based
6. **Phase 6**: Optional - deprecate TypeEventBase for new events

## References

- Arch ECS: https://github.com/genaray/Arch
- Arch.EventBus: https://github.com/genaray/Arch.EventBus
- Existing EventBus: /PokeSharp.Engine.Core/Events/
