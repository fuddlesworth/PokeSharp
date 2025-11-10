# NPCBehaviorSystem Circular Dependency Analysis

## Executive Summary

**✅ SAFE TO MOVE TO CORE** - NPCBehaviorSystem has **zero circular dependencies** and can be safely moved to `PokeSharp.Core.Systems`.

## Dependency Analysis

### 1. Core Dependencies (All from Core) ✅

**File: `/PokeSharp.Game/Systems/NpcBehaviorSystem.cs`**

```csharp
using System.Collections.Concurrent;
using Arch.Core;                                    // ✅ External (ECS)
using Microsoft.Extensions.Logging;                 // ✅ External (DI)
using PokeSharp.Core.Components.Movement;           // ✅ Core component
using PokeSharp.Core.Components.NPCs;               // ✅ Core component
using PokeSharp.Core.Logging;                       // ✅ Core infrastructure
using PokeSharp.Core.Scripting.Services;            // ✅ Core service
using PokeSharp.Core.ScriptingApi;                  // ✅ Core API
using PokeSharp.Core.Systems;                       // ✅ Core base classes
using PokeSharp.Core.Types;                         // ✅ Core types
using PokeSharp.Scripting.Runtime;                  // ✅ External scripting
```

**Namespace:**
```csharp
namespace PokeSharp.Game.Systems;  // ❌ Wrong layer - should be Core
```

### 2. Game-Layer Dependencies: NONE ✅

**No `using PokeSharp.Game` statements found in NPCBehaviorSystem.cs**

- ✅ No Game-specific types referenced
- ✅ No Game-layer services used
- ✅ No MonoGame dependencies
- ✅ No rendering/graphics code

### 3. Base Classes (All from Core) ✅

```csharp
public class NPCBehaviorSystem : BaseSystem, IUpdateSystem
```

- **BaseSystem**: `/PokeSharp.Core/Systems/BaseSystem.cs` ✅
- **IUpdateSystem**: `/PokeSharp.Core/Systems/IUpdateSystem.cs` ✅

### 4. Components Used (All from Core) ✅

```csharp
var query = QueryCache.Get<Npc, Behavior, Position>();
```

- **Npc**: `PokeSharp.Core.Components.NPCs.Npc` ✅
- **Behavior**: `PokeSharp.Core.Components.NPCs.Behavior` ✅
- **Position**: `PokeSharp.Core.Components.Movement.Position` ✅

### 5. Services Used (All from Core) ✅

```csharp
private readonly IScriptingApiProvider _apis;
private TypeRegistry<BehaviorDefinition>? _behaviorRegistry;
```

- **IScriptingApiProvider**: `PokeSharp.Core.ScriptingApi.IScriptingApiProvider` ✅
- **TypeRegistry<T>**: `PokeSharp.Core.Types.TypeRegistry<T>` ✅
- **BehaviorDefinition**: `PokeSharp.Core.Types.BehaviorDefinition` ✅

### 6. Initialization Dependencies Analysis

**Current Initializer: `/PokeSharp.Game/Initialization/NPCBehaviorInitializer.cs`**

```csharp
using PokeSharp.Game.Services;      // ⚠️ Game-layer dependency
using PokeSharp.Game.Systems;       // ⚠️ Game-layer dependency
```

**Dependencies:**
- **IGameServicesProvider** (Game layer) ⚠️
  - Provides: `EntityFactory`, `ScriptService`, `BehaviorRegistry`
  - **Solution**: Pass these services directly via DI instead of facade

- **SystemManager** (Core) ✅
- **World** (Arch.Core) ✅
- **IScriptingApiProvider** (Core) ✅

## Circular Dependency Check

### Current Dependency Flow:

```
PokeSharp.Game (Layer 3)
    ↓ references
PokeSharp.Core (Layer 2)
    ↓ references
Arch.Core (External ECS)
```

### After Moving NPCBehaviorSystem to Core:

```
PokeSharp.Core.Systems.NPCBehaviorSystem
    ↓ depends on
PokeSharp.Core.Components.*      ✅ Same layer
PokeSharp.Core.Systems.*         ✅ Same layer
PokeSharp.Core.ScriptingApi.*    ✅ Same layer
PokeSharp.Core.Types.*           ✅ Same layer
Arch.Core                        ✅ External
```

**Result: No circular dependencies - all dependencies point downward ✅**

## MonoGame Dependency Check

```bash
grep -r "MonoGame" NpcBehaviorSystem.cs
# Result: No MonoGame references found ✅
```

- ✅ No `Microsoft.Xna.Framework` namespaces
- ✅ No `SpriteBatch`, `Texture2D`, or graphics types
- ✅ No `GameTime` or rendering logic
- ✅ Pure ECS logic system

## Refactoring Required

### 1. Move System File ✅ SIMPLE

**From:**
```
/PokeSharp.Game/Systems/NpcBehaviorSystem.cs
```

