namespace PokeSharp.Engine.Core.Events.ECS;

/// <summary>
///     High-performance event bus interface for ECS struct-based events.
/// </summary>
/// <remarks>
///     <para>
///         This interface bridges to Arch.EventBus 2.1.0, providing zero-allocation
///         event publishing with struct-based events. Events are processed in priority
///         order with support for cancellation.
///     </para>
///     <para>
///         ARCHITECTURE:
///         - Struct events: Zero GC allocation, cache-friendly
///         - Priority ordering: Critical handlers execute first
///         - Cancellation support: Pre-events can prevent actions
///         - Source generation: Arch.EventBus generates optimized code
///     </para>
///     <example>
///         Publishing events:
///         <code>
/// // Simple event
/// _eventBus.Publish(new MapLoadedEvent
/// {
///     MapId = 1,
///     Timestamp = gameTime,
///     Priority = EventPriority.Normal
/// });
///
/// // Cancellable event
/// var evt = new TileEnteringEvent { /* ... */ };
/// if (_eventBus.PublishCancellable(ref evt) &amp;&amp; !evt.IsCancelled)
/// {
///     // Action allowed, publish confirmation
///     _eventBus.Publish(new TileEnteredEvent { /* ... */ });
/// }
/// </code>
///     </example>
/// </remarks>
public interface IEcsEventBus
{
    /// <summary>
    ///     Subscribes a typed handler to the specified event type.
    /// </summary>
    /// <typeparam name="TEvent">The event type to subscribe to.</typeparam>
    /// <param name="handler">The handler instance with priority property.</param>
    /// <returns>A disposable subscription that unsubscribes when disposed.</returns>
    /// <remarks>
    ///     Handlers are sorted by priority (Highest to Lowest). Use this method when
    ///     you need explicit priority control or DI-based handler registration.
    /// </remarks>
    IDisposable Subscribe<TEvent>(IEventHandler<TEvent> handler)
        where TEvent : struct, IEcsEvent;

    /// <summary>
    ///     Subscribes a delegate handler with optional priority.
    /// </summary>
    /// <typeparam name="TEvent">The event type to subscribe to.</typeparam>
    /// <param name="handler">The delegate to invoke when events are published.</param>
    /// <param name="priority">Execution priority (default: Normal).</param>
    /// <returns>A disposable subscription that unsubscribes when disposed.</returns>
    /// <remarks>
    ///     This is the simplest subscription method for inline handlers or lambdas.
    ///     Avoid capturing large closures as handlers are retained until unsubscription.
    /// </remarks>
    /// <example>
    ///     <code>
    /// _eventBus.Subscribe&lt;MapLoadedEvent&gt;(evt =&gt;
    /// {
    ///     Logger.LogInformation("Map {MapId} loaded", evt.MapId);
    /// }, EventPriority.Low);
    /// </code>
    /// </example>
    IDisposable Subscribe<TEvent>(
        Action<TEvent> handler,
        EventPriority priority = EventPriority.Normal
    )
        where TEvent : struct, IEcsEvent;

    /// <summary>
    ///     Publishes an event to all subscribers in priority order.
    /// </summary>
    /// <typeparam name="TEvent">The event type to publish.</typeparam>
    /// <param name="evt">The event instance to publish.</param>
    /// <remarks>
    ///     <para>
    ///         All subscribed handlers execute synchronously in priority order.
    ///         Handlers should be fast; for expensive work, queue to a background thread.
    ///     </para>
    ///     <para>
    ///         PERFORMANCE: Struct events are passed by value. For large structs (&gt;16 bytes),
    ///         consider using ref struct or keeping data minimal.
    ///     </para>
    /// </remarks>
    void Publish<TEvent>(TEvent evt)
        where TEvent : struct, IEcsEvent;

    /// <summary>
    ///     Publishes a cancellable event, stopping if any handler cancels it.
    /// </summary>
    /// <typeparam name="TEvent">The cancellable event type.</typeparam>
    /// <param name="evt">The event instance (passed by reference for cancellation).</param>
    /// <returns>True if not cancelled; false if any handler set IsCancelled to true.</returns>
    /// <remarks>
    ///     <para>
    ///         Handlers execute in priority order until one sets IsCancelled = true.
    ///         Subsequent handlers may still execute but should respect the cancelled state.
    ///     </para>
    ///     <para>
    ///         Use this for pre-events that validate actions (movement, interaction, etc).
    ///         Follow with a non-cancellable post-event if the action completes.
    ///     </para>
    /// </remarks>
    /// <example>
    ///     <code>
    /// var evt = new PlayerMovingEvent { /* ... */ };
    /// if (_eventBus.PublishCancellable(ref evt) &amp;&amp; !evt.IsCancelled)
    /// {
    ///     UpdatePlayerPosition(evt.ToPosition);
    ///     _eventBus.Publish(new PlayerMovedEvent { /* ... */ });
    /// }
    /// </code>
    /// </example>
    bool PublishCancellable<TEvent>(ref TEvent evt)
        where TEvent : struct, ICancellableEvent;

    /// <summary>
    ///     Gets the number of active subscribers for the specified event type.
    /// </summary>
    /// <typeparam name="TEvent">The event type to query.</typeparam>
    /// <returns>The count of registered handlers.</returns>
    /// <remarks>
    ///     Useful for diagnostics, debugging, and determining if publishing would have any effect.
    /// </remarks>
    int GetSubscriberCount<TEvent>()
        where TEvent : struct, IEcsEvent;

    /// <summary>
    ///     Clears all subscriptions for the specified event type.
    /// </summary>
    /// <typeparam name="TEvent">The event type to clear.</typeparam>
    /// <remarks>
    ///     Use with caution - this removes all handlers including those from mods and scripts.
    ///     Primarily for testing or cleanup during hot-reload.
    /// </remarks>
    void ClearSubscriptions<TEvent>()
        where TEvent : struct, IEcsEvent;

    /// <summary>
    ///     Clears all subscriptions across all event types.
    /// </summary>
    /// <remarks>
    ///     WARNING: This removes all event handlers system-wide. Only use during
    ///     shutdown, full reset, or test cleanup.
    /// </remarks>
    void ClearAllSubscriptions();
}
