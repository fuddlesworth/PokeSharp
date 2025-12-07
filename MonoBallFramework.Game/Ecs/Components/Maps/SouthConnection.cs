using MonoBallFramework.Game.Engine.Core.Types;

namespace MonoBallFramework.Game.Ecs.Components.Maps;

/// <summary>
///     Map connected to the south with horizontal offset.
/// </summary>
public struct SouthConnection
{
    public GameMapId MapId { get; set; }
    public int Offset { get; set; }

    public SouthConnection(GameMapId mapId, int offset = 0)
    {
        MapId = mapId;
        Offset = offset;
    }
}
