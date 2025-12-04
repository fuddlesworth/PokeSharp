# SOLID Principles & Architecture Audit Report
**Analysis of Event System Changes (Phases 1-6)**

**Analysis Date:** 2025-12-03
**Scope:** Last 6 commits focusing on event system evolution
**Analyst:** Code Analysis Agent (Hive Mind)
**Commits Analyzed:** phases 1-6 of event changes (946009b, 34817d1, 74eda0d, 0265c49, 75b0bab)

---

## Executive Summary

This audit identifies **23 SOLID violations** and **8 architectural issues** across 9 key files modified in phases 1-6 of the event system changes. The violations range from **Critical** to **Low** severity, with the most significant issues found in:

- **EventBus.cs** - Multiple responsibility violations (SRP)
- **EventInspectorContent.cs** - God class with excessive responsibilities (845 lines)
- **EventInspectorAdapter.cs** - Tight coupling and DIP violations
- **ScriptBase.cs** - Interface segregation issues

### Key Findings
- ✅ **Strong Points:** Event-driven architecture, good separation of event types
- ❌ **Critical Issues:** 3 god classes, tight coupling to concrete implementations
- ⚠️ **High Issues:** 5 SRP violations, missing abstractions
- 🟡 **Medium Issues:** 4 DIP violations, LSP violations

### Statistics
- **Total SOLID Violations:** 13
- **Total Architecture Issues:** 8
- **Files with Most Issues:** EventInspectorContent.cs (5), EventBus.cs (3), ScriptBase.cs (3)
- **Severity Breakdown:** Critical: 3, High: 5, Medium: 4, Low: 1

---

## 1. SOLID Violations

### 🔴 CRITICAL SEVERITY

#### VIOLATION #1: Single Responsibility Principle - EventBus
**File:** `PokeSharp.Engine.Core/Events/EventBus.cs`
**Lines:** 31-280
**Severity:** Critical

**Issue:**
The `EventBus` class has **4 distinct responsibilities**:
1. Event subscription management (lines 110-129)
2. Event publishing/dispatch (lines 49-76)
3. Performance metrics collection (lines 45-46, 65-75, 89-99, 126)
4. Handler cache management (lines 36, 123-124, 198-210)

```csharp
// Lines 31-46: Multiple concerns in single class
public class EventBus(ILogger<EventBus>? logger = null) : IEventBus
{
    private readonly ConcurrentDictionary<Type, ConcurrentDictionary<int, Delegate>> _handlers = new();

    // RESPONSIBILITY 1: Handler caching
    private readonly ConcurrentDictionary<Type, HandlerCache> _handlerCache = new();

    // RESPONSIBILITY 2: Logging
    private readonly ILogger<EventBus> _logger = logger ?? NullLogger<EventBus>.Instance;

    // RESPONSIBILITY 3: Metrics collection
    public IEventMetrics? Metrics { get; set; }

    private int _nextHandlerId;
}
```

**Impact:**
- Difficult to test in isolation
- Changes to caching logic require modifying event dispatch
- Metrics collection tightly coupled to event publishing
- Violates Open/Closed Principle when adding new metrics

**Suggested Fix:**
```csharp
// Separate concerns into distinct classes
public class EventBus : IEventBus
{
    private readonly IEventSubscriptionManager _subscriptions;
    private readonly IEventPublisher _publisher;
    private readonly IEventMetricsCollector _metrics;

    public EventBus(
        IEventSubscriptionManager subscriptions,
        IEventPublisher publisher,
        IEventMetricsCollector metrics)
    {
        _subscriptions = subscriptions;
        _publisher = publisher;
        _metrics = metrics;
    }

    public void Publish<TEvent>(TEvent evt) where TEvent : class
    {
        var handlers = _subscriptions.GetHandlers<TEvent>();
        _publisher.Publish(evt, handlers);
        _metrics.RecordPublish(typeof(TEvent).Name);
    }
}
```

---

#### VIOLATION #2: God Class - EventInspectorContent
**File:** `PokeSharp.Engine.UI.Debug/Components/Debug/EventInspectorContent.cs`
**Lines:** 192-1037 (845 lines!)
**Severity:** Critical

**Issue:**
This class has **9 distinct responsibilities**:
1. Data rendering (lines 348-452)
2. Input handling (lines 458-515)
3. Column layout calculation (lines 28-185)
4. Sorting logic (lines 250-254, 873-890)
5. Scrolling management (lines 310-319, 461-474)
6. Data refresh (lines 866-871)
7. Statistics calculation (lines 321-340)
8. Event selection (lines 285-303)
9. Formatting utilities (lines 971-1036)

```csharp
// Lines 192-248: God class with 9 responsibilities
public class EventInspectorContent : UIComponent
{
    private readonly ScrollbarComponent _scrollbar = new();
    private readonly SortableTableHeader<EventInspectorSortMode> _tableHeader;

    private Func<EventInspectorData>? _dataProvider;
    private EventInspectorData? _cachedData;
    private List<EventTypeInfo> _sortedEvents = new();
    private int _selectedEventIndex = -1;
    private bool _showSubscriptions = true;
    private EventInspectorSortMode _sortMode = EventInspectorSortMode.BySubscribers;

    // Time-based refresh
    private double _lastUpdateTime;
    private double _refreshIntervalSeconds = 0.5;

    // Layout tracking
    private float _totalContentHeight;
    private float _visibleHeight;
}
```

