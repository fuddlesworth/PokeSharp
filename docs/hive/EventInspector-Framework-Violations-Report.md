# EventInspector Debug UI Framework Violations Report

**Analyst**: UX Component Framework Analyst (Hive Mind Swarm)
**Date**: 2025-12-03
**Component**: EventInspector Debug UI
**Status**: CRITICAL VIOLATIONS FOUND

## Executive Summary

The EventInspector debug UI implementation contains **CRITICAL** framework violations that break consistency with the established debug panel architecture. While the implementation is functional, it deviates from proven patterns used in StatsPanel, ProfilerPanel, LogsPanel, and ConsolePanel.

**Total Violations Found**: 7 (3 Critical, 2 High, 2 Medium)

---

## VIOLATION 1: Missing DebugPanelBase Inheritance (LogsPanel and ConsolePanel)

**Severity**: CRITICAL
**Files**:
- `PokeSharp.Engine.UI.Debug/Components/Debug/LogsPanel.cs:17`
- `PokeSharp.Engine.UI.Debug/Components/Debug/ConsolePanel.cs:14`

### Expected Pattern (from StatsPanel, ProfilerPanel, EventInspectorPanel)
```csharp
public class StatsPanel : DebugPanelBase, IStatsOperations
{
    private readonly StatsContent _content;

    internal StatsPanel(StatsContent content, StatusBar statusBar)
        : base(statusBar)
    {
        _content = content;
        Id = "stats_panel";
        _content.Constraint.Anchor = Anchor.StretchTop;
        AddChild(_content);
    }

    protected override UIComponent GetContentComponent()
    {
        return _content;
    }

    protected override void UpdateStatusBar()
    {
        // Update logic here
    }
}
```

### Actual Implementation (LogsPanel and ConsolePanel)
```csharp
// LogsPanel.cs:17
public class LogsPanel : Panel, ILogOperations  // ❌ Extends Panel directly
{
    // Manual layout management
    // Manual status bar anchoring
    // Duplicated theme handling
}

// ConsolePanel.cs:14
public class ConsolePanel : Panel  // ❌ Extends Panel directly
{
    // Manual layout management
    // No DebugPanelBase benefits
}
```

### Impact
- **Inconsistent Architecture**: Two panels (Logs, Console) don't follow the framework
- **Code Duplication**: Manual implementation of layout logic already in DebugPanelBase
- **Maintenance Burden**: Changes to panel behavior require updating 4 places instead of 1
- **Theme Switching**: Manual OnRenderContainer implementations vs. base class handling
- **Status Bar Management**: Inconsistent patterns for status bar updates

### Evidence from Framework
DebugPanelBase provides:
1. **Automatic Layout**: `OnRenderContainer()` calculates content/status bar heights
2. **Status Bar Management**: `UpdateStatusBar()` hook with standard helpers
3. **Theme Integration**: Automatic border/background color handling
4. **Z-Order Management**: `OnRenderChildren()` ensures status bar renders on top
5. **Standard Padding**: `StandardLinePadding = 5` constant

### Recommended Fix
```csharp
// LogsPanel.cs
public class LogsPanel : DebugPanelBase, ILogOperations
{
    private readonly LogsContent _content;  // New: Extract content logic

    internal LogsPanel(LogsContent content, StatusBar statusBar)
        : base(statusBar)
    {
        _content = content;
        Id = "logs_panel";
        _content.Constraint.Anchor = Anchor.StretchTop;
        AddChild(_content);
    }

    protected override UIComponent GetContentComponent()
    {
        return _content;
    }

    protected override void UpdateStatusBar()
    {
        int totalLogs = _content.GetTotalLogCount();
        int visibleLogs = _content.GetVisibleLogCount();
        SetStatusBar($"Logs: {visibleLogs}/{totalLogs}", "↑↓: Scroll | /: Search | L: Level");
    }
}

// New LogsContent.cs - Extract from LogsPanel
public class LogsContent : UIComponent
{
    private readonly TextBuffer _logBuffer;
    // Move filtering, search, log management here
}
```

---

## VIOLATION 2: Inconsistent Content Component Base Classes

**Severity**: HIGH
**Files**:
- `PokeSharp.Engine.UI.Debug/Components/Debug/EventInspectorContent.cs:15`
- `PokeSharp.Engine.UI.Debug/Components/Debug/StatsContent.cs:16`
- `PokeSharp.Engine.UI.Debug/Components/Debug/ProfilerContent.cs:16`

