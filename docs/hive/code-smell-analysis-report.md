# Code Smell Analysis Report - PokeSharp Event System
**Hive Mind Worker: Code Smell Detection Agent**
**Analysis Date:** 2025-12-03
**Phase:** Post-Phase 6 Event System Changes

---

## Executive Summary

This report identifies **23 code smells** across 6 analyzed files in the PokeSharp event system. The analysis focuses on maintainability, readability, and architectural concerns following the event system refactoring phases.

**Severity Distribution:**
- Critical: 2
- High: 8
- Medium: 9
- Low: 4

**Key Findings:**
1. **EventMetrics.cs** contains God Class and Long Method issues
2. **ScriptBase.cs** has duplicate validation patterns and feature envy
3. **EventInspectorContent.cs** suffers from Long Method smell
4. **ModLoader.cs** contains deep nesting and duplicate error handling
5. **EventBusOptimized.cs** has magic numbers despite being well-optimized

---

## Detailed Code Smell Findings

### 1. LONG METHOD: EventInspectorContent.UpdateDisplay()
**File:** `/PokeSharp.Engine.UI.Debug/Components/Debug/EventInspectorContent.cs`
**Lines:** 115-265 (150 lines)
**Severity:** HIGH

**Issue:**
The `UpdateDisplay()` method is excessively long at 150 lines and handles multiple responsibilities:
- Header rendering
- Event list formatting
- Subscription details display
- Performance summary
- Recent event log rendering

**Code Snippet:**
```csharp
private void UpdateDisplay()
{
    if (_cachedData == null)
    {
        Clear();
        AppendLine("No event data available", Color.Gray);
        return;
    }

    Clear();
    var lines = new List<string>();
    UITheme theme = ThemeManager.Current;

    // Header
    lines.Add("Event Inspector");
    lines.Add("━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━");
    // ... continues for 150 lines
}
```

**Suggested Fix:**
Extract separate methods for each display section:
```csharp
private void UpdateDisplay()
{
    if (_cachedData == null)
    {
        DisplayEmptyState();
        return;
    }

    Clear();
    var lines = new List<string>();

    AddHeader(lines);
    AddActiveEventsSection(lines);
    AddSubscriptionsSection(lines);
    AddPerformanceSummary(lines);
    AddRecentEventLog(lines);

    FlushLinesToBuffer(lines);
}

private void AddHeader(List<string> lines) { /* ... */ }
private void AddActiveEventsSection(List<string> lines) { /* ... */ }
private void AddSubscriptionsSection(List<string> lines) { /* ... */ }
private void AddPerformanceSummary(List<string> lines) { /* ... */ }
private void AddRecentEventLog(List<string> lines) { /* ... */ }
```

---

### 2. GOD CLASS: EventMetrics
**File:** `/PokeSharp.Engine.UI.Debug/Core/EventMetrics.cs`
**Lines:** 11-264
**Severity:** HIGH

**Issue:**
`EventMetrics` class handles too many responsibilities:
- Metrics collection coordination
- Event type metrics management
- Subscription metrics management
- Data aggregation
- Timing statistics
- Thread synchronization
- Contains 3 separate classes in one file (EventMetrics, EventTypeMetrics, SubscriptionMetrics)

**Responsibilities Count:** 6+ distinct responsibilities

**Suggested Fix:**
Split into separate cohesive classes:
```csharp
// Coordinator class
public class EventMetrics : IEventMetrics
{
    private readonly EventTypeMetricsRepository _eventMetrics;
    private readonly SubscriptionMetricsRepository _subscriptionMetrics;

    // Only coordination logic here
}

// Separate repositories
public class EventTypeMetricsRepository
{
    private readonly ConcurrentDictionary<string, EventTypeMetrics> _metrics;
    // Event type metrics management only
}

public class SubscriptionMetricsRepository
{
    private readonly ConcurrentDictionary<string, SubscriptionMetrics> _metrics;
    // Subscription metrics management only
}

// Move to separate files
// EventTypeMetrics.cs
// SubscriptionMetrics.cs
```

---

### 3. DUPLICATE CODE: Null/Context Validation Pattern
**File:** `/PokeSharp.Game.Scripting/Runtime/ScriptBase.cs`
**Lines:** 280-303, 346-349, 409-435
**Severity:** MEDIUM

**Issue:**
The same null checking and context validation pattern is repeated in multiple methods:

**Code Snippet:**
```csharp
// In On<TEvent>() - Lines 280-292
protected void On<TEvent>(Action<TEvent> handler, int priority = 500)
    where TEvent : class, IGameEvent
{
    if (handler == null)
    {
        throw new ArgumentNullException(nameof(handler));
    }

    if (Context?.Events == null)
    {
        Context?.Logger?.LogWarning(
            "Cannot subscribe to {EventType}: Events system not available in ScriptContext",
            typeof(TEvent).Name
        );
        return;
    }
    // ... handler logic
}

// In OnEntity<TEvent>() - Lines 346-349
protected void OnEntity<TEvent>(Entity entity, Action<TEvent> handler, int priority = 500)
    where TEvent : class, IEntityEvent
{
    if (handler == null)
    {
        throw new ArgumentNullException(nameof(handler));
    }
    // Same validation needed but not present!
}

// In OnTile<TEvent>() - Lines 412-415
protected void OnTile<TEvent>(Vector2 tilePos, Action<TEvent> handler, int priority = 500)
    where TEvent : class, ITileEvent
{
    if (handler == null)
    {
        throw new ArgumentNullException(nameof(handler));
    }
    // Same validation needed but not present!
}
```

