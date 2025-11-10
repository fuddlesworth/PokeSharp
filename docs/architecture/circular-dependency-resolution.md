# Circular Dependency Resolution: NPCBehaviorSystem Architecture

## Executive Summary

**Problem**: Circular dependency between PokeSharp.Core and PokeSharp.Scripting
**Root Cause**: NPCBehaviorSystem (Game layer) needs TypeScriptBase and ScriptContext (Scripting layer)
**Impact**: Build failures, architectural violation, deployment blocker
**Recommended Solution**: **Option D - Split Scripting into Runtime (Core-level) and Compilation (App-level)**

---

## Current Dependency Graph (BROKEN)

```
PokeSharp.Core (ECS foundation)
    ↓
PokeSharp.Scripting (contains TypeScriptBase, ScriptContext)
    ↓
PokeSharp.Core (references for components, systems)
    ↓
PokeSharp.Game (contains NPCBehaviorSystem)
    ↓
PokeSharp.Scripting (needs TypeScriptBase, ScriptContext)
```

**CIRCULAR**: Core → Scripting → Core (INVALID!)

---

## Architecture Analysis

### Current Situation

**PokeSharp.Core** (Foundation Layer):
- ✅ Contains: ECS systems, components, TypeRegistry
- ❌ References: PokeSharp.Scripting (WRONG - creates circular dependency)
- ✅ Should be: Dependency-free foundation

**PokeSharp.Scripting** (Application Layer):
- ✅ Contains: TypeScriptBase, ScriptContext, Roslyn compilation, hot-reload
- ✅ References: PokeSharp.Core (CORRECT - depends on ECS)
- ⚠️ Problem: Contains both runtime contracts (TypeScriptBase) and compilation infrastructure

**PokeSharp.Game** (Application Layer):
- ✅ Contains: NPCBehaviorSystem (ECS system - ARCHITECTURALLY WRONG LOCATION!)
- ✅ References: Core, Scripting
- ❌ Problem: ECS systems should be in Core, not Game

### Key Insight: NPCBehaviorSystem is Misplaced

**NPCBehaviorSystem should be in Core** because:
1. It's an ECS system (not application logic)
2. It operates on Core components (Npc, Behavior, Position)
3. It's part of the game loop infrastructure
4. Systems belong in the Core layer by architectural convention

**But moving it to Core creates circular dependency** because:
1. NPCBehaviorSystem needs `TypeScriptBase` (script contract)
2. NPCBehaviorSystem needs `ScriptContext` (execution context)
3. Both types are in PokeSharp.Scripting
4. Scripting already references Core for ECS access

---

## Option Evaluation

### Option A: Move TypeScriptBase + ScriptContext to Core

**Concept**: Move runtime types into Core, keep compilation in Scripting

```
PokeSharp.Core
    ├── TypeScriptBase.cs (moved)
    ├── ScriptContext.cs (moved)
    └── Systems/NPCBehaviorSystem.cs (moved)

PokeSharp.Scripting
    ├── Compilation/ (Roslyn compiler)
    ├── HotReload/ (file watchers)
    └── Services/ScriptService.cs (uses Core types)
```

**Pros**:
- ✅ Breaks circular dependency
- ✅ NPCBehaviorSystem can move to Core (correct architecture)
- ✅ Core becomes self-contained for scripting contracts
- ✅ Clean separation: Core = contracts, Scripting = implementation

**Cons**:
- ❌ ScriptContext has many dependencies (6 API services)
- ❌ Would require moving IScriptingApiProvider to Core
- ❌ Pollutes Core with scripting infrastructure
- ❌ Violates separation of concerns (Core should be ECS-focused)

**Verdict**: ❌ **REJECTED** - Pollutes Core with too many scripting concerns

---

### Option B: Create Abstraction Layer (Interfaces in Core)

**Concept**: Define IScriptBase interface in Core, implement in Scripting

```
PokeSharp.Core
    ├── Scripting/IScriptBase.cs (new interface)
    ├── Scripting/IScriptContext.cs (new interface)
    └── Systems/NPCBehaviorSystem.cs (uses interfaces)

PokeSharp.Scripting
    ├── Runtime/TypeScriptBase.cs (implements IScriptBase)
    └── Runtime/ScriptContext.cs (implements IScriptContext)
```

