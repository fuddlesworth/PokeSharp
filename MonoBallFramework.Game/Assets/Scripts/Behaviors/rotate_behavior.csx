using Arch.Core;
using MonoBallFramework.Game.Ecs.Components.Movement;
using MonoBallFramework.Game.Engine.Core.Events.System;
using MonoBallFramework.Game.Scripting.Runtime;

/// <summary>
/// Rotate behavior - NPC rotates facing direction clockwise or counter-clockwise periodically.
/// State stored in per-entity RotateState component (not instance fields).
/// </summary>
public class RotateBehavior : ScriptBase
{
    // NO INSTANCE FIELDS! All state in components.

    public override void Initialize(ScriptContext ctx)
    {
        base.Initialize(ctx); // CRITICAL: Set Context property for event subscriptions
        ctx.Logger.LogDebug("Rotate behavior initialized (state will be created on first tick)");
    }

    public override void RegisterEventHandlers(ScriptContext ctx)
    {
        On<TickEvent>(evt =>
        {
            // Initialize state on first tick (when entity is available)
            if (!Context.HasState<RotateState>())
            {
                // Default: clockwise rotation every 1 second
                Context.World.Add(
                    Context.Entity.Value,
                    new RotateState
                    {
                        RotateTimer = 0f,
                        RotateInterval = 1.0f,
                        Clockwise = true,
                        CurrentFacing = Direction.South, // Start facing south
                    }
                );

                Context.Logger.LogInformation(
                    "Rotate initialized | clockwise: {Clockwise}, interval: {Interval}s",
                    true,
                    1.0f
                );
                return; // Skip first tick after initialization
            }

            // Get per-entity state (each NPC has its own)
            ref var state = ref Context.GetState<RotateState>();

            // Count down timer
            state.RotateTimer -= evt.DeltaTime;

            if (state.RotateTimer <= 0)
            {
                // Time to rotate
                state.CurrentFacing = RotateDirection(state.CurrentFacing, state.Clockwise);
                state.RotateTimer = state.RotateInterval;

                // Update facing direction in GridMovement component
                if (Context.World.Has<GridMovement>(Context.Entity.Value))
                {
                    ref var movement = ref Context.World.Get<GridMovement>(Context.Entity.Value);
                    movement.FacingDirection = state.CurrentFacing;
                }

                Context.Logger.LogDebug(
                    "Rotated to {Direction} ({RotationType})",
                    state.CurrentFacing,
                    state.Clockwise ? "clockwise" : "counter-clockwise"
                );
            }
        });
    }

    private static Direction RotateDirection(Direction current, bool clockwise)
    {
        if (clockwise)
        {
            return current switch
            {
                Direction.North => Direction.East,
                Direction.East => Direction.South,
                Direction.South => Direction.West,
                Direction.West => Direction.North,
                _ => Direction.South,
            };
        }
        else
        {
            // Counter-clockwise
            return current switch
            {
                Direction.North => Direction.West,
                Direction.West => Direction.South,
                Direction.South => Direction.East,
                Direction.East => Direction.North,
                _ => Direction.South,
            };
        }
    }

    public override void OnUnload()
    {
        // Cleanup per-entity state
        if (Context.HasState<RotateState>())
        {
            Context.RemoveState<RotateState>();
        }

        Context.Logger.LogDebug("Rotate behavior deactivated");

        // CRITICAL: Dispose event subscriptions to prevent AccessViolationException on entity destruction
        base.OnUnload();
    }
}

// Component to store rotate-specific state
public struct RotateState
{
    public float RotateTimer;
    public float RotateInterval;
    public bool Clockwise;
    public Direction CurrentFacing;
}

return new RotateBehavior();