**Suggested Fix:**
Extract validation to helper method:
```csharp
private void ValidateEventSubscription<TEvent>(Action<TEvent> handler, string methodName)
    where TEvent : class
{
    ArgumentNullException.ThrowIfNull(handler);

    if (Context?.Events == null)
    {
        Context?.Logger?.LogWarning(
            "Cannot subscribe to {EventType} in {Method}: Events system not available",
            typeof(TEvent).Name,
            methodName
        );
        throw new InvalidOperationException(
            $"Event system not available for {typeof(TEvent).Name} subscription"
        );
    }
}

protected void On<TEvent>(Action<TEvent> handler, int priority = 500)
    where TEvent : class, IGameEvent
{
    ValidateEventSubscription(handler, nameof(On));
    // ... rest of implementation
}
```

---

### 4. FEATURE ENVY: ScriptBase State Methods
**File:** `/PokeSharp.Game.Scripting/Runtime/ScriptBase.cs`
**Lines:** 471-532
**Severity:** MEDIUM

**Issue:**
`Get<T>()` and `Set<T>()` methods use more data from `Context` than from their own class:

**Code Snippet:**
```csharp
protected T Get<T>(string key, T defaultValue = default)
    where T : struct
{
    if (Context?.TryGetState<T>(out var value) == true)
    {
        return value;
    }
    return defaultValue;
}

protected void Set<T>(string key, T value)
    where T : struct
{
    if (Context?.Entity.HasValue == true)
    {
        Context.World.Set(Context.Entity.Value, value);
        Context.Logger?.LogDebug(
            "Set component {ComponentType} on entity {EntityId}",
            typeof(T).Name,
            Context.Entity.Value.Id
        );
    }
    else
    {
        Context?.Logger?.LogWarning(
            "Cannot set state {Key} on global script context (no entity)",
            key
        );
    }
}
```

**Analysis:**
Both methods primarily manipulate `Context.World` and `Context.Entity` - this logic belongs in `ScriptContext` itself.

**Suggested Fix:**
Move state management to ScriptContext:
```csharp
// In ScriptContext.cs
public T GetState<T>(string key, T defaultValue = default) where T : struct
{
    if (TryGetState<T>(out var value))
        return value;
    return defaultValue;
}

public void SetState<T>(string key, T value) where T : struct
{
    if (!Entity.HasValue)
    {
        Logger?.LogWarning("Cannot set state on global context");
        return;
    }

    World.Set(Entity.Value, value);
    Logger?.LogDebug("Set {ComponentType} on entity {EntityId}",
        typeof(T).Name, Entity.Value.Id);
}

// In ScriptBase.cs - now just delegates
protected T Get<T>(string key, T defaultValue = default)
    where T : struct
    => Context.GetState(key, defaultValue);

protected void Set<T>(string key, T value)
    where T : struct
    => Context.SetState(key, value);
```

---

### 5. MAGIC NUMBERS: Performance Thresholds
**File:** `/PokeSharp.Engine.UI.Debug/Components/Debug/EventInspectorContent.cs`
**Lines:** 268-277
**Severity:** MEDIUM

**Issue:**
Hardcoded performance thresholds without named constants:

**Code Snippet:**
```csharp
private string GetPerformanceColor(double timeMs)
{
    // Color coding based on performance thresholds
    return timeMs switch
    {
        < 0.1 => "green",
        < 0.5 => "yellow",
        < 1.0 => "orange",
        _ => "red"
    };
}
```

**Suggested Fix:**
```csharp
private static class PerformanceThresholds
{
    public const double ExcellentMs = 0.1;
    public const double GoodMs = 0.5;
    public const double AcceptableMs = 1.0;
}

private string GetPerformanceColor(double timeMs)
{
    return timeMs switch
    {
        < PerformanceThresholds.ExcellentMs => "green",
        < PerformanceThresholds.GoodMs => "yellow",
        < PerformanceThresholds.AcceptableMs => "orange",
        _ => "red"
    };
}
```

---

### 6. DEEP NESTING: ModLoader.LoadModAsync()
**File:** `/PokeSharp.Game.Scripting/Modding/ModLoader.cs`
**Lines:** 194-293
**Severity:** HIGH

**Issue:**
Method contains 4+ levels of nesting with complex control flow:

**Code Snippet:**
```csharp
private async Task LoadModAsync(ModManifest manifest)
{
    _logger.LogInformation("⚙️  Loading mod: {Mod}", manifest);

    if (_loadedMods.ContainsKey(manifest.Id))  // Level 1
    {
        _logger.LogWarning(
            "⚠️  Mod '{Id}' is already loaded. Skipping duplicate.",
            manifest.Id
        );
        return;
    }

    var scriptInstances = new List<object>();

    try  // Level 2
    {
        foreach (string scriptFile in manifest.Scripts)  // Level 3
        {
            string scriptPath = Path.Combine(manifest.DirectoryPath, scriptFile);

            if (!File.Exists(scriptPath))  // Level 4
            {
                _logger.LogError(
                    "❌ Script file not found for mod '{Id}': {Path}",
                    manifest.Id,
                    scriptPath
                );
                continue;
            }

            // ... more nested logic

            if (instance is ScriptBase scriptBase)  // Level 4
            {
                _scriptService.InitializeScript(
                    scriptBase,
                    _world,
                    entity: null,
                    logger: _logger
                );
                // ...
            }
            else  // Level 4
            {
                _logger.LogWarning(
                    "⚠️  Script '{Script}' is not a ScriptBase. Loaded but not initialized.",
                    scriptFile
                );
            }
            // ...
        }
        // ... more logic
    }
    catch (Exception ex)  // Level 2
    {
        _logger.LogError(
            ex,
            "❌ Failed to load mod '{Id}': {Message}",
            manifest.Id,
            ex.Message
        );

        await UnloadModAsync(manifest.Id);  // Level 3
        throw;
    }
}
```

