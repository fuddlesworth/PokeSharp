using Microsoft.Xna.Framework;
using PokeSharp.Game.Components.Movement;

namespace PokeSharp.Game.Components.Helpers;

/// <summary>
///     Helper methods for manipulating GridMovement components.
///     These methods are used by systems to update component state without violating ECS principles.
///     Components should remain pure data structures (no behavior).
/// </summary>
public static class GridMovementHelpers
{
    /// <summary>
    ///     Starts movement from the current position to a target grid position.
    /// </summary>
    /// <param name="movement">The GridMovement component to modify.</param>
    /// <param name="start">The starting pixel position.</param>
    /// <param name="target">The target pixel position.</param>
    /// <param name="direction">The direction of movement.</param>
    public static void StartMovement(
        ref GridMovement movement,
        Vector2 start,
        Vector2 target,
        Direction direction
    )
    {
        movement.IsMoving = true;
        movement.StartPosition = start;
        movement.TargetPosition = target;
        movement.MovementProgress = 0f;
        movement.FacingDirection = direction;
    }

    /// <summary>
    ///     Starts movement from the current position to a target grid position.
    ///     Direction is automatically calculated from start and target positions.
    /// </summary>
    /// <param name="movement">The GridMovement component to modify.</param>
    /// <param name="start">The starting pixel position.</param>
    /// <param name="target">The target pixel position.</param>
    public static void StartMovement(ref GridMovement movement, Vector2 start, Vector2 target)
    {
        var direction = CalculateDirection(start, target);
        StartMovement(ref movement, start, target, direction);
    }

    /// <summary>
    ///     Completes the current movement and resets state.
    ///     Note: RunningState is NOT reset here - InputSystem manages it based on input.
    ///     This allows continuous walking without turn-in-place when changing directions.
    /// </summary>
    /// <param name="movement">The GridMovement component to modify.</param>
    public static void CompleteMovement(ref GridMovement movement)
    {
        movement.IsMoving = false;
        movement.MovementProgress = 0f;
        // Don't reset RunningState - if input is still held, we want to skip turn-in-place
        // InputSystem will set RunningState = NotMoving when no input is detected
    }

    /// <summary>
    ///     Starts a turn-in-place animation (player turns to face direction without moving).
    ///     Called when input direction differs from current facing direction.
    ///     The actual turn duration is determined by the animation system (PlayOnce on go_* animation).
    /// </summary>
    /// <param name="movement">The GridMovement component to modify.</param>
    /// <param name="direction">The direction to turn and face.</param>
    public static void StartTurnInPlace(ref GridMovement movement, Direction direction)
    {
        movement.RunningState = RunningState.TurnDirection;
        movement.FacingDirection = direction;
    }

    /// <summary>
    ///     Calculates the direction based on the difference between start and target positions.
    /// </summary>
    /// <param name="start">The starting position.</param>
    /// <param name="target">The target position.</param>
    /// <returns>The primary direction of movement.</returns>
    public static Direction CalculateDirection(Vector2 start, Vector2 target)
    {
        var delta = target - start;

        // Determine primary axis (larger delta)
        if (Math.Abs(delta.X) > Math.Abs(delta.Y))
            return delta.X > 0 ? Direction.East : Direction.West;

        return delta.Y > 0 ? Direction.South : Direction.North;
    }
}
