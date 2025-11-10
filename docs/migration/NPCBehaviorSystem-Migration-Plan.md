# NPCBehaviorSystem Migration Plan: Game → Core

## Executive Summary

This document outlines the step-by-step migration plan for moving `NPCBehaviorSystem` from `PokeSharp.Game` to `PokeSharp.Core`. The migration ensures proper separation of concerns while maintaining dependency injection patterns and avoiding breaking builds.

---

## 1. Current State Analysis

### Files to Migrate
- **Source**: `/Users/ntomsic/Documents/PokeSharp/PokeSharp.Game/Systems/NpcBehaviorSystem.cs`
- **Destination**: `/Users/ntomsic/Documents/PokeSharp/PokeSharp.Core/Systems/NPCBehaviorSystem.cs`

### Files to Evaluate (Stay or Move?)
- **NPCBehaviorInitializer**: `/Users/ntomsic/Documents/PokeSharp/PokeSharp.Game/Initialization/NPCBehaviorInitializer.cs`
  - **Decision**: **STAY in Game layer** (see rationale below)

### Dependencies Analysis

#### NPCBehaviorSystem Dependencies
```csharp
// Core dependencies (already in Core)
using Arch.Core;
using PokeSharp.Core.Components.Movement;
using PokeSharp.Core.Components.NPCs;
using PokeSharp.Core.Logging;
using PokeSharp.Core.Systems;
using PokeSharp.Core.Types;

// Scripting dependencies (PokeSharp.Scripting project)
using PokeSharp.Core.Scripting.Services;  // IScriptingApiProvider interface (Core)
using PokeSharp.Core.ScriptingApi;        // ScriptContext (Core)
using PokeSharp.Scripting.Runtime;        // TypeScriptBase (Scripting project)
```

**Verdict**: ✅ All dependencies are satisfied in Core layer (Scripting project is referenced by Core)

#### NPCBehaviorInitializer Dependencies
```csharp
// Core dependencies
using Arch.Core;
using PokeSharp.Core.Systems;
using PokeSharp.Core.Types;
using PokeSharp.Core.ScriptingApi;

// Game-layer dependencies
using PokeSharp.Game.Services;  // IGameServicesProvider (Game layer)
using PokeSharp.Game.Systems;   // NPCBehaviorSystem reference
```

**Verdict**: ⚠️ **Cannot move to Core** - depends on `IGameServicesProvider` which is a Game-layer facade

---

## 2. Design Decisions

### Decision 1: System Location
**Where should NPCBehaviorSystem reside?**

✅ **CORE LAYER** (`PokeSharp.Core/Systems/`)

**Rationale**:
- Behavior execution is core ECS functionality
- No MonoGame/rendering dependencies
- Operates on pure ECS components (Npc, Behavior, Position)
- Uses Core scripting interfaces (`IScriptingApiProvider`, `ScriptContext`)
- Follows pattern established by other systems (MovementSystem, CollisionSystem, PathfindingSystem)

### Decision 2: Initializer Location
**Should NPCBehaviorInitializer move to Core?**

❌ **STAY IN GAME LAYER** (`PokeSharp.Game/Initialization/`)

**Rationale**:
- Depends on `IGameServicesProvider` (Game-layer facade)
- Coordinates between multiple Game-layer services:
  - `BehaviorRegistry` (via IGameServicesProvider)
  - `ScriptService` (via IGameServicesProvider)
  - `SystemManager` (for registration)
- Initialization logic is composition/orchestration, not core functionality
- Pattern: Core has systems, Game has initializers that wire them up

### Decision 3: DI Registration
**Where should NPCBehaviorSystem be registered in DI?**

✅ **CORE LAYER** (`PokeSharp.Core/ServiceCollectionExtensions.cs` - to be created)

**Rationale**:
- System itself is a Core component
- Can be registered as a service with required dependencies
- Game layer initializer retrieves it from DI and calls `SetBehaviorRegistry()`
- Follows dependency injection best practices (register in layer where type is defined)

