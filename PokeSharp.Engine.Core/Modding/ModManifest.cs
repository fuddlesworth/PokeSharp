using System.Text.RegularExpressions;

namespace PokeSharp.Engine.Core.Modding;

/// <summary>
///     Defines a mod's metadata and configuration.
///     Mods can add new content or patch existing content using JSON Patch operations.
/// </summary>
public sealed class ModManifest
{
    /// <summary>
    ///     Unique identifier for this mod (e.g. "myusername.coolmod")
    /// </summary>
    public required string ModId { get; init; }

    /// <summary>
    ///     Display name for the mod
    /// </summary>
    public required string Name { get; init; }

    /// <summary>
    ///     Mod author
    /// </summary>
    public string Author { get; init; } = "Unknown";

    /// <summary>
    ///     Mod version (semantic versioning recommended)
    /// </summary>
    public string Version { get; init; } = "1.0.0";

    /// <summary>
    ///     Mod description
    /// </summary>
    public string Description { get; init; } = "";

    /// <summary>
    ///     List of mod IDs this mod depends on (must be loaded before this mod)
    /// </summary>
    public List<string> Dependencies { get; init; } = new();

    /// <summary>
    ///     List of mod IDs that must be loaded before this mod (soft dependencies)
    /// </summary>
    public List<string> LoadBefore { get; init; } = new();

    /// <summary>
    ///     List of mod IDs that must be loaded after this mod
    /// </summary>
    public List<string> LoadAfter { get; init; } = new();

    /// <summary>
    ///     Explicit load order priority (lower = earlier, default = 100)
    /// </summary>
    public int LoadPriority { get; init; } = 100;

    /// <summary>
    ///     Relative paths to JSON Patch files that modify existing data
    /// </summary>
    public List<string> Patches { get; init; } = new();

    /// <summary>
    ///     Relative paths to folders containing new content (templates, definitions, etc.)
    /// </summary>
    public Dictionary<string, string> ContentFolders { get; init; } = new();

    /// <summary>
    ///     Event handlers declared by this mod.
    /// </summary>
    /// <remarks>
    ///     Mods have full access to all gameplay events. Event handlers are executed
    ///     in priority order, with error isolation to prevent one mod from crashing others.
    /// </remarks>
    public List<ModEventHandler> EventHandlers { get; init; } = new();

    /// <summary>
    ///     Custom events published by this mod (for documentation/discovery).
    /// </summary>
    public List<ModCustomEvent> CustomEvents { get; init; } = new();

    /// <summary>
    ///     Enable verbose event logging for debugging this mod's handlers.
    /// </summary>
    public bool DebugEvents { get; init; } = false;

    public void Validate()
    {
        if (string.IsNullOrWhiteSpace(ModId))
            throw new InvalidOperationException("ModId is required");

        if (string.IsNullOrWhiteSpace(Name))
            throw new InvalidOperationException("Name is required");

        // Validate semantic versioning format (basic check)
        if (!Regex.IsMatch(Version, @"^\d+\.\d+\.\d+"))
            throw new InvalidOperationException(
                $"Version must follow semantic versioning (e.g. 1.0.0): {Version}"
            );
    }

    public override string ToString()
    {
        return $"{ModId} v{Version} ({Name})";
    }
}

/// <summary>
///     Declares an event handler in the mod manifest.
/// </summary>
/// <remarks>
///     Example in mod.json:
///     <code>
///     {
///       "EventHandlers": [
///         {
///           "EventType": "TileEnteredEvent",
///           "ScriptPath": "scripts/on_tile_enter.csx",
///           "Priority": "Normal",
///           "Description": "Check for hidden items when stepping on tiles"
///         }
///       ]
///     }
///     </code>
/// </remarks>
public sealed class ModEventHandler
{
    /// <summary>
    ///     Event type name to subscribe to (e.g., "TileEnteredEvent", "BattleStartingEvent").
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
    ///     Handler priority: First, Early, Normal, Late, Last (default: Normal).
    /// </summary>
    public string Priority { get; init; } = "Normal";

    /// <summary>
    ///     Whether this handler can cancel the event (for cancellable events only).
    /// </summary>
    public bool CanCancel { get; init; } = false;

    /// <summary>
    ///     Optional filter condition (e.g., "IsPlayer == true").
    /// </summary>
    public string? Condition { get; init; }

    /// <summary>
    ///     Description of what this handler does.
    /// </summary>
    public string? Description { get; init; }
}

/// <summary>
///     Declares a custom event type published by the mod.
/// </summary>
/// <remarks>
///     Custom events allow mods to communicate with each other.
///     Other mods can subscribe to these events if they declare this mod as a dependency.
/// </remarks>
public sealed class ModCustomEvent
{
    /// <summary>
    ///     Event type name (should be prefixed with mod ID, e.g., "mymod.CustomEvent").
    /// </summary>
    public required string Name { get; init; }

    /// <summary>
    ///     Description of when this event fires.
    /// </summary>
    public string? Description { get; init; }

    /// <summary>
    ///     Whether the event can be cancelled by other mods.
    /// </summary>
    public bool IsCancellable { get; init; } = false;

    /// <summary>
    ///     Event fields with descriptions (for documentation).
    /// </summary>
    public Dictionary<string, string> Fields { get; init; } = new();
}
