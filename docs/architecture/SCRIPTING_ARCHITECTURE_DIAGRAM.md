# PokeSharp Scripting Architecture - Visual Diagrams

## 1. Layer Architecture

```
┌─────────────────────────────────────────────────────────────────────┐
│                        USER SCRIPTS (.csx)                          │
│  Example: Scripts/behaviors/patrol_guard.csx                       │
│                                                                     │
│  public class PatrolGuard : TypeScriptBase                          │
│  {                                                                  │
│      public override void OnTick(ScriptContext ctx, float dt)      │
│      {                                                              │
│          ctx.Player.GetPlayerPosition();                           │
│          ctx.Npc.FaceEntity(npc, player);                          │
│      }                                                              │
│  }                                                                  │
└──────────────────────────────┬──────────────────────────────────────┘
                               │ inherits from
┌──────────────────────────────▼──────────────────────────────────────┐
│                    LAYER 3: PokeSharp.Scripting                     │
│                    (Scripting Infrastructure)                       │
│                                                                     │
│  ┌─────────────────────────────────────────────────────────────┐  │
│  │ Runtime/                                                     │  │
│  │  ├── TypeScriptBase           (Base class for scripts)      │  │
│  │  └── ScriptContext            (Execution context)           │  │
│  └─────────────────────────────────────────────────────────────┘  │
│  ┌─────────────────────────────────────────────────────────────┐  │
│  │ Services/                                                    │  │
│  │  └── ScriptService            (Roslyn compiler/executor)    │  │
│  └─────────────────────────────────────────────────────────────┘  │
│  ┌─────────────────────────────────────────────────────────────┐  │
│  │ Compilation/                                                 │  │
│  │  └── RoslynScriptCompiler     (C# script compilation)       │  │
│  └─────────────────────────────────────────────────────────────┘  │
│  ┌─────────────────────────────────────────────────────────────┐  │
│  │ HotReload/                                                   │  │
│  │  └── ScriptHotReloadService   (File watcher)                │  │
│  └─────────────────────────────────────────────────────────────┘  │
└──────────────────────────────┬──────────────────────────────────────┘
                               │ depends on (interface)
┌──────────────────────────────▼──────────────────────────────────────┐
│                    LAYER 2A: PokeSharp.Core                         │
│                    (Scripting API - Interface Layer)                │
│                                                                     │
│  ┌─────────────────────────────────────────────────────────────┐  │
│  │ ScriptingApi/                                                │  │
│  │  ├── IScriptingApiProvider    (Master facade interface)     │  │
│  │  ├── IPlayerApi                                              │  │
│  │  ├── INPCApi                                                 │  │
│  │  ├── IMapApi                                                 │  │
│  │  ├── IGameStateApi                                           │  │
│  │  ├── IDialogueApi                                            │  │
│  │  └── IEffectApi                                              │  │
│  └─────────────────────────────────────────────────────────────┘  │
└──────────────────────────────┬──────────────────────────────────────┘
                               │ implemented by
┌──────────────────────────────▼──────────────────────────────────────┐
│                    LAYER 2B: PokeSharp.Core                         │
│                    (Scripting API - Implementation Layer)           │
│                                                                     │
│  ┌─────────────────────────────────────────────────────────────┐  │
│  │ Scripting/Services/                                          │  │
│  │  ├── ScriptingApiProvider     (Master facade impl)          │  │
│  │  ├── PlayerApiService          (Player operations)          │  │
│  │  ├── NpcApiService             (NPC operations)             │  │
│  │  ├── MapApiService             (Map queries)                │  │
│  │  ├── GameStateApiService       (Flags/variables)            │  │
│  │  ├── DialogueApiService        (Dialogue boxes)             │  │
│  │  └── EffectApiService          (Visual effects)             │  │
│  └─────────────────────────────────────────────────────────────┘  │
└──────────────────────────────┬──────────────────────────────────────┘
                               │ operates on
┌──────────────────────────────▼──────────────────────────────────────┐
│                    LAYER 2C: PokeSharp.Core                         │
│                    (ECS Components & Systems)                       │
│                                                                     │
│  ┌─────────────────────────────────────────────────────────────┐  │
│  │ Components/                                                  │  │
│  │  ├── Movement/                                               │  │
│  │  │   ├── Position                                            │  │
│  │  │   ├── GridMovement                                        │  │
│  │  │   └── Direction                                           │  │
│  │  ├── NPCs/                                                   │  │
│  │  │   ├── Npc                                                 │  │
│  │  │   ├── Behavior                                            │  │
│  │  │   └── MovementRoute                                       │  │
│  │  ├── Player/                                                 │  │
│  │  │   ├── Player                                              │  │
│  │  │   └── Wallet                                              │  │
│  │  └── Common/                                                 │  │
│  │      ├── Name                                                │  │
│  │      └── Health                                              │  │
│  └─────────────────────────────────────────────────────────────┘  │
│  ┌─────────────────────────────────────────────────────────────┐  │
│  │ Systems/                                                     │  │
│  │  ├── NPCBehaviorSystem        (Script execution)            │  │
│  │  ├── MovementSystem            (Movement processing)        │  │
│  │  └── BaseSystem                (System base class)          │  │
│  └─────────────────────────────────────────────────────────────┘  │
└──────────────────────────────┬──────────────────────────────────────┘
                               │ uses
┌──────────────────────────────▼──────────────────────────────────────┐
│                    LAYER 1: External Dependencies                   │
│                                                                     │
│  ├── Arch.Core                 (ECS framework)                     │
│  ├── Microsoft.Extensions.*    (DI, Logging)                       │
│  └── MonoGame                  (Graphics, Input)                   │
└─────────────────────────────────────────────────────────────────────┘
```

