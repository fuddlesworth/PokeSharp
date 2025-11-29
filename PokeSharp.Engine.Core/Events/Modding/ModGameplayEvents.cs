using PokeSharp.Engine.Core.Types;

namespace PokeSharp.Engine.Core.Events.Modding;

// ============================================================================
// MOVEMENT EVENTS - Full access for mods
// ============================================================================

/// <summary>
///     Published AFTER the player successfully moves.
/// </summary>
public readonly struct PlayerMovedEvent
{
    /// <summary>Player entity ID.</summary>
    public required int EntityId { get; init; }

    /// <summary>Map runtime ID.</summary>
    public required MapRuntimeId MapId { get; init; }

    /// <summary>Previous tile X position.</summary>
    public required int FromX { get; init; }

    /// <summary>Previous tile Y position.</summary>
    public required int FromY { get; init; }

    /// <summary>New tile X position.</summary>
    public required int ToX { get; init; }

    /// <summary>New tile Y position.</summary>
    public required int ToY { get; init; }

    /// <summary>Movement direction (0=Down, 1=Up, 2=Left, 3=Right).</summary>
    public required int Direction { get; init; }

    /// <summary>Game timestamp.</summary>
    public required float Timestamp { get; init; }
}

/// <summary>
///     Published AFTER an NPC successfully moves.
/// </summary>
public readonly struct NpcMovedEvent
{
    /// <summary>NPC entity ID.</summary>
    public required int EntityId { get; init; }

    /// <summary>Optional NPC identifier for debugging.</summary>
    public string? NpcId { get; init; }

    /// <summary>Map runtime ID.</summary>
    public required MapRuntimeId MapId { get; init; }

    /// <summary>Previous tile X position.</summary>
    public required int FromX { get; init; }

    /// <summary>Previous tile Y position.</summary>
    public required int FromY { get; init; }

    /// <summary>New tile X position.</summary>
    public required int ToX { get; init; }

    /// <summary>New tile Y position.</summary>
    public required int ToY { get; init; }

    /// <summary>Movement direction (0=Down, 1=Up, 2=Left, 3=Right).</summary>
    public required int Direction { get; init; }

    /// <summary>Game timestamp.</summary>
    public required float Timestamp { get; init; }
}

/// <summary>
///     Published BEFORE an entity moves to a new tile. Cancellable.
/// </summary>
public struct TileEnteringEvent : IModCancellableEvent
{
    /// <summary>Entity ID of the moving entity.</summary>
    public required int EntityId { get; init; }

    /// <summary>Whether this is the player entity.</summary>
    public required bool IsPlayer { get; init; }

    /// <summary>Map runtime ID.</summary>
    public required MapRuntimeId MapId { get; init; }

    /// <summary>Current tile X position.</summary>
    public required int FromX { get; init; }

    /// <summary>Current tile Y position.</summary>
    public required int FromY { get; init; }

    /// <summary>Target tile X position.</summary>
    public required int ToX { get; init; }

    /// <summary>Target tile Y position.</summary>
    public required int ToY { get; init; }

    /// <summary>Movement direction (0=Down, 1=Up, 2=Left, 3=Right).</summary>
    public required int Direction { get; init; }

    /// <summary>Whether the entity is running.</summary>
    public bool IsRunning { get; init; }

    /// <summary>Whether this is a jump (ledge).</summary>
    public bool IsJump { get; init; }

    /// <summary>Game timestamp.</summary>
    public required float Timestamp { get; init; }

    // IModCancellableEvent implementation
    public bool IsCancelled { get; set; }
    public string? CancelledBy { get; set; }
    public string? CancelReason { get; set; }
}

/// <summary>
///     Published AFTER an entity successfully moved to a new tile.
/// </summary>
public readonly struct TileEnteredEvent
{
    /// <summary>Entity ID of the moved entity.</summary>
    public required int EntityId { get; init; }

    /// <summary>Whether this is the player entity.</summary>
    public required bool IsPlayer { get; init; }

    /// <summary>Map runtime ID.</summary>
    public required MapRuntimeId MapId { get; init; }

    /// <summary>Previous tile X position.</summary>
    public required int FromX { get; init; }

    /// <summary>Previous tile Y position.</summary>
    public required int FromY { get; init; }

    /// <summary>New tile X position.</summary>
    public required int ToX { get; init; }

    /// <summary>New tile Y position.</summary>
    public required int ToY { get; init; }

    /// <summary>Movement direction.</summary>
    public required int Direction { get; init; }

    /// <summary>Tile behavior ID at the new position (e.g., "tall_grass", "water").</summary>
    public string? TileBehavior { get; init; }

    /// <summary>Terrain type at the new position.</summary>
    public string? TerrainType { get; init; }

    /// <summary>Game timestamp.</summary>
    public required float Timestamp { get; init; }
}

/// <summary>
///     Published when an entity exits a tile (before entering the next one).
/// </summary>
public readonly struct TileExitedEvent
{
    public required int EntityId { get; init; }
    public required bool IsPlayer { get; init; }
    public required MapRuntimeId MapId { get; init; }
    public required int X { get; init; }
    public required int Y { get; init; }
    public required int Direction { get; init; }
    public required float Timestamp { get; init; }
}