**Pros**:
- ✅ Breaks circular dependency through abstraction
- ✅ Core depends only on interfaces (SOLID principle)
- ✅ NPCBehaviorSystem can move to Core
- ✅ Maintains clean architecture layers

**Cons**:
- ❌ IScriptContext interface is massive (18 methods, 12 properties)
- ❌ Creates interface duplication (violates DRY)
- ❌ Runtime overhead from interface dispatch (minor)
- ❌ Loses concrete type safety (ref returns become impossible)
- ❌ ScriptContext's ref methods can't be abstracted through interfaces

**Verdict**: ❌ **REJECTED** - Interface is too large, loses ref return optimization

---

### Option C: Keep NPCBehaviorSystem in Game (Status Quo)

**Concept**: Accept NPCBehaviorSystem as application-layer system

```
PokeSharp.Core
    └── Systems/ (other ECS systems only)

PokeSharp.Game
    └── Systems/NPCBehaviorSystem.cs (stays here)

PokeSharp.Scripting
    └── Runtime/ (TypeScriptBase, ScriptContext)
```

**Pros**:
- ✅ No circular dependency (current state works)
- ✅ Zero refactoring required
- ✅ No code changes needed

**Cons**:
- ❌ **Violates ECS architectural principles** (systems belong in Core)
- ❌ Inconsistent with other systems (MovementSystem, CollisionSystem in Core)
- ❌ Game layer becomes bloated with infrastructure
- ❌ Harder to test NPCBehaviorSystem in isolation
- ❌ Violates Single Responsibility Principle (Game layer does presentation + ECS)

**Verdict**: ❌ **REJECTED** - Violates architectural principles

---

### Option D: Split Scripting into Runtime + Compilation ✅ RECOMMENDED

**Concept**: Create two assemblies with clear responsibilities

```
PokeSharp.Scripting.Runtime (New - Core-level)
    ├── TypeScriptBase.cs (moved)
    ├── ScriptContext.cs (moved)
    └── (minimal dependencies: Arch, MonoGame, Logging)

PokeSharp.Core
    ├── Systems/NPCBehaviorSystem.cs (moved from Game)
    └── References: PokeSharp.Scripting.Runtime

PokeSharp.Scripting (Existing - Application-level)
    ├── Compilation/ (Roslyn compiler)
    ├── HotReload/ (file watchers)
    ├── Services/ScriptService.cs
    └── References: PokeSharp.Core + PokeSharp.Scripting.Runtime

PokeSharp.Game
    └── References: Core + Scripting + Scripting.Runtime
```

**Dependency Flow**:
```
Scripting.Runtime (foundation - no Core dependency)
    ↓
Core (references Scripting.Runtime for script contracts)
    ↓
Scripting (references both Core + Scripting.Runtime)
    ↓
Game (references all three)
```

**Pros**:
- ✅ **Breaks circular dependency completely**
- ✅ NPCBehaviorSystem moves to Core (correct architecture)
- ✅ Clear separation of concerns:
  - `Scripting.Runtime` = contracts only
  - `Core` = ECS systems and components
  - `Scripting` = compilation and hot-reload
- ✅ Minimal dependencies for Scripting.Runtime (Arch, MonoGame, Logging)
- ✅ Maintains concrete types (no interface overhead)
- ✅ Preserves ref return optimization
- ✅ Scalable for future scripting features

**Cons**:
- ⚠️ Requires creating new project (one-time cost)
- ⚠️ Requires updating 4 project references (minimal)
- ⚠️ Slightly more complex project structure (manageable)

**Verdict**: ✅ **RECOMMENDED** - Best balance of architecture and practicality

---

## Recommended Solution: Option D Implementation

### Step-by-Step Plan

#### Phase 1: Create PokeSharp.Scripting.Runtime Project

**1. Create new project**:
```bash
cd PokeSharp
dotnet new classlib -n PokeSharp.Scripting.Runtime -f net9.0
```

