# ECS Event Integration Analysis for PokeSharp

**Analyst**: Hive Mind ANALYST Agent
**Date**: 2025-11-28
**Objective**: Identify integration points for Arch.EventBus within existing PokeSharp architecture

---

## Executive Summary

This analysis examines the PokeSharp codebase to identify optimal integration points for migrating from the current EventBus to Arch.EventBus, while maintaining compatibility with the scripting system, modding framework, and existing game systems.

### Key Findings

1. **Current EventBus** is a simple ConcurrentDictionary-based pub/sub system with thread-safe handler management
2. **ScriptContext** provides a clean API surface but lacks event subscription capabilities
3. **ModManifest** has no event handler registration schema
4. **TileBehaviorSystem** and **MovementSystem** already use interface-based callbacks, suitable for event migration
5. **Existing events** (DialogueRequestedEvent, EffectRequestedEvent) use record types compatible with Arch.EventBus

---

## 1. Integration Points Analysis

### 1.1 Core Systems Integration

#### **TileBehaviorSystem** (HIGH PRIORITY)
**File**: `PokeSharp.Game.Scripting/Systems/TileBehaviorSystem.cs`

**Current Architecture**:
- Uses `ITileBehaviorSystem` interface with direct method calls
- Methods: `IsMovementBlocked()`, `GetForcedMovement()`, `GetJumpDirection()`, etc.
- Invoked synchronously by MovementSystem during movement validation

**Recommended ECS Event Integration**:

```csharp
// NEW: Event-based tile behavior queries
public record TileMovementQueryEvent
{
    public required Entity TileEntity { get; init; }
    public required Direction FromDirection { get; init; }
    public required Direction ToDirection { get; init; }
    public bool IsBlocked { get; set; } // Response field
}

public record TileForcedMovementEvent
{
    public required Entity TileEntity { get; init; }
    public required Direction CurrentDirection { get; init; }
    public Direction ForcedDirection { get; set; } // Response field
}
```

**Integration Strategy**:
1. Add `EventBus eventBus` parameter to TileBehaviorSystem constructor
2. Replace direct script invocation with event publishing
3. Scripts subscribe to events during initialization via ScriptContext.Events
4. Maintain backward compatibility by keeping ITileBehaviorSystem interface

**Benefits**:
- Scripts can react to movement queries without coupling to MovementSystem
- Multiple mods can participate in movement logic (first-to-respond or voting)
- Events can be recorded for debugging/replay

---

#### **MovementSystem** (HIGH PRIORITY)
**File**: `PokeSharp.Game.Systems/Movement/MovementSystem.cs`

**Current Architecture**:
- Depends on `ITileBehaviorSystem` for tile behavior queries
- Depends on `ICollisionService` for collision validation
- Processes `MovementRequest` components synchronously

**Recommended ECS Event Integration**:

```csharp
// NEW: Movement lifecycle events
public record MovementStartedEvent
{
    public required Entity Entity { get; init; }
    public required Direction Direction { get; init; }
    public required Position FromPosition { get; init; }
    public required Position ToPosition { get; init; }
}

public record MovementCompletedEvent
{
    public required Entity Entity { get; init; }
    public required Position Position { get; init; }
    public required Direction Direction { get; init; }
}

public record CollisionDetectedEvent
{
    public required Entity MovingEntity { get; init; }
    public required Entity BlockingEntity { get; init; }
    public required Position AttemptedPosition { get; init; }
}
```

**Integration Strategy**:
1. Publish `MovementStartedEvent` when movement begins
2. Publish `MovementCompletedEvent` when entity reaches grid destination
3. Publish `CollisionDetectedEvent` when collision is detected
4. Allow scripts to subscribe and react (e.g., NPCs react to player movement)

**Benefits**:
- Scripts can trigger on movement without polling
- Multiple systems can react to same movement (audio, particles, camera)
- Event log provides debugging trail for movement issues

---

### 1.2 Scripting System Integration

#### **ScriptContext API** (CRITICAL)
**File**: `PokeSharp.Game.Scripting/Runtime/ScriptContext.cs`

**Current Architecture**:
- Exposes World, Entity, Logger, and domain APIs (Player, NPC, Map, etc.)
- No event subscription capabilities
- Scripts use imperative API calls (ctx.Dialogue.ShowMessage())

**Recommended ScriptContext.Events API**:

