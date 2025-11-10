# Scripting Architecture Research

## Executive Summary

**FINDING**: The PokeSharp codebase has a **well-designed, layered scripting abstraction** that follows the **Facade Pattern** consistently. The circular dependency concern raised is actually a **non-issue** because the architecture is already properly abstracted.

**CONCLUSION**: No changes needed. `ScriptService` in `PokeSharp.Scripting` properly depends on `IScriptingApiProvider` from Core, which is the correct abstraction layer.

---

## Architecture Overview

### Layer Hierarchy (Bottom → Top)

```
Layer 1: External Dependencies
├── Arch.Core (ECS framework)
├── Microsoft.Extensions.Logging (logging)
└── MonoGame (graphics/input)

Layer 2: PokeSharp.Core (Domain Logic)
├── Components/ (ECS components)
├── Systems/ (ECS systems)
├── Types/ (type definitions)
├── ScriptingApi/ (API interfaces)
└── Scripting/Services/ (API implementations)

Layer 3: PokeSharp.Scripting (Scripting Infrastructure)
├── Runtime/ (TypeScriptBase, ScriptContext)
├── Services/ (ScriptService)
├── Compilation/ (Roslyn compiler)
└── HotReload/ (hot-reload system)

Layer 4: PokeSharp.Game (Application Orchestration)
├── Services/ (Game-specific facades)
├── Systems/ (Game-specific systems)
└── Initialization/ (startup/wiring)
```

---

## Scripting Abstraction Analysis

### 1. Interface Layer (Core)

**Location**: `/PokeSharp.Core/ScriptingApi/`

```csharp
// Master facade interface
public interface IScriptingApiProvider
{
    PlayerApiService Player { get; }
    NpcApiService Npc { get; }
    MapApiService Map { get; }
    GameStateApiService GameState { get; }
    DialogueApiService Dialogue { get; }
    EffectApiService Effects { get; }
}

// Individual API interfaces (all in Core)
public interface IPlayerApi { ... }
public interface INPCApi { ... }
public interface IMapApi { ... }
public interface IGameStateApi { ... }
public interface IDialogueApi { ... }
public interface IEffectApi { ... }
```

**Design Pattern**: **Facade Pattern**
- Groups 6 domain-specific APIs into one provider
- Reduces DI parameter count from 6+ to 1
- All interfaces defined in Core (no Game dependency)

**Purpose**:
- Provides clean abstraction boundary for scripts
- Scripts depend ONLY on Core interfaces
- Implementation details hidden behind interface

---

### 2. Implementation Layer (Core)

**Location**: `/PokeSharp.Core/Scripting/Services/`

```csharp
// Master facade implementation
public class ScriptingApiProvider(
    PlayerApiService playerApi,
    NpcApiService npcApi,
    MapApiService mapApi,
    GameStateApiService gameStateApi,
    DialogueApiService dialogueApi,
    EffectApiService effectApi
) : IScriptingApiProvider
{
    public PlayerApiService Player => _playerApi;
    public NpcApiService Npc => _npcApi;
    // ... other API services
}

// Individual API service implementations (all in Core)
public class PlayerApiService(World world, ILogger logger) : IPlayerApi
{
    // Direct ECS operations on Core components
    public int GetMoney() { ... }
    public void GiveMoney(int amount) { ... }
}

public class NpcApiService(World world, ILogger logger) : INPCApi
{
    // Direct ECS operations on Core components
    public void MoveNPC(Entity npc, Direction dir) { ... }
    public void FaceEntity(Entity npc, Entity target) { ... }
}
```

**Key Observations**:
1. ✅ All implementations in Core layer
2. ✅ Operate directly on Core components (Position, GridMovement, Wallet, etc.)
3. ✅ Take only `World` and `ILogger` dependencies
4. ✅ Zero Game-layer dependencies
5. ✅ Pure domain logic (no MonoGame, no rendering)

---

### 3. Script Runtime Layer (PokeSharp.Scripting)

