using Arch.Core;
using Microsoft.Extensions.Logging;
using Microsoft.Xna.Framework;
using PokeSharp.Engine.Core.Systems;
using PokeSharp.Game.Components.Movement;
using PokeSharp.Game.Components.NPCs;
using PokeSharp.Game.Systems.Pathfinding;
using EcsQueries = PokeSharp.Engine.Systems.Queries.Queries;

namespace PokeSharp.Game.Systems;

/// <summary>
///     System that processes NPC path data and generates movement requests for NPCs.
///     Integrates A* pathfinding with the existing MovementSystem.
///     Uses batched processing to spread pathfinding calculations across multiple frames.
/// </summary>
/// <remarks>
///     This system runs after SpatialHashSystem but before MovementSystem.
///     It generates MovementRequest components based on the current path state.
///     Batching prevents performance spikes when many NPCs need pathfinding simultaneously.
/// </remarks>
public class PathfindingSystem : SystemBase, IUpdateSystem
{
    private readonly ILogger<PathfindingSystem>? _logger;
    private readonly List<Entity> _npcBuffer = new();
    private readonly PathfindingService _pathfindingService;
    private readonly ISpatialQuery _spatialQuery;
    private int _currentBatchIndex;
    private int _lastMapId = InvalidMapId;

    public PathfindingSystem(ISpatialQuery spatialQuery, ILogger<PathfindingSystem>? logger = null)
    {
        _spatialQuery = spatialQuery ?? throw new ArgumentNullException(nameof(spatialQuery));
        _logger = logger;
        _pathfindingService = new PathfindingService();
    }

    /// <summary>
    ///     Gets the update priority. Lower values execute first.
    ///     Pathfinding executes at priority 300, after movement (100) and collision (200).
    /// </summary>
    public int UpdatePriority => SystemPriority.Pathfinding;

    /// <inheritdoc />
    public override int Priority => SystemPriority.Pathfinding;

    /// <inheritdoc />
    public override void Update(World world, float deltaTime)
    {
        EnsureInitialized();

        // Clear buffer and collect all NPCs needing pathfinding
        _npcBuffer.Clear();

        Point? playerPosition = null;
        var currentMapId = -1;

        // Get player position for priority calculations (if player exists)
        world.Query(
            in EcsQueries.Player,
            (Entity playerEntity, ref Position pos) =>
            {
                playerPosition = new Point(pos.X, pos.Y);
                currentMapId = pos.MapId;
            }
        );

        // Reset batch index if map changed
        if (currentMapId != _lastMapId && currentMapId != InvalidMapId)
        {
            _currentBatchIndex = 0;
            _lastMapId = currentMapId;
            _logger?.LogDebug("Map changed to {MapId}, resetting batch index", currentMapId);
        }

        // Collect all NPCs with movement routes into buffer
        world.Query(
            in EcsQueries.PathFollowers,
            (
                Entity entity,
                ref Position position,
                ref GridMovement movement,
                ref MovementRoute movementRoute
            ) =>
            {
                // Only buffer NPCs that need processing
                if (
                    movementRoute.Waypoints != null
                    && movementRoute.Waypoints.Length > 0
                    && !movement.IsMoving
                    && !movementRoute.IsAtEnd
                )
                    _npcBuffer.Add(entity);
            }
        );

        // Separate priority NPCs (near player) from regular NPCs
        var priorityNpcs = new List<Entity>();
        var regularNpcs = new List<Entity>();

        if (playerPosition.HasValue)
            foreach (var entity in _npcBuffer)
            {
                ref var position = ref world.Get<Position>(entity);
                var npcPos = new Point(position.X, position.Y);
                var distance = Vector2.Distance(
                    new Vector2(npcPos.X, npcPos.Y),
                    new Vector2(playerPosition.Value.X, playerPosition.Value.Y)
                );

                if (distance <= PriorityDistanceThreshold)
                    priorityNpcs.Add(entity);
                else
                    regularNpcs.Add(entity);
            }
        else
            // No player, treat all as regular
            regularNpcs.AddRange(_npcBuffer);

        // Process ALL priority NPCs (near player) every frame for smooth experience
        foreach (var entity in priorityNpcs)
            ProcessNpcPathfinding(world, entity, deltaTime);

        // Process only a batch of regular NPCs this frame
        var startIndex = _currentBatchIndex * NPCsPerFrame;
        var endIndex = Math.Min(startIndex + NPCsPerFrame, regularNpcs.Count);

        for (var i = startIndex; i < endIndex; i++)
            ProcessNpcPathfinding(world, regularNpcs[i], deltaTime);

        // Advance to next batch, wrap around if needed
        _currentBatchIndex++;
        if (regularNpcs.Count > 0 && _currentBatchIndex * NPCsPerFrame >= regularNpcs.Count)
            _currentBatchIndex = 0;

        _logger?.LogTrace(
            "Processed {PriorityCount} priority NPCs and {BatchCount} regular NPCs (batch {BatchIndex}/{TotalBatches})",
            priorityNpcs.Count,
            endIndex - startIndex,
            _currentBatchIndex,
            regularNpcs.Count > 0 ? (regularNpcs.Count + NPCsPerFrame - 1) / NPCsPerFrame : 0
        );
    }

