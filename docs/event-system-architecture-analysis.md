# PokeSharp ECS Event System Architecture Analysis

**Date:** 2025-11-29
**Analyzed By:** Code Quality Analyzer
**Scope:** Complete event system architecture review for pokeemerald port readiness

---

## Executive Summary

The ECS event system architecture is **well-designed** with strong separation between internal ECS events and mod-facing events. However, it is **incomplete** for a full pokeemerald port, missing critical Pokemon battle mechanics, party management, and inventory systems.

### Overall Quality Score: 7.5/10

**Strengths:**
- ✅ Clean two-tier architecture (IEcsEventBus vs IModEventBus)
- ✅ Proper use of `readonly struct` for performance
- ✅ Consistent `required` properties
- ✅ EventPriority system well-implemented
- ✅ Cancellable event pattern is sound

**Critical Gaps:**
- ❌ Missing ~40+ Pokemon-specific battle events
- ❌ Missing party/team management events
- ❌ Missing inventory/item system events (only 3 basic events)
- ❌ Missing Pokemon stat/evolution events
- ❌ Missing save/load system events
- ❌ Missing menu/UI state events
- ❌ Incomplete bridge coverage (only 4 events bridged)

---

## 1. Event Design Analysis

### 1.1 Struct Design Quality ✅ EXCELLENT

**All events properly use `readonly struct`:**
```csharp
// ✅ CORRECT - Zero allocation, cache-friendly
public readonly struct MapLoadedEvent : IEcsEvent { ... }
public readonly struct TileEnteredEvent : IEcsEvent { ... }
public readonly struct EntityCreatedEvent : IEcsEvent { ... }
```

**Exception - Cancellable Events (BY DESIGN):**
```csharp
// ✅ CORRECT - Mutable struct for cancellation
public struct EntityCreatingEvent : ICancellableEvent
{
    public bool IsCancelled { get; set; } // Needs to be mutable
}
```

**Verdict:** ✅ Perfect adherence to struct conventions. Cancellable events correctly use mutable struct.

### 1.2 Required Properties Usage ✅ EXCELLENT

All events consistently use `required` for mandatory fields:
```csharp
public readonly struct TileEnteredEvent
{
    public required Entity Entity { get; init; }
    public required (int X, int Y) FromPosition { get; init; }
    public required (int X, int Y) ToPosition { get; init; }
    public required int MapId { get; init; }
    public required float Timestamp { get; init; }
    public required EventPriority Priority { get; init; }

    // Optional fields correctly omit 'required'
    public string? TileBehavior { get; init; }
    public string? TerrainType { get; init; }
}
```

**Verdict:** ✅ Excellent consistency. All events follow the pattern.

### 1.3 EventPriority Usage ⚠️ MOSTLY CONSISTENT

**ECS Events:**
```csharp
public enum EventPriority
{
    Lowest = -100,   // Diagnostics, profiling
    Low = -50,       // Analytics, logging
    Normal = 0,      // Default
    High = 50,       // Important game logic
    Highest = 100,   // Validation, security
    Critical = 1000  // Core systems only
}
```

**Mod Events:**
```csharp
public enum ModEventPriority
{
    First = 0,    // Validation/blocking
    Early = 25,   // Pre-processing
    Normal = 50,  // Default
    Late = 75,    // Post-processing
    Last = 100    // Logging/metrics
}
```

**Issues Found:**
- ⚠️ Bridge events hardcoded to `EventPriority.Lowest` (line 61, 79 in EcsToModEventBridge.cs)
- ⚠️ No events explicitly use `EventPriority.Critical` or `EventPriority.Highest`
- ⚠️ Inconsistency between ECS and Mod priority semantics (values mean different things)

**Recommendation:**
```csharp
// Bridge should respect original event priority
_subscriptions.Add(_ecsEventBus.Subscribe<ECS.TileEnteredEvent>(ecsEvt =>
{
    _modEventBus.Publish(new Modding.TileEnteredEvent { ... });
}, ecsEvt.Priority)); // Use event's priority, not hardcoded Lowest
```

---

## 2. Two-Tier Architecture Analysis

### 2.1 IEcsEventBus vs IModEventBus Separation ✅ CLEAN

**Design Goals:**
1. **IEcsEventBus**: Internal engine events with raw `Entity` references
2. **IModEventBus**: Mod-safe events with `EntityId` (int) instead of `Entity`
3. **EcsToModEventBridge**: Converts ECS → Mod events

**Implementation Quality:**

```csharp
// ✅ ECS Event - Internal use only, raw Entity
public readonly struct EntityCreatedEvent : IEcsEvent
{
    public required Entity Entity { get; init; } // Direct reference
}

// ✅ Mod Event - Safe wrapper, EntityId only
public readonly struct EntitySpawnedEvent
{
    public required int EntityId { get; init; } // Stable ID, no direct access
    public required string Archetype { get; init; }
}
```