**Alternative Pattern** (Current):
- ❌ Manually instantiated in Game layer initializer
- This works but bypasses DI container's lifecycle management

### Decision 4: ScriptService Dependency
**How to handle dependency on ScriptService?**

✅ **INJECT ISCRIPTINGAPIPROVIDER** (already done correctly)

**Rationale**:
- NPCBehaviorSystem only needs `IScriptingApiProvider` for creating `ScriptContext`
- Does not directly call ScriptService methods
- ScriptService remains in `PokeSharp.Scripting` project
- BehaviorRegistry handles script compilation/caching via initializer

---

## 3. Migration Steps (Order of Operations)

### Phase 1: Prepare Core Infrastructure ✅ (No Changes Needed)

**Status**: All infrastructure already exists in Core

- ✅ `BaseSystem` exists in Core
- ✅ `IUpdateSystem` exists in Core
- ✅ `SystemPriority` exists in Core with `NpcBehavior = 75`
- ✅ `IScriptingApiProvider` exists in Core
- ✅ `ScriptContext` exists in Core
- ✅ `Behavior` component exists in Core
- ✅ `TypeRegistry<BehaviorDefinition>` exists in Core

### Phase 2: Create Core ServiceCollectionExtensions (Optional Enhancement)

**File**: `/Users/ntomsic/Documents/PokeSharp/PokeSharp.Core/ServiceCollectionExtensions.cs`

**Purpose**: Enable DI registration of Core systems

```csharp
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using PokeSharp.Core.ScriptingApi;
using PokeSharp.Core.Systems;

namespace PokeSharp.Core;

/// <summary>
///     Extension methods for configuring Core services in DI container.
/// </summary>
public static class ServiceCollectionExtensions
{
    /// <summary>
    ///     Adds Core ECS systems to the service collection.
    /// </summary>
    public static IServiceCollection AddCoreSystemServices(this IServiceCollection services)
    {
        // Register NPCBehaviorSystem with DI
        services.AddSingleton<NPCBehaviorSystem>(sp =>
        {
            var logger = sp.GetRequiredService<ILogger<NPCBehaviorSystem>>();
            var loggerFactory = sp.GetRequiredService<ILoggerFactory>();
            var apis = sp.GetRequiredService<IScriptingApiProvider>();

            return new NPCBehaviorSystem(logger, loggerFactory, apis);
        });

        // Future: Register other systems here
        // services.AddSingleton<MovementSystem>();
        // services.AddSingleton<CollisionSystem>();

        return services;
    }
}
```

**Note**: This is optional. Current manual instantiation pattern works fine.

### Phase 3: Move NPCBehaviorSystem to Core ⚙️

**Steps**:

1. **Copy file to Core** (preserving Git history):
   ```bash
   # From project root
   git mv PokeSharp.Game/Systems/NpcBehaviorSystem.cs PokeSharp.Core/Systems/NPCBehaviorSystem.cs
   ```

2. **Update namespace in NPCBehaviorSystem.cs**:
   ```diff
   - namespace PokeSharp.Game.Systems;
   + namespace PokeSharp.Core.Systems;
   ```

3. **No other code changes needed** - all dependencies are satisfied

4. **Update file to use consistent naming**:
   ```diff
   - public class NPCBehaviorSystem : BaseSystem, IUpdateSystem
   + public class NPCBehaviorSystem : BaseSystem, IUpdateSystem
   ```
   (Filename already correct: `NPCBehaviorSystem.cs`)

### Phase 4: Update NPCBehaviorInitializer (Stays in Game) ⚙️

**File**: `/Users/ntomsic/Documents/PokeSharp/PokeSharp.Game/Initialization/NPCBehaviorInitializer.cs`

**Changes**:

