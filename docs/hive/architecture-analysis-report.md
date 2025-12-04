# Architecture Analysis Report - PokeSharp Event System (6 Phases)

**Analyst**: Architecture Analyst Worker (Hive Mind Swarm)
**Date**: 2025-12-03
**Scope**: Phases 1-6 (commits 748558e → 946009b)
**Focus**: Architecture issues only (SOLID, DRY, performance handled by other workers)

---

## Executive Summary

**Critical Finding**: The codebase contains TWO parallel EventBus implementations (`EventBus` and `EventBusOptimized`) that violate Single Source of Truth and increase maintenance burden.

**Total Issues Found**: 7 architecture issues
- **Critical**: 2
- **High**: 3
- **Medium**: 2
- **Low**: 0

---

## 🔴 CRITICAL ISSUES

### Issue #1: Dual EventBus Implementations (Parallel Architecture)

**Severity**: Critical
**Introduced**: Phase 6 (commit 946009b)
**Location**:
- `/PokeSharp.Engine.Core/Events/EventBus.cs` (210 lines)
- `/PokeSharp.Engine.Core/Events/EventBusOptimized.cs` (278 lines)

**Problem**:
Two separate implementations of `IEventBus` exist with 90% overlapping functionality:

1. **EventBus** (original):
   - Standard ConcurrentDictionary-based implementation
   - Metrics support added in Phase 5
   - Used in DI container (line 56 of CoreServicesExtensions.cs)
   - Handler ID tracking with atomic increment

2. **EventBusOptimized** (Phase 6):
   - Cached handler arrays for fast-path optimization
   - Same ConcurrentDictionary foundation
   - Same metrics support (IEventMetrics)
   - Same subscription/unsubscription logic
   - **NOT registered in DI container**

**Architecture Violations**:
- ❌ **Single Source of Truth**: Two implementations of the same abstraction
- ❌ **Don't Repeat Yourself**: 488 lines of duplicated logic
- ❌ **Interface Segregation**: Both implementations expose identical inspection methods (`GetRegisteredEventTypes`, `GetHandlerIds`)
- ❌ **Open/Closed Principle**: Cannot swap implementations without code changes

**Impact**:
- Bug fixes must be applied to both classes
- Metrics logic duplicated across 2 implementations
- Subscription pattern (Subscription inner class) duplicated
- Confusion about which implementation to use
- **Dead code**: EventBusOptimized exists but is never instantiated in production

**Evidence**:
```csharp
// CoreServicesExtensions.cs:56 - Only EventBus is registered
services.AddSingleton<EventBus>(sp => {
    ILogger<EventBus>? logger = sp.GetService<ILogger<EventBus>>();
    return new EventBus(logger);
});
services.AddSingleton<IEventBus>(sp => sp.GetRequiredService<EventBus>());
```

**Relationship to Commits**:
- Phase 1-4: Single EventBus implementation
- Phase 5: Metrics added to EventBus
- Phase 6: EventBusOptimized created as parallel implementation
- **Phase 6 introduced the architectural debt**

**Recommendation**:
```csharp
// OPTION 1: Strategy Pattern
public class EventBusFactory {
    public IEventBus Create(EventBusStrategy strategy) {
        return strategy switch {
            EventBusStrategy.Standard => new EventBusStandard(),
            EventBusStrategy.Optimized => new EventBusOptimized(),
            _ => throw new ArgumentException()
        };
    }
}

// OPTION 2: Feature Toggle in Single Implementation
public class EventBus : IEventBus {
    private readonly bool _enableOptimizations;

    public EventBus(ILogger logger, bool enableOptimizations = true) {
        _enableOptimizations = enableOptimizations;
        // Use cached handlers if optimizations enabled
    }
}

// OPTION 3: Replace EventBus with EventBusOptimized
// - Delete EventBus.cs
// - Rename EventBusOptimized → EventBus
// - Update DI registration
```

---

### Issue #2: ScriptBase Violates Single Responsibility Principle

**Severity**: Critical
**Introduced**: Phase 3 (commit 0265c49)
**Location**: `/PokeSharp.Game.Scripting/Runtime/ScriptBase.cs` (592 lines)

