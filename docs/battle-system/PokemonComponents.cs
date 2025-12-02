using Arch.Core;
using PokeSharp.Game.Components.Relationships;

namespace PokeSharp.Game.Components.Battle;

/// <summary>
///     Core component representing a Pokemon entity with all battle-relevant data.
///     This is a data-only struct following ECS principles.
/// </summary>
public struct Pokemon
{
    /// <summary>
    ///     Species identifier (e.g., "bulbasaur", "charizard").
    ///     Links to species data for base stats, type, etc.
    /// </summary>
    public string SpeciesId { get; set; }

    /// <summary>
    ///     Pokemon's current level (1-100).
    /// </summary>
    public byte Level { get; set; }

    /// <summary>
    ///     Pokemon's current experience points.
    /// </summary>
    public uint Experience { get; set; }

    /// <summary>
    ///     Pokemon's current HP.
    /// </summary>
    public ushort CurrentHp { get; set; }

    /// <summary>
    ///     Pokemon's maximum HP (calculated from base stats + IVs + EVs).
    /// </summary>
    public ushort MaxHp { get; set; }

    /// <summary>
    ///     Current status condition (0 = none, 1 = poisoned, 2 = burned, etc.).
    ///     Uses byte to save memory; maps to StatusCondition enum.
    /// </summary>
    public byte StatusCondition { get; set; }

    /// <summary>
    ///     Turns remaining for status conditions (sleep counter, toxic counter, etc.).
    /// </summary>
    public byte StatusTurns { get; set; }

    /// <summary>
    ///     Pokemon's friendship/happiness value (0-255).
    /// </summary>
    public byte Friendship { get; set; }

    /// <summary>
    ///     Gender: 0 = male, 1 = female, 2 = genderless.
    /// </summary>
    public byte Gender { get; set; }

    /// <summary>
    ///     Nature identifier (affects stat growth).
    /// </summary>
    public byte Nature { get; set; }

    /// <summary>
    ///     Ability identifier (e.g., Overgrow, Blaze).
    /// </summary>
    public ushort AbilityId { get; set; }

    /// <summary>
    ///     Held item identifier (0 = no item).
    /// </summary>
    public ushort HeldItemId { get; set; }

    /// <summary>
    ///     Original trainer ID for ownership tracking.
    /// </summary>
    public uint OriginalTrainerId { get; set; }

    /// <summary>
    ///     Personality value (determines shininess, gender, nature, etc.).
    /// </summary>
    public uint PersonalityValue { get; set; }

    /// <summary>
    ///     Whether this Pokemon is shiny.
    /// </summary>
    public bool IsShiny { get; set; }

    /// <summary>
    ///     Constructs a new Pokemon with required fields.
    /// </summary>
    public Pokemon(string speciesId, byte level)
    {
        SpeciesId = speciesId;
        Level = level;
        Experience = 0;
        CurrentHp = 0;
        MaxHp = 0;
        StatusCondition = 0;
        StatusTurns = 0;
        Friendship = 70;
        Gender = 0;
        Nature = 0;
        AbilityId = 0;
        HeldItemId = 0;
        OriginalTrainerId = 0;
        PersonalityValue = 0;
        IsShiny = false;
    }
}

/// <summary>
///     Component holding a Pokemon's Individual Values (genetics).
///     Determines hidden stat potential (0-31 per stat).
/// </summary>
public struct PokemonIVs
{
    public byte HP { get; set; }
    public byte Attack { get; set; }
    public byte Defense { get; set; }
    public byte SpecialAttack { get; set; }
    public byte SpecialDefense { get; set; }
    public byte Speed { get; set; }

    /// <summary>
    ///     Creates IVs with random values (0-31).
    /// </summary>
    public static PokemonIVs Random(Random? rng = null)
    {
        rng ??= Random.Shared;
        return new PokemonIVs
        {
            HP = (byte)rng.Next(32),
            Attack = (byte)rng.Next(32),
            Defense = (byte)rng.Next(32),
            SpecialAttack = (byte)rng.Next(32),
            SpecialDefense = (byte)rng.Next(32),
            Speed = (byte)rng.Next(32),
        };
    }
}

/// <summary>
///     Component holding a Pokemon's Effort Values (training/battle experience).
///     Gained from defeating other Pokemon (0-255 per stat, max 510 total).
/// </summary>
public struct PokemonEVs
{
    public byte HP { get; set; }
    public byte Attack { get; set; }
    public byte Defense { get; set; }
    public byte SpecialAttack { get; set; }
    public byte SpecialDefense { get; set; }
    public byte Speed { get; set; }

    /// <summary>
    ///     Calculates total EVs (cannot exceed 510).
    /// </summary>
    public readonly int Total => HP + Attack + Defense + SpecialAttack + SpecialDefense + Speed;
}