**Verdict:** ✅ Separation is architecturally sound and well-documented.

### 2.2 Mod Event Isolation ✅ GOOD

**Safety Features:**
- ✅ Mods cannot access raw `Entity` references
- ✅ Error isolation via try-catch in handlers (ArchEcsEventBus.cs:99-111)
- ✅ IModCancellableEvent tracks `CancelledBy` for debugging
- ✅ ModEventStats provides observability

**Error Handling:**
```csharp
// ArchEcsEventBus.cs - Isolates exceptions
catch (Exception ex)
{
    _logger.LogError(ex, "Handler error for {EventType}", eventType.Name);
    // Does NOT rethrow - one handler's error doesn't break others
}
```

**Verdict:** ✅ Good isolation. Mods are sandboxed from engine internals.

### 2.3 EcsToModEventBridge Completeness ❌ INCOMPLETE

**Currently Bridged (4 events):**
1. ✅ `TileEnteredEvent` (ECS → Mod)
2. ✅ `MapLoadedEvent` (ECS → Mod)
3. ✅ `EntityCreatedEvent` → `EntitySpawnedEvent`
4. ✅ `EntityDestroyedEvent` → `EntityDespawnedEvent`

**Missing Bridges (high-priority):**
- ❌ Player/NPC movement events
- ❌ Interaction events
- ❌ Encounter events
- ❌ Battle events
- ❌ Map loading/unloading events
- ❌ Script loading events
- ❌ Component add/remove/change events

**Data Loss in Bridge:**
```csharp
// EcsToModEventBridge.cs:45-61
_modEventBus.Publish(new Modding.TileEnteredEvent
{
    // ⚠️ Data loss: These fields don't exist in ECS event
    IsPlayer = false,  // TODO: Check if entity has Player component
    Direction = 0,     // Not available - need to calculate
    TileBehavior = null, // Not available
    TerrainType = null,  // Not available

    // ⚠️ Bridge doesn't have World access to fill these
});
```

**Recommendations:**
1. Add `World` reference to bridge for component lookups
2. Enhance ECS events to include commonly-needed fields
3. Bridge ALL gameplay events, not just 4

**Verdict:** ❌ Bridge is a good start but critically incomplete.

---

## 3. Pokemon Engine Events Analysis

### 3.1 Current Event Coverage

**✅ Implemented (13 event types):**
- Entity lifecycle (4 events: Creating, Created, Destroying, Destroyed)
- Component lifecycle (6 events: Adding, Added, Removing, Removed, Changed)
- Movement (4 events: PlayerMoving, PlayerMoved, NpcMoving, NpcMoved)
- Tile events (1 event: TileEntered)
- Map events (6 events: Loading, Loaded, Unloading, Unloaded)
- Interaction events (2 events: Triggering, Triggered)
- Encounter events (2 events: Triggering, Triggered)
- Battle events (4 events: Starting, Started, Ending, Ended) - **STUBS ONLY**
- Modding events (8 events: Script/Mod load/unload, HotReload)
- UI events (4 events: Dialogue, Effects)

**Total: ~35 events defined**

### 3.2 Missing Events for pokeemerald Port

#### 3.2.1 Battle System Events ❌ CRITICAL

**Current State:** Only 4 stub events exist:
```csharp
// GameplayEvents.cs:313-455 - Insufficient for battle system
public struct BattleStartingEvent : ICancellableEvent { ... }
public readonly struct BattleStartedEvent : IEcsEvent { ... }
public struct BattleEndingEvent : ICancellableEvent { ... }
public readonly struct BattleEndedEvent : IEcsEvent { ... }
```

**Missing Events (pokeemerald requirements):**

1. **Turn Flow Events:**
```csharp
// MISSING
public readonly struct BattleTurnStartedEvent : IEcsEvent
{
    public required int BattleId { get; init; }
    public required int TurnNumber { get; init; }
    public required string Weather { get; init; }
    public required string Terrain { get; init; }
}

public struct TurnActionSelectingEvent : ICancellableEvent
{
    public required int BattleId { get; init; }
    public required int ParticipantEntityId { get; init; }
    public required string ActionType { get; init; } // fight, item, switch, run
}

public readonly struct TurnOrderDeterminedEvent : IEcsEvent
{
    public required int BattleId { get; init; }
    public required int[] TurnOrder { get; init; } // Entity IDs in speed order
}
```

2. **Move Execution Events:**
```csharp
// MISSING
public struct MoveExecutingEvent : ICancellableEvent
{
    public required int BattleId { get; init; }
    public required int AttackerEntityId { get; init; }
    public required int TargetEntityId { get; init; }
    public required string MoveId { get; init; }
    public required int MovePower { get; set; }
    public required int Accuracy { get; set; }
    public required float CriticalHitRate { get; set; }
}

public readonly struct MoveExecutedEvent : IEcsEvent
{
    public required int BattleId { get; init; }
    public required string MoveId { get; init; }
    public required bool DidHit { get; init; }
    public required bool WasCritical { get; init; }
    public required float Effectiveness { get; init; } // 0, 0.5, 1, 2, 4
    public required int DamageDealt { get; init; }
}

public readonly struct MoveFailedEvent : IEcsEvent
{
    public required string Reason { get; init; } // "Miss", "Paralyzed", "Flinch", etc.
}
```

