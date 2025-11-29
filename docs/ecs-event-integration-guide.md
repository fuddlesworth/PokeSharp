# ECS Event System - Integration Guide

Step-by-step guide for integrating the ECS event system into PokeSharp.

## Prerequisites

- Arch ECS 2.1.0 already integrated ✅
- Existing `IEventBus` and `TypeEventBase` system ✅
- `ScriptContext` and `ModLoader` in place ✅

## Step 1: Add NuGet Package

Add Arch.EventBus to `PokeSharp.Engine.Core.csproj`:

```bash
cd /mnt/c/Users/nate0/RiderProjects/PokeSharp/PokeSharp.Engine.Core
dotnet add package Arch.EventBus --version 2.1.0
```

Or manually edit the `.csproj`:

```xml
<ItemGroup>
  <PackageReference Include="Arch.EventBus" Version="2.1.0" />
</ItemGroup>
```

## Step 2: Apply Source Generation Attributes

Update `/PokeSharp.Engine.Core/Events/ECS/ArchEcsEventBus.cs`:

```csharp
using Arch.EventBus;

namespace PokeSharp.Engine.Core.Events.ECS;

// Add [Event] attribute for source generation
[Event]
// Register all event types
[RegisterEvent<EntityCreatingEvent>]
[RegisterEvent<EntityCreatedEvent>]
[RegisterEvent<EntityDestroyingEvent>]
[RegisterEvent<EntityDestroyedEvent>]
[RegisterEvent<MapLoadingEvent>]
[RegisterEvent<MapLoadedEvent>]
[RegisterEvent<MapUnloadingEvent>]
[RegisterEvent<MapUnloadedEvent>]
[RegisterEvent<TileEnteringEvent>]
[RegisterEvent<TileEnteredEvent>]
[RegisterEvent<TileExitingEvent>]
[RegisterEvent<TileExitedEvent>]
[RegisterEvent<PlayerMovingEvent>]
[RegisterEvent<PlayerMovedEvent>]
[RegisterEvent<NpcMovingEvent>]
[RegisterEvent<NpcMovedEvent>]
[RegisterEvent<InteractionTriggeringEvent>]
[RegisterEvent<InteractionTriggeredEvent>]
[RegisterEvent<EncounterTriggeringEvent>]
[RegisterEvent<EncounterTriggeredEvent>]
[RegisterEvent<BattleStartingEvent>]
[RegisterEvent<BattleStartedEvent>]
[RegisterEvent<BattleEndingEvent>]
[RegisterEvent<BattleEndedEvent>]
[RegisterEvent<ScriptLoadingEvent>]
[RegisterEvent<ScriptLoadedEvent>]
[RegisterEvent<ScriptUnloadingEvent>]
[RegisterEvent<ScriptUnloadedEvent>]
[RegisterEvent<ModLoadingEvent>]
[RegisterEvent<ModLoadedEvent>]
[RegisterEvent<ModUnloadingEvent>]
[RegisterEvent<ModUnloadedEvent>]
[RegisterEvent<HotReloadTriggeringEvent>]
[RegisterEvent<HotReloadTriggeredEvent>]
public sealed partial class ArchEcsEventBus : IEcsEventBus
{
    // Existing implementation...
}
```

## Step 3: Register Services in DI Container

Find your DI registration code (likely in `Program.cs` or `Startup.cs`) and add:

```csharp
using PokeSharp.Engine.Core.Events;
using PokeSharp.Engine.Core.Events.ECS;

public void ConfigureServices(IServiceCollection services)
{
    // Existing services...

    // Register ECS event bus
    services.AddSingleton<IEcsEventBus, ArchEcsEventBus>();

    // Register adapter for legacy compatibility
    services.AddSingleton<EcsEventAdapter>();

    // Legacy event bus (already registered, keep as-is)
    // services.AddSingleton<IEventBus, EventBus>();
}
```

## Step 4: Update ScriptContext

Update `/PokeSharp.Game.Scripting/Runtime/ScriptContext.cs`:

```csharp
using PokeSharp.Engine.Core.Events.ECS;

public sealed class ScriptContext
{
    private readonly IScriptingApiProvider _apis;
    private readonly Entity? _entity;
    private readonly IEcsEventBus _eventBus; // Add this

    public ScriptContext(
        World world,
        Entity? entity,
        ILogger logger,
        IScriptingApiProvider apis,
        IEcsEventBus eventBus // Add this parameter
    )
    {
        World = world ?? throw new ArgumentNullException(nameof(world));
        Logger = logger ?? throw new ArgumentNullException(nameof(logger));
        _entity = entity;
        _apis = apis ?? throw new ArgumentNullException(nameof(apis));
        _eventBus = eventBus ?? throw new ArgumentNullException(nameof(eventBus));
    }

    // Add public property
    /// <summary>
    ///     Gets the ECS event bus for high-performance event handling.
    /// </summary>
    public IEcsEventBus EventBus => _eventBus;

    // Existing properties and methods...
}
```

## Step 5: Update ScriptService to Pass EventBus

