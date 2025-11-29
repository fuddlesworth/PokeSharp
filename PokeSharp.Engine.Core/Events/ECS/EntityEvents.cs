using Arch.Core;

namespace PokeSharp.Engine.Core.Events.ECS;

#region Entity Lifecycle Events

/// <summary>
///     Event published BEFORE an entity is created.
///     Handlers can cancel entity creation by setting IsCancelled = true.
/// </summary>
/// <remarks>
///     Use this to validate entity creation, enforce limits, or prevent
///     spawning based on game state. Cancel sparingly as it may break assumptions.
/// </remarks>
public struct EntityCreatingEvent : ICancellableEvent
{
    /// <summary>
    ///     The entity reference that will be created (may be invalid if cancelled).
    /// </summary>
    public required Entity Entity { get; init; }

    /// <summary>
    ///     Optional archetype description for the entity being created.
    /// </summary>
    public string? ArchetypeDescription { get; init; }

    /// <inheritdoc />
    public required float Timestamp { get; init; }

    /// <inheritdoc />
    public required EventPriority Priority { get; init; }

    /// <inheritdoc />
    public bool IsCancelled { get; set; }
}

/// <summary>
///     Event published AFTER an entity is successfully created.
/// </summary>
/// <remarks>
///     This is a notification event - the entity exists and has been added to the world.
///     Use this to register the entity with external systems or initialize state.
/// </remarks>
public readonly struct EntityCreatedEvent : IEcsEvent
{
    /// <summary>
    ///     The newly created entity.
    /// </summary>
    public required Entity Entity { get; init; }

    /// <summary>
    ///     The archetype of the created entity.
    /// </summary>
    public string? ArchetypeDescription { get; init; }

    /// <inheritdoc />
    public required float Timestamp { get; init; }

    /// <inheritdoc />
    public required EventPriority Priority { get; init; }
}

/// <summary>
///     Event published BEFORE an entity is destroyed.
///     Handlers can cancel destruction by setting IsCancelled = true.
/// </summary>
/// <remarks>
///     Use this to prevent essential entities from being destroyed or to
///     cleanup dependencies before destruction occurs.
/// </remarks>
public struct EntityDestroyingEvent : ICancellableEvent
{
    /// <summary>
    ///     The entity that will be destroyed.
    /// </summary>
    public required Entity Entity { get; init; }

    /// <summary>
    ///     Reason for destruction (e.g., "player-death", "cleanup", "timeout").
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
///     Event published AFTER an entity is successfully destroyed.
/// </summary>
/// <remarks>
///     The entity reference is now invalid. Use this to cleanup external
///     references or update dependent systems.
/// </remarks>
public readonly struct EntityDestroyedEvent : IEcsEvent
{
    /// <summary>
    ///     The destroyed entity (reference is now invalid).
    /// </summary>
    public required Entity Entity { get; init; }

    /// <summary>
    ///     Reason for destruction.
    /// </summary>
    public string? Reason { get; init; }

    /// <inheritdoc />
    public required float Timestamp { get; init; }

    /// <inheritdoc />
    public required EventPriority Priority { get; init; }
}

#endregion

#region Component Events

/// <summary>
///     Event published BEFORE a component is added to an entity.
///     Handlers can cancel by setting IsCancelled = true.
/// </summary>
/// <typeparam name="T">The component type being added.</typeparam>
public struct ComponentAddingEvent<T> : ICancellableEvent
    where T : struct
{
    /// <summary>
    ///     The entity receiving the component.
    /// </summary>
    public required Entity Entity { get; init; }

    /// <summary>
    ///     The component value that will be added.
    /// </summary>
    public required T Component { get; init; }

    /// <inheritdoc />
    public required float Timestamp { get; init; }

    /// <inheritdoc />
    public required EventPriority Priority { get; init; }

    /// <inheritdoc />
    public bool IsCancelled { get; set; }
}

/// <summary>
///     Event published AFTER a component is added to an entity.
/// </summary>
/// <typeparam name="T">The component type that was added.</typeparam>
public readonly struct ComponentAddedEvent<T> : IEcsEvent
    where T : struct
{
    /// <summary>
    ///     The entity that received the component.
    /// </summary>
    public required Entity Entity { get; init; }

    /// <summary>
    ///     The component value that was added.
    /// </summary>
    public required T Component { get; init; }

    /// <inheritdoc />
    public required float Timestamp { get; init; }

    /// <inheritdoc />
    public required EventPriority Priority { get; init; }
}

/// <summary>
///     Event published BEFORE a component is removed from an entity.
///     Handlers can cancel by setting IsCancelled = true.
/// </summary>
/// <typeparam name="T">The component type being removed.</typeparam>
public struct ComponentRemovingEvent<T> : ICancellableEvent
    where T : struct
{
    /// <summary>
    ///     The entity losing the component.
    /// </summary>
    public required Entity Entity { get; init; }

    /// <summary>
    ///     The current component value (read-only).
    /// </summary>
    public required T Component { get; init; }

    /// <inheritdoc />
    public required float Timestamp { get; init; }

    /// <inheritdoc />
    public required EventPriority Priority { get; init; }

    /// <inheritdoc />
    public bool IsCancelled { get; set; }
}

/// <summary>
///     Event published AFTER a component is removed from an entity.
/// </summary>
/// <typeparam name="T">The component type that was removed.</typeparam>
public readonly struct ComponentRemovedEvent<T> : IEcsEvent
    where T : struct
{
    /// <summary>
    ///     The entity that lost the component.
    /// </summary>
    public required Entity Entity { get; init; }

    /// <summary>
    ///     The component value that was removed.
    /// </summary>
    public required T Component { get; init; }

    /// <inheritdoc />
    public required float Timestamp { get; init; }

    /// <inheritdoc />
    public required EventPriority Priority { get; init; }
}

/// <summary>
///     Event published when a component value changes.
/// </summary>
/// <typeparam name="T">The component type that changed.</typeparam>
/// <remarks>
///     Systems must explicitly publish this event - it's not automatically detected.
///     Use this for important state changes that other systems need to react to.
/// </remarks>
public readonly struct ComponentChangedEvent<T> : IEcsEvent
    where T : struct
{
    /// <summary>
    ///     The entity with the changed component.
    /// </summary>
    public required Entity Entity { get; init; }

    /// <summary>
    ///     The previous component value.
    /// </summary>
    public required T OldValue { get; init; }

    /// <summary>
    ///     The new component value.
    /// </summary>
    public required T NewValue { get; init; }

    /// <inheritdoc />
    public required float Timestamp { get; init; }

    /// <inheritdoc />
    public required EventPriority Priority { get; init; }
}

#endregion
