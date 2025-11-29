# ECS Event System - Usage Examples

This document provides practical examples of using the ECS event system in PokeSharp.

## Table of Contents

1. [Basic Event Publishing](#basic-event-publishing)
2. [Event Subscription](#event-subscription)
3. [Cancellable Events](#cancellable-events)
4. [Component Events](#component-events)
5. [Mod/Script Handlers](#modscript-handlers)
6. [Event Adapters](#event-adapters)
7. [Performance Best Practices](#performance-best-practices)

---

## Basic Event Publishing

### Publishing Simple Events

```csharp
using PokeSharp.Engine.Core.Events.ECS;
using Microsoft.Extensions.DependencyInjection;

public class MovementSystem
{
    private readonly IEcsEventBus _eventBus;
    private float _gameTime;

    public MovementSystem(IEcsEventBus eventBus)
    {
        _eventBus = eventBus;
    }

    public void HandlePlayerMovement(Entity playerEntity, Direction direction)
    {
        // Get current position
        var currentPos = world.Get<Position>(playerEntity);
        var targetPos = CalculateTargetPosition(currentPos, direction);

        // Publish movement event
        _eventBus.Publish(new PlayerMovedEvent
        {
            Entity = playerEntity,
            FromPosition = (currentPos.X, currentPos.Y),
            ToPosition = (targetPos.X, targetPos.Y),
            Direction = direction,
            Timestamp = _gameTime,
            Priority = EventPriority.Normal
        });
    }
}
```

### Publishing Map Events

```csharp
public class MapLoaderSystem
{
    private readonly IEcsEventBus _eventBus;

    public void LoadMap(int mapId, string mapName)
    {
        // Load map data
        var map = LoadMapData(mapId);
        var entityCount = SpawnMapEntities(map);

        // Notify subscribers
        _eventBus.Publish(new MapLoadedEvent
        {
            MapId = mapId,
            MapName = mapName,
            EntityCount = entityCount,
            Timestamp = GetGameTime(),
            Priority = EventPriority.Normal
        });
    }
}
```

---

## Event Subscription

### Simple Delegate Subscription

```csharp
public class GameLogger
{
    private IDisposable? _subscription;

    public void Initialize(IEcsEventBus eventBus)
    {
        // Subscribe to map loaded events
        _subscription = eventBus.Subscribe<MapLoadedEvent>(evt =>
        {
            Logger.LogInformation(
                "Map '{Name}' (ID: {Id}) loaded with {Count} entities",
                evt.MapName,
                evt.MapId,
                evt.EntityCount
            );
        }, EventPriority.Low); // Low priority for logging
    }

    public void Cleanup()
    {
        _subscription?.Dispose();
    }
}
```

### Typed Handler Subscription

```csharp
public class EncounterTracker : IEventHandler<EncounterTriggeredEvent>
{
    public EventPriority Priority => EventPriority.Normal;
    private readonly List<EncounterData> _encounters = new();

    public void Handle(EncounterTriggeredEvent evt)
    {
        _encounters.Add(new EncounterData
        {
            Position = evt.Position,
            MapId = evt.MapId,
            Timestamp = evt.Timestamp,
            EncounterType = evt.EncounterType
        });

        Logger.LogDebug(
            "Encounter triggered: {Type} at ({X}, {Y})",
            evt.EncounterType,
            evt.Position.X,
            evt.Position.Y
        );
    }
}

// Registration in DI container
services.AddSingleton<IEventHandler<EncounterTriggeredEvent>, EncounterTracker>();

// Subscription
var tracker = serviceProvider.GetRequiredService<IEventHandler<EncounterTriggeredEvent>>();
var subscription = eventBus.Subscribe(tracker);
```

---

## Cancellable Events

### Validating Movement

```csharp
public class TerrainValidator : IEventHandler<TileEnteringEvent>
{
    public EventPriority Priority => EventPriority.High; // High priority for validation
    private readonly MapService _mapService;

    public void Handle(TileEnteringEvent evt)
    {
        var tile = _mapService.GetTile(evt.MapId, evt.ToPosition.X, evt.ToPosition.Y);

        // Check if tile is walkable
        if (!tile.IsWalkable)
        {
            evt.IsCancelled = true;
            return;
        }

        // Check for water tiles
        if (tile.Type == TileType.Water)
        {
            // Check if entity can surf
            if (!HasAbility(evt.Entity, "Surf"))
            {
                evt.IsCancelled = true;
                ShowMessage("You need Surf to cross water!");
            }
        }
    }
}
```

### Publishing Cancellable Events

```csharp
public class PlayerMovementSystem
{
    private readonly IEcsEventBus _eventBus;

    public void MovePlayer(Entity player, Direction direction)
    {
        var currentPos = world.Get<Position>(player);
        var targetPos = CalculateTargetPosition(currentPos, direction);

        // Create cancellable event
        var movingEvent = new TileEnteringEvent
        {
            Entity = player,
            FromPosition = (currentPos.X, currentPos.Y),
            ToPosition = (targetPos.X, targetPos.Y),
            MapId = GetCurrentMapId(),
            Timestamp = GetGameTime(),
            Priority = EventPriority.Normal
        };

        // Publish and check if cancelled
        if (_eventBus.PublishCancellable(ref movingEvent))
        {
            // Movement allowed - update position
            ref var pos = ref world.Get<Position>(player);
            pos.X = targetPos.X;
            pos.Y = targetPos.Y;

            // Publish confirmation event
            _eventBus.Publish(new TileEnteredEvent
            {
                Entity = player,
                FromPosition = (currentPos.X, currentPos.Y),
                ToPosition = (targetPos.X, targetPos.Y),
                MapId = GetCurrentMapId(),
                Timestamp = GetGameTime(),
                Priority = EventPriority.Normal
            });
        }
        else
        {
            Logger.LogDebug("Movement cancelled by handler");
        }
    }
}
```

---

## Component Events

### Publishing Component Changes

```csharp
public class HealthSystem
{
    private readonly IEcsEventBus _eventBus;

    public void ApplyDamage(Entity entity, int damage)
    {
        ref var health = ref world.Get<Health>(entity);
        var oldHealth = health;

        health.Current = Math.Max(0, health.Current - damage);

        // Publish component changed event
        _eventBus.Publish(new ComponentChangedEvent<Health>
        {
            Entity = entity,
            OldValue = oldHealth,
            NewValue = health,
            Timestamp = GetGameTime(),
            Priority = EventPriority.High
        });

        // Check for death
        if (health.Current == 0)
        {
            HandleEntityDeath(entity);
        }
    }
}
```

### Subscribing to Component Changes

```csharp
public class HealthBarUpdater : IEventHandler<ComponentChangedEvent<Health>>
{
    public EventPriority Priority => EventPriority.Normal;

    public void Handle(ComponentChangedEvent<Health> evt)
    {
        // Update UI health bar
        var healthPercent = (float)evt.NewValue.Current / evt.NewValue.Max;
        UpdateHealthBar(evt.Entity, healthPercent);

        // Show damage numbers
        var damage = evt.OldValue.Current - evt.NewValue.Current;
        if (damage > 0)
        {
            ShowDamageNumber(evt.Entity, damage);
        }
    }
}
```

---

## Mod/Script Handlers

### Creating a Mod Handler

```csharp
// In a mod DLL
using PokeSharp.Engine.Core.Events.ECS;

public class CustomEncounterMod : IEventHandler<EncounterTriggeringEvent>
{
    public EventPriority Priority => EventPriority.High;

    public void Handle(EncounterTriggeringEvent evt)
    {
        // Custom encounter logic
        if (evt.EncounterType == "grass" && IsNightTime())
        {
            // Increase encounter rate at night
            if (ShouldTriggerBonus())
            {
                Logger.LogInformation("Night-time bonus encounter triggered!");
            }
        }

        // Cancel encounters in safe zones
        if (IsInSafeZone(evt.Position, evt.MapId))
        {
            evt.IsCancelled = true;
        }
    }
}
```

### Script-Based Handler

```csharp
// Script loaded by ModLoader
public class GymDoorScript
{
    public void OnLoad(IEcsEventBus eventBus, ScriptContext context)
    {
        // Subscribe to player movement
        eventBus.Subscribe<TileEnteringEvent>(evt =>
        {
            // Check if entering gym door tile
            if (evt.ToPosition == (10, 5) && evt.MapId == 3)
            {
                // Check for required badges
                if (!context.Player.HasBadge("Boulder Badge"))
                {
                    evt.IsCancelled = true;
                    context.Dialogue.ShowMessage("You need the Boulder Badge to enter!");
                }
            }
        }, EventPriority.High);
    }
}
```

---

## Event Adapters

### Bridging to Legacy Events

```csharp
public class EventSystemSetup
{
    public void ConfigureAdapters(IServiceCollection services)
    {
        services.AddSingleton<EcsEventAdapter>(sp =>
        {
            var ecsEventBus = sp.GetRequiredService<IEcsEventBus>();
            var legacyEventBus = sp.GetRequiredService<IEventBus>();
            var logger = sp.GetRequiredService<ILogger<EcsEventAdapter>>();

            var adapter = new EcsEventAdapter(ecsEventBus, legacyEventBus, logger);

            // Setup bidirectional forwarding for compatibility
            adapter.BidirectionalForward<MapLoadedEvent, LegacyMapLoadedEvent>(
                // ECS to Legacy
                ecsEvt => new LegacyMapLoadedEvent
                {
                    TypeId = ecsEvt.MapId.ToString(),
                    Timestamp = ecsEvt.Timestamp,
                    MapName = ecsEvt.MapName ?? "",
                    EntityCount = ecsEvt.EntityCount
                },
                // Legacy to ECS
                legacyEvt => new MapLoadedEvent
                {
                    MapId = int.Parse(legacyEvt.TypeId),
                    MapName = legacyEvt.MapName,
                    EntityCount = legacyEvt.EntityCount,
                    Timestamp = legacyEvt.Timestamp,
                    Priority = EventPriority.Normal
                }
            );

            return adapter;
        });
    }
}
```

---

## Performance Best Practices

### 1. Use Struct Events for High Frequency

```csharp
// ✅ GOOD: Struct-based, zero allocation
_eventBus.Publish(new PlayerMovedEvent { /* ... */ });

// ❌ BAD: Don't use legacy events for high-frequency
_legacyEventBus.Publish(new LegacyPlayerMovedEvent { /* ... */ });
```

### 2. Dispose Subscriptions

```csharp
public class TemporaryHandler : IDisposable
{
    private readonly List<IDisposable> _subscriptions = new();

    public void Subscribe(IEcsEventBus eventBus)
    {
        _subscriptions.Add(eventBus.Subscribe<MapLoadedEvent>(OnMapLoaded));
        _subscriptions.Add(eventBus.Subscribe<PlayerMovedEvent>(OnPlayerMoved));
    }

    public void Dispose()
    {
        foreach (var sub in _subscriptions)
        {
            sub.Dispose();
        }
        _subscriptions.Clear();
    }
}
```

### 3. Avoid Heavy Work in Handlers

```csharp
// ✅ GOOD: Queue work for background processing
public void Handle(ComponentChangedEvent<Health> evt)
{
    if (evt.NewValue.Current == 0)
    {
        _deathQueue.Enqueue(evt.Entity);
    }
}

// ❌ BAD: Heavy processing blocks event publishing
public void Handle(ComponentChangedEvent<Health> evt)
{
    if (evt.NewValue.Current == 0)
    {
        PlayDeathAnimation(evt.Entity); // Blocks!
        UpdateAllUI(); // Blocks!
        SaveToDatabase(evt.Entity); // Blocks!
    }
}
```

### 4. Use Appropriate Priorities

```csharp
// Critical: Core game loop only
EventPriority.Critical

// Highest: Validation, anti-cheat
EventPriority.Highest

// High: Game logic, AI
EventPriority.High

// Normal: Standard features
EventPriority.Normal

// Low: Analytics, logging
EventPriority.Low

// Lowest: Debug, profiling
EventPriority.Lowest
```

---

## Complete Integration Example

```csharp
// Startup configuration
public class GameStartup
{
    public void ConfigureServices(IServiceCollection services)
    {
        // Register ECS event bus
        services.AddSingleton<IEcsEventBus, ArchEcsEventBus>();

        // Register legacy event bus
        services.AddSingleton<IEventBus, EventBus>();

        // Register adapter for compatibility
        services.AddSingleton<EcsEventAdapter>();

        // Register event handlers
        services.AddSingleton<IEventHandler<MapLoadedEvent>, MapLoadedHandler>();
        services.AddSingleton<IEventHandler<TileEnteringEvent>, TerrainValidator>();
        services.AddSingleton<IEventHandler<ComponentChangedEvent<Health>>, HealthBarUpdater>();
    }

    public void Initialize(IServiceProvider services)
    {
        var eventBus = services.GetRequiredService<IEcsEventBus>();

        // Subscribe all registered handlers
        foreach (var handler in services.GetServices<IEventHandler<MapLoadedEvent>>())
        {
            eventBus.Subscribe(handler);
        }

        foreach (var handler in services.GetServices<IEventHandler<TileEnteringEvent>>())
        {
            eventBus.Subscribe(handler);
        }

        // Setup adapters
        var adapter = services.GetRequiredService<EcsEventAdapter>();
        SetupEventAdapters(adapter);
    }
}
```

---

## See Also

- [ECS Event System Design](ecs-event-system-design.md)
- [Migration Guide](ecs-event-migration-guide.md)
- [API Reference](ecs-event-api-reference.md)
