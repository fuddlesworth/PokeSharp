# Event System Audit - Dead Code Analysis

## Summary

The codebase has **two parallel event hierarchies** with significant overlap. The **`Game.Systems.Events`** are actively used by production systems, while **`Engine.Core.Events.Movement` and `Engine.Core.Events.Collision`** appear to be **dead code**.

## Events Being Published by Production Systems

### ✅ ACTIVELY USED - `Game.Systems.Events`

**Published by `MovementSystem`:**
- `MovementStartedEvent` (with `Direction` enum) ✅
- `MovementCompletedEvent` (with `Direction` enum) ✅
- `MovementBlockedEvent` (with `Direction` enum) ✅

**Published by `CollisionSystem`:**
- `CollisionCheckEvent` (with `Direction` enum) ✅
- `CollisionDetectedEvent` (with `Direction` enum) ✅
- `CollisionResolvedEvent` ✅

**Published by `TileBehaviorSystem` / `NPCBehaviorSystem` / `ScriptAttachmentSystem`:**
- `TileSteppedOnEvent` (from `Engine.Core.Events.Tile`) ✅
- `TileSteppedOffEvent` (from `Engine.Core.Events.Tile`) ✅
- `TickEvent` (from `Engine.Core.Events.System`) ✅

**Published by Scripting Services:**
- `DialogueRequestedEvent` (from `Engine.Core.Types.Events`) ✅
- `EffectRequestedEvent` (from `Engine.Core.Types.Events`) ✅

---

## ❌ DEAD CODE - Never Published

### `Engine.Core.Events.Movement/` (3 files - ALL DEAD CODE)

1. **`MovementStartedEvent.cs`**
   - Uses `int Direction` instead of `Direction` enum
   - **NEVER instantiated** in production code
   - **NEVER published** anywhere
   - Replaced by: `Game.Systems.Events.MovementStartedEvent`

2. **`MovementCompletedEvent.cs`**
   - Uses `int Direction` instead of `Direction` enum
   - **NEVER instantiated** in production code
   - **NEVER published** anywhere
   - Replaced by: `Game.Systems.Events.MovementCompletedEvent`

3. **`MovementBlockedEvent.cs`**
   - Uses `int Direction` instead of `Direction` enum
   - **NEVER instantiated** in production code
   - **NEVER published** anywhere
   - Replaced by: `Game.Systems.Events.MovementBlockedEvent`

### `Engine.Core.Events.Collision/` (3 files - ALL DEAD CODE)

1. **`CollisionCheckEvent.cs`**
   - Uses `int FromDirection` instead of `Direction` enum
   - **NEVER instantiated** in production code
   - **NEVER published** anywhere
   - Replaced by: `Game.Systems.Events.CollisionCheckEvent`

2. **`CollisionDetectedEvent.cs`**
   - Uses `int Direction` instead of `Direction` enum
   - **NEVER instantiated** in production code
   - **NEVER published** anywhere
   - Replaced by: `Game.Systems.Events.CollisionDetectedEvent`

3. **`CollisionResolvedEvent.cs`**
   - Uses `int Direction` instead of `Direction` enum
   - **NEVER instantiated** in production code
   - **NEVER published** anywhere
   - Replaced by: `Game.Systems.Events.CollisionResolvedEvent`

### `Engine.Core.Events.NPC/` (3 files - DOCS ONLY)

1. **`BattleTriggeredEvent.cs`**
   - Only referenced in **documentation** (not production code)
   - Example placeholder for mod authors

2. **`DialogueStartedEvent.cs`**
   - Only referenced in **documentation** (not production code)
   - Different from `DialogueRequestedEvent` (which IS used)

3. **`NPCInteractionEvent.cs`**
   - Only referenced in **documentation** (not production code)
   - Example placeholder for mod authors

---

## Evidence: System Event Pools

The `MovementSystem` and `CollisionSystem` create **event pools** for the `Game.Systems.Events` types:

```csharp
// MovementSystem.cs lines 43-45
private static readonly EventPool<MovementStartedEvent> _startedEventPool = ...;
private static readonly EventPool<MovementCompletedEvent> _completedEventPool = ...;
private static readonly EventPool<MovementBlockedEvent> _blockedEventPool = ...;

// CollisionSystem.cs lines 29-31
private static readonly EventPool<CollisionCheckEvent> _checkEventPool = ...;
private static readonly EventPool<CollisionDetectedEvent> _detectedEventPool = ...;
private static readonly EventPool<CollisionResolvedEvent> _resolvedEventPool = ...;
```

