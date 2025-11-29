namespace PokeSharp.Game.Components.Rendering;

/// <summary>
///     Component that tracks the current animation state for an entity.
///     Works in conjunction with Sprite component to update frame rendering.
/// </summary>
public struct Animation
{
    /// <summary>
    ///     Gets or sets the name of the currently playing animation.
    ///     This references an animation in the sprite's manifest (e.g., "face_south", "go_north").
    /// </summary>
    public string CurrentAnimation { get; set; }

    /// <summary>
    ///     Gets or sets the current frame index in the animation sequence.
    /// </summary>
    public int CurrentFrame { get; set; }

    /// <summary>
    ///     Gets or sets the time elapsed since the current frame started (in seconds).
    /// </summary>
    public float FrameTimer { get; set; }

    /// <summary>
    ///     Gets or sets whether the animation is currently playing.
    /// </summary>
    public bool IsPlaying { get; set; }

    /// <summary>
    ///     Gets or sets whether the animation has completed (for non-looping animations or PlayOnce).
    /// </summary>
    public bool IsComplete { get; set; }

    /// <summary>
    ///     Gets or sets whether the animation should play only once regardless of manifest Loop setting.
    ///     When true, the animation will set IsComplete=true after one full cycle.
    ///     Used for turn-in-place animations (Pokemon Emerald WALK_IN_PLACE_FAST behavior).
    /// </summary>
    public bool PlayOnce { get; set; }

    /// <summary>
    ///     Gets or sets the bit field of frame indices that have already triggered their events.
    ///     Used to prevent re-triggering events when frame hasn't changed.
    ///     Reset when animation changes or loops.
    ///     Each bit represents a frame index (supports up to 64 frames).
    ///     Zero-allocation alternative to HashSet.
    /// </summary>
    public ulong TriggeredEventFrames { get; set; }

    /// <summary>
    ///     Initializes a new instance of the Animation struct.
    /// </summary>
    /// <param name="animationName">The initial animation name.</param>
    public Animation(string animationName)
    {
        CurrentAnimation = animationName;
        CurrentFrame = 0;
        FrameTimer = 0f;
        IsPlaying = true;
        IsComplete = false;
        PlayOnce = false;
        TriggeredEventFrames = 0;
    }
}
