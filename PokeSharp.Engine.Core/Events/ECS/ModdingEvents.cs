namespace PokeSharp.Engine.Core.Events.ECS;

#region Script Events

/// <summary>
///     Event published BEFORE a script is loaded.
///     Handlers can cancel by setting IsCancelled = true.
/// </summary>
public struct ScriptLoadingEvent : ICancellableEvent
{
    /// <summary>
    ///     The script identifier or file path.
    /// </summary>
    public required string ScriptId { get; init; }

    /// <summary>
    ///     The script type (e.g., "csharp", "lua", "python").
    /// </summary>
    public required string ScriptType { get; init; }

    /// <summary>
    ///     Optional script source path.
    /// </summary>
    public string? SourcePath { get; init; }

    /// <inheritdoc />
    public required float Timestamp { get; init; }

    /// <inheritdoc />
    public required EventPriority Priority { get; init; }

    /// <inheritdoc />
    public bool IsCancelled { get; set; }
}

/// <summary>
///     Event published AFTER a script is successfully loaded.
/// </summary>
public readonly struct ScriptLoadedEvent : IEcsEvent
{
    /// <summary>
    ///     The script identifier.
    /// </summary>
    public required string ScriptId { get; init; }

    /// <summary>
    ///     The script type.
    /// </summary>
    public required string ScriptType { get; init; }

    /// <summary>
    ///     Script source path.
    /// </summary>
    public string? SourcePath { get; init; }

    /// <summary>
    ///     Compilation/load time in milliseconds.
    /// </summary>
    public double LoadTimeMs { get; init; }

    /// <inheritdoc />
    public required float Timestamp { get; init; }

    /// <inheritdoc />
    public required EventPriority Priority { get; init; }
}

/// <summary>
///     Event published BEFORE a script is unloaded.
///     Handlers can cancel by setting IsCancelled = true.
/// </summary>
public struct ScriptUnloadingEvent : ICancellableEvent
{
    /// <summary>
    ///     The script identifier.
    /// </summary>
    public required string ScriptId { get; init; }

    /// <summary>
    ///     Reason for unloading (e.g., "hot-reload", "shutdown", "error").
    /// </summary>
    public string? Reason { get; init; }

    /// <inheritdoc />
    public required float Timestamp { get; init; }

    /// <inheritdoc />
    public required EventPriority Priority { get; init; }

    /// <inheritdoc />
    public bool IsCancelled { get; set; }
}

/// <summary>
///     Event published AFTER a script is successfully unloaded.
/// </summary>
public readonly struct ScriptUnloadedEvent : IEcsEvent
{
    /// <summary>
    ///     The script identifier.
    /// </summary>
    public required string ScriptId { get; init; }

    /// <summary>
    ///     Reason for unloading.
    /// </summary>
    public string? Reason { get; init; }

    /// <inheritdoc />
    public required float Timestamp { get; init; }

    /// <inheritdoc />
    public required EventPriority Priority { get; init; }
}

#endregion

#region Mod Events

/// <summary>
///     Event published BEFORE a mod is loaded.
///     Handlers can cancel by setting IsCancelled = true.
/// </summary>
public struct ModLoadingEvent : ICancellableEvent
{
    /// <summary>
    ///     The mod identifier (from mod.json).
    /// </summary>
    public required string ModId { get; init; }

    /// <summary>
    ///     Mod version string.
    /// </summary>
    public required string Version { get; init; }

    /// <summary>
    ///     Mod display name.
    /// </summary>
    public required string Name { get; init; }

    /// <summary>
    ///     Mod root directory path.
    /// </summary>
    public required string RootPath { get; init; }

    /// <inheritdoc />
    public required float Timestamp { get; init; }

    /// <inheritdoc />
    public required EventPriority Priority { get; init; }

    /// <inheritdoc />
    public bool IsCancelled { get; set; }
}