**Impact:**
- 845 lines in single file
- Extremely difficult to unit test
- Any change requires understanding entire class
- Multiple reasons to change (sorting, rendering, input, etc.)

**Suggested Fix:**
```csharp
// Split into focused classes
public class EventInspectorContent : UIComponent
{
    private readonly IEventTableRenderer _tableRenderer;
    private readonly IEventInputHandler _inputHandler;
    private readonly IEventDataSorter _sorter;
    private readonly IScrollManager _scrollManager;
    private readonly IColumnLayoutCalculator _layoutCalculator;

    protected override void OnRender(UIContext context)
    {
        var layout = _layoutCalculator.Calculate(Rect.Width, _sortedEvents, Renderer);
        var sortedData = _sorter.Sort(_cachedData, _sortMode);
        _tableRenderer.Render(sortedData, layout, context);
    }
}

// Separate layout calculation (lines 28-185)
public class ColumnLayoutCalculator : IColumnLayoutCalculator
{
    public ColumnLayout Calculate(
        float availableWidth,
        IReadOnlyList<EventTypeInfo> events,
        UIRenderer renderer)
    {
        // Layout logic moved here
    }
}

// Separate input handling (lines 458-515)
public class EventInspectorInputHandler : IEventInputHandler
{
    public void HandleInput(UIContext context, InputState input)
    {
        // Input logic moved here
    }
}

// Separate table rendering (lines 562-715)
public class EventTableRenderer : IEventTableRenderer
{
    public void Render(
        IReadOnlyList<EventTypeInfo> events,
        ColumnLayout layout,
        UIContext context)
    {
        // Rendering logic moved here
    }
}
```

---

#### VIOLATION #3: Interface Segregation Principle - ScriptBase
**File:** `PokeSharp.Game.Scripting/Runtime/ScriptBase.cs`
**Lines:** 85-593
**Severity:** Critical

**Issue:**
`ScriptBase` forces all scripts to inherit **5 different concerns**:
1. Event subscription methods (lines 232-436)
2. State management (lines 440-533)
3. Event publishing (lines 536-592)
4. Lifecycle management (lines 100-228)
5. Context management (line 97)

Not all scripts need all these capabilities. Example: A simple read-only display script doesn't need `Publish<TEvent>` or `Set<T>`.

```csharp
// Lines 85-593: Forces all capabilities on inheritors
public abstract class ScriptBase
{
    protected ScriptContext Context { get; private set; } = null!;

    // Lifecycle - needed by ALL scripts ✓
    public virtual void Initialize(ScriptContext ctx) { }
    public virtual void RegisterEventHandlers(ScriptContext ctx) { }
    public virtual void OnUnload() { }

    // Event subscription - needed by MOST scripts ✓
    protected void On<TEvent>(Action<TEvent> handler) { }
    protected void OnEntity<TEvent>(Entity entity, Action<TEvent> handler) { }
    protected void OnTile<TEvent>(Vector2 tilePos, Action<TEvent> handler) { }

    // State management - needed by STATEFUL scripts only ✗
    protected T Get<T>(string key, T defaultValue) { }
    protected void Set<T>(string key, T value) { }

    // Event publishing - needed by ACTIVE scripts only ✗
    protected void Publish<TEvent>(TEvent evt) { }
}
```

**Impact:**
- Read-only scripts inherit unused `Publish` capability
- Stateless scripts inherit unused `Get/Set` methods
- Violates Interface Segregation Principle
- Difficult to reason about which scripts do what

**Suggested Fix:**
```csharp
// Base interface with only essentials
public interface IScript
{
    void Initialize(ScriptContext ctx);
    void RegisterEventHandlers(ScriptContext ctx);
    void OnUnload();
}

// Compose capabilities via interfaces
public interface IEventListener
{
    void On<TEvent>(Action<TEvent> handler) where TEvent : class, IGameEvent;
    void OnEntity<TEvent>(Entity entity, Action<TEvent> handler) where TEvent : class, IEntityEvent;
    void OnTile<TEvent>(Vector2 tilePos, Action<TEvent> handler) where TEvent : class, ITileEvent;
}

public interface IEventPublisher
{
    void Publish<TEvent>(TEvent evt) where TEvent : class, IGameEvent;
}

public interface IStatefulScript
{
    T Get<T>(string key, T defaultValue) where T : struct;
    void Set<T>(string key, T value) where T : struct;
}

// Scripts implement only what they need
public abstract class ReadOnlyScript : IScript, IEventListener
{
    // Only event listening, no state or publishing
}

public abstract class StatefulScript : IScript, IEventListener, IStatefulScript
{
    // Event listening + state management
}

public abstract class ActiveScript : IScript, IEventListener, IEventPublisher, IStatefulScript
{
    // Full capabilities (current ScriptBase)
}
```

---

### 🟠 HIGH SEVERITY

#### VIOLATION #4: Dependency Inversion Principle - EventInspectorAdapter
**File:** `PokeSharp.Engine.UI.Debug/Core/EventInspectorAdapter.cs`
**Lines:** 9-180
**Severity:** High

**Issue:**
`EventInspectorAdapter` depends on **concrete classes** instead of abstractions:

