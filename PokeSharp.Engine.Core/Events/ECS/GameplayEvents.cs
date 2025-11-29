using Arch.Core;

namespace PokeSharp.Engine.Core.Events.ECS;

#region Movement Events

/// <summary>
///     Event published BEFORE the player moves.
///     Handlers can cancel movement by setting IsCancelled = true.
/// </summary>
public struct PlayerMovingEvent : ICancellableEvent
{
    /// <summary>
    ///     The player entity.
    /// </summary>
    public required Entity Entity { get; init; }

    /// <summary>
    ///     The current position.
    /// </summary>
    public required (int X, int Y) FromPosition { get; init; }

    /// <summary>
    ///     The target position.
    /// </summary>
    public required (int X, int Y) ToPosition { get; init; }

    /// <summary>
    ///     The direction of movement.
    /// </summary>
    public required EcsDirection Direction { get; init; }

    /// <inheritdoc />
    public required float Timestamp { get; init; }

    /// <inheritdoc />
    public required EventPriority Priority { get; init; }

    /// <inheritdoc />
    public bool IsCancelled { get; set; }
}

/// <summary>
///     Event published AFTER the player successfully moves.
/// </summary>
public readonly struct PlayerMovedEvent : IEcsEvent
{
    /// <summary>
    ///     The player entity.
    /// </summary>
    public required Entity Entity { get; init; }

    /// <summary>
    ///     The previous position.
    /// </summary>
    public required (int X, int Y) FromPosition { get; init; }

    /// <summary>
    ///     The new position.
    /// </summary>
    public required (int X, int Y) ToPosition { get; init; }

    /// <summary>
    ///     The direction of movement.
    /// </summary>
    public required EcsDirection Direction { get; init; }

    /// <inheritdoc />
    public required float Timestamp { get; init; }

    /// <inheritdoc />
    public required EventPriority Priority { get; init; }
}

/// <summary>
///     Event published BEFORE an NPC moves.
///     Handlers can cancel movement by setting IsCancelled = true.
/// </summary>
public struct NpcMovingEvent : ICancellableEvent
{
    /// <summary>
    ///     The NPC entity.
    /// </summary>
    public required Entity Entity { get; init; }

    /// <summary>
    ///     The current position.
    /// </summary>
    public required (int X, int Y) FromPosition { get; init; }

    /// <summary>
    ///     The target position.
    /// </summary>
    public required (int X, int Y) ToPosition { get; init; }

    /// <summary>
    ///     The direction of movement.
    /// </summary>
    public required EcsDirection Direction { get; init; }

    /// <summary>
    ///     Optional NPC identifier for debugging.
    /// </summary>
    public string? NpcId { get; init; }

    /// <inheritdoc />
    public required float Timestamp { get; init; }

    /// <inheritdoc />
    public required EventPriority Priority { get; init; }

    /// <inheritdoc />
    public bool IsCancelled { get; set; }
}

/// <summary>
///     Event published AFTER an NPC successfully moves.
/// </summary>
public readonly struct NpcMovedEvent : IEcsEvent
{
    /// <summary>
    ///     The NPC entity.
    /// </summary>
    public required Entity Entity { get; init; }

    /// <summary>
    ///     The previous position.
    /// </summary>
    public required (int X, int Y) FromPosition { get; init; }

    /// <summary>
    ///     The new position.
    /// </summary>
    public required (int X, int Y) ToPosition { get; init; }

    /// <summary>
    ///     The direction of movement.
    /// </summary>
    public required EcsDirection Direction { get; init; }

    /// <summary>
    ///     Optional NPC identifier.
    /// </summary>
    public string? NpcId { get; init; }

    /// <inheritdoc />
    public required float Timestamp { get; init; }

    /// <inheritdoc />
    public required EventPriority Priority { get; init; }
}

/// <summary>
///     Represents cardinal and intercardinal directions for ECS events.
/// </summary>
/// <remarks>
///     Named EcsDirection to avoid conflicts with game-specific Direction enums.
/// </remarks>
public enum EcsDirection : byte
{
    None = 0,
    North = 1,
    South = 2,
    East = 3,
    West = 4,
    NorthEast = 5,
    NorthWest = 6,
    SouthEast = 7,
    SouthWest = 8
}

#endregion

#region Interaction Events

/// <summary>
///     Event published BEFORE an interaction is triggered.
///     Handlers can cancel by setting IsCancelled = true.
/// </summary>
public struct InteractionTriggeringEvent : ICancellableEvent
{
    /// <summary>
    ///     The entity initiating the interaction (usually player).
    /// </summary>
    public required Entity Initiator { get; init; }

    /// <summary>
    ///     The entity being interacted with.
    /// </summary>
    public required Entity Target { get; init; }

    /// <summary>
    ///     The type of interaction (e.g., "talk", "examine", "use").
    /// </summary>
    public required string InteractionType { get; init; }

    /// <inheritdoc />
    public required float Timestamp { get; init; }

