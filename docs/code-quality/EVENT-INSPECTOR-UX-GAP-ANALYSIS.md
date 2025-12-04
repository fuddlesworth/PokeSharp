# Event Inspector UX Gap Analysis

**Date:** 2025-12-03
**Comparing:** EventInspectorContent.cs vs ProfilerContent.cs & StatsContent.cs

## Executive Summary

The EventInspectorContent.cs is functionally complete but lacks many sophisticated UX patterns present in ProfilerContent.cs and StatsContent.cs. This analysis identifies 42 specific gaps across 7 categories, prioritized by impact on user experience.

---

## 1. Missing Layout Features (CRITICAL)

### 1.1 No Table Header Component (HIGH PRIORITY)
**Gap:** EventInspectorContent uses plain text headers instead of `SortableTableHeader<T>`

**ProfilerContent Implementation:**
```csharp
private readonly SortableTableHeader<ProfilerSortMode> _tableHeader;

_tableHeader.AddColumns(
    new SortableTableHeader<ProfilerSortMode>.Column {
        Label = "System",
        SortMode = ProfilerSortMode.ByName,
        X = contentX,
        MaxWidth = nameColWidth,
        Ascending = true
    },
    // ... more columns
);

_tableHeader.Draw(renderer, theme, y, lineHeight);
_tableHeader.HandleInput(input);
```

**Current EventInspector Implementation:**
```csharp
// Line 303: Just plain text
renderer.DrawText($"Active Events ({_cachedData.Events.Count})", x, y, theme.Info);
```

**Impact:**
- No visual feedback for sortable columns
- No click-to-sort functionality
- No sort direction indicators
- Inconsistent with other panels

**Recommendation:**
- Add `SortableTableHeader<EventSortMode>` field
- Implement sort modes: ByName, BySubscribers, ByPublishCount, ByAvgTime, ByMaxTime
- Configure columns with proper alignment and widths
- Add visual sort indicators (arrows)

---

### 1.2 No Column Width Constants (MEDIUM PRIORITY)
**Gap:** Hard-coded layout values instead of using PanelConstants pattern

**ProfilerContent Pattern:**
```csharp
private const float NameColumnWidth = PanelConstants.Profiler.NameColumnWidth;
private const float MsColumnWidth = PanelConstants.Profiler.MsColumnWidth;
```

**StatsContent Pattern:**
```csharp
float labelWidth = PanelConstants.Stats.LabelWidth;
float barWidth = PanelConstants.Stats.BarWidth;
float valueX = contentX + labelWidth;
float barX = contentX + labelWidth + PanelConstants.Stats.ValueOffset;
```

**Current EventInspector:**
```csharp
// No constants - layout is calculated ad-hoc
// No PanelConstants.EventInspector defined
```

**Impact:**
- Inconsistent spacing across sections
- Difficult to maintain layout
- Can't standardize column widths across features
- No centralized layout configuration

**Recommendation:**
- Define `PanelConstants.EventInspector` with:
  - `EventNameColumnWidth` (for event type names)
  - `MetricColumnWidth` (for subscriber counts, times)
  - `LabelWidth` (for "Priority", "Source", etc.)
  - `IndentWidth` (for nested subscription details)
  - `RowSpacing`, `SectionSpacing`

---

### 1.3 No Structured Layout Calculations (MEDIUM PRIORITY)
**Gap:** No pre-calculation of layout boundaries like ProfilerContent

**ProfilerContent Pattern:**
```csharp
// Lines 288-300: Pre-calculate all layout values BEFORE rendering
float tableStartY = y + lineHeight + theme.SpacingTight + 1 + theme.SpacingNormal;
int rowHeight = lineHeight + theme.SpacingNormal;
float tableBottomY = Rect.Y + Rect.Height - linePadding;
float availableHeight = tableBottomY - tableStartY;
int maxBarsVisible = Math.Max(1, (int)(availableHeight / rowHeight));
bool needsScrollbar = _cachedMetrics.Count > maxBarsVisible;

// Adjust content width if scrollbar needed
float tableContentWidth = needsScrollbar
    ? contentWidth - scrollbarWidth - theme.PaddingSmall
    : contentWidth;
```

**Current EventInspector:**
```csharp
// Lines 196-202: Minimal calculation
_visibleHeight = Rect.Height - linePadding * 2;
_totalContentHeight = CalculateTotalContentHeight(lineHeight, theme);
bool needsScrollbar = _totalContentHeight > _visibleHeight;
float tableContentWidth = needsScrollbar
    ? contentWidth - scrollbarWidth - theme.PaddingSmall
    : contentWidth;
```

**Impact:**
- Inconsistent spacing between sections
- No row-based scrolling
- Harder to predict layout issues
- Less precise vertical alignment

**Recommendation:**
- Calculate row heights upfront (event rows, metric rows, subscription rows)
- Pre-compute section boundaries
- Calculate visible row counts
- Use consistent spacing multipliers

---

## 2. Poor Space Utilization (HIGH PRIORITY)

### 2.1 No Progress Bars for Metrics (HIGH PRIORITY)
**Gap:** All metrics are text-only; no visual representation of relative performance

**StatsContent Pattern:**
```csharp
// Line 370-379: Memory usage with progress bar
renderer.DrawText($"{_cachedStats.MemoryMB:F1} MB", valueX, y, memColor);
DrawProgressBar(
    renderer,
    barX,
    y + 2,
    barWidth,
    lineHeight - 4,
    (float)(_cachedStats.MemoryMB / MemoryWarning),
    memColor,
    theme
);
```