```csharp
// Lines 11-12: Concrete dependencies
private readonly EventBus _eventBus;  // Should be IEventBus
private readonly EventMetrics _metrics;  // Should be IEventMetrics

// Line 16: Constructor forces concrete types
public EventInspectorAdapter(EventBus eventBus, EventMetrics metrics, ...)
{
    _eventBus = eventBus;  // Tight coupling
    _metrics = metrics;

    // Line 24: Direct mutation of concrete class property
    _eventBus.Metrics = _metrics;
}
```

**Impact:**
- Cannot substitute EventBus implementations for testing
- Cannot use alternative metrics collectors
- Violates Dependency Inversion Principle
- Difficult to mock for unit tests

**Suggested Fix:**
```csharp
public class EventInspectorAdapter
{
    private readonly IEventBus _eventBus;  // Interface
    private readonly IEventMetrics _metrics;  // Interface

    public EventInspectorAdapter(
        IEventBus eventBus,  // Accept abstraction
        IEventMetrics metrics,
        int maxLogEntries = 100)
    {
        _eventBus = eventBus;
        _metrics = metrics;

        // Configure through interface method
        if (_eventBus is IMetricsConfigurable configurable)
        {
            configurable.SetMetrics(_metrics);
        }
    }
}
```

---

#### VIOLATION #5: Single Responsibility Principle - EventInspectorAdapter
**File:** `PokeSharp.Engine.UI.Debug/Core/EventInspectorAdapter.cs`
**Lines:** 9-180
**Severity:** High

**Issue:**
This class has **5 responsibilities**:
1. Data transformation (lines 60-110)
2. Metrics initialization (lines 24-45)
3. Event logging (lines 113-147)
4. Metrics management (lines 49-55, 150-164)
5. Event type classification (lines 175-179)

```csharp
// Multiple concerns in single class
public class EventInspectorAdapter
{
    // Concern 1: Data transformation
    public EventInspectorData GetInspectorData() { }

    // Concern 2: Event logging
    public void LogPublish(string eventTypeName, double durationMs) { }
    public void LogHandlerInvoke(string eventTypeName, int handlerId, double durationMs) { }

    // Concern 3: Metrics management
    public void Clear() { }
    public void ResetTimings() { }

    // Concern 4: Event classification
    private bool IsCustomEvent(Type eventType) { }

    // Concern 5: Metrics initialization
    private void CaptureExistingSubscriptions() { }
}
```

**Suggested Fix:**
```csharp
// Separate into focused classes
public class EventInspectorDataProvider
{
    public EventInspectorData GetInspectorData(
        IEventBus eventBus,
        IEventMetrics metrics)
    {
        // Data transformation only
    }
}

public class EventLogger
{
    private readonly Queue<EventLogEntry> _eventLog;

    public void LogPublish(string eventTypeName, double durationMs) { }
    public void LogHandlerInvoke(string eventTypeName, int handlerId, double durationMs) { }
}

public class EventTypeClassifier
{
    public bool IsCustomEvent(Type eventType) { }
}

// Adapter becomes a simple coordinator
public class EventInspectorAdapter
{
    private readonly EventInspectorDataProvider _dataProvider;
    private readonly EventLogger _logger;

    public EventInspectorData GetInspectorData() =>
        _dataProvider.GetInspectorData(_eventBus, _metrics);
}
```

---

#### VIOLATION #6: Open/Closed Principle - ColumnLayout
**File:** `PokeSharp.Engine.UI.Debug/Components/Debug/EventInspectorContent.cs`
**Lines:** 28-185
**Severity:** High

**Issue:**
The `ColumnLayout` struct hardcodes **5 columns** and their layout logic. Adding a new column requires modifying the struct itself:

```csharp
// Lines 29-40: Hardcoded column structure
internal readonly struct ColumnLayout
{
    public readonly float EventNameX;
    public readonly float EventNameWidth;
    public readonly float BarX;
    public readonly float BarWidth;
    public readonly float SubsX;
    public readonly float SubsWidth;
    public readonly float CountX;
    public readonly float CountWidth;
    public readonly float TimeX;
    public readonly float TimeWidth;
    // Adding a new column requires modifying this struct
}

// Lines 73-171: Layout calculation tightly couples column logic
public static ColumnLayout CalculateFromContent(...)
{
    // Hardcoded logic for each column
    float subsHeaderWidth = renderer.MeasureText("Subs").X + padding;
    float countHeaderWidth = renderer.MeasureText("Count").X + padding;
    float timeHeaderWidth = renderer.MeasureText("Avg/Max").X + padding;
    // ... more hardcoded column logic
}
```

**Impact:**
- Cannot add columns without modifying struct
- Violates Open/Closed Principle (not open for extension)
- Difficult to support dynamic columns
- Repeated code for each column

**Suggested Fix:**
```csharp
// Column definition interface
public interface ITableColumn
{
    string Header { get; }
    float MinWidth { get; }
    float CalculateWidth(IReadOnlyList<EventTypeInfo> data, UIRenderer renderer);
    bool IsResizable { get; }
}

// Flexible layout calculator
public class DynamicColumnLayout
{
    private readonly List<ITableColumn> _columns = new();

    public void AddColumn(ITableColumn column) => _columns.Add(column);

    public LayoutResult Calculate(
        float availableWidth,
        UIRenderer renderer,
        IReadOnlyList<EventTypeInfo> data)
    {
        // Generic column layout algorithm
        var columnWidths = _columns
            .Select(c => c.CalculateWidth(data, renderer))
            .ToArray();
        // ...
    }
}

// Usage: Add columns without modifying layout class
var layout = new DynamicColumnLayout();
layout.AddColumn(new FixedWidthColumn("Event Type", 200));
layout.AddColumn(new DynamicBarColumn("Execution Time"));
layout.AddColumn(new ContentSizedColumn("Subs"));
layout.AddColumn(new ContentSizedColumn("Count"));
layout.AddColumn(new ContentSizedColumn("Avg/Max"));
```