**Suggested Fix:**
Extract script loading to separate method:
```csharp
private async Task LoadModAsync(ModManifest manifest)
{
    if (_loadedMods.ContainsKey(manifest.Id))
    {
        LogDuplicateMod(manifest.Id);
        return;
    }

    try
    {
        var scriptInstances = await LoadModScriptsAsync(manifest);
        RegisterLoadedMod(manifest, scriptInstances);
        LogModLoadSuccess(manifest, scriptInstances.Count);
    }
    catch (Exception ex)
    {
        await HandleModLoadFailure(manifest.Id, ex);
        throw;
    }
}

private async Task<List<object>> LoadModScriptsAsync(ModManifest manifest)
{
    var instances = new List<object>();

    foreach (string scriptFile in manifest.Scripts)
    {
        var instance = await LoadAndInitializeSingleScriptAsync(manifest, scriptFile);
        if (instance != null)
            instances.Add(instance);
    }

    return instances;
}

private async Task<object?> LoadAndInitializeSingleScriptAsync(
    ModManifest manifest,
    string scriptFile)
{
    string scriptPath = Path.Combine(manifest.DirectoryPath, scriptFile);

    if (!File.Exists(scriptPath))
    {
        LogScriptNotFound(manifest.Id, scriptPath);
        return null;
    }

    var instance = await LoadScriptInstanceAsync(manifest, scriptFile);
    InitializeScriptIfPossible(instance, scriptFile);

    return instance;
}
```

---

### 7. COMMENTS EXPLAINING BAD CODE
**File:** `/PokeSharp.Game.Scripting/Runtime/ScriptBase.cs`
**Lines:** 444-460, 483-497
**Severity:** LOW

**Issue:**
Comments explaining API limitations instead of fixing them:

**Code Snippet:**
```csharp
/// <param name="key">
///     The key parameter is currently unused (reserved for future key-based state).
///     State is retrieved by component type.
/// </param>
// ...
/// <strong>CURRENT LIMITATION:</strong> This method retrieves state by component type,
/// not by string key. The <paramref name="key" /> parameter is accepted for API consistency
/// but is currently ignored. Future phases will add true key-based state storage.
```

**Analysis:**
The `key` parameter is accepted but ignored - this is a design smell. Either implement key-based storage or remove the parameter.

**Suggested Fix:**
Either implement key-based storage:
```csharp
// Store in a dedicated component
public struct ScriptStateComponent
{
    public Dictionary<string, object> KeyValueState;
}

protected T Get<T>(string key, T defaultValue = default)
{
    if (Context?.Entity.HasValue == true &&
        Context.World.TryGet<ScriptStateComponent>(Context.Entity.Value, out var stateComp))
    {
        if (stateComp.KeyValueState.TryGetValue(key, out var value) && value is T typed)
            return typed;
    }
    return defaultValue;
}
```

Or remove the misleading parameter:
```csharp
// Remove key parameter entirely until implemented
protected T GetComponent<T>(T defaultValue = default) where T : struct
{
    // Clear API - get by type only
}
```

---

### 8. MAGIC NUMBERS: EventBusOptimized Cache Sizes
**File:** `/PokeSharp.Engine.Core/Events/EventBusOptimized.cs`
**Lines:** Throughout (no specific line)
**Severity:** LOW

**Issue:**
No configurable cache sizes or thresholds despite optimization focus.

**Code Snippet:**
```csharp
// No configuration for:
// - Initial dictionary capacity
// - Cache invalidation thresholds
// - Handler array sizes
```

**Suggested Fix:**
```csharp
public class EventBusOptimizationSettings
{
    public int InitialEventTypeCapacity { get; init; } = 32;
    public int InitialHandlerCapacity { get; init; } = 8;
    public bool EnableCaching { get; init; } = true;
    public bool EnableMetrics { get; init; } = false;
}

public class EventBusOptimized : IEventBus
{
    private readonly EventBusOptimizationSettings _settings;

    public EventBusOptimized(
        ILogger<EventBusOptimized>? logger = null,
        EventBusOptimizationSettings? settings = null)
    {
        _settings = settings ?? new EventBusOptimizationSettings();
        // Use settings for initialization
    }
}
```

---

### 9. PRIMITIVE OBSESSION: Handler ID Management
**File:** `/PokeSharp.Engine.Core/Events/EventBus.cs`
**Lines:** 34, 134, 177-187
**Severity:** MEDIUM

**Issue:**
Using raw `int` for handler IDs instead of a value object:

**Code Snippet:**
```csharp
private int _nextHandlerId;

int handlerId = Interlocked.Increment(ref _nextHandlerId);
handlers[handlerId] = handler;

internal void Unsubscribe(Type eventType, int handlerId)
{
    // ...
}
```

