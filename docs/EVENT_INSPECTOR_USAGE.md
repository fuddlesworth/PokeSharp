# Event Inspector Tool - Usage Guide

## Overview

The Event Inspector is a real-time debug UI tool that provides comprehensive visibility into the EventBus system. It displays:

- All registered event types with subscriber counts
- Active subscriptions with priorities and sources
- Real-time event logging (publish/receive)
- Performance metrics (average time, max time per event type)
- Per-handler performance tracking

## Architecture

### Components

1. **EventMetrics** (`PokeSharp.Engine.UI.Debug.Core.EventMetrics`)
   - Collects performance data from EventBus
   - Implements `IEventMetrics` interface
   - Thread-safe concurrent collection
   - Minimal overhead when disabled

2. **EventInspectorAdapter** (`PokeSharp.Engine.UI.Debug.Core.EventInspectorAdapter`)
   - Bridges EventBus and EventMetrics
   - Provides formatted data for UI
   - Manages event logging
   - Determines custom vs. built-in events

3. **EventInspectorPanel** (`PokeSharp.Engine.UI.Debug.Components.Debug.EventInspectorPanel`)
   - Main UI component
   - Scrollable display with status bar
   - Real-time updates
   - Interactive selection

4. **EventInspectorContent** (`PokeSharp.Engine.UI.Debug.Components.Debug.EventInspectorContent`)
   - Renders event data with color-coded performance
   - Hierarchical event/subscription view
   - Performance summaries

## Integration

### Step 1: Create Metrics and Adapter

```csharp
using PokeSharp.Engine.Core.Events;
using PokeSharp.Engine.UI.Debug.Core;
using PokeSharp.Engine.UI.Debug.Components.Debug;

// Get your EventBus instance
EventBus eventBus = serviceProvider.GetRequiredService<EventBus>();

// Create metrics collector
var eventMetrics = new EventMetrics();

// Create adapter (bridges bus + metrics)
var inspector = new EventInspectorAdapter(eventBus, eventMetrics, maxLogEntries: 100);

// Enable metrics collection (important!)
inspector.IsEnabled = true;
```

### Step 2: Build the UI Panel

```csharp
// Build the inspector panel
var panel = new EventInspectorPanelBuilder()
    .WithDataProvider(() => inspector.GetInspectorData())
    .WithRefreshInterval(1) // Update every frame
    .Build();
```

### Step 3: Add to Debug Scene

```csharp
// Add panel to your debug UI scene
debugScene.AddComponent(panel);

// Position it (example)
panel.Constraint.Width = 800;
panel.Constraint.Height = 600;
panel.Constraint.X = 100;
panel.Constraint.Y = 100;
```

### Step 4: Toggle Visibility

```csharp
// Toggle metrics collection (performance optimization)
inspector.IsEnabled = !inspector.IsEnabled;

// Show/hide panel
panel.Visible = !panel.Visible;
```

## Performance Impact

### When Disabled (Default)
- **Zero overhead**: All metric calls are no-ops
- No memory allocation
- No performance impact on production code

### When Enabled
- **Minimal overhead per event**:
  - ~0.001ms for `Stopwatch` creation/timing
  - Thread-safe dictionary operations
  - Microsecond-precision timing

### Optimization Tips

1. **Only enable when debugging**:
   ```csharp
   #if DEBUG
   inspector.IsEnabled = true;
   #endif
   ```

2. **Reduce refresh rate for complex UIs**:
   ```csharp
   .WithRefreshInterval(5) // Update every 5 frames (~12 FPS)
   ```

3. **Clear metrics periodically**:
   ```csharp
   inspector.ResetTimings(); // Keeps subscriber counts, clears timing data
   inspector.Clear();        // Full reset
   ```

## UI Controls

### Keyboard Shortcuts
- `↑` / `↓`: Select event in list
- `Tab`: Toggle subscription details
- `R`: Refresh display
- `Page Up` / `Page Down`: Scroll content
- `Home` / `End`: Jump to top/bottom

### Status Bar Information
- Shows current event count
- Displays refresh rate
- Real-time metrics summary

## Example Output

```
Event Inspector
━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━

📊 Active Events (15)

  ► [green]✓[/] MovementStartedEvent [cyan](8 subscribers)[/]
      [green]avg: 0.045ms, max: 0.120ms, count: 1250[/]
    [green]✓[/] TileSteppedOnEvent [cyan](12 subscribers)[/]
      [yellow]avg: 0.085ms, max: 0.350ms, count: 3420[/]
    [green]✓[/] LedgeJumpedEvent [cyan](3 subscribers)[/] [Custom]
      [green]avg: 0.032ms, max: 0.080ms, count: 45[/]

📝 Subscriptions for: [cyan]TileSteppedOnEvent[/]

  [Priority 1000] ledge_crumble.csx
    [green]avg: 0.028ms, max: 0.065ms, calls: 1240[/]
  [Priority 500] tall_grass.csx
    [yellow]avg: 0.042ms, max: 0.280ms, calls: 2180[/]
  [Priority 500] ice_crack.csx
    [green]avg: 0.015ms, max: 0.045ms, calls: 0[/]

📈 Performance Summary

  Slowest Events:
    [yellow]TileSteppedOnEvent                      [/] [yellow]0.085ms avg, 0.350ms max[/]
    [green]MovementStartedEvent                     [/] [green]0.045ms avg, 0.120ms max[/]
    [green]LedgeJumpedEvent                         [/] [green]0.032ms avg, 0.080ms max[/]

📋 Recent Events (last 10)

  [green]14:32:18.543[/] → [cyan]MovementStartedEvent[/] [green](0.042ms)[/]
  [yellow]14:32:18.545[/] ← [cyan]TileSteppedOnEvent[/] [#5] [yellow](0.120ms)[/]
  [green]14:32:18.547[/] → [cyan]TileSteppedOnEvent[/] [green](0.055ms)[/]
```