```diff
using Arch.Core;
using Microsoft.Extensions.Logging;
using PokeSharp.Core.Logging;
using PokeSharp.Core.ScriptingApi;
using PokeSharp.Core.Systems;
using PokeSharp.Core.Types;
using PokeSharp.Game.Services;
- using PokeSharp.Game.Systems;  // Remove - now in Core

namespace PokeSharp.Game.Initialization;

/// <summary>
///     Initializes NPC behavior system with script compilation and type registry.
/// </summary>
public class NPCBehaviorInitializer(
    ILogger<NPCBehaviorInitializer> logger,
    ILoggerFactory loggerFactory,
    World world,
    SystemManager systemManager,
    IGameServicesProvider gameServices,
    IScriptingApiProvider apiProvider
)
{
    /// <summary>
    ///     Initializes the NPC behavior system with TypeRegistry and ScriptService.
    /// </summary>
    public void Initialize()
    {
        try
        {
            // Load all behavior definitions from JSON
            var loadedCount = gameServices.BehaviorRegistry.LoadAllAsync().Result;
            logger.LogWorkflowStatus("Behavior definitions loaded", ("count", loadedCount));

            // Load and compile behavior scripts for each type
            foreach (var typeId in gameServices.BehaviorRegistry.GetAllTypeIds())
            {
                var definition = gameServices.BehaviorRegistry.Get(typeId);
                if (
                    definition is IScriptedType scripted
                    && !string.IsNullOrEmpty(scripted.BehaviorScript)
                )
                {
                    logger.LogWorkflowStatus(
                        "Compiling behavior script",
                        ("behavior", typeId),
                        ("script", scripted.BehaviorScript)
                    );

                    var scriptInstance = gameServices.ScriptService
                        .LoadScriptAsync(scripted.BehaviorScript)
                        .Result;

                    if (scriptInstance != null)
                    {
                        // Initialize script with world
                        gameServices.ScriptService.InitializeScript(scriptInstance, world);

                        // Register script instance in the registry
                        gameServices.BehaviorRegistry.RegisterScript(typeId, scriptInstance);

                        logger.LogWorkflowStatus(
                            "Behavior ready",
                            ("behavior", typeId),
                            ("script", scripted.BehaviorScript)
                        );
                    }
                    else
                    {
                        logger.LogError(
                            "✗ Failed to compile script for {TypeId}: {Script}",
                            typeId,
                            scripted.BehaviorScript
                        );
                    }
                }
            }

            // Register NPCBehaviorSystem with API services
+           // Note: NPCBehaviorSystem now lives in PokeSharp.Core.Systems
            var npcBehaviorLogger = loggerFactory.CreateLogger<NPCBehaviorSystem>();
            var npcBehaviorSystem = new NPCBehaviorSystem(
                npcBehaviorLogger,
                loggerFactory,
                apiProvider
            );
            npcBehaviorSystem.SetBehaviorRegistry(gameServices.BehaviorRegistry);
            systemManager.RegisterSystem(npcBehaviorSystem);

            logger.LogSystemInitialized("NPCBehaviorSystem", ("behaviors", loadedCount));
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Failed to initialize NPC behavior system");
        }
    }
}
```

**Changes Summary**:
- ✅ Remove `using PokeSharp.Game.Systems;`
- ✅ Add comment clarifying system now lives in Core
- ✅ No other code changes (automatic resolution to Core namespace)

### Phase 5: Update Game Layer ServiceCollectionExtensions ⚙️

**File**: `/Users/ntomsic/Documents/PokeSharp/PokeSharp.Game/ServiceCollectionExtensions.cs`

**Changes**: None required - NPCBehaviorInitializer still works the same way

