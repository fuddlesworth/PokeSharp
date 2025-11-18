# Remaining Code Quality Tasks

This document lists the remaining items from `CODE_QUALITY_ANALYSIS.md` that have not yet been addressed.

## Summary

**High Priority**: ✅ All completed
**Medium Priority**: ✅ All completed
**Low Priority**: ⚠️ 1 item remaining (TODO tracking - organizational task)

---

## Medium Priority Items

### 1. ScriptService SRP Violation (3.1.2)
**Severity**: Medium  
**Location**: `PokeSharp.Game.Scripting/Services/ScriptService.cs`

**Issue**: `ScriptService` handles multiple responsibilities:
- Script compilation
- Script caching
- Script instantiation
- Script initialization
- Script disposal

**Recommendation**:
- Extract caching to `ScriptCache` class
- Extract compilation to `ScriptCompiler` class
- Keep only orchestration in `ScriptService`

**Effort**: High (significant refactoring, requires updating all call sites)

**Status**: ✅ Completed - Extracted `ScriptCache` and `ScriptCompiler` classes. `ScriptService` now orchestrates these components, improving separation of concerns and testability.

---

### 2. Hard-Coded Initialization Steps (3.2.1)
**Severity**: Medium  
**Location**: `PokeSharp.Game/PokeSharpGame.cs`

**Issue**: Initialization steps are hard-coded in `InitializeGameplaySceneAsync`, making it difficult to extend without modification.

**Recommendation**:
- Use strategy pattern or plugin architecture
- Register initialization steps via DI
- Create `IInitializationStep` interface
- Create `InitializationPipeline` class

**Effort**: High (significant architectural change)

**Status**: ✅ Completed - Implemented full initialization pipeline pattern with:
- `IInitializationStep` interface
- `InitializationContext` for shared state
- `InitializationPipeline` orchestrator
- 13 concrete step implementations
- `BuildInitializationPipeline` method in `PokeSharpGame`
- Steps are now composable and extensible

---

### 3. Generic Exception Catching (5.3.2)
**Severity**: Medium  
**Location**: Multiple files

**Issue**: Many catch blocks use `catch (Exception ex)` instead of specific exception types.

**Examples Found**:
- `PokeSharpGame.cs` - lines 275, 436
- `MapInitializer.cs` - lines 63, 100, 186
- `ScriptService.cs` - line 175 (but also catches `CompilationErrorException` specifically at line 150)
- `TypeRegistry.cs` - lines 51, 101
- `LoadingScene.cs` - lines 157, 333
- `EventBus.cs` - line 50 (intentional for handler isolation)
- `ScriptHotReloadService.cs` - line 411

**Recommendation**:
- Catch specific exceptions when possible (e.g., `FileNotFoundException`, `IOException`, `InvalidOperationException`)
- Only catch `Exception` at top-level boundaries (like event handlers, main initialization)
- Document when generic catching is intentional (error isolation)

**Effort**: Medium (requires understanding each context)

**Status**: ✅ Completed - Improved exception handling in:
- `PokeSharpGame.LoadGameDataAsync` - Now catches `FileNotFoundException`, `DirectoryNotFoundException`, `IOException` specifically
- `MapInitializer.LoadMap` and `LoadMapFromFile` - Now catches `FileNotFoundException`, `MapValidationException`, `InvalidOperationException`, `IOException` specifically
- `ScriptService.LoadScriptAsync` - Now catches `FileNotFoundException`, `IOException` specifically (already had `CompilationErrorException`)
- Generic `Exception` catch blocks remain as fallback for unexpected errors

---

## Low Priority Items

### 4. Duplicate Progress Reporting (4.4)
**Severity**: Low  
**Location**: `PokeSharp.Game/PokeSharpGame.cs`

**Issue**: Repeated pattern:
```csharp
progress.CurrentStep = "...";
progress.Progress = InitializationProgress.X;
```

**Recommendation**:
- Create helper method: `progress.Report("...", InitializationProgress.X)`
- Or create extension method on `LoadingProgress`

**Effort**: Low (simple helper method)

**Status**: ✅ Completed - Created `LoadingProgressExtensions.Report()` method and updated all progress reporting calls in `PokeSharpGame.cs` to use the new helper method

---

### 5. String Interpolation in Logging (5.4.1)
**Severity**: Low  
**Location**: Check throughout codebase

**Issue**: Some logs may use string interpolation instead of structured logging.

**Examples Found**:
- `LoadingScene.cs` - lines 285, 309, 338 (error messages, acceptable for UI)
- `PokeSharpGame.cs` - lines 82-185 (exception messages, acceptable)

**Note**: Most logging already uses structured logging. The examples found are mostly for exception messages or UI text, which is acceptable.

**Recommendation**:
- Review remaining instances
- Convert to structured logging where appropriate
- Keep string interpolation for UI text and exception messages

**Effort**: Low (mostly already done)