3. **Damage & Healing Events:**
```csharp
// MISSING
public struct DamageCalculatingEvent : ICancellableEvent
{
    public required int AttackerEntityId { get; init; }
    public required int DefenderEntityId { get; init; }
    public required int BaseDamage { get; set; }
    public required float TypeEffectiveness { get; set; }
    public required bool IsCritical { get; set; }
}

public readonly struct DamageDealtEvent : IEcsEvent
{
    public required int TargetEntityId { get; init; }
    public required int DamageAmount { get; init; }
    public required int OldHP { get; init; }
    public required int NewHP { get; init; }
    public required string DamageSource { get; init; } // Move, status, weather, etc.
}

public readonly struct HealingAppliedEvent : IEcsEvent
{
    public required int TargetEntityId { get; init; }
    public required int HealAmount { get; init; }
    public required string HealSource { get; init; }
}
```

4. **Status Effect Events:**
```csharp
// MISSING
public struct StatusInflictingEvent : ICancellableEvent
{
    public required int TargetEntityId { get; init; }
    public required string StatusId { get; init; } // "burn", "paralysis", "sleep", etc.
    public required int? SourceEntityId { get; init; }
}

public readonly struct StatusInflictedEvent : IEcsEvent
{
    public required int TargetEntityId { get; init; }
    public required string StatusId { get; init; }
    public required int Duration { get; init; }
}

public readonly struct StatusCuredEvent : IEcsEvent
{
    public required int TargetEntityId { get; init; }
    public required string StatusId { get; init; }
    public required string CureReason { get; init; }
}

public readonly struct StatusTickEvent : IEcsEvent
{
    public required int EntityId { get; init; }
    public required string StatusId { get; init; }
    public required int DamageDealt { get; init; } // For burn/poison
}
```

5. **Stat Stage Events:**
```csharp
// MISSING
public struct StatStageChangingEvent : ICancellableEvent
{
    public required int TargetEntityId { get; init; }
    public required string StatName { get; init; } // attack, defense, speed, etc.
    public required int StageChange { get; init; } // -6 to +6
}

public readonly struct StatStageChangedEvent : IEcsEvent
{
    public required int TargetEntityId { get; init; }
    public required string StatName { get; init; }
    public required int OldStage { get; init; }
    public required int NewStage { get; init; }
}
```

6. **Weather & Terrain Events:**
```csharp
// MISSING
public readonly struct WeatherChangedEvent : IEcsEvent
{
    public required int BattleId { get; init; }
    public required string? OldWeather { get; init; }
    public required string? NewWeather { get; init; }
    public required int Duration { get; init; }
}

public readonly struct TerrainChangedEvent : IEcsEvent
{
    public required int BattleId { get; init; }
    public required string? OldTerrain { get; init; }
    public required string? NewTerrain { get; init; }
}
```

7. **Ability & Item Events:**
```csharp
// MISSING
public readonly struct AbilityActivatedEvent : IEcsEvent
{
    public required int EntityId { get; init; }
    public required string AbilityId { get; init; }
    public required string Trigger { get; init; }
}

public readonly struct HeldItemActivatedEvent : IEcsEvent
{
    public required int EntityId { get; init; }
    public required string ItemId { get; init; }
    public required bool WasConsumed { get; init; }
}
```

8. **Faint & Switch Events:**
```csharp
// MISSING
public readonly struct PokemonFaintedEvent : IEcsEvent
{
    public required int EntityId { get; init; }
    public required int BattleId { get; init; }
    public required int OwnerEntityId { get; init; } // Trainer
}

public struct SwitchingPokemonEvent : ICancellableEvent
{
    public required int BattleId { get; init; }
    public required int TrainerEntityId { get; init; }
    public required int OutgoingPokemonId { get; init; }
    public required int IncomingPokemonId { get; init; }
}

public readonly struct PokemonSwitchedEvent : IEcsEvent
{
    public required int BattleId { get; init; }
    public required int TrainerEntityId { get; init; }
    public required int NewActivePokemonId { get; init; }
}
```

**Total Missing Battle Events: ~25 events**

#### 3.2.2 Pokemon Party/Team Events ❌ CRITICAL

```csharp
// ALL MISSING
public readonly struct PokemonAddedToPartyEvent : IEcsEvent
{
    public required int TrainerEntityId { get; init; }
    public required int PokemonEntityId { get; init; }
    public required int PartySlot { get; init; }
}

public readonly struct PokemonRemovedFromPartyEvent : IEcsEvent
{
    public required int TrainerEntityId { get; init; }
    public required int PokemonEntityId { get; init; }
    public required string Reason { get; init; }
}

public struct PartyReorderingEvent : ICancellableEvent
{
    public required int TrainerEntityId { get; init; }
    public required int[] NewOrder { get; init; }
}
```

