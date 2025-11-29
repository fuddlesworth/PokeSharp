using Microsoft.Extensions.Logging;
using PokeSharp.Engine.Core.Events.ECS;
using PokeSharp.Engine.Core.Events.Modding;
using PokeSharp.Engine.Core.Types;
using InteractionTriggeredEvent = PokeSharp.Engine.Core.Events.ECS.InteractionTriggeredEvent;
using MapLoadedEvent = PokeSharp.Engine.Core.Events.ECS.MapLoadedEvent;
using MapUnloadedEvent = PokeSharp.Engine.Core.Events.ECS.MapUnloadedEvent;
using NpcMovedEvent = PokeSharp.Engine.Core.Events.ECS.NpcMovedEvent;
using PlayerMovedEvent = PokeSharp.Engine.Core.Events.ECS.PlayerMovedEvent;
using TileEnteredEvent = PokeSharp.Engine.Core.Events.ECS.TileEnteredEvent;

namespace PokeSharp.Engine.Core.Events;

/// <summary>
///     Bridges internal ECS events to mod-safe events.
///     Subscribes to IEcsEventBus and republishes to IModEventBus with safe wrappers.
/// </summary>
/// <remarks>
///     <para>
///         ARCHITECTURE: ECS events expose raw Entity references for internal engine use.
///         This bridge converts them to EntityId-based mod events for safety and stability.
///     </para>
///     <para>
///         Fields that don't exist in the ECS event are left at default values.
///         Mods should check for null/default where appropriate.
///     </para>
/// </remarks>
public sealed class EcsToModEventBridge : IDisposable
{
    private readonly IEcsEventBus _ecsEventBus;
    private readonly ILogger<EcsToModEventBridge>? _logger;
    private readonly IModEventBus _modEventBus;
    private readonly List<IDisposable> _subscriptions = new();

    public EcsToModEventBridge(
        IEcsEventBus ecsEventBus,
        IModEventBus modEventBus,
        ILogger<EcsToModEventBridge>? logger = null
    )
    {
        _ecsEventBus = ecsEventBus ?? throw new ArgumentNullException(nameof(ecsEventBus));
        _modEventBus = modEventBus ?? throw new ArgumentNullException(nameof(modEventBus));
        _logger = logger;

        SetupBridges();
    }

    public void Dispose()
    {
        foreach (var sub in _subscriptions) sub.Dispose();
        _subscriptions.Clear();
        _logger?.LogInformation("[EVENT] ECS→Mod event bridge disposed");
    }