**2. Configure PokeSharp.Scripting.Runtime.csproj**:
```xml
<Project Sdk="Microsoft.NET.Sdk">
  <PropertyGroup>
    <TargetFramework>net9.0</TargetFramework>
    <ImplicitUsings>enable</ImplicitUsings>
    <Nullable>enable</Nullable>
  </PropertyGroup>
  <ItemGroup>
    <!-- Minimal dependencies - NO reference to PokeSharp.Core -->
    <PackageReference Include="Arch" Version="2.1.0" />
    <PackageReference Include="MonoGame.Framework.DesktopGL" Version="3.8.4.1" />
    <PackageReference Include="Microsoft.Extensions.Logging.Abstractions" Version="9.0.10" />
  </ItemGroup>
</Project>
```

#### Phase 2: Move Files from Scripting to Scripting.Runtime

**Files to Move**:
1. `/PokeSharp.Scripting/Runtime/TypeScriptBase.cs` → `/PokeSharp.Scripting.Runtime/TypeScriptBase.cs`
2. `/PokeSharp.Scripting/Runtime/ScriptContext.cs` → `/PokeSharp.Scripting.Runtime/ScriptContext.cs`

**Namespace Changes**:
```csharp
// OLD
namespace PokeSharp.Scripting.Runtime;

// NEW
namespace PokeSharp.Scripting.Runtime;  // Keep same namespace for compatibility
```

**ScriptContext.cs Dependency Resolution**:

**Problem**: ScriptContext currently references:
- `PokeSharp.Core.Components.Movement` (Position component)
- `PokeSharp.Core.Scripting.Services` (API services)
- `PokeSharp.Core.ScriptingApi` (API interfaces)

**Solution**: Move API service interfaces to Scripting.Runtime:

```
PokeSharp.Scripting.Runtime
    ├── IScriptingApiProvider.cs (moved from Core)
    ├── API/
    │   ├── IPlayerApi.cs (moved)
    │   ├── INpcApi.cs (moved)
    │   ├── IMapApi.cs (moved)
    │   ├── IGameStateApi.cs (moved)
    │   ├── IDialogueApi.cs (moved)
    │   └── IEffectApi.cs (moved)
    └── Runtime/
        ├── TypeScriptBase.cs
        └── ScriptContext.cs
```

**Position Component Handling**:
- ScriptContext.Position property references `PokeSharp.Core.Components.Movement.Position`
- **Solution**: Keep Position in Core, ScriptContext in Runtime references it via dependency
- This is acceptable: Runtime → Core (Position only) is a minimal, stable dependency

**Updated Scripting.Runtime.csproj**:
```xml
<ItemGroup>
  <PackageReference Include="Arch" Version="2.1.0" />
  <PackageReference Include="MonoGame.Framework.DesktopGL" Version="3.8.4.1" />
  <PackageReference Include="Microsoft.Extensions.Logging.Abstractions" Version="9.0.10" />

  <!-- Minimal Core dependency for Position component only -->
  <ProjectReference Include="..\PokeSharp.Core\PokeSharp.Core.csproj" />
</ItemGroup>
```

**Wait, this creates circular dependency again!**

**REVISED APPROACH**: ScriptContext must NOT reference Position directly.

**Better Solution**: Remove Position convenience property from ScriptContext:

```csharp
// ScriptContext.cs - REMOVE THESE LINES:
// public ref Position Position => ref GetState<Position>();
// public bool HasPosition => HasState<Position>();
```

Users access Position through generic methods:
```csharp
// Instead of: ref var pos = ref ctx.Position;
// Use: ref var pos = ref ctx.GetState<Position>();
```

This keeps Scripting.Runtime dependency-free from Core!

#### Phase 3: Update Project References

**1. PokeSharp.Core.csproj** - Add Scripting.Runtime reference:
```xml
<ItemGroup>
  <!-- REMOVE: Scripting reference (breaks circular dependency) -->
  <!-- <ProjectReference Include="..\PokeSharp.Scripting\PokeSharp.Scripting.csproj" /> -->

  <!-- ADD: Scripting.Runtime reference -->
  <ProjectReference Include="..\PokeSharp.Scripting.Runtime\PokeSharp.Scripting.Runtime.csproj" />
</ItemGroup>
```