#### 3.2.3 Pokemon Evolution Events ❌ CRITICAL

```csharp
// ALL MISSING
public struct EvolutionTriggeringEvent : ICancellableEvent
{
    public required int PokemonEntityId { get; init; }
    public required string FromSpecies { get; init; }
    public required string ToSpecies { get; init; }
    public required string TriggerReason { get; init; } // level, stone, trade, etc.
    public bool AllowCancellation { get; set; } // B button press
}

public readonly struct EvolutionCompletedEvent : IEcsEvent
{
    public required int PokemonEntityId { get; init; }
    public required string FromSpecies { get; init; }
    public required string ToSpecies { get; init; }
    public required string[] NewMoves { get; init; } // Moves learned on evolution
}

public readonly struct EvolutionCancelledEvent : IEcsEvent
{
    public required int PokemonEntityId { get; init; }
    public required string Reason { get; init; }
}
```

#### 3.2.4 Pokemon Level/Experience Events ❌ CRITICAL

```csharp
// ALL MISSING
public readonly struct ExperienceGainedEvent : IEcsEvent
{
    public required int PokemonEntityId { get; init; }
    public required int Amount { get; init; }
    public required string Source { get; init; } // Battle, rare candy, etc.
}

public readonly struct LevelUpEvent : IEcsEvent
{
    public required int PokemonEntityId { get; init; }
    public required int OldLevel { get; init; }
    public required int NewLevel { get; init; }
    public required int[] StatGains { get; init; } // HP, Atk, Def, SpA, SpD, Spe
}

public struct MoveLearnableEvent : ICancellableEvent
{
    public required int PokemonEntityId { get; init; }
    public required string MoveId { get; init; }
    public required int Level { get; init; }
    public int? ReplaceSlot { get; set; } // Which move to replace (0-3), null = skip
}
```

#### 3.2.5 Inventory System Events ❌ INCOMPLETE

**Current State (3 events):**
```csharp
// ModEcsEvents.cs:210-253 - Basic item events exist
public readonly struct ItemObtainedEvent { ... }
public struct ItemUsingEvent : IModCancellableEvent { ... }
public readonly struct ItemUsedEvent { ... }
```

**Missing Events:**
```csharp
// MISSING
public readonly struct ItemQuantityChangedEvent : IEcsEvent
{
    public required int OwnerEntityId { get; init; }
    public required string ItemId { get; init; }
    public required int OldQuantity { get; init; }
    public required int NewQuantity { get; init; }
}

public readonly struct ItemDiscardedEvent : IEcsEvent
{
    public required int OwnerEntityId { get; init; }
    public required string ItemId { get; init; }
    public required int Quantity { get; init; }
}

public struct ItemGivingEvent : ICancellableEvent
{
    public required int SourceEntityId { get; init; }
    public required int TargetEntityId { get; init; }
    public required string ItemId { get; init; }
    public required int Quantity { get; init; }
}

public readonly struct BagOpenedEvent : IEcsEvent
{
    public required int PlayerEntityId { get; init; }
    public required string BagContext { get; init; } // "field", "battle", "shop"
}
```

#### 3.2.6 Save/Load Events ❌ MISSING

**Current State (2 events):**
```csharp
// ModGameplayEvents.cs:418-436 - Only basic save/load notification
public readonly struct GameSavedEvent { ... }
public readonly struct GameLoadedEvent { ... }
```

**Missing Events:**
```csharp
// MISSING
public struct SavingGameEvent : ICancellableEvent
{
    public required string SaveSlot { get; init; }
    public required string SaveLocation { get; init; }
}

public readonly struct SaveFailedEvent : IEcsEvent
{
    public required string Reason { get; init; }
    public required string ErrorMessage { get; init; }
}

public struct LoadingGameEvent : ICancellableEvent
{
    public required string SaveSlot { get; init; }
}

public readonly struct LoadFailedEvent : IEcsEvent
{
    public required string Reason { get; init; }
}

public readonly struct AutoSaveTriggeredEvent : IEcsEvent
{
    public required string TriggerReason { get; init; }
}
```

#### 3.2.7 Menu/UI State Events ❌ MISSING

**Current State (4 events):**
```csharp
// UIEvents.cs - Only dialogue and effects, no menus
public readonly struct DialogueRequestedEvent { ... }
public readonly struct ClearDialogueRequestedEvent { ... }
public readonly struct EffectRequestedEvent { ... }
public readonly struct ClearEffectsRequestedEvent { ... }
```

