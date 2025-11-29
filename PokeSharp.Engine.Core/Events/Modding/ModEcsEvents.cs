using PokeSharp.Engine.Core.Types;

namespace PokeSharp.Engine.Core.Events.Modding;

// ============================================================================
// SAFE ECS EVENTS - Wrappers that expose EntityId instead of raw Entity
// ============================================================================
//
// These events provide mods with visibility into ECS internals without
// giving them direct Entity references that could corrupt game state.
// Mods can react to these events but cannot directly manipulate entities.
//
// For direct entity manipulation, mods should use the provided APIs
// (ScriptContext.World queries, component helpers, etc.)
// ============================================================================

/// <summary>
///     Published when a new entity is created in the ECS world.
/// </summary>
/// <remarks>
///     Provides EntityId for tracking, not raw Entity reference.
///     Use ScriptContext.World.TryGet() with EntityId if you need access.
/// </remarks>
public readonly struct EntitySpawnedEvent
{
    /// <summary>Unique entity ID (stable for entity lifetime).</summary>
    public required int EntityId { get; init; }

    /// <summary>Entity archetype description (component types).</summary>
    public required string Archetype { get; init; }

    /// <summary>Map where entity was spawned (if applicable).</summary>
    public MapRuntimeId? MapId { get; init; }

    /// <summary>Tile X position (if entity has position).</summary>
    public int? TileX { get; init; }

    /// <summary>Tile Y position (if entity has position).</summary>
    public int? TileY { get; init; }

    /// <summary>Entity type tag (e.g., "player", "npc", "tile", "item").</summary>
    public string? EntityType { get; init; }

    /// <summary>Template ID used to create entity (if any).</summary>
    public string? TemplateId { get; init; }

    /// <summary>Mod that spawned this entity (if mod-created).</summary>
    public string? SpawnedByModId { get; init; }

    /// <summary>Game timestamp.</summary>
    public required float Timestamp { get; init; }
}

/// <summary>
///     Published when an entity is destroyed.
/// </summary>
public readonly struct EntityDespawnedEvent
{
    /// <summary>Entity ID that was destroyed.</summary>
    public required int EntityId { get; init; }

    /// <summary>Entity archetype before destruction.</summary>
    public required string Archetype { get; init; }

    /// <summary>Reason for despawn.</summary>
    public required string Reason { get; init; }

    /// <summary>Map where entity was located (if applicable).</summary>
    public MapRuntimeId? MapId { get; init; }

    /// <summary>Entity type tag.</summary>
    public string? EntityType { get; init; }

    /// <summary>Mod that destroyed this entity (if mod-initiated).</summary>
    public string? DespawnedByModId { get; init; }

    /// <summary>Game timestamp.</summary>
    public required float Timestamp { get; init; }
}

/// <summary>
///     Published when a significant component is added to an entity.
/// </summary>
/// <remarks>
///     Not fired for every component (performance). Only fired for
///     "interesting" components that mods might want to react to.
/// </remarks>
public readonly struct ComponentAttachedEvent
{
    /// <summary>Entity ID receiving the component.</summary>
    public required int EntityId { get; init; }

    /// <summary>Component type name (e.g., "Position", "Health", "Inventory").</summary>
    public required string ComponentType { get; init; }

    /// <summary>Serialized component data (JSON) for inspection.</summary>
    public string? ComponentDataJson { get; init; }

    /// <summary>Game timestamp.</summary>
    public required float Timestamp { get; init; }
}

/// <summary>
///     Published when a significant component is removed from an entity.
/// </summary>
public readonly struct ComponentDetachedEvent
{
    /// <summary>Entity ID losing the component.</summary>
    public required int EntityId { get; init; }

    /// <summary>Component type name.</summary>
    public required string ComponentType { get; init; }

    /// <summary>Reason for removal.</summary>
    public string? Reason { get; init; }

    /// <summary>Game timestamp.</summary>
    public required float Timestamp { get; init; }
}

