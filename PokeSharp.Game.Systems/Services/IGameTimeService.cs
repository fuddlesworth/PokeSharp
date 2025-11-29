namespace PokeSharp.Game.Systems.Services;

/// <summary>
///     Provides access to game time information.
///     Tracks elapsed time since game start for timestamps and time-based logic.
/// </summary>
public interface IGameTimeService
{
    /// <summary>
    ///     Gets the total elapsed game time in seconds since game start.
    /// </summary>
    float TotalSeconds { get; }

    /// <summary>
    ///     Gets the total elapsed game time in milliseconds since game start.
    /// </summary>
    double TotalMilliseconds { get; }

    /// <summary>
    ///     Gets the time since the last frame in seconds (delta time).
    /// </summary>
    float DeltaTime { get; }

    /// <summary>
    ///     Updates the game time. Should be called once per frame.
    /// </summary>
    /// <param name="totalSeconds">Total elapsed time since game start in seconds.</param>
    /// <param name="deltaTime">Time since last frame in seconds.</param>
    void Update(float totalSeconds, float deltaTime);
}