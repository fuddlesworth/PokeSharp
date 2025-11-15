using Microsoft.Extensions.Logging;
using Microsoft.Xna.Framework;
using PokeSharp.Game.Components.Movement;
using PokeSharp.Game.Scripting.Services;
using PokeSharp.Game.Systems.Services;

namespace PokeSharp.Game.Scripting.Runtime;

/// <summary>
///     Base class for type behavior scripts using the ScriptContext pattern.
/// </summary>
/// <remarks>
///     <para>
///         All .csx behavior scripts should define a class that inherits from TypeScriptBase.
///         The class will be instantiated once and reused across all ticks.
///     </para>
///     <para>
///         <strong>IMPORTANT:</strong> Scripts are stateless! DO NOT use instance fields or properties.
///         Use <c>ctx.GetState&lt;T&gt;()</c> and <c>ctx.SetState&lt;T&gt;()</c> for persistent data.
///     </para>
///     <example>
///         <code>
/// public class MyScript : TypeScriptBase
/// {
///     // ❌ WRONG - instance state will break with multiple entities
///     private int counter;
///
///     // ✅ CORRECT - use ScriptContext for state
///     protected override void OnTick(ScriptContext ctx, float deltaTime)
///     {
///         var counter = ctx.GetState&lt;int&gt;("counter");
///         counter++;
///         ctx.SetState("counter", counter);
///
///         // Access ECS world, entity, logger via context
///         var position = ctx.World.Get&lt;Position&gt;(ctx.Entity);
///         ctx.Logger?.LogInformation("Position: {Pos}", position);
///     }
/// }
/// </code>
///     </example>
/// </remarks>
public abstract class TypeScriptBase
{
    // NO INSTANCE FIELDS OR PROPERTIES!
    // Scripts must be stateless - use ScriptContext.GetState<T>() for persistent data.

    // ============================================================================
    // Lifecycle Hooks
    // ============================================================================

    /// <summary>
    ///     Called once when script is loaded.
    ///     Override to set up initial state or cache data.
    /// </summary>
    /// <param name="ctx">Script execution context providing access to World, Entity, Logger, and state.</param>
    /// <remarks>
    ///     Use <c>ctx.SetState&lt;T&gt;(key, value)</c> to initialize persistent data.
    /// </remarks>
    public virtual void OnInitialize(ScriptContext ctx) { }

    /// <summary>
    ///     Called when the type is activated on an entity or globally.
    ///     Override to handle activation logic.
    /// </summary>
    /// <param name="ctx">Script execution context providing access to World, Entity, Logger, and state.</param>
    public virtual void OnActivated(ScriptContext ctx) { }

    /// <summary>
    ///     Called every frame while the type is active.
    ///     Override to implement per-frame behavior logic.
    /// </summary>
    /// <param name="ctx">Script execution context providing access to World, Entity, Logger, and state.</param>
    /// <param name="deltaTime">Time elapsed since last frame (in seconds).</param>
    public virtual void OnTick(ScriptContext ctx, float deltaTime) { }

    /// <summary>
    ///     Called when the type is deactivated from an entity or globally.
    ///     Override to handle cleanup logic.
    /// </summary>
    /// <param name="ctx">Script execution context providing access to World, Entity, Logger, and state.</param>
    public virtual void OnDeactivated(ScriptContext ctx) { }
}
