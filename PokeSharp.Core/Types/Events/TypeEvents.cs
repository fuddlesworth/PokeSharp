using Arch.Core;

namespace PokeSharp.Core.Types.Events;

/// <summary>
///     Base event for type-related events.
///     All type lifecycle events should inherit from this to maintain consistency.
/// </summary>
public abstract record TypeEventBase
{
    /// <summary>
    ///     The type identifier that this event relates to.
    /// </summary>
    public required string TypeId { get; init; }

    /// <summary>
    ///     Game timestamp when this event was created (in seconds since game start).
    /// </summary>
    public required float Timestamp { get; init; }
}

/// <summary>
///     Event fired when a type is activated/applied to an entity or game state.
/// </summary>
/// <remarks>
///     <para>
///         This event is fired when ANY type becomes active in the game world.
///         It supports both entity-specific types and global types (like weather).
///     </para>
///     <para>
///         USAGE PATTERNS:
///     </para>
///     <list type="bullet">
///         <item>
///             <description>
///                 <b>Global Types (Weather):</b> Set TargetEntity = null.
///                 Example: Weather "rain" activates globally.
///             </description>
///         </item>
///         <item>
///             <description>
///                 <b>Entity-Specific Types:</b> Set TargetEntity to the affected entity.
///                 Example: Player enters "lava" collision tile.
///             </description>
///         </item>
///     </list>
/// </remarks>
/// <example>
///     Weather activation example:
///     <code>
/// // Weather is a global type - no specific entity
/// eventBus.Publish(new TypeActivatedEvent
/// {
///     TypeId = "rain",
///     Timestamp = gameTime,
///     TargetEntity = null  // null = global weather type
/// });
/// </code>
///
///     Entity-specific activation example:
///     <code>
/// // Lava tile affects a specific player entity
/// eventBus.Publish(new TypeActivatedEvent
/// {
///     TypeId = "lava",
///     Timestamp = gameTime,
///     TargetEntity = playerEntity  // specific entity affected
/// });
/// </code>
/// </example>
public record TypeActivatedEvent : TypeEventBase
{
    /// <summary>
    ///     The entity that this type was activated on (if applicable).
    ///     Null for global types like weather.
    /// </summary>
    /// <remarks>
    ///     When null, this indicates a GLOBAL type that affects the entire game world
    ///     (e.g., weather conditions, global modifiers, environmental effects).
    ///     When set, this indicates an entity-specific type activation.
    /// </remarks>
    public Entity? TargetEntity { get; init; }
}

/// <summary>
///     Event fired every frame while a type is active.
///     Allows types to execute per-frame logic via event handlers.
/// </summary>
/// <remarks>
///     <para>
///         This event is fired EVERY FRAME for active types. Handlers should be
///         lightweight and optimized, as they will be called frequently.
///     </para>
///     <para>
///         USAGE PATTERNS:
///     </para>
///     <list type="bullet">
///         <item>
///             <description>
///                 <b>Global Types (Weather):</b> TargetEntity = null.
///                 Use for updating weather effects, particles, or global state.
///             </description>
///         </item>
///         <item>
///             <description>
///                 <b>Entity-Specific Types:</b> TargetEntity set to specific entity.
///                 Use for entity behaviors, status effects, or collision responses.
///             </description>
///         </item>
///     </list>
///     <para>
///         WARNING: This event fires every frame. Keep handlers lightweight!
///     </para>
/// </remarks>
/// <example>
///     Weather tick example:
///     <code>
/// // Weather updates globally every frame
/// eventBus.Publish(new TypeTickEvent
/// {
///     TypeId = "rain",
///     DeltaTime = deltaTime,
///     Timestamp = gameTime,
///     TargetEntity = null  // null = global weather update
/// });
/// </code>
/// </example>
public record TypeTickEvent : TypeEventBase
{
    /// <summary>
    ///     Time elapsed since last frame (in seconds).
    /// </summary>
    public required float DeltaTime { get; init; }

    /// <summary>
    ///     The entity that this type is ticking on (if applicable).
    ///     Null for global types like weather.
    /// </summary>
    /// <remarks>
    ///     When null, this indicates a GLOBAL type tick that affects the entire game world.
    ///     When set, this indicates an entity-specific type tick.
    /// </remarks>
    public Entity? TargetEntity { get; init; }
}

/// <summary>
///     Event fired when a type is deactivated/removed from an entity or game state.
/// </summary>
/// <remarks>
///     <para>
///         This event is fired when ANY type is deactivated or removed.
///         It supports both entity-specific types and global types (like weather).
///     </para>
///     <para>
///         USAGE PATTERNS:
///     </para>
///     <list type="bullet">
///         <item>
///             <description>
///                 <b>Global Types (Weather):</b> TargetEntity = null.
///                 Use for cleaning up weather effects and global state.
///             </description>
///         </item>
///         <item>
///             <description>
///                 <b>Entity-Specific Types:</b> TargetEntity set to specific entity.
///                 Use for removing entity behaviors, status effects, or collision responses.
///             </description>
///         </item>
///     </list>
/// </remarks>
/// <example>
///     Weather deactivation example:
///     <code>
/// // Weather clears globally
/// eventBus.Publish(new TypeDeactivatedEvent
/// {
///     TypeId = "rain",
///     Timestamp = gameTime,
///     TargetEntity = null  // null = global weather cleared
/// });
/// </code>
/// </example>
public record TypeDeactivatedEvent : TypeEventBase
{
    /// <summary>
    ///     The entity that this type was deactivated from (if applicable).
    ///     Null for global types like weather.
    /// </summary>
    /// <remarks>
    ///     When null, this indicates a GLOBAL type deactivation that affects the entire game world.
    ///     When set, this indicates an entity-specific type deactivation.
    /// </remarks>
    public Entity? TargetEntity { get; init; }
}
