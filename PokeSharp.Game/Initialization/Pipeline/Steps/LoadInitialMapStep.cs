using Microsoft.Extensions.Logging;
using PokeSharp.Engine.Scenes;

namespace PokeSharp.Game.Initialization.Pipeline.Steps;

/// <summary>
///     Initialization step that loads the initial map (definition-based loading).
/// </summary>
public class LoadInitialMapStep : InitializationStepBase
{
    /// <summary>
    ///     Initializes a new instance of the <see cref="LoadInitialMapStep" /> class.
    /// </summary>
    public LoadInitialMapStep()
        : base(
            "Loading map...",
            InitializationProgress.GameSystemsInitialized,
            InitializationProgress.InitialMapLoaded
        ) { }

    /// <inheritdoc />
    protected override async Task ExecuteStepAsync(
        InitializationContext context,
        LoadingProgress progress,
        CancellationToken cancellationToken
    )
    {
        if (context.MapInitializer == null)
            throw new InvalidOperationException(
                "MapInitializer must be created before loading map"
            );

        var logger = context.LoggerFactory.CreateLogger<LoadInitialMapStep>();
        await context.MapInitializer.LoadMap(context.Configuration.Initialization.InitialMap);
        logger.LogInformation("Initial map loaded successfully");
    }
}
