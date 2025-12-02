using Arch.Core;
using Arch.Core.Extensions;
using PokeSharp.Engine.Core.Systems;

namespace PokeSharp.Game.Systems.Battle;

/// <summary>
///     System managing battle initialization and lifecycle.
///     Handles battle creation, participant setup, and cleanup.
/// </summary>
public class BattleInitializationSystem : SystemBase
{
    public override int Priority => SystemPriority.BattleInitialization;

    public override void Update(World world, float deltaTime)
    {
        // Query for battles in Initialize phase
        var query = new QueryDescription().WithAll<BattleState>();

        world.Query(
            in query,
            (ref BattleState battle) =>
            {
                if (battle.Phase == (byte)BattlePhase.Initialize)
                {
                    // Initialize battle participants, field state, side states
                    // Transition to SendOut phase
                    battle.Phase = (byte)BattlePhase.SendOut;
                }
            }
        );
    }
}

/// <summary>
///     System managing turn-based battle flow and state transitions.
///     Orchestrates the battle phase progression.
/// </summary>
public class BattleTurnSystem : SystemBase
{
    public override int Priority => SystemPriority.BattleTurn;

    public override void Update(World world, float deltaTime)
    {
        // Query for active battles
        var query = new QueryDescription().WithAll<BattleState>();

        world.Query(
            in query,
            (Entity battleEntity, ref BattleState battle) =>
            {
                if (battle.IsComplete)
                    return;

                switch ((BattlePhase)battle.Phase)
                {
                    case BattlePhase.TurnStart:
                        HandleTurnStart(world, battleEntity, ref battle);
                        break;

                    case BattlePhase.InputWait:
                        // Waiting for player input (UI handles this)
                        break;

                    case BattlePhase.ActionSelect:
                        HandleActionSelection(world, battleEntity, ref battle);
                        break;

                    case BattlePhase.TurnExecute:
                        HandleTurnExecution(world, battleEntity, ref battle);
                        break;

                    case BattlePhase.TurnEnd:
                        HandleTurnEnd(world, battleEntity, ref battle);
                        break;
                }
            }
        );
    }

    private void HandleTurnStart(World world, Entity battleEntity, ref BattleState battle)
    {
        // Process start-of-turn effects:
        // - Ability activations (Intimidate on switch-in, Speed Boost, etc.)
        // - Weather damage
        // - Terrain effects
        // - Side effects (Wish healing, etc.)

        battle.TurnNumber++;
        battle.Phase = (byte)BattlePhase.InputWait;
    }

    private void HandleActionSelection(World world, Entity battleEntity, ref BattleState battle)
    {
        // Collect all BattleActionRequest components
        // Sort by priority (move priority + speed)
        // Transition to TurnExecute when all actions ready

        battle.Phase = (byte)BattlePhase.TurnExecute;
    }

    private void HandleTurnExecution(World world, Entity battleEntity, ref BattleState battle)
    {
        // Execute actions in priority order
        // MoveExecutionSystem handles the actual move logic
        // This system just orchestrates the order

        battle.Phase = (byte)BattlePhase.TurnEnd;
    }

    private void HandleTurnEnd(World world, Entity battleEntity, ref BattleState battle)
    {
        // Process end-of-turn effects:
        // - Status damage (poison, burn)
        // - Leech Seed damage
        // - Held item effects (Leftovers, Black Sludge)
        // - Decrement field effect counters
        // - Check for battle end conditions

        battle.Phase = (byte)BattlePhase.TurnStart;
    }
}

/// <summary>
///     System executing move actions during battle.
///     Handles move targeting, accuracy, damage calculation, and effect application.
/// </summary>
public class MoveExecutionSystem : SystemBase
{
    public override int Priority => SystemPriority.MoveExecution;

