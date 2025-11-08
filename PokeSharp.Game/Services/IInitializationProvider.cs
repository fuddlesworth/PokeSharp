using PokeSharp.Game.Diagnostics;
using PokeSharp.Game.Initialization;
using PokeSharp.Game.Input;

namespace PokeSharp.Game.Services;

/// <summary>
///     Provides unified access to game initialization infrastructure.
///     Facade simplifies DI by grouping initialization helpers into single provider.
/// </summary>
/// <remarks>
///     This pattern follows Phase 3 (IScriptingApiProvider), Phase 4B (IGameServicesProvider),
///     Phase 5 (ILoggingProvider), and Phase 6 (ITestingInfrastructureProvider) to maintain
///     architectural consistency across all facade implementations.
/// </remarks>
public interface IInitializationProvider
{
    /// <summary>
    ///     Gets the performance monitor for tracking frame times and diagnostics.
    /// </summary>
    /// <remarks>
    ///     Used to monitor and log performance metrics during game execution.
    ///     Updates every frame with timing information.
    /// </remarks>
    PerformanceMonitor PerformanceMonitor { get; }

    /// <summary>
    ///     Gets the input manager for processing player input.
    /// </summary>
    /// <remarks>
    ///     Handles keyboard/mouse input, zoom controls, and debug commands.
    ///     Processes input every frame and updates world state accordingly.
    /// </remarks>
    InputManager InputManager { get; }

    /// <summary>
    ///     Gets the player factory for creating player entities.
    /// </summary>
    /// <remarks>
    ///     Factory pattern for player entity creation with proper component setup.
    ///     Used during initialization to spawn the player in the world.
    /// </remarks>
    PlayerFactory PlayerFactory { get; }
}
