# PokeSharp Development Roadmap

## ✅ Phase 0: Debug Console (COMPLETE)
**Status**: 100% Complete

**Completed Features**:
- ✅ Quake-style debug console
- ✅ Roslyn C# scripting integration
- ✅ Mouse wheel + keyboard scrolling
- ✅ Color-coded output
- ✅ Input blocking (camera/zoom)
- ✅ Command history
- ✅ Visual scroll indicator
- ✅ Comprehensive API access
- ✅ Full documentation

**Next**: Choose your path below

---

## 🎯 Phase 1: Battle System (RECOMMENDED)
**Estimated Time**: 3-4 weeks
**Priority**: HIGH - Core gameplay mechanic

### Why First?
- **Core Gameplay**: Pokemon games ARE battles
- **Motivating**: Seeing battles work = huge milestone
- **Foundation**: Tests your ECS architecture
- **Blockbuster**: Creates excitement and momentum

### Components Needed
```csharp
// Battle State
public struct BattleState
{
    public Entity Opponent;
    public bool IsPlayerTurn;
    public BattlePhase Phase; // Intro, PlayerTurn, OpponentTurn, Animation, End
    public int Turn;
}

// Pokemon Stats
public struct Pokemon
{
    public string Species;
    public int Level;
    public PokemonStats Stats; // HP, Attack, Defense, Speed, etc.
    public List<Move> Moves;
    public StatusCondition Status; // Normal, Poisoned, Paralyzed, etc.
}

// Move Data
public struct Move
{
    public string Name;
    public MoveType Type; // Fire, Water, Grass, etc.
    public int Power;
    public int Accuracy;
    public int PP; // Power Points
    public MoveCategory Category; // Physical, Special, Status
}

// Battle Stats
public struct BattleStats
{
    public int CurrentHP;
    public int MaxHP;
    public int CurrentPP;
    public Dictionary<StatType, int> StatModifiers; // +/-6 stages
}
```

### Systems Needed
1. **BattleInitiationSystem** - Start battles (wild/trainer)
2. **BattleFlowSystem** - Turn management, phase transitions
3. **MoveExecutionSystem** - Apply moves, calculate damage
4. **TypeEffectivenessSystem** - Fire beats Grass, etc.
5. **StatusEffectSystem** - Poison, paralysis, burn, etc.
6. **BattleAnimationSystem** - Move animations, HP bars
7. **BattleUISystem** - Menu, text box, info display

### Implementation Steps
1. **Week 1**: Core battle structure
   - BattleState component
   - BattleFlowSystem for turn management
   - Simple 1v1 wild battles
   - Basic menu (Fight, Bag, Run)

2. **Week 2**: Move system
   - Move data and execution
   - Damage calculation formula
   - Type effectiveness chart
   - PP tracking

3. **Week 3**: Battle polish
   - Status conditions
   - Stat modifiers (Attack Up, Speed Down, etc.)
   - Critical hits
   - Experience and leveling

4. **Week 4**: Battle UI
   - HP bars with smooth animation
   - Move selection menu
   - Battle text box
   - Pokemon sprites (player + opponent)

### Test with Console
```csharp
// Start a test battle
Api.StartBattle("pidgey", level: 5);

// Check battle state
var battle = GetEntitiesWith<BattleState>().First();
Inspect(battle);

// Force player victory
Api.EndBattle(won: true);
```

---

## 🎮 Phase 2: Pokemon/Party System
**Estimated Time**: 2-3 weeks
**Priority**: HIGH - Required for battles

### Why Second?
- **Dependency**: Battles need Pokemon
- **Core Feature**: Managing your team
- **Foundation**: For all Pokemon interactions

### Components Needed
```csharp
public struct PartyMember
{
    public int PartySlot; // 0-5
    public bool IsLeader; // First in party
}

public struct PokemonData
{
    // Identity
    public string Species;
    public string Nickname;
    public int Level;
    public int Experience;

    // Stats
    public PokemonStats BaseStats;
    public PokemonStats IVs; // Individual Values (0-31)
    public PokemonStats EVs; // Effort Values (0-252)

    // Moves
    public Move[] Moves; // Up to 4

    // Status
    public StatusCondition Status;
    public int HP; // Current HP
}

public struct Ability
{
    public string Name;
    public string Description;
    public AbilityEffect Effect;
}
```

### Systems Needed
1. **PartyManagementSystem** - Add/remove Pokemon
2. **ExperienceSystem** - Level up, stat growth
3. **EvolutionSystem** - Evolution triggers and execution
4. **MoveLearnSystem** - Learn new moves at level-up
5. **StatCalculationSystem** - Calculate actual stats from base/IV/EV

### Implementation Steps
1. Create Pokemon component structure
2. Implement party management
3. Add experience/leveling
4. Implement evolution
5. Add move learning

---

## 🎒 Phase 3: Inventory System
**Estimated Time**: 2 weeks
**Priority**: MEDIUM - Quality of life

### Components Needed
```csharp
public struct Inventory
{
    public Dictionary<string, int> Items; // itemId -> quantity
    public int Money; // Already have Wallet component
}

public struct Item
{
    public string Id;
    public string Name;
    public string Description;
    public ItemCategory Category; // Pokeball, Potion, TM, KeyItem
    public ItemEffect Effect;
}

public struct EquippedItem
{
    public string ItemId; // Held item
}
```

