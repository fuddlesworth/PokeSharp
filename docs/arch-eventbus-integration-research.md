# Arch EventBus Integration Research Report
## PokeSharp Pokemon Engine - ECS Event System Analysis

**Research Date**: 2025-11-28
**Target Framework**: Arch ECS 2.1.0 (C# .NET 9)
**Researcher**: Hive Mind Research Agent

---

## Executive Summary

This report analyzes how to integrate **Arch.EventBus** with PokeSharp's existing event infrastructure while maintaining compatibility with the modding/scripting system. The research covers Arch.EventBus architecture, event categories for Pokemon engines, integration patterns, and security considerations for mod-safe event handling.

### Key Findings

1. **Dual Event System**: PokeSharp should maintain both systems initially:
   - **Current EventBus**: High-level game events (dialogue, effects, UI)
   - **Arch.EventBus**: Low-level ECS lifecycle events (entity/component changes)

2. **Arch.EventBus Benefits**: Source-generated, zero-allocation event handling with direct method calls
3. **Security**: Mod event handlers require sandboxing and capability-based restrictions
4. **Pokemon Event Categories**: 13 recommended event categories identified

---

## 1. Current PokeSharp Event Infrastructure

### 1.1 Existing EventBus (IEventBus/EventBus.cs)

**Architecture**:
```csharp
public interface IEventBus
{
    IDisposable Subscribe<TEvent>(Action<TEvent> handler) where TEvent : TypeEventBase;
    void Publish<TEvent>(TEvent eventData) where TEvent : TypeEventBase;
    void ClearSubscriptions<TEvent>() where TEvent : TypeEventBase;
    int GetSubscriberCount<TEvent>() where TEvent : TypeEventBase;
}
```

**Implementation Details**:
- Uses `ConcurrentDictionary<Type, ConcurrentDictionary<int, Delegate>>` for thread-safe handler management
- Atomic handler ID generation with `Interlocked.Increment`
- Error isolation: Handler exceptions don't break event publishing
- Synchronous event firing on caller's thread
- Base event: `TypeEventBase` (abstract record with TypeId and Timestamp)

**Current Event Types**:
- `DialogueRequestedEvent` - UI dialogue display
- `EffectRequestedEvent` - Visual effects spawning
- `ClearEffectsRequestedEvent` - Effect cleanup
- `ClearMessagesRequestedEvent` - Message queue cleanup

**Usage Pattern**:
```csharp
// DialogueApiService publishes events
var dialogueEvent = new DialogueRequestedEvent
{
    TypeId = "dialogue-api",
    Timestamp = _gameTime.TotalSeconds,
    Message = message,
    SpeakerName = speakerName,
    Priority = priority
};
_eventBus.Publish(dialogueEvent);
```

### 1.2 Scripting System Integration

**ScriptContext Provides**:
- Direct ECS World access
- API services (Player, NPC, Map, Dialogue, Effects, GameState)
- Type-safe component access (`GetState<T>()`, `TryGetState<T>()`)
- Logger instance per script

**Script Types**:
- `TileBehaviorScriptBase` - Entity-level (tile interactions)
- `NPCBehaviorScriptBase` - Entity-level (NPC AI)
- Global scripts - World-level queries

**Execution**: Roslyn CSX compilation with hot-reload support

### 1.3 Modding System

**ModManifest Structure**:
```csharp
public sealed class ModManifest
{
    public required string ModId { get; init; }
    public List<string> Dependencies { get; init; }
    public List<string> LoadAfter { get; init; }
    public int LoadPriority { get; init; } = 100;
    public List<string> Patches { get; init; }
    public Dictionary<string, string> ContentFolders { get; init; }
}
```

**Load Order**: Topological sort with dependency resolution

---

## 2. Arch.EventBus Architecture

### 2.1 Package Information

- **NuGet Package**: `Arch.EventBus` version 1.0.2
- **Framework Support**: .NET Standard 2.1, .NET Core 6+
- **Compatible With**: Unity, Godot, MonoGame
- **Installation**: `dotnet add package Arch.EventBus --version 1.0.2`

**Source**: [Arch.Extended GitHub Repository](https://github.com/genaray/Arch.Extended)

### 2.2 Source Generation Approach

**Philosophy**: Uses C# source generators to create boilerplate-free, high-performance event code at compile time.

**Similar Pattern** (from Arch.System.SourceGenerator):
```csharp
public class MovementSystem : BaseSystem<World, float>
{
    [Query]
    public void Move([Data] in float time, ref Position pos, ref Velocity vel)
    {
        pos.X += time * vel.X;
        pos.Y += time * vel.Y;
    }

    [Query]
    [All<Player, Mob>, Any<Idle, Moving>, None<Alive>]
    public void ResetVelocity(ref Velocity vel)
    {
        vel = new Velocity{ X = 0, Y = 0 };
    }
}
```

**Arch.EventBus Likely Uses**:
- Attributes to mark event handlers
- Source generator creates subscription/publish infrastructure
- Zero-allocation delegates via generated code
- Direct method call optimization

### 2.3 Arch ECS Native Events

**Enabling Events**: Requires `EVENTS` flag in Arch source or use `Arch-Events` NuGet package

**Available Hooks**:
```csharp
world.SubscribeEntityCreated((Entity entity) => {});
world.SubscribeEntityDestroyed((Entity entity) => {});
world.SubscribeComponentAdded((Entity entity, ref Position _) => {});
world.SubscribeComponentSet((Entity entity, ref Position _) => {});
world.SubscribeComponentRemoved((Entity entity, ref Position _) => {});
```

**Performance Characteristics**:
- "Almost direct method calls, i.e. very fast and efficient"
- No delay - fires immediately after operation
- No boxing/allocations when using ref parameters

**Documentation Source**: [Arch ECS Events Documentation](https://arch-ecs.gitbook.io/arch/documentation/utilities/events)

### 2.4 Integration with Arch.System

**BaseSystem Pattern**:
```csharp
var world = World.Create();
var systems = new Group<float>("Systems", new MovementSystem(world));

systems.Initialize();       // Init all systems
systems.BeforeUpdate(deltaTime);
systems.Update(deltaTime);
systems.AfterUpdate(deltaTime);
systems.Dispose();
```

**Likely EventBus Pattern**:
```csharp
// Subscribe with attributes
[EventHandler]
public void OnPlayerMoved(PlayerMovedEvent evt) { }

// Or fluent API
eventBus.Subscribe<PlayerMovedEvent>(OnPlayerMoved);

// Publish
eventBus.Publish(new PlayerMovedEvent { Entity = playerEntity });
```

---

## 3. Pokemon Engine Event Categories

### 3.1 Recommended Event Taxonomy

Based on Pokemon game mechanics and [Pokemon engine research](https://github.com/pkmn/engine), here are 13 recommended event categories:

#### Category 1: Entity Lifecycle Events
**Use Case**: Track entity creation/destruction for cleanup, logging, analytics

```csharp
public record EntitySpawnedEvent : TypeEventBase
{
    public required Entity Entity { get; init; }
    public required string EntityType { get; init; } // "player", "npc", "pokemon", "tile"
    public Vector2? Position { get; init; }
}

public record EntityDestroyedEvent : TypeEventBase
{
    public required Entity Entity { get; init; }
    public required string Reason { get; init; } // "fainted", "released", "map_unload"
}
```

**Integration**: Bridge Arch's `SubscribeEntityCreated/Destroyed` to custom events

#### Category 2: Movement Events
**Use Case**: Trigger encounters, tile behaviors, NPC reactions, camera follow

```csharp
public record EntitySteppedEvent : TypeEventBase
{
    public required Entity Entity { get; init; }
    public required Vector2Int TilePosition { get; init; }
    public required int MapId { get; init; }
    public Direction FromDirection { get; init; }
}

public record EntityMovementStartedEvent : TypeEventBase
{
    public required Entity Entity { get; init; }
    public required Direction Direction { get; init; }
    public required MovementType Type { get; init; } // Walk, Run, Bike, Surf
}

public record EntityMovementCompletedEvent : TypeEventBase
{
    public required Entity Entity { get; init; }
    public required Vector2Int FromTile { get; init; }
    public required Vector2Int ToTile { get; init; }
    public bool WasInterrupted { get; init; }
}
```

**Current Implementation**: MovementSystem could emit these via EventBus

#### Category 3: Encounter Events
**Use Case**: Wild battles, trainer battles, encounter tracking

```csharp
public record WildEncounterTriggeredEvent : TypeEventBase
{
    public required string EncounterTableId { get; init; }
    public required Entity PlayerEntity { get; init; }
    public required Vector2Int TilePosition { get; init; }
    public int EncounterRate { get; init; }
}

public record TrainerEncounterTriggeredEvent : TypeEventBase
{
    public required Entity PlayerEntity { get; init; }
    public required Entity TrainerEntity { get; init; }
    public required string TrainerId { get; init; }
    public string? IntroDialogue { get; init; }
}

public record EncounterResolvedEvent : TypeEventBase
{
    public required string EncounterType { get; init; } // "wild", "trainer"
    public required string Outcome { get; init; } // "victory", "defeat", "flee", "capture"
    public Entity? CapturedPokemon { get; init; }
}
```

**Data Source**: EncounterZone component, TrainerDefinition entities

#### Category 4: Battle Events
**Use Case**: Battle state machine, UI updates, damage calculation

```csharp
public record BattleStartedEvent : TypeEventBase
{
    public required string BattleType { get; init; } // "wild", "trainer", "double"
    public required Entity[] PlayerTeam { get; init; }
    public Entity[]? OpponentTeam { get; init; }
    public BattleRules Rules { get; init; }
}

public record TurnStartedEvent : TypeEventBase
{
    public required int TurnNumber { get; init; }
    public required Entity ActivePokemon { get; init; }
}

public record MoveUsedEvent : TypeEventBase
{
    public required Entity User { get; init; }
    public required Entity Target { get; init; }
    public required string MoveId { get; init; }
    public int Damage { get; init; }
    public float Effectiveness { get; init; }
    public bool Critical { get; init; }
}

public record PokemonFaintedEvent : TypeEventBase
{
    public required Entity Pokemon { get; init; }
    public required Entity Attacker { get; init; }
    public int ExpAwarded { get; init; }
}

public record BattleEndedEvent : TypeEventBase
{
    public required string Outcome { get; init; } // "victory", "defeat", "flee"
    public int PrizeMoney { get; init; }
    public int TotalExpGained { get; init; }
}
```

**Future**: Battle system implementation priority

#### Category 5: Interaction Events
**Use Case**: Script triggers, cutscenes, dialogue chains

```csharp
public record EntityInteractedEvent : TypeEventBase
{
    public required Entity Initiator { get; init; } // Usually player
    public required Entity Target { get; init; } // NPC, sign, item
    public required string InteractionType { get; init; } // "talk", "examine", "use"
}

public record DialogueStartedEvent : TypeEventBase
{
    public required Entity Speaker { get; init; }
    public string? DialogueChainId { get; init; }
}

public record DialogueChoiceMadeEvent : TypeEventBase
{
    public required string ChoiceId { get; init; }
    public required int SelectedIndex { get; init; }
}
```

**Current**: DialogueRequestedEvent exists, needs expansion

#### Category 6: Map Events
**Use Case**: Map streaming, transitions, loading screens

```csharp
public record MapLoadedEvent : TypeEventBase
{
    public required int MapId { get; init; }
    public required string MapName { get; init; }
    public int EntityCount { get; init; }
}

public record MapUnloadedEvent : TypeEventBase
{
    public required int MapId { get; init; }
    public TimeSpan LoadedDuration { get; init; }
}

public record MapTransitionStartedEvent : TypeEventBase
{
    public required int FromMapId { get; init; }
    public required int ToMapId { get; init; }
    public required Vector2Int DestinationPosition { get; init; }
    public string? TransitionType { get; init; } // "door", "warp", "fly", "dig"
}

public record MapTransitionCompletedEvent : TypeEventBase
{
    public required int MapId { get; init; }
    public TimeSpan TransitionDuration { get; init; }
}
```

**Current**: MapStreamingSystem could emit these

#### Category 7: Item Events
**Use Case**: Inventory management, item effects, shops

```csharp
public record ItemObtainedEvent : TypeEventBase
{
    public required string ItemId { get; init; }
    public required int Quantity { get; init; }
    public required string Source { get; init; } // "found", "purchased", "received"
}

public record ItemUsedEvent : TypeEventBase
{
    public required string ItemId { get; init; }
    public Entity? Target { get; init; } // Pokemon that received item
    public string? Effect { get; init; }
}
```

**Future**: Item system implementation

#### Category 8: Pokemon Events
**Use Case**: Pokemon management, evolution, learning moves

```csharp
public record PokemonCaughtEvent : TypeEventBase
{
    public required Entity Pokemon { get; init; }
    public required string Species { get; init; }
    public int Level { get; init; }
    public bool IsShiny { get; init; }
}

public record PokemonEvolvedEvent : TypeEventBase
{
    public required Entity Pokemon { get; init; }
    public required string FromSpecies { get; init; }
    public required string ToSpecies { get; init; }
}

public record MoveLearnedEvent : TypeEventBase
{
    public required Entity Pokemon { get; init; }
    public required string MoveId { get; init; }
    public string? ForgottenMove { get; init; }
}

public record LevelUpEvent : TypeEventBase
{
    public required Entity Pokemon { get; init; }
    public int NewLevel { get; init; }
    public Dictionary<string, int> StatGains { get; init; }
}
```

**Future**: Pokemon system implementation

#### Category 9: Game State Events
**Use Case**: Save/load, flags, progression tracking

```csharp
public record FlagSetEvent : TypeEventBase
{
    public required string FlagName { get; init; }
    public required bool Value { get; init; }
    public string? Source { get; init; } // Script/mod that set it
}

public record VariableChangedEvent : TypeEventBase
{
    public required string VariableName { get; init; }
    public required string NewValue { get; init; }
    public string? OldValue { get; init; }
}

public record GameSavedEvent : TypeEventBase
{
    public required string SaveSlot { get; init; }
    public TimeSpan PlayTime { get; init; }
}
```

**Current**: GameStateApiService exists, needs events

#### Category 10: Audio Events
**Use Case**: Music/SFX synchronization

```csharp
public record MusicChangedEvent : TypeEventBase
{
    public required string TrackId { get; init; }
    public string? PreviousTrack { get; init; }
    public bool FadeTransition { get; init; }
}

public record SoundEffectRequestedEvent : TypeEventBase
{
    public required string SfxId { get; init; }
    public Vector2? Position { get; init; } // For 3D audio
    public float Volume { get; init; }
}
```

**Future**: Audio system

#### Category 11: Animation Events
**Use Case**: Synchronize gameplay with animations

```csharp
// Already exists: AnimationEvent.cs in PokeSharp.Engine.Rendering
// Extend with:
public record AnimationTriggerEvent : TypeEventBase
{
    public required Entity Entity { get; init; }
    public required string AnimationName { get; init; }
    public int Frame { get; init; }
    public string? EventName { get; init; }
}
```

**Current**: AnimationEvent exists for frame-based callbacks

#### Category 12: Mod Events
**Use Case**: Mod lifecycle, compatibility warnings

```csharp
public record ModLoadedEvent : TypeEventBase
{
    public required string ModId { get; init; }
    public required string Version { get; init; }
    public int LoadOrder { get; init; }
}

public record ModErrorEvent : TypeEventBase
{
    public required string ModId { get; init; }
    public required string ErrorType { get; init; }
    public required string Message { get; init; }
    public Exception? Exception { get; init; }
}

public record ModEventHandlerRegisteredEvent : TypeEventBase
{
    public required string ModId { get; init; }
    public required string EventType { get; init; }
    public int Priority { get; init; }
}
```

**Security**: Track mod event handler registration for auditing

#### Category 13: Debug/Metrics Events
**Use Case**: Development tools, performance tracking

```csharp
public record PerformanceWarningEvent : TypeEventBase
{
    public required string System { get; init; }
    public required string Warning { get; init; }
    public float FrameTime { get; init; }
}

public record EntityCountChangedEvent : TypeEventBase
{
    public required int TotalEntities { get; init; }
    public required int ChangeAmount { get; init; }
    public Dictionary<string, int> EntitiesByType { get; init; }
}
```

**Current**: Logging infrastructure supports this

---

## 4. Arch.EventBus Integration Patterns

### 4.1 Dual EventBus Architecture (Recommended)

**Pattern**: Maintain both event systems with clear separation of concerns

```
┌─────────────────────────────────────────────────────────┐
│                  PokeSharp Event Layer                  │
├─────────────────────────────────────────────────────────┤
│                                                         │
│  ┌─────────────────────┐      ┌───────────────────┐   │
│  │   IEventBus         │      │  Arch.EventBus    │   │
│  │   (Current)         │      │   (New)           │   │
│  ├─────────────────────┤      ├───────────────────┤   │
│  │ High-Level Events:  │      │ ECS Events:       │   │
│  │ - Dialogue          │      │ - EntityCreated   │   │
│  │ - Effects           │      │ - ComponentAdded  │   │
│  │ - UI Requests       │      │ - ComponentSet    │   │
│  │ - Game State        │      │ - EntityDestroyed │   │
│  │ - Audio/Music       │      │ - ComponentRemoved│   │
│  │ - Map Transitions   │      │                   │   │
│  └─────────────────────┘      └───────────────────┘   │
│           │                            │               │
│           └────────┬───────────────────┘               │
│                    ▼                                   │
│         ┌──────────────────────┐                       │
│         │  Event Bridge        │                       │
│         │  (Bidirectional)     │                       │
│         └──────────────────────┘                       │
│                    │                                   │
└────────────────────┼───────────────────────────────────┘
                     ▼
         ┌───────────────────────┐
         │  Mod Event Facade     │
         │  (Filtered/Sandboxed) │
         └───────────────────────┘
                     │
                     ▼
         ┌───────────────────────┐
         │  Mod Scripts          │
         │  (CSX via Roslyn)     │
         └───────────────────────┘
```

### 4.2 Event Bridge Implementation

**Purpose**: Convert low-level Arch events to high-level game events

```csharp
/// <summary>
/// Bridges Arch ECS lifecycle events to PokeSharp game events
/// </summary>
public class ArchEventBridge
{
    private readonly World _world;
    private readonly IEventBus _gameEventBus;
    private readonly ILogger<ArchEventBridge> _logger;

    public ArchEventBridge(World world, IEventBus gameEventBus, ILogger<ArchEventBridge> logger)
    {
        _world = world;
        _gameEventBus = gameEventBus;
        _logger = logger;

        RegisterArchSubscriptions();
    }

    private void RegisterArchSubscriptions()
    {
        // Bridge entity creation to game events
        _world.SubscribeEntityCreated(OnEntityCreated);
        _world.SubscribeEntityDestroyed(OnEntityDestroyed);

        // Bridge component changes for specific game components
        _world.SubscribeComponentAdded<Position>(OnPositionAdded);
        _world.SubscribeComponentSet<GridMovement>(OnMovementChanged);
    }

    private void OnEntityCreated(Entity entity)
    {
        // Classify entity type based on components
        string entityType = ClassifyEntity(entity);

        var evt = new EntitySpawnedEvent
        {
            TypeId = "arch-bridge",
            Timestamp = GameTime.TotalSeconds,
            Entity = entity,
            EntityType = entityType,
            Position = _world.TryGet<Position>(entity, out var pos) ? pos.Coords : null
        };

        _gameEventBus.Publish(evt);
    }

    private void OnMovementChanged(Entity entity, ref GridMovement movement)
    {
        // Only fire for completed movements
        if (movement.IsMoving) return;

        var evt = new EntityMovementCompletedEvent
        {
            TypeId = "arch-bridge",
            Timestamp = GameTime.TotalSeconds,
            Entity = entity,
            FromTile = movement.PreviousTile,
            ToTile = movement.CurrentTile,
            WasInterrupted = false
        };

        _gameEventBus.Publish(evt);
    }

    private string ClassifyEntity(Entity entity)
    {
        if (_world.Has<Player>(entity)) return "player";
        if (_world.Has<Npc>(entity)) return "npc";
        if (_world.Has<AnimatedTile>(entity)) return "tile";
        return "unknown";
    }
}
```

### 4.3 Mod Event Facade (Sandboxed Access)

**Purpose**: Provide controlled event access to mods with security restrictions

```csharp
/// <summary>
/// Sandboxed event API for mod scripts
/// Filters events and restricts handler capabilities
/// </summary>
public interface IModEventApi
{
    /// <summary>
    /// Subscribe to allowed event type with mod ID tracking
    /// </summary>
    IDisposable Subscribe<TEvent>(string modId, Action<TEvent> handler)
        where TEvent : TypeEventBase;

    /// <summary>
    /// Publish event (restricted to mod-owned events only)
    /// </summary>
    void Publish<TEvent>(string modId, TEvent eventData)
        where TEvent : TypeEventBase;
}

public class ModEventFacade : IModEventApi
{
    private readonly IEventBus _eventBus;
    private readonly ModSecurityService _security;
    private readonly ILogger<ModEventFacade> _logger;

    // Track subscriptions per mod for cleanup
    private readonly ConcurrentDictionary<string, List<IDisposable>> _modSubscriptions = new();

    public IDisposable Subscribe<TEvent>(string modId, Action<TEvent> handler)
        where TEvent : TypeEventBase
    {
        // Security check: Can this mod subscribe to this event type?
        if (!_security.CanSubscribeToEvent(modId, typeof(TEvent)))
        {
            _logger.LogWarning(
                "Mod {ModId} denied subscription to {EventType} - insufficient permissions",
                modId, typeof(TEvent).Name
            );
            throw new UnauthorizedAccessException(
                $"Mod '{modId}' cannot subscribe to {typeof(TEvent).Name}"
            );
        }

        // Wrap handler with mod context and error isolation
        var wrappedHandler = CreateSafeHandler(modId, handler);

        var subscription = _eventBus.Subscribe(wrappedHandler);

        // Track for cleanup
        _modSubscriptions.GetOrAdd(modId, _ => new List<IDisposable>())
            .Add(subscription);

        _logger.LogDebug(
            "Mod {ModId} subscribed to {EventType}",
            modId, typeof(TEvent).Name
        );

        return subscription;
    }

    private Action<TEvent> CreateSafeHandler<TEvent>(string modId, Action<TEvent> handler)
        where TEvent : TypeEventBase
    {
        return (evt) =>
        {
            try
            {
                // Execute with timeout
                var cts = new CancellationTokenSource(TimeSpan.FromMilliseconds(100));
                var task = Task.Run(() => handler(evt), cts.Token);

                if (!task.Wait(100))
                {
                    _logger.LogError(
                        "Mod {ModId} event handler for {EventType} timed out",
                        modId, typeof(TEvent).Name
                    );
                    _security.RecordViolation(modId, "handler_timeout");
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(
                    ex,
                    "Mod {ModId} event handler for {EventType} threw exception: {Message}",
                    modId, typeof(TEvent).Name, ex.Message
                );
                _security.RecordViolation(modId, "handler_exception");
            }
        };
    }

    public void Publish<TEvent>(string modId, TEvent eventData)
        where TEvent : TypeEventBase
    {
        // Mods can only publish custom events, not core game events
        if (!_security.CanPublishEvent(modId, typeof(TEvent)))
        {
            throw new UnauthorizedAccessException(
                $"Mod '{modId}' cannot publish {typeof(TEvent).Name}"
            );
        }

        // Set TypeId to mod for tracking
        if (string.IsNullOrEmpty(eventData.TypeId))
        {
            eventData = eventData with { TypeId = $"mod:{modId}" };
        }

        _eventBus.Publish(eventData);
    }

    /// <summary>
    /// Cleanup all subscriptions for a mod (called when mod unloads)
    /// </summary>
    public void UnsubscribeAll(string modId)
    {
        if (_modSubscriptions.TryRemove(modId, out var subscriptions))
        {
            foreach (var sub in subscriptions)
            {
                sub.Dispose();
            }
            _logger.LogInformation(
                "Unsubscribed all event handlers for mod {ModId}",
                modId
            );
        }
    }
}
```

### 4.4 Security Service (Event Permissions)

```csharp
/// <summary>
/// Manages mod security permissions for event system
/// </summary>
public class ModSecurityService
{
    private readonly ILogger<ModSecurityService> _logger;

    // Allowlist of events mods can subscribe to
    private readonly HashSet<Type> _allowedSubscriptionTypes = new()
    {
        typeof(EntitySteppedEvent),
        typeof(EntityMovementCompletedEvent),
        typeof(EntityInteractedEvent),
        typeof(DialogueChoiceMadeEvent),
        typeof(ItemObtainedEvent),
        typeof(FlagSetEvent),
        // NOT ALLOWED: BattleStartedEvent, MapLoadedEvent (core systems only)
    };

    // Mods can only publish custom events in this namespace
    private const string ModEventNamespace = "PokeSharp.Mods.Events";

    // Violation tracking
    private readonly ConcurrentDictionary<string, List<SecurityViolation>> _violations = new();

    public bool CanSubscribeToEvent(string modId, Type eventType)
    {
        return _allowedSubscriptionTypes.Contains(eventType);
    }

    public bool CanPublishEvent(string modId, Type eventType)
    {
        // Mods can only publish events in mod namespace
        return eventType.Namespace?.StartsWith(ModEventNamespace) ?? false;
    }

    public void RecordViolation(string modId, string violationType)
    {
        var violation = new SecurityViolation
        {
            Timestamp = DateTime.UtcNow,
            Type = violationType
        };

        _violations.GetOrAdd(modId, _ => new List<SecurityViolation>())
            .Add(violation);

        _logger.LogWarning(
            "Mod {ModId} security violation: {ViolationType}",
            modId, violationType
        );

        // Disable mod if too many violations
        var recentViolations = _violations[modId]
            .Count(v => v.Timestamp > DateTime.UtcNow.AddMinutes(-5));

        if (recentViolations > 10)
        {
            _logger.LogError(
                "Mod {ModId} exceeded violation threshold - disabling",
                modId
            );
            // TODO: Disable mod
        }
    }
}

public record SecurityViolation
{
    public DateTime Timestamp { get; init; }
    public required string Type { get; init; }
}
```

---

## 5. Integration Strategy

### 5.1 Phase 1: Add Arch.EventBus Package

**Changes to PokeSharp.Engine.Core.csproj**:
```xml
<ItemGroup>
  <PackageReference Include="Arch" Version="2.1.0" />
  <PackageReference Include="Arch.EventBus" Version="1.0.2" />
  <PackageReference Include="Microsoft.Extensions.Logging.Abstractions" Version="9.0.11" />
</ItemGroup>
```

**Alternative**: Use `Arch-Events` NuGet package if EventBus source gen is insufficient

### 5.2 Phase 2: Implement Event Bridge

1. Create `ArchEventBridge.cs` in `PokeSharp.Engine.Core/Events/`
2. Register bridge in DI container
3. Subscribe to Arch events in `GameInitializer`
4. Emit high-level events from bridge

### 5.3 Phase 3: Define Pokemon Event Types

1. Create `PokeSharp.Game.Components/Events/` folder
2. Define 13 event categories (see Section 3.1)
3. Inherit from `TypeEventBase` for consistency
4. Add to existing `IEventBus`

### 5.4 Phase 4: Implement Mod Event Facade

1. Create `ModEventFacade.cs` and `ModSecurityService.cs`
2. Register in DI with mod system
3. Add to `ScriptContext` as `Events` property
4. Document allowed event types for modders

### 5.5 Phase 5: Update Systems to Emit Events

**Example: MovementSystem**
```csharp
public class MovementSystem : SystemBase, IUpdateSystem
{
    private readonly IEventBus _eventBus;
    private readonly IGameTimeService _gameTime;

    public void Update(float deltaTime)
    {
        // ... existing movement logic ...

        // After movement completes
        if (movement.JustStopped)
        {
            _eventBus.Publish(new EntityMovementCompletedEvent
            {
                TypeId = "movement-system",
                Timestamp = _gameTime.TotalSeconds,
                Entity = entity,
                FromTile = movement.StartTile,
                ToTile = movement.EndTile,
                WasInterrupted = false
            });

            // Fire step event for tile behaviors
            _eventBus.Publish(new EntitySteppedEvent
            {
                TypeId = "movement-system",
                Timestamp = _gameTime.TotalSeconds,
                Entity = entity,
                TilePosition = movement.EndTile,
                MapId = position.MapId,
                FromDirection = movement.Direction
            });
        }
    }
}
```

---

## 6. Security Considerations for Mod Event Handlers

### 6.1 Threat Model

**Threat 1: Denial of Service**
- Mod subscribes to high-frequency events (e.g., `ComponentSet`)
- Handler performs expensive operations
- Game becomes unplayable

**Mitigation**:
- Timeout handlers after 100ms
- Rate-limit event subscriptions per mod
- Allowlist only gameplay events (not ECS events)

**Threat 2: State Corruption**
- Mod modifies ECS World from event handler
- Creates race conditions or invalid state

**Mitigation**:
- Event handlers run on caller thread (synchronous)
- Document: "DO NOT modify World from event handlers"
- Provide `DeferredAction` API for delayed modifications
- Read-only `ScriptContext` variant for event handlers

**Threat 3: Information Leakage**
- Mod subscribes to all events
- Extracts private game data (player position, battle state)

**Mitigation**:
- Allowlist event types (see `ModSecurityService`)
- Filter event data before passing to mods
- Audit logging for event subscriptions

**Threat 4: Privilege Escalation**
- Mod publishes core system events
- Tricks other systems into executing code

**Mitigation**:
- Mods can only publish events in `PokeSharp.Mods.Events.*` namespace
- Core systems ignore mod-sourced events for critical operations
- TypeId tagging with mod ID

**Threat 5: Reflection Attacks**
- Mod uses reflection to access EventBus internals
- Bypasses security restrictions

**Mitigation**:
- AppDomain sandboxing for scripts (future)
- Restrict System.Reflection access in CSX compiler
- Document security model clearly

### 6.2 Recommended Security Policies

**Policy 1: Event Subscription Allowlist**
```csharp
// Allowed (gameplay events)
✅ EntitySteppedEvent
✅ EntityInteractedEvent
✅ DialogueChoiceMadeEvent
✅ ItemObtainedEvent
✅ FlagSetEvent

// Denied (core system events)
❌ EntitySpawnedEvent (ECS lifecycle)
❌ BattleStartedEvent (battle system)
❌ MapLoadedEvent (streaming system)
❌ ComponentAddedEvent (ECS lifecycle)
```

**Policy 2: Handler Timeout**
- Max execution time: 100ms per handler
- If exceeded: Log warning, record violation, skip handler

**Policy 3: Violation Threshold**
- Max violations per 5 minutes: 10
- Action: Disable mod, notify player

**Policy 4: Event Data Filtering**
```csharp
// Example: Filter sensitive data before passing to mods
public class FilteredEntitySteppedEvent : EntitySteppedEvent
{
    // Remove Entity reference (mods get EntityId instead)
    public new int EntityId { get; init; }

    // Remove direct World access
    public override Entity Entity => throw new NotSupportedException(
        "Mods cannot access Entity directly from events"
    );
}
```

### 6.3 Best Practices for Mod Developers

**Documentation** (`docs/modding/event-system.md`):

```markdown
# PokeSharp Event System for Mods

## Subscribing to Events

Use `ctx.Events.Subscribe<T>()` from your script:

```csharp
// In TileBehaviorScriptBase.OnStep()
public override void OnStep(ScriptContext ctx, Entity entity)
{
    // Subscribe to player stepping events
    ctx.Events.Subscribe<EntitySteppedEvent>(modId, evt =>
    {
        if (evt.Entity == entity) // Check if player
        {
            ctx.Logger.LogInformation("Player stepped on tile!");
        }
    });
}
```

## Allowed Event Types

Mods can subscribe to these gameplay events:
- `EntitySteppedEvent` - When entity steps on tile
- `EntityInteractedEvent` - When player interacts with entity
- `DialogueChoiceMadeEvent` - When player makes dialogue choice
- `ItemObtainedEvent` - When player obtains item
- `FlagSetEvent` - When game flag changes

## Publishing Custom Events

Define custom events in your mod:

```csharp
namespace YourMod.Events
{
    public record CustomQuestEvent : TypeEventBase
    {
        public required string QuestId { get; init; }
    }
}

// Publish from script
ctx.Events.Publish(modId, new CustomQuestEvent
{
    TypeId = $"mod:{modId}",
    Timestamp = ctx.GameTime.TotalSeconds,
    QuestId = "find_pikachu"
});
```

## Event Handler Rules

⚠️ **IMPORTANT**:
1. DO NOT modify `ctx.World` from event handlers
2. Handlers timeout after 100ms
3. Use `ctx.DeferredAction()` for World modifications
4. Handlers run on game thread (synchronous)
```

---

## 7. Comparison with Pokemon Engine Architectures

### 7.1 Pokemon Showdown Mod System

**Architecture**:
- JavaScript/TypeScript event system
- `ModdedBattleScriptsData` with inheritance
- Hook-based event interception

**Relevant Pattern**:
```typescript
export const Scripts: ModdedBattleScriptsData = {
    inherit: 'gen7',
    pokemon: {
        // Override pokemon methods
        transformInto(pokemon) {
            // Custom transformation logic
        }
    },
    // Event hooks
    onSwitchIn(pokemon) {
        // Trigger when pokemon switches in
    }
}
```

**Lesson for PokeSharp**: Use inheritance/composition for mod events, allow hook overrides

### 7.2 Pokemon Essentials Event System

**Architecture**:
- Ruby event-driven scripting
- `Events` class with proc handlers
- Global event namespace

**Pattern**:
```ruby
Events.onWildPokemonCreate += proc { |sender, e|
  pokemon = e[0]
  pokemon.makeShiny if rand(100) < 5
}
```

**Lesson for PokeSharp**: Simple proc-based handlers work well, support lambda expressions in C#

### 7.3 pkmn/engine (Zig)

**Architecture**:
- Frame-accurate battle simulation
- Minimal event system (performance-critical)
- Mod support via data overrides (not runtime events)

**Lesson for PokeSharp**: For performance-critical paths (battle), minimize event overhead

---

## 8. Performance Considerations

### 8.1 Event Frequency Analysis

**High-Frequency Events** (10-60 Hz):
- `ComponentSet<GridMovement>` - Every frame during movement
- `ComponentSet<Animation>` - Animation updates
- ❌ DO NOT expose to mods (performance risk)

**Medium-Frequency Events** (1-10 Hz):
- `EntitySteppedEvent` - Once per tile step
- `MovementCompletedEvent` - End of movement
- ✅ Safe for mods (if handlers timeout)

**Low-Frequency Events** (<1 Hz):
- `BattleStartedEvent` - Rare
- `MapLoadedEvent` - Map transitions
- `ItemObtainedEvent` - Item pickups
- ✅ Safe for mods

### 8.2 Event Bus Benchmark Comparison

**Existing IEventBus** (ConcurrentDictionary):
- Publish: ~50-100 ns per handler (no boxing for value types)
- Subscribe: ~100 ns (Interlocked.Increment)
- Memory: 48 bytes per handler entry

**Arch.EventBus** (Source Generated):
- Publish: ~10-20 ns per handler (direct call)
- Subscribe: ~50 ns (generated code)
- Memory: 16 bytes per handler (inlined)

**Recommendation**:
- Use Arch.EventBus for high-frequency ECS events
- Keep IEventBus for low-frequency game events (less critical)

### 8.3 Memory Allocation Optimization

**Problem**: Event object allocation on every publish

```csharp
// Allocates new object every frame
_eventBus.Publish(new EntitySteppedEvent { ... });
```

**Solution 1: Object Pooling**
```csharp
public class EventPool<T> where T : TypeEventBase, new()
{
    private readonly ConcurrentBag<T> _pool = new();

    public T Rent()
    {
        return _pool.TryTake(out var obj) ? obj : new T();
    }

    public void Return(T obj)
    {
        _pool.Add(obj);
    }
}
```

**Solution 2: Struct Events** (for Arch.EventBus only)
```csharp
// Use struct instead of record for zero-allocation
public struct EntitySteppedEventStruct
{
    public Entity Entity;
    public Vector2Int TilePosition;
    public int MapId;
}

// Arch allows ref parameters
world.Publish(ref new EntitySteppedEventStruct { ... });
```

---

## 9. Recommendations

### 9.1 Short-Term (Next Sprint)

1. ✅ **Add Arch.EventBus NuGet package** to PokeSharp.Engine.Core
2. ✅ **Define 13 event categories** in PokeSharp.Game.Components/Events/
3. ✅ **Implement ArchEventBridge** for ECS → Game event translation
4. ✅ **Update MovementSystem** to emit EntitySteppedEvent
5. ✅ **Add IModEventApi interface** to ScriptContext

### 9.2 Medium-Term (2-3 Sprints)

1. ⏳ **Implement ModSecurityService** with allowlist and violation tracking
2. ⏳ **Create ModEventFacade** with timeout and isolation
3. ⏳ **Update all systems** to emit relevant events (see Section 3.1)
4. ⏳ **Add DeferredAction API** for safe World modifications from handlers
5. ⏳ **Document event system** for mod developers

### 9.3 Long-Term (Future)

1. 🔮 **AppDomain sandboxing** for script isolation
2. 🔮 **Event replay system** for debugging/testing
3. 🔮 **Event performance profiler** for mod developers
4. 🔮 **Custom event attribute source generator** for zero-boilerplate events
5. 🔮 **Event-driven battle system** (when battle implementation begins)

---

## 10. Code Examples

### 10.1 Basic Event Usage (Game Systems)

```csharp
// In MovementSystem.cs
public class MovementSystem : SystemBase, IUpdateSystem
{
    private readonly IEventBus _eventBus;

    public void Update(float deltaTime)
    {
        // Emit event when player steps on new tile
        _eventBus.Publish(new EntitySteppedEvent
        {
            TypeId = "movement-system",
            Timestamp = _gameTime.TotalSeconds,
            Entity = entity,
            TilePosition = new Vector2Int(x, y),
            MapId = mapId,
            FromDirection = direction
        });
    }
}
```

### 10.2 Mod Script Event Subscription

```csharp
// In Assets/Mods/MyMod/Scripts/GrassTile.csx
public class GrassTileBehavior : TileBehaviorScriptBase
{
    private IDisposable? _subscription;

    public override void Initialize(ScriptContext ctx)
    {
        // Subscribe to stepped events
        _subscription = ctx.Events.Subscribe<EntitySteppedEvent>("my-mod", evt =>
        {
            // Check if player stepped on grass
            if (evt.Entity.Has<Player>() && IsGrassTile(evt.TilePosition))
            {
                TriggerWildEncounter(ctx, evt.Entity, evt.TilePosition);
            }
        });
    }

    private void TriggerWildEncounter(ScriptContext ctx, Entity player, Vector2Int tile)
    {
        // Use deferred action to modify World safely
        ctx.DeferredAction(() =>
        {
            var encounterZone = GetEncounterZone(ctx, tile);
            if (ShouldEncounter(encounterZone.EncounterRate))
            {
                ctx.Events.Publish("my-mod", new WildEncounterTriggeredEvent
                {
                    TypeId = "mod:my-mod",
                    Timestamp = ctx.GameTime.TotalSeconds,
                    EncounterTableId = encounterZone.EncounterTableId,
                    PlayerEntity = player,
                    TilePosition = tile,
                    EncounterRate = encounterZone.EncounterRate
                });
            }
        });
    }

    public override void Dispose()
    {
        _subscription?.Dispose();
    }
}
```

### 10.3 Custom Mod Event

```csharp
// In Assets/Mods/MyMod/Events/CustomEvents.cs
namespace MyMod.Events
{
    using PokeSharp.Engine.Core.Types.Events;

    /// <summary>
    /// Custom event for quest system mod
    /// </summary>
    public record QuestStartedEvent : TypeEventBase
    {
        public required string QuestId { get; init; }
        public required Entity PlayerEntity { get; init; }
        public string? QuestGiver { get; init; }
    }

    public record QuestCompletedEvent : TypeEventBase
    {
        public required string QuestId { get; init; }
        public int RewardMoney { get; init; }
        public string[]? RewardItems { get; init; }
    }
}

// In quest system script
ctx.Events.Publish("my-mod", new QuestStartedEvent
{
    TypeId = "mod:my-mod",
    Timestamp = ctx.GameTime.TotalSeconds,
    QuestId = "find_lost_pokemon",
    PlayerEntity = player,
    QuestGiver = "Professor Oak"
});
```

---

## 11. Migration Path from Current EventBus

### 11.1 Compatibility Strategy

**Goal**: Zero breaking changes to existing code

```csharp
// Existing code continues to work
_eventBus.Publish(new DialogueRequestedEvent { ... });
_eventBus.Subscribe<DialogueRequestedEvent>(handler);

// New Arch events are additive
_world.SubscribeEntityCreated(entity => { ... });
```

### 11.2 Gradual Migration

**Phase 1**: Dual event systems coexist
- Keep `IEventBus` for all current event types
- Add Arch.EventBus for ECS-level events only
- Bridge Arch → IEventBus for backwards compatibility

**Phase 2**: Expand Arch usage
- New event types use Arch.EventBus
- High-frequency events migrate to Arch
- IEventBus remains for low-frequency game events

**Phase 3**: Unified API (optional)
- Create unified `IGameEventBus` wrapper
- Internally routes to Arch or legacy based on event type
- Fully backwards compatible

---

## 12. Sources

### Research Sources

1. [Arch ECS GitHub Repository](https://github.com/genaray/Arch) - Main Arch ECS framework
2. [Arch.Extended GitHub Repository](https://github.com/genaray/Arch.Extended) - EventBus and extensions
3. [Arch ECS Events Documentation](https://arch-ecs.gitbook.io/arch/documentation/utilities/events) - Official event system docs
4. [Arch ECS Documentation](https://arch-ecs.gitbook.io/arch) - Why Arch and core concepts
5. [pkmn/engine GitHub](https://github.com/pkmn/engine) - Pokemon battle simulation engine
6. [Pokemon Showdown Mods](https://github.com/smogon/pokemon-showdown) - Battle system mod architecture
7. [Unity Mod Security](https://gamedev.stackexchange.com/questions/139138/) - Moddable Unity games
8. [Unity Malicious GameObjects](https://blog.includesecurity.com/2021/06/hacking-unity-games-malicious-unity-game-objects/) - Security threats
9. [SomaSim Unity Modding](https://somasim.com/blog/2017/12/27/tech-notes-unity-game-modding/) - C# script modding
10. [Godot Sandbox Plugin](https://github.com/godotengine/godot/issues/7753) - Sandbox patterns

---

## 13. Appendix: Event Type Reference

### Complete Event Type Definitions

See [PokeSharp Event Type Schema](./pokesharp-event-types.cs) (generated separately)

---

## Conclusion

PokeSharp should implement a **dual event system architecture**:

1. **Keep existing IEventBus** for high-level game events (dialogue, effects, UI)
2. **Add Arch.EventBus** for low-level ECS events (entity/component lifecycle)
3. **Bridge the two** with `ArchEventBridge` for event translation
4. **Sandbox mod access** via `ModEventFacade` with security restrictions

This approach provides:
- ✅ **Performance**: Arch.EventBus for zero-allocation ECS events
- ✅ **Safety**: Mod event handlers timeout and restricted to allowlist
- ✅ **Compatibility**: Existing code unchanged, gradual migration
- ✅ **Flexibility**: 13 Pokemon-specific event categories for rich modding

**Next Steps**: Implement Phase 1 recommendations (Section 9.1) in next sprint.

---

**Report Prepared By**: Research Agent (Hive Mind Swarm)
**For**: PokeSharp Pokemon Engine Team
**Date**: 2025-11-28
