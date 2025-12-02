// ============================================================================
// POKEEMERALD TO POKESHARP DATA CONVERTER ARCHITECTURE
// ============================================================================
// This file contains the complete design for converting pokeemerald C/assembly
// data into PokeSharp JSON format for use in the game engine.
// ============================================================================

using System;
using System.Collections.Generic;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace PokeSharp.Tools.Converters
{
    // ========================================================================
    // CORE CONVERTER INTERFACES
    // ========================================================================

    /// <summary>
    /// Base interface for all pokeemerald data converters.
    /// Provides a consistent conversion pattern across all data types.
    /// </summary>
    public interface IDataConverter<TSource, TTarget>
    {
        /// <summary>Converts a single source item to target format</summary>
        TTarget Convert(TSource source);

        /// <summary>Batch converts multiple items</summary>
        IEnumerable<TTarget> ConvertAll(IEnumerable<TSource> sources);

        /// <summary>Validates the source data before conversion</summary>
        ValidationResult Validate(TSource source);
    }

    /// <summary>
    /// Converts Pokemon species data from pokeemerald C structs to PokeSharp JSON.
    /// Source: pokeemerald/src/data/pokemon/species_info.h (SpeciesInfo struct)
    /// </summary>
    public interface IPokemonDataConverter
        : IDataConverter<EmeraldSpeciesInfo, PokemonSpeciesTemplate>
    {
        /// <summary>Parse SpeciesInfo from C struct definition</summary>
        EmeraldSpeciesInfo ParseSpeciesInfo(string cStructText, int speciesId);

        /// <summary>Get Pokedex text and description</summary>
        PokemonDescription GetDescription(int speciesId);

        /// <summary>Get evolution chain data</summary>
        EvolutionData GetEvolutions(int speciesId);

        /// <summary>Get level-up move learnset</summary>
        MoveLearnset GetLevelUpMoves(int speciesId);

        /// <summary>Get TM/HM compatibility</summary>
        TmHmLearnset GetTmHmMoves(int speciesId);

        /// <summary>Get tutor move compatibility</summary>
        TutorLearnset GetTutorMoves(int speciesId);

        /// <summary>Get egg move list</summary>
        EggMoveList GetEggMoves(int speciesId);
    }

    /// <summary>
    /// Converts move data from pokeemerald to PokeSharp format.
    /// Source: pokeemerald/src/data/battle_moves.h (BattleMove struct)
    /// </summary>
    public interface IMoveDataConverter : IDataConverter<EmeraldBattleMove, MoveDefinition>
    {
        /// <summary>Parse BattleMove from C struct</summary>
        EmeraldBattleMove ParseBattleMove(string cStructText, int moveId);

        /// <summary>Get move description text</summary>
        string GetMoveDescription(int moveId);

        /// <summary>Convert move effect to PokeSharp format</summary>
        MoveEffect ConvertMoveEffect(byte effectId);

        /// <summary>Convert move flags to PokeSharp format</summary>
        MoveFlags ConvertMoveFlags(byte flags);
    }

    /// <summary>
    /// Converts map data from pokeemerald JSON to PokeSharp/Tiled compatible format.
    /// Source: pokeemerald/data/maps/*/map.json
    /// </summary>
    public interface IMapDataConverter : IDataConverter<EmeraldMapJson, PokeSharpMapDefinition>
    {
        /// <summary>Parse pokeemerald map.json</summary>
        EmeraldMapJson ParseMapJson(string mapJsonPath);

        /// <summary>Convert to Tiled-compatible format if possible</summary>
        TiledMapData ConvertToTiledFormat(EmeraldMapJson source);

        /// <summary>Convert map connections</summary>
        IEnumerable<MapConnection> ConvertConnections(IEnumerable<EmeraldConnection> connections);

        /// <summary>Convert object events (NPCs, items, trainers)</summary>
        IEnumerable<ObjectEvent> ConvertObjectEvents(IEnumerable<EmeraldObjectEvent> events);

        /// <summary>Convert warp events</summary>
        IEnumerable<WarpEvent> ConvertWarps(IEnumerable<EmeraldWarpEvent> warps);

        /// <summary>Convert script triggers</summary>
        IEnumerable<ScriptTrigger> ConvertScriptTriggers(
            IEnumerable<EmeraldCoordEvent> coordEvents
        );

        /// <summary>Convert background events (signs, hidden items)</summary>
        IEnumerable<BackgroundEvent> ConvertBgEvents(IEnumerable<EmeraldBgEvent> bgEvents);
    }

    /// <summary>
    /// Converts pokeemerald pory scripts (.inc) to C# scripts (.csx).
    /// Source: pokeemerald/data/maps/*/scripts.inc
    /// </summary>
    public interface IScriptConverter
    {
        /// <summary>Parse pory/poryscript .inc file</summary>
        PoryScriptFile ParsePoryScript(string scriptPath);

        /// <summary>Convert to C# scripting format</summary>
        CSharpScriptFile ConvertToCSharp(PoryScriptFile poryScript);

        /// <summary>Generate .csx file content</summary>
        string GenerateCsxContent(CSharpScriptFile script);

        /// <summary>Map pory commands to PokeSharp API calls</summary>
        string ConvertScriptCommand(PoryCommand command);
    }

    /// <summary>
    /// Maps graphics asset references from pokeemerald to PokeSharp paths.
    /// Does NOT convert graphics files, only references.
    /// </summary>
    public interface IGraphicsConverter
    {
        /// <summary>Get sprite path for Pokemon species</summary>
        GraphicsReference GetPokemonSprite(int speciesId, bool isShiny, bool isFemale, bool isBack);

        /// <summary>Get icon/menu sprite for Pokemon</summary>
        GraphicsReference GetPokemonIcon(int speciesId);

        /// <summary>Get footprint graphic</summary>
        GraphicsReference GetPokemonFootprint(int speciesId);

        /// <summary>Get trainer sprite</summary>
        GraphicsReference GetTrainerSprite(int trainerId, bool isBack);

        /// <summary>Get item sprite</summary>
        GraphicsReference GetItemSprite(int itemId);

        /// <summary>Get tileset reference</summary>
        TilesetReference GetTileset(string tilesetName);

        /// <summary>Convert palette data reference</summary>
        PaletteReference GetPalette(string paletteName);
    }

    // ========================================================================
    // POKEEMERALD SOURCE DATA STRUCTURES
    // ========================================================================

    /// <summary>
    /// Represents pokeemerald's SpeciesInfo struct from species_info.h
    /// </summary>
    public class EmeraldSpeciesInfo
    {
        public int SpeciesId { get; set; }
        public string SpeciesName { get; set; }

        // Base stats
        public byte BaseHP { get; set; }
        public byte BaseAttack { get; set; }
        public byte BaseDefense { get; set; }
        public byte BaseSpeed { get; set; }
        public byte BaseSpAttack { get; set; }
        public byte BaseSpDefense { get; set; }

        // Types
        public string Type1 { get; set; }
        public string Type2 { get; set; }

        // Catch/experience
        public byte CatchRate { get; set; }
        public byte ExpYield { get; set; }

        // EV yields (0-3 each)
        public byte EvYieldHP { get; set; }
        public byte EvYieldAttack { get; set; }
        public byte EvYieldDefense { get; set; }
        public byte EvYieldSpeed { get; set; }
        public byte EvYieldSpAttack { get; set; }
        public byte EvYieldSpDefense { get; set; }

        // Items
        public string ItemCommon { get; set; }
        public string ItemRare { get; set; }

        // Breeding
        public byte GenderRatio { get; set; }
        public byte EggCycles { get; set; }
        public byte Friendship { get; set; }
        public string GrowthRate { get; set; }
        public string EggGroup1 { get; set; }
        public string EggGroup2 { get; set; }

        // Abilities
        public string Ability1 { get; set; }
        public string Ability2 { get; set; }

        // Misc
        public byte SafariZoneFleeRate { get; set; }
        public string BodyColor { get; set; }
        public bool NoFlip { get; set; }
    }

    /// <summary>
    /// Represents pokeemerald's BattleMove struct from battle_moves.h
    /// </summary>
    public class EmeraldBattleMove
    {
        public int MoveId { get; set; }
        public string MoveName { get; set; }

        public byte Effect { get; set; }
        public byte Power { get; set; }
        public string Type { get; set; }
        public byte Accuracy { get; set; }
        public byte PP { get; set; }
        public byte SecondaryEffectChance { get; set; }
        public string Target { get; set; }
        public sbyte Priority { get; set; }
        public byte Flags { get; set; }
    }

    /// <summary>
    /// Represents pokeemerald map.json structure
    /// </summary>
    public class EmeraldMapJson
    {
        public string Id { get; set; }
        public string Name { get; set; }
        public string Layout { get; set; }
        public string Music { get; set; }
        public string RegionMapSection { get; set; }
        public bool RequiresFlash { get; set; }
        public string Weather { get; set; }
        public string MapType { get; set; }
        public bool AllowCycling { get; set; }
        public bool AllowEscaping { get; set; }
        public bool AllowRunning { get; set; }
        public bool ShowMapName { get; set; }
        public string BattleScene { get; set; }

        public List<EmeraldConnection> Connections { get; set; }
        public List<EmeraldObjectEvent> ObjectEvents { get; set; }
        public List<EmeraldWarpEvent> WarpEvents { get; set; }
        public List<EmeraldCoordEvent> CoordEvents { get; set; }
        public List<EmeraldBgEvent> BgEvents { get; set; }
    }

    public class EmeraldConnection
    {
        public string Map { get; set; }
        public int Offset { get; set; }
        public string Direction { get; set; }
    }

    public class EmeraldObjectEvent
    {
        public string GraphicsId { get; set; }
        public int X { get; set; }
        public int Y { get; set; }
        public int Elevation { get; set; }
        public string MovementType { get; set; }
        public int MovementRangeX { get; set; }
        public int MovementRangeY { get; set; }
        public string TrainerType { get; set; }
        public string TrainerSightOrBerryTreeId { get; set; }
        public string Script { get; set; }
        public string Flag { get; set; }
    }

    public class EmeraldWarpEvent
    {
        public int X { get; set; }
        public int Y { get; set; }
        public int Elevation { get; set; }
        public string DestMap { get; set; }
        public int DestWarpId { get; set; }
    }

    public class EmeraldCoordEvent
    {
        public string Type { get; set; }
        public int X { get; set; }
        public int Y { get; set; }
        public int Elevation { get; set; }
        public string Var { get; set; }
        public int Value { get; set; }
        public string Script { get; set; }
    }

    public class EmeraldBgEvent
    {
        public string Type { get; set; }
        public int X { get; set; }
        public int Y { get; set; }
        public int Elevation { get; set; }
        public string PlayerFacingDir { get; set; }
        public string Script { get; set; }
    }

    // ========================================================================
    // POKESHARP TARGET DATA STRUCTURES (JSON TEMPLATES)
    // ========================================================================

    /// <summary>
    /// PokeSharp Pokemon species template (JSON output format)
    /// </summary>
    public class PokemonSpeciesTemplate
    {
        [JsonPropertyName("id")]
        public int Id { get; set; }

        [JsonPropertyName("name")]
        public string Name { get; set; }

        [JsonPropertyName("nationalDexNumber")]
        public int NationalDexNumber { get; set; }

        [JsonPropertyName("hoennDexNumber")]
        public int? HoennDexNumber { get; set; }

        [JsonPropertyName("baseStats")]
        public BaseStats BaseStats { get; set; }

        [JsonPropertyName("types")]
        public string[] Types { get; set; }

        [JsonPropertyName("abilities")]
        public string[] Abilities { get; set; }

        [JsonPropertyName("hiddenAbility")]
        public string HiddenAbility { get; set; }

        [JsonPropertyName("catchRate")]
        public int CatchRate { get; set; }

        [JsonPropertyName("baseExperience")]
        public int BaseExperience { get; set; }

        [JsonPropertyName("evYield")]
        public EvYield EvYield { get; set; }

        [JsonPropertyName("heldItems")]
        public HeldItems HeldItems { get; set; }

        [JsonPropertyName("breeding")]
        public BreedingData Breeding { get; set; }

        [JsonPropertyName("learnset")]
        public Learnset Learnset { get; set; }

        [JsonPropertyName("evolutions")]
        public Evolution[] Evolutions { get; set; }

        [JsonPropertyName("description")]
        public PokemonDescription Description { get; set; }

        [JsonPropertyName("graphics")]
        public PokemonGraphics Graphics { get; set; }

        [JsonPropertyName("metadata")]
        public PokemonMetadata Metadata { get; set; }
    }

    public class BaseStats
    {
        [JsonPropertyName("hp")]
        public int HP { get; set; }

        [JsonPropertyName("attack")]
        public int Attack { get; set; }

        [JsonPropertyName("defense")]
        public int Defense { get; set; }

        [JsonPropertyName("spAttack")]
        public int SpAttack { get; set; }

        [JsonPropertyName("spDefense")]
        public int SpDefense { get; set; }

        [JsonPropertyName("speed")]
        public int Speed { get; set; }

        [JsonPropertyName("total")]
        public int Total => HP + Attack + Defense + SpAttack + SpDefense + Speed;
    }

    public class EvYield
    {
        [JsonPropertyName("hp")]
        public int HP { get; set; }

        [JsonPropertyName("attack")]
        public int Attack { get; set; }

        [JsonPropertyName("defense")]
        public int Defense { get; set; }

        [JsonPropertyName("spAttack")]
        public int SpAttack { get; set; }

        [JsonPropertyName("spDefense")]
        public int SpDefense { get; set; }

        [JsonPropertyName("speed")]
        public int Speed { get; set; }
    }

    public class HeldItems
    {
        [JsonPropertyName("common")]
        public string Common { get; set; }

        [JsonPropertyName("rare")]
        public string Rare { get; set; }
    }

    public class BreedingData
    {
        [JsonPropertyName("eggGroups")]
        public string[] EggGroups { get; set; }

        [JsonPropertyName("eggCycles")]
        public int EggCycles { get; set; }

        [JsonPropertyName("genderRatio")]
        public GenderRatio GenderRatio { get; set; }
    }

    public class GenderRatio
    {
        [JsonPropertyName("male")]
        public float Male { get; set; }

        [JsonPropertyName("female")]
        public float Female { get; set; }

        [JsonPropertyName("genderless")]
        public bool Genderless { get; set; }
    }

    public class Learnset
    {
        [JsonPropertyName("levelUp")]
        public LevelUpMove[] LevelUp { get; set; }

        [JsonPropertyName("tmHm")]
        public string[] TmHm { get; set; }

        [JsonPropertyName("eggMoves")]
        public string[] EggMoves { get; set; }

        [JsonPropertyName("tutorMoves")]
        public string[] TutorMoves { get; set; }
    }

    public class LevelUpMove
    {
        [JsonPropertyName("level")]
        public int Level { get; set; }

        [JsonPropertyName("move")]
        public string Move { get; set; }
    }

    public class Evolution
    {
        [JsonPropertyName("method")]
        public string Method { get; set; }

        [JsonPropertyName("parameter")]
        public object Parameter { get; set; }

        [JsonPropertyName("targetSpecies")]
        public string TargetSpecies { get; set; }
    }

    public class PokemonDescription
    {
        [JsonPropertyName("category")]
        public string Category { get; set; }

        [JsonPropertyName("pokedexEntry")]
        public string PokedexEntry { get; set; }

        [JsonPropertyName("height")]
        public float Height { get; set; }

        [JsonPropertyName("weight")]
        public float Weight { get; set; }
    }

    public class PokemonGraphics
    {
        [JsonPropertyName("frontSprite")]
        public string FrontSprite { get; set; }

        [JsonPropertyName("frontSpriteShiny")]
        public string FrontSpriteShiny { get; set; }

        [JsonPropertyName("backSprite")]
        public string BackSprite { get; set; }

        [JsonPropertyName("backSpriteShiny")]
        public string BackSpriteShiny { get; set; }

        [JsonPropertyName("icon")]
        public string Icon { get; set; }

        [JsonPropertyName("footprint")]
        public string Footprint { get; set; }

        [JsonPropertyName("palette")]
        public string Palette { get; set; }

        [JsonPropertyName("shinyPalette")]
        public string ShinyPalette { get; set; }
    }

    public class PokemonMetadata
    {
        [JsonPropertyName("bodyColor")]
        public string BodyColor { get; set; }

        [JsonPropertyName("baseFriendship")]
        public int BaseFriendship { get; set; }

        [JsonPropertyName("growthRate")]
        public string GrowthRate { get; set; }

        [JsonPropertyName("safariZoneFleeRate")]
        public int SafariZoneFleeRate { get; set; }

        [JsonPropertyName("spriteFlipped")]
        public bool SpriteFlipped { get; set; }
    }

    /// <summary>
    /// PokeSharp move definition (JSON output format)
    /// </summary>
    public class MoveDefinition
    {
        [JsonPropertyName("id")]
        public int Id { get; set; }

        [JsonPropertyName("name")]
        public string Name { get; set; }

        [JsonPropertyName("type")]
        public string Type { get; set; }

        [JsonPropertyName("category")]
        public string Category { get; set; } // Physical, Special, Status

        [JsonPropertyName("power")]
        public int? Power { get; set; }

        [JsonPropertyName("accuracy")]
        public int? Accuracy { get; set; }

        [JsonPropertyName("pp")]
        public int PP { get; set; }

        [JsonPropertyName("priority")]
        public int Priority { get; set; }

        [JsonPropertyName("target")]
        public string Target { get; set; }

        [JsonPropertyName("effect")]
        public MoveEffect Effect { get; set; }

        [JsonPropertyName("secondaryEffectChance")]
        public int? SecondaryEffectChance { get; set; }

        [JsonPropertyName("flags")]
        public MoveFlags Flags { get; set; }

        [JsonPropertyName("description")]
        public string Description { get; set; }
    }

    public class MoveEffect
    {
        [JsonPropertyName("type")]
        public string Type { get; set; }

        [JsonPropertyName("parameters")]
        public Dictionary<string, object> Parameters { get; set; }
    }

    public class MoveFlags
    {
        [JsonPropertyName("makesContact")]
        public bool MakesContact { get; set; }

        [JsonPropertyName("affectedByProtect")]
        public bool AffectedByProtect { get; set; }

        [JsonPropertyName("affectedByMirrorMove")]
        public bool AffectedByMirrorMove { get; set; }

        [JsonPropertyName("affectedByKingsRock")]
        public bool AffectedByKingsRock { get; set; }

        [JsonPropertyName("soundBased")]
        public bool SoundBased { get; set; }

        [JsonPropertyName("punchMove")]
        public bool PunchMove { get; set; }
    }

    /// <summary>
    /// PokeSharp map definition (JSON output format)
    /// </summary>
    public class PokeSharpMapDefinition
    {
        [JsonPropertyName("id")]
        public string Id { get; set; }

        [JsonPropertyName("name")]
        public string Name { get; set; }

        [JsonPropertyName("tiledMapPath")]
        public string TiledMapPath { get; set; }

        [JsonPropertyName("music")]
        public string Music { get; set; }

        [JsonPropertyName("weather")]
        public string Weather { get; set; }

        [JsonPropertyName("mapType")]
        public string MapType { get; set; }

        [JsonPropertyName("battleScene")]
        public string BattleScene { get; set; }

        [JsonPropertyName("flags")]
        public MapFlags Flags { get; set; }

        [JsonPropertyName("connections")]
        public MapConnection[] Connections { get; set; }

        [JsonPropertyName("objectEvents")]
        public ObjectEvent[] ObjectEvents { get; set; }

        [JsonPropertyName("warps")]
        public WarpEvent[] Warps { get; set; }

        [JsonPropertyName("scriptTriggers")]
        public ScriptTrigger[] ScriptTriggers { get; set; }

        [JsonPropertyName("backgroundEvents")]
        public BackgroundEvent[] BackgroundEvents { get; set; }
    }

    public class MapFlags
    {
        [JsonPropertyName("showMapName")]
        public bool ShowMapName { get; set; }

        [JsonPropertyName("allowCycling")]
        public bool AllowCycling { get; set; }

        [JsonPropertyName("allowRunning")]
        public bool AllowRunning { get; set; }

        [JsonPropertyName("allowEscaping")]
        public bool AllowEscaping { get; set; }

        [JsonPropertyName("requiresFlash")]
        public bool RequiresFlash { get; set; }
    }

    public class MapConnection
    {
        [JsonPropertyName("direction")]
        public string Direction { get; set; }

        [JsonPropertyName("offset")]
        public int Offset { get; set; }

        [JsonPropertyName("targetMap")]
        public string TargetMap { get; set; }
    }

    public class ObjectEvent
    {
        [JsonPropertyName("id")]
        public string Id { get; set; }

        [JsonPropertyName("type")]
        public string Type { get; set; } // NPC, Trainer, Item, BerryTree

        [JsonPropertyName("graphicsId")]
        public string GraphicsId { get; set; }

        [JsonPropertyName("position")]
        public Position Position { get; set; }

        [JsonPropertyName("movement")]
        public MovementData Movement { get; set; }

        [JsonPropertyName("script")]
        public string Script { get; set; }

        [JsonPropertyName("flag")]
        public string Flag { get; set; }

        [JsonPropertyName("trainerData")]
        public TrainerData TrainerData { get; set; }
    }

    public class Position
    {
        [JsonPropertyName("x")]
        public int X { get; set; }

        [JsonPropertyName("y")]
        public int Y { get; set; }

        [JsonPropertyName("elevation")]
        public int Elevation { get; set; }
    }

    public class MovementData
    {
        [JsonPropertyName("type")]
        public string Type { get; set; }

        [JsonPropertyName("rangeX")]
        public int RangeX { get; set; }

        [JsonPropertyName("rangeY")]
        public int RangeY { get; set; }
    }

    public class TrainerData
    {
        [JsonPropertyName("trainerType")]
        public string TrainerType { get; set; }

        [JsonPropertyName("sightRange")]
        public int SightRange { get; set; }
    }

    public class WarpEvent
    {
        [JsonPropertyName("position")]
        public Position Position { get; set; }

        [JsonPropertyName("destMap")]
        public string DestMap { get; set; }

        [JsonPropertyName("destWarpId")]
        public int DestWarpId { get; set; }
    }

    public class ScriptTrigger
    {
        [JsonPropertyName("type")]
        public string Type { get; set; }

        [JsonPropertyName("position")]
        public Position Position { get; set; }

        [JsonPropertyName("var")]
        public string Var { get; set; }

        [JsonPropertyName("value")]
        public int Value { get; set; }

        [JsonPropertyName("script")]
        public string Script { get; set; }
    }

    public class BackgroundEvent
    {
        [JsonPropertyName("type")]
        public string Type { get; set; }

        [JsonPropertyName("position")]
        public Position Position { get; set; }

        [JsonPropertyName("facingDirection")]
        public string FacingDirection { get; set; }

        [JsonPropertyName("script")]
        public string Script { get; set; }
    }

    // ========================================================================
    // TILED FORMAT COMPATIBILITY
    // ========================================================================

    /// <summary>
    /// Tiled map format for compatibility with existing PokeSharp map loader
    /// </summary>
    public class TiledMapData
    {
        [JsonPropertyName("version")]
        public string Version { get; set; } = "1.10";

        [JsonPropertyName("tiledversion")]
        public string TiledVersion { get; set; } = "1.10.0";

        [JsonPropertyName("type")]
        public string Type { get; set; } = "map";

        [JsonPropertyName("width")]
        public int Width { get; set; }

        [JsonPropertyName("height")]
        public int Height { get; set; }

        [JsonPropertyName("tilewidth")]
        public int TileWidth { get; set; } = 16;

        [JsonPropertyName("tileheight")]
        public int TileHeight { get; set; } = 16;

        [JsonPropertyName("layers")]
        public List<TiledLayer> Layers { get; set; }

        [JsonPropertyName("tilesets")]
        public List<TiledTileset> Tilesets { get; set; }

        [JsonPropertyName("properties")]
        public List<TiledProperty> Properties { get; set; }
    }

    public class TiledLayer
    {
        [JsonPropertyName("id")]
        public int Id { get; set; }

        [JsonPropertyName("name")]
        public string Name { get; set; }

        [JsonPropertyName("type")]
        public string Type { get; set; }

        [JsonPropertyName("data")]
        public int[] Data { get; set; }

        [JsonPropertyName("width")]
        public int Width { get; set; }

        [JsonPropertyName("height")]
        public int Height { get; set; }
    }

    public class TiledTileset
    {
        [JsonPropertyName("firstgid")]
        public int FirstGid { get; set; }

        [JsonPropertyName("source")]
        public string Source { get; set; }
    }

    public class TiledProperty
    {
        [JsonPropertyName("name")]
        public string Name { get; set; }

        [JsonPropertyName("type")]
        public string Type { get; set; }

        [JsonPropertyName("value")]
        public object Value { get; set; }
    }

    // ========================================================================
    // GRAPHICS REFERENCES
    // ========================================================================

    public class GraphicsReference
    {
        [JsonPropertyName("assetPath")]
        public string AssetPath { get; set; }

        [JsonPropertyName("sourceFile")]
        public string SourceFile { get; set; }

        [JsonPropertyName("format")]
        public string Format { get; set; } // PNG, 4BPP, etc.
    }

    public class TilesetReference
    {
        [JsonPropertyName("name")]
        public string Name { get; set; }

        [JsonPropertyName("tilesPath")]
        public string TilesPath { get; set; }

        [JsonPropertyName("metatiles")]
        public string MetatilesPath { get; set; }

        [JsonPropertyName("palette")]
        public string PalettePath { get; set; }
    }

    public class PaletteReference
    {
        [JsonPropertyName("name")]
        public string Name { get; set; }

        [JsonPropertyName("path")]
        public string Path { get; set; }
    }

    // ========================================================================
    // SCRIPT CONVERSION STRUCTURES
    // ========================================================================

    public class PoryScriptFile
    {
        public string FilePath { get; set; }
        public List<PoryScript> Scripts { get; set; }
    }

    public class PoryScript
    {
        public string Name { get; set; }
        public List<PoryCommand> Commands { get; set; }
    }

    public class PoryCommand
    {
        public string Command { get; set; }
        public List<string> Arguments { get; set; }
    }

    public class CSharpScriptFile
    {
        public string FilePath { get; set; }
        public List<CSharpScript> Scripts { get; set; }
    }

    public class CSharpScript
    {
        public string Name { get; set; }
        public string CSharpCode { get; set; }
    }

    // ========================================================================
    // HELPER STRUCTURES
    // ========================================================================

    public class ValidationResult
    {
        public bool IsValid { get; set; }
        public List<string> Errors { get; set; }
        public List<string> Warnings { get; set; }
    }

    public class MoveLearnset
    {
        public List<LevelUpMove> Moves { get; set; }
    }

    public class TmHmLearnset
    {
        public List<string> Moves { get; set; }
    }

    public class TutorLearnset
    {
        public List<string> Moves { get; set; }
    }

    public class EggMoveList
    {
        public List<string> Moves { get; set; }
    }

    public class EvolutionData
    {
        public List<Evolution> Evolutions { get; set; }
    }

    // ========================================================================
    // CONVERSION PIPELINE ARCHITECTURE
    // ========================================================================

    /// <summary>
    /// Orchestrates the complete conversion process from pokeemerald to PokeSharp
    /// </summary>
    public interface IConversionPipeline
    {
        /// <summary>Initialize pipeline with source and target directories</summary>
        void Initialize(string pokeemeraldPath, string pokeSharpPath);

        /// <summary>Convert all Pokemon species data</summary>
        ConversionReport ConvertPokemonSpecies();

        /// <summary>Convert all move data</summary>
        ConversionReport ConvertMoves();

        /// <summary>Convert all maps</summary>
        ConversionReport ConvertMaps();

        /// <summary>Convert all scripts</summary>
        ConversionReport ConvertScripts();

        /// <summary>Generate graphics mapping manifest</summary>
        ConversionReport GenerateGraphicsManifest();

        /// <summary>Run complete conversion pipeline</summary>
        FullConversionReport ConvertAll();
    }

    public class ConversionReport
    {
        public int TotalItems { get; set; }
        public int SuccessfulConversions { get; set; }
        public int FailedConversions { get; set; }
        public List<ConversionError> Errors { get; set; }
        public List<string> Warnings { get; set; }
        public TimeSpan Duration { get; set; }
    }

    public class FullConversionReport
    {
        public ConversionReport PokemonReport { get; set; }
        public ConversionReport MovesReport { get; set; }
        public ConversionReport MapsReport { get; set; }
        public ConversionReport ScriptsReport { get; set; }
        public ConversionReport GraphicsReport { get; set; }
        public TimeSpan TotalDuration { get; set; }
    }

    public class ConversionError
    {
        public string ItemId { get; set; }
        public string ErrorMessage { get; set; }
        public Exception Exception { get; set; }
    }
}

