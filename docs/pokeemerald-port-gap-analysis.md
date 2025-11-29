# pokeemerald Port to PokeSharp - Gap Analysis

**Analysis Date:** 2025-11-29
**Analyst:** Research Agent
**Objective:** Identify what systems are needed to port pokeemerald (GBA Pokemon Emerald decompilation) to PokeSharp

---

## Executive Summary

PokeSharp currently has a **solid foundation** for world exploration with:
- ✅ ECS architecture (Arch.Core)
- ✅ Map streaming and connections
- ✅ Tiled map loading (pokeemerald map converter - porycon)
- ✅ Player movement and collision
- ✅ NPC system with basic behaviors
- ✅ Sprite rendering and animation
- ✅ C# scripting runtime

**Critical Gaps:**
- ❌ No battle system
- ❌ No Pokemon data structures
- ❌ No inventory/bag system
- ❌ No progression/save system
- ❌ No UI menus (party, bag, Pokedex, etc.)

**Estimated Scope:** ~12-18 months for full pokeemerald feature parity (assuming 1-2 developers)

---

## 1. Battle System (CRITICAL GAP - Priority 1)

### 1.1 What pokeemerald Has
From `/pokeemerald/include/battle.h` and source analysis:

**Core Battle Engine:**
- Turn-based state machine
- Action selection (MOVE/ITEM/SWITCH/RUN)
- Move execution pipeline
- Damage calculation formulas
- Status effect system (paralysis, sleep, burn, freeze, poison)
- Ability activation system
- Weather effects (rain, sun, hail, sandstorm)
- Entry hazards (stealth rock, spikes, toxic spikes)
- Multi-battle support (double battles, tag battles)

**Battle AI:**
- AI script commands for trainers
- Move scoring algorithms
- Switch-in logic
- Item usage decisions

**Battle Animations:**
- Move animations (20+ animation files)
- Pokemon sprite animations
- Background effects
- HP bar animations

### 1.2 What PokeSharp Has
**Current State:**
- ❌ No battle system at all
- ✅ Has animation framework (SpriteAnimationSystem)
- ✅ Has scripting runtime (could be used for battle scripts)
- ✅ Has ECS event bus (good for battle event propagation)

### 1.3 Gap Analysis

| Component | pokeemerald | PokeSharp | Gap Level |
|-----------|-------------|-----------|-----------|
| Battle state machine | ✅ Full | ❌ None | **CRITICAL** |
| Damage calculation | ✅ Full | ❌ None | **CRITICAL** |
| Move execution | ✅ Full | ❌ None | **CRITICAL** |
| Status effects | ✅ Full | ❌ None | **HIGH** |
| Battle AI | ✅ Full | ❌ None | **HIGH** |
| Abilities | ✅ Full | ❌ None | **HIGH** |
| Weather system | ✅ Full | ❌ None | **MEDIUM** |
| Double battles | ✅ Full | ❌ None | **MEDIUM** |
| Battle animations | ✅ 500+ | ❌ None | **LOW** (can start simple) |

### 1.4 Implementation Recommendations

**Phase 1 - Core Battle (3-4 months):**
1. Create `BattleSystem` ECS system
2. Implement `BattlePokemon` component (active battler stats)
3. Create state machine: `BATTLE_START → ACTION_SELECT → MOVE_EXECUTE → TURN_END → BATTLE_END`
4. Implement basic damage calculation (Type effectiveness, STAB, random rolls)
5. Add turn order calculation (Speed stat, priority moves)

**Phase 2 - Status & Effects (2-3 months):**
1. Implement status effects (paralysis, sleep, burn, freeze, poison)
2. Add stat stages (+6 to -6 modifiers)
3. Create ability framework
4. Add weather system
5. Implement held items

**Phase 3 - Advanced Battles (2-3 months):**
1. Double battle support
2. AI system with move scoring
3. Complex move effects (multi-turn, recharge, etc.)
4. Entry hazards and terrain effects

**ECS Design Suggestion:**
```csharp
// Components
public struct BattlePokemon {
    public PokemonId PokemonId;
    public int CurrentHP;
    public int Attack, Defense, SpAttack, SpDefense, Speed;
    public StatusCondition Status;
    public int[] StatStages; // -6 to +6
    public MoveId[] Moves;
    public int[] MovePP;
}

public struct BattleState {
    public BattlePhase Phase; // ActionSelect, MoveExecution, TurnEnd, etc.
    public int TurnNumber;
    public Weather Weather;
    public Entity[] ActiveBattlers; // [player1, player2, enemy1, enemy2]
}

public struct BattleAction {
    public ActionType Type; // Move, Switch, Item, Run
    public int TargetSlot;
    public MoveId MoveId;
}

// Systems
public class BattleSystem : IUpdateSystem
public class DamageCalculationSystem : IUpdateSystem
public class StatusEffectSystem : IUpdateSystem
public class BattleAISystem : IUpdateSystem
```

