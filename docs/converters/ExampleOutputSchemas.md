# Example Conversion Outputs

This document shows example JSON outputs from the pokeemerald to PokeSharp conversion process.

## Pokemon Species Template Example

**Input:** Bulbasaur from `pokeemerald/src/data/pokemon/species_info.h`

**Output:** `PokeSharp.Game/Assets/Data/Pokemon/001_Bulbasaur.json`

```json
{
  "id": 1,
  "name": "Bulbasaur",
  "nationalDexNumber": 1,
  "hoennDexNumber": 203,
  "baseStats": {
    "hp": 45,
    "attack": 49,
    "defense": 49,
    "spAttack": 65,
    "spDefense": 65,
    "speed": 45,
    "total": 318
  },
  "types": [
    "Grass",
    "Poison"
  ],
  "abilities": [
    "Overgrow"
  ],
  "hiddenAbility": "Chlorophyll",
  "catchRate": 45,
  "baseExperience": 64,
  "evYield": {
    "hp": 0,
    "attack": 0,
    "defense": 0,
    "spAttack": 1,
    "spDefense": 0,
    "speed": 0
  },
  "heldItems": {
    "common": null,
    "rare": null
  },
  "breeding": {
    "eggGroups": [
      "Monster",
      "Grass"
    ],
    "eggCycles": 20,
    "genderRatio": {
      "male": 87.5,
      "female": 12.5,
      "genderless": false
    }
  },
  "learnset": {
    "levelUp": [
      { "level": 1, "move": "Tackle" },
      { "level": 1, "move": "Growl" },
      { "level": 7, "move": "Leech Seed" },
      { "level": 9, "move": "Vine Whip" },
      { "level": 13, "move": "Poison Powder" },
      { "level": 13, "move": "Sleep Powder" },
      { "level": 15, "move": "Take Down" },
      { "level": 19, "move": "Razor Leaf" },
      { "level": 21, "move": "Sweet Scent" },
      { "level": 25, "move": "Growth" },
      { "level": 27, "move": "Double-Edge" },
      { "level": 31, "move": "Worry Seed" },
      { "level": 33, "move": "Synthesis" },
      { "level": 37, "move": "Solar Beam" }
    ],
    "tmHm": [
      "Cut",
      "Toxic",
      "Hidden Power",
      "Sunny Day",
      "Protect",
      "Giga Drain",
      "Safeguard",
      "Frustration",
      "Solar Beam",
      "Return",
      "Double Team",
      "Sludge Bomb",
      "Facade",
      "Secret Power",
      "Rest",
      "Attract",
      "Flash",
      "Strength",
      "Rock Smash"
    ],
    "eggMoves": [
      "Skull Bash",
      "Charm",
      "Petal Dance",
      "Magical Leaf",
      "Grass Whistle",
      "Curse",
      "Ingrain",
      "Nature Power",
      "Amnesia",
      "Leaf Storm"
    ],
    "tutorMoves": [
      "Body Slam",
      "Defense Curl",
      "Snore",
      "Mud-Slap",
      "Endure",
      "Fury Cutter",
      "Seed Bomb"
    ]
  },
  "evolutions": [
    {
      "method": "Level",
      "parameter": 16,
      "targetSpecies": "Ivysaur"
    }
  ],
  "description": {
    "category": "Seed Pokémon",
    "pokedexEntry": "A strange seed was planted on its back at birth. The plant sprouts and grows with this POKéMON.",
    "height": 0.7,
    "weight": 6.9
  },
  "graphics": {
    "frontSprite": "Sprites/Pokemon/001_Bulbasaur/front.png",
    "frontSpriteShiny": "Sprites/Pokemon/001_Bulbasaur/front_shiny.png",
    "backSprite": "Sprites/Pokemon/001_Bulbasaur/back.png",
    "backSpriteShiny": "Sprites/Pokemon/001_Bulbasaur/back_shiny.png",
    "icon": "Sprites/Pokemon/001_Bulbasaur/icon.png",
    "footprint": "Sprites/Pokemon/001_Bulbasaur/footprint.png",
    "palette": "Palettes/Pokemon/001_Bulbasaur/normal.pal",
    "shinyPalette": "Palettes/Pokemon/001_Bulbasaur/shiny.pal"
  },
  "metadata": {
    "bodyColor": "Green",
    "baseFriendship": 70,
    "growthRate": "MediumSlow",
    "safariZoneFleeRate": 0,
    "spriteFlipped": false
  }
}
```