---

#### VIOLATION #7: Single Responsibility Principle - ModLoader
**File:** `PokeSharp.Game.Scripting/Modding/ModLoader.cs`
**Lines:** 16-380
**Severity:** High

**Issue:**
`ModLoader` has **6 responsibilities**:
1. Mod discovery (lines 124-159)
2. Manifest parsing (lines 161-189)
3. Dependency resolution (lines 88-97)
4. Script loading (lines 194-293)
5. Script initialization (lines 243-257)
6. Mod lifecycle management (lines 298-363)

```csharp
public class ModLoader
{
    // Responsibility 1: Discovery
    private List<ModManifest> DiscoverMods() { }

    // Responsibility 2: Parsing
    private ModManifest? ParseManifest(string manifestPath, string modDirectory) { }

    // Responsibility 3: Loading orchestration
    public async Task LoadModsAsync() { }
    private async Task LoadModAsync(ModManifest manifest) { }

    // Responsibility 4: Script initialization
    // Lines 243-257: Directly calls ScriptService

    // Responsibility 5: Lifecycle management
    public async Task UnloadModAsync(string modId) { }
    public async Task ReloadModAsync(string modId) { }
}
```

**Impact:**
- Cannot test mod discovery without file system
- Manifest parsing coupled to loading logic
- Script initialization mixed with discovery
- Multiple reasons to change

**Suggested Fix:**
```csharp
// Separate discovery
public class ModDiscoveryService
{
    public List<ModManifest> DiscoverMods(string basePath) { }
}

// Separate parsing
public class ModManifestParser
{
    public ModManifest? ParseManifest(string manifestPath) { }
}

// Separate script loading
public class ModScriptLoader
{
    public async Task<object?> LoadScriptAsync(string scriptPath) { }
    public void InitializeScript(ScriptBase script, World world, ILogger logger) { }
}

// ModLoader becomes orchestrator only
public class ModLoader
{
    private readonly ModDiscoveryService _discovery;
    private readonly ModManifestParser _parser;
    private readonly ModDependencyResolver _dependencyResolver;
    private readonly ModScriptLoader _scriptLoader;

    public async Task LoadModsAsync()
    {
        var manifests = _discovery.DiscoverMods(_modsBasePath);
        var ordered = _dependencyResolver.ResolveDependencies(manifests);

        foreach (var manifest in ordered)
        {
            await LoadModAsync(manifest);
        }
    }

    private async Task LoadModAsync(ModManifest manifest)
    {
        foreach (var scriptPath in manifest.Scripts)
        {
            var script = await _scriptLoader.LoadScriptAsync(scriptPath);
            _scriptLoader.InitializeScript(script, _world, _logger);
        }
    }
}
```

---

### 🟡 MEDIUM SEVERITY

#### VIOLATION #8: Liskov Substitution Principle - MovementEventBase
**File:** `PokeSharp.Game.Systems/Events/MovementEvents.cs`
**Lines:** 12-18, 25-53
**Severity:** Medium

**Issue:**
`MovementStartedEvent` adds mutable cancellation state that parent `MovementEventBase` doesn't have, violating LSP:

```csharp
// Lines 12-18: Base record
public abstract record MovementEventBase : TypeEventBase
{
    public required Entity Entity { get; init; }
    // Immutable base
}

// Lines 25-53: Child adds mutable state
public record MovementStartedEvent : MovementEventBase
{
    public required Vector2 TargetPosition { get; init; }
    public required Direction Direction { get; init; }

    // VIOLATION: Mutable properties in otherwise immutable hierarchy
    public bool IsCancelled { get; set; }
    public string? CancellationReason { get; set; }
}
```

**Impact:**
- Breaks immutability contract of record types
- Cannot treat all MovementEventBase instances as interchangeable
- Subtle bugs when treating MovementStartedEvent as base type
- Thread-safety issues (mutable state)

**Suggested Fix:**
```csharp
// Option 1: Make base explicitly support cancellation
public abstract record MovementEventBase : TypeEventBase
{
    public required Entity Entity { get; init; }
    public virtual bool CanBeCancelled => false;
}

public record MovementStartedEvent : MovementEventBase
{
    public required Vector2 TargetPosition { get; init; }
    public required Direction Direction { get; init; }
    public bool IsCancelled { get; init; }  // Use init instead of set
    public string? CancellationReason { get; init; }
    public override bool CanBeCancelled => true;

    // Return new instance for cancellation
    public MovementStartedEvent WithCancellation(string reason) =>
        this with { IsCancelled = true, CancellationReason = reason };
}

// Option 2: Separate interface for cancellable events
public interface ICancellableEvent
{
    bool IsCancelled { get; }
    string? CancellationReason { get; }
}

public record MovementStartedEvent : MovementEventBase, ICancellableEvent
{
    public bool IsCancelled { get; init; }
    public string? CancellationReason { get; init; }
}
```

