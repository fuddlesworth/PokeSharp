using MonoBallFramework.Game.Engine.Core.Types;

namespace MonoBallFramework.Game.Ecs.Components.NPCs;

/// <summary>
///     Component that references a behavior type.
///     Links an entity to a moddable behavior from the TypeRegistry.
///     Pure data component - no methods.
/// </summary>
public struct Behavior
{
    /// <summary>
    ///     Type identifier for the behavior.
    ///     Full format: "base:behavior:movement/patrol", "base:behavior:movement/wander", etc.
    ///     References a type in the BehaviorDefinition TypeRegistry.
    /// </summary>
    public GameBehaviorId BehaviorId { get; set; }

    /// <summary>
    ///     Gets the behavior type ID string for registry lookup.
    /// </summary>
    public string BehaviorTypeId => BehaviorId.Value;

    /// <summary>
    ///     Whether this behavior is currently active and should execute its OnTick method.
    /// </summary>
    public bool IsActive { get; set; }

    /// <summary>
    ///     Whether this behavior has been initialized (OnActivated called).
    ///     Used to ensure script initialization happens only once per entity.
    /// </summary>
    public bool IsInitialized { get; set; }

    /// <summary>
    ///     Initializes a new behavior component with a behavior ID.
    /// </summary>
    public Behavior(GameBehaviorId behaviorId)
    {
        BehaviorId = behaviorId;
        IsActive = true;
        IsInitialized = false;
    }

    /// <summary>
    ///     Initializes a new behavior component with a type ID string.
    ///     The string should be the full ID format (e.g., "base:behavior:movement/patrol").
    /// </summary>
    public Behavior(string behaviorTypeId)
    {
        BehaviorId = new GameBehaviorId(behaviorTypeId);
        IsActive = true;
        IsInitialized = false;
    }
}