/// <summary>
///     Event published AFTER a mod is successfully loaded.
/// </summary>
public readonly struct ModLoadedEvent : IEcsEvent
{
    /// <summary>
    ///     The mod identifier.
    /// </summary>
    public required string ModId { get; init; }

    /// <summary>
    ///     Mod version.
    /// </summary>
    public required string Version { get; init; }

    /// <summary>
    ///     Mod display name.
    /// </summary>
    public required string Name { get; init; }

    /// <summary>
    ///     Mod root directory.
    /// </summary>
    public required string RootPath { get; init; }

    /// <summary>
    ///     Number of scripts loaded by this mod.
    /// </summary>
    public int ScriptCount { get; init; }

    /// <summary>
    ///     Load time in milliseconds.
    /// </summary>
    public double LoadTimeMs { get; init; }

    /// <inheritdoc />
    public required float Timestamp { get; init; }

    /// <inheritdoc />
    public required EventPriority Priority { get; init; }
}

/// <summary>
///     Event published BEFORE a mod is unloaded.
///     Handlers can cancel by setting IsCancelled = true.
/// </summary>
public struct ModUnloadingEvent : ICancellableEvent
{
    /// <summary>
    ///     The mod identifier.
    /// </summary>
    public required string ModId { get; init; }

    /// <summary>
    ///     Reason for unloading (e.g., "user-request", "hot-reload", "shutdown").
    /// </summary>
    public string? Reason { get; init; }

    /// <inheritdoc />
    public required float Timestamp { get; init; }

    /// <inheritdoc />
    public required EventPriority Priority { get; init; }

    /// <inheritdoc />
    public bool IsCancelled { get; set; }
}

/// <summary>
///     Event published AFTER a mod is successfully unloaded.
/// </summary>
public readonly struct ModUnloadedEvent : IEcsEvent
{
    /// <summary>
    ///     The mod identifier.
    /// </summary>
    public required string ModId { get; init; }

    /// <summary>
    ///     Reason for unloading.
    /// </summary>
    public string? Reason { get; init; }

    /// <summary>
    ///     Number of scripts that were unloaded.
    /// </summary>
    public int ScriptCount { get; init; }

    /// <inheritdoc />
    public required float Timestamp { get; init; }

    /// <inheritdoc />
    public required EventPriority Priority { get; init; }
}

#endregion

#region Hot Reload Events

/// <summary>
///     Event published BEFORE hot reload is triggered.
///     Handlers can cancel by setting IsCancelled = true.
/// </summary>
public struct HotReloadTriggeringEvent : ICancellableEvent
{
    /// <summary>
    ///     The script or mod identifier being reloaded.
    /// </summary>
    public required string TargetId { get; init; }

    /// <summary>
    ///     Target type ("script" or "mod").
    /// </summary>
    public required string TargetType { get; init; }

    /// <summary>
    ///     Reason for reload (e.g., "file-changed", "user-request").
    /// </summary>
    public string? Reason { get; init; }

    /// <inheritdoc />
    public required float Timestamp { get; init; }

    /// <inheritdoc />
    public required EventPriority Priority { get; init; }

    /// <inheritdoc />
    public bool IsCancelled { get; set; }
}

/// <summary>
///     Event published AFTER hot reload is successfully triggered.
/// </summary>
public readonly struct HotReloadTriggeredEvent : IEcsEvent
{
    /// <summary>
    ///     The script or mod identifier that was reloaded.
    /// </summary>
    public required string TargetId { get; init; }

    /// <summary>
    ///     Target type.
    /// </summary>
    public required string TargetType { get; init; }

    /// <summary>
    ///     Reason for reload.
    /// </summary>
    public string? Reason { get; init; }

    /// <summary>
    ///     Whether the reload was successful.
    /// </summary>
    public required bool Success { get; init; }

    /// <summary>
    ///     Error message if reload failed.
    /// </summary>
    public string? ErrorMessage { get; init; }

    /// <summary>
    ///     Reload time in milliseconds.
    /// </summary>
    public double ReloadTimeMs { get; init; }

    /// <inheritdoc />
    public required float Timestamp { get; init; }

    /// <inheritdoc />
    public required EventPriority Priority { get; init; }
}

#endregion