using PokeSharp.Game.Components.Movement;
using PokeSharp.Game.Scripting.Runtime;
using PokeSharp.Game.Systems.Events;

/// <summary>
///     Jump east behavior.
///     Allows jumping east but blocks west movement.
/// </summary>
public class JumpEastBehavior : ScriptBase
{
    public override void RegisterEventHandlers(ScriptContext ctx)
    {
        // Block movement from west (can't climb up the ledge)
        On<MovementStartedEvent>((evt) =>
        {
            // If on this tile trying to move west, block it (can't go back across the ledge from wrong side)
            if (evt.Direction == Direction.West)
            {
                Context.Logger.LogDebug("Jump east tile: Blocking movement to west - can't enter from east side");
                evt.PreventDefault("Can't move in that direction");
            }
        });

        // Handle jump effect when moving east onto this tile
        On<MovementCompletedEvent>((evt) =>
        {
            // If player just moved east onto this tile, trigger jump
            if (evt.Direction == Direction.East)
            {
                Context.Logger.LogDebug("Jump east tile: Player jumped east onto ledge");
                // Jump animation/effect would go here if API existed
            }
        });
    }
}

return new JumpEastBehavior();
