using Arch.Core;
using Microsoft.Extensions.Logging;
using Microsoft.Xna.Framework;
using PokeSharp.Core.Components.Movement;
using PokeSharp.Core.Components.Player;
using PokeSharp.Core.Systems;
using PokeSharp.Rendering.Components;

namespace PokeSharp.Rendering.Systems;

/// <summary>
///     System for camera following with smooth transitions and map bounds clamping.
///     Sets the camera's follow target and calls camera.Update() to handle all logic.
/// </summary>
public class CameraFollowSystem(ILogger<CameraFollowSystem>? logger = null) : ParallelSystemBase, IUpdateSystem
{
    private readonly ILogger<CameraFollowSystem>? _logger = logger;
    private QueryDescription _playerQuery;

    /// <summary>
    /// Gets the update priority. Lower values execute first.
    /// Camera follow executes at priority 825, after animation (800) and before tile animation (850).
    /// </summary>
    public int UpdatePriority => SystemPriority.CameraFollow;

    /// <inheritdoc />
    public override int Priority => SystemPriority.CameraFollow;

    /// <summary>
    /// Components this system reads to calculate camera position.
    /// </summary>
    public override List<Type> GetReadComponents() => new()
    {
        typeof(Player),
        typeof(Position)
    };

    /// <summary>
    /// Components this system writes to update camera.
    /// </summary>
    public override List<Type> GetWriteComponents() => new()
    {
        typeof(Camera)
    };

    /// <inheritdoc />
    public override void Initialize(World world)
    {
        base.Initialize(world);

        // Query for player with camera
        _playerQuery = QueryCache.Get<Player, Position, Camera>();
    }

    /// <inheritdoc />
    public override void Update(World world, float deltaTime)
    {
        if (!Enabled)
            return;

        EnsureInitialized();

        // Process each camera-equipped player
        world.Query(
            in _playerQuery,
            (ref Position position, ref Camera camera) =>
            {
                // Set follow target and let Camera.Update() handle the rest
                camera.FollowTarget = new Vector2(position.PixelX, position.PixelY);
                camera.Update(deltaTime);
            }
        );
    }
}