    private void SetupBridges()
    {
        // Bridge PlayerMovedEvent (ECS) → PlayerMovedEvent (Mod)
        _subscriptions.Add(
            _ecsEventBus.Subscribe<PlayerMovedEvent>(
                ecsEvt =>
                {
                    _modEventBus.Publish(
                        new Modding.PlayerMovedEvent
                        {
                            EntityId = ecsEvt.Entity.Id,
                            MapId = new MapRuntimeId(0), // TODO: Get actual MapId when available
                            FromX = ecsEvt.FromPosition.X,
                            FromY = ecsEvt.FromPosition.Y,
                            ToX = ecsEvt.ToPosition.X,
                            ToY = ecsEvt.ToPosition.Y,
                            Direction = ConvertEcsDirection(ecsEvt.Direction),
                            Timestamp = ecsEvt.Timestamp
                        }
                    );
                },
                EventPriority.Lowest
            )
        );

        // Bridge NpcMovedEvent (ECS) → NpcMovedEvent (Mod)
        _subscriptions.Add(
            _ecsEventBus.Subscribe<NpcMovedEvent>(
                ecsEvt =>
                {
                    _modEventBus.Publish(
                        new Modding.NpcMovedEvent
                        {
                            EntityId = ecsEvt.Entity.Id,
                            NpcId = ecsEvt.NpcId,
                            MapId = new MapRuntimeId(0), // TODO: Get actual MapId when available
                            FromX = ecsEvt.FromPosition.X,
                            FromY = ecsEvt.FromPosition.Y,
                            ToX = ecsEvt.ToPosition.X,
                            ToY = ecsEvt.ToPosition.Y,
                            Direction = ConvertEcsDirection(ecsEvt.Direction),
                            Timestamp = ecsEvt.Timestamp
                        }
                    );
                },
                EventPriority.Lowest
            )
        );

        // Bridge InteractionTriggeredEvent (ECS) → InteractionTriggeredEvent (Mod)
        _subscriptions.Add(
            _ecsEventBus.Subscribe<InteractionTriggeredEvent>(
                ecsEvt =>
                {
                    _modEventBus.Publish(
                        new Modding.InteractionTriggeredEvent
                        {
                            SourceEntityId = ecsEvt.Initiator.Id,
                            TargetEntityId = ecsEvt.Target.Id,
                            InteractionType = ecsEvt.InteractionType,
                            TargetScriptId = null, // Not available in ECS event
                            MapId = new MapRuntimeId(0), // TODO: Get actual MapId when available
                            Result = null, // Not available in ECS event
                            Timestamp = ecsEvt.Timestamp
                        }
                    );
                },
                EventPriority.Lowest
            )
        );

        // Bridge MapUnloadedEvent (ECS) → MapUnloadedEvent (Mod)
        _subscriptions.Add(
            _ecsEventBus.Subscribe<MapUnloadedEvent>(
                ecsEvt =>
                {
                    _modEventBus.Publish(
                        new Modding.MapUnloadedEvent
                        {
                            MapId = ecsEvt.MapName ?? ecsEvt.MapId.ToString(),
                            RuntimeId = new MapRuntimeId(ecsEvt.MapId),
                            Timestamp = ecsEvt.Timestamp
                        }
                    );
                },
                EventPriority.Lowest
            )
        );

        // Bridge EncounterTriggeredEvent (ECS) → WildEncounterTriggeredEvent (Mod)
        _subscriptions.Add(
            _ecsEventBus.Subscribe<EncounterTriggeredEvent>(
                ecsEvt =>
                {
                    _modEventBus.Publish(
                        new WildEncounterTriggeredEvent
                        {
                            PlayerEntityId = ecsEvt.PlayerEntity.Id,
                            MapId = new MapRuntimeId(ecsEvt.MapId),
                            TileX = ecsEvt.Position.X,
                            TileY = ecsEvt.Position.Y,
                            EncounterZoneId = ecsEvt.EncounterType, // Use EncounterType as zone ID
                            WildSpeciesId = "unknown", // Not available in ECS event
                            WildLevel = 0, // Not available in ECS event
                            IsShiny = false, // Not available in ECS event
                            Timestamp = ecsEvt.Timestamp
                        }
                    );
                },
                EventPriority.Lowest
            )
        );

        // Bridge TileEnteredEvent (ECS) → TileEnteredEvent (Mod)
        // ECS version uses Entity + tuples, Mod version uses EntityId + separate fields
        _subscriptions.Add(
            _ecsEventBus.Subscribe<TileEnteredEvent>(
                ecsEvt =>
                {
                    _modEventBus.Publish(
                        new Modding.TileEnteredEvent
                        {
                            EntityId = ecsEvt.Entity.Id,
                            IsPlayer =
                                false, // TODO: Check if entity has Player component when World access is available
                            MapId = new MapRuntimeId(ecsEvt.MapId),
                            FromX = ecsEvt.FromPosition.X,
                            FromY = ecsEvt.FromPosition.Y,
                            ToX = ecsEvt.ToPosition.X,
                            ToY = ecsEvt.ToPosition.Y,
                            Direction = 0, // Not available in ECS event - would need to calculate from delta
                            TileBehavior = null, // Not available in ECS event
                            TerrainType = null, // Not available in ECS event
                            Timestamp = ecsEvt.Timestamp
                        }
                    );
                },
                EventPriority.Lowest
            )
        ); // Run after all internal handlers

        // Bridge MapLoadedEvent (ECS) → MapLoadedEvent (Mod)
        _subscriptions.Add(
            _ecsEventBus.Subscribe<MapLoadedEvent>(
                ecsEvt =>
                {
                    _modEventBus.Publish(
                        new Modding.MapLoadedEvent
                        {
                            MapId = ecsEvt.MapName ?? ecsEvt.MapId.ToString(),
                            RuntimeId = new MapRuntimeId(ecsEvt.MapId),
                            TileCount = 0, // Not available in ECS event
                            EntityCount = 0, // Not available in ECS event
                            Width = 0, // Not available in ECS event
                            Height = 0, // Not available in ECS event
                            WorldOffsetX = ecsEvt.WorldOffsetX,
                            WorldOffsetY = ecsEvt.WorldOffsetY,
                            LoadTimeMs = 0f, // Not available in ECS event
                            Timestamp = ecsEvt.Timestamp
                        }
                    );
                },
                EventPriority.Lowest
            )
        );

        // Bridge EntityCreatedEvent → EntitySpawnedEvent (safe wrapper)
        _subscriptions.Add(
            _ecsEventBus.Subscribe<EntityCreatedEvent>(
                ecsEvt =>
                {
                    _modEventBus.Publish(
                        new EntitySpawnedEvent
                        {
                            EntityId = ecsEvt.Entity.Id,
                            Archetype = ecsEvt.ArchetypeDescription ?? "unknown",
                            Timestamp = ecsEvt.Timestamp
                        }
                    );
                },
                EventPriority.Lowest
            )
        );

        // Bridge EntityDestroyedEvent → EntityDespawnedEvent (safe wrapper)
        _subscriptions.Add(
            _ecsEventBus.Subscribe<EntityDestroyedEvent>(
                ecsEvt =>
                {
                    _modEventBus.Publish(
                        new EntityDespawnedEvent
                        {
                            EntityId = ecsEvt.Entity.Id,
                            Archetype = "unknown",
                            Reason = ecsEvt.Reason ?? "destroyed",
                            Timestamp = ecsEvt.Timestamp
                        }
                    );
                },
                EventPriority.Lowest
            )
        );

        _logger?.LogInformation(
            "[EVENT] ECS→Mod event bridge initialized with {Count} bridges",
            _subscriptions.Count
        );
    }

    /// <summary>
    ///     Converts ECS direction enum to mod-safe direction integer.
    /// </summary>
    /// <param name="direction">ECS direction enum.</param>
    /// <returns>Integer direction (0=Down, 1=Up, 2=Left, 3=Right).</returns>
    private static int ConvertEcsDirection(EcsDirection direction)
    {
        return direction switch
        {
            EcsDirection.South => 0, // Down
            EcsDirection.North => 1, // Up
            EcsDirection.West => 2, // Left
            EcsDirection.East => 3, // Right
            _ => 0 // Default to Down for None or diagonal directions
        };
    }
}