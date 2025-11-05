using Arch.Core;
using Arch.Core.Extensions;
using PokeSharp.Core.Components;
using PokeSharp.Core.Utilities;

namespace PokeSharp.Core.Systems;

/// <summary>
///     System that builds and maintains a spatial hash for efficient entity lookups by position.
///     Runs very early (Priority: 25) to ensure spatial data is available for other systems.
///     Uses dirty tracking to avoid rebuilding index for static tiles every frame.
/// </summary>
public class SpatialHashSystem : BaseSystem
{
    private readonly SpatialHash _staticHash;   // For tiles (indexed once)
    private readonly SpatialHash _dynamicHash;  // For entities with Position (cleared each frame)
    private bool _staticTilesIndexed = false;

    /// <summary>
    ///     Initializes a new instance of the SpatialHashSystem class.
    /// </summary>
    public SpatialHashSystem()
    {
        _staticHash = new SpatialHash();
        _dynamicHash = new SpatialHash();
    }

    /// <inheritdoc />
    public override int Priority => SystemPriority.SpatialHash;

    /// <inheritdoc />
    public override void Update(World world, float deltaTime)
    {
        EnsureInitialized();

        // Index static tiles once
        if (!_staticTilesIndexed)
        {
            _staticHash.Clear();

            var tileQuery = new QueryDescription().WithAll<TilePosition>();
            world.Query(
                in tileQuery,
                (Entity entity, ref TilePosition pos) =>
                {
                    _staticHash.Add(entity, pos.MapId, pos.X, pos.Y);
                }
            );

            _staticTilesIndexed = true;
        }

        // Clear and re-index all dynamic entities each frame
        _dynamicHash.Clear();

        var dynamicQuery = new QueryDescription().WithAll<Position>();
        world.Query(
            in dynamicQuery,
            (Entity entity, ref Position pos) =>
            {
                _dynamicHash.Add(entity, pos.MapId, pos.X, pos.Y);
            }
        );
    }

    /// <summary>
    ///     Forces a full rebuild of the spatial hash on the next update.
    ///     Call this when maps are loaded/unloaded or tiles are added/removed.
    /// </summary>
    public void InvalidateStaticTiles()
    {
        _staticTilesIndexed = false;
    }

    /// <summary>
    ///     Gets all entities at the specified tile position.
    /// </summary>
    /// <param name="mapId">The map identifier.</param>
    /// <param name="x">The X tile coordinate.</param>
    /// <param name="y">The Y tile coordinate.</param>
    /// <returns>Collection of entities at this position.</returns>
    public IEnumerable<Entity> GetEntitiesAt(int mapId, int x, int y)
    {
        // Return entities from both static and dynamic hashes
        return _staticHash.GetAt(mapId, x, y).Concat(_dynamicHash.GetAt(mapId, x, y));
    }

    /// <summary>
    ///     Gets all entities within the specified bounds.
    /// </summary>
    /// <param name="mapId">The map identifier.</param>
    /// <param name="bounds">The bounding rectangle in tile coordinates.</param>
    /// <returns>Collection of entities within the bounds.</returns>
    public IEnumerable<Entity> GetEntitiesInBounds(
        int mapId,
        Microsoft.Xna.Framework.Rectangle bounds
    )
    {
        // Return entities from both static and dynamic hashes
        return _staticHash.GetInBounds(mapId, bounds).Concat(_dynamicHash.GetInBounds(mapId, bounds));
    }

    /// <summary>
    ///     Gets diagnostic information about the spatial hash.
    /// </summary>
    /// <returns>A tuple with (entity count, occupied position count).</returns>
    public (int entityCount, int occupiedPositions) GetDiagnostics()
    {
        var staticCount = _staticHash.GetEntityCount();
        var dynamicCount = _dynamicHash.GetEntityCount();
        var staticPositions = _staticHash.GetOccupiedPositionCount();
        var dynamicPositions = _dynamicHash.GetOccupiedPositionCount();
        return (staticCount + dynamicCount, staticPositions + dynamicPositions);
    }
}
