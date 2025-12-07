using Arch.Core;
using MonoBallFramework.Game.Ecs.Components.Movement;
using MonoBallFramework.Game.Engine.Core.Events.System;
using MonoBallFramework.Game.Scripting.Runtime;

/// <summary>
/// Look Around behavior - NPC periodically changes facing direction.
/// State stored in per-entity LookAroundState component (not instance fields).
/// </summary>
public class LookAroundBehavior : ScriptBase
{
    // NO INSTANCE FIELDS! All state in components.

    public override void Initialize(ScriptContext ctx)
    {
        base.Initialize(ctx); // CRITICAL: Set Context property for event subscriptions
        ctx.Logger.LogDebug("Look around behavior initialized (state will be created on first tick)");
    }

    public override void RegisterEventHandlers(ScriptContext ctx)
    {
        On<TickEvent>(evt =>
        {
            // Initialize state on first tick (when entity is available)
            if (!Context.HasState<LookAroundState>())
            {
                // Default directions: all 4 cardinals
                var directions = new[] { Direction.North, Direction.South, Direction.East, Direction.West };

                Context.World.Add(
                    Context.Entity.Value,
                    new LookAroundState
                    {
                        LookTimer = 0f,
                        LookInterval = 2.0f, // Default 2 seconds between looks
                        CurrentDirectionIndex = 0,
                        Directions = directions,
                        CurrentFacing = directions[0],
                    }
                );

                Context.Logger.LogInformation(
                    "Look around initialized | directions: {Count}, interval: {Interval}s",
                    directions.Length,
                    2.0f
                );
                return; // Skip first tick after initialization
            }

            // Get per-entity state (each NPC has its own)
            ref var state = ref Context.GetState<LookAroundState>();

            // Count down timer
            state.LookTimer -= evt.DeltaTime;

            if (state.LookTimer <= 0)
            {
                // Time to look in a new direction
                state.CurrentDirectionIndex = (state.CurrentDirectionIndex + 1) % state.Directions.Length;
                state.CurrentFacing = state.Directions[state.CurrentDirectionIndex];
                state.LookTimer = state.LookInterval;

                // Update facing direction in GridMovement component
                if (Context.World.Has<GridMovement>(Context.Entity.Value))
                {
                    ref var movement = ref Context.World.Get<GridMovement>(Context.Entity.Value);
                    movement.FacingDirection = state.CurrentFacing;
                }

                Context.Logger.LogDebug(
                    "Looking {Direction} (index {Index}/{Total})",
                    state.CurrentFacing,
                    state.CurrentDirectionIndex,
                    state.Directions.Length
                );
            }
        });
    }

    public override void OnUnload()
    {
        // Cleanup per-entity state
        if (Context.HasState<LookAroundState>())
        {
            Context.RemoveState<LookAroundState>();
        }

        Context.Logger.LogDebug("Look around behavior deactivated");

        // CRITICAL: Dispose event subscriptions to prevent AccessViolationException on entity destruction
        base.OnUnload();
    }
}

// Component to store look-around-specific state
public struct LookAroundState
{
    public float LookTimer;
    public float LookInterval;
    public int CurrentDirectionIndex;
    public Direction[] Directions;
    public Direction CurrentFacing;
}

return new LookAroundBehavior();
