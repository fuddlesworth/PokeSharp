namespace PokeSharp.Engine.Core.Events.ECS;

/// <summary>
///     Defines priority levels for event handler execution order.
/// </summary>
/// <remarks>
///     <para>
///         Event handlers are executed in priority order from Highest to Lowest.
///         Handlers with the same priority execute in subscription order.
///     </para>
///     <para>
///         GUIDELINES:
///         - Critical: Core game loop, player input, physics
///         - Highest: Anti-cheat, validation, security
///         - High: Game logic, AI decisions, critical mechanics
///         - Normal: Standard gameplay features, UI updates
///         - Low: Analytics, logging, non-essential effects
///         - Lowest: Debug tools, profiling, diagnostics
///     </para>
/// </remarks>
public enum EventPriority
{
    /// <summary>
    ///     Lowest priority - typically diagnostics and profiling.
    /// </summary>
    Lowest = -100,

    /// <summary>
    ///     Low priority - analytics, logging, non-essential features.
    /// </summary>
    Low = -50,

    /// <summary>
    ///     Normal priority - default for most handlers.
    /// </summary>
    Normal = 0,

    /// <summary>
    ///     High priority - important game logic and mechanics.
    /// </summary>
    High = 50,

    /// <summary>
    ///     Highest priority - validation, security, anti-cheat.
    /// </summary>
    Highest = 100,

    /// <summary>
    ///     Critical priority - core systems only (input, physics, game loop).
    /// </summary>
    /// <remarks>
    ///     Reserved for essential engine systems. Mod/script handlers should not use this.
    /// </remarks>
    Critical = 1000,
}
