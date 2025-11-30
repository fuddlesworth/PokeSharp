# Arch ECS Event System Research Report

**Date:** 2025-11-29
**Researcher:** Hive Mind Research Agent
**Context:** PokeSharp EventBus Migration to Arch.Event Integration

---

## Executive Summary

This research investigates Arch ECS's event system capabilities and how they can replace PokeSharp's current custom EventBus implementation. Arch ECS provides **two distinct event systems**:

1. **Arch-Events** (v2.1.0) - Built-in ECS entity/component lifecycle events
2. **Arch.EventBus** (v1.0.2) - Source-generated high-performance general-purpose event bus

The current PokeSharp EventBus can be replaced with **Arch.EventBus**, while **Arch-Events** should be used for ECS-specific entity lifecycle tracking.

---

## 1. Arch-Events Package Overview

### Package Details
- **NuGet Package:** `Arch-Events`
- **Current Version:** 2.1.0 (released May 27, 2025)
- **License:** Apache 2.0
- **Downloads:** 833 total
- **Repository:** https://github.com/genaray/Arch

### Supported Frameworks
- .NET 6.0
- .NET 8.0
- .NET Standard 2.1

### Dependencies
```xml
<PackageReference Include="Arch-Events" Version="2.1.0" />
```

Arch-Events requires:
- Arch.LowLevel (≥ 1.1.5)
- Collections.Pooled (≥ 2.0.0-preview.27)
- CommunityToolkit.HighPerformance (≥ 8.2.2)
- Microsoft.Extensions.ObjectPool (≥ 7.0.0)
- System.Runtime.CompilerServices.Unsafe (≥ 6.0.0)
- ZeroAllocJobScheduler (≥ 1.1.2)

### Installation
```bash
dotnet add package Arch-Events --version 2.1.0
```

---

## 2. Arch-Events API: Entity & Component Lifecycle Events

### Overview
Arch-Events provides direct hooks into ECS entity and component lifecycle events. These events are **built into the Arch World** and execute as **synchronous method calls** with zero allocation.

### Enabling Events
Events require the `EVENTS` flag to be set in Arch's source code. See the [PURE_ECS documentation](https://arch-ecs.gitbook.io/arch/documentation/optimizations/pure_ecs) for details.

### Available Event Subscriptions

```csharp
using Arch.Core;

// Create world
var world = World.Create();

// Entity lifecycle events
world.SubscribeEntityCreated((Entity entity) => {
    Console.WriteLine($"Entity {entity.Id} was created");
});

world.SubscribeEntityDestroyed((Entity entity) => {
    Console.WriteLine($"Entity {entity.Id} was destroyed");
});

// Component lifecycle events (type-specific)
world.SubscribeComponentAdded((Entity entity, ref Position position) => {
    Console.WriteLine($"Position component added to entity {entity.Id}");
});

world.SubscribeComponentSet((Entity entity, ref Position position) => {
    Console.WriteLine($"Position component modified on entity {entity.Id}");
});

world.SubscribeComponentRemoved((Entity entity, ref Position position) => {
    Console.WriteLine($"Position component removed from entity {entity.Id}");
});
```

### Event Characteristics
- **Zero-allocation**: Events are direct method calls
- **Synchronous**: Execute immediately on the same thread
- **Type-safe**: Component events are generic and type-specific
- **High-performance**: Designed for real-time game loops
- **No delay**: Callbacks fire immediately after the operation

### Use Cases for PokeSharp
Arch-Events is ideal for:
- Debugging entity creation/destruction
- Tracking component lifecycle for debugging UI
- Implementing ECS-specific patterns (e.g., reactive systems)
- **NOT suitable for game events** (dialogue, battles, warps)

---

## 3. Arch.EventBus Package Overview

### Package Details
- **NuGet Package:** `Arch.EventBus`
- **Current Version:** 1.0.2 (released May 7, 2023)
- **License:** Apache 2.0
- **Downloads:** 6.1K total
- **Repository:** https://github.com/genaray/Arch.Extended

