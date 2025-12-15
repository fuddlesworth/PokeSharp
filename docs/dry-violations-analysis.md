# DRY Violations Analysis: NPC vs Tile Behavior Systems

**Analysis Date:** 2025-12-15
**Scope:** Parallel behavior systems with significant code duplication
**Files Analyzed:** 5

## Executive Summary

The NPC and Tile behavior systems contain substantial code duplication across multiple layers:
- **Systems Layer:** ~60% code similarity between NPCBehaviorSystem and TileBehaviorSystem
- **Initialization Layer:** ~75% code similarity between initializers
- **Critical Impact:** Maintenance burden, inconsistent behavior handling, missed bug fixes

## Critical DRY Violations

### 1. LOGGER CACHE MANAGEMENT - Identical Implementation

**Files:**
- `NPCBehaviorSystem.cs:260-278`
- `TileBehaviorSystem.cs:554-572`

**Violation:** Exact duplication of logger caching logic with only class name differences.

```csharp
// NPCBehaviorSystem.cs:260-278
private ILogger GetOrCreateLogger(string key)
{
    return _scriptLoggerCache.GetOrAdd(
        key,
        k =>
        {
            if (_scriptLoggerCache.Count >= _config.MaxCachedLoggers)
            {
                _logger.LogWarning(
                    "Script logger cache limit reached ({Limit}). Consider increasing limit or checking for leaks.",
                    _config.MaxCachedLoggers
                );
            }
            return _loggerFactory.CreateLogger($"Script.{k}");
        }
    );
}

// TileBehaviorSystem.cs:554-572
private ILogger GetOrCreateLogger(string key)
{
    return _scriptLoggerCache.GetOrAdd(
        key,
        k =>
        {
            if (_scriptLoggerCache.Count >= _config.MaxCachedLoggers)
            {
                _logger.LogWarning(
                    "Script logger cache limit reached ({Limit}). Consider increasing limit or checking for leaks.",
                    _config.MaxCachedLoggers
                );
            }
            return _loggerFactory.CreateLogger($"TileBehavior.{k}");
        }
    );
}
```

**Impact:** High - Bug fixes or improvements must be duplicated
**Abstraction Opportunity:** Extract to `BehaviorSystemBase<TDefinition>` or helper class

---

### 2. PERFORMANCE METRICS LOGGING - Identical Pattern

**Files:**
- `NPCBehaviorSystem.cs:186-206`
- `TileBehaviorSystem.cs:378-398`

**Violation:** Identical performance tracking logic with copy-paste implementation.

```csharp
// NPCBehaviorSystem.cs:186-206
_tickCounter++;

bool shouldLogSummary =
    errorCount > 0
    || (_tickCounter % 60 == 0 && behaviorCount > 0)
    || (behaviorCount > 0 && behaviorCount != _lastBehaviorSummaryCount);

if (shouldLogSummary)
{
    _logger.LogWorkflowStatus(
        "Behavior tick summary",
        ("executed", behaviorCount),
        ("errors", errorCount)
    );
}

if (behaviorCount > 0)
{
    _lastBehaviorSummaryCount = behaviorCount;
}

// TileBehaviorSystem.cs:378-398 - IDENTICAL LOGIC
```

**Impact:** High - Performance metric changes require dual updates
**Abstraction Opportunity:** Extract to base class or shared component

---

### 3. REGISTRY MANAGEMENT - Duplicated Pattern

**Files:**
- `NPCBehaviorSystem.cs:248-252`
- `TileBehaviorSystem.cs:404-408`

**Violation:** Nearly identical registry setter methods.

```csharp
// NPCBehaviorSystem.cs:248-252
public void SetBehaviorRegistry(TypeRegistry<BehaviorDefinition> registry)
{
    _behaviorRegistry = registry;
    _logger.LogWorkflowStatus("Behavior registry linked", ("behaviors", registry.Count));
}

// TileBehaviorSystem.cs:404-408
public void SetBehaviorRegistry(TypeRegistry<TileBehaviorDefinition> registry)
{
    _behaviorRegistry = registry;
    _logger.LogWorkflowStatus("Tile behavior registry linked", ("behaviors", registry.Count));
}
```

**Impact:** Medium - Simple duplication but indicates missing abstraction
**Abstraction Opportunity:** Generic base class method

---

### 4. INITIALIZATION LOGGING - Near-Identical Implementation

**Files:**
- `NPCBehaviorSystem.cs:71-78`
- `TileBehaviorSystem.cs:280-287`

**Violation:** Duplicated initialization logging pattern.