**Location**: `/PokeSharp.Scripting/Runtime/`

#### A. Base Class for Scripts

```csharp
public abstract class TypeScriptBase
{
    // Lifecycle hooks
    public virtual void OnInitialize(ScriptContext ctx) { }
    public virtual void OnActivated(ScriptContext ctx) { }
    public virtual void OnTick(ScriptContext ctx, float deltaTime) { }
    public virtual void OnDeactivated(ScriptContext ctx) { }
}
```

**Purpose**: Base class that all .csx scripts inherit from

#### B. Script Execution Context

```csharp
public sealed class ScriptContext
{
    // Core properties
    public World World { get; }
    public Entity? Entity { get; }
    public ILogger Logger { get; }

    // API access (via facade)
    public PlayerApiService Player => _apis.Player;
    public NpcApiService Npc => _apis.Npc;
    public MapApiService Map => _apis.Map;
    public GameStateApiService GameState => _apis.GameState;
    public DialogueApiService Dialogue => _apis.Dialogue;
    public EffectApiService Effects => _apis.Effects;

    // Type-safe component access
    public ref T GetState<T>() where T : struct { ... }
    public bool TryGetState<T>(out T state) where T : struct { ... }
    public ref T GetOrAddState<T>() where T : struct { ... }
}

// Constructor
public ScriptContext(
    World world,
    Entity? entity,
    ILogger logger,
    IScriptingApiProvider apis  // ← Dependency on Core interface
)
```

**Key Design Decisions**:
1. ✅ Depends on `IScriptingApiProvider` (Core interface)
2. ✅ Provides unified context to scripts
3. ✅ Encapsulates ECS `World` access
4. ✅ Type-safe component operations
5. ✅ No Game-layer dependencies

---

### 4. Script Compilation & Execution (PokeSharp.Scripting)

**Location**: `/PokeSharp.Scripting/Services/ScriptService.cs`

```csharp
public class ScriptService : IAsyncDisposable
{
    private readonly IScriptingApiProvider _apis;  // ← Core interface
    private readonly ILogger<ScriptService> _logger;
    private readonly string _scriptsBasePath;

    // Script caching
    private readonly ConcurrentDictionary<string, (Script<object>, Type?)> _scriptCache;
    private readonly ConcurrentDictionary<string, object> _scriptInstances;

    public ScriptService(
        string scriptsBasePath,
        ILogger<ScriptService> logger,
        IScriptingApiProvider apis  // ← Depends on Core abstraction
    )
    {
        _scriptsBasePath = scriptsBasePath;
        _logger = logger;
        _apis = apis;
    }

    public async Task<object?> LoadScriptAsync(string scriptPath) { ... }
    public async Task ExecuteScriptAsync(...) { ... }
    public void ClearCache() { ... }
}
```

**Dependencies**:
1. ✅ `IScriptingApiProvider` (Core interface)
2. ✅ `ILogger` (Microsoft.Extensions.Logging)
3. ✅ No Game-layer dependencies
4. ✅ No circular dependencies

---

## Dependency Flow Analysis

### Current Architecture (Correct ✅)

```
Script Files (.csx)
    ↓ inherits
TypeScriptBase (PokeSharp.Scripting)
    ↓ receives
ScriptContext (PokeSharp.Scripting)
    ↓ depends on
IScriptingApiProvider (PokeSharp.Core/ScriptingApi/)
    ↓ implemented by
ScriptingApiProvider (PokeSharp.Core/Scripting/Services/)
    ↓ uses
API Service Implementations (PokeSharp.Core/Scripting/Services/)
    ↓ operates on
ECS Components (PokeSharp.Core/Components/)
```

**Result**: **Zero circular dependencies** ✅

---

## Comparison with Existing Facade Patterns

The codebase consistently uses the **Facade Pattern** for grouping related services:

### Pattern 1: IScriptingApiProvider (Phase 3)
```csharp
// PokeSharp.Core/ScriptingApi/
public interface IScriptingApiProvider
{
    PlayerApiService Player { get; }
    NpcApiService Npc { get; }
    MapApiService Map { get; }
    // ... 6 total services
}
```
**Purpose**: Groups 6 domain-specific APIs for script access

### Pattern 2: IGameServicesProvider (Phase 4B)
```csharp
// PokeSharp.Game/Services/
public interface IGameServicesProvider
{
    IEntityFactoryService EntityFactory { get; }
    ScriptService ScriptService { get; }
    TypeRegistry<BehaviorDefinition> BehaviorRegistry { get; }
}
```
**Purpose**: Groups core game services for initialization

### Pattern 3: ILoggingProvider (Phase 5)
```csharp
// PokeSharp.Game/Services/
public interface ILoggingProvider
{
    ILogger Logger { get; }
    ILoggerFactory LoggerFactory { get; }
}
```
**Purpose**: Groups logging infrastructure

### Pattern 4: IInitializationProvider (Phase 7)
```csharp
// PokeSharp.Game/Services/
public interface IInitializationProvider
{
    PerformanceMonitor PerformanceMonitor { get; }
    InputManager InputManager { get; }
    PlayerFactory PlayerFactory { get; }
}
```
**Purpose**: Groups initialization helpers

**Common Theme**: All facades reduce DI complexity by grouping related services

---

## ScriptService Design Rationale

### Why ScriptService Depends on IScriptingApiProvider

```csharp
public class ScriptService
{
    private readonly IScriptingApiProvider _apis;

    public async Task ExecuteScriptAsync(...)
    {
        // Creates context for script execution
        var context = new ScriptContext(
            world: _world,
            entity: entity,
            logger: _logger,
            apis: _apis  // ← Passes APIs to script context
        );

        // Script receives context and can access APIs
        script.OnTick(context, deltaTime);
    }
}
```

**Design Benefits**:
1. ✅ **Single Responsibility**: ScriptService compiles/executes, APIs provide domain logic
2. ✅ **Dependency Inversion**: Depends on Core interface, not Game implementation
3. ✅ **Testability**: Can mock `IScriptingApiProvider` for testing
4. ✅ **Extensibility**: Can add new APIs without changing ScriptService
5. ✅ **Clean Separation**: Scripting infrastructure separate from domain APIs

---

## Why This is NOT a Circular Dependency

### Misconception
> "ScriptService (PokeSharp.Scripting) depends on IScriptingApiProvider (Core), which might create a circular dependency."

### Reality

```
PokeSharp.Scripting
    ↓ depends on (interface)
PokeSharp.Core/ScriptingApi/IScriptingApiProvider
    ↓ implemented in
PokeSharp.Core/Scripting/Services/ScriptingApiProvider
    ↓ uses
PokeSharp.Core/Scripting/Services/*ApiService
    ↓ operates on
PokeSharp.Core/Components/*
```

**Key Points**:
1. ✅ `PokeSharp.Scripting` references `PokeSharp.Core` (allowed, correct direction)
2. ✅ `PokeSharp.Core` does NOT reference `PokeSharp.Scripting` (no reverse dependency)
3. ✅ Interface defined in Core, implementation also in Core
4. ✅ Scripting layer depends on abstraction (IScriptingApiProvider), not concrete implementation
5. ✅ This is **Dependency Inversion Principle** in action

---

## Project Reference Analysis

### PokeSharp.Core.csproj
```xml
<Project Sdk="Microsoft.NET.Sdk">
  <ItemGroup>
    <!-- External dependencies only -->
    <PackageReference Include="Arch.Core" />
    <PackageReference Include="Microsoft.Extensions.Logging" />
    <!-- NO reference to PokeSharp.Scripting -->
  </ItemGroup>
</Project>
```

### PokeSharp.Scripting.csproj
```xml
<Project Sdk="Microsoft.NET.Sdk">
  <ItemGroup>
    <ProjectReference Include="..\PokeSharp.Core\PokeSharp.Core.csproj" />
    <PackageReference Include="Microsoft.CodeAnalysis.CSharp.Scripting" />
  </ItemGroup>
</Project>
```