```csharp
public sealed class ScriptContext
{
    // ... existing properties ...

    /// <summary>
    /// Gets the event subscription API for this script context.
    /// </summary>
    public ScriptEventApi Events { get; }
}

/// <summary>
/// Provides event subscription API for scripts.
/// </summary>
public sealed class ScriptEventApi
{
    private readonly EventBus _eventBus;
    private readonly List<IDisposable> _subscriptions;

    public ScriptEventApi(EventBus eventBus)
    {
        _eventBus = eventBus;
        _subscriptions = new();
    }

    /// <summary>
    /// Subscribe to an event type with a handler.
    /// Subscription is automatically cleaned up when script is disposed.
    /// </summary>
    public void Subscribe<TEvent>(Action<TEvent> handler)
        where TEvent : struct
    {
        var subscription = _eventBus.Subscribe(handler);
        _subscriptions.Add(subscription);
    }

    /// <summary>
    /// Publish an event to all subscribers.
    /// </summary>
    public void Publish<TEvent>(in TEvent eventData)
        where TEvent : struct
    {
        _eventBus.Send(eventData);
    }

    /// <summary>
    /// Clean up all event subscriptions for this script.
    /// Called when script is reloaded or entity is destroyed.
    /// </summary>
    internal void Dispose()
    {
        foreach (var subscription in _subscriptions)
        {
            subscription.Dispose();
        }
        _subscriptions.Clear();
    }
}
```

**Example Script Usage**:

```csharp
// In TileBehaviorScriptBase
public override void OnActivated(ScriptContext ctx)
{
    // Subscribe to movement queries for this tile
    ctx.Events.Subscribe<TileMovementQueryEvent>(e =>
    {
        if (e.TileEntity == ctx.Entity)
        {
            // Ice tile blocks movement from south
            if (e.FromDirection == Direction.South)
            {
                e.IsBlocked = true;
            }
        }
    });

    // Subscribe to player stepping on tile
    ctx.Events.Subscribe<MovementCompletedEvent>(e =>
    {
        if (e.Position == ctx.Position)
        {
            // Player stepped on ice - trigger slide effect
            ctx.Events.Publish(new TileForcedMovementEvent
            {
                TileEntity = ctx.Entity.Value,
                CurrentDirection = e.Direction,
                ForcedDirection = e.Direction // Continue sliding
            });
        }
    });
}
```

**Integration Strategy**:
1. Add `EventBus` to `ScriptContext` constructor
2. Create `ScriptEventApi` wrapper for type-safe event access
3. Auto-dispose subscriptions when scripts are reloaded (hot reload support)
4. Provide both Subscribe and Publish APIs for scripts

**Benefits**:
- Scripts become event-driven instead of poll-based
- Hot reload support (subscriptions are cleaned up)
- Type-safe event handling within scripts
- No breaking changes to existing ScriptContext API

---

### 1.3 Modding System Integration

#### **ModManifest Event Handler Schema** (MEDIUM PRIORITY)
**File**: `PokeSharp.Engine.Core/Modding/ModManifest.cs`

**Current Schema**:
- Patches: JSON Patch files for data modification
- ContentFolders: New content (templates, definitions)
- Dependencies: Load order control
- **No event handler registration**

**Recommended Event Handler Schema**:

```json
{
  "ModId": "myusername.eventmod",
  "Name": "Event Handler Mod",
  "Version": "1.0.0",
  "EventHandlers": [
    {
      "EventType": "MovementCompletedEvent",
      "HandlerScript": "Scripts/movement_logger.csx",
      "Priority": 100,
      "Enabled": true
    },
    {
      "EventType": "TileMovementQueryEvent",
      "HandlerScript": "Scripts/custom_ice_behavior.csx",
      "Priority": 50
    }
  ],
  "ContentFolders": {
    "Scripts": "Scripts/"
  }
}
```

**C# Schema Update**:

```csharp
public sealed class ModManifest
{
    // ... existing properties ...

    /// <summary>
    /// Event handlers registered by this mod.
    /// Handlers are loaded and subscribed during mod initialization.
    /// </summary>
    public List<EventHandlerDefinition> EventHandlers { get; init; } = new();
}

public sealed class EventHandlerDefinition
{
    /// <summary>
    /// Event type name (e.g., "MovementCompletedEvent").
    /// Must match an existing event type in the engine.
    /// </summary>
    public required string EventType { get; init; }

    /// <summary>
    /// Relative path to handler script (.csx file).
    /// Script must define a class implementing IEventHandler<TEvent>.
    /// </summary>
    public required string HandlerScript { get; init; }

    /// <summary>
    /// Handler priority (lower = earlier execution).
    /// Default is 100.
    /// </summary>
    public int Priority { get; init; } = 100;

    /// <summary>
    /// Whether this handler is enabled.
    /// Allows mods to ship optional event handlers.
    /// </summary>
    public bool Enabled { get; init; } = true;
}
```

**Integration Strategy**:
1. Extend `ModManifest` with `EventHandlers` list
2. Create `EventHandlerLoader` service to load and compile handler scripts
3. Register handlers during `ModLoader.DiscoverMods()` phase
4. Unregister handlers when mods are disabled/unloaded