/// <summary>
///     Component holding a Pokemon's calculated battle stats.
///     Computed from base stats + IVs + EVs + level + nature.
/// </summary>
public struct PokemonStats
{
    public ushort Attack { get; set; }
    public ushort Defense { get; set; }
    public ushort SpecialAttack { get; set; }
    public ushort SpecialDefense { get; set; }
    public ushort Speed { get; set; }

    /// <summary>
    ///     Accuracy modifier (starts at 0, can range from -6 to +6).
    /// </summary>
    public sbyte AccuracyStage { get; set; }

    /// <summary>
    ///     Evasion modifier (starts at 0, can range from -6 to +6).
    /// </summary>
    public sbyte EvasionStage { get; set; }
}

/// <summary>
///     Component holding temporary battle stat modifiers (stages).
///     Reset when Pokemon switches out.
/// </summary>
public struct PokemonStatModifiers
{
    /// <summary>
    ///     Attack stage modifier (-6 to +6).
    /// </summary>
    public sbyte AttackStage { get; set; }

    /// <summary>
    ///     Defense stage modifier (-6 to +6).
    /// </summary>
    public sbyte DefenseStage { get; set; }

    /// <summary>
    ///     Special Attack stage modifier (-6 to +6).
    /// </summary>
    public sbyte SpecialAttackStage { get; set; }

    /// <summary>
    ///     Special Defense stage modifier (-6 to +6).
    /// </summary>
    public sbyte SpecialDefenseStage { get; set; }

    /// <summary>
    ///     Speed stage modifier (-6 to +6).
    /// </summary>
    public sbyte SpeedStage { get; set; }

    /// <summary>
    ///     Accuracy stage modifier (-6 to +6).
    /// </summary>
    public sbyte AccuracyStage { get; set; }

    /// <summary>
    ///     Evasion stage modifier (-6 to +6).
    /// </summary>
    public sbyte EvasionStage { get; set; }

    /// <summary>
    ///     Resets all stat modifiers to 0.
    /// </summary>
    public void Reset()
    {
        AttackStage = 0;
        DefenseStage = 0;
        SpecialAttackStage = 0;
        SpecialDefenseStage = 0;
        SpeedStage = 0;
        AccuracyStage = 0;
        EvasionStage = 0;
    }

    /// <summary>
    ///     Gets the multiplier for a given stage (-6 to +6).
    /// </summary>
    public static float GetStageMultiplier(sbyte stage)
    {
        // Clamp to valid range
        stage = Math.Max((sbyte)-6, Math.Min((sbyte)6, stage));

        // Stage formula: positive = (2+stage)/2, negative = 2/(2-stage)
        return stage >= 0 ? (2 + stage) / 2.0f : 2.0f / (2 - stage);
    }
}

/// <summary>
///     Component holding a Pokemon's moveset (up to 4 moves).
///     Uses fixed-size array for performance.
/// </summary>
public struct PokemonMoveSet
{
    /// <summary>
    ///     Move IDs (0 = empty slot). Max 4 moves.
    /// </summary>
    public ushort Move1Id { get; set; }
    public ushort Move2Id { get; set; }
    public ushort Move3Id { get; set; }
    public ushort Move4Id { get; set; }

    /// <summary>
    ///     Current PP for each move.
    /// </summary>
    public byte Move1PP { get; set; }
    public byte Move2PP { get; set; }
    public byte Move3PP { get; set; }
    public byte Move4PP { get; set; }

    /// <summary>
    ///     Maximum PP for each move (base PP + PP Ups).
    /// </summary>
    public byte Move1MaxPP { get; set; }
    public byte Move2MaxPP { get; set; }
    public byte Move3MaxPP { get; set; }
    public byte Move4MaxPP { get; set; }

    /// <summary>
    ///     Gets the move ID at the specified slot (0-3).
    /// </summary>
    public readonly ushort GetMoveId(int slot)
    {
        return slot switch
        {
            0 => Move1Id,
            1 => Move2Id,
            2 => Move3Id,
            3 => Move4Id,
            _ => 0,
        };
    }

    /// <summary>
    ///     Gets the current PP for the specified slot (0-3).
    /// </summary>
    public readonly byte GetCurrentPP(int slot)
    {
        return slot switch
        {
            0 => Move1PP,
            1 => Move2PP,
            2 => Move3PP,
            3 => Move4PP,
            _ => 0,
        };
    }

    /// <summary>
    ///     Checks if a move slot has usable PP.
    /// </summary>
    public readonly bool HasPP(int slot)
    {
        return GetCurrentPP(slot) > 0;
    }

