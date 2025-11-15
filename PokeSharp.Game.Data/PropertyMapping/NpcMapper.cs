using Arch.Core;
using PokeSharp.Game.Components.NPCs;

namespace PokeSharp.Game.Data.PropertyMapping;

/// <summary>
///     Maps Tiled properties to NPC components.
///     Handles "trainer", "pokemon_id", "view_range", and "npcId" properties.
/// </summary>
public class NpcMapper : IEntityPropertyMapper<Npc>
{
    public bool CanMap(Dictionary<string, object> properties)
    {
        // Can map if has NPC-related properties
        return properties.ContainsKey("trainer")
            || properties.ContainsKey("pokemon_id")
            || properties.ContainsKey("view_range")
            || properties.ContainsKey("npcId");
    }

    public Npc Map(Dictionary<string, object> properties)
    {
        if (!CanMap(properties))
            throw new InvalidOperationException("Cannot map properties to Npc component");

        // Get NPC ID (required)
        var npcId = properties.TryGetValue("npcId", out var idValue)
            ? idValue?.ToString() ?? "unknown"
            : "unknown";

        var npc = new Npc(npcId);

        // Check if this NPC is a trainer
        if (properties.TryGetValue("trainer", out var trainerValue))
        {
            npc.IsTrainer = trainerValue switch
            {
                bool b => b,
                string s => bool.TryParse(s, out var result) && result,
                _ => false,
            };
        }

        // Get view range
        if (properties.TryGetValue("view_range", out var rangeValue))
        {
            npc.ViewRange = rangeValue switch
            {
                int i => i,
                string s when int.TryParse(s, out var result) => result,
                _ => 0,
            };
        }

        return npc;
    }

    public void MapAndAdd(World world, Entity entity, Dictionary<string, object> properties)
    {
        if (CanMap(properties))
        {
            var npc = Map(properties);
            world.Add(entity, npc);
        }
    }
}