**Benefits**:
- Mods can extend event-driven behavior without core code changes
- Declarative event handler registration in mod.json
- Priority system allows conflict resolution
- Enable/disable handlers without recompiling

---

## 2. Migration Path: Current EventBus → Arch.EventBus

### 2.1 Existing Events to Migrate

**Current Events** (`PokeSharp.Engine.Core/Types/Events/`):
1. `TypeEventBase` - Base class for type lifecycle events
2. `DialogueRequestedEvent` - UI dialogue display request
3. `EffectRequestedEvent` - Visual effect spawn request
4. `ClearEffectsRequestedEvent` - Clear all effects request
5. `ClearMessagesRequestedEvent` - Clear all messages request

**Migration Strategy**:

#### **Phase 1: Dual Event System (Compatibility)**

```csharp
// Create adapter that bridges old EventBus to Arch.EventBus
public class EventBusAdapter : IEventBus
{
    private readonly Arch.EventBus _archEventBus;
    private readonly ILogger<EventBusAdapter> _logger;

    public EventBusAdapter(Arch.EventBus archEventBus, ILogger<EventBusAdapter> logger)
    {
        _archEventBus = archEventBus;
        _logger = logger;
    }

    // Implement IEventBus methods by delegating to Arch.EventBus
    public IDisposable Subscribe<TEvent>(Action<TEvent> handler)
        where TEvent : TypeEventBase
    {
        // Convert Action<TEvent> to EventListener
        var listener = new ActionEventListener<TEvent>(handler);
        _archEventBus.Subscribe<TEvent>(listener);
        return new DisposableSubscription(() => _archEventBus.Unsubscribe<TEvent>(listener));
    }

    public void Publish<TEvent>(TEvent eventData)
        where TEvent : TypeEventBase
    {
        _archEventBus.Send(eventData);
    }

    // ... other IEventBus methods ...
}
```

**Benefits**:
- Existing code continues to work
- Gradual migration system-by-system
- Both APIs available during transition

---

#### **Phase 2: Event Type Conversion**

**Problem**: Arch.EventBus requires `struct` events, current events use `record` (class)

**Solution**: Create struct-based equivalents with conversion helpers

```csharp
// NEW: Struct-based event for Arch.EventBus
public struct DialogueRequestedEventV2
{
    public string TypeId { get; init; }
    public float Timestamp { get; init; }
    public string Message { get; init; }
    public string? SpeakerName { get; init; }
    public int Priority { get; init; }
    public Color? Tint { get; init; }

    // Conversion from old event
    public static DialogueRequestedEventV2 FromV1(DialogueRequestedEvent v1)
    {
        return new DialogueRequestedEventV2
        {
            TypeId = v1.TypeId,
            Timestamp = v1.Timestamp,
            Message = v1.Message,
            SpeakerName = v1.SpeakerName,
            Priority = v1.Priority,
            Tint = v1.Tint
        };
    }
}
```

**Migration Timeline**:
1. **Week 1-2**: Implement EventBusAdapter, test dual system
2. **Week 3-4**: Create V2 struct events, add conversion helpers
3. **Week 5-6**: Migrate high-traffic events (movement, collision)
4. **Week 7-8**: Migrate UI events (dialogue, effects)
5. **Week 9**: Remove old EventBus, remove TypeEventBase constraint

---

### 2.2 Breaking Changes Analysis

**BREAKING**: TypeEventBase constraint
- **Current**: All events inherit from `TypeEventBase` (class)
- **Arch.EventBus**: Events must be `struct`
- **Impact**: Cannot directly use existing event types
- **Mitigation**: EventBusAdapter + dual event system during transition

**BREAKING**: Subscription disposal
- **Current**: Returns `IDisposable` with Subscription class
- **Arch.EventBus**: Uses `EventListener` pattern
- **Impact**: Code using `using` or manual disposal needs updating
- **Mitigation**: EventBusAdapter wraps disposal logic

**NON-BREAKING**: Event publishing
- **Current**: `Publish<TEvent>(TEvent eventData)`
- **Arch.EventBus**: `Send<TEvent>(in TEvent eventData)`
- **Impact**: Minimal (same pattern, different method name)
- **Mitigation**: EventBusAdapter aliases methods

---

## 3. Recommended Implementation Order

### Phase 1: Foundation (Week 1-2)
1. ✅ Create `EventBusAdapter` bridging old/new systems
2. ✅ Add Arch.EventBus NuGet package
3. ✅ Inject Arch.EventBus into World creation
4. ✅ Test dual event system with simple event