**Problem**:
ScriptBase handles 5 distinct responsibilities in a single 592-line class:

1. **Lifecycle Management** (lines 100-228):
   - Initialize(), RegisterEventHandlers(), OnUnload()
   - Subscription tracking (line 88: `List<IDisposable> subscriptions`)

2. **Event Subscription** (lines 230-435):
   - On<TEvent>() - Generic subscription
   - OnEntity<TEvent>() - Entity-filtered subscription
   - OnTile<TEvent>() - Tile-position-filtered subscription

3. **State Management** (lines 437-532):
   - Get<T>() - Component retrieval by type (NOT by key)
   - Set<T>() - Component mutation
   - Warning: `key` parameter unused (architectural debt)

4. **Event Publishing** (lines 534-591):
   - Publish<TEvent>() - Custom event publishing

5. **Context Management** (lines 92-97):
   - ScriptContext property storage

**Architecture Violations**:
- ❌ **Single Responsibility**: 5 responsibilities in one class
- ❌ **God Object Anti-Pattern**: Central class with too many concerns
- ❌ **High Coupling**: ScriptBase depends on IEventBus, World, Entity, ILogger, IScriptingApiProvider
- ❌ **Low Cohesion**: Lifecycle, events, state, and publishing are loosely related

**Impact**:
- 592 lines makes testing difficult
- Changes to state management affect event subscriptions
- Hard to extend without modifying the class
- New script developers face steep learning curve
- Future features (e.g., coroutines, async handlers) will bloat this class further

**Evidence**:
```csharp
// ScriptBase.cs - 5 responsibilities
public abstract class ScriptBase
{
    // RESPONSIBILITY 1: Lifecycle
    public virtual void Initialize(ScriptContext ctx) { }
    public virtual void RegisterEventHandlers(ScriptContext ctx) { }
    public virtual void OnUnload() { }

    // RESPONSIBILITY 2: Event Subscriptions
    protected void On<TEvent>(Action<TEvent> handler, int priority = 500) { }
    protected void OnEntity<TEvent>(Entity entity, Action<TEvent> handler) { }
    protected void OnTile<TEvent>(Vector2 tilePos, Action<TEvent> handler) { }

    // RESPONSIBILITY 3: State Management
    protected T Get<T>(string key, T defaultValue = default) { }
    protected void Set<T>(string key, T value) { }

    // RESPONSIBILITY 4: Event Publishing
    protected void Publish<TEvent>(TEvent evt) { }

    // RESPONSIBILITY 5: Context
    protected ScriptContext Context { get; private set; }
}
```

**Relationship to Commits**:
- Phase 1-2: No ScriptBase (pre-unification)
- Phase 3: ScriptBase introduced at 592 lines (all-in-one design)
- Phase 4-6: No refactoring performed

**Recommendation**:
```csharp
// DECOMPOSE into separate concerns:

// 1. Lifecycle (base class)
public abstract class ScriptBase {
    public virtual void Initialize(ScriptContext ctx) { }
    public virtual void RegisterEventHandlers(ScriptContext ctx) { }
    public virtual void OnUnload() { }
}

// 2. Event Subscription (mixin/interface)
public interface IEventSubscriber {
    void On<TEvent>(Action<TEvent> handler, int priority = 500);
    void OnEntity<TEvent>(Entity entity, Action<TEvent> handler);
    void OnTile<TEvent>(Vector2 tilePos, Action<TEvent> handler);
}

// 3. State Management (separate service)
public class ScriptState {
    public T Get<T>(string key, T defaultValue = default);
    public void Set<T>(string key, T value);
}

// 4. Event Publishing (delegate to EventBus)
public interface IEventPublisher {
    void Publish<TEvent>(TEvent evt);
}

// 5. Composition
public abstract class ModernScriptBase : ScriptBase {
    protected IEventSubscriber Events { get; }
    protected ScriptState State { get; }
    protected IEventPublisher Publisher { get; }
}
```

---

## 🟠 HIGH PRIORITY ISSUES

### Issue #3: ScriptAttachmentSystem Violates Command-Query Separation