**Optional Enhancement** (if implementing Core DI registration):
```diff
public static IServiceCollection AddGameServices(this IServiceCollection services)
{
    // Core ECS
    services.AddSingleton(sp =>
    {
        var world = World.Create();
        return world;
    });

+   // Register Core system services
+   services.AddCoreSystemServices();

    // System Manager - Using ParallelSystemManager for inter-system parallelism
    services.AddSingleton<SystemManager>(sp =>
    {
        var world = sp.GetRequiredService<World>();
        var logger = sp.GetService<ILogger<ParallelSystemManager>>();
        return new ParallelSystemManager(world, enableParallel: true, logger);
    });

    // ... rest of registrations ...
```

### Phase 6: Verify Build and Tests ✅

**Build Commands**:
```bash
# Clean build
dotnet clean

# Restore packages
dotnet restore

# Build entire solution
dotnet build

# Run tests (if any exist)
dotnet test
```

**Verification Checklist**:
- ✅ Solution builds without errors
- ✅ No missing namespace errors
- ✅ NPCBehaviorInitializer can find NPCBehaviorSystem
- ✅ System registration works in SystemManager
- ✅ Behavior scripts compile and execute
- ✅ No runtime exceptions during system initialization

### Phase 7: Update Documentation 📝

**Files to Update**:
1. `README.md` (if mentions system locations)
2. Architecture documentation
3. Developer guides
4. Code comments referencing old location

**Git Commit Message**:
```
refactor: Move NPCBehaviorSystem from Game to Core layer

- Moved NPCBehaviorSystem to PokeSharp.Core/Systems/
- Updated namespace from PokeSharp.Game.Systems to PokeSharp.Core.Systems
- NPCBehaviorInitializer remains in Game layer (depends on IGameServicesProvider)
- No breaking changes - system functionality unchanged

Rationale:
- System operates on pure ECS components without Game-layer dependencies
- Follows pattern established by MovementSystem, CollisionSystem, etc.
- Improves separation of concerns (Core = systems, Game = composition)
```

---

## 4. File Structure After Migration

```
PokeSharp.Core/
├── Systems/
│   ├── BaseSystem.cs                    (exists)
│   ├── SystemBase.cs                    (exists)
│   ├── ISystem.cs                       (exists)
│   ├── IUpdateSystem.cs                 (exists)
│   ├── SystemPriority.cs                (exists)
│   ├── SystemManager.cs                 (exists)
│   ├── MovementSystem.cs                (exists)
│   ├── CollisionSystem.cs               (exists)
│   ├── PathfindingSystem.cs             (exists)
│   └── NPCBehaviorSystem.cs             ✨ NEW (migrated from Game)
├── ServiceCollectionExtensions.cs       ⭐ OPTIONAL (new file)
└── ...

PokeSharp.Game/
├── Initialization/
│   ├── GameInitializer.cs               (exists)
│   ├── MapInitializer.cs                (exists)
│   └── NPCBehaviorInitializer.cs        ✅ STAYS HERE (updated using statement)
├── ServiceCollectionExtensions.cs       ✅ STAYS HERE (no changes or optional enhancement)
└── Systems/
    └── (NpcBehaviorSystem.cs removed)   🗑️ DELETED
```

---

## 5. Namespace Changes Summary

### NPCBehaviorSystem
```diff
- namespace PokeSharp.Game.Systems;
+ namespace PokeSharp.Core.Systems;
```

### NPCBehaviorInitializer (using statements)
```diff
using Arch.Core;
using Microsoft.Extensions.Logging;
using PokeSharp.Core.Logging;
using PokeSharp.Core.ScriptingApi;
using PokeSharp.Core.Systems;
using PokeSharp.Core.Types;
using PokeSharp.Game.Services;
- using PokeSharp.Game.Systems;  // Remove this

namespace PokeSharp.Game.Initialization;
```

---

## 6. Import/Using Statement Updates

### Files Requiring Updates