Find where `ScriptContext` is constructed (likely in `ScriptService.cs`):

```csharp
public class ScriptService
{
    private readonly IEcsEventBus _eventBus;
    private readonly IScriptingApiProvider _apis;
    // ... other fields

    public ScriptService(IEcsEventBus eventBus, IScriptingApiProvider apis /* ... */)
    {
        _eventBus = eventBus;
        _apis = apis;
        // ...
    }

    public void ExecuteEntityScript(Entity entity, string scriptPath)
    {
        // Update ScriptContext construction
        var context = new ScriptContext(
            _world,
            entity,
            _logger,
            _apis,
            _eventBus // Add this
        );

        // ... execute script with context
    }
}
```

## Step 6: Setup Event Adapters (Optional - For Migration)

Create a startup class to configure adapters:

```csharp
public class EventSystemConfiguration
{
    public static void ConfigureAdapters(IServiceProvider services)
    {
        var adapter = services.GetRequiredService<EcsEventAdapter>();
        var logger = services.GetRequiredService<ILogger<EventSystemConfiguration>>();

        // Example: Forward MapLoadedEvent to legacy system
        adapter.ForwardToLegacy<MapLoadedEvent, DialogueRequestedEvent>(ecsEvt =>
        {
            // Convert ECS event to legacy event
            // Only if needed for compatibility
            return new DialogueRequestedEvent
            {
                TypeId = ecsEvt.MapId.ToString(),
                Timestamp = ecsEvt.Timestamp,
                Message = $"Map {ecsEvt.MapName} loaded with {ecsEvt.EntityCount} entities",
                Priority = 0
            };
        });

        logger.LogInformation("Event adapters configured");
    }
}

// Call in Program.cs after services are built
var serviceProvider = services.BuildServiceProvider();
EventSystemConfiguration.ConfigureAdapters(serviceProvider);
```

## Step 7: Publish First ECS Event

Update a system to publish an ECS event (example: MapLoaderSystem):

```csharp
public class MapLoaderSystem
{
    private readonly IEcsEventBus _eventBus;
    private readonly ILogger<MapLoaderSystem> _logger;

    public MapLoaderSystem(IEcsEventBus eventBus, ILogger<MapLoaderSystem> logger)
    {
        _eventBus = eventBus;
        _logger = logger;
    }

    public void LoadMap(int mapId, string mapName)
    {
        _logger.LogInformation("Loading map {MapId}...", mapId);

        // Publish pre-event (cancellable)
        var loadingEvent = new MapLoadingEvent
        {
            MapId = mapId,
            MapName = mapName,
            Timestamp = GetGameTime(),
            Priority = EventPriority.Normal
        };

        if (!_eventBus.PublishCancellable(ref loadingEvent))
        {
            _logger.LogWarning("Map loading cancelled for {MapId}", mapId);
            return;
        }

        // Load map data
        var entityCount = LoadMapData(mapId);

        // Publish post-event (notification)
        _eventBus.Publish(new MapLoadedEvent
        {
            MapId = mapId,
            MapName = mapName,
            EntityCount = entityCount,
            Timestamp = GetGameTime(),
            Priority = EventPriority.Normal
        });

        _logger.LogInformation("Map {MapId} loaded successfully", mapId);
    }
}
```

## Step 8: Create First Event Handler

Create a handler to respond to events:

```csharp
using PokeSharp.Engine.Core.Events.ECS;

public class MapLoadedLogger : IEventHandler<MapLoadedEvent>
{
    private readonly ILogger<MapLoadedLogger> _logger;

    public MapLoadedLogger(ILogger<MapLoadedLogger> logger)
    {
        _logger = logger;
    }

    public EventPriority Priority => EventPriority.Low; // Low priority for logging

    public void Handle(MapLoadedEvent evt)
    {
        _logger.LogInformation(
            "📍 Map loaded: {Name} (ID: {Id}) with {Count} entities at {Time:F2}s",
            evt.MapName,
            evt.MapId,
            evt.EntityCount,
            evt.Timestamp
        );
    }
}
```

Register and subscribe:

```csharp
// In DI configuration
services.AddSingleton<IEventHandler<MapLoadedEvent>, MapLoadedLogger>();

// In startup/initialization
var eventBus = services.GetRequiredService<IEcsEventBus>();
var mapLoadedLogger = services.GetRequiredService<IEventHandler<MapLoadedEvent>>();
eventBus.Subscribe(mapLoadedLogger);
```

## Step 9: Enable Scripts to Use Events

Create an example script that uses ECS events:

```csharp
// Example script: SafeZoneChecker.cs
using PokeSharp.Engine.Core.Events.ECS;
using PokeSharp.Game.Scripting.Runtime;

public class SafeZoneChecker
{
    private IDisposable? _subscription;

    public void Initialize(ScriptContext ctx)
    {
        // Subscribe to tile entering events
        _subscription = ctx.EventBus.Subscribe<TileEnteringEvent>(evt =>
        {
            // Check if entering a safe zone tile
            var tile = ctx.Map.GetTile(evt.MapId, evt.ToPosition.X, evt.ToPosition.Y);

            if (tile?.Properties.ContainsKey("safe_zone") == true)
            {
                ctx.Logger.LogInformation("Entering safe zone - encounters disabled");
                evt.IsCancelled = false; // Allow movement
            }
        }, EventPriority.High);
    }

    public void Cleanup()
    {
        _subscription?.Dispose();
    }
}
```