**Severity**: High
**Introduced**: Phase 3 (commit 0265c49)
**Location**: `/PokeSharp.Game.Scripting/Systems/ScriptAttachmentSystem.cs` (410 lines)

**Problem**:
`Update()` method performs both queries AND mutations on `ScriptAttachment` components:

```csharp
// ScriptAttachmentSystem.cs:82-155
public override void Update(World world, float deltaTime)
{
    world.Query(
        new QueryDescription().WithAll<ScriptAttachment>(),
        (Entity entity, ref ScriptAttachment attachment) =>
        {
            // MUTATION 1: Set IsActive = false on errors (lines 98, 107, 119, 133, 153)
            attachment.IsActive = false;

            // MUTATION 2: Set ScriptInstance (line 199)
            attachment.ScriptInstance = scriptInstance;

            // MUTATION 3: Set ScriptInstance = null on detach (line 363)
            attachment.ScriptInstance = null;
        }
    );
}
```

**Architecture Violations**:
- ❌ **Command-Query Separation**: Update() should only execute, not modify state
- ❌ **Hidden Side Effects**: Mutations buried inside query callback
- ❌ **Race Conditions**: Multiple systems could query while Update() mutates
- ❌ **Testability**: Cannot test queries without triggering mutations

**Impact**:
- Debugging is difficult (state changes hidden in loop)
- Cannot replay queries without side effects
- Potential race conditions with other systems querying ScriptAttachment
- Violates ECS best practice of separating queries from mutations

**Relationship to Commits**:
- Phase 3: System introduced with mutation-in-query pattern
- Phase 4-6: Pattern persists

**Recommendation**:
```csharp
// SEPARATE queries from mutations:

public override void Update(World world, float deltaTime)
{
    // STEP 1: QUERY (read-only)
    var attachmentsToProcess = new List<(Entity, ScriptAttachment)>();
    world.Query(
        new QueryDescription().WithAll<ScriptAttachment>(),
        (Entity entity, ref ScriptAttachment attachment) =>
        {
            if (attachment.IsActive && attachment.ScriptInstance != null) {
                attachmentsToProcess.Add((entity, attachment));
            }
        }
    );

    // STEP 2: COMMAND (mutations)
    foreach (var (entity, attachment) in attachmentsToProcess) {
        ExecuteScript(world, entity, attachment, deltaTime);

        if (hasError) {
            world.Set(entity, attachment with { IsActive = false });
        }
    }
}
```

---

### Issue #4: ModLoader Tightly Coupled to File System

**Severity**: High
**Introduced**: Phase 5 (commit 34817d1)
**Location**: `/PokeSharp.Game.Scripting/Modding/ModLoader.cs` (380 lines)

**Problem**:
ModLoader directly accesses file system APIs (`Directory.GetDirectories`, `File.Exists`, `File.ReadAllText`) instead of using an abstraction:

```csharp
// ModLoader.cs:124-159
private List<ModManifest> DiscoverMods()
{
    foreach (string modDirectory in Directory.GetDirectories(_modsBasePath)) // ❌ Direct FS access
    {
        string manifestPath = Path.Combine(modDirectory, ModManifestFileName);

        if (!File.Exists(manifestPath)) // ❌ Direct FS access
            continue;

        ModManifest? manifest = ParseManifest(manifestPath, modDirectory);
    }
}

private ModManifest? ParseManifest(string manifestPath, string modDirectory)
{
    string json = File.ReadAllText(manifestPath); // ❌ Direct FS access
    ModManifest? manifest = JsonSerializer.Deserialize<ModManifest>(json);
}
```

**Architecture Violations**:
- ❌ **Dependency Inversion Principle**: High-level module depends on low-level I/O
- ❌ **Testability**: Cannot unit test without real file system
- ❌ **Platform Coupling**: Assumes local file system (breaks on cloud, mobile, etc.)
- ❌ **Open/Closed Principle**: Cannot swap file source (e.g., embedded resources, network)

**Impact**:
- Integration tests require real file system setup
- Cannot mock file system for unit tests
- Cannot load mods from alternative sources (embedded, cloud, pak files)
- Hard to test error handling (e.g., permission denied, disk full)