**Status**: ✅ Completed - Reviewed codebase; all logging calls use structured logging. String interpolation found is only for UI text display (RawText), which is acceptable and appropriate

---

### 6. Hard-Coded Configuration (5.7.1)
**Severity**: Low  
**Location**: `PokeSharp.Game/PokeSharpGame.cs`

**Issue**: Some configuration is still hard-coded:
```csharp
var windowConfig = GameWindowConfig.CreateDefault();
```

**Current State**: We've extracted many magic strings/numbers to constants, but some configuration could be loaded from files.

**Recommendation**:
- Load from `appsettings.json`
- Use `IOptions<T>` pattern
- Support environment-specific configuration

**Effort**: Medium (requires configuration infrastructure)

**Status**: ✅ Completed - Moved hard-coded configuration to `appsettings.json` using `IOptions<T>` pattern:
- Created `GameConfiguration` class with `GameWindowConfig` and `GameInitializationConfig`
- Added configuration sections to `appsettings.json` and `appsettings.Development.json`
- Configured options binding in `ServiceCollectionExtensions`
- Updated `PokeSharpGame` to use `IOptions<GameConfiguration>`
- Updated all initialization steps to use configuration from context
- Window settings, asset paths, map IDs, and spawn coordinates are now configurable

---

### 7. Commented-Out Code (2.4)
**Severity**: Low  
**Location**: `PokeSharp.Engine.Rendering/Assets/AssetManager.cs` (line 48-49)

**Issue**: Commented-out code should be removed.

**Current State**: The comment actually explains why code was removed (documentation), which is acceptable:
```csharp
// REMOVED: LoadManifest() and LoadManifestInternal() - obsolete
// manifest.json has been replaced by EF Core MapDefinition and on-demand texture loading
```

**Recommendation**:
- This is actually good documentation, not dead code
- Consider moving to XML documentation if needed
- No action required

**Status**: ✅ Acceptable as-is

---

### 8. TODO Comments (8.1)
**Severity**: Low  
**Location**: Multiple files

**Found TODOs**:
- `MapObjectSpawner.cs:262` - Battle system implementation
- `TiledMapLoader.cs:359` - Logging implementation
- `TmxDocumentValidator.cs:10,32` - Validator rewrite needed
- `TrainerDefinition.cs:47,83` - Join table replacements
- `GameDataContext.cs:13` - Pokemon system implementation

**Recommendation**:
- Track TODOs in issue tracker
- Add issue numbers to TODO comments
- Prioritize and schedule TODOs

**Effort**: Low (organizational task, not code fix)

**Status**: ⏳ Not started

---

## Priority Recommendations

### Quick Wins (Low Effort, Good Value)
1. ✅ **Progress Reporting Helper** (4.4) - Easy helper method - **COMPLETED**
2. ✅ **String Interpolation Review** (5.4.1) - Quick review, mostly done - **COMPLETED**

### Medium-Term Improvements
3. ✅ **Generic Exception Catching** (5.3.2) - Improve specific exception handling - **COMPLETED**
4. ✅ **Configuration Loading** (5.7.1) - Move to configuration files - **COMPLETED**

### Long-Term Refactorings
5. ✅ **ScriptService SRP** (3.1.2) - Significant refactoring - **COMPLETED**
6. ✅ **Initialization Pipeline Pattern** (3.2.1) - Architectural change - **COMPLETED**

### Organizational Tasks
7. ⚠️ **TODO Tracking** (8.1) - Issue tracker integration

---

## Completed Items Summary

All **High Priority** items have been completed:
- ✅ Extract initialization pipeline (broken into 12 methods)
- ✅ Standardize error handling (documented strategy)
- ✅ Fix disposal patterns (standardized async disposal)
- ✅ Document design decisions (comprehensive XML docs)

All **Medium Priority** items completed:
- ✅ Extract interfaces (`IGameInitializer`, `IMapInitializer`)
- ✅ Service locator documented (explained why `IServiceProvider` is needed)
- ✅ Improve nullable annotations (fixed `MapInitializer`)
- ✅ ScriptService SRP violation (extracted `ScriptCache` and `ScriptCompiler`)
- ✅ Hard-coded initialization steps (implemented pipeline pattern)

Most **Low Priority** items completed:
- ✅ Extract magic numbers (constants created)
- ✅ Cache reflection results (implemented)
- ✅ Optimize LINQ usage (direct iteration)

---

## Next Steps

1. ✅ **Progress Reporting Helper** - COMPLETED
2. ✅ **Exception Handling** - COMPLETED
3. ✅ **ScriptService Refactoring** - COMPLETED
4. ✅ **Initialization Pipeline** - COMPLETED
5. ⚠️ **Configuration Loading** - Move hard-coded config to `appsettings.json` using `IOptions<T>`
6. ⚠️ **TODO Tracking** - Set up issue tracking for TODO comments