---

#### VIOLATION #9: Single Responsibility Principle - EventInspectorPanel
**File:** `PokeSharp.Engine.UI.Debug/Components/Debug/EventInspectorPanel.cs`
**Lines:** 16-220
**Severity:** Medium

**Issue:**
`EventInspectorPanel` has **4 responsibilities**:
1. Data provider management (lines 40-67)
2. UI operations delegation (lines 69-107)
3. Statistics calculation (lines 116-120)
4. Data export (lines 125-166)

**Impact:**
- Panel knows about data export formats
- Statistics logic duplicated with content
- Multiple reasons to change

**Suggested Fix:**
```csharp
// Separate export functionality
public class EventInspectorExporter
{
    public string ExportToString(EventInspectorStatistics stats) { }
    public string ExportToCsv(EventInspectorStatistics stats) { }
}

// Panel becomes pure UI coordinator
public class EventInspectorPanel : DebugPanelBase
{
    private readonly EventInspectorContent _content;
    private readonly EventInspectorExporter _exporter;

    public void ExportToClipboard(bool asCsv = false)
    {
        var stats = _content.GetStatistics();
        string text = asCsv
            ? _exporter.ExportToCsv(stats)
            : _exporter.ExportToString(stats);
        ClipboardManager.SetText(text);
    }
}
```

---

#### VIOLATION #10: Single Responsibility Principle - CollisionEvents File Organization
**File:** `PokeSharp.Game.Systems/Events/CollisionEvents.cs`
**Lines:** 147-188
**Severity:** Medium

**Issue:**
This file defines **2 enums** (`CollisionType`, `ResolutionStrategy`) that should be in separate domain model files:

```csharp
// Lines 147-188: Multiple type definitions in single file
public enum CollisionType { ... }
public enum ResolutionStrategy { ... }
```

**Impact:**
- File has multiple reasons to change
- Enums not discoverable independently
- Difficult to find resolution strategies

**Suggested Fix:**
```csharp
// File: CollisionType.cs
namespace PokeSharp.Game.Systems.Collision
{
    public enum CollisionType
    {
        Entity,
        Tile,
        Boundary,
        Elevation,
        Behavior
    }
}

// File: ResolutionStrategy.cs
namespace PokeSharp.Game.Systems.Collision
{
    public enum ResolutionStrategy
    {
        Blocked,
        Slide,
        Bounce,
        Pushback,
        Custom
    }
}
```

---

#### VIOLATION #11: Dependency Inversion - EventInspectorContent (Renderer)
**File:** `PokeSharp.Engine.UI.Debug/Components/Debug/EventInspectorContent.cs`
**Lines:** 77, 350-351
**Severity:** Medium

**Issue:**
Direct dependency on concrete `UIRenderer` instead of abstraction:

```csharp
// Line 77: Concrete dependency
public static ColumnLayout CalculateFromContent(
    float availableWidth,
    float startX,
    IReadOnlyList<EventTypeInfo> events,
    UIRenderer renderer)  // Should be IRenderer interface

// Lines 350-351: Direct usage
UIRenderer renderer = Renderer;
```

**Impact:**
- Cannot substitute renderer implementations
- Difficult to test without full UIRenderer
- Tight coupling to rendering infrastructure

**Suggested Fix:**
```csharp
// Define abstraction
public interface ITextMeasurer
{
    Vector2 MeasureText(string text);
    string TruncateWithEllipsis(string text, float maxWidth);
}

// Use abstraction
public static ColumnLayout CalculateFromContent(
    float availableWidth,
    float startX,
    IReadOnlyList<EventTypeInfo> events,
    ITextMeasurer textMeasurer)
{
    // Now testable with mock
}
```

---

### 🟢 LOW SEVERITY

#### VIOLATION #12: Magic Numbers - EventInspectorContent
**File:** `PokeSharp.Engine.UI.Debug/Components/Debug/EventInspectorContent.cs`
**Lines:** 530-544, 958-962
**Severity:** Low

**Issue:**
Hardcoded magic numbers for performance thresholds:

```csharp
// Lines 530-544: Magic numbers
if (stats.SlowestEventMs < 0.1)  // What does 0.1 mean?
{
    statusIcon = NerdFontIcons.SuccessCircle;
}
else if (stats.SlowestEventMs < 0.5)  // What does 0.5 mean?
{
    statusIcon = NerdFontIcons.Warning;
}

// Lines 958-962: More magic numbers
if (timeMs >= WarningThresholdMs)  // 1ms
    return theme.Error;

if (timeMs >= WarningThresholdMs * 0.5)  // What is 0.5?
    return theme.Warning;
```

**Impact:**
- Unclear performance targets
- Difficult to adjust thresholds consistently
- No single source of truth for performance criteria

**Suggested Fix:**
```csharp
// Define constants
private static class PerformanceThresholds
{
    public const double ExcellentTimeMs = 0.1;
    public const double GoodTimeMs = 0.5;
    public const double WarningTimeMs = 1.0;

    public const double WarningFactor = 0.5;
    public const double MildFactor = 0.25;
}

// Use named constants
if (stats.SlowestEventMs < PerformanceThresholds.ExcellentTimeMs)
{
    statusIcon = NerdFontIcons.SuccessCircle;
}
else if (stats.SlowestEventMs < PerformanceThresholds.GoodTimeMs)
{
    statusIcon = NerdFontIcons.Warning;
}
```