---

## 2. Pokemon Data System (CRITICAL GAP - Priority 1)

### 2.1 What pokeemerald Has
From `/pokeemerald/include/pokemon.h`:

**Pokemon Species Data:**
- 386 species (Gen 1-3)
- Base stats (HP, Attack, Defense, SpAtk, SpDef, Speed)
- Types (primary/secondary)
- Abilities (primary/secondary/hidden)
- Egg groups
- Gender ratio
- Evolution methods
- Learnsets (level-up, TM/HM, egg moves, tutor)

**Individual Pokemon:**
- IVs (0-31 per stat)
- EVs (0-255 per stat, max 510 total)
- Nature (25 natures affecting stats)
- Personality value (determines shininess, gender, ability slot)
- Friendship value
- Held item
- Move set (4 moves)
- Status conditions
- Pokerus

### 2.2 What PokeSharp Has
**Current State:**
- ❌ No Pokemon data structures
- ✅ Has `TrainerDefinition` with placeholder `PartyJson` field
- ✅ Has ECS component system (can easily add Pokemon components)

### 2.3 Gap Analysis

| Component | pokeemerald | PokeSharp | Gap Level |
|-----------|-------------|-----------|-----------|
| Species database | ✅ 386 species | ❌ None | **CRITICAL** |
| Stats system | ✅ Full (IVs/EVs/Nature) | ❌ None | **CRITICAL** |
| Move database | ✅ 354 moves | ❌ None | **CRITICAL** |
| Ability database | ✅ 77 abilities | ❌ None | **HIGH** |
| Evolution system | ✅ Full | ❌ None | **HIGH** |
| Breeding/eggs | ✅ Full | ❌ None | **MEDIUM** |
| Pokemon storage | ✅ PC boxes | ❌ None | **MEDIUM** |

### 2.4 Implementation Recommendations

**Phase 1 - Data Structures (2-3 months):**
1. Create `SpeciesData` database (load from pokeemerald JSON exports)
2. Implement `Pokemon` component with full stat calculation
3. Create `MoveData` database
4. Build stat calculation formulas (pokeemerald formulas are well-documented)
5. Add nature and ability systems

**Phase 2 - Pokemon Management (1-2 months):**
1. Party management (6 Pokemon max)
2. Pokemon storage system (PC boxes)
3. Pokemon ownership tracking
4. Nickname and metadata

**ECS Design Suggestion:**
```csharp
// Components
public struct Pokemon {
    public SpeciesId Species;
    public int Level;
    public int Experience;

    // Individual values
    public Stats IVs;  // 0-31
    public Stats EVs;  // 0-255
    public Nature Nature;

    // Combat stats (calculated)
    public Stats Stats;

    // State
    public int CurrentHP;
    public StatusCondition Status;
    public int Friendship;

    // Moves
    public MoveSlot[] Moves; // Max 4

    // Metadata
    public string Nickname;
    public bool IsShiny;
    public Gender Gender;
    public AbilityId Ability;
    public ItemId HeldItem;
}

public struct Stats {
    public int HP, Attack, Defense, SpAttack, SpDefense, Speed;
}

// Static data (loaded from JSON)
public class SpeciesDatabase {
    public Dictionary<SpeciesId, SpeciesData> Species;
}

public struct SpeciesData {
    public string Name;
    public Stats BaseStats;
    public TypeId[] Types;
    public AbilityId[] Abilities;
    public Learnset Learnset;
    public Evolution[] Evolutions;
}
```

**Data Extraction:**
The pokeemerald repo has JSON exports of all Pokemon data. We can write a converter to extract:
- `/pokeemerald/src/data/pokemon/species_info/*.h` → PokeSharp JSON
- `/pokeemerald/src/data/pokemon/level_up_learnsets.h` → Learnsets
- `/pokeemerald/src/data/moves/*.c` → Move database

---

## 3. Inventory/Item System (HIGH Priority - Priority 2)

### 3.1 What pokeemerald Has
From `/pokeemerald/include/item.h`:

**Item Categories:**
- Pokeballs (13 types)
- Medicine (potions, status healers, revives)
- Battle items (X Attack, Guard Spec, etc.)
- Berries (43 berries with effects)
- TMs/HMs (50 TMs, 8 HMs)
- Key items (Bike, Pokedex, HM items, etc.)
- Hold items (Leftovers, Choice Band, etc.)

**Systems:**
- Bag UI (categorized pockets)
- Item usage (in battle, out of battle, on Pokemon)
- Item acquisition (shops, find, gift)
- TM/HM compatibility checking

### 3.2 What PokeSharp Has
**Current State:**
- ❌ No inventory system
- ✅ Has `Wallet` component (money tracking exists!)
- ⚠️ `TrainerDefinition` has placeholder `Items` field (string)

### 3.3 Gap Analysis

| Component | pokeemerald | PokeSharp | Gap Level |
|-----------|-------------|-----------|-----------|
| Item database | ✅ 377 items | ❌ None | **HIGH** |
| Bag/inventory | ✅ Full UI | ❌ None | **HIGH** |
| Item effects | ✅ Full | ❌ None | **HIGH** |
| Shops | ✅ Full | ❌ None | **MEDIUM** |
| TM/HM system | ✅ Full | ❌ None | **MEDIUM** |

### 3.4 Implementation Recommendations

**Phase 1 - Core Inventory (1-2 months):**
1. Create `ItemDatabase` with item data from pokeemerald
2. Implement `Inventory` component with categorized storage
3. Add item usage system (consumables, key items)
4. Create simple bag UI

**Phase 2 - Advanced Items (1-2 months):**
1. TM/HM system with compatibility checking
2. Held item effects in battle
3. Berry effects and growing system (optional)
4. Shop system

**ECS Design:**
```csharp
public struct Inventory {
    public Dictionary<ItemId, int> Items; // ItemId → quantity
    public int Money;
}

public struct ItemData {
    public string Name;
    public ItemCategory Category;
    public int Price;
    public bool Consumable;
    public bool Holdable;
    // Effect is handled by ItemEffectSystem based on item type
}
```

---

## 4. Game Progression System (HIGH Priority - Priority 2)

### 4.1 What pokeemerald Has

**Progression Tracking:**
- 8 Gym badges
- Elite Four completion
- Pokedex (seen/caught flags)
- Story flags (700+ event flags)
- Trainer battle tracking (rematch system)
- Time-based events (Berry growth, lottery, etc.)

**Save System:**
- Save blocks (player data, Pokemon boxes, game state)
- Corruption detection
- Hall of Fame records

### 4.2 What PokeSharp Has
**Current State:**
- ❌ No save system
- ❌ No progression tracking
- ✅ Has scripting system (can implement flag system in scripts)
- ✅ Has ECS events (good for progression triggers)

### 4.3 Gap Analysis

| Component | pokeemerald | PokeSharp | Gap Level |
|-----------|-------------|-----------|-----------|
| Save/load | ✅ Full | ❌ None | **HIGH** |
| Event flags | ✅ 700+ | ❌ None | **HIGH** |
| Badge tracking | ✅ Full | ❌ None | **MEDIUM** |
| Pokedex | ✅ Full | ❌ None | **MEDIUM** |
| Time system | ✅ RTC | ❌ None | **MEDIUM** |

### 4.4 Implementation Recommendations

**Phase 1 - Save System (1 month):**
1. Implement save file serialization (JSON or binary)
2. Save world state (player position, inventory, party)
3. Save flags and progression data
4. Auto-save and manual save

**Phase 2 - Progression (1-2 months):**
1. Event flag system
2. Badge tracking
3. Pokedex (seen/caught tracking)
4. Trainer rematch system

**ECS Design:**
```csharp
public struct GameProgress {
    public HashSet<string> EventFlags; // "DEFEATED_ROXANNE", "HAS_POKEDEX", etc.
    public byte Badges; // Bitfield for 8 badges
    public int PokedexSeen; // Bitfield or array
    public int PokedexCaught;
}

public struct SaveData {
    public PlayerData Player;
    public PokemonData[] Party;
    public PokemonData[] PC;
    public InventoryData Inventory;
    public GameProgressData Progress;
    public MapData CurrentMap;
}
```

---

## 5. UI Systems (MEDIUM Priority - Priority 3)

### 5.1 What pokeemerald Has

**Game Menus:**
- Party screen (view team, reorder, use items)
- Bag menu (items categorized)
- Pokedex (search, sort, view entries)
- Trainer card (ID, badges, time played)
- Options menu
- Battle UI (action selection, move selection, switch menu)