**These are the ONLY event pools** - there are NO pools for the `Engine.Core.Events.Movement` or `Engine.Core.Events.Collision` types.

---

## Why This Confusion Exists

Looking at the architecture:

```
Engine.Common (primitives)
    ↓
Engine.Core (intended to be game-agnostic)
    ↓
Game.Components (game-specific types like Direction enum)
    ↓
Game.Systems (game-specific logic)
```

The original design tried to keep `Engine.Core` "game-agnostic" by using `int Direction` instead of the `Direction` enum. But this created:

1. **Type safety issues** - magic numbers instead of enums
2. **Circular dependency concerns** - if Engine.Core used Game.Components
3. **Developer confusion** - which events to use?

The solution was to:
- Keep `Engine.Core.Events` for **base interfaces** (`IGameEvent`, `ICancellableEvent`, etc.)
- Create `Game.Systems.Events` for **concrete events with proper types**
- Update all systems to publish `Game.Systems.Events`

But the old `Engine.Core.Events.Movement` and `Engine.Core.Events.Collision` were **never deleted**, creating dead code.

---

## Cleanup Recommendation

### SAFE TO DELETE (6 files - ~500 LOC)

```
Engine.Core/Events/Movement/
  ❌ MovementStartedEvent.cs
  ❌ MovementCompletedEvent.cs
  ❌ MovementBlockedEvent.cs

Engine.Core/Events/Collision/
  ❌ CollisionCheckEvent.cs
  ❌ CollisionDetectedEvent.cs
  ❌ CollisionResolvedEvent.cs
```

These files are:
- Never instantiated in production code
- Never published by any system
- Never subscribed to (scripts use `Game.Systems.Events` instead)
- Causing confusion (tile behaviors were using the wrong imports)

### KEEP (Documentation Examples)

```
Engine.Core/Events/NPC/
  ✅ BattleTriggeredEvent.cs (docs only - example for modders)
  ✅ DialogueStartedEvent.cs (docs only - example for modders)
  ✅ NPCInteractionEvent.cs (docs only - example for modders)
```

These are **example templates** for mod developers, not dead code.

### KEEP (Actively Used)

```
Engine.Core/Events/Tile/
  ✅ TileSteppedOnEvent.cs (published by TileBehaviorSystem)
  ✅ TileSteppedOffEvent.cs (published by TileBehaviorSystem)

Engine.Core/Events/System/
  ✅ TickEvent.cs (published by ScriptAttachmentSystem, NPCBehaviorSystem)

Engine.Core/Events/ (Base classes)
  ✅ IGameEvent.cs
  ✅ ICancellableEvent.cs
  ✅ IEntityEvent.cs
  ✅ ITileEvent.cs
  ✅ IPoolableEvent.cs
  ✅ GameEventBase.cs (CancellableEventBase)
  ✅ NotificationEventBase.cs
  ✅ EventBus.cs
  ✅ EventPool.cs
```

---

## Impact Analysis

### Breaking Changes: NONE

Deleting the dead code files will **NOT break anything** because:

1. ✅ **No production code uses them** - verified by grep searches
2. ✅ **No systems publish them** - all systems use `Game.Systems.Events`
3. ✅ **No scripts should import them** - we just fixed the tile behaviors to use correct imports
4. ✅ **Tests use live events** - tests instantiate events for testing but use production event types

### Documentation Updates Needed

- Update any docs that reference `Engine.Core.Events.Movement`
- Update any docs that reference `Engine.Core.Events.Collision`
- Point developers to `Game.Systems.Events` instead

---

## Verification Commands

To verify these events are truly dead:

```bash
# Check for any instantiation (new keyword)
rg "new (Movement|Collision)(Started|Completed|Blocked|Check|Detected|Resolved)Event" --type cs

# Check for event pools
rg "EventPool<(Movement|Collision)" --type cs

# Check for Publish calls
rg "Publish.*Event" PokeSharp.Game.Systems/ --type cs
```

All of these confirm: **Only `Game.Systems.Events` are used in production.**

