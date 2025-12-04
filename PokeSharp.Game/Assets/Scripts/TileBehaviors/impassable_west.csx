using PokeSharp.Game.Components.Movement;
using PokeSharp.Game.Scripting.Runtime;
using PokeSharp.Game.Systems.Events;

/// <summary>
///     Impassable west behavior.
///     Blocks movement from west.
/// </summary>
public class ImpassableWestBehavior : ScriptBase
{
    public override void RegisterEventHandlers(ScriptContext ctx)
    {
        On<CollisionCheckEvent>(evt =>
        {
            // Block if moving from west
            if (evt.FromDirection == Direction.West)
            {
                evt.PreventDefault("Cannot pass from west");
            }
        });
    }
}

return new ImpassableWestBehavior();