#### ✅ NPCBehaviorSystem.cs
**Before**:
```csharp
using System.Collections.Concurrent;
using Arch.Core;
using Microsoft.Extensions.Logging;
using PokeSharp.Core.Components.Movement;
using PokeSharp.Core.Components.NPCs;
using PokeSharp.Core.Logging;
using PokeSharp.Core.Scripting.Services;
using PokeSharp.Core.ScriptingApi;
using PokeSharp.Core.Systems;
using PokeSharp.Core.Types;
using PokeSharp.Scripting.Runtime;

namespace PokeSharp.Game.Systems;
```

**After**:
```csharp
using System.Collections.Concurrent;
using Arch.Core;
using Microsoft.Extensions.Logging;
using PokeSharp.Core.Components.Movement;
using PokeSharp.Core.Components.NPCs;
using PokeSharp.Core.Logging;
using PokeSharp.Core.Scripting.Services;
using PokeSharp.Core.ScriptingApi;
using PokeSharp.Core.Systems;
using PokeSharp.Core.Types;
using PokeSharp.Scripting.Runtime;

namespace PokeSharp.Core.Systems;  // ✨ Only change
```

#### ✅ NPCBehaviorInitializer.cs
**Before**:
```csharp
using Arch.Core;
using Microsoft.Extensions.Logging;
using PokeSharp.Core.Logging;
using PokeSharp.Core.ScriptingApi;
using PokeSharp.Core.Systems;
using PokeSharp.Core.Types;
using PokeSharp.Game.Services;
using PokeSharp.Game.Systems;  // ❌ Remove

namespace PokeSharp.Game.Initialization;
```

**After**:
```csharp
using Arch.Core;
using Microsoft.Extensions.Logging;
using PokeSharp.Core.Logging;
using PokeSharp.Core.ScriptingApi;
using PokeSharp.Core.Systems;  // ✅ NPCBehaviorSystem now here
using PokeSharp.Core.Types;
using PokeSharp.Game.Services;

namespace PokeSharp.Game.Initialization;
```

### No Changes Required
- ❌ `ServiceCollectionExtensions.cs` - doesn't directly reference NPCBehaviorSystem type
- ❌ Other Game layer files - don't import or use NPCBehaviorSystem directly

---

## 7. DI Registration Strategy

### Current Pattern (Manual Instantiation)

**Location**: `NPCBehaviorInitializer.Initialize()` method

```csharp
// Current approach (works fine)
var npcBehaviorLogger = loggerFactory.CreateLogger<NPCBehaviorSystem>();
var npcBehaviorSystem = new NPCBehaviorSystem(
    npcBehaviorLogger,
    loggerFactory,
    apiProvider
);
npcBehaviorSystem.SetBehaviorRegistry(gameServices.BehaviorRegistry);
systemManager.RegisterSystem(npcBehaviorSystem);
```

**Pros**:
- ✅ Simple and explicit
- ✅ No additional DI configuration needed
- ✅ Already working

**Cons**:
- ⚠️ Bypasses DI container lifecycle management
- ⚠️ Harder to mock for testing

### Recommended Pattern (DI Registration in Core)

**Location**: `PokeSharp.Core/ServiceCollectionExtensions.cs` (new file)

```csharp
// Core layer - Register system
services.AddSingleton<NPCBehaviorSystem>(sp =>
{
    var logger = sp.GetRequiredService<ILogger<NPCBehaviorSystem>>();
    var loggerFactory = sp.GetRequiredService<ILoggerFactory>();
    var apis = sp.GetRequiredService<IScriptingApiProvider>();

    return new NPCBehaviorSystem(logger, loggerFactory, apis);
});
```

**Game layer** - `NPCBehaviorInitializer.Initialize()`:
```csharp
// Retrieve from DI instead of manual instantiation
var npcBehaviorSystem = serviceProvider.GetRequiredService<NPCBehaviorSystem>();
npcBehaviorSystem.SetBehaviorRegistry(gameServices.BehaviorRegistry);
systemManager.RegisterSystem(npcBehaviorSystem);
```