**Missing Events:**
```csharp
// ALL MISSING
public readonly struct MenuOpenedEvent : IEcsEvent
{
    public required string MenuId { get; init; }
    public required string MenuType { get; init; } // "start", "bag", "pokemon", "save", etc.
}

public readonly struct MenuClosedEvent : IEcsEvent
{
    public required string MenuId { get; init; }
}

public struct MenuSelectionEvent : ICancellableEvent
{
    public required string MenuId { get; init; }
    public required int SelectedIndex { get; init; }
    public required string SelectedOption { get; init; }
}

public readonly struct ConfirmationPromptEvent : IEcsEvent
{
    public required string PromptText { get; init; }
    public required string PromptId { get; init; }
}

public readonly struct ConfirmationResultEvent : IEcsEvent
{
    public required string PromptId { get; init; }
    public required bool Confirmed { get; init; }
}
```

#### 3.2.8 Script Trigger Events ⚠️ PARTIAL

**Current State (8 events):**
```csharp
// ModdingEvents.cs - Script/Mod load/unload exists
public struct ScriptLoadingEvent : ICancellableEvent { ... }
public readonly struct ScriptLoadedEvent : IEcsEvent { ... }
// ... etc
```

**Missing Events:**
```csharp
// MISSING - pokeemerald script triggers
public readonly struct ScriptTriggeredEvent : IEcsEvent
{
    public required string ScriptId { get; init; }
    public required string TriggerType { get; init; } // "interact", "tile", "auto", "warp"
    public required int TriggerEntityId { get; init; }
}

public readonly struct ScriptCompletedEvent : IEcsEvent
{
    public required string ScriptId { get; init; }
    public required string Result { get; init; }
}

public readonly struct ScriptErrorEvent : IEcsEvent
{
    public required string ScriptId { get; init; }
    public required string ErrorMessage { get; init; }
}
```

#### 3.2.9 Trainer Battle Events ❌ MISSING

```csharp
// ALL MISSING
public struct TrainerBattleStartingEvent : ICancellableEvent
{
    public required int PlayerEntityId { get; init; }
    public required int TrainerEntityId { get; init; }
    public required string TrainerClass { get; init; }
    public required string TrainerName { get; init; }
    public required int[] TrainerParty { get; init; } // Pokemon entity IDs
    public string? IntroMessage { get; set; }
}

public readonly struct TrainerDefeatedEvent : IEcsEvent
{
    public required int TrainerEntityId { get; init; }
    public required int PrizeMoneyAwarded { get; init; }
    public required bool CanRebattle { get; init; }
}

public readonly struct TrainerRematchEvent : IEcsEvent
{
    public required int TrainerEntityId { get; init; }
    public required int RematchCount { get; init; }
}
```

#### 3.2.10 Warp/Connection Events ⚠️ PARTIAL

**Current State:**
```csharp
// Map loading exists, but no warp-specific events
public struct MapLoadingEvent : ICancellableEvent { ... }
public readonly struct MapLoadedEvent : IEcsEvent { ... }
```

**Missing Events:**
```csharp
// MISSING
public struct WarpTriggeringEvent : ICancellableEvent
{
    public required int PlayerEntityId { get; init; }
    public required string TargetMapId { get; init; }
    public required int TargetX { get; init; }
    public required int TargetY { get; init; }
    public required string WarpType { get; init; } // "door", "warp", "hole", "teleport"
}

public readonly struct WarpExecutedEvent : IEcsEvent
{
    public required string FromMapId { get; init; }
    public required string ToMapId { get; init; }
    public required (int X, int Y) FromPosition { get; init; }
    public required (int X, int Y) ToPosition { get; init; }
}
```

#### 3.2.11 Pokemon Catching Events ❌ CRITICAL

```csharp
// ALL MISSING
public struct PokemonCatchAttemptEvent : ICancellableEvent
{
    public required int PlayerEntityId { get; init; }
    public required int WildPokemonEntityId { get; init; }
    public required string PokeballType { get; init; }
    public required float BaseCatchRate { get; set; }
    public required int HPPercent { get; init; }
    public required string? StatusCondition { get; init; }
}

public readonly struct PokeballShakeEvent : IEcsEvent
{
    public required int ShakeNumber { get; init; } // 1-3
    public required bool DidBreakOut { get; init; }
}

public readonly struct PokemonCaughtEvent : IEcsEvent
{
    public required int PokemonEntityId { get; init; }
    public required string Species { get; init; }
    public required int Level { get; init; }
    public required bool IsNewSpecies { get; init; } // First time catching
}

public readonly struct PokemonBreakoutEvent : IEcsEvent
{
    public required int WildPokemonEntityId { get; init; }
    public required int ShakesBeforeBreakout { get; init; }
}
```

---

## 4. Performance Concerns Analysis

### 4.1 Event Allocation Patterns ✅ EXCELLENT

**Zero-allocation design:**
```csharp
// ✅ Struct events passed by value - no heap allocation
public void Publish<TEvent>(TEvent evt)
    where TEvent : struct, IEcsEvent
{
    // evt is on stack, handlers receive copy
}

// ✅ Handler caching prevents re-sorting
private volatile bool _isSorted;
private IHandlerWrapperBase[]? _sortedCache;
```