## Move Definition Example

**Input:** Flamethrower from `pokeemerald/src/data/battle_moves.h`

**Output:** `PokeSharp.Game/Assets/Data/Moves/053_Flamethrower.json`

```json
{
  "id": 53,
  "name": "Flamethrower",
  "type": "Fire",
  "category": "Special",
  "power": 95,
  "accuracy": 100,
  "pp": 15,
  "priority": 0,
  "target": "Selected",
  "effect": {
    "type": "DamageWithStatus",
    "parameters": {
      "status": "Burn",
      "chance": 10
    }
  },
  "secondaryEffectChance": 10,
  "flags": {
    "makesContact": false,
    "affectedByProtect": true,
    "affectedByMirrorMove": true,
    "affectedByKingsRock": false,
    "soundBased": false,
    "punchMove": false
  },
  "description": "The target is scorched with an intense blast of fire. It may also leave the target with a burn."
}
```

## Map Definition Example

**Input:** Route 102 from `pokeemerald/data/maps/Route102/map.json`

**Output:** `PokeSharp.Game/Assets/Data/Maps/Route102/map.json`

```json
{
  "id": "Route102",
  "name": "Route 102",
  "tiledMapPath": "Maps/Route102/layout.tmx",
  "music": "Route101",
  "weather": "Sunny",
  "mapType": "Route",
  "battleScene": "Normal",
  "flags": {
    "showMapName": true,
    "allowCycling": true,
    "allowRunning": true,
    "allowEscaping": false,
    "requiresFlash": false
  },
  "connections": [
    {
      "direction": "Left",
      "offset": -10,
      "targetMap": "PetalburgCity"
    },
    {
      "direction": "Right",
      "offset": 0,
      "targetMap": "OldaleTown"
    }
  ],
  "objectEvents": [
    {
      "id": "route102_little_boy",
      "type": "NPC",
      "graphicsId": "LittleBoy",
      "position": {
        "x": 18,
        "y": 11,
        "elevation": 3
      },
      "movement": {
        "type": "LookAround",
        "rangeX": 0,
        "rangeY": 0
      },
      "script": "Route102_EventScript_LittleBoy",
      "flag": null,
      "trainerData": null
    },
    {
      "id": "route102_youngster_calvin",
      "type": "Trainer",
      "graphicsId": "Youngster",
      "position": {
        "x": 33,
        "y": 14,
        "elevation": 3
      },
      "movement": {
        "type": "FaceDown",
        "rangeX": 0,
        "rangeY": 0
      },
      "script": "Route102_EventScript_Calvin",
      "flag": null,
      "trainerData": {
        "trainerType": "Normal",
        "sightRange": 3
      }
    },
    {
      "id": "route102_item_potion",
      "type": "Item",
      "graphicsId": "ItemBall",
      "position": {
        "x": 11,
        "y": 15,
        "elevation": 3
      },
      "movement": {
        "type": "FaceDown",
        "rangeX": 0,
        "rangeY": 0
      },
      "script": "Route102_EventScript_ItemPotion",
      "flag": "FLAG_ITEM_ROUTE_102_POTION",
      "trainerData": null
    },
    {
      "id": "route102_berry_tree_oran",
      "type": "BerryTree",
      "graphicsId": "BerryTree",
      "position": {
        "x": 24,
        "y": 2,
        "elevation": 3
      },
      "movement": {
        "type": "BerryTreeGrowth",
        "rangeX": 0,
        "rangeY": 0
      },
      "script": "BerryTreeScript",
      "flag": null,
      "trainerData": null
    }
  ],
  "warps": [],
  "scriptTriggers": [],
  "backgroundEvents": [
    {
      "type": "Sign",
      "position": {
        "x": 17,
        "y": 2,
        "elevation": 0
      },
      "facingDirection": "Any",
      "script": "Route102_EventScript_RouteSignPetalburg"
    },
    {
      "type": "Sign",
      "position": {
        "x": 40,
        "y": 9,
        "elevation": 0
      },
      "facingDirection": "Any",
      "script": "Route102_EventScript_RouteSignOldale"
    }
  ]
}
```