### Phase 2: ScriptContext Integration (Week 3-4)
1. ✅ Create `ScriptEventApi` wrapper class
2. ✅ Add `Events` property to `ScriptContext`
3. ✅ Implement subscription cleanup on hot reload
4. ✅ Write example tile behavior script using events
5. ✅ Test hot reload with event subscriptions

### Phase 3: Movement Events (Week 5-6)
1. ✅ Define `MovementStartedEvent`, `MovementCompletedEvent` structs
2. ✅ Publish events in `MovementSystem.Update()`
3. ✅ Create example script subscribing to movement events
4. ✅ Migrate `TileBehaviorSystem` to event-based queries
5. ✅ Performance test (ensure <5% overhead)

### Phase 4: Mod Support (Week 7-8)
1. ✅ Extend `ModManifest` with `EventHandlers` schema
2. ✅ Create `EventHandlerLoader` service
3. ✅ Implement handler registration in `ModLoader`
4. ✅ Create example mod with event handlers
5. ✅ Test mod enable/disable with handlers

### Phase 5: Complete Migration (Week 9-10)
1. ✅ Migrate UI events (DialogueRequestedEvent, etc.)
2. ✅ Remove `EventBusAdapter` once all systems migrated
3. ✅ Remove `TypeEventBase` constraint
4. ✅ Update documentation and examples
5. ✅ Performance profiling and optimization

---

## 4. Performance Considerations

### Current EventBus Performance
- **Structure**: ConcurrentDictionary<Type, ConcurrentDictionary<int, Delegate>>
- **Subscribe**: O(1) atomic increment + dictionary insert
- **Publish**: O(n) iteration over handlers with error isolation
- **Memory**: One delegate allocation per subscription

### Arch.EventBus Performance
- **Structure**: ComponentType-based lookup with archetype iteration
- **Send**: O(1) entity iteration for listeners
- **Memory**: Zero allocation for struct events
- **Batch Support**: `BufferedEventBus` for deferred processing

### Migration Performance Impact
**Expected**: <5% overhead from event-driven architecture
**Actual**: Depends on event frequency and handler count

**Optimization Strategies**:
1. Use `BufferedEventBus` for high-frequency events (>1000/frame)
2. Filter events early (e.g., only publish for player, not all NPCs)
3. Batch event processing in systems (collect events, process once)
4. Use `WorldEvent` attribute for global vs. entity-scoped events

---

## 5. Compatibility Matrix

| System | Current Integration | Event Integration | Migration Effort |
|--------|-------------------|------------------|------------------|
| TileBehaviorSystem | ITileBehaviorSystem interface | Event-based queries | Medium (2-3 days) |
| MovementSystem | Direct component access | Lifecycle events | Low (1-2 days) |
| ScriptContext | API facade pattern | ScriptEventApi | Medium (3-4 days) |
| ModLoader | JSON patches only | Event handler schema | High (5-6 days) |
| DialogueSystem | EventBus.Subscribe | Arch.EventBus | Low (1 day) |
| EffectSystem | EventBus.Subscribe | Arch.EventBus | Low (1 day) |

**Total Estimated Effort**: 4-5 weeks (1 developer, includes testing)

---

## 6. Risk Analysis

### High Risk
- **Hot Reload Compatibility**: Event subscriptions must be cleaned up during script reload
  - **Mitigation**: Auto-dispose in ScriptEventApi, add hot reload tests

### Medium Risk
- **Mod Load Order**: Event handler execution order must respect mod dependencies
  - **Mitigation**: Priority system + dependency-aware handler registration

### Low Risk
- **Performance Regression**: Event overhead on movement system
  - **Mitigation**: Performance benchmarks before/after, optimize if >5% overhead

---

## 7. Conclusion

### Summary of Recommendations

1. **ScriptContext.Events API**: Critical for script-driven event handling
   - Add `ScriptEventApi` with Subscribe/Publish methods
   - Auto-cleanup subscriptions on hot reload

2. **Movement Events**: High-value integration point
   - Publish lifecycle events (started, completed, collision)
   - Allow scripts and mods to react to movement

3. **Mod Event Handlers**: Extend modding capabilities
   - Add `EventHandlers` to ModManifest schema
   - Priority-based handler execution

4. **Phased Migration**: Minimize disruption
   - EventBusAdapter for backward compatibility
   - Migrate system-by-system over 4-5 weeks
   - Remove old EventBus only after full migration

### Next Steps

1. **Architect Agent**: Design detailed event schema (structs, fields, relationships)
2. **Coder Agent**: Implement EventBusAdapter and ScriptEventApi
3. **Tester Agent**: Create integration tests for event flow
4. **Reviewer Agent**: Review event API design for consistency with existing patterns

---

**Document Version**: 1.0
**Last Updated**: 2025-11-28
**Status**: ✅ COMPLETE - Ready for Architecture Phase