**Performance Characteristics:**
- ✅ O(1) publish when no handlers
- ✅ O(n) iteration over handlers
- ✅ O(n log n) sorting only when handler list changes
- ✅ Zero GC allocations for event publishing
- ✅ Cached sorted handler lists

**Potential Issue - Large Structs:**
```csharp
// ⚠️ Some events are getting large (>16 bytes)
public readonly struct BattleEndedEvent : IEcsEvent
{
    public required Entity PlayerEntity { get; init; }      // 8 bytes
    public required Entity OpponentEntity { get; init; }    // 8 bytes
    public required string Outcome { get; init; }           // 8 bytes (ref)
    public int BattleId { get; init; }                     // 4 bytes
    public int ExperienceGained { get; init; }             // 4 bytes
    public int MoneyGained { get; init; }                  // 4 bytes
    public required float Timestamp { get; init; }         // 4 bytes
    public required EventPriority Priority { get; init; }  // 4 bytes
    // Total: 44 bytes - Still acceptable for stack
}
```

**Recommendation:** Continue monitoring struct sizes. Consider `ref struct` if events exceed 64 bytes.

**Verdict:** ✅ Excellent performance characteristics. No allocation issues.

### 4.2 Handler Registration/Disposal ✅ GOOD

**Thread-safe registration:**
```csharp
// ArchEcsEventBus.cs:57
private readonly ConcurrentDictionary<Type, HandlerCollection> _handlers = new();

// ArchEcsEventBus.cs:192-211
private IDisposable SubscribeInternal<TEvent>(Action<TEvent> handler, EventPriority priority)
{
    var collection = _handlers.GetOrAdd(eventType, _ => new HandlerCollection());
    // Thread-safe add
    collection.Add(wrapper);
    return new EcsSubscription(...);
}
```

**Proper disposal:**
```csharp
// EcsSubscription.cs:330-338
public void Dispose()
{
    if (!_disposed)
    {
        _eventBus.Unsubscribe(_eventType, _handlerId);
        _disposed = true; // Prevents double-disposal
    }
}
```

**Potential Issue - Memory Leaks:**
```csharp
// ⚠️ EcsToModEventBridge stores all subscriptions in List
private readonly List<IDisposable> _subscriptions = new();

// ✅ But properly disposes on cleanup
public void Dispose()
{
    foreach (var sub in _subscriptions)
        sub.Dispose();
    _subscriptions.Clear();
}
```

**Recommendation:** Monitor long-running sessions for subscription leaks. Consider weak references for ephemeral handlers.

**Verdict:** ✅ Solid implementation with proper cleanup.

### 4.3 Event Bubbling/Cancellation ✅ EFFICIENT

**Cancellation short-circuit:**
```csharp
// ArchEcsEventBus.cs:123-139
foreach (var wrapper in handlers)
{
    wrapper.Handler(evt);

    if (evt.IsCancelled)
    {
        _logger.LogDebug("Event {EventType} cancelled", eventType.Name);
        return false; // ✅ Early exit - no wasted iterations
    }
}
```

**Priority ordering prevents re-evaluation:**
```csharp
// ArchEcsEventBus.cs:269-273
var sorted = _handlers.Values
    .Cast<HandlerWrapper<TEvent>>()
    .OrderByDescending(h => h.Priority)  // ✅ Highest first
    .ThenBy(h => h.HandlerId)            // ✅ Subscription order
    .ToArray();
```

**Verdict:** ✅ Efficient cancellation with early exit.

---

## 5. Missing Events Summary for pokeemerald Port

### 5.1 Critical Missing Events (Must-Have)

**Battle System (25 events):**
- Turn flow (3 events)
- Move execution (3 events)
- Damage/healing (3 events)
- Status effects (4 events)
- Stat stages (2 events)
- Weather/terrain (2 events)
- Abilities/items (2 events)
- Faint/switch (3 events)
- Trainer battles (3 events)

**Pokemon Management (15 events):**
- Party management (3 events)
- Evolution (3 events)
- Level/experience (3 events)
- Catching (4 events)
- Stat changes (2 events)

**Total Critical: ~40 events**

### 5.2 High-Priority Missing Events (Should-Have)

**Inventory System (4 events):**
- Quantity changes
- Item discarding
- Item giving/trading
- Bag UI

**Save/Load System (4 events):**
- Save/load cancellation
- Save/load failure
- Autosave triggers

**Menu/UI (5 events):**
- Menu open/close
- Menu selection
- Confirmation prompts

**Total High-Priority: ~13 events**

### 5.3 Medium-Priority Missing Events (Nice-to-Have)

**Script System (3 events):**
- Script execution triggers
- Script completion
- Script errors

**Warp/Connection (2 events):**
- Warp triggering
- Warp execution

