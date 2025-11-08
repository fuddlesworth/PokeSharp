using System.Collections.Concurrent;
using Arch.Core;
using Microsoft.Extensions.Logging;
using PokeSharp.Core.Components.Movement;
using PokeSharp.Core.Components.NPCs;
using PokeSharp.Core.Scripting.Services;
using PokeSharp.Core.ScriptingApi;
using PokeSharp.Core.Systems;
using PokeSharp.Core.Types;
using PokeSharp.Scripting.Runtime;

namespace PokeSharp.Game.Systems;

/// <summary>
///     System responsible for executing NPC behavior scripts using the ScriptContext pattern.
///     Queries entities with behavior data and executes their OnTick methods.
/// </summary>
/// <remarks>
///     CLEAN ARCHITECTURE:
///     This system uses the unified TypeScriptBase pattern with ScriptContext instances.
///     Scripts are cached as singletons in TypeRegistry and executed with per-tick
///     ScriptContext instances to prevent state corruption.
/// </remarks>
public class NPCBehaviorSystem : BaseSystem
{
    private readonly ILogger<NPCBehaviorSystem> _logger;
    private readonly ILoggerFactory _loggerFactory;
    private readonly IScriptingApiProvider _apis;
    private readonly ConcurrentDictionary<string, ILogger> _scriptLoggerCache = new();
    private TypeRegistry<BehaviorDefinition>? _behaviorRegistry;

    public NPCBehaviorSystem(
        ILogger<NPCBehaviorSystem> logger,
        ILoggerFactory loggerFactory,
        IScriptingApiProvider apis
    )
    {
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        _loggerFactory = loggerFactory ?? throw new ArgumentNullException(nameof(loggerFactory));
        _apis = apis ?? throw new ArgumentNullException(nameof(apis));
    }

    /// <summary>
    ///     System priority for NPC behaviors - runs after spatial hash, before movement.
    /// </summary>
    public override int Priority => SystemPriority.NpcBehavior;

    /// <summary>
    ///     Set the behavior registry for loading behavior scripts.
    /// </summary>
    public void SetBehaviorRegistry(TypeRegistry<BehaviorDefinition> registry)
    {
        _behaviorRegistry = registry;
        _logger.LogInformation("Behavior registry set with {Count} behaviors", registry.Count);
    }

    public override void Initialize(World world)
    {
        base.Initialize(world);
        _logger.LogInformation("NPCBehaviorSystem initialized");
    }

    public override void Update(World world, float deltaTime)
    {
        if (_behaviorRegistry == null)
        {
            _logger.LogWarning("Behavior registry not set on NPCBehaviorSystem");
            return;
        }

        // Query all NPCs with behavior data
        var query = new QueryDescription().WithAll<Npc, Behavior, Position>();

        var behaviorCount = 0;
        var errorCount = 0;

        world.Query(
            in query,
            (Entity entity, ref Npc npc, ref Behavior behavior) =>
            {
                try
                {
                    // Skip if behavior is not active
                    if (!behavior.IsActive)
                        return;

                    // Get script from registry
                    var scriptObj = _behaviorRegistry.GetScript(behavior.BehaviorTypeId);
                    if (scriptObj == null)
                    {
                        _logger.LogWarning(
                            "Behavior script not found: {TypeId} (NPC: {NpcId})",
                            behavior.BehaviorTypeId,
                            npc.NpcId
                        );
                        // Deactivate behavior (with cleanup if needed)
                        DeactivateBehavior(null, ref behavior, null, "script not found");
                        return;
                    }

                    // Cast to TypeScriptBase
                    if (scriptObj is not TypeScriptBase script)
                    {
                        _logger.LogError(
                            "Script is not TypeScriptBase: {TypeId} (Type: {ActualType})",
                            behavior.BehaviorTypeId,
                            scriptObj.GetType().Name
                        );
                        // Deactivate behavior (with cleanup if needed)
                        DeactivateBehavior(null, ref behavior, null, "wrong script type");
                        return;
                    }

                    // Create ScriptContext for this entity (with cached logger and API services)
                    var scriptLogger = _scriptLoggerCache.GetOrAdd(
                        $"{behavior.BehaviorTypeId}.{npc.NpcId}",
                        key => _loggerFactory.CreateLogger($"Script.{key}")
                    );
                    var context = new ScriptContext(
                        world,
                        entity,
                        scriptLogger,
                        _apis
                    );

                    // Initialize on first tick
                    if (!behavior.IsInitialized)
                    {
                        _logger.LogInformation(
                            "Activating behavior script for NPC {NpcId}, type {TypeId}",
                            npc.NpcId,
                            behavior.BehaviorTypeId
                        );

                        script.OnActivated(context);
                        behavior.IsInitialized = true;
                    }

                    // Execute tick
                    script.OnTick(context, deltaTime);
                    behaviorCount++;
                }
                catch (Exception ex)
                {
                    // Isolate errors - one NPC's script error shouldn't crash all behaviors
                    _logger.LogError(
                        ex,
                        "Script error for NPC {NpcId}: {Message}",
                        npc.NpcId,
                        ex.Message
                    );
                    errorCount++;

                    // Deactivate behavior with cleanup
                    var scriptObj = _behaviorRegistry.GetScript(behavior.BehaviorTypeId);
                    if (scriptObj is TypeScriptBase script)
                    {
                        var scriptLogger = _scriptLoggerCache.GetOrAdd(
                            $"{behavior.BehaviorTypeId}.{npc.NpcId}",
                            key => _loggerFactory.CreateLogger($"Script.{key}")
                        );
                        var context = new ScriptContext(world, entity, scriptLogger, _apis);
                        DeactivateBehavior(script, ref behavior, context, $"error: {ex.Message}");
                    }
                    else
                    {
                        behavior.IsActive = false;
                    }
                }
            }
        );

        // Log performance metrics periodically
        if (behaviorCount > 0)
            _logger.LogTrace(
                "Executed {Count} NPC behaviors ({Errors} errors)",
                behaviorCount,
                errorCount
            );
    }

    /// <summary>
    ///     Safely deactivates a behavior by calling OnDeactivated and cleaning up state.
    /// </summary>
    /// <param name="script">The behavior script instance (null if not available).</param>
    /// <param name="behavior">Reference to the behavior component.</param>
    /// <param name="context">Script context for cleanup (null if not available).</param>
    /// <param name="reason">Reason for deactivation (for logging).</param>
    private void DeactivateBehavior(
        TypeScriptBase? script,
        ref Behavior behavior,
        ScriptContext? context,
        string reason
    )
    {
        if (behavior.IsInitialized && script != null && context != null)
        {
            try
            {
                script.OnDeactivated(context);
                _logger.LogInformation(
                    "Deactivated behavior {TypeId}: {Reason}",
                    behavior.BehaviorTypeId,
                    reason
                );
            }
            catch (Exception ex)
            {
                _logger.LogError(
                    ex,
                    "Error during OnDeactivated for {TypeId}: {Message}",
                    behavior.BehaviorTypeId,
                    ex.Message
                );
            }

            behavior.IsInitialized = false;
        }

        behavior.IsActive = false;
    }
}
