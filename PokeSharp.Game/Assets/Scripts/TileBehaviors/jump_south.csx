using PokeSharp.Game.Components.Movement;
using PokeSharp.Game.Scripting.Runtime;
using PokeSharp.Game.Systems.Events;

/// <summary>
///     Jump south behavior.
///     Allows jumping south but blocks north movement.
/// </summary>
public class JumpSouthBehavior : ScriptBase
{
    public override void RegisterEventHandlers(ScriptContext ctx)
    {
        // Block movement from north (can't climb up the ledge)
        On<MovementStartedEvent>((evt) =>
        {
            // If on this tile trying to move north, block it (can't go back up the ledge from wrong side)
            if (evt.Direction == Direction.North)
            {
                Context.Logger.LogDebug("Jump south tile: Blocking movement to north - can't enter from south side");
                evt.PreventDefault("Can't move in that direction");
            }
        });

        // Handle jump effect when moving south onto this tile
        On<MovementCompletedEvent>((evt) =>
        {
            // If player just moved south onto this tile, trigger jump
            if (evt.Direction == Direction.South)
            {
                Context.Logger.LogDebug("Jump south tile: Player jumped south onto ledge");
                // Jump animation/effect would go here if API existed
            }
        });
    }
}

return new JumpSouthBehavior();
