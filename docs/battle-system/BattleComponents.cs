using Arch.Core;
using PokeSharp.Game.Components.Relationships;

namespace PokeSharp.Game.Components.Battle;

/// <summary>
///     Component representing a battle instance entity.
///     This is the root entity for a battle that contains battle-wide state.
/// </summary>
public struct BattleState
{
    /// <summary>
    ///     Unique battle identifier.
    /// </summary>
    public string BattleId { get; set; }

    /// <summary>
    ///     Battle type: 0 = Wild, 1 = Trainer, 2 = Multi/Tag.
    /// </summary>
    public byte BattleType { get; set; }

    /// <summary>
    ///     Battle format: 0 = Single, 1 = Double.
    /// </summary>
    public byte BattleFormat { get; set; }

    /// <summary>
    ///     Current turn number.
    /// </summary>
    public ushort TurnNumber { get; set; }

    /// <summary>
    ///     Current battle phase (see BattlePhase enum).
    /// </summary>
    public byte Phase { get; set; }

    /// <summary>
    ///     Whether the battle is over.
    /// </summary>
    public bool IsComplete { get; set; }

    /// <summary>
    ///     Winner: 0 = player, 1 = opponent, 2 = draw/fled.
    /// </summary>
    public byte Winner { get; set; }

    /// <summary>
    ///     Can the player run from this battle?
    /// </summary>
    public bool CanEscape { get; set; }

    /// <summary>
    ///     Number of failed escape attempts.
    /// </summary>
    public byte EscapeAttempts { get; set; }

    public BattleState(string battleId, byte battleType, byte battleFormat)
    {
        BattleId = battleId;
        BattleType = battleType;
        BattleFormat = battleFormat;
        TurnNumber = 0;
        Phase = (byte)BattlePhase.Initialize;
        IsComplete = false;
        Winner = 0;
        CanEscape = battleType == 0; // Can escape from wild battles
        EscapeAttempts = 0;
    }
}

/// <summary>
///     Component representing a battle participant (player or opponent trainer).
/// </summary>
public struct BattleParticipant
{
    /// <summary>
    ///     Reference to the battle entity.
    /// </summary>
    public EntityRef BattleRef { get; set; }

    /// <summary>
    ///     Reference to the trainer entity (player or NPC).
    /// </summary>
    public EntityRef TrainerRef { get; set; }

    /// <summary>
    ///     Side: 0 = player, 1 = opponent.
    /// </summary>
    public byte Side { get; set; }

    /// <summary>
    ///     Whether this participant is controlled by AI.
    /// </summary>
    public bool IsAI { get; set; }

    /// <summary>
    ///     AI difficulty level (0-10, higher = smarter).
    /// </summary>
    public byte AIDifficulty { get; set; }

    /// <summary>
    ///     Money awarded if this trainer loses (trainer battles only).
    /// </summary>
    public uint PrizeMoney { get; set; }

    public BattleParticipant(Entity battle, Entity trainer, byte side, bool isAI)
    {
        BattleRef = new EntityRef(battle);
        TrainerRef = new EntityRef(trainer);
        Side = side;
        IsAI = isAI;
        AIDifficulty = 5;
        PrizeMoney = 0;
    }
}

/// <summary>
///     Component for side-wide battle effects (Reflect, Light Screen, hazards, etc.).
/// </summary>
public struct BattleSideState
{
    /// <summary>
    ///     Reference to the battle entity.
    /// </summary>
    public EntityRef BattleRef { get; set; }

    /// <summary>
    ///     Side: 0 = player, 1 = opponent.
    /// </summary>
    public byte Side { get; set; }

    /// <summary>
    ///     Reflect turns remaining (halves physical damage).
    /// </summary>
    public byte ReflectTurns { get; set; }

    /// <summary>
    ///     Light Screen turns remaining (halves special damage).
    /// </summary>
    public byte LightScreenTurns { get; set; }

    /// <summary>
    ///     Mist turns remaining (prevents stat reduction).
    /// </summary>
    public byte MistTurns { get; set; }

    /// <summary>
    ///     Safeguard turns remaining (prevents status conditions).
    /// </summary>
    public byte SafeguardTurns { get; set; }

    /// <summary>
    ///     Tailwind turns remaining (doubles speed).
    /// </summary>
    public byte TailwindTurns { get; set; }

    /// <summary>
    ///     Entry hazard layers.
    /// </summary>
    public byte SpikesLayers { get; set; } // 0-3 layers
    public byte ToxicSpikesLayers { get; set; } // 0-2 layers
    public bool StealthRock { get; set; }
    public bool StickyWeb { get; set; }

    /// <summary>
    ///     Field effects on this side.
    /// </summary>
    public bool Wish { get; set; }
    public byte WishTurns { get; set; }
    public ushort WishHealAmount { get; set; }

    public BattleSideState(Entity battle, byte side)
    {
        BattleRef = new EntityRef(battle);
        Side = side;
        ReflectTurns = 0;
        LightScreenTurns = 0;
        MistTurns = 0;
        SafeguardTurns = 0;
        TailwindTurns = 0;
        SpikesLayers = 0;
        ToxicSpikesLayers = 0;
        StealthRock = false;
        StickyWeb = false;
        Wish = false;
        WishTurns = 0;
        WishHealAmount = 0;
    }
}

/// <summary>
///     Component for battle-wide field effects (weather, terrain, etc.).
/// </summary>
public struct BattleFieldState
{
    /// <summary>
    ///     Reference to the battle entity.
    /// </summary>
    public EntityRef BattleRef { get; set; }

    /// <summary>
    ///     Current weather condition (see WeatherCondition enum).
    /// </summary>
    public byte Weather { get; set; }

