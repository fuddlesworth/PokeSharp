using PokeSharp.Game.Components.Movement;
using PokeSharp.Game.Scripting.Runtime;
using PokeSharp.Game.Systems.Events;

/// <summary>
///     Impassable east behavior.
///     Blocks movement from east.
/// </summary>
public class ImpassableEastBehavior : ScriptBase
{
    public override void RegisterEventHandlers(ScriptContext ctx)
    {
        On<CollisionCheckEvent>(evt =>
        {
            // Block if moving from east
            if (evt.FromDirection == Direction.East)
            {
                evt.PreventDefault("Cannot pass from east");
            }
        });
    }
}

return new ImpassableEastBehavior();