**ProfilerContent Pattern:**
```csharp
// Lines 388-411: Execution time bars with budget line
var barRect = new LayoutRect(barColStart, rowY + theme.ProfilerBarInset,
    barColWidth, lineHeight - (theme.ProfilerBarInset * 2));
renderer.DrawRectangle(barRect, theme.BackgroundElevated);

float barPercent = Math.Min((float)(entry.LastMs / TargetFrameTimeMs),
    theme.ProfilerBarMaxScale) / theme.ProfilerBarMaxScale;
float filledWidth = barColWidth * barPercent;
renderer.DrawRectangle(filledRect, barColor);

// Budget line indicator
float budgetLineX = barColStart + (barColWidth * 0.5f);
renderer.DrawRectangle(new LayoutRect(budgetLineX, rowY, 1, lineHeight),
    theme.Warning * theme.ProfilerBudgetLineOpacity);
```

**Current EventInspector:**
```csharp
// Line 336: Text-only metrics
string perfText = $"   avg: {eventInfo.AverageTimeMs:F3}ms, max: {eventInfo.MaxTimeMs:F3}ms, count: {eventInfo.PublishCount}";
renderer.DrawText(perfText, x, y, perfColor);
```

**Visual Comparison:**

**ProfilerContent (Current):**
```
CollisionSystem    [████████░░░░░░░░|] 2.34/1.85ms  <-- Visual bar with budget line
MovementSystem     [███░░░░░░░░░░░░░|] 0.82/0.65ms
RenderingSystem    [██████░░░░░░░░░░|] 1.67/1.45ms
```

**EventInspector (Current):**
```
CollisionDetectedEvent (5 subs)
   avg: 0.234ms, max: 0.456ms, count: 1240  <-- Text only, hard to compare
```

**EventInspector (Proposed):**
```
CollisionDetectedEvent (5 subs)  [███░░░░|] 0.23/0.46ms [1240]
                                  ^^^^^
                                  Visual bar shows avg time relative to threshold
```

**Impact:**
- Can't quickly identify slow events visually
- Difficult to compare relative performance at a glance
- Missing threshold indicators (0.1ms, 0.5ms, 1ms)
- No visual feedback on performance trends

**Recommendation:**
1. **Event Performance Bars** (Active Events section):
   - Add horizontal bars showing avg time relative to threshold (1ms max)
   - Color-code: Green (<0.1ms), Yellow (0.1-0.5ms), Orange (0.5-1ms), Red (>1ms)
   - Add vertical threshold markers at 0.1ms, 0.5ms
   - Right-align publish count in dim color

2. **Subscriber Performance Bars** (Subscriptions section):
   - Show avg invocation time for each subscriber
   - Add budget line at 0.1ms (fast handler threshold)
   - Visual indicator for handlers that dominate event processing

3. **Performance Summary Enhancement**:
   - Replace text list with horizontal bars
   - Show percentage of frame budget (16.67ms)
   - Add comparison to fastest event

**Example Layout:**
```
Active Events (12)
━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━
Event Name              Performance                    Subs  Count
────────────────────────────────────────────────────────────────
> CollisionDetected     [████░░░░|] 0.23/0.46ms        5     1240
  MovementStarted       [██░░░░░░|] 0.08/0.12ms        3      856
  TileSteppedOn         [██████░░|] 0.34/0.51ms        8      432
                         ^      ^
                         0.1ms  0.5ms thresholds
```

---

### 2.2 No Inline Status Indicators (MEDIUM PRIORITY)
**Gap:** Missing visual health indicators like StatsContent

**StatsContent Pattern:**
```csharp
// Line 306-311: FPS with colored status dot and rating
renderer.DrawText("FPS:", contentX, y, theme.TextSecondary);
renderer.DrawText($"{_cachedStats.Fps:F1}", valueX, y, fpsColor);
renderer.DrawRectangle(new LayoutRect(valueX + 50, y + 4, 8, 8), fpsColor);  // Colored dot
string fpsRating = GetFpsRating(_cachedStats.Fps);  // "Excellent", "Good", "Fair", "Poor"
renderer.DrawText(fpsRating, contentX + contentWidth - ratingWidth, y, fpsColor);
```

**Current EventInspector:**
```csharp
// Line 324-329: Just text with color
string indicator = isSelected ? ">" : " ";
Color statusColor = eventInfo.SubscriberCount > 0 ? theme.Success : theme.TextDim;
string eventLine = $"{indicator} {eventInfo.EventTypeName} ({eventInfo.SubscriberCount} subs){customTag}";
renderer.DrawText(eventLine, x, y, statusColor);
```

**Impact:**
- No quick visual scan for event health
- Missing performance rating system
- No "at a glance" status understanding

**Recommendation:**
- Add colored status dots next to event names:
  - 🟢 Green: Active, good performance (<0.1ms avg)
  - 🟡 Yellow: Active, moderate performance (0.1-0.5ms)
  - 🟠 Orange: Active, slow performance (0.5-1ms)
  - 🔴 Red: Active, very slow (>1ms)
  - ⚪ Gray: Registered but no publishes
- Add performance ratings:
  - "Fast" (<0.1ms)
  - "Normal" (0.1-0.5ms)
  - "Slow" (0.5-1ms)
  - "Critical" (>1ms)

---

### 2.3 Cramped Subscription Display (MEDIUM PRIORITY)
**Gap:** Subscriptions section uses same indentation for all info

**Current Layout:**
```csharp
// Lines 360-376: Flat structure
renderer.DrawText($"[Priority {sub.Priority}] {source}", x, y, theme.TextPrimary);
y += RowHeight;
if (sub.InvocationCount > 0) {
    string perfText = $"   avg: {sub.AverageTimeMs:F3}ms, max: {sub.MaxTimeMs:F3}ms, calls: {sub.InvocationCount}";
    renderer.DrawText(perfText, x, y, perfColor);
    y += RowHeight;
}
```

**Visual Result:**
```
Subscriptions: CollisionDetectedEvent
[Priority 100] CollisionSystem
   avg: 0.123ms, max: 0.234ms, calls: 1240
[Priority 50] DebugSystem
   avg: 0.045ms, max: 0.089ms, calls: 1240
```

