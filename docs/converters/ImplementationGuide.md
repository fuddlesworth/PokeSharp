# Pokeemerald to PokeSharp Data Converter Implementation Guide

## Overview

This guide details the implementation strategy for converting pokeemerald's C-based data structures into PokeSharp's JSON format.

## Architecture

### Conversion Pipeline

```
pokeemerald (C/Assembly/Binary)
    ↓
  Parsers (Extract data from C structs/files)
    ↓
  Converters (Transform to PokeSharp format)
    ↓
  Validators (Ensure data integrity)
    ↓
  Serializers (Write JSON files)
    ↓
PokeSharp (JSON Templates)
```

## Data Source Analysis

### 1. Pokemon Species Data

**Source Files:**
- `/pokeemerald/src/data/pokemon/species_info.h` - Base stats, types, abilities
- `/pokeemerald/src/data/pokemon/pokedex_entries.h` - Pokedex descriptions
- `/pokeemerald/src/data/pokemon/pokedex_text.h` - Category, height, weight
- `/pokeemerald/src/data/pokemon/level_up_learnsets.h` - Level-up moves
- `/pokeemerald/src/data/pokemon/tmhm_learnsets.h` - TM/HM compatibility
- `/pokeemerald/src/data/pokemon/egg_moves.h` - Egg moves
- `/pokeemerald/src/data/pokemon/tutor_learnsets.h` - Tutor moves
- `/pokeemerald/src/data/pokemon/evolution.h` - Evolution data

**Parsing Strategy:**
1. Use regex to extract C struct definitions
2. Parse field values (handle constants like ITEM_NONE, TYPE_GRASS)
3. Cross-reference multiple files for complete Pokemon data
4. Resolve constant names to actual values

**Key Constants to Map:**
```c
// Types
TYPE_NORMAL → "Normal"
TYPE_GRASS → "Grass"
TYPE_POISON → "Poison"

// Abilities
ABILITY_OVERGROW → "Overgrow"
ABILITY_CHLOROPHYLL → "Chlorophyll"

// Items
ITEM_NONE → null
ITEM_ORAN_BERRY → "Oran Berry"

// Growth Rates
GROWTH_MEDIUM_SLOW → "MediumSlow"
GROWTH_FAST → "Fast"

// Egg Groups
EGG_GROUP_MONSTER → "Monster"
EGG_GROUP_GRASS → "Grass"
EGG_GROUP_NO_EGGS_DISCOVERED → "Undiscovered"

// Body Colors
BODY_COLOR_GREEN → "Green"
BODY_COLOR_RED → "Red"
```

### 2. Move Data

**Source Files:**
- `/pokeemerald/src/data/battle_moves.h` - Move mechanics
- `/pokeemerald/include/constants/moves.h` - Move IDs
- Move descriptions (text data)

**Parsing Strategy:**
1. Parse BattleMove array from battle_moves.h
2. Map move effects to descriptive names
3. Convert flag bitmasks to individual boolean flags
4. Determine move category (Physical/Special/Status) based on Gen 3 type

**Move Effect Mapping:**
```c
EFFECT_HIT → { "type": "Damage", "parameters": {} }
EFFECT_BURN_HIT → { "type": "DamageWithStatus", "parameters": { "status": "Burn", "chance": 10 } }
EFFECT_HIGH_CRITICAL → { "type": "Damage", "parameters": { "highCritical": true } }
EFFECT_MULTI_HIT → { "type": "MultiHit", "parameters": { "minHits": 2, "maxHits": 5 } }
```

**Move Flags (Bitmask):**
```c
FLAG_MAKES_CONTACT          = 0x01
FLAG_PROTECT_AFFECTED       = 0x02
FLAG_MIRROR_MOVE_AFFECTED   = 0x04
FLAG_KINGS_ROCK_AFFECTED    = 0x08
FLAG_SOUND_BASED            = 0x10
FLAG_PUNCH_MOVE             = 0x20
```

### 3. Map Data

**Source Files:**
- `/pokeemerald/data/maps/*/map.json` - Map metadata, events
- `/pokeemerald/data/maps/*/scripts.inc` - Map scripts
- `/pokeemerald/data/layouts/*/layout.json` - Tileset references (if available)

**Parsing Strategy:**
1. Read map.json (already JSON format!)
2. Convert object_events to PokeSharp ObjectEvent format
3. Identify event types (NPC, Trainer, Item, BerryTree)
4. Convert script references to .csx paths
5. Convert warp destinations to PokeSharp map naming

**Object Event Type Detection:**
```c
// Based on GraphicsId and TrainerType
OBJ_EVENT_GFX_ITEM_BALL → Type: "Item"
OBJ_EVENT_GFX_BERRY_TREE → Type: "BerryTree"
TrainerType: "TRAINER_TYPE_NORMAL" → Type: "Trainer"
TrainerType: "TRAINER_TYPE_NONE" → Type: "NPC"
```