    /// <summary>
    ///     Processes pathfinding for a single NPC entity.
    /// </summary>
    private void ProcessNpcPathfinding(World world, Entity entity, float deltaTime)
    {
        ref var position = ref world.Get<Position>(entity);
        ref var movement = ref world.Get<GridMovement>(entity);
        ref var movementRoute = ref world.Get<MovementRoute>(entity);

        ProcessMovementRoute(
            world,
            entity,
            ref position,
            ref movement,
            ref movementRoute,
            deltaTime
        );
    }

    /// <summary>
    ///     Processes a single entity's path data.
    /// </summary>
    private void ProcessMovementRoute(
        World world,
        Entity entity,
        ref Position position,
        ref GridMovement movement,
        ref MovementRoute movementRoute,
        float deltaTime
    )
    {
        // Skip if no waypoints
        if (movementRoute.Waypoints == null || movementRoute.Waypoints.Length == 0)
            return;

        // Skip if entity is currently moving
        if (movement.IsMoving)
            return;

        // Check if at end of path
        if (movementRoute.IsAtEnd)
        {
            _logger?.LogTrace("Entity {Entity} reached end of path", entity.Id);
            return;
        }

        // Handle waypoint wait time
        if (movementRoute.CurrentWaitTime < movementRoute.WaypointWaitTime)
        {
            movementRoute.CurrentWaitTime += deltaTime;
            return;
        }

        // Get current and target positions
        var currentPos = new Point(position.X, position.Y);
        var targetWaypoint = movementRoute.CurrentWaypoint;

        // If already at current waypoint, advance to next
        if (currentPos == targetWaypoint)
        {
            AdvanceToNextWaypoint(ref movementRoute);

            // Check if we just reached the end
            if (movementRoute.IsAtEnd)
                return;

            targetWaypoint = movementRoute.CurrentWaypoint;
        }

        // Try to move toward the waypoint
        if (!TryMoveTowardWaypoint(world, entity, currentPos, targetWaypoint, position.MapId))
        {
            // Movement blocked - try to find alternative path to waypoint
            _logger?.LogDebug(
                "Direct path blocked, attempting pathfinding from {Current} to {Target}",
                currentPos,
                targetWaypoint
            );

            TryFindAlternativePath(world, entity, currentPos, targetWaypoint, position.MapId);
        }
    }