**Result**: One-way dependency (Scripting → Core), no circular reference ✅

---

## Existing Patterns in Codebase

### Pattern: Service Layer Depends on API Provider

This pattern is used throughout the codebase:

#### Example 1: NPCBehaviorSystem
```csharp
// PokeSharp.Core/Systems/NPCBehaviorSystem.cs
public class NPCBehaviorSystem : BaseSystem
{
    private readonly IScriptingApiProvider _apis;  // ← Core interface

    public NPCBehaviorSystem(
        ILogger logger,
        IScriptingApiProvider apis  // ← Injected
    )
}
```

#### Example 2: ScriptService (this file)
```csharp
// PokeSharp.Scripting/Services/ScriptService.cs
public class ScriptService
{
    private readonly IScriptingApiProvider _apis;  // ← Core interface

    public ScriptService(
        string scriptsBasePath,
        ILogger logger,
        IScriptingApiProvider apis  // ← Injected
    )
}
```

**Pattern Consistency**: Multiple services in different layers depend on `IScriptingApiProvider` from Core ✅

---

## Architecture Strengths

### 1. Clean Layering ✅
- Core: Domain logic and abstractions
- Scripting: Infrastructure for executing scripts
- Game: Orchestration and wiring
- No layer depends on a higher layer

### 2. Dependency Inversion ✅
```csharp
// High-level module (ScriptService)
class ScriptService
{
    private readonly IScriptingApiProvider _apis;  // ← Depends on abstraction
}

// Abstraction (defined in Core)
interface IScriptingApiProvider { ... }

// Low-level module (implementations in Core)
class ScriptingApiProvider : IScriptingApiProvider { ... }
class PlayerApiService : IPlayerApi { ... }
```

### 3. Facade Pattern ✅
- Simplifies complex subsystems
- Reduces coupling
- Provides unified interface
- Consistent pattern across codebase

### 4. Testability ✅
```csharp
// Can mock IScriptingApiProvider for testing
var mockApis = new Mock<IScriptingApiProvider>();
var scriptService = new ScriptService(path, logger, mockApis.Object);
```

### 5. Extensibility ✅
- Can add new APIs to `IScriptingApiProvider` without changing ScriptService
- Can swap implementations without changing dependents
- Scripts automatically get access to new APIs

---

## Common Abstraction Strategies (Not Needed Here)

For reference, here are abstraction strategies IF there was a circular dependency:

### Strategy 1: Interface Extraction
```csharp
// Move interface to lower layer
// PokeSharp.Core/Services/IScriptService.cs
public interface IScriptService
{
    Task<object?> LoadScriptAsync(string path);
}

// Implementation stays in higher layer
// PokeSharp.Scripting/Services/ScriptService.cs
public class ScriptService : IScriptService { ... }
```

### Strategy 2: Provider Pattern
```csharp
// Provider interface in Core
public interface IScriptProvider
{
    Task<object?> ExecuteScript(string path, ScriptContext ctx);
}

// Implementation in Scripting layer
public class RoslynScriptProvider : IScriptProvider { ... }
```

### Strategy 3: Dependency Injection Container
```csharp
// Register at app startup
services.AddSingleton<IScriptingApiProvider, ScriptingApiProvider>();
services.AddSingleton<ScriptService>();
// Container resolves dependencies at runtime
```

**BUT**: None of these are needed because the architecture is already correct! ✅

---

## Recommended Action

### For Circular Dependency Concern: **NO ACTION NEEDED**

The concern about circular dependencies is **unfounded**. The architecture already follows best practices:

1. ✅ Proper layer separation (Core → Scripting → Game)
2. ✅ Dependency Inversion (depends on abstractions)
3. ✅ Facade Pattern (consistent with rest of codebase)
4. ✅ No circular references in project files
5. ✅ Clean dependency flow (all dependencies point downward)