**Map Naming Convention:**
```
pokeemerald: MAP_ROUTE102
PokeSharp:   Route102
```

### 4. Script Data

**Source Files:**
- `/pokeemerald/data/maps/*/scripts.inc` - Pory/Poryscript files

**Parsing Strategy:**
1. Use porycon parser (exists in project at `/porycon/`)
2. Parse pory scripts to AST
3. Convert pory commands to C# equivalents
4. Generate .csx files for PokeSharp scripting

**Pory to C# Command Mapping:**
```pory
msgbox("Hello!") → await Dialog.ShowMessage("Hello!");
giveitem(ITEM_POTION) → await Inventory.GiveItem("Potion");
checkflag(FLAG_VISITED_ROUTE) → Game.GetFlag("FLAG_VISITED_ROUTE")
setflag(FLAG_VISITED_ROUTE) → Game.SetFlag("FLAG_VISITED_ROUTE");
applymovement(OBJ_EVENT_ID_PLAYER, Movement_Walk) → await Player.ApplyMovement(Movement.Walk);
```

### 5. Graphics References

**Source Files:**
- `/pokeemerald/graphics/pokemon/` - Pokemon sprites
- `/pokeemerald/graphics/trainers/` - Trainer sprites
- `/pokeemerald/graphics/items/` - Item sprites

**Strategy:**
- **Do NOT convert graphics files** (keep as .png)
- **Map file paths** from pokeemerald to PokeSharp asset structure
- Generate manifest JSON for asset loading

**Path Mapping:**
```
pokeemerald: /graphics/pokemon/bulbasaur/front.png
PokeSharp:   /Assets/Sprites/Pokemon/001_Bulbasaur/front.png

pokeemerald: /graphics/trainers/youngster.png
PokeSharp:   /Assets/Sprites/Trainers/Youngster/sprite.png
```

## Implementation Steps

### Phase 1: C Parser Implementation

Create parsers for C struct extraction:

```csharp
public class CStructParser
{
    public Dictionary<string, string> ParseStructDefinition(string cCode)
    {
        // Extract key-value pairs from C struct
        // Handle array syntax: .types = { TYPE_GRASS, TYPE_POISON }
        // Handle bitfields: .evYield_HP = 2
        // Resolve constants to values
    }
}
```

### Phase 2: Constant Resolution

Build constant lookup tables:

```csharp
public class ConstantResolver
{
    private Dictionary<string, int> _typeConstants;
    private Dictionary<string, int> _abilityConstants;
    private Dictionary<string, string> _itemConstants;

    public void LoadConstants(string pokeemeraldPath)
    {
        // Parse /include/constants/pokemon.h
        // Parse /include/constants/items.h
        // Build lookup tables
    }

    public string ResolveTypeName(int typeId) { }
    public string ResolveAbilityName(int abilityId) { }
    public string ResolveItemName(int itemId) { }
}
```

### Phase 3: Individual Converters

Implement each converter interface:

```csharp
public class PokemonDataConverter : IPokemonDataConverter
{
    private readonly CStructParser _parser;
    private readonly ConstantResolver _resolver;

    public PokemonSpeciesTemplate Convert(EmeraldSpeciesInfo source)
    {
        return new PokemonSpeciesTemplate
        {
            Id = source.SpeciesId,
            Name = source.SpeciesName,
            BaseStats = new BaseStats
            {
                HP = source.BaseHP,
                Attack = source.BaseAttack,
                Defense = source.BaseDefense,
                SpAttack = source.BaseSpAttack,
                SpDefense = source.BaseSpDefense,
                Speed = source.BaseSpeed
            },
            Types = new[]
            {
                _resolver.ResolveTypeName(source.Type1),
                source.Type2 != source.Type1
                    ? _resolver.ResolveTypeName(source.Type2)
                    : null
            }.Where(t => t != null).ToArray(),
            // ... etc
        };
    }
}
```

### Phase 4: Pipeline Orchestration

```csharp
public class ConversionPipeline : IConversionPipeline
{
    public void Initialize(string pokeemeraldPath, string pokeSharpPath)
    {
        _emeraldPath = pokeemeraldPath;
        _targetPath = pokeSharpPath;

        // Initialize converters
        _pokemonConverter = new PokemonDataConverter(...);
        _moveConverter = new MoveDataConverter(...);
        _mapConverter = new MapDataConverter(...);
    }

    public ConversionReport ConvertPokemonSpecies()
    {
        var report = new ConversionReport();
        var speciesFile = Path.Combine(_emeraldPath,
            "src/data/pokemon/species_info.h");

        // Parse all species
        var species = _parser.ParseAllSpecies(speciesFile);

        foreach (var spec in species)
        {
            try
            {
                var template = _pokemonConverter.Convert(spec);
                var json = JsonSerializer.Serialize(template, _jsonOptions);

                var outputPath = Path.Combine(_targetPath,
                    $"Data/Pokemon/{spec.SpeciesId:D3}_{spec.SpeciesName}.json");
                File.WriteAllText(outputPath, json);

                report.SuccessfulConversions++;
            }
            catch (Exception ex)
            {
                report.Errors.Add(new ConversionError
                {
                    ItemId = spec.SpeciesName,
                    ErrorMessage = ex.Message,
                    Exception = ex
                });
                report.FailedConversions++;
            }
        }

        return report;
    }
}
```