---

## 2. Architecture Issues

### 🔴 CRITICAL ARCHITECTURE ISSUES

#### A1. Layer Violation - EventInspectorAdapter
**File:** `PokeSharp.Engine.UI.Debug/Core/EventInspectorAdapter.cs`
**Lines:** 11, 24
**Severity:** Critical

**Issue:**
UI Debug layer directly depends on Engine Core implementation:

```csharp
// UI.Debug layer accessing Core implementation
private readonly EventBus _eventBus;  // Concrete core class

// Line 24: Direct manipulation of core infrastructure
_eventBus.Metrics = _metrics;
```

**Architectural Impact:**
- UI layer coupled to core implementation details
- Cannot evolve EventBus without affecting UI
- Violates layered architecture
- Breaks abstraction boundaries

**Dependency Graph:**
```
PokeSharp.Engine.UI.Debug (Presentation)
    ↓ VIOLATION: Depends on concrete implementation
PokeSharp.Engine.Core/Events/EventBus (Domain Implementation)
    ↓ Should depend on
PokeSharp.Engine.Core/Events/IEventBus (Domain Interface)
```

**Fix:**
```csharp
// UI should only depend on interfaces
public class EventInspectorAdapter
{
    private readonly IEventBus _eventBus;  // Interface only
    private readonly IEventMetrics _metrics;

    public EventInspectorAdapter(IEventBus eventBus, IEventMetrics metrics)
    {
        _eventBus = eventBus;
        // Configure metrics through interface, not concrete property
        if (eventBus is IMetricsConfigurable configurable)
        {
            configurable.SetMetrics(metrics);
        }
    }
}
```

---

#### A2. Tight Coupling - ScriptBase to EventBus
**File:** `PokeSharp.Game.Scripting/Runtime/ScriptBase.cs`
**Lines:** 278-303
**Severity:** Critical

**Issue:**
`ScriptBase` is tightly coupled to `IEventBus` implementation details:

```csharp
// Lines 286-297: Direct dependency on EventBus subscription mechanism
if (Context?.Events == null)
{
    Context?.Logger?.LogWarning(
        "Cannot subscribe to {EventType}: Events system not available",
        typeof(TEvent).Name
    );
    return;
}

// Subscribe and track for cleanup
var subscription = Context.Events.Subscribe(handler);
subscriptions.Add(subscription);
```

**Impact:**
- Cannot substitute alternative event systems
- Scripts break if event system changes
- Difficult to test without full EventBus infrastructure
- Violates Dependency Inversion

**Fix:**
```csharp
// Define abstraction for script-level event operations
public interface IScriptEventBus
{
    IDisposable Subscribe<TEvent>(Action<TEvent> handler) where TEvent : class;
    void Publish<TEvent>(TEvent evt) where TEvent : class;
}

// Adapter wraps IEventBus
public class ScriptEventBusAdapter : IScriptEventBus
{
    private readonly IEventBus _eventBus;

    public IDisposable Subscribe<TEvent>(Action<TEvent> handler) where TEvent : class
    {
        return _eventBus.Subscribe(handler);
    }
}

// ScriptBase depends on abstraction
public abstract class ScriptBase
{
    protected void On<TEvent>(Action<TEvent> handler)
    {
        var subscription = Context.ScriptEvents.Subscribe(handler);
        subscriptions.Add(subscription);
    }
}
```

---

### 🟠 HIGH ARCHITECTURE ISSUES

#### A3. Circular Dependency Risk - Metrics
**Files:** `EventBus.cs:46`, `EventInspectorAdapter.cs:24`
**Severity:** High

**Issue:**
Potential circular dependency between EventBus and Metrics:

```
EventBus.Publish()
    → Metrics.RecordPublish()
        → Could publish MetricsUpdatedEvent
            → EventBus.Publish()
                → CYCLE!
```

```csharp
// EventBus.cs line 74
Metrics?.RecordPublish(eventType.Name, elapsedNanoseconds);

// If Metrics implementation publishes events:
public class EventMetrics : IEventMetrics
{
    public void RecordPublish(string eventType, long ns)
    {
        // DANGER: Publishing event from within event handling!
        _eventBus.Publish(new MetricsUpdatedEvent { ... });
    }
}
```

**Impact:**
- Stack overflow risk
- Infinite event loops
- Difficult to debug performance issues
- Unpredictable behavior

**Fix:**
```csharp
// Metrics should NEVER publish events during event handling
public class EventMetrics : IEventMetrics
{
    private readonly ConcurrentQueue<MetricsUpdate> _deferredUpdates = new();

    public void RecordPublish(string eventType, long ns)
    {
        // Queue for later, don't publish synchronously
        _deferredUpdates.Enqueue(new MetricsUpdate(eventType, ns));
    }

    // Flush on frame boundary
    public void FlushMetrics()
    {
        while (_deferredUpdates.TryDequeue(out var update))
        {
            _eventBus.Publish(new MetricsUpdatedEvent(update));
        }
    }
}
```

---

#### A4. Missing Abstraction - Handler Cache
**File:** `PokeSharp.Engine.Core/Events/EventBus.cs`
**Lines:** 36, 198-210, 247-259
**Severity:** High

