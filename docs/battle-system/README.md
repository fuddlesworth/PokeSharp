# Pokemon Battle System - ECS Design

## Overview

This document outlines the Entity Component System (ECS) architecture for the Pokemon battle system in PokeSharp, designed to replicate Pokemon Emerald's turn-based battle mechanics using Arch.Core ECS.

## Architecture Principles

### ECS Pattern
- **Components**: Pure data structs with no logic
- **Systems**: Logic processors that operate on entities with specific component combinations
- **Events**: Record-based event types for battle state changes
- **Component Pooling**: Request components stay on entities and are marked inactive to avoid expensive archetype transitions

### Design Goals
1. **Performance**: Utilize Arch.Core's high-performance archetype-based queries
2. **Modularity**: Separate concerns (damage calculation, status effects, AI, etc.)
3. **Maintainability**: Clean separation of data and logic
4. **Extensibility**: Easy to add new moves, abilities, and mechanics
5. **Gen III Accuracy**: Faithful implementation of Pokemon Emerald battle formulas

## Component Architecture

### Pokemon Components

#### `Pokemon` (Core Data)
- Species, level, experience, HP
- Status conditions, friendship
- Gender, nature, ability, held item
- Personality value, shininess

#### `PokemonIVs` (Individual Values)
- HP, Attack, Defense, Special Attack, Special Defense, Speed
- Range: 0-31 per stat (genetic/hidden stats)

#### `PokemonEVs` (Effort Values)
- HP, Attack, Defense, Special Attack, Special Defense, Speed
- Range: 0-255 per stat, max 510 total
- Gained from defeating Pokemon

#### `PokemonStats` (Calculated Battle Stats)
- Attack, Defense, Special Attack, Special Defense, Speed
- Computed from: base stats + IVs + EVs + level + nature

#### `PokemonStatModifiers` (Temporary Battle Modifiers)
- Stage modifiers for all stats (-6 to +6)
- Accuracy and evasion stages
- Reset when Pokemon switches out
- `GetStageMultiplier()` method for stage-to-multiplier conversion

#### `PokemonMoveSet` (Moveset Data)
- Up to 4 moves (fixed-size for performance)
- Move IDs, current PP, max PP
- Methods: `GetMoveId()`, `GetCurrentPP()`, `HasPP()`, `DecrementPP()`

#### `VolatileStatus` (Temporary Battle Conditions)
- Confusion, flinch, infatuation, leech seed, curse
- Trapped/bound status, substitute HP
- Locked move (Encore, Choice items)
- Reset when Pokemon switches out