    /// <summary>
    ///     Decrements PP for the specified move slot.
    /// </summary>
    public void DecrementPP(int slot)
    {
        switch (slot)
        {
            case 0:
                if (Move1PP > 0)
                    Move1PP--;
                break;
            case 1:
                if (Move2PP > 0)
                    Move2PP--;
                break;
            case 2:
                if (Move3PP > 0)
                    Move3PP--;
                break;
            case 3:
                if (Move4PP > 0)
                    Move4PP--;
                break;
        }
    }
}

/// <summary>
///     Component marking a Pokemon as being in a party.
///     Links to the trainer entity that owns this Pokemon.
/// </summary>
public struct InParty
{
    /// <summary>
    ///     Reference to the trainer/player entity that owns this Pokemon.
    /// </summary>
    public EntityRef TrainerRef { get; set; }

    /// <summary>
    ///     Position in the party (0-5, where 0 is the lead Pokemon).
    /// </summary>
    public byte PartySlot { get; set; }

    public InParty(Entity trainer, byte partySlot)
    {
        TrainerRef = new EntityRef(trainer);
        PartySlot = partySlot;
    }
}

/// <summary>
///     Tag component marking a Pokemon as currently active in battle.
///     Only one Pokemon per trainer can have this at a time in single battles.
/// </summary>
public struct ActiveInBattle
{
    /// <summary>
    ///     Reference to the battle entity this Pokemon is participating in.
    /// </summary>
    public EntityRef BattleRef { get; set; }

    /// <summary>
    ///     Battle slot (0 = player side, 1 = opponent side in single battle).
    ///     For double battles: 0-1 = player, 2-3 = opponent.
    /// </summary>
    public byte BattleSlot { get; set; }

    public ActiveInBattle(Entity battle, byte battleSlot)
    {
        BattleRef = new EntityRef(battle);
        BattleSlot = battleSlot;
    }
}

/// <summary>
///     Component for volatile battle conditions that reset when Pokemon switches out.
/// </summary>
public struct VolatileStatus
{
    /// <summary>
    ///     Confusion status (turns remaining).
    /// </summary>
    public byte ConfusionTurns { get; set; }

    /// <summary>
    ///     Flinch flag (lasts one turn).
    /// </summary>
    public bool Flinched { get; set; }

    /// <summary>
    ///     Infatuation flag (attracted to opponent).
    /// </summary>
    public bool Infatuated { get; set; }

    /// <summary>
    ///     Leech Seed flag.
    /// </summary>
    public bool LeechSeeded { get; set; }

    /// <summary>
    ///     Curse flag (Ghost-type Curse).
    /// </summary>
    public bool Cursed { get; set; }

    /// <summary>
    ///     Trapped/bound turns remaining (Wrap, Bind, etc.).
    /// </summary>
    public byte TrappedTurns { get; set; }

    /// <summary>
    ///     Reference to Pokemon that trapped this one.
    /// </summary>
    public EntityRef TrappedBy { get; set; }

    /// <summary>
    ///     Substitute HP remaining (0 = no substitute).
    /// </summary>
    public ushort SubstituteHp { get; set; }

    /// <summary>
    ///     Whether this Pokemon must use the same move (Encore, Choice items).
    /// </summary>
    public bool LockedIntoMove { get; set; }

    /// <summary>
    ///     The move slot this Pokemon is locked into (0-3).
    /// </summary>
    public byte LockedMoveSlot { get; set; }

    /// <summary>
    ///     Turns remaining for locked move.
    /// </summary>
    public byte LockedMoveTurns { get; set; }

    /// <summary>
    ///     Resets all volatile status conditions.
    /// </summary>
    public void Reset()
    {
        ConfusionTurns = 0;
        Flinched = false;
        Infatuated = false;
        LeechSeeded = false;
        Cursed = false;
        TrappedTurns = 0;
        TrappedBy = EntityRef.Null;
        SubstituteHp = 0;
        LockedIntoMove = false;
        LockedMoveSlot = 0;
        LockedMoveTurns = 0;
    }
}

/// <summary>
///     Component tracking Pokemon's type effectiveness multipliers during battle.
///     Cached to avoid recalculation.
/// </summary>
public struct TypeEffectiveness
{
    /// <summary>
    ///     Primary type (e.g., Fire, Water, Grass).
    /// </summary>
    public byte Type1 { get; set; }

    /// <summary>
    ///     Secondary type (0 = no secondary type).
    /// </summary>
    public byte Type2 { get; set; }

    /// <summary>
    ///     Whether types have been modified by abilities or moves (Forest's Curse, Soak, etc.).
    /// </summary>
    public bool TypesModified { get; set; }
}

/// <summary>
///     Status condition enumeration for Pokemon.StatusCondition byte field.
/// </summary>
public enum StatusCondition : byte
{
    None = 0,
    Poisoned = 1,
    BadlyPoisoned = 2,
    Burned = 3,
    Paralyzed = 4,
    Asleep = 5,
    Frozen = 6,
}
