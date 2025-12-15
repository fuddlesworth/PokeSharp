using MonoBallFramework.Game.Engine.Core.Types;

namespace MonoBallFramework.Game.Ecs.Components.Tiles;

/// <summary>
///     Component that references a tile behavior type.
///     Links a tile entity to a moddable behavior from the TypeRegistry.
///     Pure data component - no methods.
/// </summary>
public struct TileBehavior
{
    /// <summary>
    ///     Type identifier for the behavior.
    ///     Full format: "base:tile_behavior:movement/jump_south", "base:tile_behavior:blocking/impassable_east", etc.
    ///     References a type in the TileBehaviorDefinition TypeRegistry.
    /// </summary>
    public GameTileBehaviorId BehaviorId { get; set; }

    /// <summary>
    ///     Gets the behavior type ID string for registry lookup.
    /// </summary>
    public string BehaviorTypeId => BehaviorId.Value;

    /// <summary>
    ///     Whether this behavior is currently active and should execute its logic.
    /// </summary>
    public bool IsActive { get; set; }

    /// <summary>
    ///     Whether this behavior has been initialized (OnActivated called).
    ///     Used to ensure script initialization happens only once per entity.
    /// </summary>
    public bool IsInitialized { get; set; }

    /// <summary>
    ///     Initializes a new tile behavior component with a behavior ID.
    /// </summary>
    public TileBehavior(GameTileBehaviorId behaviorId)
    {
        BehaviorId = behaviorId;
        IsActive = true;
        IsInitialized = false;
    }

    /// <summary>
    ///     Initializes a new tile behavior component with a type ID string.
    ///     The string should be the full ID format (e.g., "base:tile_behavior:movement/jump_south").
    /// </summary>
    public TileBehavior(string behaviorTypeId)
    {
        BehaviorId = new GameTileBehaviorId(behaviorTypeId);
        IsActive = true;
        IsInitialized = false;
    }
}