**To:**
```
/PokeSharp.Core/Systems/NPCBehaviorSystem.cs
```

**Change namespace:**
```csharp
// Before
namespace PokeSharp.Game.Systems;

// After
namespace PokeSharp.Core.Systems;
```

### 2. Update Initializer ⚠️ REQUIRES REFACTOR

**Current Problem:**
```csharp
public class NPCBehaviorInitializer(
    IGameServicesProvider gameServices,  // ❌ Game-layer facade
    ...
)
{
    gameServices.BehaviorRegistry.LoadAllAsync();
    gameServices.ScriptService.LoadScriptAsync();
}
```

**Solution - Direct DI:**
```csharp
public class NPCBehaviorInitializer(
    ILogger<NPCBehaviorInitializer> logger,
    ILoggerFactory loggerFactory,
    World world,
    SystemManager systemManager,
    IScriptingApiProvider apiProvider,
    TypeRegistry<BehaviorDefinition> behaviorRegistry,  // ✅ Direct injection
    ScriptService scriptService                         // ✅ Direct injection
)
{
    behaviorRegistry.LoadAllAsync();
    scriptService.LoadScriptAsync();
}
```

**Benefits:**
- ✅ No Game-layer dependency
- ✅ Could move initializer to Core as well
- ✅ Clearer dependency graph
- ✅ Better testability

### 3. Update Registration Code

**Current (in PokeSharp.Game):**
```csharp
// ServiceCollectionExtensions.cs
services.AddSingleton<IGameServicesProvider, GameServicesProvider>();
```

**After Move:**
```csharp
// Core registration (PokeSharp.Core or DI setup)
services.AddSingleton<TypeRegistry<BehaviorDefinition>>();
services.AddSingleton<ScriptService>();
services.AddSingleton<NPCBehaviorSystem>();  // Now in Core
```

## Move Safety Checklist

- [x] No Game-layer type references
- [x] No MonoGame dependencies
- [x] All base classes in Core
- [x] All components in Core
- [x] All interfaces in Core
- [x] No rendering/graphics code
- [x] No circular dependency risk
- [x] Follows ECS architecture (systems in Core)
- [x] Initialization pattern can be refactored
- [x] DI registration can be updated

## Recommended Migration Steps

### Phase 1: Move System (Zero Risk)
1. Create `/PokeSharp.Core/Systems/NPCBehaviorSystem.cs`
2. Copy content from Game layer
3. Update namespace: `PokeSharp.Game.Systems` → `PokeSharp.Core.Systems`
4. No other changes needed to the system itself ✅

### Phase 2: Update Initializer (Low Risk)
1. Update `NPCBehaviorInitializer` to use direct DI
2. Remove `IGameServicesProvider` dependency
3. Inject `TypeRegistry<BehaviorDefinition>` and `ScriptService` directly
4. Update registration in `ServiceCollectionExtensions.cs`

### Phase 3: Cleanup (Zero Risk)
1. Remove old `/PokeSharp.Game/Systems/NpcBehaviorSystem.cs`
2. Update any import statements in Game layer
3. Verify no broken references

### Phase 4: Consider Moving Initializer (Optional)
If initializer no longer has Game dependencies after Phase 2:
- Could move to `/PokeSharp.Core/Initialization/`
- Would make entire NPC behavior system Core-only ✅

## Architecture Validation

### Before (Current State)
```
PokeSharp.Game/
  ├── Systems/
  │   └── NpcBehaviorSystem.cs        ❌ Wrong layer
  └── Initialization/
      └── NPCBehaviorInitializer.cs   ⚠️ Uses Game facade
```

### After (Correct Architecture)
```
PokeSharp.Core/
  ├── Systems/
  │   └── NPCBehaviorSystem.cs        ✅ Correct layer
  └── Initialization/
      └── NPCBehaviorInitializer.cs   ✅ Direct DI (optional move)
```

## Conclusion

**✅ ZERO CIRCULAR DEPENDENCIES FOUND**

**NPCBehaviorSystem is a pure ECS system that:**
- Operates only on Core components (Npc, Behavior, Position)
- Uses only Core services (IScriptingApiProvider, TypeRegistry)
- Inherits from Core base classes (BaseSystem, IUpdateSystem)
- Has no Game-layer or MonoGame dependencies
- **Belongs in Core by design**

**The move is:**
- ✅ Safe
- ✅ Architecturally correct
- ✅ Zero risk of circular dependencies
- ✅ Recommended as part of proper layering

**Only consideration:** Update the initializer to use direct DI instead of the Game-layer `IGameServicesProvider` facade, which is good architecture anyway.

## Next Steps

1. **Move NPCBehaviorSystem to Core** (5 minutes, zero risk)
2. **Refactor NPCBehaviorInitializer DI** (10 minutes, low risk)
3. **Update registrations** (5 minutes, low risk)
4. **Remove old file** (1 minute, zero risk)

**Total effort: ~20 minutes for proper architecture ✅**
