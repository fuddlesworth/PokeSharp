// scripts/events/on-wild-encounter.csx
// Example event hook script for wild Pokemon encounters in Pokemon Emerald
// This script runs AFTER a WildEncounterEvent is published

using PokeSharp.Engine.Core.Types.Events;
using PokeSharp.Game.Events;
using PokeSharp.Game.Components;
using System;
using System.Linq;

// Context is provided by the EventHookRegistry
// Available objects:
// - Context.Event: The event being handled
// - Context.Logger: ILogger for logging
// - Context.EventBus: IEventBus for publishing events
// - Context.World: Arch.Core.World for ECS queries
// - Context.Modpack: LoadedModpack instance
// - Context.Config: ModpackConfiguration

// Get the encounter event
var encounterEvent = (WildEncounterEvent)Context.Event;

// Log the encounter
Context.Logger.LogInformation(
    "Wild {Pokemon} (Level {Level}) appeared at {Location}!",
    encounterEvent.PokemonId,
    encounterEvent.Level,
    encounterEvent.Location
);

// --- EMERALD-SPECIFIC ENCOUNTER MODIFICATIONS ---

// 1. Tall Grass Level Boost
// In tall grass, wild Pokemon are 2-3 levels higher
if (encounterEvent.Location.Contains("tall-grass") ||
    encounterEvent.TerrainType == "TallGrass")
{
    int levelBoost = Random.Shared.Next(2, 4); // 2-3 levels
    encounterEvent.Level += levelBoost;

    Context.Logger.LogDebug(
        "Tall grass encounter - boosted level by {Boost}",
        levelBoost
    );
}

// 2. Water Encounter Fishing Rod Check
// Different fishing rods affect encounter levels
if (encounterEvent.EncounterMethod == "Fishing")
{
    var playerInventory = GetPlayerInventory();

    if (playerInventory.HasItem("super-rod"))
    {
        // Super Rod: Higher level Pokemon (30-45)
        encounterEvent.Level = Random.Shared.Next(30, 46);
    }
    else if (playerInventory.HasItem("good-rod"))
    {
        // Good Rod: Mid-level Pokemon (20-35)
        encounterEvent.Level = Random.Shared.Next(20, 36);
    }
    else // Old Rod
    {
        // Old Rod: Low-level Pokemon (5-15)
        encounterEvent.Level = Random.Shared.Next(5, 16);
    }
}

// 3. Shiny Pokemon Check (1/8192 in Emerald)
// With broken RNG, same trainer ID/secret ID can produce same shiny
int shinyChance = 8192;

// Check for Shiny Charm (if modded in)
if (Context.Config.Features.ContainsKey("shinyCharm") &&
    GetPlayerInventory().HasItem("shiny-charm"))
{
    shinyChance = 1365; // ~3x rate with Shiny Charm
}

if (Random.Shared.Next(shinyChance) == 0)
{
    encounterEvent.IsShiny = true;

    Context.Logger.LogInformation("✨ SHINY POKEMON ENCOUNTERED! ✨");

    // Play shiny sparkle animation
    Context.EventBus.Publish(new EffectRequestedEvent
    {
        EffectId = "shiny-sparkle",
        TargetEntity = encounterEvent.PokemonEntity,
        Duration = 2.0f
    });

    // Play shiny sound effect
    PlaySound("shiny-encounter");
}

// 4. Pokerus Check (3/65536 chance in Emerald)
// Rare beneficial status condition
if (Random.Shared.Next(65536) < 3)
{
    encounterEvent.HasPokerus = true;

    Context.Logger.LogInformation("Pokemon has Pokerus!");
}

// 5. Safari Zone Encounter Modifications
if (encounterEvent.Location.Contains("safari-zone"))
{
    // Safari Zone Pokemon are always specific levels
    var safariLevels = new[] { 25, 27, 29, 31 };
    encounterEvent.Level = safariLevels[Random.Shared.Next(safariLevels.Length)];

    // Safari Zone has special catch mechanics
    encounterEvent.IsSafariEncounter = true;
}

// 6. Repel Effect Check
var playerEntity = GetPlayerEntity();
if (playerEntity.Has<RepelEffect>())
{
    var repel = playerEntity.Get<RepelEffect>();
    var playerParty = GetPlayerParty();

    // Repel prevents encounters with Pokemon lower than party lead
    if (playerParty.Count > 0)
    {
        var leadPokemon = playerParty[0];
        var leadLevel = leadPokemon.Get<PokemonData>().Level;

        if (encounterEvent.Level < leadLevel)
        {
            // Cancel this encounter
            encounterEvent.Cancelled = true;
            Context.Logger.LogDebug("Encounter blocked by Repel effect");
            return;
        }
    }
}

// 7. White Flute / Black Flute Effects (Emerald items)
if (playerEntity.Has<ActiveFlute>())
{
    var flute = playerEntity.Get<ActiveFlute>();

    if (flute.Type == "White")
    {
        // White Flute increases encounter rate by 50%
        // (This would be handled earlier in encounter table logic)
        Context.Logger.LogDebug("White Flute active - encounter rate increased");
    }
    else if (flute.Type == "Black")
    {
        // Black Flute decreases encounter rate by 50%
        Context.Logger.LogDebug("Black Flute active - encounter rate decreased");
    }
}

