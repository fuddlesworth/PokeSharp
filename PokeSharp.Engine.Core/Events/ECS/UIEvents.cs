using Microsoft.Xna.Framework;

namespace PokeSharp.Engine.Core.Events.ECS;

#region UI Events

/// <summary>
///     Event raised when dialogue text should be displayed to the player.
/// </summary>
public readonly struct DialogueRequestedEvent : IEcsEvent
{
    /// <summary>
    ///     Gets the dialogue message to display.
    /// </summary>
    public required string Message { get; init; }

    /// <summary>
    ///     Gets the name of the speaker, if any.
    /// </summary>
    public string? SpeakerName { get; init; }

    /// <summary>
    ///     Gets the display priority for dialogue layering. Higher values appear above lower values.
    ///     Defaults to 0.
    /// </summary>
    public int DisplayPriority { get; init; }

    /// <summary>
    ///     Gets the optional tint color to apply to the dialogue display.
    /// </summary>
    public Color? Tint { get; init; }

    /// <summary>
    ///     Gets the timestamp when this event was raised.
    /// </summary>
    public required float Timestamp { get; init; }

    /// <summary>
    ///     Gets the priority level for event processing.
    /// </summary>
    public required EventPriority Priority { get; init; }
}

/// <summary>
///     Event raised when all active dialogue should be cleared from the screen.
/// </summary>
public readonly struct ClearDialogueRequestedEvent : IEcsEvent
{
    /// <summary>
    ///     Gets the timestamp when this event was raised.
    /// </summary>
    public required float Timestamp { get; init; }

    /// <summary>
    ///     Gets the priority level for event processing.
    /// </summary>
    public required EventPriority Priority { get; init; }
}

/// <summary>
///     Event raised when a visual effect should be displayed at a specific position.
/// </summary>
public readonly struct EffectRequestedEvent : IEcsEvent
{
    /// <summary>
    ///     Gets the unique identifier for the effect to display.
    /// </summary>
    public required string EffectId { get; init; }

    /// <summary>
    ///     Gets the screen position where the effect should be displayed.
    /// </summary>
    public required Point Position { get; init; }

    /// <summary>
    ///     Gets the duration of the effect in seconds. A value of 0 means the effect runs until completion.
    ///     Defaults to 0.
    /// </summary>
    public float Duration { get; init; }

    /// <summary>
    ///     Gets the scale factor to apply to the effect rendering.
    ///     A value of 0 means no scaling was specified (use 1.0 as default in handler).
    /// </summary>
    public float Scale { get; init; }

    /// <summary>
    ///     Gets the optional tint color to apply to the effect.
    /// </summary>
    public Color? Tint { get; init; }

    /// <summary>
    ///     Gets the timestamp when this event was raised.
    /// </summary>
    public required float Timestamp { get; init; }

    /// <summary>
    ///     Gets the priority level for event processing.
    /// </summary>
    public required EventPriority Priority { get; init; }
}

/// <summary>
///     Event raised when all active visual effects should be cleared from the screen.
/// </summary>
public readonly struct ClearEffectsRequestedEvent : IEcsEvent
{
    /// <summary>
    ///     Gets the timestamp when this event was raised.
    /// </summary>
    public required float Timestamp { get; init; }

    /// <summary>
    ///     Gets the priority level for event processing.
    /// </summary>
    public required EventPriority Priority { get; init; }
}

#endregion