---

## 2. Dependency Flow

### Clean One-Way Dependencies (No Circular References)

```
┌──────────────────┐
│ Script Files     │
│ (.csx)           │
└────────┬─────────┘
         │ inherits
         ▼
┌──────────────────┐
│ TypeScriptBase   │  ◄──┐
└────────┬─────────┘     │
         │ uses          │
         ▼               │
┌──────────────────┐     │
│ ScriptContext    │     │
└────────┬─────────┘     │  PokeSharp.Scripting
         │ depends on    │  (Layer 3)
         ▼               │
┌──────────────────┐     │
│ ScriptService    │  ◄──┘
└────────┬─────────┘
         │ depends on
         ▼
┌───────────────────────────┐
│ IScriptingApiProvider     │  ◄──┐
│ (Interface)               │     │
└────────┬──────────────────┘     │
         │ implemented by         │
         ▼                        │  PokeSharp.Core
┌───────────────────────────┐     │  (Layer 2)
│ ScriptingApiProvider      │     │
│ (Facade)                  │     │
└────────┬──────────────────┘     │
         │ aggregates             │
         ▼                        │
┌───────────────────────────┐     │
│ API Services:             │     │
│ - PlayerApiService        │     │
│ - NpcApiService           │     │
│ - MapApiService           │  ◄──┘
│ - GameStateApiService     │
│ - DialogueApiService      │
│ - EffectApiService        │
└────────┬──────────────────┘
         │ operates on
         ▼
┌───────────────────────────┐
│ ECS Components:           │
│ - Position                │
│ - GridMovement            │
│ - Npc, Behavior           │
│ - Player, Wallet          │
│ - etc.                    │
└───────────────────────────┘
```

**Key Points**:
- All arrows point **downward** ✅
- No reverse dependencies ✅
- Scripting layer depends on Core interfaces ✅
- Core implements interfaces and provides components ✅

---

## 3. Facade Pattern Structure

### IScriptingApiProvider Facade

```
┌─────────────────────────────────────────────────────────────┐
│         IScriptingApiProvider (Interface)                   │
│                                                             │
│  public interface IScriptingApiProvider                     │
│  {                                                          │
│      PlayerApiService Player { get; }                       │
│      NpcApiService Npc { get; }                             │
│      MapApiService Map { get; }                             │
│      GameStateApiService GameState { get; }                 │
│      DialogueApiService Dialogue { get; }                   │
│      EffectApiService Effects { get; }                      │
│  }                                                          │
└─────────────────────────┬───────────────────────────────────┘
                          │ implemented by
┌─────────────────────────▼───────────────────────────────────┐
│         ScriptingApiProvider (Implementation)               │
│                                                             │
│  public class ScriptingApiProvider : IScriptingApiProvider  │
│  {                                                          │
│      private readonly PlayerApiService _playerApi;          │
│      private readonly NpcApiService _npcApi;                │
│      private readonly MapApiService _mapApi;                │
│      private readonly GameStateApiService _gameStateApi;    │
│      private readonly DialogueApiService _dialogueApi;      │
│      private readonly EffectApiService _effectApi;          │
│                                                             │
│      public PlayerApiService Player => _playerApi;          │
│      public NpcApiService Npc => _npcApi;                   │
│      // ... other properties                                │
│  }                                                          │
└─────────────────────────┬───────────────────────────────────┘
                          │ aggregates
        ┌─────────────────┴─────────────────┬─────────────────┐
        ▼                 ▼                  ▼                 ▼
┌───────────────┐ ┌───────────────┐ ┌───────────────┐ ┌──────────────┐
│PlayerApiService│ │ NpcApiService │ │ MapApiService │ │GameStateApi..│
│               │ │               │ │               │ │              │
│ GetMoney()    │ │ MoveNPC()     │ │ IsWalkable()  │ │ SetFlag()    │
│ GiveMoney()   │ │ FaceEntity()  │ │ GetEntities() │ │ GetFlag()    │
│ GetPosition() │ │ SetNPCPath()  │ │ Transition()  │ │ SetVar()     │
└───────────────┘ └───────────────┘ └───────────────┘ └──────────────┘
```