// ============================================================================
// EXAMPLE CONVERSION USAGE
// ============================================================================
/*

// Example: Converting Bulbasaur from pokeemerald to PokeSharp

INPUT (pokeemerald species_info.h):
[SPECIES_BULBASAUR] =
{
    .baseHP        = 45,
    .baseAttack    = 49,
    .baseDefense   = 49,
    .baseSpeed     = 45,
    .baseSpAttack  = 65,
    .baseSpDefense = 65,
    .types = { TYPE_GRASS, TYPE_POISON },
    .catchRate = 45,
    .expYield = 64,
    .evYield_SpAttack  = 1,
    .itemCommon = ITEM_NONE,
    .itemRare   = ITEM_NONE,
    .genderRatio = PERCENT_FEMALE(12.5),
    .eggCycles = 20,
    .friendship = STANDARD_FRIENDSHIP,
    .growthRate = GROWTH_MEDIUM_SLOW,
    .eggGroups = { EGG_GROUP_MONSTER, EGG_GROUP_GRASS },
    .abilities = {ABILITY_OVERGROW, ABILITY_NONE},
    .bodyColor = BODY_COLOR_GREEN,
}

OUTPUT (PokeSharp JSON):
{
  "id": 1,
  "name": "Bulbasaur",
  "nationalDexNumber": 1,
  "hoennDexNumber": null,
  "baseStats": {
    "hp": 45,
    "attack": 49,
    "defense": 49,
    "spAttack": 65,
    "spDefense": 65,
    "speed": 45,
    "total": 318
  },
  "types": ["Grass", "Poison"],
  "abilities": ["Overgrow"],
  "hiddenAbility": null,
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
    "eggGroups": ["Monster", "Grass"],
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
      { "level": 7, "move": "Leech Seed" }
    ],
    "tmHm": ["Cut", "Toxic", "Hidden Power"],
    "eggMoves": ["Skull Bash", "Charm"],
    "tutorMoves": ["Body Slam"]
  },
  "evolutions": [
    {
      "method": "Level",
      "parameter": 16,
      "targetSpecies": "Ivysaur"
    }
  ],
  "description": {
    "category": "Seed Pokemon",
    "pokedexEntry": "A strange seed was planted on its back at birth...",
    "height": 0.7,
    "weight": 6.9
  },
  "graphics": {
    "frontSprite": "sprites/pokemon/bulbasaur/front.png",
    "frontSpriteShiny": "sprites/pokemon/bulbasaur/front_shiny.png",
    "backSprite": "sprites/pokemon/bulbasaur/back.png",
    "backSpriteShiny": "sprites/pokemon/bulbasaur/back_shiny.png",
    "icon": "sprites/pokemon/bulbasaur/icon.png",
    "footprint": "sprites/pokemon/bulbasaur/footprint.png",
    "palette": "palettes/pokemon/bulbasaur/normal.pal",
    "shinyPalette": "palettes/pokemon/bulbasaur/shiny.pal"
  },
  "metadata": {
    "bodyColor": "Green",
    "baseFriendship": 70,
    "growthRate": "MediumSlow",
    "safariZoneFleeRate": 0,
    "spriteFlipped": false
  }
}

*/