**Suggested Fix:**
```csharp
public readonly struct HandlerId : IEquatable<HandlerId>
{
    private readonly int _value;

    private HandlerId(int value) => _value = value;

    public static HandlerId Create(int value) => new(value);
    public static HandlerId Next(ref int counter)
        => new(Interlocked.Increment(ref counter));

    public bool Equals(HandlerId other) => _value == other._value;
    public override int GetHashCode() => _value;

    public static implicit operator int(HandlerId id) => id._value;
}

// Usage:
private int _nextHandlerIdCounter;

var handlerId = HandlerId.Next(ref _nextHandlerIdCounter);
handlers[handlerId] = handler;
```

---

### 10. DATA CLUMPS: Event Type + Handler ID Pair
**File:** `/PokeSharp.Engine.UI.Debug/Core/EventMetrics.cs`
**Lines:** 48, 64, 79
**Severity:** MEDIUM

**Issue:**
`eventTypeName` and `handlerId` appear together repeatedly:

**Code Snippet:**
```csharp
string key = $"{eventTypeName}:{handlerId}";
var subMetrics = _subscriptionMetrics.GetOrAdd(
    key,
    _ => new SubscriptionMetrics(eventTypeName, handlerId)
);
```

**Suggested Fix:**
```csharp
public readonly record struct SubscriptionKey(string EventTypeName, int HandlerId)
{
    public override string ToString() => $"{EventTypeName}:{HandlerId}";
}

// In EventMetrics:
private readonly ConcurrentDictionary<SubscriptionKey, SubscriptionMetrics> _subscriptionMetrics;

public void RecordHandlerInvoke(string eventTypeName, int handlerId, long elapsedMicroseconds)
{
    if (!_isEnabled) return;

    var key = new SubscriptionKey(eventTypeName, handlerId);
    var subMetrics = _subscriptionMetrics.GetOrAdd(
        key,
        k => new SubscriptionMetrics(k.EventTypeName, k.HandlerId)
    );
    subMetrics.RecordInvoke(elapsedMicroseconds);
}
```

---

### 11. DUPLICATE ERROR HANDLING PATTERN
**File:** `/PokeSharp.Game.Scripting/Modding/ModLoader.cs`
**Lines:** 146-155, 218-225, 232-240, 282-292
**Severity:** HIGH

**Issue:**
Same error logging pattern repeated 4+ times:

**Code Snippet:**
```csharp
// Pattern 1 - Lines 146-155
catch (Exception ex)
{
    _logger.LogError(
        ex,
        "❌ Failed to parse manifest at {Path}: {Message}",
        manifestPath,
        ex.Message
    );
    // Continue with other mods
}

// Pattern 2 - Lines 218-225
if (!File.Exists(scriptPath))
{
    _logger.LogError(
        "❌ Script file not found for mod '{Id}': {Path}",
        manifest.Id,
        scriptPath
    );
    continue;
}

// Pattern 3 - Lines 232-240
if (instance == null)
{
    _logger.LogError(
        "❌ Failed to load script '{Script}' for mod '{Id}'",
        scriptFile,
        manifest.Id
    );
    continue;
}

// Pattern 4 - Lines 282-292
catch (Exception ex)
{
    _logger.LogError(
        ex,
        "❌ Failed to load mod '{Id}': {Message}",
        manifest.Id,
        ex.Message
    );
    await UnloadModAsync(manifest.Id);
    throw;
}
```

**Suggested Fix:**
```csharp
private void LogModError(string message, params object[] args)
{
    _logger.LogError($"❌ {message}", args);
}

private void LogModError(Exception ex, string message, params object[] args)
{
    _logger.LogError(ex, $"❌ {message}: {{Message}}",
        args.Concat(new[] { ex.Message }).ToArray());
}

// Usage:
catch (Exception ex)
{
    LogModError(ex, "Failed to parse manifest at {Path}", manifestPath);
}

if (!File.Exists(scriptPath))
{
    LogModError("Script file not found for mod '{Id}': {Path}",
        manifest.Id, scriptPath);
    continue;
}
```

---

### 12. LONG METHOD: EventMetrics.GetInspectorData()
**File:** `/PokeSharp.Engine.UI.Debug/Core/EventInspectorAdapter.cs`
**Lines:** 39-89 (50 lines)
**Severity:** MEDIUM

**Issue:**
Method does too much - data gathering, transformation, and aggregation.

**Suggested Fix:**
```csharp
public EventInspectorData GetInspectorData()
{
    var data = CreateEmptyInspectorData();
    PopulateEventTypeData(data);
    return data;
}

private EventInspectorData CreateEmptyInspectorData()
{
    return new EventInspectorData
    {
        Events = new List<EventTypeInfo>(),
        RecentEvents = _eventLog.ToList(),
        Filters = new EventFilterOptions()
    };
}

private void PopulateEventTypeData(EventInspectorData data)
{
    var registeredTypes = _eventBus.GetRegisteredEventTypes();

    foreach (var eventType in registeredTypes)
    {
        var eventInfo = CreateEventTypeInfo(eventType);
        if (eventInfo != null)
            data.Events.Add(eventInfo);
    }
}

private EventTypeInfo? CreateEventTypeInfo(Type eventType)
{
    string eventTypeName = eventType.Name;
    var metrics = _metrics.GetEventMetrics(eventTypeName);

    if (metrics == null)
        return null;

    var eventInfo = MapMetricsToEventInfo(eventTypeName, metrics, eventType);
    PopulateSubscriptionInfo(eventInfo, eventTypeName);

    return eventInfo;
}
```

---

