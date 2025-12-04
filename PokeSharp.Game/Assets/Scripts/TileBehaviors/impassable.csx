using PokeSharp.Game.Scripting.Runtime;
using PokeSharp.Game.Systems.Events;

/// <summary>
///     Impassable tile behavior.
///     Blocks all movement in any direction.
/// </summary>
public class ImpassableBehavior : ScriptBase
{
    public override void RegisterEventHandlers(ScriptContext ctx)
    {
        On<CollisionCheckEvent>(evt =>
        {
            evt.PreventDefault("Tile is impassable");
        });
    }
}

return new ImpassableBehavior();
