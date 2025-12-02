// ============================================================================
// EXAMPLE CONVERTER IMPLEMENTATION
// ============================================================================
// This file demonstrates a working implementation of the Pokemon species
// converter to show the pattern for all converters.
// ============================================================================

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Text.RegularExpressions;

namespace PokeSharp.Tools.Converters.Implementation
{
    // ========================================================================
    // EXAMPLE 1: C STRUCT PARSER
    // ========================================================================

    /// <summary>
    /// Parses C struct definitions from pokeemerald source files
    /// </summary>
    public class CStructParser
    {
        private readonly ConstantResolver _constantResolver;

        public CStructParser(ConstantResolver constantResolver)
        {
            _constantResolver = constantResolver;
        }

        /// <summary>
        /// Parse a single SpeciesInfo struct from C code
        /// </summary>
        public EmeraldSpeciesInfo ParseSpeciesInfo(
            string structText,
            int speciesId,
            string speciesName
        )
        {
            var info = new EmeraldSpeciesInfo { SpeciesId = speciesId, SpeciesName = speciesName };

            // Parse base stats
            info.BaseHP = ParseByteField(structText, "baseHP");
            info.BaseAttack = ParseByteField(structText, "baseAttack");
            info.BaseDefense = ParseByteField(structText, "baseDefense");
            info.BaseSpeed = ParseByteField(structText, "baseSpeed");
            info.BaseSpAttack = ParseByteField(structText, "baseSpAttack");
            info.BaseSpDefense = ParseByteField(structText, "baseSpDefense");

            // Parse types (array syntax)
            var types = ParseArrayField(structText, "types");
            info.Type1 = _constantResolver.ResolveTypeName(types[0]);
            info.Type2 =
                types.Length > 1 ? _constantResolver.ResolveTypeName(types[1]) : info.Type1;

            // Parse catch/experience
            info.CatchRate = ParseByteField(structText, "catchRate");
            info.ExpYield = ParseByteField(structText, "expYield");

            // Parse EV yields
            info.EvYieldHP = ParseByteField(structText, "evYield_HP");
            info.EvYieldAttack = ParseByteField(structText, "evYield_Attack");
            info.EvYieldDefense = ParseByteField(structText, "evYield_Defense");
            info.EvYieldSpeed = ParseByteField(structText, "evYield_Speed");
            info.EvYieldSpAttack = ParseByteField(structText, "evYield_SpAttack");
            info.EvYieldSpDefense = ParseByteField(structText, "evYield_SpDefense");

            // Parse items
            info.ItemCommon = _constantResolver.ResolveItemName(
                ParseStringField(structText, "itemCommon")
            );
            info.ItemRare = _constantResolver.ResolveItemName(
                ParseStringField(structText, "itemRare")
            );

            // Parse breeding
            info.GenderRatio = ParseGenderRatio(structText);
            info.EggCycles = ParseByteField(structText, "eggCycles");
            info.Friendship = ParseFriendship(structText);
            info.GrowthRate = _constantResolver.ResolveGrowthRate(
                ParseStringField(structText, "growthRate")
            );

            var eggGroups = ParseArrayField(structText, "eggGroups");
            info.EggGroup1 = _constantResolver.ResolveEggGroup(eggGroups[0]);
            info.EggGroup2 =
                eggGroups.Length > 1
                    ? _constantResolver.ResolveEggGroup(eggGroups[1])
                    : info.EggGroup1;

            // Parse abilities
            var abilities = ParseArrayField(structText, "abilities");
            info.Ability1 = _constantResolver.ResolveAbilityName(abilities[0]);
            info.Ability2 =
                abilities.Length > 1 && abilities[1] != "ABILITY_NONE"
                    ? _constantResolver.ResolveAbilityName(abilities[1])
                    : null;

            // Parse misc
            info.SafariZoneFleeRate = ParseByteField(structText, "safariZoneFleeRate");
            info.BodyColor = _constantResolver.ResolveBodyColor(
                ParseStringField(structText, "bodyColor")
            );
            info.NoFlip = ParseBoolField(structText, "noFlip");

            return info;
        }

