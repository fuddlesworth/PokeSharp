# TypeScriptBase & ScriptContext Dependency Analysis

## Executive Summary

**CRITICAL FINDING**: **CIRCULAR DEPENDENCY EXISTS**
- PokeSharp.Core references PokeSharp.Scripting
- PokeSharp.Scripting references PokeSharp.Core

**RECOMMENDATION**: **YES** - Both `TypeScriptBase` and `ScriptContext` can and **SHOULD** move to Core to break the circular dependency.

---

## 1. TypeScriptBase Dependency Analysis

### File Location
`/Users/ntomsic/Documents/PokeSharp/PokeSharp.Scripting/Runtime/TypeScriptBase.cs`

### Direct Dependencies
```csharp
using Microsoft.Extensions.Logging;              // ✅ Standard .NET library
using Microsoft.Xna.Framework;                    // ✅ Already in Core (MonoGame)
using PokeSharp.Core.Components.Movement;         // ✅ Already in Core
using PokeSharp.Scripting.Services;               // ❌ Scripting dependency (but only for namespacing)
```

### Dependency Analysis

#### What TypeScriptBase Depends On:
1. **ScriptContext** (same namespace) - Will move with it
2. **ILogger** - Standard library ✅
3. **Position** component reference in docs - Already in Core ✅

#### What TypeScriptBase IS:
- Abstract base class with virtual lifecycle methods
- **NO Roslyn dependencies**
- **NO compilation infrastructure dependencies**
- **NO scripting-specific infrastructure**
- Pure domain-level API surface

### Can It Move? **✅ YES**

**Zero blocking dependencies**. All references are to:
- Standard libraries (Microsoft.Extensions.Logging)
- MonoGame (already in Core)
- Core components (Position)
- ScriptContext (which can also move)

---

## 2. ScriptContext Dependency Analysis

### File Location
`/Users/ntomsic/Documents/PokeSharp/PokeSharp.Scripting/Runtime/ScriptContext.cs`

### Direct Dependencies
```csharp
using Arch.Core;                                  // ✅ Already in Core (ECS library)
using Microsoft.Extensions.Logging;               // ✅ Standard .NET library
using PokeSharp.Core.Components.Movement;         // ✅ Already in Core
using PokeSharp.Core.Scripting.Services;          // ✅ Already in Core
using PokeSharp.Core.ScriptingApi;                // ✅ Already in Core
using PokeSharp.Scripting.Services;               // ❌ Scripting dependency
```

### Dependency Analysis

#### What ScriptContext Depends On:
1. **World** (Arch.Core) - Already in Core ✅
2. **Entity** (Arch.Core) - Already in Core ✅
3. **ILogger** - Standard library ✅
4. **IScriptingApiProvider** - Already in Core ✅
5. **API Services** (PlayerApiService, NpcApiService, etc.) - Already in Core ✅
6. **Position** component - Already in Core ✅

#### What ScriptContext IS:
- Context object providing ECS access to scripts
- Facade for API services
- Component access helper methods
- **NO compilation logic**
- **NO Roslyn dependencies**
- **NO scripting infrastructure**

### Can It Move? **✅ YES**

**Zero blocking dependencies**. The only Scripting namespace usage is for:
- Itself (Runtime namespace)
- Service interfaces that should probably be in Core anyway

---

## 3. Circular Dependency Problem

### Current Architecture

```
PokeSharp.Core
  ├── References: PokeSharp.Scripting  ❌ CIRCULAR!
  └── Contains:
      ├── IScriptingApiProvider (Core/ScriptingApi/)
      ├── ScriptingApiProvider (Core/Scripting/Services/)
      └── All API services

PokeSharp.Scripting
  ├── References: PokeSharp.Core  ❌ CIRCULAR!
  └── Contains:
      ├── TypeScriptBase (Scripting/Runtime/)
      ├── ScriptContext (Scripting/Runtime/)
      ├── ScriptService (compilation infrastructure)
      └── RoslynScriptCompiler (Roslyn dependencies)
```

### Why This Is Bad