**Benefits**:
- Reduces DI from 6 parameters to 1 ✅
- Provides single point of API access ✅
- Encapsulates service dependencies ✅
- Consistent with other facades in codebase ✅

---

## 4. Script Execution Flow

```
┌─────────────────────────────────────────────────────────────┐
│                   GAME LOOP TICK                            │
└────────────────────────┬────────────────────────────────────┘
                         │
                         ▼
┌─────────────────────────────────────────────────────────────┐
│  SystemManager.Update(World, deltaTime)                     │
└────────────────────────┬────────────────────────────────────┘
                         │
                         ▼
┌─────────────────────────────────────────────────────────────┐
│  NPCBehaviorSystem.Update(World, deltaTime)                 │
│                                                             │
│  1. Query entities with Npc + Behavior + Position          │
│     var query = QueryCache.Get<Npc, Behavior, Position>()  │
│                                                             │
│  2. For each matching entity:                              │
└────────────────────────┬────────────────────────────────────┘
                         │
                         ▼
┌─────────────────────────────────────────────────────────────┐
│  Create ScriptContext for entity                            │
│                                                             │
│  var ctx = new ScriptContext(                               │
│      world: _world,                                         │
│      entity: entity,                                        │
│      logger: _logger,                                       │
│      apis: _scriptingApiProvider                            │
│  );                                                         │
└────────────────────────┬────────────────────────────────────┘
                         │
                         ▼
┌─────────────────────────────────────────────────────────────┐
│  Execute Script                                             │
│                                                             │
│  var script = behavior.ScriptInstance as TypeScriptBase;   │
│  script?.OnTick(ctx, deltaTime);                            │
└────────────────────────┬────────────────────────────────────┘
                         │
                         ▼
┌─────────────────────────────────────────────────────────────┐
│  INSIDE SCRIPT:                                             │
│  public override void OnTick(ScriptContext ctx, float dt)   │
│  {                                                          │
│      // Access APIs through context                        │
│      var pos = ctx.Player.GetPlayerPosition();             │
│      ctx.Npc.FaceEntity(ctx.Entity.Value, playerEntity);   │
│      ctx.Dialogue.ShowMessage("Hello!");                   │
│  }                                                          │
└────────────────────────┬────────────────────────────────────┘
                         │
                         ▼
┌─────────────────────────────────────────────────────────────┐
│  API Services Execute Operations                            │
│                                                             │
│  PlayerApiService.GetPlayerPosition()                       │
│  ├── Query ECS for Player entity                           │
│  ├── Get Position component                                │
│  └── Return Point(x, y)                                    │
│                                                             │
│  NpcApiService.FaceEntity(npc, target)                      │
│  ├── Get Position of both entities                         │
│  ├── Calculate facing direction                            │
│  └── Update GridMovement.FacingDirection                   │
└─────────────────────────────────────────────────────────────┘
```

---

## 5. Component Access Pattern

### How Scripts Access ECS Components