    /// <summary>
    ///     Weather turns remaining (0 = infinite/ability weather).
    /// </summary>
    public byte WeatherTurns { get; set; }

    /// <summary>
    ///     Current terrain (see TerrainType enum).
    /// </summary>
    public byte Terrain { get; set; }

    /// <summary>
    ///     Terrain turns remaining.
    /// </summary>
    public byte TerrainTurns { get; set; }

    /// <summary>
    ///     Trick Room active (reverses speed order).
    /// </summary>
    public bool TrickRoom { get; set; }

    /// <summary>
    ///     Trick Room turns remaining.
    /// </summary>
    public byte TrickRoomTurns { get; set; }

    /// <summary>
    ///     Magic Room active (suppresses held items).
    /// </summary>
    public bool MagicRoom { get; set; }

    /// <summary>
    ///     Magic Room turns remaining.
    /// </summary>
    public byte MagicRoomTurns { get; set; }

    /// <summary>
    ///     Wonder Room active (swaps Defense and Special Defense).
    /// </summary>
    public bool WonderRoom { get; set; }

    /// <summary>
    ///     Wonder Room turns remaining.
    /// </summary>
    public byte WonderRoomTurns { get; set; }

    /// <summary>
    ///     Gravity active (grounds all Pokemon, increases accuracy).
    /// </summary>
    public bool Gravity { get; set; }

    /// <summary>
    ///     Gravity turns remaining.
    /// </summary>
    public byte GravityTurns { get; set; }

    public BattleFieldState(Entity battle)
    {
        BattleRef = new EntityRef(battle);
        Weather = (byte)WeatherCondition.None;
        WeatherTurns = 0;
        Terrain = 0;
        TerrainTurns = 0;
        TrickRoom = false;
        TrickRoomTurns = 0;
        MagicRoom = false;
        MagicRoomTurns = 0;
        WonderRoom = false;
        WonderRoomTurns = 0;
        Gravity = false;
        GravityTurns = 0;
    }
}

/// <summary>
///     Component representing a pending battle action (move, switch, item, run).
///     Uses component pooling pattern like MovementRequest.
/// </summary>
public struct BattleActionRequest
{
    /// <summary>
    ///     Action type (see BattleActionType enum).
    /// </summary>
    public byte ActionType { get; set; }

    /// <summary>
    ///     Reference to the Pokemon performing the action.
    /// </summary>
    public EntityRef ActorRef { get; set; }

    /// <summary>
    ///     Reference to the target Pokemon (for moves/items).
    /// </summary>
    public EntityRef TargetRef { get; set; }

    /// <summary>
    ///     Move slot (0-3) for move actions.
    /// </summary>
    public byte MoveSlot { get; set; }

    /// <summary>
    ///     Item ID for item actions.
    /// </summary>
    public ushort ItemId { get; set; }

    /// <summary>
    ///     Switch target party slot (0-5) for switch actions.
    /// </summary>
    public byte SwitchSlot { get; set; }

    /// <summary>
    ///     Priority of this action (calculated from move priority + speed).
    /// </summary>
    public int Priority { get; set; }

    /// <summary>
    ///     Whether this action is active and pending processing.
    ///     Uses pooling pattern to avoid archetype transitions.
    /// </summary>
    public bool Active { get; set; }

    public BattleActionRequest(byte actionType, Entity actor)
    {
        ActionType = actionType;
        ActorRef = new EntityRef(actor);
        TargetRef = EntityRef.Null;
        MoveSlot = 0;
        ItemId = 0;
        SwitchSlot = 0;
        Priority = 0;
        Active = true;
    }
}

/// <summary>
///     Component for turn execution state.
///     Attached to the battle entity during turn processing.
/// </summary>
public struct TurnExecutionState
{
    /// <summary>
    ///     Current action index being processed.
    /// </summary>
    public byte CurrentActionIndex { get; set; }

    /// <summary>
    ///     Total number of actions this turn.
    /// </summary>
    public byte TotalActions { get; set; }

    /// <summary>
    ///     Whether actions are sorted by priority.
    /// </summary>
    public bool ActionsSorted { get; set; }

    /// <summary>
    ///     Whether the turn is complete.
    /// </summary>
    public bool TurnComplete { get; set; }
}

/// <summary>
///     Battle phase enumeration.
/// </summary>
public enum BattlePhase : byte
{
    Initialize = 0, // Battle setup
    SendOut = 1, // Send out initial Pokemon
    TurnStart = 2, // Start of turn (abilities, weather damage)
    InputWait = 3, // Waiting for player/AI input
    ActionSelect = 4, // Collecting actions from all participants
    TurnExecute = 5, // Executing turn actions
    TurnEnd = 6, // End of turn (status damage, field effects)
    BattleEnd = 7, // Battle complete
}

/// <summary>
///     Battle action type enumeration.
/// </summary>
public enum BattleActionType : byte
{
    Move = 0,
    Switch = 1,
    Item = 2,
    Run = 3,
    Pass = 4, // For double battles when one Pokemon faints
}

/// <summary>
///     Weather condition enumeration.
/// </summary>
public enum WeatherCondition : byte
{
    None = 0,
    Sun = 1, // Harsh sunlight
    Rain = 2, // Rain
    Sandstorm = 3, // Sandstorm
    Hail = 4, // Hail
    HarshSun = 5, // Extremely harsh sunlight (Primal Groudon)
    HeavyRain = 6, // Heavy rain (Primal Kyogre)
    StrongWinds = 7, // Mysterious air current (Mega Rayquaza)
}

/// <summary>
///     Terrain type enumeration.
/// </summary>
public enum TerrainType : byte
{
    None = 0,
    Electric = 1, // Electric Terrain
    Grassy = 2, // Grassy Terrain
    Misty = 3, // Misty Terrain
    Psychic = 4, // Psychic Terrain
}