1. **Build order ambiguity** - Which compiles first?
2. **Tight coupling** - Cannot evolve independently
3. **Prevents clean architecture** - Core should be dependency-free
4. **Testing complexity** - Cannot test Core without Scripting
5. **Violates dependency inversion** - Core depends on implementation details

---

## 4. What References These Types

### Files Using TypeScriptBase
```
PokeSharp.Game/Systems/NPCBehaviorSystem.cs      - ✅ Can reference Core
PokeSharp.Scripting/Services/ScriptService.cs    - ⚠️  Stays in Scripting (compilation)
PokeSharp.Core/Types/TypeRegistry.cs             - ✅ Already in Core (via object)
```

### Files Using ScriptContext
```
PokeSharp.Game/Systems/NPCBehaviorSystem.cs      - ✅ Can reference Core
PokeSharp.Scripting/Services/ScriptService.cs    - ⚠️  Stays in Scripting (compilation)
*.csx behavior scripts                            - ✅ Can reference Core
```

### Impact Assessment
- **NPCBehaviorSystem** already references Core, no change needed
- **ScriptService** is in Scripting layer, will just change using statement
- **Behavior scripts** (.csx files) can reference Core APIs
- **TypeRegistry** stores scripts as `object` - no type dependency

---

## 5. Recommended Refactoring

### Phase 1: Move to Core

**Create:**
```
PokeSharp.Core/Scripting/Runtime/
  ├── TypeScriptBase.cs          (from Scripting/Runtime/)
  └── ScriptContext.cs            (from Scripting/Runtime/)
```

**Update namespace:**
```csharp
namespace PokeSharp.Core.Scripting.Runtime;  // Was: PokeSharp.Scripting.Runtime
```

### Phase 2: Remove Circular Reference

**Remove from Core.csproj:**
```xml
<!-- DELETE THIS LINE -->
<ProjectReference Include="..\PokeSharp.Scripting\PokeSharp.Scripting.csproj" />
```

### Phase 3: Update Consumers

**Files to update:**
1. `PokeSharp.Game/Systems/NPCBehaviorSystem.cs`
   ```csharp
   // Change:
   using PokeSharp.Scripting.Runtime;
   // To:
   using PokeSharp.Core.Scripting.Runtime;
   ```

2. `PokeSharp.Scripting/Services/ScriptService.cs`
   ```csharp
   // Change:
   using PokeSharp.Scripting.Runtime;
   // To:
   using PokeSharp.Core.Scripting.Runtime;
   ```

3. All `.csx` behavior scripts:
   ```csharp
   // Change:
   using PokeSharp.Scripting.Runtime;
   // To:
   using PokeSharp.Core.Scripting.Runtime;
   ```

---

## 6. Post-Move Architecture

### Clean Dependency Flow

```
PokeSharp.Core
  ├── NO external project references ✅
  └── Contains:
      ├── Scripting/Runtime/
      │   ├── TypeScriptBase.cs
      │   └── ScriptContext.cs
      ├── ScriptingApi/
      │   └── IScriptingApiProvider
      └── Scripting/Services/
          └── API services

PokeSharp.Scripting
  ├── References: PokeSharp.Core ✅ (one-way)
  └── Contains:
      ├── Compilation/ (Roslyn infrastructure)
      ├── HotReload/ (file watching)
      └── Services/ScriptService
```

### Benefits

1. **✅ No circular dependencies**
2. **✅ Core is truly core** - No external project refs
3. **✅ Clean separation** - Runtime API vs Compilation infrastructure
4. **✅ Better testability** - Core can be tested independently
5. **✅ Follows SOLID** - Dependency flows toward abstractions
6. **✅ Easier to maintain** - Clear boundaries

---

## 7. Migration Risks

### Low Risk ✅

1. **No API changes** - Just namespace changes
2. **No breaking logic** - Pure code movement
3. **Compile-time safety** - Any issues caught immediately
4. **Small surface area** - Only 2 files moving

### Potential Issues

