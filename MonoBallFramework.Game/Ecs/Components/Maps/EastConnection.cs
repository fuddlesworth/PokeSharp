using MonoBallFramework.Game.Engine.Core.Types;

namespace MonoBallFramework.Game.Ecs.Components.Maps;

/// <summary>
///     Map connected to the east with vertical offset.
/// </summary>
public struct EastConnection
{
    public GameMapId MapId { get; set; }
    public int Offset { get; set; }

    public EastConnection(GameMapId mapId, int offset = 0)
    {
        MapId = mapId;
        Offset = offset;
    }
}