    public override void Update(World world, float deltaTime)
    {
        // Query for active move action requests
        var query = new QueryDescription().WithAll<BattleActionRequest>();

        world.Query(
            in query,
            (Entity actionEntity, ref BattleActionRequest action) =>
            {
                if (!action.Active || action.ActionType != (byte)BattleActionType.Move)
                    return;

                // Validate actor and target
                if (!action.ActorRef.IsValid(world))
                {
                    action.Active = false;
                    return;
                }

                // Get actor Pokemon
                var actor = action.ActorRef.Value;
                if (!world.TryGet<Pokemon>(actor, out var pokemon))
                {
                    action.Active = false;
                    return;
                }

                // Execute move:
                // 1. Check PP
                // 2. Check if Pokemon can move (paralysis, sleep, confusion)
                // 3. Calculate accuracy
                // 4. Apply move effects (damage, status, stat changes)
                // 5. Trigger abilities and held item effects
                // 6. Decrement PP

                ExecuteMove(world, actor, action.TargetRef.Value, action.MoveSlot);

                action.Active = false;
            }
        );
    }

    private void ExecuteMove(World world, Entity attacker, Entity target, byte moveSlot)
    {
        // Placeholder for actual move execution logic
        // This would:
        // - Get move data from move ID
        // - Calculate damage using DamageCalculationSystem
        // - Apply status effects using StatusEffectSystem
        // - Handle move-specific logic
    }
}

/// <summary>
///     System calculating damage for Pokemon moves.
///     Implements Gen III damage formula from Pokemon Emerald.
/// </summary>
public class DamageCalculationSystem : SystemBase
{
    public override int Priority => SystemPriority.DamageCalculation;

    public override void Update(World world, float deltaTime)
    {
        // This system is called by MoveExecutionSystem, not on every frame
        // Could be refactored to a service if preferred
    }

    /// <summary>
    ///     Calculates damage using Gen III formula.
    /// </summary>
    public ushort CalculateDamage(
        World world,
        Entity attacker,
        Entity defender,
        ushort moveId,
        byte movePower,
        bool isPhysical
    )
    {
        // Get Pokemon components
        if (
            !world.TryGet<Pokemon>(attacker, out var atkPokemon)
            || !world.TryGet<Pokemon>(defender, out var defPokemon)
            || !world.TryGet<PokemonStats>(attacker, out var atkStats)
            || !world.TryGet<PokemonStats>(defender, out var defStats)
            || !world.TryGet<PokemonStatModifiers>(attacker, out var atkMods)
            || !world.TryGet<PokemonStatModifiers>(defender, out var defMods)
        )
        {
            return 0;
        }

        // Gen III Damage Formula:
        // Damage = ((2 * Level / 5 + 2) * Power * A/D / 50 + 2) * Modifiers

        // Get attack and defense stats
        ushort attack = isPhysical ? atkStats.Attack : atkStats.SpecialAttack;
        ushort defense = isPhysical ? defStats.Defense : defStats.SpecialDefense;

        // Apply stat modifiers
        sbyte atkStage = isPhysical ? atkMods.AttackStage : atkMods.SpecialAttackStage;
        sbyte defStage = isPhysical ? defMods.DefenseStage : defMods.SpecialDefenseStage;

        float atkMultiplier = PokemonStatModifiers.GetStageMultiplier(atkStage);
        float defMultiplier = PokemonStatModifiers.GetStageMultiplier(defStage);

        float modifiedAttack = attack * atkMultiplier;
        float modifiedDefense = defense * defMultiplier;

        // Base damage calculation
        float baseDamage = (2 * atkPokemon.Level / 5.0f + 2) * movePower;
        baseDamage = baseDamage * (modifiedAttack / modifiedDefense) / 50.0f + 2;

        // Apply modifiers (STAB, type effectiveness, random, etc.)
        float damage = ApplyDamageModifiers(world, attacker, defender, moveId, baseDamage);

        return (ushort)Math.Max(1, damage);
    }

    private float ApplyDamageModifiers(
        World world,
        Entity attacker,
        Entity defender,
        ushort moveId,
        float baseDamage
    )
    {
        float modifiers = 1.0f;

        // STAB (Same Type Attack Bonus) - 1.5x
        // Type effectiveness - 0x, 0.5x, 2x, etc.
        // Critical hit - 2x in Gen III
        // Random factor - 0.85 to 1.0
        // Weather modifiers
        // Held item modifiers
        // Ability modifiers

        return baseDamage * modifiers;
    }
}