**Pros**:
- ✅ Proper DI lifecycle management
- ✅ Easier testing (can inject mocks)
- ✅ Follows best practices
- ✅ Centralizes system creation

**Cons**:
- ⚠️ Requires additional configuration file
- ⚠️ NPCBehaviorInitializer needs IServiceProvider injected

### Decision
**Start with Current Pattern**, optionally migrate to DI pattern later as enhancement.

---

## 8. Testing Strategy

### Unit Tests to Create

#### Test File: `PokeSharp.Core.Tests/Systems/NPCBehaviorSystemTests.cs`

**Test Cases**:
1. **Constructor Tests**
   - Throws ArgumentNullException when logger is null
   - Throws ArgumentNullException when loggerFactory is null
   - Throws ArgumentNullException when apis is null

2. **SetBehaviorRegistry Tests**
   - Successfully sets behavior registry
   - Allows null registry (system should handle gracefully)

3. **Initialize Tests**
   - Calls base.Initialize correctly
   - Sets World property

4. **Update Tests**
   - Returns early if behavior registry not set
   - Skips inactive behaviors
   - Skips entities without Behavior component
   - Executes OnActivated on first tick
   - Executes OnTick for active behaviors
   - Deactivates behavior on script error
   - Calls OnDeactivated when deactivating

5. **Priority Tests**
   - UpdatePriority returns SystemPriority.NpcBehavior (75)
   - Priority returns SystemPriority.NpcBehavior (75)

### Integration Tests

#### Test File: `PokeSharp.Game.Tests/Initialization/NPCBehaviorInitializerTests.cs`

**Test Cases**:
1. **Initialize Tests**
   - Loads behavior definitions from registry
   - Compiles behavior scripts
   - Initializes scripts with world
   - Registers scripts in registry
   - Creates NPCBehaviorSystem instance
   - Sets behavior registry on system
   - Registers system with SystemManager
   - Logs appropriate messages

2. **Error Handling Tests**
   - Handles missing behavior definitions gracefully
   - Handles script compilation errors
   - Handles script initialization errors
   - Logs errors appropriately

### Manual Testing Checklist

- [ ] Game starts without errors
- [ ] NPCs with behaviors spawn correctly
- [ ] Behavior scripts execute (check logs)
- [ ] NPCs respond to behavior OnTick calls
- [ ] Behavior errors don't crash the game
- [ ] Hot reload still works (if implemented)
- [ ] Performance is unchanged

---

## 9. Risk Assessment & Mitigation

### Risk 1: Build Breaks
**Impact**: High
**Likelihood**: Low
**Mitigation**:
- Use `git mv` to preserve history
- Update namespaces before committing
- Test build after each phase
- Keep rollback plan ready

### Risk 2: Runtime Errors
**Impact**: High
**Likelihood**: Medium
**Mitigation**:
- Thoroughly test NPCBehaviorInitializer
- Verify system registration works
- Test with sample NPCs and behaviors
- Check logs for initialization errors

### Risk 3: Lost Git History
**Impact**: Medium
**Likelihood**: Low
**Mitigation**:
- Use `git mv` instead of manual move
- Verify history with `git log --follow`

### Risk 4: Circular Dependencies
**Impact**: High
**Likelihood**: Very Low
**Mitigation**:
- Already verified dependencies are one-way
- Core doesn't reference Game
- No circular dependency possible

### Risk 5: Performance Regression
**Impact**: Medium
**Likelihood**: Very Low
**Mitigation**:
- No code logic changes
- Only namespace/location changes
- Run performance tests if available

---

## 10. Rollback Plan

If migration fails:

1. **Revert Git Commit**:
   ```bash
   git revert HEAD
   git push
   ```