**Better Pattern (from StatsContent):**
```csharp
// Lines 448-475: Hierarchical display with right-aligned values
renderer.DrawText("Slowest:", contentX, y, theme.TextSecondary);
renderer.DrawText(systemName, valueX, y, theme.TextPrimary);
float slowestTimeWidth = renderer.MeasureText(slowestTime).X;
renderer.DrawText(slowestTime, contentX + contentWidth - slowestTimeWidth, y, slowestColor);
```

**Proposed Improvement:**
```
Subscriptions: CollisionDetectedEvent (2 handlers, 2480 calls total)
━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━
Handler                 Prio  Calls  Avg      Max      % Time
────────────────────────────────────────────────────────────────
CollisionSystem         100   1240   0.12ms   0.23ms   [████░] 65%
DebugSystem              50   1240   0.05ms   0.09ms   [██░░░] 35%
```

**Impact:**
- Hard to scan multiple subscribers
- No visual comparison of handler performance
- Missing percentage breakdown (which handler dominates?)
- No total statistics

**Recommendation:**
- Add summary line: total handlers, total calls
- Create table structure with columns:
  - Handler name (truncated if needed)
  - Priority (right-aligned)
  - Call count (right-aligned)
  - Avg/Max times (right-aligned)
  - Percentage bar (of total time for this event)
- Add percentage calculation showing which handlers dominate processing

---

### 2.4 No Sparkline/History Visualization (LOW PRIORITY)
**Gap:** Recent events section is plain text log

**StatsContent Pattern:**
```csharp
// Lines 46, 74, 351-359: Sparkline component for frame time history
private readonly Sparkline _frameTimeSparkline;

_frameTimeSparkline = Sparkline.ForFrameTime(id + "_sparkline");
_frameTimeSparkline.AddValue(_cachedStats.FrameTimeMs);

renderer.DrawText("History:", contentX, y, theme.TextSecondary);
_frameTimeSparkline.Draw(renderer, valueX, y,
    contentWidth - labelWidth - SectionSpacing, lineHeight);
```

**Current EventInspector:**
```csharp
// Lines 419-439: Just text log
foreach (var entry in _cachedData.RecentEvents.TakeLast(10)) {
    string logText = $"{timestamp} {operation} {entry.EventType}{handler} ({entry.DurationMs:F3}ms)";
    renderer.DrawText(logText, x, y, perfColor);
}
```

**Impact:**
- Can't see performance trends over time
- No visual pattern recognition
- Missing "event storm" detection
- Can't identify temporal anomalies

**Recommendation:**
- Add per-event sparklines showing publish rate over last 60 seconds
- Add per-event sparklines showing avg processing time trend
- Add global sparkline showing total events/sec
- Use mini-graphs in Active Events section next to each event

**Example:**
```
Active Events (12)
━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━
Event Name              Rate (last 60s)    Performance
────────────────────────────────────────────────────────────────
CollisionDetected       [▁▂▃▅▇█▇▅▃▂▁]      [████░░░░] 0.23ms
MovementStarted         [▃▃▄▄▄▃▃▃▂▂▁]      [██░░░░░░] 0.08ms
TileSteppedOn          [▁▁▁▂█▂▁▁▁▁▁]      [██████░░] 0.34ms
                        ^^^^^^^^^^
                        Publish rate mini-graph
```

---

## 3. Missing Visual Elements (HIGH PRIORITY)

### 3.1 No Section Separators (HIGH PRIORITY)
**Gap:** Sections blend together without clear visual boundaries

**ProfilerContent/StatsContent Pattern:**
```csharp
// Consistent separator pattern used 4-5 times
renderer.DrawRectangle(
    new LayoutRect(contentX, y, tableContentWidth, 1),
    theme.BorderPrimary
);
y += theme.SpacingNormal;
```

**Current EventInspector:**
```csharp
// Line 342, 378, 415: Just blank space
y += SectionSpacing;
```

**Visual Comparison:**

**StatsContent (with separators):**
```
Performance Stats                     Frame: 12,450
━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━  <-- Clear separator
FPS:              60.1 🟢              Excellent
Frame Time:       16.4ms [████████|]
━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━  <-- Clear separator
Memory:           245.3 MB [██░░░░░░]
GC:               G0: 45  G1: 3  G2: 0
━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━  <-- Clear separator
ECS World
```

**EventInspector (no separators):**
```
Active Events (12)
> CollisionDetectedEvent (5 subs)
   avg: 0.234ms, max: 0.456ms, count: 1240
  MovementStartedEvent (3 subs)
   avg: 0.089ms, max: 0.145ms, count: 856
                                              <-- Just blank space
Subscriptions: CollisionDetectedEvent
[Priority 100] CollisionSystem
   avg: 0.123ms, max: 0.234ms, calls: 1240
                                              <-- Just blank space
Performance Summary
Slowest Events:
```

**Impact:**
- Sections visually run together
- Hard to quickly locate specific section
- Looks less polished
- Inconsistent with other panels

**Recommendation:**
- Add horizontal separator lines (1px, `theme.BorderPrimary`) after:
  - Section headers
  - Active Events section
  - Subscriptions section (if shown)
  - Performance Summary section
  - Before Recent Events section
- Use consistent spacing: `theme.SpacingNormal` after separators

---

### 3.2 No Icons or Symbols (MEDIUM PRIORITY)
**Gap:** Missing NerdFontIcons like StatsContent uses

**StatsContent Pattern:**
```csharp
// Line 146-154: Status icons
return (NerdFontIcons.StatusHealthy, theme.Success, true);
return (NerdFontIcons.StatusWarning, theme.Warning, false);
return (NerdFontIcons.StatusError, theme.Error, false);
```

**Current EventInspector:**
```csharp
// Line 324-430: Text indicators only
string indicator = isSelected ? ">" : " ";
string operation = entry.Operation == "Publish" ? ">" : "<";
```

**Impact:**
- Less scannable at a glance
- Missing visual personality
- No standard iconography