/// <summary>
///     System managing status effects (poison, burn, paralysis, etc.).
/// </summary>
public class StatusEffectSystem : SystemBase
{
    public override int Priority => SystemPriority.StatusEffect;

    public override void Update(World world, float deltaTime)
    {
        // Query Pokemon with status conditions
        var query = new QueryDescription().WithAll<Pokemon, ActiveInBattle>();

        world.Query(
            in query,
            (Entity entity, ref Pokemon pokemon) =>
            {
                if (pokemon.StatusCondition == (byte)StatusCondition.None)
                    return;

                ApplyStatusEffect(world, entity, ref pokemon);
            }
        );
    }

    private void ApplyStatusEffect(World world, Entity entity, ref Pokemon pokemon)
    {
        switch ((StatusCondition)pokemon.StatusCondition)
        {
            case StatusCondition.Poisoned:
                // Lose 1/8 max HP per turn
                pokemon.CurrentHp = (ushort)Math.Max(0, pokemon.CurrentHp - pokemon.MaxHp / 8);
                break;

            case StatusCondition.BadlyPoisoned:
                // Lose increasing HP per turn (1/16, 2/16, 3/16...)
                var toxicDamage = pokemon.MaxHp * pokemon.StatusTurns / 16;
                pokemon.CurrentHp = (ushort)Math.Max(0, pokemon.CurrentHp - toxicDamage);
                pokemon.StatusTurns++;
                break;

            case StatusCondition.Burned:
                // Lose 1/8 max HP per turn, attack halved
                pokemon.CurrentHp = (ushort)Math.Max(0, pokemon.CurrentHp - pokemon.MaxHp / 8);
                break;

            case StatusCondition.Paralyzed:
                // 25% chance to not move, speed quartered
                break;

            case StatusCondition.Asleep:
                // Cannot move, decrements sleep counter
                if (pokemon.StatusTurns > 0)
                {
                    pokemon.StatusTurns--;
                    if (pokemon.StatusTurns == 0)
                    {
                        pokemon.StatusCondition = (byte)StatusCondition.None;
                    }
                }
                break;

            case StatusCondition.Frozen:
                // Cannot move, 20% chance to thaw each turn
                break;
        }
    }

    /// <summary>
    ///     Attempts to inflict a status condition on a Pokemon.
    /// </summary>
    public bool TryInflictStatus(World world, Entity target, StatusCondition status, byte turns = 0)
    {
        if (!world.TryGet<Pokemon>(target, out var pokemon))
            return false;

        // Cannot inflict status if already has one
        if (pokemon.StatusCondition != (byte)StatusCondition.None)
            return false;

        // Type immunities
        if (world.TryGet<TypeEffectiveness>(target, out var types))
        {
            // Fire-types can't be burned
            // Electric-types can't be paralyzed (Gen VI+, but we could implement)
            // Ice-types can't be frozen
            // Poison/Steel types can't be poisoned
        }

        pokemon.StatusCondition = (byte)status;
        pokemon.StatusTurns = turns > 0 ? turns : GetDefaultStatusDuration(status);

        world.Set(target, pokemon);
        return true;
    }

    private byte GetDefaultStatusDuration(StatusCondition status)
    {
        return status switch
        {
            StatusCondition.Asleep => (byte)Random.Shared.Next(1, 4), // 1-3 turns
            _ => 0,
        };
    }
}

/// <summary>
///     System managing AI decision-making for trainer battles.
///     Analyzes battle state and selects optimal moves.
/// </summary>
public class BattleAISystem : SystemBase
{
    public override int Priority => SystemPriority.BattleAI;

