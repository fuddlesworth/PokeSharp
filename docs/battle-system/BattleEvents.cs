namespace PokeSharp.Game.Events.Battle;

/// <summary>
///     Base interface for all battle events.
/// </summary>
public interface IBattleEvent
{
    /// <summary>
    ///     Timestamp when the event occurred.
    /// </summary>
    DateTime Timestamp { get; }
}

/// <summary>
///     Event fired when a battle starts.
/// </summary>
public record BattleStartedEvent : IBattleEvent
{
    public required string BattleId { get; init; }
    public required byte BattleType { get; init; }
    public required byte BattleFormat { get; init; }
    public required string PlayerTrainerId { get; init; }
    public required string OpponentTrainerId { get; init; }
    public DateTime Timestamp { get; init; } = DateTime.UtcNow;
}

/// <summary>
///     Event fired when a battle ends.
/// </summary>
public record BattleEndedEvent : IBattleEvent
{
    public required string BattleId { get; init; }
    public required byte Winner { get; init; } // 0 = player, 1 = opponent, 2 = draw
    public required ushort TurnCount { get; init; }
    public required uint ExperienceGained { get; init; }
    public required uint MoneyGained { get; init; }
    public DateTime Timestamp { get; init; } = DateTime.UtcNow;
}

/// <summary>
///     Event fired when a new turn begins.
/// </summary>
public record TurnStartedEvent : IBattleEvent
{
    public required string BattleId { get; init; }
    public required ushort TurnNumber { get; init; }
    public DateTime Timestamp { get; init; } = DateTime.UtcNow;
}

/// <summary>
///     Event fired when a Pokemon uses a move.
/// </summary>
public record MoveUsedEvent : IBattleEvent
{
    public required string BattleId { get; init; }
    public required int AttackerEntityId { get; init; }
    public required string AttackerName { get; init; }
    public required ushort MoveId { get; init; }
    public required string MoveName { get; init; }
    public required int? TargetEntityId { get; init; }
    public DateTime Timestamp { get; init; } = DateTime.UtcNow;
}

/// <summary>
///     Event fired when a move hits and deals damage.
/// </summary>
public record DamageDealtEvent : IBattleEvent
{
    public required string BattleId { get; init; }
    public required int AttackerEntityId { get; init; }
    public required int DefenderEntityId { get; init; }
    public required ushort DamageAmount { get; init; }
    public required ushort RemainingHp { get; init; }
    public required float Effectiveness { get; init; } // 0, 0.5, 1, 2, 4
    public required bool IsCritical { get; init; }
    public DateTime Timestamp { get; init; } = DateTime.UtcNow;
}

/// <summary>
///     Event fired when a move misses.
/// </summary>
public record MoveMissedEvent : IBattleEvent
{
    public required string BattleId { get; init; }
    public required int AttackerEntityId { get; init; }
    public required int DefenderEntityId { get; init; }
    public required ushort MoveId { get; init; }
    public DateTime Timestamp { get; init; } = DateTime.UtcNow;
}

/// <summary>
///     Event fired when a status condition is inflicted.
/// </summary>
public record StatusInflictedEvent : IBattleEvent
{
    public required string BattleId { get; init; }
    public required int TargetEntityId { get; init; }
    public required string TargetName { get; init; }
    public required byte StatusCondition { get; init; }
    public required int? InflictedByEntityId { get; init; }
    public DateTime Timestamp { get; init; } = DateTime.UtcNow;
}

/// <summary>
///     Event fired when a status condition is cured.
/// </summary>
public record StatusCuredEvent : IBattleEvent
{
    public required string BattleId { get; init; }
    public required int TargetEntityId { get; init; }
    public required string TargetName { get; init; }
    public required byte StatusCondition { get; init; }
    public DateTime Timestamp { get; init; } = DateTime.UtcNow;
}

/// <summary>
///     Event fired when a Pokemon's stats are modified.
/// </summary>
public record StatChangedEvent : IBattleEvent
{
    public required string BattleId { get; init; }
    public required int TargetEntityId { get; init; }
    public required string TargetName { get; init; }
    public required string StatName { get; init; } // "Attack", "Defense", etc.
    public required sbyte StageChange { get; init; } // -6 to +6
    public required sbyte NewStage { get; init; }
    public DateTime Timestamp { get; init; } = DateTime.UtcNow;
}

/// <summary>
///     Event fired when a Pokemon switches in.
/// </summary>
public record PokemonSwitchedInEvent : IBattleEvent
{
    public required string BattleId { get; init; }
    public required int PokemonEntityId { get; init; }
    public required string PokemonName { get; init; }
    public required byte Side { get; init; } // 0 = player, 1 = opponent
    public required byte BattleSlot { get; init; }
    public DateTime Timestamp { get; init; } = DateTime.UtcNow;
}

/// <summary>
///     Event fired when a Pokemon switches out.
/// </summary>
public record PokemonSwitchedOutEvent : IBattleEvent
{
    public required string BattleId { get; init; }
    public required int PokemonEntityId { get; init; }
    public required string PokemonName { get; init; }
    public required byte Side { get; init; }
    public DateTime Timestamp { get; init; } = DateTime.UtcNow;
}

