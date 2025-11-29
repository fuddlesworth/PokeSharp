namespace PokeSharp.Engine.Core.Events.Modding;

/// <summary>
///     Configuration for a mod's event handler declared in mod.json.
/// </summary>
public sealed class ModEventHandlerConfig
{
    /// <summary>
    ///     Event type name to subscribe to (e.g., "TileEnteredEvent").
    /// </summary>
    public required string EventType { get; init; }

    /// <summary>
    ///     Path to the script file containing the handler (relative to mod root).
    /// </summary>
    public required string ScriptPath { get; init; }

    /// <summary>
    ///     Method name in the script to invoke (default: "Handle").
    /// </summary>
    public string MethodName { get; init; } = "Handle";

    /// <summary>
    ///     Handler priority (First, Early, Normal, Late, Last).
    /// </summary>
    public string Priority { get; init; } = "Normal";

    /// <summary>
    ///     Whether this handler can cancel the event (for cancellable events).
    /// </summary>
    public bool CanCancel { get; init; } = false;

    /// <summary>
    ///     Optional condition expression for when to invoke handler.
    /// </summary>
    /// <example>"event.IsPlayer == true"</example>
    public string? Condition { get; init; }

    /// <summary>
    ///     Description of what this handler does (for documentation).
    /// </summary>
    public string? Description { get; init; }
}

/// <summary>
///     Extended mod manifest with event handler support.
/// </summary>
public sealed class ModEventManifest
{
    /// <summary>
    ///     Event handlers declared by this mod.
    /// </summary>
    public List<ModEventHandlerConfig> EventHandlers { get; init; } = new();

    /// <summary>
    ///     Events this mod publishes (for documentation/discovery).
    /// </summary>
    public List<ModCustomEventDefinition> CustomEvents { get; init; } = new();

    /// <summary>
    ///     Whether to enable verbose event logging for debugging.
    /// </summary>
    public bool DebugEvents { get; init; } = false;
}

/// <summary>
///     Definition of a custom event type added by a mod.
/// </summary>
public sealed class ModCustomEventDefinition
{
    /// <summary>
    ///     Event type name.
    /// </summary>
    public required string Name { get; init; }

    /// <summary>
    ///     Description of when this event fires.
    /// </summary>
    public string? Description { get; init; }

    /// <summary>
    ///     Whether the event is cancellable.
    /// </summary>
    public bool IsCancellable { get; init; }

    /// <summary>
    ///     Event fields for documentation.
    /// </summary>
    public Dictionary<string, string> Fields { get; init; } = new();
}
