namespace PokeSharp.Game.Systems.Services;

/// <summary>
///     Default implementation of IGameTimeService.
///     Tracks game time for timestamps and time-based game logic.
/// </summary>
public class GameTimeService : IGameTimeService
{
    private float _totalSeconds;
    private float _deltaTime;

    /// <inheritdoc />
    public float TotalSeconds => _totalSeconds;

    /// <inheritdoc />
    public double TotalMilliseconds => _totalSeconds * 1000.0;

    /// <inheritdoc />
    public float DeltaTime => _deltaTime;

    /// <inheritdoc />
    public void Update(float totalSeconds, float deltaTime)
    {
        _totalSeconds = totalSeconds;
        _deltaTime = deltaTime;
    }
}
