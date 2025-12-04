using PokeSharp.Game.Components.Movement;
using PokeSharp.Game.Scripting.Runtime;
using PokeSharp.Game.Systems.Events;

/// <summary>
///     Impassable north behavior.
///     Blocks movement from north.
/// </summary>
public class ImpassableNorthBehavior : ScriptBase
{
    public override void RegisterEventHandlers(ScriptContext ctx)
    {
        On<CollisionCheckEvent>(evt =>
        {
            // Block if moving from north
            if (evt.FromDirection == Direction.North)
            {
                evt.PreventDefault("Cannot pass from north");
            }
        });
    }
}

return new ImpassableNorthBehavior();