### For Script API Access: **CURRENT DESIGN IS OPTIMAL**

Scripts access game APIs through `ScriptContext`, which receives `IScriptingApiProvider`:

```csharp
// Script file (.csx)
public class MyScript : TypeScriptBase
{
    protected override void OnTick(ScriptContext ctx, float deltaTime)
    {
        // Clean, intuitive API access
        var playerMoney = ctx.Player.GetMoney();
        ctx.Dialogue.ShowMessage("You have: " + playerMoney);
        ctx.Npc.FaceEntity(npcEntity, playerEntity);
        ctx.Map.TransitionToMap(2, 10, 10);
    }
}
```

**Benefits**:
- ✅ Scripts depend only on Core abstractions
- ✅ Intellisense-friendly API discovery
- ✅ Type-safe API access
- ✅ Consistent with ECS architecture
- ✅ Easy to test and mock

---

## Architecture Diagram

```
┌─────────────────────────────────────────────────────────────────┐
│ Script Files (.csx)                                             │
│ public class MyScript : TypeScriptBase { ... }                  │
└────────────────────────────┬────────────────────────────────────┘
                             │ inherits
┌────────────────────────────▼────────────────────────────────────┐
│ PokeSharp.Scripting                                             │
│ ├── Runtime/TypeScriptBase                                      │
│ ├── Runtime/ScriptContext                                       │
│ └── Services/ScriptService                                      │
└────────────────────────────┬────────────────────────────────────┘
                             │ depends on
┌────────────────────────────▼────────────────────────────────────┐
│ PokeSharp.Core/ScriptingApi/IScriptingApiProvider               │
│ (Interface - Abstraction Layer)                                 │
└────────────────────────────┬────────────────────────────────────┘
                             │ implemented by
┌────────────────────────────▼────────────────────────────────────┐
│ PokeSharp.Core/Scripting/Services/                              │
│ ├── ScriptingApiProvider (Facade)                               │
│ ├── PlayerApiService                                            │
│ ├── NpcApiService                                               │
│ ├── MapApiService                                               │
│ ├── GameStateApiService                                         │
│ ├── DialogueApiService                                          │
│ └── EffectApiService                                            │
└────────────────────────────┬────────────────────────────────────┘
                             │ operates on
┌────────────────────────────▼────────────────────────────────────┐
│ PokeSharp.Core/Components/                                      │
│ ├── Movement/ (Position, GridMovement, Direction)               │
│ ├── NPCs/ (Npc, Behavior, MovementRoute)                        │
│ ├── Player/ (Player, Wallet)                                    │
│ └── Common/ (Name, Health)                                      │
└─────────────────────────────────────────────────────────────────┘
```

**Flow**: Scripts → Runtime → API Provider (interface) → API Services → Components

**No circular dependencies**: All arrows point downward ✅

---

## Script Example Usage

### Example Script (.csx)
```csharp
// Scripts/behaviors/patrol_guard.csx
using PokeSharp.Scripting.Runtime;

public class PatrolGuard : TypeScriptBase
{
    public override void OnTick(ScriptContext ctx, float deltaTime)
    {
        // Access APIs through context
        var player = ctx.Player.GetPlayerPosition();
        var npc = ctx.Npc.GetNPCPosition(ctx.Entity.Value);

        // Calculate distance
        var distance = Math.Abs(player.X - npc.X) + Math.Abs(player.Y - npc.Y);

        if (distance < 5)
        {
            // Face player when nearby
            ctx.Npc.FaceEntity(ctx.Entity.Value, playerEntity);
            ctx.Dialogue.ShowMessage("Hey! Stop right there!");
            ctx.Player.SetPlayerMovementLocked(true);
        }
        else
        {
            // Resume patrol
            if (!ctx.Npc.IsNPCMoving(ctx.Entity.Value))
            {
                ctx.Npc.ResumeNPCPath(ctx.Entity.Value, waitTime: 1.0f);
            }
        }
    }
}
```