### Pattern Analysis

| Panel | Content Component | Base Class | Consistency |
|-------|------------------|------------|-------------|
| EventInspector | EventInspectorContent | TextBuffer | ✓ Correct |
| Stats | StatsContent | UIComponent | ❌ Inconsistent |
| Profiler | ProfilerContent | UIComponent | ❌ Inconsistent |
| Logs | (embedded in panel) | N/A | ❌ No separation |
| Console | (embedded in panel) | N/A | ❌ No separation |

### Expected Pattern
Content components that display scrollable text/lists should extend **TextBuffer** for automatic:
- Scrolling support
- Line rendering
- Color formatting
- Search integration
- Selection handling

### Actual Implementation
```csharp
// EventInspectorContent.cs:15 - ✓ CORRECT
public class EventInspectorContent : TextBuffer
{
    public EventInspectorContent() : base("event_inspector_content")
    {
        LinePadding = DebugPanelBase.StandardLinePadding;  // ✓ Uses standard padding
    }
}

// StatsContent.cs:16 - ❌ INCONSISTENT
public class StatsContent : UIComponent  // Should extend TextBuffer?
{
    // Manual rendering logic
    // Custom scrolling implementation
}

// ProfilerContent.cs:16 - ❌ INCONSISTENT
public class ProfilerContent : UIComponent  // Should extend TextBuffer?
{
    // Manual rendering logic
    // Custom scrolling implementation
}
```

### Impact
- **Mixed Patterns**: Three different approaches to content rendering
- **Feature Disparity**: TextBuffer provides search/selection that others lack
- **Code Duplication**: Custom scrolling logic in Stats/Profiler
- **Reusability**: TextBuffer features not leveraged consistently

### Recommended Fix
**Decision Required**: Choose one pattern:

**Option A: TextBuffer for All** (Recommended for text-heavy displays)
```csharp
public class StatsContent : TextBuffer
{
    public StatsContent(string id) : base(id)
    {
        LinePadding = DebugPanelBase.StandardLinePadding;
    }

    private void UpdateDisplay()
    {
        Clear();
        AppendLine("Performance Stats", ThemeManager.Current.TextPrimary);
        AppendLine($"FPS: {_cachedStats.Fps:F1}", GetFpsColor());
        // Use TextBuffer rendering
    }
}
```

**Option B: UIComponent with Justification** (For custom visualizations)
- Document WHY TextBuffer is insufficient
- Stats uses Sparkline component (custom visualization)
- Profiler uses horizontal bar charts (custom visualization)
- Keep UIComponent but standardize scrolling

---

## VIOLATION 3: Hardcoded Decorative Characters

**Severity**: MEDIUM
**File**: `PokeSharp.Engine.UI.Debug/Components/Debug/EventInspectorContent.cs:131`

### Expected Pattern
Use theme constants or named constants for repeated visual elements.

### Actual Implementation
```csharp
// EventInspectorContent.cs:129-132
// Header
lines.Add("Event Inspector");
lines.Add("━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━");  // ❌ Hardcoded
lines.Add("");
```

### Impact
- **Magic Numbers**: 62 hardcoded box-drawing characters
- **Maintenance**: Width changes require manual counting
- **Consistency**: Other panels may use different separators
- **Localization**: Box-drawing characters may not render on all systems

### Evidence from Other Panels
```csharp
// ProfilerContent uses calculated separators
private string GetSeparator(int width)
{
    return new string('─', width);
}

// TextBuffer provides line rendering without hardcoded decorations
```

### Recommended Fix
```csharp
// Option 1: Theme-based separator
UITheme theme = ThemeManager.Current;
string separator = new string(theme.SeparatorChar, (int)(Rect.Width / theme.CharWidth));
lines.Add(separator);

// Option 2: Named constant
private const char SEPARATOR_CHAR = '━';
private string GetSeparator(float width)
{
    return new string(SEPARATOR_CHAR, (int)(width / ThemeManager.Current.CharWidth));
}
lines.Add(GetSeparator(Rect.Width));

// Option 3: Use TextBuffer divider feature (if available)
AppendDivider();
```

---

## VIOLATION 4: Inline Color Strings Instead of Theme Colors