**Relationship to Commits**:
- Phase 5: ModLoader introduced with direct file system access
- Phase 6: No refactoring

**Recommendation**:
```csharp
// ABSTRACT file system access:

public interface IModFileSystem
{
    IEnumerable<string> GetModDirectories(string basePath);
    bool FileExists(string path);
    string ReadAllText(string path);
}

// Production implementation
public class FileSystemModProvider : IModFileSystem
{
    public IEnumerable<string> GetModDirectories(string basePath)
        => Directory.GetDirectories(basePath);

    public bool FileExists(string path)
        => File.Exists(path);

    public string ReadAllText(string path)
        => File.ReadAllText(path);
}

// Test implementation
public class InMemoryModProvider : IModFileSystem
{
    private readonly Dictionary<string, string> _files = new();

    public void AddMod(string path, string manifestJson)
        => _files[path] = manifestJson;
}

// Updated ModLoader
public class ModLoader
{
    private readonly IModFileSystem _fileSystem;

    public ModLoader(IModFileSystem fileSystem, ...) {
        _fileSystem = fileSystem;
    }

    private List<ModManifest> DiscoverMods()
    {
        foreach (string modDirectory in _fileSystem.GetModDirectories(_modsBasePath))
        {
            if (!_fileSystem.FileExists(manifestPath))
                continue;

            string json = _fileSystem.ReadAllText(manifestPath);
        }
    }
}
```

---

### Issue #5: EventPool and EventBus Have Circular Dependency Risk

**Severity**: High
**Introduced**: Phase 6 (commit 946009b)
**Location**:
- `/PokeSharp.Engine.Core/Events/EventPool.cs` (117 lines)
- Extension method `PublishPooled<TEvent>()` (lines 105-116)

**Problem**:
EventPool extension method `PublishPooled()` creates tight coupling between pooling and event bus:

```csharp
// EventPool.cs:105-116
public static void PublishPooled<TEvent>(
    this IEventBus eventBus,
    TEvent evt,
    EventPool<TEvent> pool)
    where TEvent : class, new()
{
    try {
        eventBus.Publish(evt);  // ❌ EventPool knows about IEventBus
    }
    finally {
        pool.Return(evt);       // Pool lifecycle tied to publish lifecycle
    }
}
```

**Architecture Violations**:
- ❌ **Circular Dependency**: EventPool → IEventBus, IEventBus may need EventPool for pooling
- ❌ **Tight Coupling**: Pool return logic embedded in event publishing
- ❌ **Violation of Separation of Concerns**: Pooling logic mixed with event bus logic
- ❌ **Leaky Abstraction**: Callers must manage pool lifecycle manually

**Impact**:
- Cannot use EventPool without IEventBus reference
- Risk of forgetting to return events to pool
- Handlers might hold references to pooled events (use-after-return bug)
- No protection against pool corruption (event mutation after return)

**Relationship to Commits**:
- Phase 6: EventPool introduced with `PublishPooled()` extension

**Recommendation**:
```csharp
// OPTION 1: Remove extension, use try-finally pattern
var pool = EventPool<TickEvent>.Shared;
var evt = pool.Rent();
try {
    evt.DeltaTime = deltaTime;
    eventBus.Publish(evt);
}
finally {
    pool.Return(evt);
}

// OPTION 2: IEventBus owns pooling internally
public interface IEventBus {
    void PublishPooled<TEvent>(Action<TEvent> configureEvent)
        where TEvent : class, new();
}

// Usage:
eventBus.PublishPooled<TickEvent>(evt => evt.DeltaTime = deltaTime);

// OPTION 3: Event Bus manages pool lifecycle
public class EventBus : IEventBus {
    private readonly EventPool<TickEvent> _tickPool = new();

    public void Publish<TEvent>(TEvent evt) {
        // Publish to handlers

        // Auto-return to pool if event type is pooled
        if (evt is IPoolable poolable) {
            poolable.ReturnToPool();
        }
    }
}
```

---

## 🟡 MEDIUM PRIORITY ISSUES

### Issue #6: IEventMetrics Interface Exposes Implementation Details

