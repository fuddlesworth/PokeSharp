using PokeSharp.Game.Components.Movement;
using PokeSharp.Game.Scripting.Runtime;
using PokeSharp.Game.Systems.Events;

/// <summary>
///     Impassable south behavior.
///     Blocks movement from south.
/// </summary>
public class ImpassableSouthBehavior : ScriptBase
{
    public override void RegisterEventHandlers(ScriptContext ctx)
    {
        On<CollisionCheckEvent>(evt =>
        {
            // Block if moving from south
            if (evt.FromDirection == Direction.South)
            {
                evt.PreventDefault("Cannot pass from south");
            }
        });
    }
}

return new ImpassableSouthBehavior();
