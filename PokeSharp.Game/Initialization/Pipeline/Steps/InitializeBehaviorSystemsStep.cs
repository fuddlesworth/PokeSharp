using Microsoft.Extensions.Logging;
using PokeSharp.Engine.Scenes;
using PokeSharp.Game.Initialization.Behaviors;

namespace PokeSharp.Game.Initialization.Pipeline.Steps;

/// <summary>
///     Initialization step that initializes NPC and tile behavior systems.
/// </summary>
public class InitializeBehaviorSystemsStep : InitializationStepBase
{
    /// <summary>
    ///     Initializes a new instance of the <see cref="InitializeBehaviorSystemsStep" /> class.
    /// </summary>
    public InitializeBehaviorSystemsStep()
        : base(
            "Initializing behavior systems...",
            InitializationProgress.GameSystemsInitialized,
            InitializationProgress.GameSystemsInitialized
        ) { }

    /// <inheritdoc />
    protected override async Task ExecuteStepAsync(
        InitializationContext context,
        LoadingProgress progress,
        CancellationToken cancellationToken
    )
    {
        if (context.GameInitializer == null)
            throw new InvalidOperationException(
                "GameInitializer must be initialized before behavior systems"
            );

        var logger = context.LoggerFactory.CreateLogger<InitializeBehaviorSystemsStep>();

        // Initialize NPC behavior system
        var npcBehaviorInitializerLogger =
            context.LoggerFactory.CreateLogger<NPCBehaviorInitializer>();
        var npcBehaviorInitializer = new NPCBehaviorInitializer(
            npcBehaviorInitializerLogger,
            context.LoggerFactory,
            context.World,
            context.SystemManager,
            context.BehaviorRegistry,
            context.ScriptService,
            context.ApiProvider
        );
        await npcBehaviorInitializer.InitializeAsync();

        // Initialize tile behavior system
        var tileBehaviorInitializerLogger =
            context.LoggerFactory.CreateLogger<TileBehaviorInitializer>();
        var tileBehaviorInitializer = new TileBehaviorInitializer(
            tileBehaviorInitializerLogger,
            context.LoggerFactory,
            context.World,
            context.SystemManager,
            context.TileBehaviorRegistry,
            context.ScriptService,
            context.ApiProvider,
            context.GameInitializer.CollisionService
        );
        await tileBehaviorInitializer.InitializeAsync();

        // Set sprite texture loader in MapInitializer (must be called after MapInitializer is created)
        if (context.SpriteTextureLoader != null && context.MapInitializer != null)
            context.MapInitializer.SetSpriteTextureLoader(context.SpriteTextureLoader);

        logger.LogInformation("Behavior systems initialized successfully");
    }
}