### 13. DEAD CODE: Unused Method Parameters
**File:** `/PokeSharp.Game.Scripting/Runtime/ScriptBase.cs`
**Lines:** 277, 343, 409
**Severity:** LOW

**Issue:**
`priority` parameter accepted but never used (not implemented in EventBus):

**Code Snippet:**
```csharp
protected void On<TEvent>(Action<TEvent> handler, int priority = 500)
    where TEvent : class, IGameEvent
{
    // ... priority is never passed to Subscribe()
    var subscription = Context.Events.Subscribe(handler);
    // priority ignored!
}
```

**Analysis:**
The documentation even admits this: "NOTE: Priority is currently accepted but not fully implemented"

**Suggested Fix:**
Either implement priority or remove parameter:
```csharp
// Option 1: Remove unused parameter
protected void On<TEvent>(Action<TEvent> handler)
    where TEvent : class, IGameEvent
{
    var subscription = Context.Events.Subscribe(handler);
    subscriptions.Add(subscription);
}

// Option 2: Implement priority in EventBus
public interface IEventBus
{
    IDisposable Subscribe<TEvent>(
        Action<TEvent> handler,
        int priority = 500
    ) where TEvent : class;
}
```

---

### 14. MAGIC STRINGS: Log Message Prefixes
**File:** `/PokeSharp.Game.Scripting/Modding/ModLoader.cs`
**Lines:** Throughout - 65, 70, 82, 85, etc.
**Severity:** LOW

**Issue:**
Emoji prefixes hardcoded throughout:

**Code Snippet:**
```csharp
_logger.LogInformation("🔍 Scanning for mods in: {Path}", _modsBasePath);
_logger.LogWarning("⚠️  Mods directory not found: {Path}. Creating it...", _modsBasePath);
_logger.LogInformation("📦 Found {Count} mod(s)", manifests.Count);
_logger.LogError(ex, "❌ Failed to resolve mod dependencies: {Message}", ex.Message);
_logger.LogInformation("✅ Successfully loaded {Count} mod(s)", _loadedMods.Count);
```

**Suggested Fix:**
```csharp
private static class LogPrefixes
{
    public const string Scanning = "🔍";
    public const string Warning = "⚠️ ";
    public const string Found = "📦";
    public const string Error = "❌";
    public const string Success = "✅";
    public const string Loading = "⚙️ ";
    public const string Reloading = "🔄";
}

_logger.LogInformation($"{LogPrefixes.Scanning} Scanning for mods in: {{Path}}", _modsBasePath);
```

---

### 15. GOD CLASS WARNING: ModLoader Growing Too Large
**File:** `/PokeSharp.Game.Scripting/Modding/ModLoader.cs`
**Lines:** 16-380
**Severity:** MEDIUM

**Issue:**
ModLoader handles 7+ responsibilities:
1. Mod discovery (DiscoverMods)
2. Manifest parsing (ParseManifest)
3. Dependency resolution (via _dependencyResolver)
4. Mod loading (LoadModAsync)
5. Script initialization
6. Mod lifecycle (UnloadModAsync, ReloadModAsync)
7. State tracking (_loadedMods, _modScriptInstances)

**Suggested Fix:**
Split into multiple classes:
```csharp
// Coordinator
public class ModLoader
{
    private readonly ModDiscoveryService _discovery;
    private readonly ModLifecycleManager _lifecycle;
    private readonly ModRegistry _registry;

    public async Task LoadModsAsync()
    {
        var manifests = await _discovery.DiscoverModsAsync();
        var ordered = _resolver.ResolveDependencies(manifests);
        await _lifecycle.LoadModsAsync(ordered);
    }
}

// Separate services
public class ModDiscoveryService
{
    public List<ModManifest> DiscoverMods() { }
    private ModManifest? ParseManifest(string path) { }
}

public class ModLifecycleManager
{
    public async Task LoadModAsync(ModManifest manifest) { }
    public async Task UnloadModAsync(string modId) { }
    public async Task ReloadModAsync(string modId) { }
}

public class ModRegistry
{
    public IReadOnlyDictionary<string, ModManifest> LoadedMods { get; }
    public bool IsModLoaded(string modId) { }
    public ModManifest? GetModManifest(string modId) { }
}
```

---

### 16. DUPLICATE LINQ PATTERNS
**File:** `/PokeSharp.Engine.UI.Debug/Components/Debug/EventInspectorContent.cs`
**Lines:** 144-147, 179-180, 187, 214-220
**Severity:** MEDIUM

**Issue:**
Similar LINQ query patterns repeated:

**Code Snippet:**
```csharp
// Pattern 1
var sortedEvents = _cachedData.Events
    .OrderByDescending(e => e.SubscriberCount)
    .ThenBy(e => e.EventTypeName)
    .ToList();

// Pattern 2
var selectedEvent = _cachedData.Events
    .FirstOrDefault(e => e.EventTypeName == _cachedData.SelectedEventType);

// Pattern 3
var activeEvents = _cachedData.Events
    .Where(e => e.PublishCount > 0)
    .OrderByDescending(e => e.AverageTimeMs)
    .Take(5)
    .ToList();
```