### Description
"A source generated EventBus, send Events with high-performance!"

### Installation
```bash
dotnet add package Arch.EventBus --version 1.0.2
```

### Dependencies
- No dependencies (targets .NET Standard 2.0+)
- Compatible with .NET Standard 2.1, .NET 6, 7, 8+
- Works with Unity and Godot

---

## 4. Arch.EventBus API: Source-Generated Event Bus

### Overview
Arch.EventBus uses **source generation** to create a high-performance event bus. When added to a project, it generates an `EventBus` class via `Arch.Bus.QueryGenerator\EventBus.g.cs`.

### Sending Events

```csharp
// Define event struct
public struct DialogueRequestEvent
{
    public string Message { get; init; }
    public string? SpeakerName { get; init; }
    public int Priority { get; init; }
}

// Send event
var dialogueEvent = new DialogueRequestEvent
{
    Message = "Hello, traveler!",
    SpeakerName = "Professor Oak",
    Priority = 1
};

EventBus.Send(ref dialogueEvent);
```

### Receiving Events

**NOTE:** The exact subscription API is not fully documented in public sources. Based on the MonoGame community announcement, Arch.EventBus supports "instance support" suggesting:

```csharp
// Likely subscription pattern (needs verification)
EventBus.On<DialogueRequestEvent>((ref DialogueRequestEvent evt) => {
    Console.WriteLine($"Dialogue: {evt.Message}");
});
```

### Integration with World

From GitHub issue #65:
```csharp
// Events can include World references
var @event = (Main.World, key);
EventBus.Send(ref @event);
```

### Known Issues
- **Naming Conflicts:** The generated `EventBus` class can conflict with custom EventBus types
- **Solution:** Use fully qualified names or rename custom implementations

---

## 5. Comparison: Custom EventBus vs Arch.EventBus

### PokeSharp's Current EventBus

**Location:** `/PokeSharp.Engine.Core/Events/EventBus.cs`

**Architecture:**
```csharp
public class EventBus : IEventBus
{
    // ConcurrentDictionary for thread-safe handler management
    private readonly ConcurrentDictionary<Type, ConcurrentDictionary<int, Delegate>> _handlers;

    // Publish/subscribe pattern
    public void Publish<TEvent>(TEvent eventData) where TEvent : TypeEventBase
    public IDisposable Subscribe<TEvent>(Action<TEvent> handler) where TEvent : TypeEventBase

    // Management methods
    public void ClearSubscriptions<TEvent>()
    public void ClearAllSubscriptions()
    public int GetSubscriberCount<TEvent>()
}
```