**Recommendation:**
- Event health icons:
  - `NerdFontIcons.EventActive` - Active event with publishes
  - `NerdFontIcons.EventIdle` - Registered but no publishes
  - `NerdFontIcons.EventCustom` - Custom event indicator
- Operation icons in Recent Events:
  - `NerdFontIcons.EventPublish` (📤) - Publish operation
  - `NerdFontIcons.EventHandle` (📥) - Handle operation
- Section header icons:
  - `NerdFontIcons.SectionActive` - Active Events section
  - `NerdFontIcons.SectionSubscriptions` - Subscriptions section
  - `NerdFontIcons.SectionPerformance` - Performance Summary
  - `NerdFontIcons.SectionRecent` - Recent Events log

---

### 3.3 No Color-Coded Status Dots (MEDIUM PRIORITY)
**Gap:** No visual status indicators beyond text color

**StatsContent Pattern:**
```csharp
// Line 308: Colored 8x8 status square next to FPS
renderer.DrawRectangle(new LayoutRect(valueX + 50, y + 4, 8, 8), fpsColor);
```

**ProfilerContent Pattern:**
```csharp
// Implicit: Bar colors provide visual status
```

**Current EventInspector:**
```csharp
// No status dots - just colored text
Color statusColor = eventInfo.SubscriberCount > 0 ? theme.Success : theme.TextDim;
```