/// <summary>
///     Published when an entity's health changes (common case).
/// </summary>
public readonly struct HealthChangedEvent
{
    public required int EntityId { get; init; }
    public required int OldHealth { get; init; }
    public required int NewHealth { get; init; }
    public required int MaxHealth { get; init; }
    public required string ChangeReason { get; init; }
    public int? SourceEntityId { get; init; }
    public required float Timestamp { get; init; }
}

/// <summary>
///     Published when an entity's position changes significantly.
/// </summary>
/// <remarks>
///     Not fired for every pixel movement - only tile changes.
///     For smooth movement tracking, use TileEntered/TileExited events.
/// </remarks>
public readonly struct PositionChangedEvent
{
    public required int EntityId { get; init; }
    public required MapRuntimeId MapId { get; init; }
    public required int OldTileX { get; init; }
    public required int OldTileY { get; init; }
    public required int NewTileX { get; init; }
    public required int NewTileY { get; init; }
    public required string MovementType { get; init; }
    public required float Timestamp { get; init; }
}

// ============================================================================
// NPC-SPECIFIC EVENTS
// ============================================================================

/// <summary>
///     Published when an NPC starts a behavior/patrol.
/// </summary>
public readonly struct NpcBehaviorStartedEvent
{
    public required int NpcEntityId { get; init; }
    public required string NpcId { get; init; }
    public required string BehaviorType { get; init; }
    public required string BehaviorScriptId { get; init; }
    public required MapRuntimeId MapId { get; init; }
    public required float Timestamp { get; init; }
}

/// <summary>
///     Published when an NPC changes state.
/// </summary>
public readonly struct NpcStateChangedEvent
{
    public required int NpcEntityId { get; init; }
    public required string NpcId { get; init; }
    public required string OldState { get; init; }
    public required string NewState { get; init; }
    public string? TriggerReason { get; init; }
    public required float Timestamp { get; init; }
}

/// <summary>
///     Published when an NPC spots the player (for trainer battles).
/// </summary>
public struct NpcSpottedPlayerEvent : IModCancellableEvent
{
    public required int NpcEntityId { get; init; }
    public required string NpcId { get; init; }
    public required int PlayerEntityId { get; init; }
    public required int Distance { get; init; }
    public required int Direction { get; init; }
    public required MapRuntimeId MapId { get; init; }
    public required float Timestamp { get; init; }

    /// <summary>If true, NPC will initiate interaction (battle/dialogue).</summary>
    public bool WillInitiateInteraction { get; set; }

    // IModCancellableEvent
    public bool IsCancelled { get; set; }
    public string? CancelledBy { get; set; }
    public string? CancelReason { get; set; }
}

// ============================================================================
// ITEM EVENTS
// ============================================================================

/// <summary>
///     Published when an item is obtained.
/// </summary>
public readonly struct ItemObtainedEvent
{
    public required int PlayerEntityId { get; init; }
    public required string ItemId { get; init; }
    public required int Quantity { get; init; }
    public required string ObtainMethod { get; init; }
    public MapRuntimeId? MapId { get; init; }
    public int? SourceEntityId { get; init; }
    public required float Timestamp { get; init; }
}

/// <summary>
///     Published BEFORE an item is used. Cancellable.
/// </summary>
public struct ItemUsingEvent : IModCancellableEvent
{
    public required int PlayerEntityId { get; init; }
    public required string ItemId { get; init; }
    public int? TargetEntityId { get; init; }
    public required string UseContext { get; init; }
    public required float Timestamp { get; init; }

    // IModCancellableEvent
    public bool IsCancelled { get; set; }
    public string? CancelledBy { get; set; }
    public string? CancelReason { get; set; }
}

/// <summary>
///     Published AFTER an item is used.
/// </summary>
public readonly struct ItemUsedEvent
{
    public required int PlayerEntityId { get; init; }
    public required string ItemId { get; init; }
    public int? TargetEntityId { get; init; }
    public required string UseContext { get; init; }
    public required string Result { get; init; }
    public bool WasConsumed { get; init; }
    public required float Timestamp { get; init; }
}