    public override void Update(World world, float deltaTime)
    {
        // Query for AI-controlled participants waiting for input
        var query = new QueryDescription().WithAll<BattleParticipant, BattleState>();

        world.Query(
            in query,
            (Entity participantEntity, ref BattleParticipant participant) =>
            {
                if (!participant.IsAI)
                    return;

                var battle = participant.BattleRef.Value;
                if (!world.TryGet<BattleState>(battle, out var battleState))
                    return;

                if (battleState.Phase != (byte)BattlePhase.InputWait)
                    return;

                // Get active Pokemon for this participant
                var activeQuery = new QueryDescription().WithAll<
                    ActiveInBattle,
                    Pokemon,
                    PokemonMoveSet
                >();

                world.Query(
                    in activeQuery,
                    (
                        Entity pokemonEntity,
                        ref ActiveInBattle active,
                        ref Pokemon pokemon,
                        ref PokemonMoveSet moves
                    ) =>
                    {
                        if (active.BattleRef.Value != battle)
                            return;

                        // AI decision making based on difficulty
                        var action = SelectBestAction(
                            world,
                            pokemonEntity,
                            participant.AIDifficulty
                        );

                        // Create action request
                        CreateActionRequest(world, pokemonEntity, action);
                    }
                );
            }
        );
    }

    private BattleActionRequest SelectBestAction(World world, Entity pokemon, byte difficulty)
    {
        // AI difficulty levels:
        // 0-2: Random moves
        // 3-5: Prefer super-effective moves
        // 6-8: Consider stat changes and status
        // 9-10: Optimal play with prediction

        // Placeholder - real AI would analyze:
        // - Type matchups
        // - Move power and accuracy
        // - Opponent's weaknesses
        // - Status conditions
        // - Stat modifiers
        // - Switch-in opportunities

        return new BattleActionRequest((byte)BattleActionType.Move, pokemon)
        {
            MoveSlot = 0, // Placeholder
        };
    }

    private void CreateActionRequest(World world, Entity pokemon, BattleActionRequest action)
    {
        // Add or update BattleActionRequest component
        if (world.Has<BattleActionRequest>(pokemon))
        {
            world.Set(pokemon, action);
        }
        else
        {
            world.Add(pokemon, action);
        }
    }
}

/// <summary>
///     System managing field effects (weather, terrain, rooms).
/// </summary>
public class FieldEffectSystem : SystemBase
{
    public override int Priority => SystemPriority.FieldEffect;

    public override void Update(World world, float deltaTime)
    {
        // Query battles with field states
        var query = new QueryDescription().WithAll<BattleState, BattleFieldState>();

        world.Query(
            in query,
            (Entity battleEntity, ref BattleFieldState field) =>
            {
                // Decrement weather duration
                if (field.Weather != (byte)WeatherCondition.None && field.WeatherTurns > 0)
                {
                    field.WeatherTurns--;
                    if (field.WeatherTurns == 0)
                    {
                        field.Weather = (byte)WeatherCondition.None;
                    }
                }

                // Decrement terrain duration
                if (field.Terrain != 0 && field.TerrainTurns > 0)
                {
                    field.TerrainTurns--;
                    if (field.TerrainTurns == 0)
                    {
                        field.Terrain = 0;
                    }
                }

                // Decrement room effects
                DecrementRoomEffect(ref field.TrickRoom, ref field.TrickRoomTurns);
                DecrementRoomEffect(ref field.MagicRoom, ref field.MagicRoomTurns);
                DecrementRoomEffect(ref field.WonderRoom, ref field.WonderRoomTurns);
                DecrementRoomEffect(ref field.Gravity, ref field.GravityTurns);
            }
        );
    }

    private void DecrementRoomEffect(ref bool active, ref byte turns)
    {
        if (active && turns > 0)
        {
            turns--;
            if (turns == 0)
            {
                active = false;
            }
        }
    }
}

/// <summary>
///     System priority constants for battle systems.
/// </summary>
public static class SystemPriority
{
    public const int BattleInitialization = 1000;
    public const int BattleAI = 2000;
    public const int BattleTurn = 3000;
    public const int MoveExecution = 4000;
    public const int DamageCalculation = 4100;
    public const int StatusEffect = 5000;
    public const int FieldEffect = 6000;
}