**2. PokeSharp.Scripting.csproj** - Add Scripting.Runtime reference:
```xml
<ItemGroup>
  <PackageReference Include="Arch" Version="2.1.0" />
  <PackageReference Include="MonoGame.Framework.DesktopGL" Version="3.8.4.1" />
  <PackageReference Include="Microsoft.CodeAnalysis.CSharp.Scripting" Version="4.14.0" />
  <PackageReference Include="Microsoft.Extensions.Logging.Abstractions" Version="9.0.10" />
</ItemGroup>
<ItemGroup>
  <!-- Keep Core reference for ECS access -->
  <ProjectReference Include="..\PokeSharp.Core\PokeSharp.Core.csproj" />

  <!-- ADD: Scripting.Runtime reference -->
  <ProjectReference Include="..\PokeSharp.Scripting.Runtime\PokeSharp.Scripting.Runtime.csproj" />
</ItemGroup>
```

**3. PokeSharp.Game.csproj** - Add Scripting.Runtime reference:
```xml
<ItemGroup>
  <ProjectReference Include="..\PokeSharp.Core\PokeSharp.Core.csproj" />
  <ProjectReference Include="..\PokeSharp.Scripting\PokeSharp.Scripting.csproj" />

  <!-- ADD: Scripting.Runtime reference -->
  <ProjectReference Include="..\PokeSharp.Scripting.Runtime\PokeSharp.Scripting.Runtime.csproj" />

  <ProjectReference Include="..\PokeSharp.Rendering\PokeSharp.Rendering.csproj" />
  <ProjectReference Include="..\PokeSharp.Input\PokeSharp.Input.csproj" />
</ItemGroup>
```

#### Phase 4: Move NPCBehaviorSystem to Core

**1. Move file**:
```bash
mv PokeSharp.Game/Systems/NPCBehaviorSystem.cs PokeSharp.Core/Systems/NPCBehaviorSystem.cs
```

**2. Update namespace**:
```csharp
// OLD
namespace PokeSharp.Game.Systems;

// NEW
namespace PokeSharp.Core.Systems;
```

**3. Update using statements**:
```csharp
using PokeSharp.Scripting.Runtime; // TypeScriptBase, ScriptContext
using PokeSharp.Core.Components.Movement;
using PokeSharp.Core.Components.NPCs;
using PokeSharp.Core.Logging;
using PokeSharp.Core.Scripting.Services;
using PokeSharp.Core.ScriptingApi;
using PokeSharp.Core.Systems;
using PokeSharp.Core.Types;
```

#### Phase 5: Update References Throughout Codebase

**Files to update**:
1. **PokeSharp.Game** - Remove NPCBehaviorSystem registration (moved to Core)
2. **PokeSharp.Scripting** - Update `using` statements to reference `PokeSharp.Scripting.Runtime`
3. **Test projects** - Add Scripting.Runtime references

**Search for references**:
```bash
grep -r "PokeSharp.Scripting.Runtime" PokeSharp.Scripting/
grep -r "NPCBehaviorSystem" PokeSharp.Game/
```

#### Phase 6: Verification

**1. Build order test**:
```bash
dotnet build PokeSharp.Scripting.Runtime/PokeSharp.Scripting.Runtime.csproj
dotnet build PokeSharp.Core/PokeSharp.Core.csproj
dotnet build PokeSharp.Scripting/PokeSharp.Scripting.csproj
dotnet build PokeSharp.Game/PokeSharp.Game.csproj
```

**2. Dependency graph validation**:
```
Scripting.Runtime (no dependencies on Core)
    ↓
Core (depends on Scripting.Runtime)
    ↓
Scripting (depends on Core + Scripting.Runtime)
    ↓
Game (depends on all three)
```

**3. Run tests**:
```bash
dotnet test
```

---

## Architecture Decision Record (ADR)

**Title**: Split PokeSharp.Scripting into Runtime and Compilation Layers