**Severity**: HIGH
**File**: `PokeSharp.Engine.UI.Debug/Components/Debug/EventInspectorContent.cs` (multiple locations)

### Expected Pattern
Use `ThemeManager.Current` colors for consistency and theme switching support.

### Actual Implementation
```csharp
// EventInspectorContent.cs:154
string statusColor = eventInfo.SubscriberCount > 0 ? "green" : "gray";  // ❌ String literals

lines.Add(
    $"  {indicator} [{statusColor}]✓[/] {eventInfo.EventTypeName} " +
    $"[cyan]({eventInfo.SubscriberCount} subscribers)[/]{customTag}"  // ❌ "cyan" hardcoded
);

// Lines 164-169, 197-201, 225-229, 249-255: More color strings
string perfColor = GetPerformanceColor(eventInfo.AverageTimeMs);  // Returns "green", "yellow", etc.
```

### Expected Pattern (from StatsPanel)
```csharp
// StatsPanel.cs uses Theme colors properly
UITheme theme = ThemeManager.Current;
Color textColor = isHealthy ? theme.Success : theme.Error;
Color warningColor = theme.Warning;

// Render with actual Color objects
renderer.DrawText(text, position, textColor);
```

### Impact
- **Theme Switching**: Colors won't update when theme changes
- **Accessibility**: Can't adjust colors for color blindness
- **Consistency**: Different panels may have different color semantics
- **Type Safety**: String literals vs. Color objects

### Evidence
EventInspectorContent uses an intermediary string-based color system:
```csharp
// EventInspectorContent.cs:267-277
private string GetPerformanceColor(double timeMs)
{
    return timeMs switch
    {
        < 0.1 => "green",    // ❌ String literals
        < 0.5 => "yellow",
        < 1.0 => "orange",
        _ => "red"
    };
}
```

This suggests EventInspectorContent is using a markup system (like BBCode or similar) instead of direct Color rendering. This is INCONSISTENT with other panels.

### Recommended Fix

**Option A: Use TextBuffer's Color Parameter** (Recommended)
```csharp
// EventInspectorContent extends TextBuffer - use its color support
UITheme theme = ThemeManager.Current;
Color statusColor = eventInfo.SubscriberCount > 0 ? theme.Success : theme.TextSecondary;

AppendLine(
    $"  {indicator} ✓ {eventInfo.EventTypeName} ({eventInfo.SubscriberCount} subscribers){customTag}",
    statusColor
);

Color perfColor = GetPerformanceColor(eventInfo.AverageTimeMs);
AppendLine(
    $"      avg: {eventInfo.AverageTimeMs:F3}ms, max: {eventInfo.MaxTimeMs:F3}ms, count: {eventInfo.PublishCount}",
    perfColor
);

// Update method signature
private Color GetPerformanceColor(double timeMs)
{
    UITheme theme = ThemeManager.Current;
    return timeMs switch
    {
        < 0.1 => theme.Success,
        < 0.5 => theme.Warning,
        < 1.0 => theme.Warning,
        _ => theme.Error
    };
}
```

**Option B: Document Markup System**
If the string-based markup is intentional (for rich text formatting):
1. Document the markup system in comments
2. Define constants for color names
3. Ensure parser respects theme colors
4. Add theme-aware color mapping

---

## VIOLATION 5: Inconsistent Builder Patterns

**Severity**: MEDIUM
**Files**: All PanelBuilder implementations

### Pattern Analysis

| Builder | Static Create()? | Method Chaining | Constraint Setting | Status Bar Creation |
|---------|------------------|----------------|-------------------|---------------------|
| StatsPanelBuilder | ✓ Yes | ✓ Yes | ✓ In Build() | ✓ Helper method |
| ProfilerPanelBuilder | ✓ Yes | ✓ Yes | ✓ In Build() | ✓ Helper method |
| EventInspectorPanelBuilder | ❌ No | ✓ Yes | ❌ In constructor | ❌ Inline in Build() |

