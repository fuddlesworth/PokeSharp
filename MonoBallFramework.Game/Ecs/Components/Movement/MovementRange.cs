namespace MonoBallFramework.Game.Ecs.Components.Movement;

/// <summary>
///     Component that defines movement bounds for NPC wander behaviors.
///     Specifies how far an NPC can move from their spawn position.
///     Used by wander behaviors to constrain random movement.
/// </summary>
public struct MovementRange
{
    /// <summary>
    ///     Gets or sets the maximum distance the NPC can move on the X axis (in tiles).
    ///     A value of 0 means no horizontal movement allowed.
    /// </summary>
    public int RangeX { get; set; }

    /// <summary>
    ///     Gets or sets the maximum distance the NPC can move on the Y axis (in tiles).
    ///     A value of 0 means no vertical movement allowed.
    /// </summary>
    public int RangeY { get; set; }

    /// <summary>
    ///     Gets or sets the spawn X coordinate (in tiles).
    ///     Used as the center point for range calculations.
    /// </summary>
    public int SpawnX { get; set; }

    /// <summary>
    ///     Gets or sets the spawn Y coordinate (in tiles).
    ///     Used as the center point for range calculations.
    /// </summary>
    public int SpawnY { get; set; }

    /// <summary>
    ///     Initializes a new instance of the MovementRange struct with specified ranges.
    /// </summary>
    /// <param name="rangeX">Maximum horizontal movement distance in tiles.</param>
    /// <param name="rangeY">Maximum vertical movement distance in tiles.</param>
    public MovementRange(int rangeX, int rangeY)
    {
        RangeX = rangeX;
        RangeY = rangeY;
        SpawnX = 0;
        SpawnY = 0;
    }

    /// <summary>
    ///     Initializes a new instance of the MovementRange struct with spawn position.
    /// </summary>
    /// <param name="rangeX">Maximum horizontal movement distance in tiles.</param>
    /// <param name="rangeY">Maximum vertical movement distance in tiles.</param>
    /// <param name="spawnX">Spawn X coordinate in tiles.</param>
    /// <param name="spawnY">Spawn Y coordinate in tiles.</param>
    public MovementRange(int rangeX, int rangeY, int spawnX, int spawnY)
    {
        RangeX = rangeX;
        RangeY = rangeY;
        SpawnX = spawnX;
        SpawnY = spawnY;
    }

    /// <summary>
    ///     Checks if a target position is within the allowed movement range.
    /// </summary>
    /// <param name="targetX">Target X coordinate in tiles.</param>
    /// <param name="targetY">Target Y coordinate in tiles.</param>
    /// <returns>True if the position is within range, false otherwise.</returns>
    public readonly bool IsWithinRange(int targetX, int targetY)
    {
        int deltaX = Math.Abs(targetX - SpawnX);
        int deltaY = Math.Abs(targetY - SpawnY);

        return deltaX <= RangeX && deltaY <= RangeY;
    }

    /// <summary>
    ///     Gets the minimum X coordinate the NPC can move to.
    /// </summary>
    public readonly int MinX => SpawnX - RangeX;

    /// <summary>
    ///     Gets the maximum X coordinate the NPC can move to.
    /// </summary>
    public readonly int MaxX => SpawnX + RangeX;

    /// <summary>
    ///     Gets the minimum Y coordinate the NPC can move to.
    /// </summary>
    public readonly int MinY => SpawnY - RangeY;

    /// <summary>
    ///     Gets the maximum Y coordinate the NPC can move to.
    /// </summary>
    public readonly int MaxY => SpawnY + RangeY;

    /// <summary>
    ///     Returns true if no movement is allowed (both ranges are 0).
    /// </summary>
    public readonly bool IsStationary => RangeX == 0 && RangeY == 0;

    /// <summary>
    ///     Returns true if movement is only allowed horizontally.
    /// </summary>
    public readonly bool IsHorizontalOnly => RangeX > 0 && RangeY == 0;

    /// <summary>
    ///     Returns true if movement is only allowed vertically.
    /// </summary>
    public readonly bool IsVerticalOnly => RangeX == 0 && RangeY > 0;
}
