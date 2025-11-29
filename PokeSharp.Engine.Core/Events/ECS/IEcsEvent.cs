namespace PokeSharp.Engine.Core.Events.ECS;

/// <summary>
///     Marker interface for all ECS events.
///     ECS events are struct-based for zero-allocation performance with Arch.EventBus.
/// </summary>
/// <remarks>
///     <para>
///         This interface provides the foundation for the high-performance event system
///         that integrates with Arch ECS 2.1.0. All ECS events should be readonly structs
///         for optimal memory layout and cache efficiency.
///     </para>
///     <para>
///         PERFORMANCE: Struct-based events eliminate GC pressure and enable better CPU
///         cache utilization compared to reference-based events. Use this for high-frequency
///         events like component changes, movement, and collision detection.
///     </para>
/// </remarks>
public interface IEcsEvent
{
    /// <summary>
    ///     Gets the timestamp when this event was created, in game time seconds since start.
    /// </summary>
    /// <remarks>
    ///     This allows event consumers to determine event age and implement time-based
    ///     filtering or debouncing. The timestamp should match the game's current time.
    /// </remarks>
    float Timestamp { get; }

    /// <summary>
    ///     Gets the priority level for this event, determining handler execution order.
    /// </summary>
    /// <remarks>
    ///     Handlers are executed in priority order (Highest to Lowest). Events with the
    ///     same priority execute in subscription order. Critical priority should be reserved
    ///     for essential game logic like player input and core mechanics.
    /// </remarks>
    EventPriority Priority { get; }
}

/// <summary>
///     Marker interface for cancellable events that support pre-event validation.
/// </summary>
/// <remarks>
///     <para>
///         Cancellable events follow the "pre/post" pattern where a "pre" event can be
///         cancelled to prevent an action, and a "post" event confirms the action occurred.
///     </para>
///     <example>
///         Movement validation:
///         <code>
/// var evt = new TileEnteringEvent { ... };
/// if (_eventBus.PublishCancellable(ref evt) &amp;&amp; !evt.IsCancelled)
/// {
///     // Movement allowed - update position
///     _eventBus.Publish(new TileEnteredEvent { ... });
/// }
/// </code>
///     </example>
/// </remarks>
public interface ICancellableEvent : IEcsEvent
{
    /// <summary>
    ///     Gets or sets whether this event has been cancelled by a handler.
    /// </summary>
    /// <remarks>
    ///     When set to true, subsequent handlers may skip processing, and the event
    ///     source should abort the action. Handlers should check this before making
    ///     state changes.
    /// </remarks>
    bool IsCancelled { get; set; }
}
