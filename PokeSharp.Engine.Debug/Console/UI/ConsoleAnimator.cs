using PokeSharp.Engine.Debug.Console.Configuration;

namespace PokeSharp.Engine.Debug.Console.UI;

/// <summary>
///     Handles the Quake-style slide-down/slide-up animation for the console.
/// </summary>
public class ConsoleAnimator
{
    private const float AnimationSpeed = ConsoleConstants.Animation.SlideSpeed;
    private float _targetY;

    /// <summary>
    ///     Gets whether the animation is currently running.
    /// </summary>
    public bool IsAnimating => Math.Abs(CurrentY - _targetY) > 0.1f;

    /// <summary>
    ///     Gets the current Y position.
    /// </summary>
    public float CurrentY { get; private set; }

    /// <summary>
    ///     Initializes the animator with a starting position.
    /// </summary>
    /// <param name="initialY">The initial Y position (typically negative to hide).</param>
    public void Initialize(float initialY)
    {
        CurrentY = initialY;
        _targetY = initialY;
    }

    /// <summary>
    ///     Shows the console by sliding down.
    /// </summary>
    public void Show()
    {
        _targetY = 0;
    }

    /// <summary>
    ///     Hides the console by sliding up.
    /// </summary>
    /// <param name="hideY">The Y position when hidden (typically negative).</param>
    public void Hide(float hideY)
    {
        _targetY = hideY;
    }

    /// <summary>
    ///     Updates the animation and returns the new position.
    /// </summary>
    /// <param name="deltaTime">Time elapsed since last frame in seconds.</param>
    /// <returns>The updated Y position.</returns>
    public float Update(float deltaTime)
    {
        if (!IsAnimating)
            return CurrentY;

        var direction = Math.Sign(_targetY - CurrentY);
        var distance = AnimationSpeed * deltaTime;

        CurrentY += direction * distance;

        // Clamp to target
        if (direction > 0)
            CurrentY = Math.Min(CurrentY, _targetY);
        else
            CurrentY = Math.Max(CurrentY, _targetY);

        return CurrentY;
    }
}
