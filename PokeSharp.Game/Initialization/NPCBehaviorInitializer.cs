using Arch.Core;
using Microsoft.Extensions.Logging;
using PokeSharp.Engine.Common.Logging;
using PokeSharp.Engine.Core.Types;
using PokeSharp.Engine.Systems.Management;
using PokeSharp.Game.Scripting.Api;
using PokeSharp.Game.Scripting.Systems;
using PokeSharp.Game.Services;

namespace PokeSharp.Game.Initialization;

/// <summary>
///     Initializes NPC behavior system with script compilation and type registry.
/// </summary>
public class NPCBehaviorInitializer(
    ILogger<NPCBehaviorInitializer> logger,
    ILoggerFactory loggerFactory,
    World world,
    SystemManager systemManager,
    IGameServicesProvider gameServices,
    IScriptingApiProvider apiProvider
)
{
    /// <summary>
    ///     Initializes the NPC behavior system with TypeRegistry and ScriptService.
    /// </summary>
    public void Initialize()
    {
        try
        {
            // Load all behavior definitions from JSON
            var loadedCount = gameServices.BehaviorRegistry.LoadAllAsync().Result;
            logger.LogWorkflowStatus("Behavior definitions loaded", ("count", loadedCount));

            // Load and compile behavior scripts for each type
            foreach (var typeId in gameServices.BehaviorRegistry.GetAllTypeIds())
            {
                var definition = gameServices.BehaviorRegistry.Get(typeId);
                if (
                    definition is IScriptedType scripted
                    && !string.IsNullOrEmpty(scripted.BehaviorScript)
                )
                {
                    logger.LogWorkflowStatus(
                        "Compiling behavior script",
                        ("behavior", typeId),
                        ("script", scripted.BehaviorScript)
                    );

                    var scriptInstance = gameServices
                        .ScriptService.LoadScriptAsync(scripted.BehaviorScript)
                        .Result;

                    if (scriptInstance != null)
                    {
                        // Initialize script with world
                        gameServices.ScriptService.InitializeScript(scriptInstance, world);

                        // Register script instance in the registry
                        gameServices.BehaviorRegistry.RegisterScript(typeId, scriptInstance);

                        logger.LogWorkflowStatus(
                            "Behavior ready",
                            ("behavior", typeId),
                            ("script", scripted.BehaviorScript)
                        );
                    }
                    else
                    {
                        logger.LogError(
                            "✗ Failed to compile script for {TypeId}: {Script}",
                            typeId,
                            scripted.BehaviorScript
                        );
                    }
                }
            }

            // Register NPCBehaviorSystem with API services
            var npcBehaviorLogger = loggerFactory.CreateLogger<NPCBehaviorSystem>();
            var npcBehaviorSystem = new NPCBehaviorSystem(
                npcBehaviorLogger,
                loggerFactory,
                apiProvider
            );
            npcBehaviorSystem.SetBehaviorRegistry(gameServices.BehaviorRegistry);
            systemManager.RegisterUpdateSystem(npcBehaviorSystem);

            logger.LogSystemInitialized("NPCBehaviorSystem", ("behaviors", loadedCount));
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Failed to initialize NPC behavior system");
        }
    }
}