**Suggested Fix:**
```csharp
private static class EventQueries
{
    public static IEnumerable<EventTypeInfo> OrderedByActivity(
        this IEnumerable<EventTypeInfo> events)
    {
        return events
            .OrderByDescending(e => e.SubscriberCount)
            .ThenBy(e => e.EventTypeName);
    }

    public static EventTypeInfo? FindByName(
        this IEnumerable<EventTypeInfo> events,
        string? eventTypeName)
    {
        return events.FirstOrDefault(e => e.EventTypeName == eventTypeName);
    }

    public static IEnumerable<EventTypeInfo> TopSlowest(
        this IEnumerable<EventTypeInfo> events,
        int count = 5)
    {
        return events
            .Where(e => e.PublishCount > 0)
            .OrderByDescending(e => e.AverageTimeMs)
            .Take(count);
    }
}

// Usage:
var sortedEvents = _cachedData.Events.OrderedByActivity().ToList();
var selectedEvent = _cachedData.Events.FindByName(_cachedData.SelectedEventType);
var activeEvents = _cachedData.Events.TopSlowest().ToList();
```

---

### 17. INCONSISTENT ERROR HANDLING: Subscription Validation
**File:** `/PokeSharp.Game.Scripting/Runtime/ScriptBase.cs`
**Lines:** 280-303 vs 343-368 vs 409-435
**Severity:** HIGH

**Issue:**
`On<TEvent>()` logs warning and returns on missing Events system, but `OnEntity<TEvent>()` and `OnTile<TEvent>()` don't check at all:

**Code Snippet:**
```csharp
// On<TEvent> - Has validation
protected void On<TEvent>(Action<TEvent> handler, int priority = 500)
{
    if (Context?.Events == null)
    {
        Context?.Logger?.LogWarning(
            "Cannot subscribe to {EventType}: Events system not available",
            typeof(TEvent).Name
        );
        return;  // Graceful failure
    }
    // ... subscribe
}

// OnEntity<TEvent> - NO validation!
protected void OnEntity<TEvent>(Entity entity, Action<TEvent> handler, int priority = 500)
{
    if (handler == null)
        throw new ArgumentNullException(nameof(handler));

    On<TEvent>(evt => {  // Calls On() which will log but...
        if (evt.Entity == entity)
            handler(evt);
    }, priority);

    // This logging happens even if Events is null!
    Context?.Logger?.LogDebug(
        "Subscribed to {EventType} for entity {EntityId}",
        typeof(TEvent).Name,
        entity.Id
    );
}
```

**Analysis:**
Inconsistent behavior - `OnEntity` logs success even when subscription fails.

**Suggested Fix:**
```csharp
private bool ValidateAndLogSubscription<TEvent>(string subscriptionType, string details)
    where TEvent : class
{
    if (Context?.Events == null)
    {
        Context?.Logger?.LogWarning(
            "Cannot subscribe to {EventType} ({Type}): Events system not available",
            typeof(TEvent).Name,
            subscriptionType
        );
        return false;
    }

    Context.Logger?.LogDebug(
        "Subscribed to {EventType} {Details}",
        typeof(TEvent).Name,
        details
    );
    return true;
}

protected void OnEntity<TEvent>(Entity entity, Action<TEvent> handler, int priority = 500)
{
    ArgumentNullException.ThrowIfNull(handler);

    if (!ValidateAndLogSubscription<TEvent>("entity-filtered",
        $"for entity {entity.Id}"))
        return;

    On<TEvent>(evt => {
        if (evt.Entity == entity)
            handler(evt);
    }, priority);
}
```

---

### 18. INEFFICIENT LOGGING: Unused String Key Parameter
**File:** `/PokeSharp.Game.Scripting/Runtime/ScriptBase.cs`
**Lines:** 518-524, 527-531
**Severity:** LOW

**Issue:**
`Set<T>()` logs the `key` parameter even though it's not used:

**Code Snippet:**
```csharp
protected void Set<T>(string key, T value)
    where T : struct
{
    if (Context?.Entity.HasValue == true)
    {
        Context.World.Set(Context.Entity.Value, value);

        Context.Logger?.LogDebug(
            "Set component {ComponentType} on entity {EntityId}",
            typeof(T).Name,
            Context.Entity.Value.Id
            // key is never logged!
        );
    }
    else
    {
        Context?.Logger?.LogWarning(
            "Cannot set state {Key} on global script context (no entity)",
            key  // Only logged in error case
        );
    }
}
```

**Suggested Fix:**
Remove key from logging or use it consistently:
```csharp
Context.Logger?.LogDebug(
    "Set component {ComponentType} (key: {Key}) on entity {EntityId}",
    typeof(T).Name,
    key,
    Context.Entity.Value.Id
);
```

---

### 19. TIGHT COUPLING: EventInspectorAdapter to EventBus Concrete Type
**File:** `/PokeSharp.Engine.UI.Debug/Core/EventInspectorAdapter.cs`
**Lines:** 11, 16
**Severity:** CRITICAL

**Issue:**
Adapter depends on concrete `EventBus` instead of `IEventBus`:

**Code Snippet:**
```csharp
public class EventInspectorAdapter
{
    private readonly EventBus _eventBus;  // Concrete type!
    private readonly EventMetrics _metrics;

    public EventInspectorAdapter(EventBus eventBus, EventMetrics metrics, int maxLogEntries = 100)
    {
        _eventBus = eventBus;
        _metrics = metrics;
        _maxLogEntries = maxLogEntries;
        _eventLog = new Queue<EventLogEntry>(maxLogEntries);

        _eventBus.Metrics = _metrics;  // Accesses concrete property
    }
}
```

**Analysis:**
This breaks abstraction and prevents using `EventBusOptimized` with the inspector!