**Total Medium-Priority: ~5 events**

### 5.4 Bridge Completeness Gap

**Currently Bridged:** 4 events
**Should Be Bridged:** ~15 gameplay events
**Gap:** 11 events not bridged

---

## 6. Recommendations

### 6.1 Immediate Actions (Critical)

1. **Add Battle Event File:**
```csharp
// Create: PokeSharp.Engine.Core/Events/ECS/BattleEvents.cs
// Add all 25 battle-related events with proper struct design
```

2. **Add Pokemon Event File:**
```csharp
// Create: PokeSharp.Engine.Core/Events/ECS/PokemonEvents.cs
// Add all 15 Pokemon management events
```

3. **Expand Bridge Coverage:**
```csharp
// EcsToModEventBridge.cs - Add bridges for:
- PlayerMovingEvent/PlayerMovedEvent
- InteractionTriggeringEvent/InteractionTriggeredEvent
- EncounterTriggeringEvent/EncounterTriggeredEvent
- BattleStartingEvent/BattleEndedEvent
```

4. **Fix Bridge Data Loss:**
```csharp
// Add World reference to bridge
public sealed class EcsToModEventBridge
{
    private readonly World _world; // NEW

    public EcsToModEventBridge(
        IEcsEventBus ecsEventBus,
        IModEventBus modEventBus,
        World world, // NEW
        ILogger<EcsToModEventBridge>? logger = null)
    {
        _world = world;
        // ...
    }

    // Now can query components:
    _modEventBus.Publish(new Modding.TileEnteredEvent
    {
        IsPlayer = _world.TryGet(ecsEvt.Entity, out Player _),
        // Can fill other fields from components
    });
}
```

### 6.2 Design Improvements (High-Priority)

1. **Unify Priority Systems:**
```csharp
// Create shared priority enum or document mapping
// Current: ECS uses -100 to 1000, Mod uses 0 to 100
// Recommendation: Map Mod priorities to ECS priorities
public static class PriorityMapper
{
    public static EventPriority ToEcsPriority(ModEventPriority modPriority)
    {
        return modPriority switch
        {
            ModEventPriority.First => EventPriority.Highest,
            ModEventPriority.Early => EventPriority.High,
            ModEventPriority.Normal => EventPriority.Normal,
            ModEventPriority.Late => EventPriority.Low,
            ModEventPriority.Last => EventPriority.Lowest,
            _ => EventPriority.Normal
        };
    }
}
```

2. **Add Event Validation:**
```csharp
// Add to IEcsEvent
public interface IEcsEvent
{
    float Timestamp { get; }
    EventPriority Priority { get; }

    // NEW
    bool Validate(out string? errorMessage);
}

// Events can validate themselves
public readonly struct BattleStartingEvent : IEcsEvent, IValidatable
{
    public bool Validate(out string? errorMessage)
    {
        if (PlayerEntity == Entity.Null)
        {
            errorMessage = "PlayerEntity cannot be null";
            return false;
        }
        errorMessage = null;
        return true;
    }
}
```

3. **Add Event Telemetry:**
```csharp
// Track event publishing metrics
public interface IEcsEventBus
{
    // NEW
    EventBusMetrics GetMetrics();
}

public readonly struct EventBusMetrics
{
    public required long TotalEventsPublished { get; init; }
    public required long TotalEventsCancelled { get; init; }
    public required double AverageHandlerTimeMs { get; init; }
    public required Dictionary<Type, long> EventCountsByType { get; init; }
}
```

### 6.3 Architecture Enhancements (Medium-Priority)

1. **Event Replay System:**
```csharp
// For debugging and testing
public interface IEventRecorder
{
    void StartRecording();
    void StopRecording();
    IReadOnlyList<RecordedEvent> GetRecordedEvents();
    void Replay(IEcsEventBus eventBus);
}
```

2. **Event Filtering:**
```csharp
// Allow conditional subscriptions
eventBus.Subscribe<TileEnteredEvent>(
    handler,
    filter: evt => evt.IsPlayer, // Only player events
    priority: EventPriority.Normal
);
```

3. **Async Event Handlers:**
```csharp
// For expensive operations
public interface IEcsEventBus
{
    IDisposable SubscribeAsync<TEvent>(
        Func<TEvent, Task> handler,
        EventPriority priority = EventPriority.Normal
    ) where TEvent : struct, IEcsEvent;
}
```

### 6.4 Documentation Improvements

1. **Add Event Catalog:**
```markdown
# docs/event-catalog.md
## All Available Events

### Entity Lifecycle
- EntityCreatingEvent - Before entity spawn (cancellable)
- EntityCreatedEvent - After entity spawn
...
```

2. **Add Usage Examples:**
```csharp
// docs/examples/event-usage.md
## Cancelling Movement

var sub = ecsEventBus.Subscribe<PlayerMovingEvent>(evt =>
{
    if (IsBlockedPosition(evt.ToPosition))
    {
        evt.IsCancelled = true;
    }
}, EventPriority.High);
```