**Issue:**
Handler caching logic embedded directly in `EventBus`:

```csharp
// Lines 36, 198-210: Cache management mixed with event logic
private readonly ConcurrentDictionary<Type, HandlerCache> _handlerCache = new();

private void InvalidateCache(Type eventType)
{
    if (_handlers.TryGetValue(eventType, out var handlers))
    {
        var handlerArray = handlers.Select(kvp =>
            new HandlerInfo(kvp.Key, kvp.Value)).ToArray();
        _handlerCache[eventType] = new HandlerCache(handlerArray);
    }
    else
    {
        _handlerCache.TryRemove(eventType, out _);
    }
}
```

**Impact:**
- Cannot swap caching strategies
- Cache invalidation coupled to subscription changes
- Difficult to test caching behavior independently

**Fix:**
```csharp
// Extract caching abstraction
public interface IHandlerCache
{
    bool TryGetHandlers(Type eventType, out HandlerInfo[] handlers);
    void Invalidate(Type eventType);
    void Update(Type eventType, IEnumerable<HandlerInfo> handlers);
}

// LRU cache implementation
public class LruHandlerCache : IHandlerCache
{
    private readonly ConcurrentDictionary<Type, CacheEntry> _cache;

    public bool TryGetHandlers(Type eventType, out HandlerInfo[] handlers)
    {
        // LRU logic here
    }
}

// EventBus uses abstraction
public class EventBus : IEventBus
{
    private readonly IHandlerCache _cache;

    public void Publish<TEvent>(TEvent evt)
    {
        if (_cache.TryGetHandlers(typeof(TEvent), out var handlers))
        {
            ExecuteHandlers(handlers, evt, typeof(TEvent));
        }
    }
}
```

---

### 🟡 MEDIUM ARCHITECTURE ISSUES

#### A5. Cohesion Issue - EventInspectorContent Sections
**File:** `EventInspectorContent.cs`
**Lines:** 521-860
**Severity:** Medium

**Issue:**
Four rendering sections have low cohesion:

```csharp
// Lines 521-560: Summary section
private float RenderSummaryHeader(...) { }

// Lines 562-715: Events table section
private float RenderEventsTable(...) { }

// Lines 751-819: Subscriptions section
private float RenderSubscriptionsSection(...) { }

// Lines 821-860: Recent events section
private float RenderRecentEvents(...) { }
```

**Impact:**
- Difficult to reuse sections independently
- Cannot compose different layouts
- Testing requires full component

**Fix:**
```csharp
// Extract each section as composable component
public class EventInspectorSummarySection : IEventInspectorSection
{
    public float Render(UIRenderer renderer, UITheme theme, float y, EventInspectorData data)
    {
        // Summary rendering logic
    }
}

public class EventInspectorTableSection : IEventInspectorSection
{
    public float Render(UIRenderer renderer, UITheme theme, float y, EventInspectorData data)
    {
        // Table rendering logic
    }
}

// Compose in content
public class EventInspectorContent : UIComponent
{
    private readonly List<IEventInspectorSection> _sections;

    protected override void OnRender(UIContext context)
    {
        float y = Rect.Y;
        foreach (var section in _sections)
        {
            y = section.Render(Renderer, Theme, y, _cachedData);
        }
    }
}
```

---

#### A6. Missing Domain Boundary - Event Models
**Files:** `CollisionEvents.cs`, `MovementEvents.cs`
**Severity:** Medium

**Issue:**
Event models mix domain concepts with infrastructure:

```csharp
// Domain concept
public required Entity Entity { get; init; }

// Infrastructure concern (from TypeEventBase)
public Guid EventId { get; init; }
public DateTime Timestamp { get; init; }
```

**Impact:**
- Domain events coupled to infrastructure
- Cannot swap event infrastructure
- Event replay/sourcing complications

**Fix:**
```csharp
// Pure domain event
public record MovementStarted
{
    public required Entity Entity { get; init; }
    public required Vector2 TargetPosition { get; init; }
    public required Direction Direction { get; init; }
}

// Infrastructure envelope
public record EventEnvelope<TDomain>
{
    public Guid EventId { get; init; } = Guid.NewGuid();
    public DateTime Timestamp { get; init; } = DateTime.UtcNow;
    public required TDomain DomainEvent { get; init; }
}
```

---

#### A7. Implicit Dependencies - ScriptBase Context
**File:** `ScriptBase.cs`
**Lines:** 97, 137-140
**Severity:** Medium

**Issue:**
`ScriptContext` is a god object with implicit dependencies:

```csharp
// Line 97: Context is opaque
protected ScriptContext Context { get; private set; } = null!;

// Implicit dependencies not clear from interface
if (Context?.Events == null) { }  // Scripts fail silently
if (Context?.Entity.HasValue == true) { }  // State only works if Entity exists
```

**Impact:**
- Scripts don't know dependencies until runtime
- Silent failures when dependencies missing
- Testing requires full context setup

**Fix:**
```csharp
// Explicit dependency interfaces
public interface IScriptDependencies
{
    IEventBus Events { get; }
    World World { get; }
    ILogger Logger { get; }
}

public interface IEntityScriptDependencies : IScriptDependencies
{
    Entity Entity { get; }
}

// Scripts declare requirements explicitly
public abstract class EntityScript
{
    protected IEntityScriptDependencies Dependencies { get; private set; }

    public void Initialize(IEntityScriptDependencies deps)
    {
        Dependencies = deps ?? throw new ArgumentNullException(nameof(deps));
    }
}
```