**Status**: Proposed

**Context**:
- NPCBehaviorSystem (ECS system) is misplaced in Game layer
- Moving NPCBehaviorSystem to Core creates circular dependency
- Core needs script contracts (TypeScriptBase, ScriptContext) but shouldn't depend on compilation infrastructure

**Decision**:
Create **PokeSharp.Scripting.Runtime** as a new assembly containing only script contracts and execution context. This assembly has minimal dependencies and can be referenced by Core without creating circular dependencies.

**Consequences**:

**Positive**:
- ✅ Breaks circular dependency
- ✅ Correct architectural layering (systems in Core)
- ✅ Clear separation of concerns (contracts vs implementation)
- ✅ Minimal dependencies for Scripting.Runtime
- ✅ Preserves concrete types and performance optimizations
- ✅ Scalable for future scripting features

**Negative**:
- ⚠️ One additional project in solution (manageable)
- ⚠️ Requires one-time refactoring effort
- ⚠️ Developers must understand two scripting assemblies

**Alternatives Considered**:
- Option A: Move types to Core (rejected - pollutes Core)
- Option B: Create interface abstractions (rejected - too complex)
- Option C: Keep NPCBehaviorSystem in Game (rejected - violates architecture)

**Risks**:
- Low risk: Changes are structural, not functional
- Mitigation: Comprehensive testing after refactoring

---

## Final Dependency Graph (CORRECT)

```
PokeSharp.Scripting.Runtime (contracts only - no Core dependency)
    ↓
PokeSharp.Core (references Scripting.Runtime)
    ├── Components/
    ├── Systems/
    │   └── NPCBehaviorSystem.cs (uses TypeScriptBase, ScriptContext)
    └── Types/TypeRegistry.cs
    ↓
PokeSharp.Scripting (references Core + Scripting.Runtime)
    ├── Compilation/ (Roslyn)
    ├── HotReload/ (watchers)
    └── Services/ (ScriptService)
    ↓
PokeSharp.Game (references all three)
    ├── Game.cs (MonoGame entry point)
    └── Application logic
```

**Dependencies**:
- **Scripting.Runtime**: Arch, MonoGame, Logging (3 packages)
- **Core**: Scripting.Runtime, Arch, MonoGame, Logging, Spectre.Console (5 total)
- **Scripting**: Core, Scripting.Runtime, Roslyn, FileWatchers (6+ total)
- **Game**: Core, Scripting, Scripting.Runtime, Rendering, Input (8+ total)

---

## Implementation Checklist

- [ ] Create PokeSharp.Scripting.Runtime project
- [ ] Configure Scripting.Runtime.csproj with minimal dependencies
- [ ] Move TypeScriptBase.cs to Scripting.Runtime
- [ ] Move ScriptContext.cs to Scripting.Runtime (remove Position property)
- [ ] Move API interfaces to Scripting.Runtime
- [ ] Update PokeSharp.Core.csproj (remove Scripting, add Scripting.Runtime)
- [ ] Update PokeSharp.Scripting.csproj (add Scripting.Runtime)
- [ ] Update PokeSharp.Game.csproj (add Scripting.Runtime)
- [ ] Move NPCBehaviorSystem.cs to PokeSharp.Core/Systems/
- [ ] Update NPCBehaviorSystem namespace and usings
- [ ] Update all `using PokeSharp.Scripting.Runtime` statements
- [ ] Build all projects in dependency order
- [ ] Run full test suite
- [ ] Update documentation

---

## Conclusion

**Option D (Split Scripting into Runtime + Compilation) is the recommended solution** because it:

1. **Solves the problem**: Breaks circular dependency completely
2. **Fixes architecture**: Moves NPCBehaviorSystem to Core where it belongs
3. **Maintains performance**: Preserves concrete types and ref returns
4. **Scales well**: Clear separation enables future scripting features
5. **Minimal cost**: One-time refactoring with low risk

The separation of **contracts** (Scripting.Runtime) from **implementation** (Scripting) is a well-established architectural pattern that provides long-term maintainability and flexibility.