1. **IDE tooling** - May need to restart after namespace change
2. **Git history** - Use `git mv` to preserve history
3. **Build cache** - Clean build recommended after move
4. **Script recompilation** - All .csx files need recompile

---

## 8. Testing Strategy

### After Migration, Verify:

1. **Core builds without Scripting reference** ✅
   ```bash
   dotnet build PokeSharp.Core/PokeSharp.Core.csproj
   ```

2. **Scripting still compiles** ✅
   ```bash
   dotnet build PokeSharp.Scripting/PokeSharp.Scripting.csproj
   ```

3. **Game layer works** ✅
   ```bash
   dotnet build PokeSharp.Game/PokeSharp.Game.csproj
   dotnet test
   ```

4. **Scripts still execute** ✅
   - Load existing .csx behavior scripts
   - Verify OnTick, OnActivated lifecycle
   - Test hot-reload functionality

---

## 9. Dependency Trees (Complete)

### TypeScriptBase Full Tree
```
TypeScriptBase
├── ScriptContext (parameter) ──────────────────┐
│   ├── World (Arch.Core) ✅ Core              │
│   ├── Entity (Arch.Core) ✅ Core             │
│   ├── ILogger ✅ Standard library            │
│   ├── IScriptingApiProvider ✅ Core          │
│   └── Position (convenience) ✅ Core         │
└── NO OTHER DEPENDENCIES                       │
                                                │
ScriptContext (WILL MOVE WITH TypeScriptBase) ◄─┘
├── Arch.Core (World, Entity) ✅ Core
├── Microsoft.Extensions.Logging ✅ Standard
├── PokeSharp.Core.Components.Movement ✅ Core
├── PokeSharp.Core.ScriptingApi ✅ Core
└── PokeSharp.Core.Scripting.Services ✅ Core
```

**Analysis**: Zero external dependencies. Clean move.

---

## 10. Final Recommendation

### ✅ MOVE BOTH TO CORE

**Why:**
1. **Breaks circular dependency** - Critical architectural improvement
2. **Zero technical blockers** - All dependencies already in Core
3. **Semantically correct** - These are runtime APIs, not compilation tools
4. **Improves testability** - Core becomes independently testable
5. **Follows SOLID principles** - Dependencies flow toward abstractions
6. **Low migration risk** - Namespace changes only, no logic changes

**When:**
- **Now** - This is a foundational architecture issue
- Blocks clean separation of concerns
- Will only get harder as codebase grows

**Effort:**
- **2-3 hours** - Including testing
- Move files + update namespaces + update consumers + test

---

## Appendix: Files to Modify

### Files to Move
```
PokeSharp.Scripting/Runtime/TypeScriptBase.cs
  → PokeSharp.Core/Scripting/Runtime/TypeScriptBase.cs

PokeSharp.Scripting/Runtime/ScriptContext.cs
  → PokeSharp.Core/Scripting/Runtime/ScriptContext.cs
```

### Files to Update (Namespace Changes)
```
PokeSharp.Game/Systems/NPCBehaviorSystem.cs
PokeSharp.Scripting/Services/ScriptService.cs
PokeSharp.Game/Assets/Scripts/Behaviors/wander_behavior.csx
PokeSharp.Game/Assets/Scripts/Behaviors/patrol_behavior.csx
PokeSharp.Game/Assets/Scripts/Behaviors/guard_behavior.csx
```

### Project Files to Update
```
PokeSharp.Core/PokeSharp.Core.csproj
  → REMOVE <ProjectReference Include="..\PokeSharp.Scripting\..." />
```

### Total Files Affected: **9 files**
### Estimated Time: **2-3 hours**
### Risk Level: **LOW** ✅

---

## Conclusion

**YES - Move both TypeScriptBase and ScriptContext to Core immediately.**

This is a no-brainer architectural improvement that:
- Fixes a circular dependency anti-pattern
- Has zero technical blockers
- Improves code organization and maintainability
- Takes minimal effort (2-3 hours)
- Has compile-time safety guarantees

**No refactoring needed** - Just move files and update namespaces. All dependencies are already satisfied in Core.