**Text System:**
- Text boxes with typewriter effect
- Multiple language support
- Text scrolling and pagination

### 5.2 What PokeSharp Has
**Current State:**
- ⚠️ Has `LoadingScene` and `GameplayScene` (basic scene management)
- ✅ Has Gum UI framework integration (mentioned in code)
- ❌ No Pokemon-specific UI

### 5.3 Gap Analysis

| Component | pokeemerald | PokeSharp | Gap Level |
|-----------|-------------|-----------|-----------|
| Party menu | ✅ Full | ❌ None | **HIGH** |
| Bag menu | ✅ Full | ❌ None | **HIGH** |
| Battle UI | ✅ Full | ❌ None | **HIGH** |
| Pokedex | ✅ Full | ❌ None | **MEDIUM** |
| Start menu | ✅ Full | ❌ None | **MEDIUM** |
| Text boxes | ✅ Full | ⚠️ Basic | **MEDIUM** |

### 5.4 Implementation Recommendations

**Phase 1 - Core Menus (2-3 months):**
1. Create party menu UI (view Pokemon stats, moves, HP)
2. Build bag menu with categories
3. Implement start menu (Pokedex, Pokemon, Bag, Save, Options, Exit)
4. Add text box system with typewriter effect

**Phase 2 - Battle UI (2 months):**
1. Battle action menu (Fight, Pokemon, Bag, Run)
2. Move selection UI
3. Pokemon switch UI during battle
4. HP bars and status indicators

**Phase 3 - Advanced Menus (1-2 months):**
1. Pokedex UI with search/filter
2. Summary screen for Pokemon
3. PC storage UI
4. Options menu

**Technology:**
PokeSharp uses **Gum UI framework**. This should work well for Pokemon menus.

---

## 6. World Systems (ALREADY MOSTLY COMPLETE ✅)

### 6.1 What PokeSharp Already Has

✅ **Map Loading & Streaming:**
- `MapStreamingSystem` - Loads adjacent maps seamlessly
- `MapLoader` - Loads Tiled maps from pokeemerald (via porycon)
- Map connections (North/South/East/West)
- Multi-map rendering

✅ **Movement & Collision:**
- Grid-based movement (pokeemerald-style)
- `CollisionSystem` with tile behavior checking
- Elevation system
- Directional collision (impassable_north, etc.)

✅ **NPCs:**
- `NpcDefinition` database
- NPC behaviors (wander, patrol, guard, chase)
- NPC interactions
- Sprite animations

✅ **Map Tools:**
- **porycon** - Converts pokeemerald maps to Tiled format
- Metatile rendering
- Animation support
- Tileset generation

### 6.2 Minor Gaps

| Feature | pokeemerald | PokeSharp | Gap Level |
|---------|-------------|-----------|-----------|
| Warps | ✅ Full | ⚠️ Partial (can add via scripts) | **LOW** |
| Wild encounters | ✅ Full | ❌ Trigger zones exist, no encounters | **MEDIUM** |
| Scripts | ✅ Full | ✅ Has C# runtime | **LOW** |
| Map scripts | ✅ OnLoad, OnWarp | ⚠️ Can implement | **LOW** |

### 6.3 Recommendations

**Phase 1 - Wild Encounters (1 month):**
1. Implement `WildEncounterSystem` using existing `EncounterZone` component
2. Load encounter tables from pokeemerald data
3. Trigger battles when walking in grass/water/caves
4. Encounter rate calculation (steps counter)

**Phase 2 - Warps (1 week):**
1. Add `WarpTile` component
2. Create `WarpSystem` to handle map transitions
3. Support warp types (normal, spin, fall through hole, etc.)

---

## 7. Priority Implementation Roadmap

### Phase 1: Battle Foundation (4-6 months)
**Goal:** Implement basic battles between trainer and wild Pokemon

1. **Month 1-2:** Pokemon data structures
   - Species database (load from pokeemerald)
   - Pokemon component with stats
   - Move database
   - Basic stat calculation

2. **Month 3-4:** Core battle system
   - Battle state machine
   - Turn-based combat
   - Damage calculation
   - Type effectiveness
   - Basic move execution

3. **Month 5-6:** Battle refinement
   - Status effects
   - Stat stages
   - Basic abilities
   - AI system (simple move selection)

**Deliverable:** Can battle wild Pokemon and trainers with basic moves

---

### Phase 2: Game Loop (3-4 months)
**Goal:** Implement progression and inventory