## Performance Color Coding

- **Green** (`< 0.1ms`): Excellent performance
- **Yellow** (`0.1-0.5ms`): Good performance
- **Orange** (`0.5-1.0ms`): Acceptable, watch for issues
- **Red** (`> 1.0ms`): Performance concern

## Advanced Features

### Custom Event Sources

Track which script or system created subscriptions:

```csharp
// When subscribing from a script
eventBus.Subscribe<TileSteppedOnEvent>(handler);

// EventMetrics will try to infer source from call stack
// Or manually specify:
eventMetrics.RecordSubscription(
    "TileSteppedOnEvent",
    handlerId,
    source: "tall_grass.csx",
    priority: 500
);
```

### Filtering (Future Enhancement)

```csharp
var data = inspector.GetInspectorData();
data.Filters.EventTypeFilter = "Movement";
data.Filters.SourceFilter = "ledge";
data.Filters.ShowOnlyActive = true;
```

## Troubleshooting

### Panel shows "No event data provider configured"
- Ensure you called `panel.SetDataProvider(() => inspector.GetInspectorData())`
- Check that adapter is created with valid EventBus

### No metrics appearing
- Verify `inspector.IsEnabled = true`
- Ensure EventBus.Metrics is set to your EventMetrics instance
- Check that events are actually being published

### Performance degradation
- Disable metrics: `inspector.IsEnabled = false`
- Increase refresh interval: `panel.SetRefreshInterval(10)`
- Clear old data: `inspector.ResetTimings()`

## Example: Full Integration in Game

```csharp
public class DebugEventInspectorInitializer
{
    public static EventInspectorPanel CreateEventInspector(
        EventBus eventBus,
        DebugScene debugScene)
    {
        // Create metrics system
        var metrics = new EventMetrics();
        var adapter = new EventInspectorAdapter(eventBus, metrics);

        // Build panel
        var panel = new EventInspectorPanelBuilder()
            .WithDataProvider(() => adapter.GetInspectorData())
            .WithRefreshInterval(2) // 30 FPS refresh
            .Build();

        // Configure layout
        panel.Constraint.Anchor = Anchor.StretchAll;
        panel.Constraint.Margin = 10;

        // Add to scene
        debugScene.AddComponent(panel);

        // Toggle with F9
        InputManager.OnKeyPressed(Keys.F9, () =>
        {
            adapter.IsEnabled = !adapter.IsEnabled;
            panel.Visible = adapter.IsEnabled;
        });

        return panel;
    }
}
```

## API Reference

### EventMetrics
- `bool IsEnabled`: Enable/disable collection
- `RecordPublish(string, long)`: Record event publish
- `RecordHandlerInvoke(string, int, long)`: Record handler execution
- `RecordSubscription(string, int, string?, int)`: Record new subscription
- `RecordUnsubscription(string, int)`: Record removed subscription
- `GetAllEventMetrics()`: Get all event type metrics
- `GetSubscriptionMetrics(string)`: Get subscriptions for event type
- `Clear()`: Clear all metrics
- `ResetTimings()`: Reset timing data, keep counts

### EventInspectorAdapter
- `bool IsEnabled`: Enable/disable metrics
- `GetInspectorData()`: Get formatted UI data
- `LogPublish(string, double, string?)`: Log publish operation
- `LogHandlerInvoke(string, int, double, string?)`: Log handler call
- `Clear()`: Clear all data
- `ResetTimings()`: Reset performance data

### EventInspectorPanel
- `SetDataProvider(Func<EventInspectorData>)`: Set data source
- `Refresh()`: Force immediate update
- `SetRefreshInterval(int)`: Set frame refresh rate
- `ToggleSubscriptions()`: Show/hide subscription details
- `SelectNextEvent()`: Navigate to next event
- `SelectPreviousEvent()`: Navigate to previous event
- `ScrollUp(int)` / `ScrollDown(int)`: Scroll content

## See Also

- [Event System Documentation](./EVENT_SYSTEM.md)
- [Debug UI Architecture](../PokeSharp.Engine.UI.Debug/UI_ARCHITECTURE.md)
- [Implementation Roadmap](./IMPLEMENTATION-ROADMAP.md) - Phase 5.2