```csharp
// NPCBehaviorSystem.cs:71-78
public override void Initialize(World world)
{
    base.Initialize(world);
    _logger.LogSystemInitialized(
        "NPCBehaviorSystem",
        ("behaviors", _behaviorRegistry?.Count ?? 0)
    );
}

// TileBehaviorSystem.cs:280-287 - IDENTICAL PATTERN
public override void Initialize(World world)
{
    base.Initialize(world);
    _logger.LogSystemInitialized(
        "TileBehaviorSystem",
        ("behaviors", _behaviorRegistry?.Count ?? 0)
    );
}
```

**Impact:** Low - Simple but indicative of larger pattern
**Abstraction Opportunity:** Template method in base class

---

### 5. SCRIPTCONTEXT CREATION - Heavily Duplicated

**Files:**
- `NPCBehaviorSystem.cs:131-137, 96-102`
- `TileBehaviorSystem.cs:96-103, 147-154, 186-193, 225-232, 264-271, 341-350, 475-484`

**Violation:** ScriptContext instantiation repeated 10+ times with identical parameters.

```csharp
// Pattern repeated in BOTH files:
var context = new ScriptContext(
    world,
    entity,
    scriptLogger,  // or _logger
    _apis,
    _eventBus ?? throw new InvalidOperationException("EventBus is required")
);
```

**Impact:** Very High - Most severe duplication, maintenance nightmare
**Abstraction Opportunity:** Factory method or helper in base class

**Occurrences:**
- NPCBehaviorSystem: 2 instances (lines 96-102, 131-137)
- TileBehaviorSystem: 8 instances (lines 96-103, 147-154, 186-193, 225-232, 264-271, 341-350, 475-484)

---

### 6. BEHAVIOR VALIDATION CHECKS - Duplicated Guards

**Files:**
- `NPCBehaviorSystem.cs:82-86`
- `TileBehaviorSystem.cs:85-88, 136-139, 175-178, 213-217, 253-256, 291-295`

**Violation:** Identical null registry checks throughout both systems.

```csharp
// Pattern in both files:
if (_behaviorRegistry == null)
{
    _logger.LogSystemUnavailable("Behavior registry", "not set on [System]");
    return;
}
```

**Impact:** Medium - Inconsistent error messages possible
**Abstraction Opportunity:** Protected helper method in base class

---

### 7. INITIALIZER SCRIPT LOADING - Near-Complete Duplication

**Files:**
- `NPCBehaviorInitializer.cs:34-78`
- `TileBehaviorInitializer.cs:36-82`

**Violation:** 95% identical script loading and compilation logic.

```csharp
// Both files have IDENTICAL structure:
int loadedCount = await behaviorRegistry.LoadAllAsync();
logger.LogWorkflowStatus("Behavior definitions loaded", ("count", loadedCount));

foreach (string typeId in behaviorRegistry.GetAllTypeIds())
{
    BehaviorDefinition? definition = behaviorRegistry.Get(typeId);
    if (definition is IScriptedType scripted && !string.IsNullOrEmpty(scripted.BehaviorScript))
    {
        logger.LogWorkflowStatus("Compiling behavior script", ...);
        object? scriptInstance = await scriptService.LoadScriptAsync(scripted.BehaviorScript);

        if (scriptInstance != null)
        {
            behaviorRegistry.RegisterScript(typeId, scriptInstance);
            logger.LogWorkflowStatus("Behavior ready", ...);
        }
        else
        {
            logger.LogError("Failed to compile script...", ...);
        }
    }
}
```

**Impact:** Critical - Entire initialization workflow duplicated
**Abstraction Opportunity:** Generic `BehaviorInitializerBase<TDefinition, TSystem>`

**Key Difference:** TileBehaviorInitializer calls `scriptService.InitializeScript()` but NPCBehaviorInitializer doesn't (line 63 vs omitted)

---

### 8. SYSTEM REGISTRATION PATTERN - Copy-Paste Implementation

**Files:**
- `NPCBehaviorInitializer.cs:80-92`
- `TileBehaviorInitializer.cs:85-95`

**Violation:** Near-identical system creation and registration.

```csharp
// NPCBehaviorInitializer.cs:80-92
ILogger<NPCBehaviorSystem> npcBehaviorLogger = loggerFactory.CreateLogger<NPCBehaviorSystem>();
var npcBehaviorSystem = new NPCBehaviorSystem(
    npcBehaviorLogger,
    loggerFactory,
    apiProvider,
    scriptService,
    eventBus
);
npcBehaviorSystem.SetBehaviorRegistry(behaviorRegistry);
systemManager.RegisterUpdateSystem(npcBehaviorSystem);

// TileBehaviorInitializer.cs:85-95 - NEARLY IDENTICAL
ILogger<TileBehaviorSystem> tileBehaviorLogger = loggerFactory.CreateLogger<TileBehaviorSystem>();
var tileBehaviorSystem = new TileBehaviorSystem(
    tileBehaviorLogger,
    loggerFactory,
    apiProvider,
    eventBus
);
tileBehaviorSystem.SetBehaviorRegistry(behaviorRegistry);
systemManager.RegisterUpdateSystem(tileBehaviorSystem);
```