1. **Month 7-8:** Inventory system
   - Item database
   - Bag UI
   - Item usage (potions, Pokeballs)
   - Shops

2. **Month 9-10:** Progression & UI
   - Save/load system
   - Party menu UI
   - Event flags
   - Badge tracking
   - Pokedex tracking

**Deliverable:** Can catch Pokemon, use items, save progress

---

### Phase 3: Advanced Features (2-4 months)
**Goal:** Polish and advanced systems

1. **Month 11-12:** Advanced battle
   - Weather
   - Entry hazards
   - Complex abilities
   - Double battles

2. **Month 13-14 (Optional):** Polish
   - Battle animations
   - Advanced UI (Pokedex viewer, PC)
   - Breeding system
   - Post-game features

**Deliverable:** Feature parity with pokeemerald

---

## 8. Complexity Estimates

### Critical Path (Must Have)
| System | Complexity | Time Estimate |
|--------|-----------|---------------|
| Pokemon data structures | Medium | 2-3 months |
| Core battle system | High | 3-4 months |
| Status effects | Medium | 1-2 months |
| Inventory system | Medium | 1-2 months |
| Save/load | Low | 1 month |
| Party UI | Medium | 1-2 months |
| Wild encounters | Low | 1 month |
| **TOTAL** | | **10-15 months** |

### Nice to Have
| System | Complexity | Time Estimate |
|--------|-----------|---------------|
| Battle animations | High | 2-3 months |
| Advanced abilities | High | 2-3 months |
| Breeding system | Medium | 1-2 months |
| Pokedex UI | Medium | 1 month |
| Double battles | Medium | 1-2 months |
| Post-game features | Variable | 2-6 months |

---

## 9. Technical Recommendations

### Leverage Existing Systems
1. **Use ECS events for battle**: Already has `IEcsEventBus` - perfect for battle events
2. **Use scripting for abilities**: C# scripting runtime can handle complex ability logic
3. **Reuse animation system**: `SpriteAnimationSystem` can animate battle sprites
4. **Leverage porycon**: Already converts pokeemerald maps perfectly

### Data Migration Strategy
1. **Extract pokeemerald data to JSON:**
   ```bash
   # Pokemon species
   pokeemerald/src/data/pokemon/species_info/*.h → species.json

   # Moves
   pokeemerald/src/data/moves/*.c → moves.json

   # Items
   pokeemerald/src/data/items.h → items.json

   # Trainers
   pokeemerald/src/data/trainer_parties.h → trainers.json
   ```

2. **Load at runtime using existing `GameDataLoader`**

### Architecture Patterns
1. **Battle System:** State pattern for battle phases
2. **Pokemon Stats:** Strategy pattern for stat calculation
3. **Move Effects:** Command pattern for move execution
4. **Abilities:** Observer pattern triggered by battle events

### Testing Strategy
1. **Unit tests for battle math** (damage calc, stat calc)
2. **Integration tests for battle flow**
3. **Data validation tests** (ensure all 386 Pokemon load correctly)

---

## 10. Conclusion

### Current Strengths ✅
PokeSharp has an **excellent foundation** for world exploration:
- Solid ECS architecture with Arch.Core
- Pokemon-style map streaming (already works!)
- Complete map conversion pipeline (porycon)
- Flexible scripting system
- Good performance infrastructure

### Critical Gaps ❌
The **battle system** is the biggest missing piece:
- No battle state machine
- No Pokemon data structures
- No damage calculation
- No status effects

### Realistic Timeline
**Minimum Viable Product (MVP):** 10-12 months
- Can walk around, battle, catch Pokemon, save progress

**Full Feature Parity:** 15-18 months
- All pokeemerald features, polish, testing

### Recommended Approach
1. **Start with Pokemon data structures** (foundation for everything)
2. **Build battle system incrementally** (start simple, add complexity)
3. **Reuse existing systems** (maps, movement, NPCs work great)
4. **Extract pokeemerald data** (don't rewrite, convert)

---

## Next Steps

1. **Decide on battle system architecture** (ECS components + systems)
2. **Create Pokemon data extraction tool** (pokeemerald → JSON)
3. **Implement basic Pokemon component** (stats, moves)
4. **Build prototype battle system** (1v1 trainer battle)
5. **Iterate and expand**

---

**Total Estimated Effort:** 12-18 months (1-2 developers full-time)

**Difficulty Level:** High (battle system is complex, but pokeemerald provides reference)

**Feasibility:** ✅ Very feasible - PokeSharp's architecture is well-suited for Pokemon gameplay
