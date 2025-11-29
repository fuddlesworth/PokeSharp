namespace PokeSharp.Engine.Core.Events.ECS;

/// <summary>
///     Interface for typed event handlers with priority support.
/// </summary>
/// <typeparam name="TEvent">The event type this handler processes.</typeparam>
/// <remarks>
///     <para>
///         Implement this interface for handlers that need explicit priority control
///         or are registered through dependency injection. For simple handlers, use
///         the delegate-based subscription methods on IEcsEventBus.
///     </para>
///     <example>
///         Validation handler:
///         <code>
/// public class WaterTileValidator : IEventHandler&lt;TileEnteringEvent&gt;
/// {
///     public EventPriority Priority =&gt; EventPriority.High;
/// 
///     public void Handle(TileEnteringEvent evt)
///     {
///         if (IsWaterTile(evt.ToPosition) &amp;&amp; !CanSurf(evt.Entity))
///         {
///             evt.IsCancelled = true;
///         }
///     }
/// }
/// </code>
///     </example>
/// </remarks>
public interface IEventHandler<in TEvent>
    where TEvent : struct, IEcsEvent
{
    /// <summary>
    ///     Gets the execution priority for this handler.
    /// </summary>
    /// <remarks>
    ///     Handlers execute in priority order (Highest to Lowest). Return EventPriority.Normal
    ///     for standard handlers, or a higher/lower value for critical/non-essential logic.
    /// </remarks>
    EventPriority Priority { get; }

    /// <summary>
    ///     Handles the specified event.
    /// </summary>
    /// <param name="evt">The event to process. For cancellable events, set IsCancelled to prevent action.</param>
    /// <remarks>
    ///     This method should be fast and avoid blocking operations. For expensive processing,
    ///     consider queuing work or deferring to a background thread.
    /// </remarks>
    void Handle(TEvent evt);
}