2. **Manual Rollback** (if needed):
   ```bash
   # Move file back
   git mv PokeSharp.Core/Systems/NPCBehaviorSystem.cs PokeSharp.Game/Systems/NpcBehaviorSystem.cs

   # Restore namespace
   # Edit file and change namespace back to PokeSharp.Game.Systems

   # Restore using statements
   # Edit NPCBehaviorInitializer.cs and add back using PokeSharp.Game.Systems;

   # Rebuild
   dotnet clean
   dotnet build
   ```

3. **Verify Rollback**:
   - Solution builds
   - Tests pass
   - Game runs normally

---

## 11. Post-Migration Tasks

### Immediate (Same PR)
- [ ] Update namespace references
- [ ] Remove old file location
- [ ] Build and test
- [ ] Update comments mentioning old location
- [ ] Git commit with clear message

### Short-term (Follow-up PRs)
- [ ] Create unit tests for NPCBehaviorSystem in Core.Tests
- [ ] Create integration tests for NPCBehaviorInitializer
- [ ] Update architecture documentation
- [ ] Add XML comments if missing

### Long-term (Future Enhancements)
- [ ] Consider DI registration pattern for all systems
- [ ] Evaluate if other Game systems should move to Core
- [ ] Create system registration abstraction
- [ ] Improve test coverage for all systems

---

## 12. Success Criteria

Migration is successful when:

✅ **Build Success**:
- Solution builds without errors or warnings
- All projects compile successfully

✅ **Runtime Success**:
- Game starts without exceptions
- NPCs spawn and execute behaviors
- Behavior scripts compile and run
- System logs show normal initialization

✅ **Code Quality**:
- No circular dependencies
- Clean namespace organization
- Consistent with other Core systems
- Maintains separation of concerns

✅ **Testing**:
- Existing tests pass (if any)
- Manual testing shows no regressions
- New tests cover migrated functionality

✅ **Documentation**:
- Architecture docs updated
- Comments reference correct locations
- Migration plan documented

---

## Appendix A: Command Reference

### Git Commands
```bash
# Move file preserving history
git mv PokeSharp.Game/Systems/NpcBehaviorSystem.cs PokeSharp.Core/Systems/NPCBehaviorSystem.cs

# Check file history after move
git log --follow PokeSharp.Core/Systems/NPCBehaviorSystem.cs

# View changes
git diff

# Stage changes
git add .

# Commit
git commit -m "refactor: Move NPCBehaviorSystem from Game to Core layer"

# Revert if needed
git revert HEAD
```

### Build Commands
```bash
# Clean build
dotnet clean

# Restore packages
dotnet restore

# Build solution
dotnet build

# Build specific project
dotnet build PokeSharp.Core

# Run tests
dotnet test

# Run with verbose output
dotnet build -v detailed
```

---

## Appendix B: File Paths Quick Reference

| Description | Current Path | New Path |
|-------------|--------------|----------|
| NPCBehaviorSystem | `/Users/ntomsic/Documents/PokeSharp/PokeSharp.Game/Systems/NpcBehaviorSystem.cs` | `/Users/ntomsic/Documents/PokeSharp/PokeSharp.Core/Systems/NPCBehaviorSystem.cs` |
| NPCBehaviorInitializer | `/Users/ntomsic/Documents/PokeSharp/PokeSharp.Game/Initialization/NPCBehaviorInitializer.cs` | (No change - stays in Game) |
| Game ServiceCollectionExtensions | `/Users/ntomsic/Documents/PokeSharp/PokeSharp.Game/ServiceCollectionExtensions.cs` | (No change) |
| Core ServiceCollectionExtensions | (Does not exist) | `/Users/ntomsic/Documents/PokeSharp/PokeSharp.Core/ServiceCollectionExtensions.cs` (Optional) |

---

## Appendix C: Contact & Support

For questions about this migration:
- Review architecture documentation
- Check existing Core systems for patterns
- Verify with team leads before major changes
- Run tests frequently during migration

---

**Document Version**: 1.0
**Last Updated**: 2025-11-10
**Author**: Migration Planning Agent
**Status**: Ready for Implementation