// 8. Synchronize Ability (First Pokemon in party)
// If lead Pokemon has Synchronize, 50% chance wild Pokemon has same nature
var playerParty = GetPlayerParty();
if (playerParty.Count > 0)
{
    var leadPokemon = playerParty[0];
    var leadData = leadPokemon.Get<PokemonData>();

    if (leadData.Ability == "synchronize" && Random.Shared.Next(2) == 0)
    {
        encounterEvent.Nature = leadData.Nature;
        Context.Logger.LogDebug(
            "Synchronize activated - wild Pokemon has {Nature} nature",
            encounterEvent.Nature
        );
    }
}

// 9. Static / Magnet Pull Abilities
// Increase encounter rate of Electric/Steel types (handled in encounter table)
// But we can log it here
if (playerParty.Count > 0)
{
    var leadPokemon = playerParty[0];
    var leadData = leadPokemon.Get<PokemonData>();

    if (leadData.Ability == "static" && encounterEvent.PokemonType1 == "Electric")
    {
        Context.Logger.LogDebug("Static ability attracted Electric-type Pokemon");
    }
    else if (leadData.Ability == "magnet-pull" && encounterEvent.PokemonType1 == "Steel")
    {
        Context.Logger.LogDebug("Magnet Pull ability attracted Steel-type Pokemon");
    }
}

// 10. Store Encounter Data in Player Statistics
var playerData = GetPlayerData();
playerData.AddEncounter(new EncounterRecord
{
    PokemonId = encounterEvent.PokemonId,
    Species = encounterEvent.Species,
    Level = encounterEvent.Level,
    Location = encounterEvent.Location,
    EncounterMethod = encounterEvent.EncounterMethod,
    IsShiny = encounterEvent.IsShiny,
    Timestamp = DateTime.UtcNow
});

// Update Pokedex "Seen" data
UpdatePokedexSeen(encounterEvent.Species, encounterEvent.Location);

// 11. Battle Transition Effect
// Different areas have different battle transition animations
string transitionEffect = encounterEvent.Location switch
{
    var loc when loc.Contains("cave") => "cave-battle-transition",
    var loc when loc.Contains("water") || loc.Contains("surf") => "water-battle-transition",
    var loc when loc.Contains("sky") => "sky-battle-transition",
    _ => "grass-battle-transition"
};

Context.EventBus.Publish(new EffectRequestedEvent
{
    EffectId = transitionEffect,
    Duration = 1.5f
});

// 12. Play Wild Battle Music
string battleMusic = encounterEvent.IsLegendary
    ? "battle-legendary"
    : "battle-wild";

PlayMusic(battleMusic);

// 13. Achievement / Milestone Tracking
CheckEncounterAchievements(encounterEvent);

// --- HELPER FUNCTIONS ---

object GetPlayerInventory()
{
    var playerEntity = Context.World.Query(
        new QueryDescription().WithAll<PlayerTag, Inventory>()
    ).First();

    return playerEntity.Get<Inventory>();
}

object GetPlayerEntity()
{
    return Context.World.Query(
        new QueryDescription().WithAll<PlayerTag>()
    ).First();
}

List<object> GetPlayerParty()
{
    var playerEntity = GetPlayerEntity();
    var party = playerEntity.Get<PokemonParty>();
    return party.Pokemon.ToList();
}

object GetPlayerData()
{
    var playerEntity = GetPlayerEntity();
    return playerEntity.Get<PlayerData>();
}

void PlaySound(string soundId)
{
    var sound = Context.Modpack.GetSound(soundId);
    sound?.Play();
}

void PlayMusic(string musicId)
{
    Context.EventBus.Publish(new MusicRequestedEvent
    {
        MusicId = musicId,
        FadeIn = 1.0f
    });
}

void UpdatePokedexSeen(string species, string location)
{
    var playerEntity = GetPlayerEntity();
    var pokedex = playerEntity.Get<PokedexData>();

    if (!pokedex.HasSeen(species))
    {
        pokedex.MarkSeen(species, location);

        Context.Logger.LogInformation(
            "New Pokedex entry: {Species} (Seen)",
            species
        );

        // Play Pokedex update sound
        PlaySound("pokedex-update");
    }
}

void CheckEncounterAchievements(WildEncounterEvent evt)
{
    var playerData = GetPlayerData();

    // First encounter
    if (playerData.TotalEncounters == 1)
    {
        UnlockAchievement("first-encounter");
    }

    // 100th encounter
    if (playerData.TotalEncounters == 100)
    {
        UnlockAchievement("encounter-veteran");
    }

    // First shiny encounter
    if (evt.IsShiny && playerData.ShinyEncounters == 0)
    {
        UnlockAchievement("shiny-hunter");
    }
}

void UnlockAchievement(string achievementId)
{
    Context.EventBus.Publish(new AchievementUnlockedEvent
    {
        AchievementId = achievementId,
        Timestamp = DateTime.UtcNow
    });

    Context.Logger.LogInformation("Achievement unlocked: {Achievement}", achievementId);
}

Context.Logger.LogDebug("Wild encounter event processing complete");
