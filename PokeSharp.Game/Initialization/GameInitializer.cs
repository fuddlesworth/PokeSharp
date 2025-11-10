using Arch.Core;
using Microsoft.Extensions.Logging;
using Microsoft.Xna.Framework.Graphics;
using PokeSharp.Core.Factories;
using PokeSharp.Core.Logging;
using PokeSharp.Core.Parallel;
using PokeSharp.Core.Pooling;
using PokeSharp.Core.Systems;
using PokeSharp.Game.Diagnostics;
using PokeSharp.Input.Systems;
using PokeSharp.Rendering.Animation;
using PokeSharp.Rendering.Assets;
using PokeSharp.Rendering.Loaders;
using PokeSharp.Rendering.Systems;

namespace PokeSharp.Game.Initialization;

/// <summary>
///     Initializes the core game systems, managers, and infrastructure.
/// </summary>
public class GameInitializer(
    ILogger<GameInitializer> logger,
    ILoggerFactory loggerFactory,
    World world,
    SystemManager systemManager,
    AssetManager assetManager,
    IEntityFactoryService entityFactory,
    MapLoader mapLoader
)
{
    private readonly AssetManager _assetManager = assetManager;
    private readonly IEntityFactoryService _entityFactory = entityFactory;
    private readonly ILogger<GameInitializer> _logger = logger;
    private readonly ILoggerFactory _loggerFactory = loggerFactory;
    private readonly MapLoader _mapLoader = mapLoader;
    private readonly SystemManager _systemManager = systemManager;
    private readonly World _world = world;
    private EntityPoolManager _poolManager = null!;
    private ComponentPoolManager _componentPoolManager = null!;

    /// <summary>
    ///     Gets the animation library.
    /// </summary>
    public AnimationLibrary AnimationLibrary { get; private set; } = null!;

    /// <summary>
    ///     Gets the spatial hash system.
    /// </summary>
    public SpatialHashSystem SpatialHashSystem { get; private set; } = null!;

    /// <summary>
    ///     Gets the render system.
    /// </summary>
    public ZOrderRenderSystem RenderSystem { get; private set; } = null!;

    /// <summary>
    ///     Gets the entity pool manager.
    /// </summary>
    public EntityPoolManager PoolManager => _poolManager;

    /// <summary>
    ///     Gets the component pool manager for temporary component operations.
    /// </summary>
    public ComponentPoolManager ComponentPoolManager => _componentPoolManager;

    /// <summary>
    ///     Initializes all game systems and infrastructure.
    /// </summary>
    /// <param name="graphicsDevice">The graphics device for rendering.</param>
    public void Initialize(GraphicsDevice graphicsDevice)
    {
        // Load asset manifest
        try
        {
            _assetManager.LoadManifest();
            _logger.LogResourceLoaded("Manifest", "Assets/manifest.json");

            // Run diagnostics
            AssetDiagnostics.PrintAssetManagerStatus(_assetManager, _logger);
        }
        catch (Exception ex)
        {
            _logger.LogOperationFailedWithRecovery(
                "Load manifest",
                "Continuing with empty asset manager"
            );
            _logger.LogDebug(ex, "Manifest load exception details");
        }

        // Create animation library with default player animations
        AnimationLibrary = new AnimationLibrary();
        _logger.LogComponentInitialized("AnimationLibrary", AnimationLibrary.Count);

        // Initialize entity pooling system
        _poolManager = new EntityPoolManager(_world);

        // Register and warmup pools for common entity types
        _poolManager.RegisterPool("player", initialSize: 1, maxSize: 10, warmup: true);
        _poolManager.RegisterPool("npc", initialSize: 20, maxSize: 100, warmup: true);
        _poolManager.RegisterPool("tile", initialSize: 2000, maxSize: 5000, warmup: true);

        _logger.LogInformation(
            "Entity pool manager initialized with {NPCPoolSize} NPC, {PlayerPoolSize} player, and {TilePoolSize} tile pool capacity",
            20,
            1,
            2000
        );

        // Initialize component pooling system (Phase 4B)
        // Pools frequently-used components for temporary operations (copying, calculations)
        var componentPoolLogger = _loggerFactory.CreateLogger<ComponentPoolManager>();
        _componentPoolManager = new ComponentPoolManager(componentPoolLogger, enableStatistics: true);

        _logger.LogInformation(
            "Component pool manager initialized for temporary component operations (Position, Velocity, Sprite, Animation, GridMovement)"
        );

        // Create and register systems in priority order

        // === Update Systems (Logic Only) ===

        // SpatialHashSystem (Priority: 25) - must run early to build spatial index
        var spatialHashLogger = _loggerFactory.CreateLogger<SpatialHashSystem>();
        SpatialHashSystem = new SpatialHashSystem(spatialHashLogger);
        _systemManager.RegisterUpdateSystem(SpatialHashSystem);

        // Register pool management systems
        var poolCleanupLogger = _loggerFactory.CreateLogger<PoolCleanupSystem>();
        _systemManager.RegisterUpdateSystem(new PoolCleanupSystem(_poolManager, poolCleanupLogger));

        // InputSystem with Pokemon-style input buffering (5 inputs, 200ms timeout)
        var inputLogger = _loggerFactory.CreateLogger<InputSystem>();
        var inputSystem = new InputSystem(5, 0.2f, inputLogger);
        _systemManager.RegisterUpdateSystem(inputSystem);

        // Register MovementSystem (Priority: 100, handles movement and collision checking)
        var movementLogger = _loggerFactory.CreateLogger<MovementSystem>();
        var movementSystem = new MovementSystem(SpatialHashSystem, movementLogger);
        _systemManager.RegisterUpdateSystem(movementSystem);

        // Register CollisionSystem (Priority: 200, provides tile collision checking)
        var collisionLogger = _loggerFactory.CreateLogger<CollisionSystem>();
        var collisionSystem = new CollisionSystem(SpatialHashSystem, collisionLogger);
        _systemManager.RegisterUpdateSystem(collisionSystem);

        // Register AnimationSystem (Priority: 800, after movement, before rendering)
        var animationLogger = _loggerFactory.CreateLogger<AnimationSystem>();
        _systemManager.RegisterUpdateSystem(new AnimationSystem(AnimationLibrary, animationLogger));

        // Register CameraFollowSystem (Priority: 825, after Animation, before TileAnimation)
        var cameraFollowLogger = _loggerFactory.CreateLogger<CameraFollowSystem>();
        _systemManager.RegisterUpdateSystem(new CameraFollowSystem(cameraFollowLogger));

        // Register TileAnimationSystem (Priority: 850, animates water/grass tiles between Animation and Render)
        var tileAnimLogger = _loggerFactory.CreateLogger<TileAnimationSystem>();
        _systemManager.RegisterUpdateSystem(new TileAnimationSystem(tileAnimLogger));

        // NOTE: NPCBehaviorSystem is registered separately in NPCBehaviorInitializer
        // It requires ScriptService and behavior registry to be set up first

        // === Render Systems (Rendering Only) ===

        // Register ZOrderRenderSystem (Priority: 1000) - unified rendering with Z-order sorting
        var renderLogger = _loggerFactory.CreateLogger<ZOrderRenderSystem>();
        RenderSystem = new ZOrderRenderSystem(graphicsDevice, _assetManager, renderLogger);
        _systemManager.RegisterRenderSystem(RenderSystem);

        // Initialize all systems
        _systemManager.Initialize(_world);

        // Build parallel execution plan for inter-system parallelism
        if (_systemManager is ParallelSystemManager parallelManager)
        {
            parallelManager.RebuildExecutionPlan();
            _logger.LogWorkflowStatus("Parallel execution plan ready");

            // Log execution stages for debugging
            LogExecutionStages(parallelManager);
        }

        _logger.LogWorkflowStatus("Game initialization complete");
    }

    private void LogExecutionStages(ParallelSystemManager parallelManager)
    {
        var executionStages = parallelManager.ExecutionStages;
        if (executionStages == null || executionStages.Count == 0)
            return;

        // Log each stage with structured formatting
        for (int i = 0; i < executionStages.Count; i++)
        {
            var stage = executionStages[i];
            var stageNumber = i + 1;

            // Format the systems list with priorities
            var systemsList = stage
                .Select(s => $"{s.GetType().Name} (P:{s.Priority})")
                .ToList();

            _logger.LogWorkflowStatus(
                $"Stage {stageNumber}",
                ("parallel", stage.Count),
                ("systems", string.Join(", ", systemsList))
            );
        }
    }
}