**Key Observations**:
1. ✅ Script only references `ScriptContext` and `TypeScriptBase`
2. ✅ All game APIs accessed through `ctx.Player`, `ctx.Npc`, etc.
3. ✅ Type-safe, Intellisense-friendly
4. ✅ No direct ECS manipulation (encapsulated by API services)
5. ✅ Clean, readable, maintainable

---

## Integration with ECS Systems

### How Scripts Are Executed

```csharp
// PokeSharp.Core/Systems/NPCBehaviorSystem.cs
public class NPCBehaviorSystem : BaseSystem, IUpdateSystem
{
    private readonly IScriptingApiProvider _apis;

    public override void Update(World world, float deltaTime)
    {
        var query = QueryCache.Get<Npc, Behavior, Position>();

        world.Query(in query, (Entity entity, ref Behavior behavior) =>
        {
            if (behavior.ScriptInstance != null)
            {
                // Create context for this entity
                var ctx = new ScriptContext(world, entity, _logger, _apis);

                // Execute script's OnTick
                var script = behavior.ScriptInstance as TypeScriptBase;
                script?.OnTick(ctx, deltaTime);
            }
        });
    }
}
```

**Flow**:
1. System queries entities with Behavior component
2. Creates `ScriptContext` for each entity
3. Passes context to script's `OnTick`
4. Script uses APIs via context
5. APIs manipulate ECS components
6. System continues processing

**Clean Separation**: System orchestrates, script implements behavior, APIs provide domain operations ✅

---

## Conclusion

### Summary of Findings

1. **No Circular Dependencies**: The architecture has proper one-way dependencies (Scripting → Core)
2. **Correct Abstraction**: `IScriptingApiProvider` in Core provides clean interface boundary
3. **Consistent Pattern**: Facade pattern used consistently across codebase
4. **Optimal Design**: Scripts depend only on Core abstractions, not Game implementation
5. **Well-Architected**: Follows SOLID principles (SRP, DIP, ISP)

### Recommended Actions

**NONE** - The existing architecture is optimal and requires no changes.

### Key Takeaways

- ✅ `ScriptService` depending on `IScriptingApiProvider` is **correct design**
- ✅ No abstraction layer needed (already has one)
- ✅ No circular dependency exists or is possible with current structure
- ✅ Pattern matches rest of codebase (`IGameServicesProvider`, `ILoggingProvider`, etc.)
- ✅ Architecture enables testability, extensibility, and maintainability

---

## References

### Files Analyzed
1. `/PokeSharp.Core/ScriptingApi/IScriptingApiProvider.cs`
2. `/PokeSharp.Core/Scripting/Services/ScriptingApiProvider.cs`
3. `/PokeSharp.Core/Scripting/Services/PlayerApiService.cs`
4. `/PokeSharp.Core/Scripting/Services/NpcApiService.cs`
5. `/PokeSharp.Scripting/Runtime/TypeScriptBase.cs`
6. `/PokeSharp.Scripting/Runtime/ScriptContext.cs`
7. `/PokeSharp.Scripting/Services/ScriptService.cs`
8. `/PokeSharp.Game/Services/IGameServicesProvider.cs`
9. `/PokeSharp.Core/Systems/NPCBehaviorSystem.cs` (referenced)

### Architecture Documents
1. `docs/analysis/npc-behavior-system-circular-dependency-analysis.md`
2. `docs/migration/NPCBehaviorSystem-Migration-Plan.md`
3. `docs/architecture/late-registration-*.md`

### Design Patterns Applied
1. **Facade Pattern**: `IScriptingApiProvider`, `IGameServicesProvider`, etc.
2. **Dependency Inversion Principle**: Services depend on Core interfaces
3. **Strategy Pattern**: `TypeScriptBase` allows different script implementations
4. **Service Locator Pattern**: `ScriptContext` provides unified access to services

---

**Research completed**: 2024-01-10
**Researcher**: AI Research Agent
**Status**: COMPLETE ✅