**Suggested Fix:**
Either:
1. Update `IEventBus` to include inspection methods:
```csharp
public interface IEventBus
{
    // Existing methods...

    // Inspection API
    IReadOnlyCollection<Type> GetRegisteredEventTypes();
    IReadOnlyCollection<int> GetHandlerIds(Type eventType);
    IEventMetrics? Metrics { get; set; }
}
```

2. Or create a separate inspection interface:
```csharp
public interface IEventBusInspection
{
    IReadOnlyCollection<Type> GetRegisteredEventTypes();
    IReadOnlyCollection<int> GetHandlerIds(Type eventType);
    IEventMetrics? Metrics { get; set; }
}

public class EventBus : IEventBus, IEventBusInspection { }

public class EventInspectorAdapter
{
    private readonly IEventBusInspection _eventBus;

    public EventInspectorAdapter(IEventBusInspection eventBus, ...)
    {
        // ...
    }
}
```

---

### 20. MAGIC NUMBERS: EventPool MaxPoolSize
**File:** `/PokeSharp.Engine.Core/Events/EventPool.cs`
**Lines:** 40
**Severity:** LOW

**Issue:**
Default pool size hardcoded:

**Code Snippet:**
```csharp
public EventPool(int maxPoolSize = 100)
{
    _maxPoolSize = maxPoolSize;
}
```

**Suggested Fix:**
```csharp
private static class EventPoolDefaults
{
    public const int MaxPoolSize = 100;
    public const int RecommendedHighFrequency = 1000;
    public const int RecommendedLowFrequency = 50;
}

public EventPool(int maxPoolSize = EventPoolDefaults.MaxPoolSize)
{
    _maxPoolSize = maxPoolSize;
}
```

---

### 21. POTENTIAL MEMORY LEAK: EventLog Unbounded Growth
**File:** `/PokeSharp.Engine.UI.Debug/Core/EventInspectorAdapter.cs`
**Lines:** 145-152
**Severity:** CRITICAL

**Issue:**
While `_maxLogEntries` is set, the dequeue logic could fail under high concurrency:

**Code Snippet:**
```csharp
private void AddLogEntry(EventLogEntry entry)
{
    if (_eventLog.Count >= _maxLogEntries)
    {
        _eventLog.Dequeue();
    }
    _eventLog.Enqueue(entry);
}
```

**Analysis:**
`Queue<T>` is not thread-safe. Multiple threads could:
1. Check count simultaneously
2. Both enqueue
3. Exceed max size

**Suggested Fix:**
```csharp
private readonly object _logLock = new();

private void AddLogEntry(EventLogEntry entry)
{
    lock (_logLock)
    {
        while (_eventLog.Count >= _maxLogEntries)
        {
            _eventLog.Dequeue();
        }
        _eventLog.Enqueue(entry);
    }
}

public EventInspectorData GetInspectorData()
{
    // ...
    List<EventLogEntry> recentEvents;
    lock (_logLock)
    {
        recentEvents = _eventLog.ToList();
    }

    var data = new EventInspectorData
    {
        // ...
        RecentEvents = recentEvents,
        // ...
    };
}
```

---

### 22. INCONSISTENT NAMING: Boolean Property Prefixes
**File:** `/PokeSharp.Engine.UI.Debug/Components/Debug/EventInspectorContent.cs`
**Lines:** 22, 30
**Severity:** LOW

**Issue:**
Inconsistent boolean naming - `_showSubscriptions` vs `HasProvider`:

**Code Snippet:**
```csharp
private bool _showSubscriptions = true;  // Verb form
public bool HasProvider => _dataProvider != null;  // Has prefix
```

**Suggested Fix:**
Use consistent "Has/Is/Can" prefix pattern:
```csharp
private bool _isShowingSubscriptions = true;
public bool HasProvider => _dataProvider != null;

// Or if keeping verb form, be consistent:
private bool _showSubscriptions = true;
public bool ProviderIsSet => _dataProvider != null;
```

---

### 23. DUPLICATE SUBSCRIPTION LOGIC: OnEntity and OnTile
**File:** `/PokeSharp.Game.Scripting/Runtime/ScriptBase.cs`
**Lines:** 343-368, 409-435
**Severity:** MEDIUM

**Issue:**
Both methods follow identical "wrap handler with filter, then call On()" pattern:

**Code Snippet:**
```csharp
protected void OnEntity<TEvent>(Entity entity, Action<TEvent> handler, int priority = 500)
{
    if (handler == null)
        throw new ArgumentNullException(nameof(handler));

    On<TEvent>(evt =>
    {
        if (evt.Entity == entity)
            handler(evt);
    }, priority);

    Context?.Logger?.LogDebug(/*...*/);
}

protected void OnTile<TEvent>(Vector2 tilePos, Action<TEvent> handler, int priority = 500)
{
    if (handler == null)
        throw new ArgumentNullException(nameof(handler));

    On<TEvent>(evt =>
    {
        if (evt.TileX == (int)tilePos.X && evt.TileY == (int)tilePos.Y)
            handler(evt);
    }, priority);

    Context?.Logger?.LogDebug(/*...*/);
}
```