```
┌────────────────────────────────────────────────────────────┐
│                    SCRIPT CODE                             │
│                                                            │
│  public override void OnTick(ScriptContext ctx, float dt)  │
│  {                                                         │
│      // Method 1: Direct component access (type-safe)     │
│      ref var pos = ref ctx.GetState<Position>();          │
│      pos.X += 1;                                           │
│                                                            │
│      // Method 2: Try pattern (safe)                      │
│      if (ctx.TryGetState<Health>(out var health))         │
│      {                                                     │
│          health.Current -= 10;                            │
│      }                                                     │
│                                                            │
│      // Method 3: API service access (recommended)        │
│      ctx.Player.GiveMoney(100);                           │
│      ctx.Npc.MoveNPC(entity, Direction.Up);               │
│  }                                                         │
└────────────────────────┬───────────────────────────────────┘
                         │
                         ▼
┌────────────────────────────────────────────────────────────┐
│                  ScriptContext                             │
│                                                            │
│  // Direct component access                               │
│  public ref T GetState<T>() where T : struct              │
│  {                                                         │
│      return ref World.Get<T>(Entity.Value);               │
│  }                                                         │
│                                                            │
│  // API access (delegates to services)                    │
│  public PlayerApiService Player => _apis.Player;          │
└────────────────────────┬───────────────────────────────────┘
                         │
                         ▼
┌────────────────────────────────────────────────────────────┐
│              API Services (Core)                           │
│                                                            │
│  public class PlayerApiService                             │
│  {                                                         │
│      private readonly World _world;                        │
│                                                            │
│      public void GiveMoney(int amount)                     │
│      {                                                     │
│          var player = GetPlayerEntity();                  │
│          ref var wallet = ref _world.Get<Wallet>(player); │
│          wallet.Balance += amount;                        │
│      }                                                     │
│  }                                                         │
└────────────────────────────────────────────────────────────┘
```

---

## 6. Registration & DI Setup

```
┌─────────────────────────────────────────────────────────────┐
│            ServiceCollectionExtensions.cs                   │
│            (PokeSharp.Game or PokeSharp.Core)               │
│                                                             │
│  public static IServiceCollection AddScripting(...)         │
│  {                                                          │
│      // 1. Register individual API services                │
│      services.AddSingleton<PlayerApiService>();            │
│      services.AddSingleton<NpcApiService>();               │
│      services.AddSingleton<MapApiService>();               │
│      services.AddSingleton<GameStateApiService>();         │
│      services.AddSingleton<DialogueApiService>();          │
│      services.AddSingleton<EffectApiService>();            │
│                                                             │
│      // 2. Register facade (aggregates above services)     │
│      services.AddSingleton<IScriptingApiProvider,          │
│                            ScriptingApiProvider>();        │
│                                                             │
│      // 3. Register scripting infrastructure               │
│      services.AddSingleton<ScriptService>(sp =>            │
│          new ScriptService(                                │
│              scriptsBasePath: "Scripts/",                  │
│              logger: sp.GetRequiredService<ILogger<...>>(),│
│              apis: sp.GetRequiredService<IScriptingApiPr...>()│
│          )                                                 │
│      );                                                     │
│                                                             │
│      // 4. Register systems that use scripts               │
│      services.AddSingleton<NPCBehaviorSystem>();           │
│                                                             │
│      return services;                                       │
│  }                                                          │
└─────────────────────────────────────────────────────────────┘
```

**Dependency Resolution at Runtime**:
```
Container.Resolve<ScriptService>()
    ↓ requires
IScriptingApiProvider
    ↓ resolves to
ScriptingApiProvider
    ↓ requires
[PlayerApiService, NpcApiService, MapApiService, ...]
    ↓ each requires
[World, ILogger]
    ↓ injected automatically
```

---

## 7. Comparison with Other Facades

### Consistent Pattern Across Codebase

```
┌────────────────────────────────────────────────────────────┐
│  IScriptingApiProvider (Phase 3)                           │
│  ├── Player      : PlayerApiService                        │
│  ├── Npc         : NpcApiService                           │
│  ├── Map         : MapApiService                           │
│  ├── GameState   : GameStateApiService                     │
│  ├── Dialogue    : DialogueApiService                      │
│  └── Effects     : EffectApiService                        │
│                                                            │
│  Purpose: Script API access (6 services → 1 provider)     │
└────────────────────────────────────────────────────────────┘

┌────────────────────────────────────────────────────────────┐
│  IGameServicesProvider (Phase 4B)                          │
│  ├── EntityFactory  : IEntityFactoryService                │
│  ├── ScriptService  : ScriptService                        │
│  └── BehaviorRegistry : TypeRegistry<BehaviorDefinition>   │
│                                                            │
│  Purpose: Core game services (3 services → 1 provider)    │
└────────────────────────────────────────────────────────────┘

┌────────────────────────────────────────────────────────────┐
│  ILoggingProvider (Phase 5)                                │
│  ├── Logger        : ILogger                               │
│  └── LoggerFactory : ILoggerFactory                        │
│                                                            │
│  Purpose: Logging infrastructure (2 services → 1 provider) │
└────────────────────────────────────────────────────────────┘

┌────────────────────────────────────────────────────────────┐
│  IInitializationProvider (Phase 7)                         │
│  ├── PerformanceMonitor : PerformanceMonitor               │
│  ├── InputManager       : InputManager                     │
│  └── PlayerFactory      : PlayerFactory                    │
│                                                            │
│  Purpose: Initialization helpers (3 services → 1 provider) │
└────────────────────────────────────────────────────────────┘
```