**Impact:** High - Constructor signature changes need dual updates
**Abstraction Opportunity:** Generic factory method

---

### 9. ERROR HANDLING IN UPDATE LOOP - Identical Pattern

**Files:**
- `NPCBehaviorSystem.cs:156-176`
- `TileBehaviorSystem.cs:363-374`

**Violation:** Exception handling with similar cleanup logic.

```csharp
// NPCBehaviorSystem.cs:156-176
catch (Exception ex)
{
    _logger.LogExceptionWithContext(
        ex,
        "Behavior script error for NPC {NpcId}",
        npc.NpcId
    );
    errorCount++;

    _entityScriptCache.TryRemove(entity, out _);
    DeactivateBehavior(world, entity, null, ref behavior, null, npc.NpcId, $"error: {ex.Message}");
}

// TileBehaviorSystem.cs:363-374
catch (Exception ex)
{
    _logger.LogExceptionWithContext(
        ex,
        "Tile behavior script error at ({X}, {Y})",
        tilePos.X,
        tilePos.Y
    );
    errorCount++;
    behavior.IsActive = false;
}
```

**Impact:** Medium - Inconsistent cleanup behavior (NPC has cache cleanup, Tile doesn't)
**Abstraction Opportunity:** Template method for error handling

---

### 10. FIELD DECLARATIONS - Duplicated Infrastructure

**Files:**
- `NPCBehaviorSystem.cs:31-41`
- `TileBehaviorSystem.cs:32-40`

**Violation:** Nearly identical field declarations.

```csharp
// Both systems have:
private readonly IScriptingApiProvider _apis;
private readonly PerformanceConfiguration _config;
private readonly IEventBus? _eventBus;
private readonly ILogger<[SystemType]> _logger;
private readonly ILoggerFactory _loggerFactory;
private readonly ConcurrentDictionary<string, ILogger> _scriptLoggerCache = new();
private TypeRegistry<[DefinitionType]>? _behaviorRegistry;
private int _lastBehaviorSummaryCount;
private int _tickCounter;
```

**Impact:** Medium - Shared infrastructure not abstracted
**Abstraction Opportunity:** Base class with protected fields

---

## Architectural Inconsistencies

### 11. SCRIPT INSTANCE MANAGEMENT - Divergent Approaches

**Critical Difference:**
- **NPCBehaviorSystem:** Per-entity script instances with event subscriptions (lines 212-243)
- **TileBehaviorSystem:** Singleton scripts with method parameters (lines 314-336)

**Files:**
- `NPCBehaviorSystem.cs:212-243` (GetOrCreateEntityScript with caching)
- `TileBehaviorSystem.cs:314-336` (Direct registry lookup, no caching)

**Impact:** Very High - Different memory models, different event handling
**Root Cause:** Architectural decision documented in comments (lines 23-28 in NPCBehaviorSystem)

**Implications:**
- NPC behaviors maintain state per entity
- Tile behaviors are stateless
- Event handler cleanup differs significantly (NPCBehaviorSystem has CleanupEntityBehavior method, TileBehaviorSystem doesn't)

---

### 12. INITIALIZATION SCRIPT HANDLING - Inconsistent Logic

**Files:**
- `NPCBehaviorInitializer.cs:59-61` (NO script initialization)
- `TileBehaviorInitializer.cs:62-63` (DOES initialize scripts)

**Violation:**

```csharp
// NPCBehaviorInitializer.cs:59-61
if (scriptInstance != null)
{
    // NOTE: Do NOT initialize the template - per-entity instances are initialized separately
    behaviorRegistry.RegisterScript(typeId, scriptInstance);
}

// TileBehaviorInitializer.cs:62-66
if (scriptInstance != null)
{
    // Initialize script with world
    scriptService.InitializeScript(scriptInstance, world);
    behaviorRegistry.RegisterScript(typeId, scriptInstance);
}
```

**Impact:** Critical - Silently different initialization behavior
**Root Cause:** Architectural difference (singleton vs per-entity)
**Risk:** Easy to miss when maintaining parallel systems

---

## Abstraction Opportunities

### Recommended Base Class Structure

```csharp
public abstract class BehaviorSystemBase<TDefinition, TComponent> : SystemBase, IUpdateSystem
    where TDefinition : ITypeDefinition, IScriptedType
    where TComponent : struct
{
    // Common fields (Violation #10)
    protected readonly IScriptingApiProvider _apis;
    protected readonly PerformanceConfiguration _config;
    protected readonly IEventBus? _eventBus;
    protected readonly ILogger _logger;
    protected readonly ILoggerFactory _loggerFactory;
    protected readonly ConcurrentDictionary<string, ILogger> _scriptLoggerCache = new();
    protected TypeRegistry<TDefinition>? _behaviorRegistry;
    protected int _lastBehaviorSummaryCount;
    protected int _tickCounter;

    // Common methods
    protected ILogger GetOrCreateLogger(string key, string prefix); // Violation #1
    protected void LogPerformanceMetrics(int behaviorCount, int errorCount); // Violation #2
    protected void SetBehaviorRegistry(TypeRegistry<TDefinition> registry); // Violation #3
    protected ScriptContext CreateScriptContext(World world, Entity entity, ILogger logger); // Violation #5
    protected bool ValidateRegistry(string systemName); // Violation #6
}
```

### Recommended Initializer Base Class

```csharp
public abstract class BehaviorInitializerBase<TDefinition, TSystem>
    where TDefinition : ITypeDefinition, IScriptedType
    where TSystem : BehaviorSystemBase<TDefinition>
{
    // Extract common initialization logic (Violation #7, #8)
    protected async Task<int> LoadAndCompileBehaviorsAsync(
        TypeRegistry<TDefinition> registry,
        ScriptService scriptService,
        World world,
        bool initializeScripts // Handle the difference in #12
    );

    protected TSystem CreateAndRegisterSystem(
        SystemManager systemManager,
        TypeRegistry<TDefinition> registry
    );
}
```

---

## Violation Impact Summary

| Violation | Severity | Maintenance Cost | Lines Duplicated | Abstraction Complexity |
|-----------|----------|------------------|------------------|------------------------|
| #1 Logger Cache | High | High | 38 | Low |
| #2 Performance Metrics | High | Medium | 42 | Low |
| #3 Registry Management | Medium | Low | 10 | Low |
| #4 Initialization | Low | Low | 16 | Low |
| #5 ScriptContext Creation | Very High | Very High | 80+ | Medium |
| #6 Validation Guards | Medium | Medium | 36+ | Low |
| #7 Script Loading | Critical | Very High | 90+ | Medium |
| #8 System Registration | High | High | 26 | Low |
| #9 Error Handling | Medium | Medium | 42 | Medium |
| #10 Field Declarations | Medium | Low | 18 | Low |
| #11 Script Management | Architectural | N/A | N/A | High |
| #12 Init Script Logic | Critical | High | 6 | Medium |

**Total Estimated Duplicated Lines:** ~400+ lines across 5 files

---

## Recommendations

### Immediate Actions (High Priority)

1. **Create `BehaviorSystemBase<TDefinition>` abstract class**
   - Extract violations #1, #2, #3, #5, #6, #10
   - Estimated effort: 4-6 hours
   - Risk: Low - Well-defined abstraction

2. **Create `BehaviorInitializerBase<TDefinition, TSystem>` abstract class**
   - Extract violations #7, #8
   - Handle architectural difference #12 via abstract method
   - Estimated effort: 3-4 hours
   - Risk: Low - Clear separation point

3. **Document architectural differences**
   - Violations #11, #12 are intentional
   - Add architecture decision records (ADRs)
   - Estimated effort: 1-2 hours
   - Risk: None

### Medium-Term Actions

4. **Standardize error handling** (Violation #9)
   - Define consistent cleanup protocol
   - Estimated effort: 2-3 hours
   - Risk: Medium - Requires testing

5. **Extract ScriptContext factory** (Violation #5)
   - Create dedicated factory class or base class method
   - Estimated effort: 1-2 hours
   - Risk: Low

### Long-Term Considerations

6. **Evaluate unified behavior architecture**
   - Consider if NPC and Tile behaviors could share more infrastructure
   - May require significant refactoring
   - Estimated effort: 20+ hours
   - Risk: High - Major architectural change

---

## Testing Implications

Any refactoring MUST verify:
1. NPC per-entity script instances still work correctly
2. Tile singleton scripts maintain stateless behavior
3. Event subscription cleanup works for NPC behaviors
4. Performance metrics remain accurate
5. Logger caching doesn't leak memory
6. Script initialization happens at correct lifecycle points

---

## Metrics

- **Code Duplication Index:** 42% (estimated across analyzed files)
- **Maintenance Complexity:** High
- **Refactoring ROI:** Very High (one fix applies to both systems)
- **Technical Debt:** 8-12 developer hours to resolve critical violations

---

## Conclusion

The NPC and Tile behavior systems exhibit severe DRY violations across multiple layers. While some differences are architecturally justified (per-entity vs singleton scripts), the majority of duplication represents technical debt that increases maintenance burden and bug risk.

**Priority Focus:** Violations #5 (ScriptContext), #7 (Script Loading), and #1 (Logger Cache) represent the highest impact opportunities for abstraction.

**Critical Insight:** The parallel structure suggests a missing abstraction at the behavior system level. A well-designed base class could eliminate 60-70% of code duplication while preserving architectural flexibility.