    /// <summary>
    ///     Attempts to move one step toward the target waypoint.
    /// </summary>
    private bool TryMoveTowardWaypoint(
        World world,
        Entity entity,
        Point current,
        Point target,
        int mapId
    )
    {
        // Calculate direction to target
        var dx = target.X - current.X;
        var dy = target.Y - current.Y;

        Direction moveDirection;

        // Prioritize movement based on larger delta
        if (Math.Abs(dx) > Math.Abs(dy))
            moveDirection = dx > 0 ? Direction.East : Direction.West;
        else if (Math.Abs(dy) > 0)
            moveDirection = dy > 0 ? Direction.South : Direction.North;
        else
            // Already at target
            return true;

        // Create movement request
        var movementRequest = new MovementRequest(moveDirection);

        if (world.Has<MovementRequest>(entity))
            world.Set(entity, movementRequest);
        else
            world.Add(entity, movementRequest);

        return true;
    }

    /// <summary>
    ///     Tries to find an alternative path using A* pathfinding.
    /// </summary>
    private void TryFindAlternativePath(
        World world,
        Entity entity,
        Point current,
        Point target,
        int mapId
    )
    {
        if (_spatialQuery == null)
            return;

        // Use A* to find path
        var path = _pathfindingService.FindPath(
            current,
            target,
            mapId,
            _spatialQuery,
            MaxPathfindingIterations
        );

        if (path == null || path.Count == 0)
        {
            _logger?.LogWarning(
                "No alternative path found for entity {Entity} from {Current} to {Target}",
                entity.Id,
                current,
                target
            );
            return;
        }

        // Smooth the path to reduce waypoints
        path = _pathfindingService.SmoothPath(path, mapId, _spatialQuery);

        // NOTE: ToArray() allocation is unavoidable here because:
        // 1. MovementRoute.Waypoints is Point[] (required by component design)
        // 2. This only happens when pathfinding recalculates (NPC hits obstacle)
        // 3. Frequency is low (typically once per NPC per obstacle encounter)
        // This is acceptable because it's not a per-frame allocation
        var newWaypoints = path.ToArray();

        if (world.Has<MovementRoute>(entity))
        {
            ref var movementRoute = ref world.Get<MovementRoute>(entity);
            movementRoute.Waypoints = newWaypoints;
            movementRoute.CurrentWaypointIndex = 0;
            movementRoute.CurrentWaitTime = 0f;

            _logger?.LogInformation(
                "Alternative path found for entity {Entity}: {Count} waypoints",
                entity.Id,
                newWaypoints.Length
            );
        }
    }

    /// <summary>
    ///     Advances to the next waypoint in the path.
    /// </summary>
    private void AdvanceToNextWaypoint(ref MovementRoute movementRoute)
    {
        movementRoute.CurrentWaypointIndex++;

        // Check if we reached the end
        if (movementRoute.CurrentWaypointIndex >= movementRoute.Waypoints.Length)
        {
            if (movementRoute.Loop)
            {
                // Loop back to start
                movementRoute.CurrentWaypointIndex = 0;
                _logger?.LogTrace("Path looped back to start");
            }
            else
            {
                // Stay at last waypoint
                movementRoute.CurrentWaypointIndex = movementRoute.Waypoints.Length - 1;
                _logger?.LogTrace("Path completed");
            }
        }

        // Reset wait time for the new waypoint
        movementRoute.CurrentWaitTime = 0f;
    }

    #region Constants

    /// <summary>
    ///     Maximum number of NPCs to process per frame for pathfinding to prevent performance spikes.
    /// </summary>
    private const int NPCsPerFrame = 10;

    /// <summary>
    ///     Distance threshold in tiles for priority processing.
    ///     NPCs within this distance from the player are processed every frame.
    /// </summary>
    private const float PriorityDistanceThreshold = 15.0f;

    /// <summary>
    ///     Maximum iterations for A* pathfinding algorithm to prevent infinite loops.
    /// </summary>
    private const int MaxPathfindingIterations = 500;

    /// <summary>
    ///     Sentinel value indicating no map is currently tracked.
    /// </summary>
    private const int InvalidMapId = -1;

    #endregion
}