// ============================================================================
// ENCOUNTER EVENTS - Full access for mods
// ============================================================================

/// <summary>
///     Published BEFORE a wild encounter is triggered. Cancellable.
/// </summary>
public struct WildEncounterTriggeringEvent : IModCancellableEvent
{
    /// <summary>Player entity ID.</summary>
    public required int PlayerEntityId { get; init; }

    /// <summary>Map where encounter occurs.</summary>
    public required MapRuntimeId MapId { get; init; }

    /// <summary>Tile X position.</summary>
    public required int TileX { get; init; }

    /// <summary>Tile Y position.</summary>
    public required int TileY { get; init; }

    /// <summary>Encounter zone ID (e.g., "route101_grass").</summary>
    public required string EncounterZoneId { get; init; }

    /// <summary>Terrain type triggering the encounter.</summary>
    public required string TerrainType { get; init; }

    /// <summary>Base encounter rate (0-100).</summary>
    public required int BaseEncounterRate { get; init; }

    /// <summary>Calculated encounter rate after modifiers.</summary>
    public int EffectiveEncounterRate { get; set; }

    /// <summary>Random roll result (0-100).</summary>
    public required int RandomRoll { get; init; }

    /// <summary>Game timestamp.</summary>
    public required float Timestamp { get; init; }

    // IModCancellableEvent
    public bool IsCancelled { get; set; }
    public string? CancelledBy { get; set; }
    public string? CancelReason { get; set; }
}

/// <summary>
///     Published AFTER a wild encounter is confirmed (battle will start).
/// </summary>
public readonly struct WildEncounterTriggeredEvent
{
    public required int PlayerEntityId { get; init; }
    public required MapRuntimeId MapId { get; init; }
    public required int TileX { get; init; }
    public required int TileY { get; init; }
    public required string EncounterZoneId { get; init; }

    /// <summary>Species ID of the wild Pokemon.</summary>
    public required string WildSpeciesId { get; init; }

    /// <summary>Level of the wild Pokemon.</summary>
    public required int WildLevel { get; init; }

    /// <summary>Whether this is a shiny Pokemon.</summary>
    public bool IsShiny { get; init; }

    public required float Timestamp { get; init; }
}

// ============================================================================
// INTERACTION EVENTS - Full access for mods
// ============================================================================

/// <summary>
///     Published BEFORE an interaction is triggered. Cancellable.
/// </summary>
public struct InteractionTriggeringEvent : IModCancellableEvent
{
    /// <summary>Entity initiating the interaction (usually player).</summary>
    public required int SourceEntityId { get; init; }

    /// <summary>Entity being interacted with.</summary>
    public required int TargetEntityId { get; init; }

    /// <summary>Type of interaction (e.g., "talk", "examine", "use").</summary>
    public required string InteractionType { get; init; }

    /// <summary>Target's script ID if any.</summary>
    public string? TargetScriptId { get; init; }

    /// <summary>Map where interaction occurs.</summary>
    public required MapRuntimeId MapId { get; init; }

    /// <summary>Target tile X.</summary>
    public required int TargetX { get; init; }

    /// <summary>Target tile Y.</summary>
    public required int TargetY { get; init; }

    /// <summary>Direction the source is facing.</summary>
    public required int FacingDirection { get; init; }

    public required float Timestamp { get; init; }

    // IModCancellableEvent
    public bool IsCancelled { get; set; }
    public string? CancelledBy { get; set; }
    public string? CancelReason { get; set; }
}

/// <summary>
///     Published AFTER an interaction completes.
/// </summary>
public readonly struct InteractionTriggeredEvent
{
    public required int SourceEntityId { get; init; }
    public required int TargetEntityId { get; init; }
    public required string InteractionType { get; init; }
    public string? TargetScriptId { get; init; }
    public required MapRuntimeId MapId { get; init; }
    public required float Timestamp { get; init; }

    /// <summary>Result of the interaction (mod-defined).</summary>
    public string? Result { get; init; }
}

// ============================================================================
// BATTLE EVENTS - Full access for mods
// ============================================================================

/// <summary>
///     Published BEFORE a battle starts. Cancellable.
/// </summary>
public struct BattleStartingEvent : IModCancellableEvent
{
    /// <summary>Type of battle (wild, trainer, legendary, etc.).</summary>
    public required string BattleType { get; init; }

    /// <summary>Player entity ID.</summary>
    public required int PlayerEntityId { get; init; }

    /// <summary>Opponent entity ID (for trainer battles).</summary>
    public int? OpponentEntityId { get; init; }

    /// <summary>Wild Pokemon species (for wild battles).</summary>
    public string? WildSpeciesId { get; init; }

    /// <summary>Wild Pokemon level.</summary>
    public int? WildLevel { get; init; }

    /// <summary>Trainer class (for trainer battles).</summary>
    public string? TrainerClass { get; init; }

    /// <summary>Trainer name.</summary>
    public string? TrainerName { get; init; }