    /// <inheritdoc />
    public required EventPriority Priority { get; init; }

    /// <inheritdoc />
    public bool IsCancelled { get; set; }
}

/// <summary>
///     Event published AFTER an interaction is successfully triggered.
/// </summary>
public readonly struct InteractionTriggeredEvent : IEcsEvent
{
    /// <summary>
    ///     The entity that initiated the interaction.
    /// </summary>
    public required Entity Initiator { get; init; }

    /// <summary>
    ///     The entity that was interacted with.
    /// </summary>
    public required Entity Target { get; init; }

    /// <summary>
    ///     The type of interaction.
    /// </summary>
    public required string InteractionType { get; init; }

    /// <inheritdoc />
    public required float Timestamp { get; init; }

    /// <inheritdoc />
    public required EventPriority Priority { get; init; }
}

#endregion

#region Encounter Events

/// <summary>
///     Event published BEFORE a random encounter is triggered.
///     Handlers can cancel by setting IsCancelled = true.
/// </summary>
public struct EncounterTriggeringEvent : ICancellableEvent
{
    /// <summary>
    ///     The player entity.
    /// </summary>
    public required Entity PlayerEntity { get; init; }

    /// <summary>
    ///     The position where the encounter triggered.
    /// </summary>
    public required (int X, int Y) Position { get; init; }

    /// <summary>
    ///     The map ID where the encounter occurred.
    /// </summary>
    public required int MapId { get; init; }

    /// <summary>
    ///     Encounter type (e.g., "grass", "water", "cave").
    /// </summary>
    public required string EncounterType { get; init; }

    /// <inheritdoc />
    public required float Timestamp { get; init; }

    /// <inheritdoc />
    public required EventPriority Priority { get; init; }

    /// <inheritdoc />
    public bool IsCancelled { get; set; }
}

/// <summary>
///     Event published AFTER an encounter is successfully triggered.
/// </summary>
public readonly struct EncounterTriggeredEvent : IEcsEvent
{
    /// <summary>
    ///     The player entity.
    /// </summary>
    public required Entity PlayerEntity { get; init; }

    /// <summary>
    ///     The position where the encounter triggered.
    /// </summary>
    public required (int X, int Y) Position { get; init; }

    /// <summary>
    ///     The map ID where the encounter occurred.
    /// </summary>
    public required int MapId { get; init; }

    /// <summary>
    ///     Encounter type.
    /// </summary>
    public required string EncounterType { get; init; }

    /// <summary>
    ///     Optional encounter entity if spawned.
    /// </summary>
    public Entity? EncounterEntity { get; init; }

    /// <inheritdoc />
    public required float Timestamp { get; init; }

    /// <inheritdoc />
    public required EventPriority Priority { get; init; }
}

#endregion

#region Battle Events (Future)

/// <summary>
///     Event published BEFORE a battle starts.
///     Handlers can cancel by setting IsCancelled = true.
/// </summary>
public struct BattleStartingEvent : ICancellableEvent
{
    /// <summary>
    ///     The player entity entering battle.
    /// </summary>
    public required Entity PlayerEntity { get; init; }

    /// <summary>
    ///     The opponent entity (NPC or wild Pokemon).
    /// </summary>
    public required Entity OpponentEntity { get; init; }

    /// <summary>
    ///     Battle type (e.g., "wild", "trainer", "gym").
    /// </summary>
    public required string BattleType { get; init; }

    /// <inheritdoc />
    public required float Timestamp { get; init; }

    /// <inheritdoc />
    public required EventPriority Priority { get; init; }

    /// <inheritdoc />
    public bool IsCancelled { get; set; }
}

/// <summary>
///     Event published AFTER a battle successfully starts.
/// </summary>
public readonly struct BattleStartedEvent : IEcsEvent
{
    /// <summary>
    ///     The player entity in battle.
    /// </summary>
    public required Entity PlayerEntity { get; init; }

    /// <summary>
    ///     The opponent entity.
    /// </summary>
    public required Entity OpponentEntity { get; init; }

    /// <summary>
    ///     Battle type.
    /// </summary>
    public required string BattleType { get; init; }

    /// <summary>
    ///     Optional battle ID for tracking.
    /// </summary>
    public int BattleId { get; init; }

    /// <inheritdoc />
    public required float Timestamp { get; init; }

    /// <inheritdoc />
    public required EventPriority Priority { get; init; }
}

/// <summary>
///     Event published BEFORE a battle ends.
///     Handlers can cancel by setting IsCancelled = true (rarely used).
/// </summary>
public struct BattleEndingEvent : ICancellableEvent
{
    /// <summary>
    ///     The player entity.
    /// </summary>
    public required Entity PlayerEntity { get; init; }

    /// <summary>
    ///     The opponent entity.
    /// </summary>
    public required Entity OpponentEntity { get; init; }

