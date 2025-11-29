using PokeSharp.Game.Components.Rendering;

namespace PokeSharp.Game.Components.Helpers;

/// <summary>
///     Helper methods for manipulating Animation components.
///     These methods are used by systems to update component state without violating ECS principles.
///     Components should remain pure data structures (no behavior).
/// </summary>
public static class AnimationHelpers
{
    /// <summary>
    ///     Changes the current animation to a new one.
    ///     Resets frame timer and frame index unless the animation is already playing.
    /// </summary>
    /// <param name="animation">The Animation component to modify.</param>
    /// <param name="animationName">The new animation name.</param>
    /// <param name="forceRestart">Whether to restart even if already playing this animation.</param>
    /// <param name="playOnce">Whether to play the animation once (ignoring manifest Loop setting).</param>
    public static void ChangeAnimation(
        ref Animation animation,
        string animationName,
        bool forceRestart = false,
        bool playOnce = false
    )
    {
        if (animation.CurrentAnimation != animationName || forceRestart)
        {
            animation.CurrentAnimation = animationName;
            animation.CurrentFrame = 0;
            animation.FrameTimer = 0f;
            animation.IsPlaying = true;
            animation.IsComplete = false;
            animation.PlayOnce = playOnce;
            animation.TriggeredEventFrames = 0;
        }
        else if (playOnce && !animation.PlayOnce)
        {
            // Same animation but switching to PlayOnce mode - don't reset frame
            animation.PlayOnce = true;
        }
    }

    /// <summary>
    ///     Resets the animation to the first frame.
    /// </summary>
    /// <param name="animation">The Animation component to modify.</param>
    public static void Reset(ref Animation animation)
    {
        animation.CurrentFrame = 0;
        animation.FrameTimer = 0f;
        animation.IsComplete = false;
        animation.PlayOnce = false;
        animation.TriggeredEventFrames = 0;
    }

    /// <summary>
    ///     Pauses the animation.
    /// </summary>
    /// <param name="animation">The Animation component to modify.</param>
    public static void Pause(ref Animation animation)
    {
        animation.IsPlaying = false;
    }

    /// <summary>
    ///     Resumes the animation.
    /// </summary>
    /// <param name="animation">The Animation component to modify.</param>
    public static void Resume(ref Animation animation)
    {
        animation.IsPlaying = true;
        animation.IsComplete = false;
    }

    /// <summary>
    ///     Stops the animation and resets to the first frame.
    /// </summary>
    /// <param name="animation">The Animation component to modify.</param>
    public static void Stop(ref Animation animation)
    {
        animation.IsPlaying = false;
        Reset(ref animation);
    }
}