        private byte ParseByteField(string structText, string fieldName)
        {
            var pattern = $@"\.{fieldName}\s*=\s*(\d+)";
            var match = Regex.Match(structText, pattern);
            return match.Success ? byte.Parse(match.Groups[1].Value) : (byte)0;
        }

        private string ParseStringField(string structText, string fieldName)
        {
            var pattern = $@"\.{fieldName}\s*=\s*([A-Z_]+)";
            var match = Regex.Match(structText, pattern);
            return match.Success ? match.Groups[1].Value : null;
        }

        private string[] ParseArrayField(string structText, string fieldName)
        {
            var pattern = $@"\.{fieldName}\s*=\s*\{{\s*([^}}]+)\s*\}}";
            var match = Regex.Match(structText, pattern);
            if (!match.Success)
                return Array.Empty<string>();

            return match
                .Groups[1]
                .Value.Split(',')
                .Select(s => s.Trim())
                .Where(s => !string.IsNullOrEmpty(s))
                .ToArray();
        }

        private bool ParseBoolField(string structText, string fieldName)
        {
            var value = ParseStringField(structText, fieldName);
            return value == "TRUE" || value == "1";
        }

        private byte ParseGenderRatio(string structText)
        {
            var pattern = @"\.genderRatio\s*=\s*(.+?)[,\n]";
            var match = Regex.Match(structText, pattern);
            if (!match.Success)
                return 127; // Default 50/50

            var value = match.Groups[1].Value.Trim();

            // Handle PERCENT_FEMALE(12.5) macro
            if (value.StartsWith("PERCENT_FEMALE"))
            {
                var percentMatch = Regex.Match(value, @"PERCENT_FEMALE\(([\d.]+)\)");
                if (percentMatch.Success)
                {
                    var percent = float.Parse(percentMatch.Groups[1].Value);
                    return (byte)Math.Min(254, (percent * 255) / 100);
                }
            }
            else if (value == "MON_GENDERLESS")
            {
                return 255;
            }
            else if (value == "MON_MALE")
            {
                return 0;
            }
            else if (value == "MON_FEMALE")
            {
                return 254;
            }

            return byte.Parse(value);
        }

        private byte ParseFriendship(string structText)
        {
            var value = ParseStringField(structText, "friendship");
            if (value == "STANDARD_FRIENDSHIP")
                return 70;
            return byte.Parse(value ?? "70");
        }
    }

    // ========================================================================
    // EXAMPLE 2: CONSTANT RESOLVER
    // ========================================================================

    /// <summary>
    /// Resolves pokeemerald constants to human-readable names
    /// </summary>
    public class ConstantResolver
    {
        private readonly Dictionary<string, string> _typeMap = new();
        private readonly Dictionary<string, string> _abilityMap = new();
        private readonly Dictionary<string, string> _itemMap = new();
        private readonly Dictionary<string, string> _growthRateMap = new();
        private readonly Dictionary<string, string> _eggGroupMap = new();
        private readonly Dictionary<string, string> _bodyColorMap = new();

        public ConstantResolver()
        {
            InitializeTypeMaps();
            InitializeAbilityMaps();
            InitializeItemMaps();
            InitializeGrowthRateMaps();
            InitializeEggGroupMaps();
            InitializeBodyColorMaps();
        }

        private void InitializeTypeMaps()
        {
            _typeMap["TYPE_NORMAL"] = "Normal";
            _typeMap["TYPE_FIGHTING"] = "Fighting";
            _typeMap["TYPE_FLYING"] = "Flying";
            _typeMap["TYPE_POISON"] = "Poison";
            _typeMap["TYPE_GROUND"] = "Ground";
            _typeMap["TYPE_ROCK"] = "Rock";
            _typeMap["TYPE_BUG"] = "Bug";
            _typeMap["TYPE_GHOST"] = "Ghost";
            _typeMap["TYPE_STEEL"] = "Steel";
            _typeMap["TYPE_FIRE"] = "Fire";
            _typeMap["TYPE_WATER"] = "Water";
            _typeMap["TYPE_GRASS"] = "Grass";
            _typeMap["TYPE_ELECTRIC"] = "Electric";
            _typeMap["TYPE_PSYCHIC"] = "Psychic";
            _typeMap["TYPE_ICE"] = "Ice";
            _typeMap["TYPE_DRAGON"] = "Dragon";
            _typeMap["TYPE_DARK"] = "Dark";
        }

