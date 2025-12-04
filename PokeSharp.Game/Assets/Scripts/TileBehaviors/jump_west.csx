using PokeSharp.Game.Components.Movement;
using PokeSharp.Game.Scripting.Runtime;
using PokeSharp.Game.Systems.Events;

/// <summary>
///     Jump west behavior.
///     Allows jumping west but blocks east movement.
/// </summary>
public class JumpWestBehavior : ScriptBase
{
    public override void RegisterEventHandlers(ScriptContext ctx)
    {
        // Block movement from east (can't climb up the ledge)
        On<MovementStartedEvent>((evt) =>
        {
            // If on this tile trying to move east, block it (can't go back across the ledge from wrong side)
            if (evt.Direction == Direction.East)
            {
                Context.Logger.LogDebug("Jump west tile: Blocking movement to east - can't enter from west side");
                evt.PreventDefault("Can't move in that direction");
            }
        });

        // Handle jump effect when moving west onto this tile
        On<MovementCompletedEvent>((evt) =>
        {
            // If player just moved west onto this tile, trigger jump
            if (evt.Direction == Direction.West)
            {
                Context.Logger.LogDebug("Jump west tile: Player jumped west onto ledge");
                // Jump animation/effect would go here if API existed
            }
        });
    }
}

return new JumpWestBehavior();