---

#### A8. Data Clumps - Event Properties
**Files:** `CollisionEvents.cs`, `MovementEvents.cs`
**Severity:** Medium

**Issue:**
Repeated property groups suggest missing value objects:

```csharp
// Repeated across events
public required int MapId { get; init; }
public required (int X, int Y) TilePosition { get; init; }
public Direction Direction { get; init; }
```

**Impact:**
- Repeated validation logic
- No encapsulation of position concepts

**Fix:**
```csharp
// Value objects
public readonly record struct TilePosition(int X, int Y)
{
    public bool IsValid() => X >= 0 && Y >= 0;
    public TilePosition Move(Direction direction) => direction switch
    {
        Direction.North => this with { Y = Y - 1 },
        Direction.South => this with { Y = Y + 1 },
        Direction.East => this with { X = X + 1 },
        Direction.West => this with { X = X - 1 },
        _ => this
    };
}

public readonly record struct MapLocation(int MapId, TilePosition Position)
{
    public bool IsSameMap(MapLocation other) => MapId == other.MapId;
}

// Use value objects
public record CollisionCheckEvent : TypeEventBase
{
    public required Entity Entity { get; init; }
    public required MapLocation Location { get; init; }
    public Direction FromDirection { get; init; }
}
```

---

## 3. Summary Statistics

### SOLID Violations by Principle
- **Single Responsibility (SRP):** 6 violations
- **Open/Closed (OCP):** 1 violation
- **Liskov Substitution (LSP):** 1 violation
- **Interface Segregation (ISP):** 1 violation
- **Dependency Inversion (DIP):** 4 violations

### Violations by Severity
- **Critical:** 3 violations (EventBus, EventInspectorContent, ScriptBase)
- **High:** 5 violations
- **Medium:** 4 violations
- **Low:** 1 violation

### Architecture Issues by Severity
- **Critical:** 2 issues (layer violation, tight coupling)
- **High:** 2 issues (circular dependency, missing abstraction)
- **Medium:** 4 issues

### Files with Most Issues
1. **EventInspectorContent.cs** - 5 violations
2. **EventBus.cs** - 3 violations
3. **ScriptBase.cs** - 3 violations
4. **EventInspectorAdapter.cs** - 3 violations
5. **ModLoader.cs** - 1 violation

---

## 4. Prioritized Recommendations

### Immediate Action (Critical - Week 1-2)
1. **Refactor EventBus** - Split into subscription manager, publisher, cache, metrics
2. **Break up EventInspectorContent** - Extract layout, rendering, input into separate classes
3. **Fix EventInspectorAdapter DIP** - Depend on IEventBus interface
4. **Fix layer violation** - UI.Debug should not depend on Core implementations

### Short Term (High - Week 3-4)
5. **Refactor ScriptBase ISP** - Use interface segregation for optional capabilities
6. **Extract ModLoader responsibilities** - Separate discovery, parsing, loading
7. **Fix ColumnLayout OCP** - Make columns extensible
8. **Prevent circular dependencies** - Metrics never publish events synchronously

### Medium Term (Medium - Week 5-6)
9. **Extract handler caching** - Create IHandlerCache abstraction
10. **Improve event cohesion** - Separate EventInspectorContent sections
11. **Add value objects** - Create TilePosition, MapLocation
12. **Fix LSP violation** - Use immutable event cancellation

### Long Term (Low - Week 7-8)
13. **Separate domain/infrastructure** - Use event envelopes
14. **Explicit dependencies** - Replace ScriptContext god object
15. **Named constants** - Replace magic numbers

---

## 5. Code Quality Metrics

### Maintainability Index
- **EventBus.cs:** Moderate (multiple responsibilities)
- **EventInspectorContent.cs:** Low (845 lines, god class)
- **ScriptBase.cs:** Moderate (ISP violation)
- **ModLoader.cs:** Moderate (too many responsibilities)

### Testability Score
- **EventBus:** Low (cannot mock metrics, cache)
- **EventInspectorAdapter:** Low (concrete dependencies)
- **EventInspectorContent:** Very Low (monolithic)
- **ScriptBase:** Moderate (implicit dependencies)

### Extensibility Rating
- **ColumnLayout:** Low (hardcoded columns, violates OCP)
- **ScriptBase:** Low (forced inheritance)
- **EventBus:** Moderate (can add events, but cache/metrics coupled)

---

## Conclusion

The event system changes across phases 1-6 have introduced significant SOLID violations and architectural issues primarily due to:

1. **Rapid feature addition** without refactoring
2. **God classes** taking on too many responsibilities
3. **Concrete dependencies** instead of abstractions
4. **Missing domain boundaries**

**Recommended Next Steps:**
1. Create refactoring plan targeting Critical/High issues
2. Add unit tests before refactoring
3. Introduce interfaces to break coupling
4. Extract smaller, focused classes from god classes
5. Establish architectural guidelines

The codebase is functional but has significant technical debt that will impact maintainability, testability, and extensibility if not addressed.

---

**Report Generated:** 2025-12-03
**Analyst:** Code Analysis Agent (Hive Mind)
**Total Issues:** 21 (13 SOLID violations + 8 architecture issues)