#### `TypeEffectiveness` (Type Information)
- Primary and secondary types
- Type modification tracking (Soak, Forest's Curse, etc.)

#### `InParty` (Party Membership)
- EntityRef to trainer/player
- Party slot position (0-5)

#### `ActiveInBattle` (Battle Participation)
- EntityRef to battle entity
- Battle slot (0 = player, 1 = opponent in single battles)

### Battle Components

#### `BattleState` (Battle Instance)
- Battle ID, type (wild/trainer), format (single/double)
- Turn number, phase (see `BattlePhase` enum)
- Completion status, winner
- Escape mechanics

#### `BattleParticipant` (Trainer in Battle)
- EntityRefs to battle and trainer entities
- Side (0 = player, 1 = opponent)
- AI settings (enabled, difficulty level)
- Prize money

#### `BattleSideState` (Side-Wide Effects)
- Screens: Reflect, Light Screen, Mist, Safeguard, Tailwind
- Entry hazards: Spikes (0-3 layers), Toxic Spikes (0-2), Stealth Rock, Sticky Web
- Wish mechanics

#### `BattleFieldState` (Battle-Wide Effects)
- Weather: None, Sun, Rain, Sandstorm, Hail, Primal weather
- Terrain: None, Electric, Grassy, Misty, Psychic
- Rooms: Trick Room, Magic Room, Wonder Room
- Gravity

#### `BattleActionRequest` (Pending Actions)
- Action type: Move, Switch, Item, Run, Pass
- EntityRefs to actor and target
- Move slot, item ID, switch slot
- Priority (calculated from move priority + speed)
- **Uses component pooling**: `Active` flag instead of component removal

#### `TurnExecutionState` (Turn Processing)
- Current action index, total actions
- Sorting status, completion flag

## System Architecture

### System Priorities (Execution Order)
```
1000: BattleInitializationSystem
2000: BattleAISystem
3000: BattleTurnSystem
4000: MoveExecutionSystem
4100: DamageCalculationSystem
5000: StatusEffectSystem
6000: FieldEffectSystem
```

### Core Systems

#### `BattleInitializationSystem` (Priority: 1000)
- Handles battle creation and setup
- Initializes participants, field state, side states
- Transitions from Initialize → SendOut phase

#### `BattleTurnSystem` (Priority: 3000)
- Orchestrates battle phase progression
- Phase transitions:
  - **TurnStart**: Process abilities, weather, terrain effects → InputWait
  - **InputWait**: Wait for player/AI input (UI-driven)
  - **ActionSelect**: Collect and sort actions by priority → TurnExecute
  - **TurnExecute**: Execute actions in order → TurnEnd
  - **TurnEnd**: Status damage, item effects, check battle end → TurnStart

#### `BattleAISystem` (Priority: 2000)
- Makes decisions for AI-controlled trainers
- Difficulty levels:
  - 0-2: Random moves
  - 3-5: Prefer super-effective moves
  - 6-8: Consider stat changes and status
  - 9-10: Optimal play with prediction
- Creates `BattleActionRequest` components

#### `MoveExecutionSystem` (Priority: 4000)
- Executes move actions
- Workflow:
  1. Validate actor and target
  2. Check PP availability
  3. Check if Pokemon can move (status checks)
  4. Calculate accuracy
  5. Apply damage (via `DamageCalculationSystem`)
  6. Apply secondary effects (status, stat changes)
  7. Trigger abilities and held items
  8. Decrement PP

#### `DamageCalculationSystem` (Priority: 4100)
- Implements Gen III damage formula
- **Formula**: `((2 * Level / 5 + 2) * Power * A/D / 50 + 2) * Modifiers`
- **Modifiers**:
  - STAB (Same Type Attack Bonus): 1.5x
  - Type effectiveness: 0x, 0.5x, 1x, 2x, 4x
  - Critical hit: 2x (Gen III)
  - Random: 0.85-1.0
  - Weather, abilities, held items

#### `StatusEffectSystem` (Priority: 5000)
- Manages status conditions
- **Poison**: 1/8 max HP per turn
- **Badly Poisoned**: Increasing damage (1/16, 2/16, 3/16...)
- **Burn**: 1/8 max HP per turn, attack halved
- **Paralysis**: 25% immobilization, speed quartered
- **Sleep**: Cannot move, 1-3 turns
- **Freeze**: Cannot move, 20% thaw chance
- Type immunities enforced

#### `FieldEffectSystem` (Priority: 6000)
- Manages weather, terrain, and room effects
- Decrements turn counters
- Removes expired effects

## Event System

All battle events implement `IBattleEvent` with a `DateTime Timestamp`.

### Event Categories

#### Battle Lifecycle
- `BattleStartedEvent`: Battle initialization
- `BattleEndedEvent`: Battle completion with rewards
- `TurnStartedEvent`: New turn begins

#### Combat Events
- `MoveUsedEvent`: Pokemon uses a move
- `DamageDealtEvent`: Damage dealt with effectiveness and crit flag
- `MoveMissedEvent`: Move fails to hit

#### Status Events
- `StatusInflictedEvent`: Status condition applied
- `StatusCuredEvent`: Status condition removed
- `StatChangedEvent`: Stat stage modified

#### Pokemon Events
- `PokemonSwitchedInEvent`: Pokemon enters battle
- `PokemonSwitchedOutEvent`: Pokemon leaves battle
- `PokemonFaintedEvent`: Pokemon reaches 0 HP
- `LevelUpEvent`: Pokemon gains a level
- `ExperienceGainedEvent`: EXP awarded
- `MoveLearnedEvent`: New move learned

#### Field Events
- `WeatherChangedEvent`: Weather transition
- `TerrainChangedEvent`: Terrain transition
- `FieldEffectActivatedEvent`: Room/Gravity activated
- `FieldEffectEndedEvent`: Effect expires
- `HazardSetEvent`: Entry hazard placed
- `HazardClearedEvent`: Entry hazard removed

#### Miscellaneous
- `AbilityActivatedEvent`: Ability triggers
- `ItemUsedEvent`: Item consumed
- `FleeAttemptedEvent`: Escape attempt
- `BattleMessageEvent`: UI messages

## Battle Flow State Machine

```
Initialize
    ↓
SendOut (send out initial Pokemon)
    ↓
TurnStart (abilities, weather, field effects)
    ↓
InputWait (wait for player input)
    ↓
ActionSelect (AI makes decisions, collect all actions)
    ↓
TurnExecute (execute actions by priority)
    ↓
TurnEnd (status damage, item effects, check win condition)
    ↓
TurnStart (loop) OR BattleEnd
```

## Integration with Existing PokeSharp Systems

### Component Patterns
- Follows existing conventions: pure data structs, no methods except utilities
- Uses `EntityRef` for safe entity references with validation
- Consistent naming and documentation style

### System Patterns
- Extends `SystemBase` for DI support and initialization handling
- Uses `Priority` constants for execution order
- Implements `ISystem` interface (Priority, Enabled, Initialize, Update)

### Performance Optimizations
- **Component Pooling**: `BattleActionRequest` uses `Active` flag instead of add/remove to avoid archetype transitions (same pattern as `MovementRequest`)
- **Fixed-Size Arrays**: `PokemonMoveSet` uses 4 separate fields instead of arrays for cache efficiency
- **Byte/UShort Usage**: Minimize memory footprint where possible
- **Query Caching**: Systems use `QueryDescription` for optimal archetype queries

### Event Bus Integration
- All events are records (immutable)
- Compatible with `IEventBus` from `PokeSharp.Engine.Core.Events`
- UI systems can subscribe to battle events for real-time updates

## File Organization

```
PokeSharp.Game.Components/
  Components/
    Battle/
      Pokemon.cs              (Pokemon core components)
      PokemonStats.cs         (IV/EV/Stats components)
      PokemonMoves.cs         (MoveSet component)
      BattleState.cs          (Battle instance components)
      BattleParticipant.cs    (Participant components)
      BattleActions.cs        (Action request components)
      FieldEffects.cs         (Weather/Terrain components)
      StatusEffects.cs        (Volatile status components)

PokeSharp.Game.Systems/
  Battle/
    BattleInitializationSystem.cs
    BattleTurnSystem.cs
    MoveExecutionSystem.cs
    DamageCalculationSystem.cs
    StatusEffectSystem.cs
    FieldEffectSystem.cs
    BattleAISystem.cs

PokeSharp.Game.Events/
  Battle/
    BattleEvents.cs         (All battle event types)
```

## Testing Considerations

### Unit Tests
- Damage calculation verification (Gen III formulas)
- Stat stage multipliers
- Status effect application/removal
- Type effectiveness calculations
- Action priority sorting

### Integration Tests
- Full battle simulation (wild encounter)
- Trainer battle with AI
- Double battle mechanics
- Weather/terrain interactions
- Entry hazard damage

### Performance Tests
- 1000 entities with Pokemon components
- Battle turn execution time
- Action sorting performance
- Component pooling vs. add/remove benchmarks

## Future Extensions

### Potential Enhancements
1. **Double/Triple Battle Support**: Expand `BattleFormat` enum, multi-target moves
2. **Mega Evolution**: Add `MegaEvolved` component and transformation system
3. **Z-Moves**: Add Z-Move components and execution logic
4. **Online Battles**: Add network synchronization components
5. **Battle Replay**: Record events for replay functionality
6. **Advanced AI**: Neural network integration for smarter opponents

### Data-Driven Design
- Move data: JSON/CSV for move definitions (power, accuracy, effects)
- Species data: Base stats, learn sets, evolution chains
- Ability data: Effect definitions and triggers
- Item data: Battle item effects

## References

- **Arch.Core Documentation**: [GitHub](https://github.com/genaray/Arch)
- **Pokemon Emerald Mechanics**: [Bulbapedia](https://bulbapedia.bulbagarden.net/)
- **Gen III Damage Formula**: [Smogon University](https://www.smogon.com/dp/articles/damage_formula)
- **PokeSharp Component Examples**: `PokeSharp.Game.Components/Components/`

---

**Note**: This design is currently in documentation form. Implementation files should be created in the appropriate directories following PokeSharp's project structure.
