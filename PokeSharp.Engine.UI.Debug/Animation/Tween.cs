using System;

namespace PokeSharp.Engine.UI.Debug.Animation;

/// <summary>
/// Advanced animator with easing functions and configurable duration.
/// Supports timed animations with various easing curves.
/// </summary>
public class Tween
{
    private float _startValue;
    private float _endValue;
    private float _currentTime;
    private float _duration;
    private EasingType _easing;
    private bool _isPlaying;
    private bool _isComplete;

    /// <summary>
    /// Gets the current animated value.
    /// </summary>
    public float CurrentValue { get; private set; }

    /// <summary>
    /// Gets the start value of the animation.
    /// </summary>
    public float StartValue => _startValue;

    /// <summary>
    /// Gets the end/target value of the animation.
    /// </summary>
    public float EndValue => _endValue;

    /// <summary>
    /// Gets the animation duration in seconds.
    /// </summary>
    public float Duration => _duration;

    /// <summary>
    /// Gets the easing function type.
    /// </summary>
    public EasingType Easing => _easing;

    /// <summary>
    /// Gets whether the tween is currently playing.
    /// </summary>
    public bool IsPlaying => _isPlaying;

    /// <summary>
    /// Gets whether the tween has completed.
    /// </summary>
    public bool IsComplete => _isComplete;

    /// <summary>
    /// Gets the normalized time (0-1) of the animation.
    /// </summary>
    public float NormalizedTime => _duration > 0 ? Math.Clamp(_currentTime / _duration, 0f, 1f) : 1f;

    /// <summary>
    /// Event fired when the tween completes.
    /// </summary>
    public event Action? OnComplete;

    /// <summary>
    /// Creates a new tween.
    /// </summary>
    public Tween(float startValue, float endValue, float duration, EasingType easing = EasingType.Linear)
    {
        _startValue = startValue;
        _endValue = endValue;
        _duration = Math.Max(0, duration);
        _easing = easing;
        CurrentValue = startValue;
        _currentTime = 0;
        _isPlaying = false;
        _isComplete = false;
    }

    /// <summary>
    /// Starts or restarts the tween.
    /// </summary>
    public void Play()
    {
        _currentTime = 0;
        _isPlaying = true;
        _isComplete = false;
        CurrentValue = _startValue;
    }

    /// <summary>
    /// Pauses the tween.
    /// </summary>
    public void Pause()
    {
        _isPlaying = false;
    }

    /// <summary>
    /// Resumes the tween from its current position.
    /// </summary>
    public void Resume()
    {
        if (!_isComplete)
        {
            _isPlaying = true;
        }
    }

    /// <summary>
    /// Stops the tween and resets to start value.
    /// </summary>
    public void Stop()
    {
        _isPlaying = false;
        _isComplete = false;
        _currentTime = 0;
        CurrentValue = _startValue;
    }

    /// <summary>
    /// Immediately completes the tween, jumping to the end value.
    /// </summary>
    public void Complete()
    {
        _currentTime = _duration;
        CurrentValue = _endValue;
        _isPlaying = false;
        _isComplete = true;
        OnComplete?.Invoke();
    }

    /// <summary>
    /// Updates the tween animation.
    /// </summary>
    public void Update(float deltaTime)
    {
        if (!_isPlaying || _isComplete)
            return;

        _currentTime += deltaTime;

        if (_currentTime >= _duration)
        {
            // Animation complete
            _currentTime = _duration;
            CurrentValue = _endValue;
            _isPlaying = false;
            _isComplete = true;
            OnComplete?.Invoke();
        }
        else
        {
            // Interpolate using easing function
            float t = NormalizedTime;
            CurrentValue = EasingFunctions.Lerp(_startValue, _endValue, t, _easing);
        }
    }

    /// <summary>
    /// Changes the target value, creating a new animation from current value to new target.
    /// </summary>
    public void SetTarget(float newEndValue, float? newDuration = null)
    {
        _startValue = CurrentValue;
        _endValue = newEndValue;

        if (newDuration.HasValue)
        {
            _duration = Math.Max(0, newDuration.Value);
        }

        _currentTime = 0;
        _isPlaying = true;
        _isComplete = false;
    }

    /// <summary>
    /// Changes the easing function.
    /// </summary>
    public void SetEasing(EasingType easing)
    {
        _easing = easing;
    }

    /// <summary>
    /// Factory method to create and immediately start a tween.
    /// </summary>
    public static Tween To(float from, float to, float duration, EasingType easing = EasingType.Linear)
    {
        var tween = new Tween(from, to, duration, easing);
        tween.Play();
        return tween;
    }
}