**Common Theme**: All facades reduce DI complexity and group related services ✅

---

## 8. Why No Circular Dependency

### Project Reference Graph

```
┌─────────────────────────┐
│   PokeSharp.Game        │
│   (Layer 4)             │
└───────────┬─────────────┘
            │ references
            ▼
┌─────────────────────────┐
│  PokeSharp.Scripting    │
│  (Layer 3)              │
└───────────┬─────────────┘
            │ references
            ▼
┌─────────────────────────┐
│   PokeSharp.Core        │
│   (Layer 2)             │
└───────────┬─────────────┘
            │ references
            ▼
┌─────────────────────────┐
│  External Dependencies  │
│  (Layer 1)              │
│  - Arch.Core            │
│  - Microsoft.*          │
│  - MonoGame             │
└─────────────────────────┘
```

**Key Facts**:
1. ✅ PokeSharp.Scripting → references → PokeSharp.Core (allowed)
2. ✅ PokeSharp.Core → does NOT reference → PokeSharp.Scripting (no reverse)
3. ✅ All dependencies point downward (proper layering)
4. ✅ Interface and implementation both in Core (no split)
5. ✅ Scripting depends on abstraction (IScriptingApiProvider), not concrete types

**Conclusion**: No circular dependency possible with this structure ✅

---

## 9. Alternative (Wrong) Architectures

### ❌ Anti-Pattern 1: Circular Reference

```
┌─────────────────────┐
│  PokeSharp.Core     │  ◄─────┐
└──────────┬──────────┘        │ BAD!
           │ references        │
           ▼                   │
┌─────────────────────┐        │
│ PokeSharp.Scripting │────────┘
└─────────────────────┘  references

Result: Compilation error! ❌
```

### ❌ Anti-Pattern 2: God Object

```
┌────────────────────────────────┐
│  ScriptService                 │
│                                │
│  + GetMoney()                  │
│  + MoveNPC()                   │
│  + TransitionMap()             │
│  + SetFlag()                   │
│  + ShowDialogue()              │
│  + ... 50 more methods         │
└────────────────────────────────┘

Problems:
- Violates Single Responsibility ❌
- Hard to test ❌
- Hard to extend ❌
- Tight coupling ❌
```

### ✅ Current Architecture (Correct)

```
┌────────────────┐
│ ScriptService  │
│                │
│ + LoadScript() │  ◄── Focused responsibility
│ + ExecuteScript│  ◄── Only scripting logic
│ + ClearCache() │
└───────┬────────┘
        │ uses
        ▼
┌───────────────────────┐
│ IScriptingApiProvider │  ◄── Facade for domain APIs
│                       │
│ + Player              │
│ + Npc                 │
│ + Map                 │
│ + GameState           │
│ + Dialogue            │
│ + Effects             │
└───────┬───────────────┘
        │ implemented by
        ▼
┌───────────────────────┐
│ 6 separate API        │  ◄── Each has focused responsibility
│ service classes       │
└───────────────────────┘

Benefits:
- Single Responsibility ✅
- Easy to test ✅
- Easy to extend ✅
- Loose coupling ✅
```

---

## Summary

### Architecture Strengths
1. ✅ **Clean Layering**: Core → Scripting → Game
2. ✅ **No Circular Dependencies**: One-way dependencies only
3. ✅ **Facade Pattern**: Consistent with rest of codebase
4. ✅ **Dependency Inversion**: Depends on abstractions
5. ✅ **Single Responsibility**: Each service has focused purpose
6. ✅ **Testability**: Easy to mock interfaces
7. ✅ **Extensibility**: Can add APIs without changing ScriptService

### Key Takeaway
**The existing architecture is optimal and requires no changes.** ✅

---

**Document Version**: 1.0
**Last Updated**: 2024-01-10
**Status**: COMPLETE