    /// <summary>Map where battle was triggered.</summary>
    public required MapRuntimeId MapId { get; init; }

    /// <summary>Battle music override.</summary>
    public string? MusicOverride { get; set; }

    /// <summary>Battle background override.</summary>
    public string? BackgroundOverride { get; set; }

    public required float Timestamp { get; init; }

    // IModCancellableEvent
    public bool IsCancelled { get; set; }
    public string? CancelledBy { get; set; }
    public string? CancelReason { get; set; }
}

/// <summary>
///     Published AFTER a battle ends.
/// </summary>
public readonly struct BattleEndedEvent
{
    public required string BattleType { get; init; }
    public required int PlayerEntityId { get; init; }

    /// <summary>Battle result (won, lost, fled, caught).</summary>
    public required string Result { get; init; }

    /// <summary>Total experience gained.</summary>
    public int ExperienceGained { get; init; }

    /// <summary>Money gained/lost.</summary>
    public int MoneyChange { get; init; }

    /// <summary>Pokemon caught (if any).</summary>
    public string? CaughtSpeciesId { get; init; }

    /// <summary>Battle duration in seconds.</summary>
    public float DurationSeconds { get; init; }

    /// <summary>Number of turns taken.</summary>
    public int TurnCount { get; init; }

    public required float Timestamp { get; init; }
}

// ============================================================================
// MAP EVENTS - Full access for mods
// ============================================================================

/// <summary>
///     Published BEFORE a map starts loading. Cancellable (can redirect).
/// </summary>
public struct MapLoadingEvent : IModCancellableEvent
{
    /// <summary>Map identifier being loaded.</summary>
    public required string MapId { get; init; }

    /// <summary>Map runtime ID assigned.</summary>
    public required MapRuntimeId RuntimeId { get; init; }

    /// <summary>World X offset for this map.</summary>
    public float WorldOffsetX { get; set; }

    /// <summary>World Y offset for this map.</summary>
    public float WorldOffsetY { get; set; }

    /// <summary>Whether this is the initial map load.</summary>
    public bool IsInitialLoad { get; init; }

    /// <summary>Whether this is from map streaming (adjacent map).</summary>
    public bool IsStreamedLoad { get; init; }

    /// <summary>Map to redirect to (mods can change this).</summary>
    public string? RedirectToMapId { get; set; }

    public required float Timestamp { get; init; }

    // IModCancellableEvent
    public bool IsCancelled { get; set; }
    public string? CancelledBy { get; set; }
    public string? CancelReason { get; set; }
}

/// <summary>
///     Published AFTER a map is fully loaded.
/// </summary>
public readonly struct MapLoadedEvent
{
    public required string MapId { get; init; }
    public required MapRuntimeId RuntimeId { get; init; }
    public required int TileCount { get; init; }
    public required int EntityCount { get; init; }
    public required int Width { get; init; }
    public required int Height { get; init; }
    public float WorldOffsetX { get; init; }
    public float WorldOffsetY { get; init; }
    public required float LoadTimeMs { get; init; }
    public required float Timestamp { get; init; }
}

/// <summary>
///     Published BEFORE a map is unloaded.
/// </summary>
public struct MapUnloadingEvent : IModCancellableEvent
{
    public required string MapId { get; init; }
    public required MapRuntimeId RuntimeId { get; init; }
    public required string UnloadReason { get; init; }
    public required float Timestamp { get; init; }

    // IModCancellableEvent
    public bool IsCancelled { get; set; }
    public string? CancelledBy { get; set; }
    public string? CancelReason { get; set; }
}

/// <summary>
///     Published AFTER a map is unloaded.
/// </summary>
public readonly struct MapUnloadedEvent
{
    public required string MapId { get; init; }
    public required MapRuntimeId RuntimeId { get; init; }
    public required float Timestamp { get; init; }
}

// ============================================================================
// GAME STATE EVENTS - Full access for mods
// ============================================================================

/// <summary>
///     Published when a game flag changes.
/// </summary>
public readonly struct FlagChangedEvent
{
    public required string FlagName { get; init; }
    public required bool OldValue { get; init; }
    public required bool NewValue { get; init; }
    public string? ChangedByModId { get; init; }
    public required float Timestamp { get; init; }
}

/// <summary>
///     Published when a game variable changes.
/// </summary>
public readonly struct VariableChangedEvent
{
    public required string VariableName { get; init; }
    public required string? OldValue { get; init; }
    public required string? NewValue { get; init; }
    public string? ChangedByModId { get; init; }
    public required float Timestamp { get; init; }
}

/// <summary>
///     Published when the game saves.
/// </summary>
public readonly struct GameSavedEvent
{
    public required string SaveSlot { get; init; }
    public required float PlayTimeSeconds { get; init; }
    public required string PlayerMapId { get; init; }
    public required float Timestamp { get; init; }
}

/// <summary>
///     Published when the game loads a save.
/// </summary>
public readonly struct GameLoadedEvent
{
    public required string SaveSlot { get; init; }
    public required float PlayTimeSeconds { get; init; }
    public required string PlayerMapId { get; init; }
    public required float Timestamp { get; init; }
}
