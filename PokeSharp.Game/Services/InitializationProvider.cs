using PokeSharp.Game.Diagnostics;
using PokeSharp.Game.Initialization;
using PokeSharp.Game.Input;

namespace PokeSharp.Game.Services;

/// <summary>
///     Default implementation of IInitializationProvider that wraps initialization infrastructure.
///     Facade simplifies DI by reducing initialization dependencies from 3 to 1.
/// </summary>
/// <remarks>
///     Uses primary constructor pattern (C# 12) for concise initialization.
///     Follows exact pattern from Phase 5 LoggingProvider, Phase 4B GameServicesProvider,
///     and Phase 6 TestingInfrastructureProvider.
/// </remarks>
public class InitializationProvider(
    PerformanceMonitor performanceMonitor,
    InputManager inputManager,
    PlayerFactory playerFactory
) : IInitializationProvider
{
    private readonly PerformanceMonitor _performanceMonitor =
        performanceMonitor ?? throw new ArgumentNullException(nameof(performanceMonitor));
    private readonly InputManager _inputManager =
        inputManager ?? throw new ArgumentNullException(nameof(inputManager));
    private readonly PlayerFactory _playerFactory =
        playerFactory ?? throw new ArgumentNullException(nameof(playerFactory));

    /// <inheritdoc />
    public PerformanceMonitor PerformanceMonitor => _performanceMonitor;

    /// <inheritdoc />
    public InputManager InputManager => _inputManager;

    /// <inheritdoc />
    public PlayerFactory PlayerFactory => _playerFactory;
}