## Script Conversion Example

**Input:** `pokeemerald/data/maps/Route102/scripts.inc` (Pory format)

```pory
Route102_EventScript_LittleBoy::
    msgbox Route102_Text_LittleBoy, MSGBOX_NPC
    end

Route102_EventScript_Calvin::
    trainerbattle_single TRAINER_CALVIN, Route102_Text_CalvinIntro, Route102_Text_CalvinDefeated
    msgbox Route102_Text_CalvinPostBattle, MSGBOX_AUTOCLOSE
    end

Route102_EventScript_ItemPotion::
    finditem ITEM_POTION
    end

Route102_Text_LittleBoy:
    .string "I saw something blue running\n"
    .string "around near the lake!$"

Route102_Text_CalvinIntro:
    .string "Let me try out my Pokémon!$"

Route102_Text_CalvinDefeated:
    .string "Darn, I need to train more!$"

Route102_Text_CalvinPostBattle:
    .string "My Pokémon will get stronger!$"
```

**Output:** `PokeSharp.Game/Assets/Data/Maps/Route102/scripts.csx` (C# format)

```csharp
// Route 102 Event Scripts
// Converted from pokeemerald poryscript

using PokeSharp.Game.Scripting;
using PokeSharp.Game.Scripting.API;

/// <summary>
/// Little boy NPC on Route 102
/// </summary>
public async Task Route102_EventScript_LittleBoy()
{
    await Dialog.ShowMessage("I saw something blue running\naround near the lake!");
}

/// <summary>
/// Youngster Calvin trainer battle
/// </summary>
public async Task Route102_EventScript_Calvin()
{
    var result = await Battle.StartTrainerBattle(
        trainerType: TrainerType.Single,
        trainerId: "TRAINER_CALVIN",
        introMessage: "Let me try out my Pokémon!",
        defeatedMessage: "Darn, I need to train more!"
    );

    if (result == BattleResult.PlayerWon)
    {
        await Dialog.ShowMessage("My Pokémon will get stronger!");
    }
}

/// <summary>
/// Item: Potion on Route 102
/// </summary>
public async Task Route102_EventScript_ItemPotion()
{
    await Items.FindItem("Potion", "FLAG_ITEM_ROUTE_102_POTION");
}
```

## Graphics Manifest Example

**Output:** `PokeSharp.Game/Assets/Data/Graphics/manifest.json`

```json
{
  "version": "1.0",
  "generatedFrom": "pokeemerald",
  "pokemon": [
    {
      "speciesId": 1,
      "name": "Bulbasaur",
      "sprites": {
        "frontNormal": "Sprites/Pokemon/001_Bulbasaur/front.png",
        "frontShiny": "Sprites/Pokemon/001_Bulbasaur/front_shiny.png",
        "backNormal": "Sprites/Pokemon/001_Bulbasaur/back.png",
        "backShiny": "Sprites/Pokemon/001_Bulbasaur/back_shiny.png",
        "icon": "Sprites/Pokemon/001_Bulbasaur/icon.png",
        "footprint": "Sprites/Pokemon/001_Bulbasaur/footprint.png"
      },
      "palettes": {
        "normal": "Palettes/Pokemon/001_Bulbasaur/normal.pal",
        "shiny": "Palettes/Pokemon/001_Bulbasaur/shiny.pal"
      }
    }
  ],
  "trainers": [
    {
      "trainerId": "TRAINER_CALVIN",
      "trainerClass": "Youngster",
      "sprites": {
        "front": "Sprites/Trainers/Youngster/front.png",
        "back": "Sprites/Trainers/Youngster/back.png"
      }
    }
  ],
  "items": [
    {
      "itemId": "ITEM_POTION",
      "name": "Potion",
      "sprite": "Sprites/Items/potion.png"
    }
  ],
  "tilesets": [
    {
      "name": "Route",
      "tiles": "Tilesets/Route/tiles.png",
      "metatiles": "Tilesets/Route/metatiles.bin",
      "palette": "Tilesets/Route/palette.pal"
    }
  ]
}
```

## Tiled Map Format Example (Optional)

**Output:** `PokeSharp.Game/Assets/Data/Maps/Route102/layout.tmx`

```xml
<?xml version="1.0" encoding="UTF-8"?>
<map version="1.10" tiledversion="1.10.0" orientation="orthogonal"
     renderorder="right-down" width="50" height="20" tilewidth="16" tileheight="16">
  <properties>
    <property name="music" value="Route101"/>
    <property name="weather" value="Sunny"/>
    <property name="mapType" value="Route"/>
  </properties>

  <tileset firstgid="1" source="../../Tilesets/Route/tileset.tsx"/>

  <layer id="1" name="Ground" width="50" height="20">
    <data encoding="csv">
1,1,1,1,1,1,1,1,1,1,1,1,1,1,1,1,1,1,1,1...
    </data>
  </layer>

  <layer id="2" name="Features" width="50" height="20">
    <data encoding="csv">
0,0,0,0,0,45,46,0,0,0,0,0,0,0,0,0,0,0,0,0...
    </data>
  </layer>

  <objectgroup id="3" name="Events">
    <object id="1" name="route102_little_boy" type="NPC" x="288" y="176">
      <properties>
        <property name="graphicsId" value="LittleBoy"/>
        <property name="script" value="Route102_EventScript_LittleBoy"/>
      </properties>
    </object>
  </objectgroup>
</map>
```

## Conversion Statistics Example

**Output:** Console output from conversion process

```
===== POKEEMERALD TO POKESHARP CONVERSION REPORT =====

Pokemon Species:
  Total: 386
  Successful: 386
  Failed: 0
  Duration: 2.3s

Moves:
  Total: 354
  Successful: 354
  Failed: 0
  Duration: 0.8s

Maps:
  Total: 432
  Successful: 428
  Failed: 4
  Duration: 12.5s
  Errors:
    - MAP_UNDERWATER_ROUTE_124: Missing layout data
    - MAP_UNION_ROOM: Unsupported map type
    - MAP_MYSTERY_GIFT: Unsupported map type
    - MAP_CAVE_OF_ORIGIN_B1F: Invalid connection offset

Scripts:
  Total: 1,247
  Successful: 1,198
  Failed: 49
  Duration: 45.2s
  Warnings:
    - 49 scripts require manual review for complex logic
    - 12 scripts use custom commands not in API

Graphics Manifest:
  Pokemon sprites: 386 species
  Trainer sprites: 89 classes
  Item sprites: 377 items
  Tilesets: 47 sets
  Duration: 1.1s

TOTAL DURATION: 61.9s
SUCCESS RATE: 97.8%

===== MANUAL REVIEW REQUIRED =====
- 4 maps with conversion errors
- 49 scripts with complex logic
- Review all script conversions for accuracy
```

## Notes

- All file paths use forward slashes for cross-platform compatibility
- JSON uses camelCase for property names (PokeSharp convention)
- Enum values use PascalCase strings (e.g., "MediumSlow", not "MEDIUM_SLOW")
- Null values are used instead of "NONE" constants
- Scripts are converted to async/await C# pattern
- Graphics references point to assets, not embedded data