        private void InitializeAbilityMaps()
        {
            _abilityMap["ABILITY_NONE"] = null;
            _abilityMap["ABILITY_OVERGROW"] = "Overgrow";
            _abilityMap["ABILITY_CHLOROPHYLL"] = "Chlorophyll";
            _abilityMap["ABILITY_BLAZE"] = "Blaze";
            _abilityMap["ABILITY_TORRENT"] = "Torrent";
            _abilityMap["ABILITY_SWARM"] = "Swarm";
            // ... Add all abilities
        }

        private void InitializeItemMaps()
        {
            _itemMap["ITEM_NONE"] = null;
            _itemMap["ITEM_ORAN_BERRY"] = "Oran Berry";
            _itemMap["ITEM_PECHA_BERRY"] = "Pecha Berry";
            _itemMap["ITEM_LIGHT_BALL"] = "Light Ball";
            // ... Add all items
        }

        private void InitializeGrowthRateMaps()
        {
            _growthRateMap["GROWTH_MEDIUM_FAST"] = "MediumFast";
            _growthRateMap["GROWTH_MEDIUM_SLOW"] = "MediumSlow";
            _growthRateMap["GROWTH_FAST"] = "Fast";
            _growthRateMap["GROWTH_SLOW"] = "Slow";
            _growthRateMap["GROWTH_ERRATIC"] = "Erratic";
            _growthRateMap["GROWTH_FLUCTUATING"] = "Fluctuating";
        }

        private void InitializeEggGroupMaps()
        {
            _eggGroupMap["EGG_GROUP_MONSTER"] = "Monster";
            _eggGroupMap["EGG_GROUP_WATER_1"] = "Water1";
            _eggGroupMap["EGG_GROUP_WATER_2"] = "Water2";
            _eggGroupMap["EGG_GROUP_WATER_3"] = "Water3";
            _eggGroupMap["EGG_GROUP_BUG"] = "Bug";
            _eggGroupMap["EGG_GROUP_FLYING"] = "Flying";
            _eggGroupMap["EGG_GROUP_FIELD"] = "Field";
            _eggGroupMap["EGG_GROUP_FAIRY"] = "Fairy";
            _eggGroupMap["EGG_GROUP_GRASS"] = "Grass";
            _eggGroupMap["EGG_GROUP_HUMAN_LIKE"] = "HumanLike";
            _eggGroupMap["EGG_GROUP_MINERAL"] = "Mineral";
            _eggGroupMap["EGG_GROUP_AMORPHOUS"] = "Amorphous";
            _eggGroupMap["EGG_GROUP_DRAGON"] = "Dragon";
            _eggGroupMap["EGG_GROUP_DITTO"] = "Ditto";
            _eggGroupMap["EGG_GROUP_NO_EGGS_DISCOVERED"] = "Undiscovered";
        }

        private void InitializeBodyColorMaps()
        {
            _bodyColorMap["BODY_COLOR_RED"] = "Red";
            _bodyColorMap["BODY_COLOR_BLUE"] = "Blue";
            _bodyColorMap["BODY_COLOR_YELLOW"] = "Yellow";
            _bodyColorMap["BODY_COLOR_GREEN"] = "Green";
            _bodyColorMap["BODY_COLOR_BLACK"] = "Black";
            _bodyColorMap["BODY_COLOR_BROWN"] = "Brown";
            _bodyColorMap["BODY_COLOR_PURPLE"] = "Purple";
            _bodyColorMap["BODY_COLOR_GRAY"] = "Gray";
            _bodyColorMap["BODY_COLOR_WHITE"] = "White";
            _bodyColorMap["BODY_COLOR_PINK"] = "Pink";
        }