**Severity**: Medium
**Introduced**: Phase 5 (commit 34817d1)
**Location**: Referenced in EventBus.cs, EventBusOptimized.cs

**Problem**:
The `IEventMetrics` interface requires event bus implementations to track handler IDs and call metrics methods at specific points:

```csharp
// EventBus.cs:40-42
public IEventMetrics? Metrics { get; set; }

// EventBus.cs:138
Metrics?.RecordSubscription(eventType.Name, handlerId);

// EventBus.cs:185
Metrics?.RecordUnsubscription(eventType.Name, handlerId);
```

**Architecture Violations**:
- ❌ **Interface Segregation**: IEventBus forced to handle metrics concerns
- ❌ **Open/Closed Principle**: Cannot change metrics strategy without modifying EventBus
- ❌ **Single Responsibility**: EventBus handles both event routing AND metrics collection
- ❌ **Tell, Don't Ask**: EventBus must know when to call metrics methods

**Impact**:
- Metrics logic scattered across EventBus publish/subscribe methods
- Cannot swap metrics implementation without changing EventBus
- Metrics collection adds cognitive load to EventBus maintenance
- Duplicated metrics calls in EventBus and EventBusOptimized

**Relationship to Commits**:
- Phase 5: IEventMetrics added to EventBus
- Phase 6: Duplicated to EventBusOptimized

**Recommendation**:
```csharp
// OPTION 1: Decorator Pattern (Proxy)
public class MetricsEventBusDecorator : IEventBus {
    private readonly IEventBus _inner;
    private readonly IEventMetrics _metrics;

    public void Publish<TEvent>(TEvent evt) {
        var sw = Stopwatch.StartNew();
        _inner.Publish(evt);
        sw.Stop();
        _metrics.RecordPublish(typeof(TEvent).Name, sw.ElapsedTicks);
    }
}

// DI registration:
services.AddSingleton<EventBus>();
services.AddSingleton<IEventBus>(sp => {
    var eventBus = sp.GetRequiredService<EventBus>();
    var metrics = sp.GetRequiredService<IEventMetrics>();
    return new MetricsEventBusDecorator(eventBus, metrics);
});

// OPTION 2: Observer Pattern (Event-Based Metrics)
public class EventBus : IEventBus {
    public event Action<string, long>? PublishCompleted;
    public event Action<string, int>? Subscribed;

    public void Publish<TEvent>(TEvent evt) {
        // Publish logic
        PublishCompleted?.Invoke(typeof(TEvent).Name, elapsedTicks);
    }
}

// Metrics collector subscribes to events
public class EventMetricsCollector {
    public EventMetricsCollector(EventBus eventBus) {
        eventBus.PublishCompleted += (name, ticks) => RecordPublish(name, ticks);
        eventBus.Subscribed += (name, id) => RecordSubscription(name, id);
    }
}
```

---

### Issue #7: ScriptContext Lacks Clear Ownership Semantics

**Severity**: Medium
**Introduced**: Phase 3 (commit 0265c49)
**Location**: `/PokeSharp.Game.Scripting/Runtime/ScriptBase.cs` (lines 92-97, 137-140)

**Problem**:
ScriptContext is passed to `Initialize()` and stored in a property, but ownership is unclear:

```csharp
// ScriptBase.cs:92-97
protected ScriptContext Context { get; private set; } = null!;

// ScriptBase.cs:137-140
public virtual void Initialize(ScriptContext ctx)
{
    Context = ctx ?? throw new ArgumentNullException(nameof(ctx));
}
```

**Architecture Questions**:
- ❓ Who owns ScriptContext? (ScriptBase or caller?)
- ❓ Is it safe for multiple scripts to share the same context?
- ❓ Should ScriptContext be disposed when script unloads?
- ❓ Can Context be mutated after Initialize()?
- ❓ What happens if Initialize() is called twice?

**Architecture Violations**:
- ❌ **Unclear Lifecycle**: Context lifetime not documented
- ❌ **Potential Sharing Issues**: No protection against context reuse
- ❌ **No Immutability**: Context can be replaced after initialization
- ❌ **Missing Validation**: No check if Initialize() called multiple times