### Systems Needed
1. **InventorySystem** - Add/remove items
2. **ItemEffectSystem** - Use items (potions, pokeballs)
3. **ItemShopSystem** - Buy/sell items

---

## 📱 Phase 4: UI/Menu System
**Estimated Time**: 2-3 weeks
**Priority**: MEDIUM - User experience

### Menus Needed
1. **Main Menu** - Pokemon, Bag, Save, Options
2. **Pokemon Menu** - View party, switch order, use items
3. **Bag Menu** - Browse and use items
4. **Save Menu** - Save game state
5. **Battle Menu** - Fight, Bag, Pokemon, Run

### Can Use GUM UI or ImGui
- **GUM**: Designed for games, matches MonoGame
- **ImGui**: Quick prototyping, immediate mode

---

## 💾 Phase 5: Save/Load System
**Estimated Time**: 1-2 weeks
**Priority**: MEDIUM - Player retention

### What to Save
- Player position and map
- Party Pokemon (all stats, moves, HP)
- Inventory and money
- Story flags and variables (already have GameStateApiService)
- Play time
- Badges/achievements

### Implementation
- Serialize ECS world state
- Save to JSON or binary
- Multiple save slots
- Auto-save on map transition

---

## ✨ Phase 6: Polish & Features
**Estimated Time**: Ongoing
**Priority**: LOW - Nice to have

### Features
- Weather system (rain, sandstorm)
- Day/night cycle
- Evolution animations
- Pokédex system
- Trading system
- Badge/achievement system
- Music and sound effects
- Cutscenes and story events

---

## 📊 My Recommendation: Start Here

### Option A: Battle System First (RECOMMENDED)
**Best for**: Getting a playable game quickly

**Pros**:
- ✅ Most exciting milestone
- ✅ Tests your entire architecture
- ✅ Motivating to see battles work
- ✅ Can test immediately with console

**Cons**:
- ⚠️ Requires Pokemon system (can stub it)
- ⚠️ Longer initial dev time

**Start with**:
```bash
# 1. Create battle components
touch PokeSharp.Game.Components/Components/Battle/BattleState.cs
touch PokeSharp.Game.Components/Components/Battle/Move.cs

# 2. Create battle systems
mkdir -p PokeSharp.Game.Systems/Battle
touch PokeSharp.Game.Systems/Battle/BattleFlowSystem.cs

# 3. Create Pokemon stub (minimal for testing)
touch PokeSharp.Game.Components/Components/Pokemon/Pokemon.cs
```

### Option B: Pokemon System First
**Best for**: Building foundation properly

**Pros**:
- ✅ Logical dependency order
- ✅ Battle system can use real Pokemon
- ✅ Shorter, focused tasks

**Cons**:
- ⚠️ Less exciting initially
- ⚠️ Can't see battles until later

---

## 🎯 Quick Start Guide

### If You Choose Battle System:

1. **Day 1-2**: Create basic components
   ```csharp
   // Minimal to start
   - BattleState component
   - Move component (stub)
   - Pokemon component (stub with just HP/Level)
   ```

2. **Day 3-5**: Build BattleFlowSystem
   ```csharp
   // Turn-based state machine
   - Intro phase
   - Player turn (menu)
   - Execute move
   - Opponent turn
   - End battle
   ```

3. **Week 2**: Add real mechanics
   ```csharp
   // Make battles interesting
   - Damage calculation
   - Type effectiveness
   - Status effects
   ```

4. **Week 3-4**: Polish
   ```csharp
   // Make it feel good
   - Animations
   - UI
   - Sound (optional)
   ```

### If You Choose Pokemon System:

1. **Day 1-3**: Create Pokemon component
2. **Day 4-5**: Add party management
3. **Week 2**: Add leveling/experience
4. **Week 2-3**: Add evolution

---

## 🔧 Using the Console for Development

The debug console will be **invaluable** for testing:

```csharp
// Test battles
Api.StartBattle("pikachu", 10);
Api.Battle.DealDamage(20);
Api.Battle.EndBattle(true);

// Test Pokemon
Api.Party.AddPokemon("charmander", 5);
Api.Party.GiveExperience(100);
Api.Party.LevelUp();

// Test inventory
Api.Inventory.AddItem("potion", 10);
Api.Inventory.UseItem("potion", pokemonIndex: 0);

// Debug specific systems
var battle = GetEntitiesWith<BattleState>().First();
Inspect(battle);
```

---

## 📈 Success Metrics

### Phase 1 Complete When:
- [ ] Can start a wild Pokemon battle
- [ ] Can select and use moves
- [ ] Damage calculation works
- [ ] Can win/lose battles
- [ ] Experience is awarded
- [ ] Can test with console commands

---

## 🎉 Your Call!

**What do you want to build first?**

1. **Battle System** (recommended - most exciting)
2. **Pokemon/Party System** (logical foundation)
3. **Something else?**

Let me know and I'll create a detailed implementation plan! 🚀