        public string ResolveTypeName(string constant) =>
            _typeMap.TryGetValue(constant, out var name) ? name : constant;

        public string ResolveAbilityName(string constant) =>
            _abilityMap.TryGetValue(constant, out var name) ? name : constant;

        public string ResolveItemName(string constant) =>
            _itemMap.TryGetValue(constant, out var name) ? name : constant;

        public string ResolveGrowthRate(string constant) =>
            _growthRateMap.TryGetValue(constant, out var name) ? name : constant;

        public string ResolveEggGroup(string constant) =>
            _eggGroupMap.TryGetValue(constant, out var name) ? name : constant;

        public string ResolveBodyColor(string constant) =>
            _bodyColorMap.TryGetValue(constant, out var name) ? name : constant;
    }

    // ========================================================================
    // EXAMPLE 3: POKEMON DATA CONVERTER
    // ========================================================================

    /// <summary>
    /// Converts pokeemerald SpeciesInfo to PokeSharp Pokemon template
    /// </summary>
    public class PokemonDataConverter : IPokemonDataConverter
    {
        private readonly CStructParser _parser;
        private readonly ConstantResolver _resolver;
        private readonly string _pokeemeraldPath;

        public PokemonDataConverter(string pokeemeraldPath)
        {
            _pokeemeraldPath = pokeemeraldPath;
            _resolver = new ConstantResolver();
            _parser = new CStructParser(_resolver);
        }

        public PokemonSpeciesTemplate Convert(EmeraldSpeciesInfo source)
        {
            return new PokemonSpeciesTemplate
            {
                Id = source.SpeciesId,
                Name = source.SpeciesName,
                NationalDexNumber = source.SpeciesId,
                HoennDexNumber = GetHoennDexNumber(source.SpeciesId),

                BaseStats = new BaseStats
                {
                    HP = source.BaseHP,
                    Attack = source.BaseAttack,
                    Defense = source.BaseDefense,
                    SpAttack = source.BaseSpAttack,
                    SpDefense = source.BaseSpDefense,
                    Speed = source.BaseSpeed,
                },

                Types = GetTypes(source),

                Abilities = GetAbilities(source),
                HiddenAbility = null, // Gen 3 doesn't have hidden abilities

                CatchRate = source.CatchRate,
                BaseExperience = source.ExpYield,

                EvYield = new EvYield
                {
                    HP = source.EvYieldHP,
                    Attack = source.EvYieldAttack,
                    Defense = source.EvYieldDefense,
                    SpAttack = source.EvYieldSpAttack,
                    SpDefense = source.EvYieldSpDefense,
                    Speed = source.EvYieldSpeed,
                },

                HeldItems = new HeldItems { Common = source.ItemCommon, Rare = source.ItemRare },

                Breeding = new BreedingData
                {
                    EggGroups = GetEggGroups(source),
                    EggCycles = source.EggCycles,
                    GenderRatio = ConvertGenderRatio(source.GenderRatio),
                },

                Learnset = GetLearnset(source.SpeciesId),
                Evolutions = GetEvolutions(source.SpeciesId),
                Description = GetDescription(source.SpeciesId),
                Graphics = GetGraphics(source.SpeciesId, source.SpeciesName),

                Metadata = new PokemonMetadata
                {
                    BodyColor = source.BodyColor,
                    BaseFriendship = source.Friendship,
                    GrowthRate = source.GrowthRate,
                    SafariZoneFleeRate = source.SafariZoneFleeRate,
                    SpriteFlipped = !source.NoFlip,
                },
            };
        }

        public IEnumerable<PokemonSpeciesTemplate> ConvertAll(
            IEnumerable<EmeraldSpeciesInfo> sources
        )
        {
            return sources.Select(Convert);
        }