**Impact**:
- Risk of context being shared between scripts (unintended side effects)
- No guarantee Context is valid during script execution
- Cannot detect if script is re-initialized
- Hard to debug context-related bugs

**Relationship to Commits**:
- Phase 3: ScriptBase introduced with mutable Context property
- Phase 4-6: No refactoring

**Recommendation**:
```csharp
// OPTION 1: Immutable Context (Constructor Injection)
public abstract class ScriptBase
{
    protected ScriptContext Context { get; }

    protected ScriptBase(ScriptContext context) {
        Context = context ?? throw new ArgumentNullException(nameof(context));
    }

    // Remove Initialize() method
}

// OPTION 2: Explicit Ownership Tracking
public abstract class ScriptBase
{
    private ScriptContext? _context;
    protected ScriptContext Context => _context ?? throw new InvalidOperationException("Not initialized");

    public virtual void Initialize(ScriptContext ctx)
    {
        if (_context != null)
            throw new InvalidOperationException("Already initialized");

        _context = ctx ?? throw new ArgumentNullException(nameof(ctx));
    }
}

// OPTION 3: Context as Parameter (No Storage)
public abstract class ScriptBase
{
    public virtual void Initialize(ScriptContext ctx) { }
    public virtual void RegisterEventHandlers(ScriptContext ctx) { }

    protected void On<TEvent>(ScriptContext ctx, Action<TEvent> handler) { }
    protected T Get<T>(ScriptContext ctx, string key) { }
}
```

---

## Summary Table

| Issue | Severity | Component | Phase Introduced | Status |
|-------|----------|-----------|------------------|--------|
| #1: Dual EventBus Implementations | Critical | EventBus/EventBusOptimized | Phase 6 (946009b) | ❌ Active |
| #2: ScriptBase God Object | Critical | ScriptBase | Phase 3 (0265c49) | ❌ Active |
| #3: ScriptAttachmentSystem Command-Query | High | ScriptAttachmentSystem | Phase 3 (0265c49) | ❌ Active |
| #4: ModLoader File System Coupling | High | ModLoader | Phase 5 (34817d1) | ❌ Active |
| #5: EventPool Circular Dependency | High | EventPool | Phase 6 (946009b) | ❌ Active |
| #6: IEventMetrics Interface Violation | Medium | IEventBus | Phase 5 (34817d1) | ❌ Active |
| #7: ScriptContext Ownership | Medium | ScriptBase | Phase 3 (0265c49) | ❌ Active |

---

## Architectural Debt Summary

**Total Lines Affected**: ~2,100 lines across 7 issues
**Estimated Refactoring Effort**: 2-3 weeks (with comprehensive testing)

**Priority Order for Remediation**:
1. **Issue #1** (Critical): Consolidate EventBus implementations → 3-5 days
2. **Issue #2** (Critical): Decompose ScriptBase → 5-7 days
3. **Issue #4** (High): Abstract file system in ModLoader → 2-3 days
4. **Issue #3** (High): Separate queries from mutations in ScriptAttachmentSystem → 2-3 days
5. **Issue #5** (High): Decouple EventPool from IEventBus → 1-2 days
6. **Issue #6** (Medium): Refactor metrics to decorator pattern → 2-3 days
7. **Issue #7** (Medium): Clarify ScriptContext ownership → 1 day

---

## Positive Findings

Despite the issues above, the architecture has several strengths:

✅ **Clean Interface Design**: IEventBus is simple and testable
✅ **Dependency Injection**: Proper DI usage in CoreServicesExtensions.cs
✅ **Event-Driven Architecture**: Good separation of concerns via events
✅ **Testability**: Most classes accept interfaces (ILogger, IEventBus, etc.)
✅ **Extensibility**: ScriptBase provides good extension points for mods

---

## Recommendations for Next Steps

1. **Phase 7 Planning**: Address Issues #1 and #2 before adding new features
2. **Architectural Review**: Establish architecture review process for future phases
3. **Refactoring Sprints**: Dedicate 1-2 sprints to architectural debt reduction
4. **Documentation**: Document ownership semantics for ScriptContext
5. **Testing**: Add integration tests for ModLoader with mocked file system

---

**End of Architecture Analysis Report**