### Expected Pattern (StatsPanelBuilder, ProfilerPanelBuilder)
```csharp
public class StatsPanelBuilder
{
    public static StatsPanelBuilder Create()  // ✓ Static factory
    {
        return new StatsPanelBuilder();
    }

    public StatsPanel Build()
    {
        StatsContent content = CreateDefaultContent();  // ✓ Helper method
        StatusBar statusBar = CreateDefaultStatusBar();  // ✓ Helper method

        var panel = new StatsPanel(content, statusBar);
        // Configure panel
        return panel;
    }

    private static StatsContent CreateDefaultContent()
    {
        return new StatsContent("stats_content")
        {
            Constraint = new LayoutConstraint { Anchor = Anchor.StretchTop },  // ✓ Set here
        };
    }

    private static StatusBar CreateDefaultStatusBar()
    {
        return new StatusBar("stats_status")
        {
            Constraint = new LayoutConstraint { Anchor = Anchor.StretchBottom },
        };
    }
}
```

### Actual Implementation (EventInspectorPanelBuilder)
```csharp
public class EventInspectorPanelBuilder
{
    // ❌ No static Create() method

    public EventInspectorPanel Build()
    {
        // ❌ Inline creation
        var content = new EventInspectorContent();
        content.SetDataProvider(_dataProvider);
        content.SetRefreshInterval(_refreshInterval);

        // ❌ Inline creation with different ID pattern
        var statusBar = new StatusBar("event_inspector_status");

        // ❌ Constraint setting happens in EventInspectorPanel constructor
        var panel = new EventInspectorPanel(content, statusBar);

        return panel;
    }
}
```

### Impact
- **Discovery**: No consistent way to create builders
- **Testability**: Harder to mock builder creation
- **Readability**: Mixed patterns confuse developers
- **Constraint Management**: Setting constraints in different places

### Recommended Fix
```csharp
public class EventInspectorPanelBuilder
{
    public static EventInspectorPanelBuilder Create()
    {
        return new EventInspectorPanelBuilder();
    }

    public EventInspectorPanel Build()
    {
        EventInspectorContent content = CreateDefaultContent();
        StatusBar statusBar = CreateDefaultStatusBar();

        var panel = new EventInspectorPanel(content, statusBar);
        return panel;
    }

    private EventInspectorContent CreateDefaultContent()
    {
        var content = new EventInspectorContent
        {
            Constraint = new LayoutConstraint { Anchor = Anchor.StretchTop }
        };
        content.SetDataProvider(_dataProvider);
        content.SetRefreshInterval(_refreshInterval);
        return content;
    }

    private static StatusBar CreateDefaultStatusBar()
    {
        return new StatusBar("event_inspector_status")
        {
            Constraint = new LayoutConstraint { Anchor = Anchor.StretchBottom }
        };
    }
}
```

---

## VIOLATION 6: Update() Called from UpdateStatusBar()

**Severity**: MEDIUM
**File**: `PokeSharp.Engine.UI.Debug/Components/Debug/EventInspectorPanel.cs:113`

### Expected Pattern
`UpdateStatusBar()` should only update status bar display, not trigger data refreshes.

### Actual Implementation
```csharp
// EventInspectorPanel.cs:110-124
protected override void UpdateStatusBar()
{
    // Update content first
    _content.Update();  // ❌ Triggers data refresh + display update

    if (!_content.HasProvider)
    {
        SetStatusBar("No event data provider configured", "");
        return;
    }

    int refreshRate = 60 / _content.GetRefreshInterval();
    string hints = $"↑↓: Select Event | Tab: Toggle Details | R: Refresh | Refresh: ~{refreshRate}fps";
    SetStatusBar("Event Inspector Active", hints);
}
```

### Expected Pattern (from StatsPanel, ProfilerPanel)
```csharp
// StatsPanel.cs:145-182
protected override void UpdateStatusBar()
{
    // Handle empty state
    if (!_content.HasProvider)
    {
        SetStatusBar("No stats provider configured", "");
        return;
    }

    // ✓ Only reads cached data, doesn't trigger updates
    UITheme theme = ThemeManager.Current;
    (string indicator, Color color, bool isHealthy) health = _content.GetOverallHealth(theme);

    string stats = $"{health.indicator} FPS: {_content.CurrentFps:F0} | ...";
    string hints = $"Refresh: ~{refreshRate}fps";

    SetStatusBar(stats, hints);
}
```

### Impact
- **Performance**: Status bar updates trigger expensive data fetches
- **Separation of Concerns**: UpdateStatusBar() doing more than updating status
- **Predictability**: Side effects in what should be a read-only method
- **Frame Rate**: Could cause stuttering if data refresh is slow