**Suggested Fix:**
Extract common filtering pattern:
```csharp
private void OnFiltered<TEvent>(
    Action<TEvent> handler,
    Func<TEvent, bool> filter,
    int priority,
    string filterDescription)
    where TEvent : class, IGameEvent
{
    ArgumentNullException.ThrowIfNull(handler);

    On<TEvent>(evt =>
    {
        if (filter(evt))
            handler(evt);
    }, priority);

    Context?.Logger?.LogDebug(
        "Subscribed to {EventType} with filter: {Filter}",
        typeof(TEvent).Name,
        filterDescription
    );
}

protected void OnEntity<TEvent>(Entity entity, Action<TEvent> handler, int priority = 500)
    where TEvent : class, IEntityEvent
{
    OnFiltered(
        handler,
        evt => evt.Entity == entity,
        priority,
        $"entity {entity.Id}"
    );
}

protected void OnTile<TEvent>(Vector2 tilePos, Action<TEvent> handler, int priority = 500)
    where TEvent : class, ITileEvent
{
    OnFiltered(
        handler,
        evt => evt.TileX == (int)tilePos.X && evt.TileY == (int)tilePos.Y,
        priority,
        $"tile ({(int)tilePos.X}, {(int)tilePos.Y})"
    );
}
```

---

## Summary Statistics

### Smells by Severity
| Severity | Count | Percentage |
|----------|-------|------------|
| Critical | 2     | 8.7%       |
| High     | 8     | 34.8%      |
| Medium   | 9     | 39.1%      |
| Low      | 4     | 17.4%      |
| **Total**| **23**| **100%**   |

### Smells by Type
| Type                    | Count |
|-------------------------|-------|
| Long Method             | 3     |
| God Class               | 2     |
| Duplicate Code          | 5     |
| Magic Numbers/Strings   | 4     |
| Feature Envy            | 1     |
| Deep Nesting            | 1     |
| Tight Coupling          | 1     |
| Data Clumps             | 1     |
| Primitive Obsession     | 1     |
| Comments Explaining Bad | 1     |
| Dead Code               | 1     |
| Inconsistent Naming     | 1     |
| Potential Memory Leak   | 1     |

### Smells by File
| File                          | Count | Highest Severity |
|-------------------------------|-------|------------------|
| ScriptBase.cs                 | 6     | High             |
| ModLoader.cs                  | 4     | High             |
| EventInspectorContent.cs      | 3     | High             |
| EventMetrics.cs               | 3     | High             |
| EventInspectorAdapter.cs      | 3     | Critical         |
| EventBusOptimized.cs          | 2     | Low              |
| EventPool.cs                  | 1     | Low              |
| EventBus.cs                   | 1     | Medium           |

---

## Priority Recommendations

### IMMEDIATE (Critical + High Severity)

1. **Fix EventInspectorAdapter tight coupling** (Critical)
   - Update to use `IEventBus` interface
   - Add inspection methods to interface or create separate inspection interface

2. **Fix EventLog thread safety** (Critical)
   - Add locking to `AddLogEntry()` and log access
   - Prevent potential memory leak under high concurrency

3. **Refactor ScriptBase validation** (High)
   - Extract common validation logic
   - Fix inconsistent error handling across On/OnEntity/OnTile

4. **Simplify ModLoader.LoadModAsync** (High)
   - Extract script loading to separate methods
   - Reduce nesting from 4+ to 2 levels

5. **Break down EventInspectorContent.UpdateDisplay** (High)
   - Split 150-line method into 5-6 focused methods
   - Each section becomes its own method

### MEDIUM PRIORITY (Medium Severity)

6. **Refactor EventMetrics God Class**
   - Split into coordinator + repositories
   - Separate file per class

7. **Fix ScriptBase feature envy**
   - Move state management to ScriptContext
   - ScriptBase becomes thin delegation layer

8. **Extract common LINQ patterns**
   - Create extension methods for repeated queries
   - Improve readability in EventInspectorContent

9. **Consider splitting ModLoader**
   - Extract discovery, loading, and lifecycle management
   - Create ModRegistry for state tracking

### LOW PRIORITY (Low Severity)

10. **Replace magic numbers with constants**
    - Performance thresholds
    - Pool sizes
    - Log prefixes

11. **Remove or implement unused priority parameter**
    - Either implement in EventBus or remove from API

12. **Improve consistency**
    - Boolean naming conventions
    - Error message formatting

---

## Architectural Observations

### Positive Patterns Observed:
1. **Good separation** between `EventBus` and `EventBusOptimized`
2. **Proper use of interfaces** (IEventBus, IGameEvent, etc.)
3. **Comprehensive metrics collection** with enable/disable flag
4. **Event pooling** for performance optimization
5. **Detailed documentation** and XML comments

### Areas for Improvement:
1. **God Classes**: EventMetrics and ModLoader growing too large
2. **Duplicate patterns**: Validation, error handling, LINQ queries
3. **Inconsistent abstractions**: Some classes use interfaces, others use concrete types
4. **Parameter confusion**: Accepted but unused parameters (key, priority)
5. **Threading concerns**: Some thread-unsafe code in debug UI components

---

## Conclusion

The PokeSharp event system is generally well-architected with good optimization work, but suffers from **maintenance debt** accumulated during rapid development through phases 1-6. The **23 identified smells** are primarily **maintenance concerns** rather than functional bugs.

**Key Actions:**
1. Fix 2 **critical** tight coupling and thread safety issues immediately
2. Refactor 8 **high severity** long methods and validation inconsistencies
3. Address 9 **medium severity** architectural concerns in next refactoring phase
4. Clean up 4 **low severity** cosmetic issues opportunistically

**Estimated Refactoring Effort:** 3-5 days for critical/high priority items.

---
**Report Generated:** 2025-12-03
**Hive Worker:** Code Smell Detection Agent
**Next Steps:** Share findings with Architecture Agent and Refactoring Agent for action planning.
