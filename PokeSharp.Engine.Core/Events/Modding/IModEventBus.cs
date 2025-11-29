namespace PokeSharp.Engine.Core.Events.Modding;

/// <summary>
///     Event bus interface for mods with full gameplay event access.
/// </summary>
/// <remarks>
///     <para>
///         This interface provides mods with access to ALL gameplay events while
///         providing safety rails for error isolation and optional timeouts.
///         Mods are trusted community members, not adversaries.
///     </para>
///     <para>
///         DESIGN PHILOSOPHY:
///         - Full access to gameplay events (movement, encounters, battles, etc.)
///         - Safe wrappers for ECS internals (EntityId instead of raw Entity)
///         - Error isolation (one mod's exception doesn't crash others)
///         - Priority ordering respects mod load order
///     </para>
/// </remarks>
public interface IModEventBus
{
    /// <summary>
    ///     Subscribe to any event type with full access.
    /// </summary>
    /// <typeparam name="TEvent">The event type to subscribe to.</typeparam>
    /// <param name="handler">The handler delegate.</param>
    /// <param name="priority">Handler priority (default: Normal).</param>
    /// <returns>Disposable subscription handle.</returns>
    IDisposable Subscribe<TEvent>(
        Action<TEvent> handler,
        ModEventPriority priority = ModEventPriority.Normal
    )
        where TEvent : struct;

    /// <summary>
    ///     Subscribe to a cancellable event with ability to cancel/modify.
    /// </summary>
    /// <typeparam name="TEvent">The cancellable event type.</typeparam>
    /// <param name="handler">The handler delegate (receives ref for modification).</param>
    /// <param name="priority">Handler priority.</param>
    /// <returns>Disposable subscription handle.</returns>
    IDisposable SubscribeCancellable<TEvent>(
        ModCancellableHandler<TEvent> handler,
        ModEventPriority priority = ModEventPriority.Normal
    )
        where TEvent : struct, IModCancellableEvent;

    /// <summary>
    ///     Publish an event to all mod subscribers.
    /// </summary>
    /// <typeparam name="TEvent">The event type.</typeparam>
    /// <param name="evt">The event data.</param>
    void Publish<TEvent>(TEvent evt)
        where TEvent : struct;

    /// <summary>
    ///     Publish a cancellable event, allowing mods to cancel or modify it.
    /// </summary>
    /// <typeparam name="TEvent">The cancellable event type.</typeparam>
    /// <param name="evt">The event data (passed by ref for modification).</param>
    /// <returns>True if not cancelled; false if any handler cancelled it.</returns>
    bool PublishCancellable<TEvent>(ref TEvent evt)
        where TEvent : struct, IModCancellableEvent;

    /// <summary>
    ///     Gets statistics about event handling for debugging.
    /// </summary>
    ModEventStats GetStats();

    /// <summary>
    ///     Clears all subscriptions for a specific mod (used during mod unload).
    /// </summary>
    /// <param name="modId">The mod identifier.</param>
    void ClearModSubscriptions(string modId);
}

/// <summary>
///     Delegate for cancellable event handlers that can modify the event.
/// </summary>
public delegate void ModCancellableHandler<TEvent>(ref TEvent evt)
    where TEvent : struct;

/// <summary>
///     Priority levels for mod event handlers.
/// </summary>
/// <remarks>
///     Handlers execute in priority order (First → Last).
///     Within the same priority, handlers execute in subscription order.
/// </remarks>
public enum ModEventPriority
{
    /// <summary>Execute first - use for validation/blocking.</summary>
    First = 0,

    /// <summary>Execute early - use for pre-processing.</summary>
    Early = 25,

    /// <summary>Default priority for most handlers.</summary>
    Normal = 50,

    /// <summary>Execute late - use for post-processing.</summary>
    Late = 75,

    /// <summary>Execute last - use for logging/metrics.</summary>
    Last = 100,
}

/// <summary>
///     Interface for events that can be cancelled or modified by mods.
/// </summary>
public interface IModCancellableEvent
{
    /// <summary>Gets or sets whether this event has been cancelled.</summary>
    bool IsCancelled { get; set; }

    /// <summary>Gets the mod ID that cancelled this event, if any.</summary>
    string? CancelledBy { get; set; }

    /// <summary>Gets or sets the cancellation reason.</summary>
    string? CancelReason { get; set; }
}

/// <summary>
///     Statistics about mod event handling.
/// </summary>
public readonly struct ModEventStats
{
    /// <summary>Total events published.</summary>
    public required long TotalEventsPublished { get; init; }

    /// <summary>Total handlers invoked.</summary>
    public required long TotalHandlersInvoked { get; init; }

    /// <summary>Total handler errors caught and isolated.</summary>
    public required long TotalErrorsIsolated { get; init; }

    /// <summary>Average handler execution time in microseconds.</summary>
    public required double AverageHandlerTimeMicroseconds { get; init; }

    /// <summary>Number of active subscriptions.</summary>
    public required int ActiveSubscriptions { get; init; }
}