### Analysis
The issue stems from EventInspectorContent having TWO update mechanisms:

1. **Automatic (frame-based)**: `Update()` increments frame counter and refreshes
2. **Manual**: `Refresh()` forces immediate refresh

Other content components handle refresh timing internally or externally.

### Recommended Fix

**Option A: External Update Call** (Recommended)
```csharp
// EventInspectorPanel.cs
protected override void OnRenderContainer(UIContext context)
{
    // Update content before base layout calculation
    _content.Update();  // ✓ Called once per frame in render

    base.OnRenderContainer(context);  // Calls UpdateStatusBar()
}

protected override void UpdateStatusBar()
{
    // ✓ Only reads cached data
    if (!_content.HasProvider)
    {
        SetStatusBar("No event data provider configured", "");
        return;
    }

    int refreshRate = 60 / _content.GetRefreshInterval();
    string hints = $"↑↓: Select Event | Tab: Toggle Details | R: Refresh | Refresh: ~{refreshRate}fps";
    SetStatusBar("Event Inspector Active", hints);
}
```

**Option B: DebugPanelBase Hook**
Add a new hook to DebugPanelBase for content updates:
```csharp
// DebugPanelBase.cs
protected virtual void UpdateContent()
{
    // Optional hook for panels to update content before status bar
}

protected override void OnRenderContainer(UIContext context)
{
    // ... existing code ...

    // Update content before status bar
    UpdateContent();
    UpdateStatusBar();
}

// EventInspectorPanel.cs
protected override void UpdateContent()
{
    _content.Update();
}
```

---

## VIOLATION 7: Data Provider Pattern Inconsistency

**Severity**: LOW (Architectural Note)
**Files**: Multiple panels

### Pattern Analysis

| Panel | Provider Type | Provider Pattern |
|-------|--------------|------------------|
| Stats | `Func<StatsData>?` | Polling function |
| Profiler | `Func<IReadOnlyDictionary<...>>?` | Polling function |
| EventInspector | `Func<EventInspectorData>?` | Polling function |
| Logs | Push via `AddLog()` | Push model |
| Console | Push via `AddOutput()` | Push model |

### Observation
Two different patterns for data flow:

1. **Pull (Polling)**: Stats, Profiler, EventInspector call provider functions
2. **Push**: Logs, Console receive data via method calls

### Impact
- **Flexibility**: Pull is better for high-frequency data (stats, events)
- **Performance**: Push is better for occasional data (logs, commands)
- **Consistency**: Mixed patterns can confuse developers

### Recommendation
Document the rationale:
```csharp
/// <summary>
/// Data Flow Patterns in Debug Panels:
///
/// PULL PATTERN (Func<T> provider):
/// - Used for: High-frequency, continuous data
/// - Examples: StatsPanel (60fps), ProfilerPanel (10fps), EventInspectorPanel
/// - Benefit: Panel controls refresh timing, caching, throttling
/// - Pattern: Panel calls provider.Invoke() at controlled intervals
///
/// PUSH PATTERN (Add/Append methods):
/// - Used for: Event-driven, occasional data
/// - Examples: LogsPanel, ConsolePanel
/// - Benefit: Data source controls when updates happen
/// - Pattern: External code calls panel.AddLog() / panel.AddOutput()
/// </summary>
```

This is NOT a violation - just an architectural note for documentation.

---

## Summary of Violations

| # | Violation | Severity | Files Affected | Fix Effort |
|---|-----------|----------|----------------|------------|
| 1 | Missing DebugPanelBase inheritance | CRITICAL | LogsPanel, ConsolePanel | HIGH |
| 2 | Inconsistent content base classes | HIGH | Stats, Profiler, EventInspector | MEDIUM |
| 3 | Hardcoded decorative characters | MEDIUM | EventInspectorContent | LOW |
| 4 | Inline color strings | HIGH | EventInspectorContent | MEDIUM |
| 5 | Inconsistent builder patterns | MEDIUM | EventInspectorPanelBuilder | LOW |
| 6 | Update in UpdateStatusBar | MEDIUM | EventInspectorPanel | LOW |
| 7 | Data provider pattern docs | LOW | N/A - Documentation | TRIVIAL |

---

## Prioritized Recommendations