        public ValidationResult Validate(EmeraldSpeciesInfo source)
        {
            var result = new ValidationResult { IsValid = true };

            var totalStats =
                source.BaseHP
                + source.BaseAttack
                + source.BaseDefense
                + source.BaseSpeed
                + source.BaseSpAttack
                + source.BaseSpDefense;

            if (totalStats < 200 || totalStats > 780)
            {
                result.Warnings.Add($"Unusual base stat total: {totalStats}");
            }

            var totalEvs =
                source.EvYieldHP
                + source.EvYieldAttack
                + source.EvYieldDefense
                + source.EvYieldSpeed
                + source.EvYieldSpAttack
                + source.EvYieldSpDefense;

            if (totalEvs > 3)
            {
                result.IsValid = false;
                result.Errors.Add($"EV yield total {totalEvs} exceeds maximum of 3");
            }

            if (string.IsNullOrEmpty(source.Type1))
            {
                result.IsValid = false;
                result.Errors.Add("Pokemon must have at least one type");
            }

            return result;
        }

        // Helper methods
        private string[] GetTypes(EmeraldSpeciesInfo source)
        {
            if (source.Type1 == source.Type2)
                return new[] { source.Type1 };
            return new[] { source.Type1, source.Type2 };
        }

        private string[] GetAbilities(EmeraldSpeciesInfo source)
        {
            var abilities = new List<string>();
            if (!string.IsNullOrEmpty(source.Ability1))
                abilities.Add(source.Ability1);
            if (!string.IsNullOrEmpty(source.Ability2) && source.Ability2 != source.Ability1)
                abilities.Add(source.Ability2);
            return abilities.ToArray();
        }

        private string[] GetEggGroups(EmeraldSpeciesInfo source)
        {
            if (source.EggGroup1 == source.EggGroup2)
                return new[] { source.EggGroup1 };
            return new[] { source.EggGroup1, source.EggGroup2 };
        }

        private GenderRatio ConvertGenderRatio(byte genderRatio)
        {
            if (genderRatio == 255)
            {
                return new GenderRatio
                {
                    Male = 0,
                    Female = 0,
                    Genderless = true,
                };
            }

            float femalePercent = (genderRatio / 254f) * 100f;
            return new GenderRatio
            {
                Male = 100f - femalePercent,
                Female = femalePercent,
                Genderless = false,
            };
        }

        private int? GetHoennDexNumber(int speciesId)
        {
            // Would parse from pokedex_orders.h
            // Simplified for example
            return speciesId <= 386 ? (int?)speciesId : null;
        }

        private Learnset GetLearnset(int speciesId)
        {
            // Would parse from level_up_learnsets.h, tmhm_learnsets.h, etc.
            // Simplified for example
            return new Learnset
            {
                LevelUp = new LevelUpMove[0],
                TmHm = new string[0],
                EggMoves = new string[0],
                TutorMoves = new string[0],
            };
        }

        private Evolution[] GetEvolutions(int speciesId)
        {
            // Would parse from evolution.h
            // Simplified for example
            return Array.Empty<Evolution>();
        }

        private PokemonDescription GetDescription(int speciesId)
        {
            // Would parse from pokedex_entries.h and pokedex_text.h
            // Simplified for example
            return new PokemonDescription
            {
                Category = "Unknown",
                PokedexEntry = "No description available.",
                Height = 0,
                Weight = 0,
            };
        }

        private PokemonGraphics GetGraphics(int speciesId, string speciesName)
        {
            var id = speciesId.ToString("D3");
            var basePath = $"Sprites/Pokemon/{id}_{speciesName}";

            return new PokemonGraphics
            {
                FrontSprite = $"{basePath}/front.png",
                FrontSpriteShiny = $"{basePath}/front_shiny.png",
                BackSprite = $"{basePath}/back.png",
                BackSpriteShiny = $"{basePath}/back_shiny.png",
                Icon = $"{basePath}/icon.png",
                Footprint = $"{basePath}/footprint.png",
                Palette = $"Palettes/Pokemon/{id}_{speciesName}/normal.pal",
                ShinyPalette = $"Palettes/Pokemon/{id}_{speciesName}/shiny.pal",
            };
        }

