using Microsoft.Xna.Framework;

namespace PokeSharp.Game.Components.Movement;

/// <summary>
///     Player running states matching pokeemerald's behavior.
///     See pokeemerald/include/global.fieldmap.h lines 320-322.
/// </summary>
public enum RunningState
{
    /// <summary>
    ///     Player is not moving and no input detected.
    /// </summary>
    NotMoving = 0,

    /// <summary>
    ///     Player is turning in place to face a new direction.
    ///     This happens when input direction differs from facing direction.
    ///     Movement won't start until the turn completes and input is still held.
    /// </summary>
    TurnDirection = 1,

    /// <summary>
    ///     Player is actively moving between tiles.
    /// </summary>
    Moving = 2
}

/// <summary>
///     Component for grid-based movement with smooth interpolation.
///     Used for Pokemon-style tile-by-tile movement.
/// </summary>
public struct GridMovement
{
    /// <summary>
    ///     Gets or sets whether the entity is currently moving between tiles.
    /// </summary>
    public bool IsMoving { get; set; }

    /// <summary>
    ///     Gets or sets the starting position of the current movement.
    /// </summary>
    public Vector2 StartPosition { get; set; }

    /// <summary>
    ///     Gets or sets the target position of the current movement.
    /// </summary>
    public Vector2 TargetPosition { get; set; }

    /// <summary>
    ///     Gets or sets the movement progress from 0 (start) to 1 (complete).
    /// </summary>
    public float MovementProgress { get; set; }

    /// <summary>
    ///     Gets or sets the movement speed in tiles per second.
    /// </summary>
    public float MovementSpeed { get; set; }

    /// <summary>
    ///     Gets or sets the current facing direction.
    /// </summary>
    public Direction FacingDirection { get; set; }

    /// <summary>
    ///     Gets or sets whether movement is locked (e.g., during cutscenes, dialogue, or battles).
    ///     When true, the entity cannot initiate new movement.
    /// </summary>
    public bool MovementLocked { get; set; }

    /// <summary>
    ///     Gets or sets the current running state (pokeemerald-style state machine).
    ///     Controls whether player is standing, turning in place, or moving.
    /// </summary>
    public RunningState RunningState { get; set; }

    /// <summary>
    ///     Initializes a new instance of the GridMovement struct.
    /// </summary>
    /// <param name="speed">Movement speed in tiles per second (default 4.0).</param>
    public GridMovement(float speed = 4.0f)
    {
        IsMoving = false;
        StartPosition = Vector2.Zero;
        TargetPosition = Vector2.Zero;
        MovementProgress = 0f;
        MovementSpeed = speed;
        FacingDirection = Direction.South;
        MovementLocked = false;
        RunningState = RunningState.NotMoving;
    }
}