### Phase 1: Critical Fixes (1-2 days)
1. **Refactor LogsPanel to extend DebugPanelBase**
   - Extract LogsContent component
   - Implement GetContentComponent() and UpdateStatusBar()
   - Test layout and theme switching

2. **Refactor ConsolePanel to extend DebugPanelBase** (if applicable)
   - Evaluate if ConsolePanel should follow panel pattern
   - May be intentionally different due to complex interactions

### Phase 2: High-Priority Fixes (1 day)
3. **Fix EventInspectorContent color system**
   - Replace string literals with Theme colors
   - Update GetPerformanceColor() to return Color
   - Test theme switching

4. **Standardize content component patterns**
   - Document when to use TextBuffer vs UIComponent
   - Consider TextBuffer for Stats/Profiler if beneficial

### Phase 3: Medium-Priority Polish (4 hours)
5. **Fix EventInspectorPanelBuilder**
   - Add static Create() method
   - Extract helper methods for content/status bar creation

6. **Move Update() call out of UpdateStatusBar()**
   - Call Update() in OnRenderContainer() instead
   - Test refresh timing

7. **Replace hardcoded separator**
   - Use calculated separator based on width
   - Add theme constant for separator character

### Phase 4: Documentation (1 hour)
8. **Document data provider patterns**
   - Add XML comments explaining pull vs push
   - Update architecture docs

---

## Framework Compliance Checklist

Use this checklist for future debug panels:

### Panel Class
- [ ] Extends `DebugPanelBase` (not Panel directly)
- [ ] Implements `GetContentComponent()` returning content component
- [ ] Implements `UpdateStatusBar()` for status display
- [ ] Uses `SetStatusBar(stats, hints)` helper
- [ ] Uses `SetStatusBarHealthColor()` for color coding
- [ ] Constructor calls `base(statusBar)` first
- [ ] Content added with `Anchor.StretchTop`

### Content Component
- [ ] Extends `TextBuffer` for text displays OR `UIComponent` with justification
- [ ] Uses `DebugPanelBase.StandardLinePadding` constant
- [ ] Implements data caching for performance
- [ ] Update methods are frame-rate independent
- [ ] Uses `ThemeManager.Current` for all colors
- [ ] Handles empty/null data gracefully

### Builder Class
- [ ] Provides `static Create()` factory method
- [ ] Uses method chaining pattern (`return this`)
- [ ] Extracts `CreateDefaultContent()` helper
- [ ] Extracts `CreateDefaultStatusBar()` helper
- [ ] Sets LayoutConstraints in helper methods
- [ ] Configures IDs consistently (`{panel}_content`, `{panel}_status`)

### Theme Integration
- [ ] No hardcoded colors (use `ThemeManager.Current`)
- [ ] No hardcoded dimensions (use theme constants or calculate)
- [ ] Border/background colors set in base class
- [ ] Colors update when theme changes

### Performance
- [ ] Data provider called at controlled intervals
- [ ] No expensive operations in UpdateStatusBar()
- [ ] Caching used for repeated data access
- [ ] Scrolling uses existing components (ScrollbarComponent)

---

## Testing Recommendations

After fixes, verify:

1. **Theme Switching**: Change theme, verify all colors update
2. **Layout**: Resize panel, verify content/status bar layout
3. **Performance**: Check frame rate with rapid updates
4. **Empty State**: Test with no data provider
5. **Scrolling**: Test with content exceeding viewport
6. **Status Bar**: Verify stats and hints display correctly

---

## Conclusion

The EventInspectorPanel demonstrates both good practices (extending DebugPanelBase, using TextBuffer) and areas for improvement (color system, builder pattern). The most critical issue is that LogsPanel and ConsolePanel don't follow the framework at all, creating maintenance burden and inconsistency.

**Immediate Action Items**:
1. Fix LogsPanel to extend DebugPanelBase (CRITICAL)
2. Fix EventInspectorContent color system (HIGH)
3. Document framework patterns for future developers

**Framework Strength**: DebugPanelBase provides excellent abstraction when used properly.
**Framework Weakness**: No enforcement - panels can bypass framework entirely.

---

**Report Generated by**: Hive Mind UX Component Framework Analyst
**Review Status**: Ready for Technical Lead Review
**Next Steps**: Prioritize fixes based on Phase recommendations