/// <summary>
///     Event fired when a Pokemon faints.
/// </summary>
public record PokemonFaintedEvent : IBattleEvent
{
    public required string BattleId { get; init; }
    public required int PokemonEntityId { get; init; }
    public required string PokemonName { get; init; }
    public required byte Side { get; init; }
    public DateTime Timestamp { get; init; } = DateTime.UtcNow;
}

/// <summary>
///     Event fired when weather changes.
/// </summary>
public record WeatherChangedEvent : IBattleEvent
{
    public required string BattleId { get; init; }
    public required byte OldWeather { get; init; }
    public required byte NewWeather { get; init; }
    public required byte Duration { get; init; } // 0 = infinite
    public DateTime Timestamp { get; init; } = DateTime.UtcNow;
}

/// <summary>
///     Event fired when terrain changes.
/// </summary>
public record TerrainChangedEvent : IBattleEvent
{
    public required string BattleId { get; init; }
    public required byte OldTerrain { get; init; }
    public required byte NewTerrain { get; init; }
    public required byte Duration { get; init; }
    public DateTime Timestamp { get; init; } = DateTime.UtcNow;
}

/// <summary>
///     Event fired when a field effect is activated.
/// </summary>
public record FieldEffectActivatedEvent : IBattleEvent
{
    public required string BattleId { get; init; }
    public required string EffectName { get; init; } // "TrickRoom", "Gravity", etc.
    public required byte Duration { get; init; }
    public DateTime Timestamp { get; init; } = DateTime.UtcNow;
}

/// <summary>
///     Event fired when a field effect ends.
/// </summary>
public record FieldEffectEndedEvent : IBattleEvent
{
    public required string BattleId { get; init; }
    public required string EffectName { get; init; }
    public DateTime Timestamp { get; init; } = DateTime.UtcNow;
}

/// <summary>
///     Event fired when entry hazards are set.
/// </summary>
public record HazardSetEvent : IBattleEvent
{
    public required string BattleId { get; init; }
    public required byte Side { get; init; }
    public required string HazardType { get; init; } // "Spikes", "StealthRock", etc.
    public required byte Layers { get; init; }
    public DateTime Timestamp { get; init; } = DateTime.UtcNow;
}

/// <summary>
///     Event fired when entry hazards are cleared.
/// </summary>
public record HazardClearedEvent : IBattleEvent
{
    public required string BattleId { get; init; }
    public required byte Side { get; init; }
    public required string HazardType { get; init; }
    public DateTime Timestamp { get; init; } = DateTime.UtcNow;
}

/// <summary>
///     Event fired when an ability activates.
/// </summary>
public record AbilityActivatedEvent : IBattleEvent
{
    public required string BattleId { get; init; }
    public required int PokemonEntityId { get; init; }
    public required string PokemonName { get; init; }
    public required ushort AbilityId { get; init; }
    public required string AbilityName { get; init; }
    public DateTime Timestamp { get; init; } = DateTime.UtcNow;
}

/// <summary>
///     Event fired when an item is used in battle.
/// </summary>
public record ItemUsedEvent : IBattleEvent
{
    public required string BattleId { get; init; }
    public required int? UserEntityId { get; init; } // Null for trainer-used items
    public required ushort ItemId { get; init; }
    public required string ItemName { get; init; }
    public required int? TargetEntityId { get; init; }
    public DateTime Timestamp { get; init; } = DateTime.UtcNow;
}

/// <summary>
///     Event fired when experience is gained.
/// </summary>
public record ExperienceGainedEvent : IBattleEvent
{
    public required string BattleId { get; init; }
    public required int PokemonEntityId { get; init; }
    public required string PokemonName { get; init; }
    public required uint ExperienceGained { get; init; }
    public required uint NewTotalExperience { get; init; }
    public DateTime Timestamp { get; init; } = DateTime.UtcNow;
}

/// <summary>
///     Event fired when a Pokemon levels up.
/// </summary>
public record LevelUpEvent : IBattleEvent
{
    public required string BattleId { get; init; }
    public required int PokemonEntityId { get; init; }
    public required string PokemonName { get; init; }
    public required byte OldLevel { get; init; }
    public required byte NewLevel { get; init; }
    public DateTime Timestamp { get; init; } = DateTime.UtcNow;
}

/// <summary>
///     Event fired when a Pokemon learns a new move.
/// </summary>
public record MoveLearnedEvent : IBattleEvent
{
    public required int PokemonEntityId { get; init; }
    public required string PokemonName { get; init; }
    public required ushort MoveId { get; init; }
    public required string MoveName { get; init; }
    public required byte? ReplacedMoveSlot { get; init; } // Null if learned to empty slot
    public DateTime Timestamp { get; init; } = DateTime.UtcNow;
}

/// <summary>
///     Event fired when a Pokemon attempts to flee.
/// </summary>
public record FleeAttemptedEvent : IBattleEvent
{
    public required string BattleId { get; init; }
    public required bool Success { get; init; }
    public required byte AttemptNumber { get; init; }
    public DateTime Timestamp { get; init; } = DateTime.UtcNow;
}

/// <summary>
///     Event fired for battle messages/dialogue.
/// </summary>
public record BattleMessageEvent : IBattleEvent
{
    public required string BattleId { get; init; }
    public required string Message { get; init; }
    public required byte MessageType { get; init; } // 0 = info, 1 = warning, 2 = critical
    public DateTime Timestamp { get; init; } = DateTime.UtcNow;
}