## Step 10: Verify Integration

### Build the Project

```bash
dotnet build PokeSharp.Engine.Core
```

Check for source generation output:

```bash
ls obj/Debug/net8.0/generated/Arch.EventBus/
```

You should see generated files like `ArchEcsEventBus.g.cs`.

### Run Integration Tests

```csharp
[Fact]
public void EcsEventBus_PublishAndSubscribe_WorksCorrectly()
{
    // Arrange
    var eventBus = new ArchEcsEventBus();
    var receivedEvent = false;

    eventBus.Subscribe<MapLoadedEvent>(evt =>
    {
        receivedEvent = true;
        Assert.Equal(1, evt.MapId);
    });

    // Act
    eventBus.Publish(new MapLoadedEvent
    {
        MapId = 1,
        MapName = "Test Map",
        EntityCount = 10,
        Timestamp = 0f,
        Priority = EventPriority.Normal
    });

    // Assert
    Assert.True(receivedEvent);
}

[Fact]
public void CancellableEvent_CanBeCancelled()
{
    // Arrange
    var eventBus = new ArchEcsEventBus();

    eventBus.Subscribe<MapLoadingEvent>(evt =>
    {
        evt.IsCancelled = true;
    }, EventPriority.Highest);

    // Act
    var evt = new MapLoadingEvent
    {
        MapId = 1,
        MapName = "Test",
        Timestamp = 0f,
        Priority = EventPriority.Normal
    };

    var result = eventBus.PublishCancellable(ref evt);

    // Assert
    Assert.False(result);
    Assert.True(evt.IsCancelled);
}
```

## Step 11: Performance Benchmarking (Optional)

Create benchmarks to verify performance:

```csharp
using BenchmarkDotNet.Attributes;
using PokeSharp.Engine.Core.Events.ECS;

[MemoryDiagnoser]
public class EventBusBenchmarks
{
    private IEcsEventBus _eventBus = null!;

    [GlobalSetup]
    public void Setup()
    {
        _eventBus = new ArchEcsEventBus();

        // Subscribe 10 handlers
        for (int i = 0; i < 10; i++)
        {
            _eventBus.Subscribe<MapLoadedEvent>(evt => { /* no-op */ });
        }
    }

    [Benchmark]
    public void PublishMapLoadedEvent()
    {
        _eventBus.Publish(new MapLoadedEvent
        {
            MapId = 1,
            MapName = "Test",
            EntityCount = 100,
            Timestamp = 0f,
            Priority = EventPriority.Normal
        });
    }
}
```

Run benchmarks:

```bash
dotnet run -c Release --project Benchmarks.csproj
```

## Common Issues & Solutions

### Issue: Source Generation Not Working

**Symptoms**: No generated files in `obj/Generated/`

**Solution**:
1. Ensure `[Event]` attribute is on the class
2. Ensure class is marked `partial`
3. Clean and rebuild: `dotnet clean && dotnet build`
4. Check build output for generator warnings

### Issue: Handler Not Receiving Events

**Symptoms**: Events published but handlers not called

**Solution**:
1. Verify subscription: `eventBus.GetSubscriberCount<TEvent>()`
2. Check event type matches exactly (including namespace)
3. Ensure handler is subscribed before publishing
4. Check for exceptions in handler (they're caught and logged)

### Issue: IEcsEventBus Not Resolved from DI

**Symptoms**: `NullReferenceException` or DI errors

**Solution**:
1. Verify `services.AddSingleton<IEcsEventBus, ArchEcsEventBus>()` is called
2. Check DI container is built before resolving
3. Ensure no circular dependencies

### Issue: Events Causing Performance Issues

**Symptoms**: Frame drops when publishing events

**Solution**:
1. Check handler count: Too many handlers slow down dispatch
2. Avoid heavy work in handlers - queue work instead
3. Use `EventPriority.Low` for non-critical handlers
4. Profile with benchmarks to identify bottlenecks

## Next Steps

1. ✅ Integration complete - events are flowing
2. 📊 Monitor performance in real-world usage
3. 🔄 Migrate high-frequency events (movement, component changes)
4. 📝 Document event contracts for mod authors
5. 🧪 Add integration tests for all event types
6. 🚀 Consider event replay/debugging tools
7. 📈 Track metrics: event counts, handler timings

## Reference

- [Design Document](ecs-event-system-design.md)
- [Usage Examples](ecs-event-usage-examples.md)
- [Source Generation Guide](ecs-event-source-generation.md)
- [Deliverables Summary](ecs-event-deliverables-summary.md)

---

**Status**: Ready for integration
**Estimated Time**: 2-4 hours for full integration
**Testing Recommended**: Unit tests, integration tests, performance benchmarks