## Data Validation

### Validation Rules

1. **Pokemon Species:**
   - Base stats must sum to reasonable total (200-780)
   - At least one type must be specified
   - At least one ability must be specified
   - Gender ratio must be valid (0-254 or 255 for genderless)
   - EV yields must sum to max 3

2. **Moves:**
   - Power must be 0-250
   - Accuracy must be 0-100 or null (never-miss)
   - PP must be 1-40
   - Priority must be -7 to +5

3. **Maps:**
   - Map connections must reference valid maps
   - Warp destinations must exist
   - Script files must be present

### Example Validator

```csharp
public class PokemonValidator
{
    public ValidationResult Validate(PokemonSpeciesTemplate pokemon)
    {
        var result = new ValidationResult { IsValid = true };

        var totalStats = pokemon.BaseStats.Total;
        if (totalStats < 200 || totalStats > 780)
        {
            result.Warnings.Add(
                $"Unusual base stat total: {totalStats}");
        }

        if (pokemon.Types.Length == 0)
        {
            result.IsValid = false;
            result.Errors.Add("Pokemon must have at least one type");
        }

        var totalEvs = pokemon.EvYield.HP + pokemon.EvYield.Attack +
                      pokemon.EvYield.Defense + pokemon.EvYield.SpAttack +
                      pokemon.EvYield.SpDefense + pokemon.EvYield.Speed;
        if (totalEvs > 3)
        {
            result.IsValid = false;
            result.Errors.Add($"EV yield total {totalEvs} exceeds maximum of 3");
        }

        return result;
    }
}
```

## Output Structure

### Directory Organization

```
PokeSharp.Game/Assets/
├── Data/
│   ├── Pokemon/
│   │   ├── 001_Bulbasaur.json
│   │   ├── 002_Ivysaur.json
│   │   └── ...
│   ├── Moves/
│   │   ├── 001_Pound.json
│   │   ├── 002_KarateChop.json
│   │   └── ...
│   ├── Maps/
│   │   ├── Route102/
│   │   │   ├── map.json
│   │   │   ├── scripts.csx
│   │   │   └── layout.tmx (Tiled format)
│   │   └── ...
│   └── Graphics/
│       └── manifest.json
└── Sprites/
    ├── Pokemon/
    │   ├── 001_Bulbasaur/
    │   │   ├── front.png
    │   │   ├── front_shiny.png
    │   │   ├── back.png
    │   │   ├── back_shiny.png
    │   │   ├── icon.png
    │   │   └── footprint.png
    │   └── ...
    ├── Trainers/
    └── Items/
```

## Usage Example

```csharp
// Create and run conversion pipeline
var pipeline = new ConversionPipeline();
pipeline.Initialize(
    pokeemeraldPath: "/path/to/pokeemerald",
    pokeSharpPath: "/path/to/PokeSharp.Game/Assets"
);

// Convert all data
var report = pipeline.ConvertAll();

// Output results
Console.WriteLine($"Pokemon: {report.PokemonReport.SuccessfulConversions}/{report.PokemonReport.TotalItems}");
Console.WriteLine($"Moves: {report.MovesReport.SuccessfulConversions}/{report.MovesReport.TotalItems}");
Console.WriteLine($"Maps: {report.MapsReport.SuccessfulConversions}/{report.MapsReport.TotalItems}");
Console.WriteLine($"Total time: {report.TotalDuration}");

// Handle errors
foreach (var error in report.PokemonReport.Errors)
{
    Console.WriteLine($"Error converting {error.ItemId}: {error.ErrorMessage}");
}
```

## Next Steps

1. Implement `CStructParser` for parsing C code
2. Build `ConstantResolver` with pokeemerald constants
3. Create individual converters (Pokemon, Moves, Maps)
4. Implement validation layer
5. Build pipeline orchestrator
6. Add progress reporting and logging
7. Create command-line tool for conversion
8. Document any manual post-conversion steps

## Notes

- **Script conversion** may require manual review for complex logic
- **Graphics paths** should use consistent naming conventions
- **Tiled format compatibility** depends on pokeemerald layout data availability
- **Consider incremental conversion** for large datasets
- **Version control** converted data separately from source