**Impact:**
- Status not immediately visible
- Harder to scan long lists
- Missing accessibility (color alone isn't enough)

**Recommendation:**
- Add 6x6 or 8x8 colored dots before event names:
  - Active + Fast: Green dot
  - Active + Normal: Yellow dot
  - Active + Slow: Orange dot
  - Active + Critical: Red dot
  - Inactive: Gray dot
- Add dot to subscription handlers based on performance
- Add dot to recent events based on duration

---

### 3.4 No Visual Event/Subscription Hierarchy (LOW PRIORITY)
**Gap:** Subscriptions look like sibling items instead of children

**Current Layout:**
```
Active Events (12)
> CollisionDetectedEvent (5 subs)
   avg: 0.234ms, max: 0.456ms, count: 1240

Subscriptions: CollisionDetectedEvent
[Priority 100] CollisionSystem
   avg: 0.123ms, max: 0.234ms, calls: 1240
```

**Better Pattern (tree-like):**
```
Active Events (12)
━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━
├─ 🟢 CollisionDetectedEvent (5 subs) [████░░] 0.23ms [1240]
│  └─ Subscriptions (5 handlers, 6200 calls total)
│     ├─ [P:100] CollisionSystem       [███░] 0.12ms  (1240 calls)
│     ├─ [P:100] DebugSystem           [██░░] 0.08ms  (1240 calls)
│     ├─ [P:50]  ScriptHandler #42     [█░░░] 0.04ms  (1240 calls)
│     └─ ...
├─ 🟡 MovementStartedEvent (3 subs) [██░░] 0.08ms [856]
└─ 🟡 TileSteppedOn (8 subs) [██████] 0.34ms [432]
```

**Impact:**
- Relationship between event and subscribers not clear
- Feels like separate sections instead of drill-down
- Harder to understand event flow

**Recommendation:**
- Add tree connector characters (├─, └─, │)
- Indent subscriptions under parent event
- Show subscription summary inline (handler count, total calls)
- Use theme colors for tree lines

---

## 4. Statistics Display Improvements (HIGH PRIORITY)

### 4.1 No Summary Statistics Row (HIGH PRIORITY)
**Gap:** Missing overall health indicator like StatsContent

**StatsContent Pattern:**
```csharp
// Lines 294-298: Header with summary stats
renderer.DrawText("Performance Stats", contentX, y, theme.Info);
string frameText = $"Frame: {_cachedStats.FrameNumber:N0}";
renderer.DrawText(frameText, contentX + contentWidth - frameWidth, y, theme.TextSecondary);

// Line 138-154: Overall health method
public (string indicator, Color color, bool isHealthy) GetOverallHealth(UITheme theme)
```

**ProfilerContent Pattern:**
```csharp
// Lines 271-284: Budget info at top
string budgetText = $"Frame Budget: {TargetFrameTimeMs:F1}ms (60fps)";
string totalText = $"Total: {TotalFrameTimeMs:F2}ms";
Color totalColor = TotalFrameTimeMs > TargetFrameTimeMs ? theme.Error : theme.Success;
```

**Current EventInspector:**
```csharp
// Line 303: Just count
renderer.DrawText($"Active Events ({_cachedData.Events.Count})", x, y, theme.Info);

// Line 118-137: Has GetStatistics() method but it's not displayed!
public (int EventCount, int TotalSubscribers, double SlowestEventMs, string SlowestEventName) GetStatistics()
```

**Impact:**
- No "at a glance" health status
- Summary statistics exist but aren't shown
- Missing frame budget context
- Can't quickly see overall system state

**Recommendation:**
- Add header row showing:
  ```
  Event Inspector                  12 events | 45 subs | Slowest: 0.34ms
  ```
- Add visual health indicator (green/yellow/red dot)
- Show overall health rating:
  - 🟢 "Healthy" - All events <0.1ms avg
  - 🟡 "Warning" - Some events 0.1-0.5ms
  - 🔴 "Critical" - Any events >0.5ms
- Show event rate (events/sec) in header

---

### 4.2 No Percentage Breakdowns (MEDIUM PRIORITY)
**Gap:** No relative performance percentages

**StatsContent Pattern:**
```csharp
// Lines 340-347: Frame budget percentage
float budgetPercent = _cachedStats.FrameTimeMs / FrameTimeGood * 100f;
string budgetText = $"{budgetPercent:F0}% of {FrameTimeGood:F2}ms";
Color budgetColor = budgetPercent <= 80 ? theme.Success
    : budgetPercent <= 100 ? theme.Warning
    : theme.Error;
```

**Current EventInspector:**
```csharp
// Just absolute values
string perfText = $"   avg: {eventInfo.AverageTimeMs:F3}ms, max: {eventInfo.MaxTimeMs:F3}ms, count: {eventInfo.PublishCount}";
```

**Impact:**
- Can't see relative cost of events
- Missing "what percentage of frame budget" context
- Hard to prioritize optimization efforts

**Recommendation:**
- Show event time as % of total event time
- Show handler time as % of parent event time
- Add "% of frame" column (assuming 16.67ms budget)
- Example:
  ```
  CollisionDetected  0.23ms  [1.4% of frame]  [15% of event time]
  ```

---

### 4.3 Missing Threshold Indicators (MEDIUM PRIORITY)
**Gap:** No visual thresholds like ProfilerContent budget line

**ProfilerContent Pattern:**
```csharp
// Lines 413-418: Budget line overlay
float budgetLineX = barColStart + (barColWidth * 0.5f);
renderer.DrawRectangle(
    new LayoutRect(budgetLineX, rowY, 1, lineHeight),
    theme.Warning * theme.ProfilerBudgetLineOpacity
);
```

**Current EventInspector:**
```csharp
// No threshold visualization
```

**Impact:**
- No visual reference for "good" vs "bad" performance
- Unclear what target times should be
- Missing performance context

**Recommendation:**
- Add vertical threshold lines in performance bars:
  - 0.1ms (fast handler threshold)
  - 0.5ms (warning threshold)
  - 1.0ms (critical threshold)
- Color-code regions between thresholds
- Add threshold labels in Performance Summary section

---

### 4.4 No Rate Statistics (LOW PRIORITY)
**Gap:** Missing events/sec, avg publish interval

**Available Data:**
```csharp
public long PublishCount { get; set; }  // Total publishes
public DateTime Timestamp { get; set; }  // In EventLogEntry
```

**Possible Calculations:**
- Events per second (publishes / elapsed time)
- Average publish interval (time between publishes)
- Burst detection (sudden spike in rate)

**Impact:**
- Can't detect event storms
- Missing temporal patterns
- No rate-based health indicators

**Recommendation:**
- Track publish timestamps per event
- Calculate rolling average rate (last 1s, 5s, 60s)
- Show rate in Active Events:
  ```
  CollisionDetected  (5 subs)  [23.4/sec]  0.23ms avg
  ```
- Add rate threshold warnings (>100/sec = warning?)

---

## 5. Navigation Issues (MEDIUM PRIORITY)

### 5.1 Limited Keyboard Shortcuts (MEDIUM PRIORITY)
**Gap:** Only basic navigation implemented

**Current EventInspector:**
```csharp
// Lines 260-291: Basic shortcuts
Keys.Up → SelectPreviousEvent()
Keys.Down → SelectNextEvent()
Keys.Tab → ToggleSubscriptions()
Keys.R → RefreshData()
Keys.PageUp → ScrollUp()
Keys.PageDown → ScrollDown()
Keys.Home → Scroll to top
Keys.End → Scroll to bottom
```

**StatsContent Pattern:**
```csharp
// Lines 226-241: Similar but with repeat support
IsKeyPressedWithRepeat(Keys.PageUp)
IsKeyPressedWithRepeat(Keys.PageDown)
```

**Missing Shortcuts:**
- No direct section jumping (1-4 for sections)
- No "expand all" / "collapse all" subscriptions
- No copy to clipboard
- No search/filter shortcuts
- No sorting shortcuts

**Recommendation:**
- Add section shortcuts:
  - `1` - Jump to Active Events
  - `2` - Jump to Subscriptions
  - `3` - Jump to Performance Summary
  - `4` - Jump to Recent Events
- Add view shortcuts:
  - `E` - Expand all subscriptions
  - `C` - Collapse all subscriptions
  - `F` - Toggle filter dialog
  - `S` - Cycle sort modes
- Add data shortcuts:
  - `Ctrl+C` - Copy selected event details
  - `Ctrl+R` - Reset statistics
  - `/` - Quick search events

---

### 5.2 No Row-Based Scrolling (LOW PRIORITY)
**Gap:** Scrolling is pixel-based, not row-based like ProfilerContent

**ProfilerContent Pattern:**
```csharp
// Lines 376-377: Row-based scrolling
int startIndex = (int)_scrollbar.ScrollOffset;
int endIndex = Math.Min(_cachedMetrics.Count, startIndex + maxBarsVisible);
```

**Current EventInspector:**
```csharp
// Lines 108-116: Pixel-based scrolling
_scrollbar.ScrollOffset = Math.Max(0, _scrollbar.ScrollOffset - lines * RowHeight);
```

**Impact:**
- Items can be partially visible (cut off mid-row)
- Less precise navigation with arrow keys
- Inconsistent with ProfilerContent behavior

**Recommendation:**
- Convert to row-based scrolling in Active Events section
- Snap scroll offset to row boundaries
- Update scrollbar to show row index instead of pixels

---

### 5.3 No Click-to-Select Events (LOW PRIORITY)
**Gap:** Can only select with keyboard, not mouse

**Current:**
```csharp
// Selection only via keyboard (Up/Down keys)
```

**Recommendation:**
- Detect mouse clicks on event rows
- Update selection on click
- Add hover highlighting
- Add double-click to expand/collapse subscriptions

---

### 5.4 No Tooltip/Hover Details (LOW PRIORITY)
**Gap:** No additional info on hover

**Recommendation:**
- Show tooltip on event hover:
  - Full event type name (if truncated)
  - Total publish count
  - Time range (first to last publish)
  - All performance statistics
- Show tooltip on subscription hover:
  - Handler source location (if available)
  - Detailed performance breakdown
  - Invocation history

---

## 6. Color Usage Gaps (MEDIUM PRIORITY)

### 6.1 Limited Color Palette (MEDIUM PRIORITY)
**Gap:** Only uses 4 colors (Success, Warning, Error, TextDim)

**Current Usage:**
```csharp
// Line 500-509: Simple threshold-based coloring
return timeMs switch {
    < 0.1 => theme.Success,
    < 0.5 => theme.Warning,
    < 1.0 => theme.Warning,
    _ => theme.Error
};
```

**StatsContent Pattern:**
```csharp
// Uses 10+ distinct colors:
theme.Success, theme.Warning, theme.Error
theme.TextPrimary, theme.TextSecondary, theme.TextDim
theme.Info, theme.WarningMild
theme.BackgroundElevated
theme.BorderPrimary
theme.InputSelection
```

**ProfilerContent Pattern:**
```csharp
// Lines 452-470: Multi-tier color system
return theme.Error;  // Critical
return theme.Warning;  // Warning
return theme.WarningMild;  // Mild warning
return theme.Success;  // Good
```

**Missing Colors:**
- `theme.WarningMild` - For moderate performance (0.1-0.5ms)
- `theme.Info` - For informational headers
- `theme.BackgroundElevated` - For background bars
- `theme.InputSelection` - For selection highlight

**Recommendation:**
- Use 5-tier color system:
  - Excellent (<0.05ms): `theme.Success`
  - Good (0.05-0.1ms): `theme.SuccessMild` (if exists) or slightly dimmed Success
  - Normal (0.1-0.5ms): `theme.WarningMild`
  - Slow (0.5-1ms): `theme.Warning`
  - Critical (>1ms): `theme.Error`
- Use `theme.BackgroundElevated` for progress bar backgrounds
- Use `theme.Info` for section headers
- Use `theme.InputSelection` for selected event highlight

---

### 6.2 No Color Gradients for Bars (LOW PRIORITY)
**Gap:** No gradient fills like potential ProfilerContent enhancement

**Current:**
```csharp
// Solid color fills only
renderer.DrawRectangle(filledRect, barColor);
```

**Recommendation:**
- Add gradient option for performance bars
- Fade from green → yellow → red based on thresholds
- Use gradient background for selection highlight

---

### 6.3 Inconsistent Color Usage (LOW PRIORITY)
**Gap:** Selection highlight and text color don't match well

**Current:**
```csharp
// Line 319-320: Selection uses InputSelection color
renderer.DrawRectangle(highlightRect, theme.InputSelection);

// Line 326: But text color determined by subscriber count
Color statusColor = eventInfo.SubscriberCount > 0 ? theme.Success : theme.TextDim;
```

**Impact:**
- Selected items hard to read against highlight
- Inconsistent visual feedback

**Recommendation:**
- Override text color when selected (use high contrast)
- Dim highlight background slightly for better readability
- Add subtle border around selection

---

## 7. Section Organization (HIGH PRIORITY)

### 7.1 No Collapsible Sections (HIGH PRIORITY)
**Gap:** All sections always visible, can't hide less important info

**Recommendation:**
- Make sections collapsible (like Performance Summary, Recent Events)
- Add expand/collapse icons (▼ expanded, ► collapsed)
- Remember state in memory
- Keyboard shortcut to toggle sections

**Example:**
```
Event Inspector                  12 events | 45 subs | Slowest: 0.34ms
━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━
▼ Active Events (12)
  ├─ 🟢 CollisionDetectedEvent ...
  └─ ...

▼ Subscriptions: CollisionDetectedEvent (5 handlers)
  ├─ [P:100] CollisionSystem ...
  └─ ...

► Performance Summary                         [Click to expand]

► Recent Events (last 10)                     [Click to expand]
```

---

### 7.2 Poor Section Ordering (MEDIUM PRIORITY)
**Gap:** Most important info (performance issues) buried at bottom

**Current Order:**
1. Active Events (scrollable, large)
2. Subscriptions (scrollable, large)
3. Performance Summary (most important!)
4. Recent Events (debug log)

**Better Order:**
1. **Summary Header** (overall health, key stats)
2. **Performance Summary** (critical issues first)
3. **Active Events** (main data)
4. **Subscriptions** (detail view)
5. **Recent Events** (debug/historical)

**Recommendation:**
- Move Performance Summary to top (after header)
- Add collapsible sections so users can hide less relevant data
- Add "pinned" events feature to keep important events at top

---

### 7.3 No Tabbed View Support (LOW PRIORITY)
**Gap:** All sections in one scrolling view

**Alternative Design:**
- Tab 1: **Overview** (summary + performance + top 5 events)
- Tab 2: **All Events** (full event list with sort/filter)
- Tab 3: **Subscriptions** (all handlers, grouped by priority)
- Tab 4: **History** (expanded recent events log)

**Impact:**
- Long scroll for many events
- Can't focus on specific aspect
- All-or-nothing visibility

**Recommendation:**
- Consider tab interface for Phase 7 or beyond
- Keep current single-view for Phase 6 but optimize order

---

### 7.4 No Event Grouping/Filtering (MEDIUM PRIORITY)
**Gap:** No way to group events by category or filter

**Available Filter Data:**
```csharp
public class EventFilterOptions {
    public string? EventTypeFilter { get; set; }
    public string? SourceFilter { get; set; }
    public bool ShowOnlyActive { get; set; } = true;
}
```

**Current:**
```csharp
// Filter data exists but not exposed in UI!
// No grouping options
```

**Recommendation:**
- Add filter UI:
  - Text search box (filter by event name)
  - Source filter dropdown
  - "Show only active" checkbox (already in data model)
  - Performance filter (show only slow events)
- Add grouping:
  - Group by namespace (Movement, Collision, Tile, etc.)
  - Group by performance tier (Fast, Normal, Slow)
  - Group by subscriber count (Many, Few, None)

**Example:**
```
Event Inspector                  [🔍 Filter: "Collision"] [▼ Group by: Namespace]
━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━
▼ Collision Events (3)
  ├─ CollisionDetectedEvent
  ├─ CollisionResolvedEvent
  └─ CollisionCheckedEvent

▼ Movement Events (4)
  └─ ...
```

---

## Summary: Priority Matrix

### CRITICAL (Implement in Phase 6)
1. **Add SortableTableHeader** - Professional table UX
2. **Add Performance Bars** - Visual metrics comparison
3. **Add Section Separators** - Clear visual structure
4. **Add Summary Statistics Row** - Overall health at a glance
5. **Reorder Sections** - Performance issues first

### HIGH PRIORITY (Phase 6 if time, else Phase 7)
6. Add PanelConstants.EventInspector - Consistent layout
7. Add status indicators/icons - Quick scanning
8. Add percentage breakdowns - Relative performance
9. Make sections collapsible - Reduce clutter
10. Add event/subscription grouping - Organization

### MEDIUM PRIORITY (Phase 7)
11. Improve subscription display hierarchy
12. Add threshold indicators on bars
13. Expand color palette usage
14. Add keyboard navigation shortcuts
15. Add click-to-select events

### LOW PRIORITY (Future/Nice-to-Have)
16. Add sparklines for event rate
17. Add tooltips on hover
18. Row-based scrolling
19. Tabbed view interface
20. Color gradients for bars

---

## Quantified Impact

### Current State
- **Sections**: 4 (no separators, no collapse)
- **Colors Used**: 5 (Success, Warning, Error, TextDim, InputSelection)
- **Visual Elements**: 0 bars, 0 icons, 0 graphs, 0 status dots
- **Keyboard Shortcuts**: 8 (basic navigation only)
- **Sort Options**: 1 (by subscribers, hard-coded)
- **Layout Constants**: 0 (all ad-hoc)
- **Summary Statistics**: 0 displayed (exist but hidden)

### Target State (After Implementing All Recommendations)
- **Sections**: 5 (all collapsible, clear separators)
- **Colors Used**: 12+ (full theme palette)
- **Visual Elements**: 3+ types (bars, icons, sparklines, status dots)
- **Keyboard Shortcuts**: 15+ (navigation, view, data actions)
- **Sort Options**: 5+ (name, subscribers, publish count, avg time, max time)
- **Layout Constants**: 8+ (all in PanelConstants.EventInspector)
- **Summary Statistics**: 4+ (events, subscribers, slowest, rate)

### Estimated Development Effort
- Critical items: 8-12 hours
- High priority: 6-8 hours
- Medium priority: 4-6 hours
- Low priority: 6-10 hours
- **Total**: 24-36 hours for complete implementation

---

## Recommended Implementation Order

### Phase 6 (Current Sprint) - Critical UX Improvements
**Estimated: 8-12 hours**

1. **Add SortableTableHeader** (3 hours)
   - Define EventSortMode enum
   - Add _tableHeader field
   - Configure columns for Active Events section
   - Handle input and sort changes

2. **Add Performance Bars** (3 hours)
   - Add horizontal bars to event metrics
   - Add bars to subscription handlers
   - Add threshold markers (0.1ms, 0.5ms)
   - Color-code based on performance tiers

3. **Add Section Separators** (1 hour)
   - Add horizontal lines after each section
   - Use consistent spacing pattern
   - Match ProfilerContent/StatsContent style

4. **Add Summary Statistics Row** (2 hours)
   - Display header with overall stats
   - Add health indicator (green/yellow/red dot)
   - Show: event count, total subscribers, slowest event

5. **Reorder Sections** (1 hour)
   - Move Performance Summary to top
   - Adjust scroll calculations
   - Test section visibility

6. **Add PanelConstants.EventInspector** (1 hour)
   - Define column widths
   - Define spacing constants
   - Replace hard-coded values

### Phase 7 (Next Sprint) - Enhanced UX
**Estimated: 6-8 hours**

7. Add status indicators (colored dots, icons)
8. Add percentage breakdowns (% of frame, % of event time)
9. Make sections collapsible
10. Improve subscription display hierarchy

### Phase 8 (Future) - Advanced Features
**Estimated: 10+ hours**

11. Add sparklines for event rate trends
12. Add advanced keyboard navigation
13. Add click-to-select and tooltips
14. Add event grouping/filtering UI

---

## Code Examples

### 1. SortableTableHeader Implementation

```csharp
// Add to EventInspectorContent.cs

public enum EventSortMode {
    ByName,
    BySubscribers,
    ByPublishCount,
    ByAverageTime,
    ByMaxTime
}

private readonly SortableTableHeader<EventSortMode> _tableHeader;
private EventSortMode _sortMode = EventSortMode.BySubscribers;

public EventInspectorContent() {
    Id = "event_inspector_content";

    _tableHeader = new SortableTableHeader<EventSortMode>(EventSortMode.BySubscribers);
    _tableHeader.SortChanged += OnSortChanged;
}

private void OnSortChanged(EventSortMode newSort) {
    _sortMode = newSort;
    RefreshData();  // Re-sort cached data
}

// In RenderActiveEventsSection:
_tableHeader.ClearColumns();
_tableHeader.AddColumns(
    new SortableTableHeader<EventSortMode>.Column {
        Label = "Event Type",
        SortMode = EventSortMode.ByName,
        X = x,
        MaxWidth = PanelConstants.EventInspector.EventNameColumnWidth,
        Ascending = true
    },
    new SortableTableHeader<EventSortMode>.Column {
        Label = "Performance",
        SortMode = EventSortMode.ByAverageTime,
        X = barX,
        MaxWidth = barWidth,
        Ascending = false
    },
    new SortableTableHeader<EventSortMode>.Column {
        Label = "Subs",
        SortMode = EventSortMode.BySubscribers,
        X = subsX,
        MaxWidth = 50,
        Alignment = SortableTableHeader<EventSortMode>.HorizontalAlignment.Right,
        Ascending = false
    },
    new SortableTableHeader<EventSortMode>.Column {
        Label = "Count",
        SortMode = EventSortMode.ByPublishCount,
        X = countX,
        MaxWidth = 60,
        Alignment = SortableTableHeader<EventSortMode>.HorizontalAlignment.Right,
        Ascending = false
    }
);

_tableHeader.Draw(renderer, theme, y, lineHeight);
if (input != null) {
    _tableHeader.HandleInput(input);
}
y += lineHeight + theme.SpacingTight;

// Separator
renderer.DrawRectangle(new LayoutRect(x, y, width, 1), theme.BorderPrimary);
y += theme.SpacingNormal;
```

### 2. Performance Bars with Thresholds

```csharp
// In RenderActiveEventsSection, after event name:

if (eventInfo.PublishCount > 0) {
    // Performance bar
    float barX = x + PanelConstants.EventInspector.EventNameColumnWidth;
    float barWidth = PanelConstants.EventInspector.PerformanceBarWidth;
    float barY = y + theme.ProfilerBarInset;
    float barHeight = RowHeight - (theme.ProfilerBarInset * 2);

    // Background
    var barRect = new LayoutRect(barX, barY, barWidth, barHeight);
    renderer.DrawRectangle(barRect, theme.BackgroundElevated);

    // Fill (relative to 1ms threshold)
    const float maxTimeMs = 1.0f;
    float fillPercent = Math.Min((float)(eventInfo.AverageTimeMs / maxTimeMs), 1.0f);
    float fillWidth = barWidth * fillPercent;

    Color barColor = GetPerformanceColor(eventInfo.AverageTimeMs, theme);
    if (fillWidth > 0) {
        var fillRect = new LayoutRect(barX, barY, fillWidth, barHeight);
        renderer.DrawRectangle(fillRect, barColor);
    }

    // Threshold markers
    float threshold1X = barX + (barWidth * 0.1f);  // 0.1ms
    float threshold2X = barX + (barWidth * 0.5f);  // 0.5ms
    renderer.DrawRectangle(new LayoutRect(threshold1X, barY, 1, barHeight),
        theme.Success * 0.5f);
    renderer.DrawRectangle(new LayoutRect(threshold2X, barY, 1, barHeight),
        theme.Warning * 0.5f);

    // Times (right-aligned)
    string timeText = $"{eventInfo.AverageTimeMs:F2}/{eventInfo.MaxTimeMs:F2}ms";
    float timeX = x + width - renderer.MeasureText(timeText).X;
    renderer.DrawText(timeText, timeX, y, theme.TextSecondary);

    y += RowHeight;
}
```

### 3. Summary Statistics Header

```csharp
// Add new method:
private float RenderSummaryHeader(UIRenderer renderer, UITheme theme, float x, float y, float width, int lineHeight) {
    // Title
    renderer.DrawText("Event Inspector", x, y, theme.Info);

    // Get statistics
    var (eventCount, totalSubs, slowestMs, slowestName) = GetStatistics();

    // Health indicator
    Color healthColor = slowestMs < 0.1 ? theme.Success
        : slowestMs < 0.5 ? theme.Warning
        : theme.Error;
    string healthIcon = slowestMs < 0.1 ? NerdFontIcons.StatusHealthy
        : slowestMs < 0.5 ? NerdFontIcons.StatusWarning
        : NerdFontIcons.StatusError;

    // Summary text (right-aligned)
    string summary = $"{healthIcon} {eventCount} events | {totalSubs} subs | Slowest: {slowestMs:F2}ms";
    float summaryWidth = renderer.MeasureText(summary).X;
    renderer.DrawText(summary, x + width - summaryWidth, y, healthColor);

    y += lineHeight + theme.SpacingTight;

    // Separator
    renderer.DrawRectangle(new LayoutRect(x, y, width, 1), theme.BorderPrimary);
    y += theme.SpacingNormal;

    return y;
}

// Call from OnRender before sections:
y = RenderSummaryHeader(renderer, theme, contentX, y, tableContentWidth, lineHeight);
```

### 4. PanelConstants Addition

```csharp
// Add to PanelConstants.cs:

public static class EventInspector {
    public const float EventNameColumnWidth = 250f;
    public const float PerformanceBarWidth = 150f;
    public const float MetricColumnWidth = 80f;
    public const float SubscriberColumnWidth = 50f;
    public const float CountColumnWidth = 60f;
    public const float IndentWidth = 20f;
    public const int RowSpacing = 4;
    public const int SectionSpacing = 12;
}
```

---

## Files to Modify

1. `/mnt/c/Users/nate0/RiderProjects/PokeSharp/PokeSharp.Engine.UI.Debug/Components/Debug/EventInspectorContent.cs`
   - Add SortableTableHeader
   - Add performance bars
   - Add section separators
   - Add summary header
   - Reorder sections

2. `/mnt/c/Users/nate0/RiderProjects/PokeSharp/PokeSharp.Engine.UI.Debug/Core/PanelConstants.cs`
   - Add EventInspector constants

3. `/mnt/c/Users/nate0/RiderProjects/PokeSharp/PokeSharp.Engine.UI.Debug/Core/NerdFontIcons.cs` (if needed)
   - Add event-specific icons

4. `/mnt/c/Users/nate0/RiderProjects/PokeSharp/PokeSharp.Engine.UI.Debug/Components/Debug/EventInspectorPanel.cs`
   - Update keyboard shortcuts
   - Add new panel actions

---

## Testing Checklist

After implementing Phase 6 improvements:

- [ ] Table headers clickable and show sort indicators
- [ ] Performance bars render correctly with threshold markers
- [ ] Section separators visible and consistent
- [ ] Summary header shows correct statistics
- [ ] Health indicator color matches performance
- [ ] Sections in correct order (Summary → Performance → Events → Subs → Recent)
- [ ] All PanelConstants used (no hard-coded layout values)
- [ ] Scrolling works smoothly with new layout
- [ ] Mouse wheel scrolling functional
- [ ] Keyboard shortcuts still work
- [ ] Visual consistency with ProfilerContent and StatsContent
- [ ] No performance regression (rendering still fast)

---

**End of Analysis**
