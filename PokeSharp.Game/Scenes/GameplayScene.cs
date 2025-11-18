using Arch.Core;
using Microsoft.Extensions.Logging;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using PokeSharp.Engine.Scenes;
using PokeSharp.Engine.Systems.Management;
using PokeSharp.Game.Infrastructure.Diagnostics;
using PokeSharp.Game.Infrastructure.Services;
using PokeSharp.Game.Input;
using PokeSharp.Game.Initialization;
using PokeSharp.Game.Initialization.Initializers;
using PokeSharp.Game.Systems.Services;

namespace PokeSharp.Game.Scenes;

/// <summary>
///     Main gameplay scene that contains all game logic and rendering.
///     This scene is created after async initialization completes.
/// </summary>
public class GameplayScene : SceneBase
{
    private readonly World _world;
    private readonly SystemManager _systemManager;
    private readonly IGameInitializer _gameInitializer;
    private readonly IMapInitializer _mapInitializer;
    private readonly InputManager _inputManager;
    private readonly PerformanceMonitor _performanceMonitor;
    private readonly IGameTimeService _gameTime;

    /// <summary>
    ///     Initializes a new instance of the GameplayScene class.
    /// </summary>
    /// <param name="graphicsDevice">The graphics device.</param>
    /// <param name="services">The service provider.</param>
    /// <param name="logger">The logger.</param>
    /// <param name="world">The ECS world.</param>
    /// <param name="systemManager">The system manager.</param>
    /// <param name="gameInitializer">The game initializer.</param>
    /// <param name="mapInitializer">The map initializer.</param>
    /// <param name="inputManager">The input manager.</param>
    /// <param name="performanceMonitor">The performance monitor.</param>
    /// <param name="gameTime">The game time service.</param>
    public GameplayScene(
        GraphicsDevice graphicsDevice,
        IServiceProvider services,
        ILogger<GameplayScene> logger,
        World world,
        SystemManager systemManager,
        IGameInitializer gameInitializer,
        IMapInitializer mapInitializer,
        InputManager inputManager,
        PerformanceMonitor performanceMonitor,
        IGameTimeService gameTime
    )
        : base(graphicsDevice, services, logger)
    {
        ArgumentNullException.ThrowIfNull(world);
        ArgumentNullException.ThrowIfNull(systemManager);
        ArgumentNullException.ThrowIfNull(gameInitializer);
        ArgumentNullException.ThrowIfNull(mapInitializer);
        ArgumentNullException.ThrowIfNull(inputManager);
        ArgumentNullException.ThrowIfNull(performanceMonitor);
        ArgumentNullException.ThrowIfNull(gameTime);

        _world = world;
        _systemManager = systemManager;
        _gameInitializer = gameInitializer;
        _mapInitializer = mapInitializer;
        _inputManager = inputManager;
        _performanceMonitor = performanceMonitor;
        _gameTime = gameTime;
    }

    /// <inheritdoc />
    public override void Update(GameTime gameTime)
    {

        var deltaTime = (float)gameTime.ElapsedGameTime.TotalSeconds;
        var totalSeconds = (float)gameTime.TotalGameTime.TotalSeconds;
        var frameTimeMs = (float)gameTime.ElapsedGameTime.TotalMilliseconds;

        // Update game time service
        _gameTime.Update(totalSeconds, deltaTime);

        // Update performance monitoring
        _performanceMonitor.Update(frameTimeMs);

        // Handle input (zoom, debug controls)
        // Pass render system so InputManager can control profiling when P is pressed
        _inputManager.ProcessInput(_world, deltaTime, _gameInitializer.RenderSystem);

        // Update all systems
        _systemManager.Update(_world, deltaTime);
    }

    /// <summary>
    ///     Draws the gameplay scene.
    ///     Clears the screen and delegates rendering to SystemManager.
    /// </summary>
    /// <param name="gameTime">Provides timing information.</param>
    public override void Draw(GameTime gameTime)
    {
        // Clear screen for gameplay
        GraphicsDevice.Clear(Color.CornflowerBlue);

        // Render all systems (this includes the ElevationRenderSystem)
        _systemManager.Render(_world);
    }
}