**Features:**
- Thread-safe with `ConcurrentDictionary`
- Synchronous event firing on caller's thread
- Error isolation (handlers don't break event publishing)
- Disposable subscriptions with unique handler IDs
- Type constraint: Events must inherit from `TypeEventBase`
- Logging with `ILogger<EventBus>`

**Current Event Types:**
```csharp
// Base event
public abstract record TypeEventBase
{
    public required string TypeId { get; init; }
    public required float Timestamp { get; init; }
}

// Implemented events
public sealed record DialogueRequestedEvent : TypeEventBase
public sealed record EffectRequestedEvent : TypeEventBase
public sealed record ClearEffectsRequestedEvent : TypeEventBase
public sealed record ClearMessagesRequestedEvent : TypeEventBase
```

### Arch.EventBus Capabilities

**Advantages:**
1. **Source-generated** - Zero runtime reflection overhead
2. **High-performance** - Optimized code generation
3. **Pass-by-reference** - `EventBus.Send(ref @event)` avoids copies
4. **No base class requirement** - Events can be any struct
5. **Part of Arch ecosystem** - Seamless integration

**Limitations (from available research):**
1. **Sparse documentation** - Limited public examples
2. **Subscription API unclear** - Need to examine source code
3. **No built-in error isolation** - Unknown handler exception behavior
4. **No subscriber count API** - Management features unclear

---

## 6. Migration Strategy: Custom EventBus → Arch.EventBus

### Phase 1: Add Arch.EventBus Package

```xml
<!-- PokeSharp.Engine.Core/PokeSharp.Engine.Core.csproj -->
<ItemGroup>
  <PackageReference Include="Arch" Version="2.1.0" />
  <PackageReference Include="Arch.EventBus" Version="1.0.2" />
  <PackageReference Include="Microsoft.Extensions.Logging.Abstractions" Version="10.0.0" />
</ItemGroup>
```

### Phase 2: Investigate Arch.EventBus Generated API

**Action Items:**
1. Add `Arch.EventBus` to `PokeSharp.Engine.Core`
2. Build project and examine generated `EventBus.g.cs`
3. Document subscription/unsubscription patterns
4. Test error handling behavior
5. Verify thread-safety guarantees

### Phase 3: Create Adapter/Wrapper (if needed)

If Arch.EventBus lacks features like error isolation or logging, create a wrapper:

```csharp
/// <summary>
/// Wrapper around Arch.EventBus with logging and error isolation.
/// </summary>
public class ArchEventBusAdapter : IEventBus
{
    private readonly ILogger<ArchEventBusAdapter> _logger;

    public void Publish<TEvent>(TEvent eventData) where TEvent : TypeEventBase
    {
        try
        {
            EventBus.Send(ref eventData);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error publishing event {EventType}", typeof(TEvent).Name);
        }
    }

    // Implement Subscribe, etc.
}
```

### Phase 4: Migrate Event Types

Convert from record classes to structs for Arch.EventBus:

**Before (Custom EventBus):**
```csharp
public sealed record DialogueRequestedEvent : TypeEventBase
{
    public required string Message { get; init; }
    public string? SpeakerName { get; init; }
    public int Priority { get; init; } = 0;
}
```

**After (Arch.EventBus):**
```csharp
public struct DialogueRequestedEvent
{
    public string TypeId { get; init; }
    public float Timestamp { get; init; }
    public string Message { get; init; }
    public string? SpeakerName { get; init; }
    public int Priority { get; init; }
}
```

### Phase 5: Update Subscribers

**Before:**
```csharp
var subscription = _eventBus.Subscribe<DialogueRequestedEvent>(evt => {
    DisplayDialogue(evt.Message, evt.SpeakerName);
});

// Later...
subscription.Dispose();
```

**After (using Arch.EventBus):**
```csharp
// TODO: Verify exact API from generated code
EventBus.On<DialogueRequestedEvent>((ref DialogueRequestedEvent evt) => {
    DisplayDialogue(evt.Message, evt.SpeakerName);
});

// TODO: Verify unsubscription pattern
```

### Phase 6: Testing & Validation

1. **Unit tests** for event publishing/subscription
2. **Integration tests** for cross-system communication
3. **Performance benchmarks** comparing custom vs Arch.EventBus
4. **Memory profiling** to verify zero-allocation claims

---

## 7. Recommended Event Types for Pokemon Game Mechanics

### Battle Events
```csharp
public struct BattleStartedEvent
{
    public string TypeId { get; init; }
    public float Timestamp { get; init; }
    public Entity PlayerEntity { get; init; }
    public Entity OpponentEntity { get; init; }
    public BattleType Type { get; init; } // Wild, Trainer, Horde
}

public struct BattleTurnEvent
{
    public string TypeId { get; init; }
    public float Timestamp { get; init; }
    public Entity Attacker { get; init; }
    public Entity Defender { get; init; }
    public string MoveName { get; init; }
    public int Damage { get; init; }
}

public struct BattleEndedEvent
{
    public string TypeId { get; init; }
    public float Timestamp { get; init; }
    public BattleResult Result { get; init; } // Won, Lost, Fled, Draw
    public int ExperienceGained { get; init; }
    public int MoneyGained { get; init; }
}
```

### Encounter Events
```csharp
public struct WildEncounterEvent
{
    public string TypeId { get; init; }
    public float Timestamp { get; init; }
    public string PokemonSpecies { get; init; }
    public int Level { get; init; }
    public Point Position { get; init; }
    public EncounterMethod Method { get; init; } // Grass, Cave, Water, Horde
}

public struct TrainerEncounterEvent
{
    public string TypeId { get; init; }
    public float Timestamp { get; init; }
    public Entity TrainerEntity { get; init; }
    public string TrainerName { get; init; }
    public string TrainerClass { get; init; }
}
```

### Map & Warp Events
```csharp
public struct WarpRequestedEvent
{
    public string TypeId { get; init; }
    public float Timestamp { get; init; }
    public int TargetMapId { get; init; }
    public Point TargetPosition { get; init; }
    public Direction FacingDirection { get; init; }
    public WarpType Type { get; init; } // Door, Stairs, Teleport, Fly
}

public struct MapTransitionEvent
{
    public string TypeId { get; init; }
    public float Timestamp { get; init; }
    public int PreviousMapId { get; init; }
    public int CurrentMapId { get; init; }
    public string MapName { get; init; }
}
```

### Dialogue Events (existing)
```csharp
public struct DialogueRequestedEvent
{
    public string TypeId { get; init; }
    public float Timestamp { get; init; }
    public string Message { get; init; }
    public string? SpeakerName { get; init; }
    public int Priority { get; init; }
}

public struct DialogueCompletedEvent
{
    public string TypeId { get; init; }
    public float Timestamp { get; init; }
    public string DialogueId { get; init; }
    public int Choice { get; init; } // For yes/no prompts
}
```

### Party & Pokemon Events
```csharp
public struct PokemonCaughtEvent
{
    public string TypeId { get; init; }
    public float Timestamp { get; init; }
    public string Species { get; init; }
    public int Level { get; init; }
    public bool IsShiny { get; init; }
    public string Nickname { get; init; }
}

public struct PokemonFaintedEvent
{
    public string TypeId { get; init; }
    public float Timestamp { get; init; }
    public Entity PokemonEntity { get; init; }
    public string Species { get; init; }
}

public struct PokemonEvolvedEvent
{
    public string TypeId { get; init; }
    public float Timestamp { get; init; }
    public Entity PokemonEntity { get; init; }
    public string FromSpecies { get; init; }
    public string ToSpecies { get; init; }
}
```

### Item Events
```csharp
public struct ItemUsedEvent
{
    public string TypeId { get; init; }
    public float Timestamp { get; init; }
    public string ItemId { get; init; }
    public Entity? TargetPokemon { get; init; }
    public ItemUseResult Result { get; init; }
}

public struct ItemObtainedEvent
{
    public string TypeId { get; init; }
    public float Timestamp { get; init; }
    public string ItemId { get; init; }
    public int Quantity { get; init; }
    public ObtainMethod Method { get; init; } // Pickup, Purchase, Gift
}
```

### Menu & UI Events
```csharp
public struct MenuOpenedEvent
{
    public string TypeId { get; init; }
    public float Timestamp { get; init; }
    public MenuType Type { get; init; } // Party, Bag, Pokemon, Save
}

public struct MenuClosedEvent
{
    public string TypeId { get; init; }
    public float Timestamp { get; init; }
    public MenuType Type { get; init; }
}
```

### Effect Events (existing)
```csharp
public struct EffectRequestedEvent
{
    public string TypeId { get; init; }
    public float Timestamp { get; init; }
    public string EffectId { get; init; }
    public Point Position { get; init; }
    public float Duration { get; init; }
    public float Scale { get; init; }
}
```

---

## 8. Integration Patterns with Arch.Core World

### Pattern 1: Event-Driven Systems

```csharp
using Arch.Core;

public class BattleSystem : ISystem
{
    private readonly World _world;

    public BattleSystem(World world)
    {
        _world = world;

        // Subscribe to battle events
        EventBus.On<BattleStartedEvent>((ref BattleStartedEvent evt) =>
        {
            InitializeBattle(evt.PlayerEntity, evt.OpponentEntity);
        });
    }

    private void InitializeBattle(Entity player, Entity opponent)
    {
        // Create battle state components
        _world.Add(player, new BattleState { IsActive = true });

        // Publish turn start event
        var turnEvent = new BattleTurnEvent
        {
            TypeId = "battle-system",
            Timestamp = Time.GameTime,
            Attacker = player,
            Defender = opponent
        };
        EventBus.Send(ref turnEvent);
    }
}
```

### Pattern 2: Reactive Component Updates

Combine **Arch-Events** (entity lifecycle) with **Arch.EventBus** (game events):

```csharp
public class HealthSystem : ISystem
{
    private readonly World _world;

    public HealthSystem(World world)
    {
        _world = world;

        // React to Pokemon fainting (game event)
        EventBus.On<PokemonFaintedEvent>((ref PokemonFaintedEvent evt) =>
        {
            if (_world.IsAlive(evt.PokemonEntity))
            {
                _world.Add(evt.PokemonEntity, new FaintedTag());
            }
        });

        // React to component changes (ECS event)
        _world.SubscribeComponentSet((Entity entity, ref Health health) =>
        {
            if (health.Current <= 0)
            {
                // Publish game event
                var faintedEvent = new PokemonFaintedEvent
                {
                    TypeId = "health-system",
                    Timestamp = Time.GameTime,
                    PokemonEntity = entity
                };
                EventBus.Send(ref faintedEvent);
            }
        });
    }
}
```

### Pattern 3: Cross-System Communication

```csharp
// Warp System publishes map transitions
public class WarpSystem : ISystem
{
    public void ProcessWarp(Entity player, int targetMap, Point targetPos)
    {
        // Execute warp logic...

        // Notify other systems
        var transitionEvent = new MapTransitionEvent
        {
            TypeId = "warp-system",
            Timestamp = Time.GameTime,
            PreviousMapId = currentMap,
            CurrentMapId = targetMap,
            MapName = GetMapName(targetMap)
        };
        EventBus.Send(ref transitionEvent);
    }
}

// Map Streaming System reacts to transitions
public class MapStreamingSystem : ISystem
{
    public MapStreamingSystem()
    {
        EventBus.On<MapTransitionEvent>((ref MapTransitionEvent evt) =>
        {
            UnloadMap(evt.PreviousMapId);
            LoadMap(evt.CurrentMapId);
        });
    }
}
```

---

## 9. Key Differences: Custom EventBus vs Arch.EventBus

| Feature | Custom EventBus | Arch.EventBus |
|---------|----------------|---------------|
| **Event Type** | Record classes | Structs (value types) |
| **Base Class** | Requires `TypeEventBase` | No requirement |
| **Passing** | By value (copy) | By reference (`ref`) |
| **Performance** | Reflection-based | Source-generated |
| **Thread Safety** | `ConcurrentDictionary` | Unknown (needs research) |
| **Error Handling** | Isolated (try-catch per handler) | Unknown (needs research) |
| **Subscriber Count** | `GetSubscriberCount<T>()` | Unknown API |
| **Clear Subscriptions** | `ClearSubscriptions<T>()` | Unknown API |
| **Logging** | `ILogger` integration | No built-in logging |
| **Disposable Subscriptions** | Yes (IDisposable) | Unknown pattern |
| **World Integration** | No direct integration | Can include World in events |

---

## 10. Research Gaps & Action Items

### Questions Requiring Investigation

1. **Arch.EventBus Subscription API**
   - How to subscribe to events?
   - How to unsubscribe?
   - Are subscriptions disposable?

2. **Error Handling**
   - What happens if a handler throws?
   - Is there error isolation?
   - Can we wrap handlers in try-catch?

3. **Thread Safety**
   - Is Arch.EventBus thread-safe?
   - Can events be published from multiple threads?
   - Are handlers executed on the publishing thread?

4. **Management Features**
   - Can we get subscriber count?
   - Can we clear subscriptions?
   - Can we list active subscriptions?

5. **Performance Characteristics**
   - Allocation profile
   - Throughput benchmarks
   - Latency measurements

### Next Steps

1. **Install & Examine Generated Code**
   ```bash
   dotnet add package Arch.EventBus --version 1.0.2
   dotnet build
   # Examine: obj/Debug/net9.0/generated/Arch.Bus.QueryGenerator/EventBus.g.cs
   ```

2. **Create Proof-of-Concept**
   - Test event publishing/subscription
   - Verify error handling
   - Benchmark performance

3. **Join Arch Discord**
   - URL: https://discord.gg/htc8tX3NxZ
   - Ask maintainers about subscription patterns
   - Request documentation improvements

4. **Review Source Code**
   - Repository: https://github.com/genaray/Arch.Extended
   - Examine EventBus source generator
   - Check for examples in tests

---

## 11. Recommendations

### Short-Term (Immediate)
1. **Keep Custom EventBus** until Arch.EventBus API is fully understood
2. **Add Arch-Events** for entity/component lifecycle tracking (debugging UI)
3. **Create POC** to test Arch.EventBus in isolated environment

### Medium-Term (1-2 Weeks)
1. **Document Arch.EventBus API** through code generation examination
2. **Create adapter layer** if needed for missing features
3. **Migrate one subsystem** (e.g., dialogue) as a pilot test

### Long-Term (1-2 Months)
1. **Full migration** to Arch.EventBus if pilot succeeds
2. **Implement event types** for all Pokemon game mechanics
3. **Performance optimization** based on profiling data

---

## 12. Sources & References

### Official Documentation
- [Arch ECS Documentation](https://arch-ecs.gitbook.io/arch)
- [Arch Events Documentation](https://arch-ecs.gitbook.io/arch/documentation/utilities/events)
- [Arch GitHub Repository](https://github.com/genaray/Arch)
- [Arch.Extended GitHub Repository](https://github.com/genaray/Arch.Extended)

### NuGet Packages
- [Arch NuGet Package](https://www.nuget.org/packages/Arch)
- [Arch-Events NuGet Package](https://www.nuget.org/packages/Arch-Events)
- [Arch.EventBus NuGet Package](https://www.nuget.org/packages/Arch.EventBus)

### Community Resources
- [Arch Discord](https://discord.gg/htc8tX3NxZ)
- [MonoGame Community: Arch ECS Announcement](https://community.monogame.net/t/arch-ecs-received-improvements-eventbus-instance-support-and-its-own-discord-check-it-out/18959)

### Pokemon Game Mechanics
- [Essentials Docs: Event Encounters](https://essentialsdocs.fandom.com/wiki/Event_encounters)
- [Bulbapedia: Pokemon Battle](https://bulbapedia.bulbagarden.net/wiki/Pok%C3%A9mon_battle)
- [Bulbapedia: Wild Pokemon Events](https://bulbapedia.bulbagarden.net/wiki/List_of_wild_Pok%C3%A9mon_from_in-game_events)

---

## Conclusion

Arch ECS provides **two complementary event systems** that can enhance PokeSharp:

1. **Arch-Events** - For ECS-specific entity/component lifecycle tracking
2. **Arch.EventBus** - For general-purpose game event distribution

The current custom EventBus can be replaced with **Arch.EventBus** for improved performance and integration with the Arch ecosystem. However, **further research is required** to document the subscription API and verify feature parity.

**Recommended Next Action:** Install Arch.EventBus, examine generated code, and create a proof-of-concept migration for the dialogue system.

---

**End of Research Report**