        // Interface implementations (simplified for example)
        public EmeraldSpeciesInfo ParseSpeciesInfo(string cStructText, int speciesId)
        {
            var speciesName = ExtractSpeciesName(cStructText);
            return _parser.ParseSpeciesInfo(cStructText, speciesId, speciesName);
        }

        public PokemonDescription GetDescription(int speciesId) => GetDescription(speciesId);

        public EvolutionData GetEvolutions(int speciesId) =>
            new EvolutionData { Evolutions = GetEvolutions(speciesId).ToList() };

        public MoveLearnset GetLevelUpMoves(int speciesId) =>
            new MoveLearnset { Moves = new List<LevelUpMove>() };

        public TmHmLearnset GetTmHmMoves(int speciesId) =>
            new TmHmLearnset { Moves = new List<string>() };

        public TutorLearnset GetTutorMoves(int speciesId) =>
            new TutorLearnset { Moves = new List<string>() };

        public EggMoveList GetEggMoves(int speciesId) =>
            new EggMoveList { Moves = new List<string>() };

        private string ExtractSpeciesName(string cStructText)
        {
            var match = Regex.Match(cStructText, @"\[SPECIES_(\w+)\]");
            return match.Success ? match.Groups[1].Value : "Unknown";
        }
    }

    // ========================================================================
    // EXAMPLE 4: USAGE IN PIPELINE
    // ========================================================================

    public class ExamplePipelineUsage
    {
        public static void Main(string[] args)
        {
            var pokeemeraldPath = "/path/to/pokeemerald";
            var targetPath = "/path/to/PokeSharp.Game/Assets";

            var converter = new PokemonDataConverter(pokeemeraldPath);

            // Read species_info.h
            var speciesFile = Path.Combine(pokeemeraldPath, "src/data/pokemon/species_info.h");
            var fileContent = File.ReadAllText(speciesFile);

            // Extract all species (simplified - would need proper parsing)
            var speciesStructs = ExtractSpeciesStructs(fileContent);

            var jsonOptions = new JsonSerializerOptions
            {
                WriteIndented = true,
                PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
            };

            int successCount = 0;
            int errorCount = 0;

            foreach (var (speciesId, structText) in speciesStructs)
            {
                try
                {
                    // Parse C struct
                    var emeraldInfo = converter.ParseSpeciesInfo(structText, speciesId);

                    // Validate
                    var validation = converter.Validate(emeraldInfo);
                    if (!validation.IsValid)
                    {
                        Console.WriteLine($"Validation failed for species {speciesId}:");
                        foreach (var error in validation.Errors)
                            Console.WriteLine($"  - {error}");
                        errorCount++;
                        continue;
                    }

                    // Convert to PokeSharp format
                    var template = converter.Convert(emeraldInfo);

                    // Serialize to JSON
                    var json = JsonSerializer.Serialize(template, jsonOptions);

                    // Write to file
                    var outputDir = Path.Combine(targetPath, "Data/Pokemon");
                    Directory.CreateDirectory(outputDir);

                    var fileName = $"{speciesId:D3}_{emeraldInfo.SpeciesName}.json";
                    var outputPath = Path.Combine(outputDir, fileName);

                    File.WriteAllText(outputPath, json);

                    Console.WriteLine($"✓ Converted {emeraldInfo.SpeciesName}");
                    successCount++;
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"✗ Error converting species {speciesId}: {ex.Message}");
                    errorCount++;
                }
            }

            Console.WriteLine($"\nConversion complete:");
            Console.WriteLine($"  Successful: {successCount}");
            Console.WriteLine($"  Errors: {errorCount}");
        }

        private static Dictionary<int, string> ExtractSpeciesStructs(string fileContent)
        {
            // Simplified extraction - real implementation would be more robust
            var structs = new Dictionary<int, string>();
            var pattern = @"\[SPECIES_(\w+)\]\s*=\s*\{([^}]+)\}";
            var matches = Regex.Matches(fileContent, pattern, RegexOptions.Singleline);

            int speciesId = 1;
            foreach (Match match in matches)
            {
                structs[speciesId++] = match.Groups[2].Value;
            }

            return structs;
        }
    }
}