    /// <summary>
    ///     Battle outcome (e.g., "victory", "defeat", "flee").
    /// </summary>
    public required string Outcome { get; init; }

    /// <summary>
    ///     Battle ID.
    /// </summary>
    public int BattleId { get; init; }

    /// <inheritdoc />
    public required float Timestamp { get; init; }

    /// <inheritdoc />
    public required EventPriority Priority { get; init; }

    /// <inheritdoc />
    public bool IsCancelled { get; set; }
}

/// <summary>
///     Event published AFTER a battle ends.
/// </summary>
public readonly struct BattleEndedEvent : IEcsEvent
{
    /// <summary>
    ///     The player entity.
    /// </summary>
    public required Entity PlayerEntity { get; init; }

    /// <summary>
    ///     The opponent entity.
    /// </summary>
    public required Entity OpponentEntity { get; init; }

    /// <summary>
    ///     Battle outcome.
    /// </summary>
    public required string Outcome { get; init; }

    /// <summary>
    ///     Battle ID.
    /// </summary>
    public int BattleId { get; init; }

    /// <summary>
    ///     Experience gained by player.
    /// </summary>
    public int ExperienceGained { get; init; }

    /// <summary>
    ///     Money gained/lost.
    /// </summary>
    public int MoneyGained { get; init; }

    /// <inheritdoc />
    public required float Timestamp { get; init; }

    /// <inheritdoc />
    public required EventPriority Priority { get; init; }
}

#endregion

#region Tile Events

/// <summary>
///     Event published when an entity enters a new tile.
/// </summary>
public readonly struct TileEnteredEvent : IEcsEvent
{
    /// <summary>
    ///     The entity that entered the tile.
    /// </summary>
    public required Entity Entity { get; init; }

    /// <summary>
    ///     The previous position as (X, Y) tile coordinates.
    /// </summary>
    public required (int X, int Y) FromPosition { get; init; }

    /// <summary>
    ///     The new position as (X, Y) tile coordinates.
    /// </summary>
    public required (int X, int Y) ToPosition { get; init; }

    /// <summary>
    ///     The map ID where the tile transition occurred.
    /// </summary>
    public required int MapId { get; init; }

    /// <inheritdoc />
    public required float Timestamp { get; init; }

    /// <inheritdoc />
    public required EventPriority Priority { get; init; }
}

#endregion

#region Map Events

/// <summary>
///     Event published BEFORE a map starts loading.
///     Handlers can cancel by setting IsCancelled = true.
/// </summary>
public struct MapLoadingEvent : ICancellableEvent
{
    /// <summary>
    ///     The map ID being loaded.
    /// </summary>
    public required int MapId { get; init; }

    /// <summary>
    ///     The name of the map being loaded.
    /// </summary>
    public required string MapName { get; init; }

    /// <inheritdoc />
    public required float Timestamp { get; init; }

    /// <inheritdoc />
    public required EventPriority Priority { get; init; }

    /// <inheritdoc />
    public bool IsCancelled { get; set; }
}

/// <summary>
///     Event published AFTER a map successfully loads.
/// </summary>
public readonly struct MapLoadedEvent : IEcsEvent
{
    /// <summary>
    ///     The map ID that was loaded.
    /// </summary>
    public required int MapId { get; init; }

    /// <summary>
    ///     The name of the loaded map.
    /// </summary>
    public required string MapName { get; init; }

    /// <summary>
    ///     The world offset X coordinate of the loaded map.
    /// </summary>
    public required int WorldOffsetX { get; init; }

    /// <summary>
    ///     The world offset Y coordinate of the loaded map.
    /// </summary>
    public required int WorldOffsetY { get; init; }

    /// <inheritdoc />
    public required float Timestamp { get; init; }

    /// <inheritdoc />
    public required EventPriority Priority { get; init; }
}

/// <summary>
///     Event published BEFORE a map unloads.
///     Handlers can cancel by setting IsCancelled = true.
/// </summary>
public struct MapUnloadingEvent : ICancellableEvent
{
    /// <summary>
    ///     The map ID being unloaded.
    /// </summary>
    public required int MapId { get; init; }

    /// <summary>
    ///     The name of the map being unloaded.
    /// </summary>
    public required string MapName { get; init; }

    /// <inheritdoc />
    public required float Timestamp { get; init; }

    /// <inheritdoc />
    public required EventPriority Priority { get; init; }

    /// <inheritdoc />
    public bool IsCancelled { get; set; }
}

/// <summary>
///     Event published AFTER a map unloads.
/// </summary>
public readonly struct MapUnloadedEvent : IEcsEvent
{
    /// <summary>
    ///     The map ID that was unloaded.
    /// </summary>
    public required int MapId { get; init; }

    /// <summary>
    ///     The name of the unloaded map.
    /// </summary>
    public required string MapName { get; init; }

    /// <inheritdoc />
    public required float Timestamp { get; init; }

    /// <inheritdoc />
    public required EventPriority Priority { get; init; }
}

#endregion