3. **Add Bridge Documentation:**
```markdown
# docs/event-bridge-architecture.md
## Why Two Event Buses?

ECS events expose raw Entity references for performance.
Mod events use EntityId (int) for safety and API stability.
```

---

## 7. Conclusion

### 7.1 Overall Assessment

The event system architecture is **well-designed** but **incomplete**. The two-tier separation is sound, struct-based events are performant, and error isolation works correctly. However, the system needs significant expansion to support a full pokeemerald port.

### 7.2 Readiness Score by System

| System | Current State | Missing Events | Readiness |
|--------|---------------|----------------|-----------|
| Entity/Component | ✅ Complete | 0 | 100% |
| Movement | ✅ Complete | 0 | 100% |
| Map Loading | ✅ Complete | 0 | 100% |
| Tile Events | ✅ Complete | 0 | 100% |
| Basic Interaction | ✅ Complete | 0 | 100% |
| Basic Encounters | ⚠️ Partial | 2 | 70% |
| **Battle System** | ❌ Stub Only | 25 | **15%** |
| **Pokemon Mgmt** | ❌ Missing | 15 | **0%** |
| **Inventory** | ⚠️ Basic Only | 4 | **40%** |
| **Save/Load** | ⚠️ Basic Only | 4 | **50%** |
| **UI/Menus** | ⚠️ Partial | 5 | **30%** |
| Scripts/Mods | ✅ Complete | 3 | 80% |
| Event Bridge | ❌ Minimal | 11 | **25%** |

**Overall Readiness: ~55%**

### 7.3 Priority Roadmap

**Phase 1 (Critical - 2-3 weeks):**
- Add all 25 battle events
- Add all 15 Pokemon management events
- Expand bridge to cover 15+ gameplay events
- Fix bridge data loss issues

**Phase 2 (High - 1-2 weeks):**
- Add inventory system events
- Add save/load system events
- Add menu/UI events
- Add event validation

**Phase 3 (Polish - 1 week):**
- Add telemetry/metrics
- Add event replay system
- Complete documentation
- Add usage examples

**Total Estimated Effort: 4-6 weeks**

### 7.4 Final Verdict

**Architecture Quality: A- (9/10)**
- Excellent design patterns
- Good performance characteristics
- Clean separation of concerns

**Implementation Completeness: C (6/10)**
- Missing critical battle events
- Missing Pokemon management events
- Bridge coverage too limited

**Overall Grade: B- (7.5/10)**

The foundation is solid, but significant work is needed before this can support a full pokeemerald port. The good news is that the architecture can accommodate all the missing events without major refactoring.

---

## Appendix A: Event Count by Category

| Category | ECS Events | Mod Events | Bridged | Total |
|----------|------------|------------|---------|-------|
| Entity Lifecycle | 8 | 2 | 2 | 10 |
| Component Lifecycle | 6 | 2 | 0 | 8 |
| Movement | 4 | 3 | 1 | 7 |
| Tile | 1 | 2 | 1 | 3 |
| Map | 6 | 4 | 1 | 10 |
| Interaction | 2 | 2 | 0 | 4 |
| Encounters | 2 | 2 | 0 | 4 |
| Battle (current) | 4 | 2 | 0 | 6 |
| Modding | 8 | 0 | 0 | 8 |
| UI | 4 | 0 | 0 | 4 |
| Items | 0 | 3 | 0 | 3 |
| NPCs | 0 | 3 | 0 | 3 |
| Save/Load | 0 | 2 | 0 | 2 |
| Flags/Variables | 0 | 2 | 0 | 2 |
| **TOTAL** | **45** | **29** | **5** | **74** |

**Required for pokeemerald: ~130 events**
**Current coverage: ~57%**

---

## Appendix B: Code Quality Issues Found

### Critical Issues: 0
### High-Priority Issues: 3

1. **Bridge Data Loss (High)**
   - Location: `EcsToModEventBridge.cs:50-61`
   - Issue: Bridge cannot populate all mod event fields
   - Fix: Add World reference to bridge

2. **Inconsistent Priority Systems (High)**
   - Location: `EventPriority.cs`, `ModEventPriority.cs`
   - Issue: Different value ranges and semantics
   - Fix: Unify or document mapping

3. **Hardcoded Bridge Priority (Medium)**
   - Location: `EcsToModEventBridge.cs:61, 79`
   - Issue: All bridge events use EventPriority.Lowest
   - Fix: Respect original event priority

### Low-Priority Issues: 2

4. **Large Struct Sizes (Low)**
   - Location: Various event files
   - Issue: Some events approaching 64 bytes
   - Fix: Monitor and consider ref struct if needed

5. **Missing Event Validation (Low)**
   - Location: All event files
   - Issue: No validation of required fields at runtime
   - Fix: Add IValidatable interface

---

**End of Report**
