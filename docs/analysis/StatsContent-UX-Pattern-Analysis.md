# StatsContent.cs - Comprehensive UX Pattern Analysis

**File**: `/PokeSharp.Engine.UI.Debug/Components/Debug/StatsContent.cs`
**Date**: 2025-12-03
**Purpose**: Deep analysis of layout patterns, visual design, and rendering techniques

---

## 1. Layout Structure & Space Division

### 1.1 Vertical Layout Flow

The component uses a **sequential vertical layout** with explicit Y-position tracking:

```csharp
// Starting Y position with scroll offset applied
float baseY = Rect.Y + linePadding;
float y = baseY - _scrollbar.ScrollOffset;

// Each section advances Y position:
y += lineHeight + SectionSpacing;       // After header
y += rowHeight;                         // After each data row
y += rowHeight + SectionSpacing;        // After section groups
```

**Key Measurements**:
- `linePadding = DebugPanelBase.StandardLinePadding = 5px` - Content inset from edges
- `lineHeight = renderer.GetLineHeight()` - Dynamic from font (typically 16-20px)
- `rowHeight = lineHeight + PanelConstants.Stats.RowSpacing` - Row spacing = 4px
- `SectionSpacing = ThemeManager.Current.SectionSpacing` - Between major sections

### 1.2 Horizontal Layout System

**Three-column layout architecture**:

```csharp
float labelWidth = PanelConstants.Stats.LabelWidth;        // 100px - Label column
float valueX = contentX + labelWidth;                       // Value column start
float barX = contentX + labelWidth + PanelConstants.Stats.ValueOffset;  // +80px - Bar column start
float barWidth = PanelConstants.Stats.BarWidth;            // 150px - Bar width
```

**Visual Structure**:
```
[Padding 5px][Label 100px][Value 80px offset][Bar 150px][Right-aligned text][Padding 5px]
│           │             │                  │           │                  │
contentX    valueX        barX               barX+150    contentX+width     Rect.Right
```

### 1.3 Content Width Calculation

```csharp
// Accounts for scrollbar when needed
bool needsScrollbar = _totalContentHeight > visibleHeight;
float contentWidth = Rect.Width - (linePadding * 2) - (needsScrollbar ? scrollbarWidth : 0);
float contentX = Rect.X + linePadding;
```

**Progressive width calculation**:
- Full width: `Rect.Width`
- Minus side padding: `- (linePadding * 2)` = `- 10px`
- Minus scrollbar when visible: `- scrollbarWidth` (theme-dependent, typically 10px)

---

## 2. Visual Hierarchy & Information Priority

### 2.1 Section Organization

The content is organized into **three hierarchical levels**:

#### Level 1: Section Headers (Highest Priority)
```csharp
renderer.DrawText("Performance Stats", contentX, y, theme.Info);  // Blue/cyan color
renderer.DrawText("ECS World", contentX, y, theme.Info);
```
- **Color**: `theme.Info` (Blue #61afef in OneDark)
- **Spacing**: `lineHeight + SectionSpacing` after header
- **Visual Weight**: Brightest blue draws attention

#### Level 2: Data Rows (Primary Content)
```csharp
renderer.DrawText("FPS:", contentX, y, theme.TextSecondary);      // Label - dimmed
renderer.DrawText($"{_cachedStats.Fps:F1}", valueX, y, fpsColor); // Value - color-coded
```
- **Labels**: `theme.TextSecondary` (dimmed, less prominent)
- **Values**: Color-coded by health status (Success/Warning/Error)
- **Spacing**: `rowHeight` between rows

#### Level 3: Separators (Visual Boundaries)
```csharp
renderer.DrawRectangle(new LayoutRect(contentX, y, contentWidth, 1), theme.BorderPrimary);
```
- **Height**: 1px horizontal lines
- **Color**: `theme.BorderPrimary` (subtle, dark)
- **Spacing**: `SectionSpacing` before and after

### 2.2 Color-Coded Priority System

**Performance Thresholds**:

| Metric | Good (Green) | Fair (Yellow) | Warning (Orange) | Critical (Red) |
|--------|-------------|---------------|------------------|----------------|
| **FPS** | ≥60 | 55-60 | 30-55 | <30 |
| **Frame Time** | ≤16.67ms | 16.67-25ms | 25-33.33ms | >33.33ms |
| **Memory** | <256 MB | 256-512 MB | 512-768 MB | >768 MB |
| **GC Gen0** | <10/sec | 10-20/sec | >20/sec | - |
| **GC Gen2** | 0/sec | - | - | >0/sec |

**Implementation**:
```csharp
private static Color GetFpsColor(float fps, UITheme theme)
{
    return fps >= FpsGood ? theme.Success      // ≥55 FPS
        : fps >= FpsFair ? theme.Warning       // 30-55 FPS
        : theme.Error;                         // <30 FPS
}
```

---

## 3. Statistics Display Patterns

### 3.1 Basic Metric Row Pattern

**Pattern**: Label (left) + Value (fixed offset) + Context (right-aligned)

```csharp
// FPS Row Example
renderer.DrawText("FPS:", contentX, y, theme.TextSecondary);           // Label
renderer.DrawText($"{_cachedStats.Fps:F1}", valueX, y, fpsColor);     // Value
renderer.DrawRectangle(new LayoutRect(valueX + 50, y + 4, 8, 8), fpsColor);  // Status indicator
string fpsRating = GetFpsRating(_cachedStats.Fps);                    // "Excellent"/"Good"/"Fair"/"Poor"
float ratingWidth = renderer.MeasureText(fpsRating).X;
renderer.DrawText(fpsRating, contentX + contentWidth - ratingWidth, y, fpsColor);  // Right-aligned
```

**Visual Layout**:
```
FPS:        59.8 ■ [8x8 square]                    Good
│           │    │                                 │
contentX    valueX +50px offset                   Right edge
```

### 3.2 Metric + Progress Bar Pattern

**Pattern**: Label + Value + Visual Progress Bar

```csharp
renderer.DrawText("Frame Time:", contentX, y, theme.TextSecondary);
renderer.DrawText($"{_cachedStats.FrameTimeMs:F2}ms", valueX, y, frameTimeColor);
DrawProgressBar(
    renderer,
    barX,                              // X position: contentX + labelWidth + valueOffset
    y + 2,                             // Y position: slightly below text baseline
    barWidth,                          // 150px width
    lineHeight - 4,                    // Height: lineHeight with 2px margin top/bottom
    _cachedStats.FrameTimeMs / FrameTimeMax,  // Fill percentage (0.0-1.0)
    frameTimeColor,
    theme
);
```

**Progress Bar Structure**:
```
Frame Time: 15.23ms [████████████████          ] 50% of 16.67ms
│           │       │                          │ │
contentX    valueX  barX (80px offset)         │ Right-aligned percentage
                    └─ 150px bar width ────────┘
```

**Reference Line Indicator**:
```csharp
// Vertical line at 50% (16.67ms / 33.33ms max = 0.5)
float budgetX = barX + (barWidth * 0.5f);
renderer.DrawRectangle(new LayoutRect(budgetX, y, 2, lineHeight), theme.Warning);
```

### 3.3 Multi-Column Data Pattern

**GC Collections Row**:
```csharp
renderer.DrawText("GC:", contentX, y, theme.TextSecondary);
float gcX = valueX;
renderer.DrawText($"G0: {_cachedStats.Gen0Collections}", gcX, y, gen0Color);
gcX += GcColumnWidth;  // 60px from ThemeManager
renderer.DrawText($"G1: {_cachedStats.Gen1Collections}", gcX, y, gen1Color);
gcX += GcColumnWidth;
renderer.DrawText($"G2: {_cachedStats.Gen2Collections}", gcX, y, gen2Color);

// Right-aligned delta rate
string deltaText = $"+{_gen0Delta}/{_gen1Delta}/{_gen2Delta}/s";
float deltaWidth = renderer.MeasureText(deltaText).X;
renderer.DrawText(deltaText, contentX + contentWidth - deltaWidth, y, deltaColor);
```

**Visual Layout**:
```
GC:         G0: 1234      G1: 45        G2: 2           +12/2/0/s
│           │             │             │               │
contentX    valueX        +60px         +60px           Right edge
```

### 3.4 Range Display Pattern

**Frame Time Range**:
```csharp
renderer.DrawText("Range:", contentX, y, theme.TextSecondary);
renderer.DrawText(
    $"{_cachedStats.MinFrameTimeMs:F1} - {_cachedStats.MaxFrameTimeMs:F1}ms",
    valueX, y, theme.TextPrimary
);

// Budget percentage calculation
float budgetPercent = _cachedStats.FrameTimeMs / FrameTimeGood * 100f;
string budgetText = $"{budgetPercent:F0}% of {FrameTimeGood:F2}ms";
Color budgetColor = budgetPercent <= 80 ? theme.Success
    : budgetPercent <= 100 ? theme.Warning
    : theme.Error;
float budgetWidth = renderer.MeasureText(budgetText).X;
renderer.DrawText(budgetText, contentX + contentWidth - budgetWidth, y, budgetColor);
```

---

## 4. Graph/Chart Rendering - Sparkline Component

### 4.1 Sparkline Integration

**Creation**:
```csharp
_frameTimeSparkline = Sparkline.ForFrameTime(id + "_sparkline");
// Configured with:
// - Reference line at 16.67ms (60fps target)
// - Warning threshold at 16.67ms
// - Error threshold at 25ms
// - Auto-scaling (no fixed min/max)
```

**Rendering**:
```csharp
renderer.DrawText("History:", contentX, y, theme.TextSecondary);
_frameTimeSparkline.Draw(
    renderer,
    valueX,                                              // X position
    y,                                                   // Y position
    contentWidth - labelWidth - SectionSpacing,          // Width (fill remaining space)
    lineHeight                                           // Height (single line)
);
```

### 4.2 Sparkline Visual Design

**Bar Chart Layout**:
```csharp
float barWidth = (rect.Width - (_barGap * (_data.Length - 1))) / _data.Length;
// For 60 data points in 150px width with 1px gap:
// barWidth = (150 - (1 * 59)) / 60 = 1.52px per bar
```

**Color Coding**:
```csharp
Color color = val >= _errorThreshold ? errColor      // Red: >25ms
    : val >= _warningThreshold ? warnColor           // Yellow: >16.67ms
    : goodColor;                                     // Green: ≤16.67ms
```

**Reference Line**:
```csharp
if (_referenceLine.HasValue)  // 16.67ms budget line
{
    float refY = rect.Y + rect.Height - ((_referenceLine.Value - minVal) / range * rect.Height);
    renderer.DrawRectangle(new LayoutRect(rect.X, refY, rect.Width, 1), theme.Warning * 0.5f);
}
```

**Auto-Scaling**:
```csharp
float minVal = _fixedMin ?? (dataMin > 0 ? dataMin * 0.9f : 0);     // 10% padding below
float maxVal = _fixedMax ?? Math.Max(dataMax * 1.1f, minVal + 1);   // 10% padding above
```

### 4.3 Sparkline Visual Layout

```
History: [▁▁▂▂▃▃▄▄▅▅▆▆▇▇███████▇▇▆▆▅▅▄▄▃▃▂▂▁▁]
│        │                                      │
contentX valueX                                 contentX + contentWidth - labelWidth
         └─────── Fills remaining width ───────┘

         Reference line (16.67ms) shown as horizontal line
         Bars scale from 0-maxVal with 10% padding
         60 bars with 1px gap between each
```

---

## 5. Progress Bar Rendering Details

### 5.1 DrawProgressBar Method

```csharp
private void DrawProgressBar(
    UIRenderer renderer,
    float x,
    float y,
    float width,          // 150px from PanelConstants.Stats.BarWidth
    float height,         // lineHeight - 4 (typically 12-16px)
    float percent,        // 0.0 to 1.0 (clamped)
    Color fillColor,      // Success/Warning/Error based on metric
    UITheme theme
)
{
    // 1. Draw background (track)
    renderer.DrawRectangle(new LayoutRect(x, y, width, height), theme.BackgroundElevated);

    // 2. Draw filled portion
    float fillWidth = width * Math.Clamp(percent, 0, 1);
    if (fillWidth > 0)
    {
        renderer.DrawRectangle(new LayoutRect(x, y, fillWidth, height), fillColor);
    }

    // 3. Draw border (outline)
    renderer.DrawRectangleOutline(new LayoutRect(x, y, width, height), theme.BorderPrimary);
}
```

### 5.2 Visual Anatomy

```
┌────────────────────────────────────────────┐  ← Border (theme.BorderPrimary)
│████████████████████                        │  ← Filled portion (fillColor)
│                                            │
└────────────────────────────────────────────┘
├──── fillWidth (width * percent) ───┤
├──────────────── width (150px) ─────────────┤

Background: theme.BackgroundElevated (darker shade)
Fill: Color-coded (Success/Warning/Error)
Height: lineHeight - 4 (2px margin top/bottom from baseline)
```

### 5.3 Progress Bar Examples

**Frame Time Bar** (16.67ms / 33.33ms = 50%):
```
[████████████████          ] ← 50% filled, green color
```

**Memory Bar** (384 MB / 512 MB warning = 75%):
```
[██████████████████████    ] ← 75% filled, yellow color
```

---

## 6. Constants & Theme Values

### 6.1 PanelConstants.Stats

```csharp
public static class Stats
{
    public const float LabelWidth = 100f;      // Left column for labels
    public const float BarWidth = 150f;        // Width of progress bars
    public const float ValueOffset = 80f;      // Gap between label and value text
    public const float RowSpacing = 4f;        // Extra spacing per row
}
```

**Usage Context**:
- `LabelWidth` - Ensures all labels align vertically (e.g., "FPS:", "Memory:", "Entities:")
- `BarWidth` - Consistent width for all progress bars
- `ValueOffset` - Space for numeric values before bar (e.g., "59.8" before FPS bar)
- `RowSpacing` - Visual breathing room between rows

### 6.2 DebugPanelBase.StandardLinePadding

```csharp
public const int StandardLinePadding = 5;
```

**Purpose**: Unified padding value used across all debug components:
- `StatsContent` - Content inset from panel edges
- `TextBuffer` - Text inset in console/logs
- `StatusBar` - Internal padding

**Visual Consistency**:
```
┌─────────────────────────────────────┐
│ [5px padding]                       │  ← StandardLinePadding
│     Performance Stats               │
│     FPS: 59.8                       │
│                                     │
│                         [5px padding]│
└─────────────────────────────────────┘
```

### 6.3 ThemeManager Dynamic Values

```csharp
// Accessed via properties for theme switching support
private int SectionSpacing => ThemeManager.Current.SectionSpacing;
private int GcColumnWidth => ThemeManager.Current.TableColumnWidth;
private int ScrollSpeed => ThemeManager.Current.ScrollSpeed;
```

**OneDark Theme Values** (from UITheme.cs):
```csharp
SectionSpacing = 8        // Space between major sections
TableColumnWidth = 60     // Multi-column data spacing (GC row)
ScrollSpeed = 12          // Mouse wheel scroll pixels per tick
ScrollbarWidth = 10       // Scrollbar track width
BorderWidth = 1           // Border thickness
```

---

## 7. Text Alignment Patterns

### 7.1 Left-Aligned Text

**Standard Pattern**:
```csharp
renderer.DrawText(text, contentX, y, color);
// Simply use contentX as X coordinate
```

**Examples**:
- Labels: "FPS:", "Memory:", "Systems:"
- Section headers: "Performance Stats", "ECS World"

### 7.2 Right-Aligned Text

**Pattern**: Measure text width, subtract from right edge:
```csharp
string text = "Excellent";
float textWidth = renderer.MeasureText(text).X;
float x = contentX + contentWidth - textWidth;
renderer.DrawText(text, x, y, color);
```

**Examples**:
- FPS rating: "Excellent", "Good", "Fair", "Poor"
- Frame counter: "Frame: 12,345"
- Budget percentage: "92% of 16.67ms"
- Delta rates: "+12/2/0/s"

### 7.3 Fixed-Offset Text

**Pattern**: Position relative to label column:
```csharp
float valueX = contentX + labelWidth;  // 100px offset
renderer.DrawText($"{value:F1}", valueX, y, color);
```

**Examples**:
- Numeric values: "59.8", "15.23ms", "384.5 MB"
- Entity counts: "1,234"
- System counts: "42"

---

## 8. Scrolling Implementation

### 8.1 Scroll Offset Application

```csharp
float baseY = Rect.Y + linePadding;
float y = baseY - _scrollbar.ScrollOffset;  // Apply negative offset to shift content up

// As content is drawn, Y advances:
y += lineHeight;
y += rowHeight;

// At end, calculate total height:
_totalContentHeight = y - baseY + _scrollbar.ScrollOffset + linePadding;
```

### 8.2 Scrollbar Positioning

```csharp
var scrollbarRect = new LayoutRect(
    Rect.Right - scrollbarWidth,              // Right edge minus scrollbar width
    Rect.Y + linePadding,                     // Top edge with padding
    scrollbarWidth,                           // 10px width (theme-dependent)
    Rect.Height - (linePadding * 2)          // Full height minus top/bottom padding
);
```

**Visual Layout**:
```
┌──────────────────────────────────┬──┐
│ [5px]                            │▓▓│ ← Scrollbar (10px)
│      Content Area                │▓▓│
│                                  │▓▓│
│                                  │░░│
│                      [5px]       │░░│
└──────────────────────────────────┴──┘
                                   └─┘
                                  10px width
```

### 8.3 Clipping Rectangle

```csharp
var clipRect = new LayoutRect(
    Rect.X,
    Rect.Y,
    Rect.Width - (needsScrollbar ? scrollbarWidth : 0),  // Exclude scrollbar from clip
    Rect.Height
);
renderer.PushClip(clipRect);
// ... draw content ...
renderer.PopClip();
```

**Purpose**: Prevents content from drawing over scrollbar or outside panel bounds.

### 8.4 Input Handling

**Mouse Wheel**:
```csharp
_scrollbar.HandleMouseWheel(context.Input, _totalContentHeight, visibleHeight);
```

**Keyboard**:
```csharp
if (context.Input.IsKeyPressedWithRepeat(Keys.PageUp))
    _scrollbar.ScrollOffset = Math.Max(0, _scrollbar.ScrollOffset - (visibleHeight * 0.8f));
else if (context.Input.IsKeyPressedWithRepeat(Keys.PageDown))
    _scrollbar.ScrollOffset = Math.Min(maxScroll, _scrollbar.ScrollOffset + (visibleHeight * 0.8f));
else if (context.Input.IsKeyPressed(Keys.Home))
    _scrollbar.ScrollToTop();
else if (context.Input.IsKeyPressed(Keys.End))
    _scrollbar.ScrollOffset = maxScroll;
```

**PageUp/PageDown**: Scrolls 80% of visible height (allows overlap for context)
**Home/End**: Jump to top/bottom

---

## 9. Empty State Handling

### 9.1 No Provider State

```csharp
if (!HasProvider)
{
    y = EmptyStateComponent.DrawLeftAligned(
        renderer,
        theme,
        contentX,
        y,
        "Stats provider not configured.",    // Title
        "Waiting for stats data..."         // Description
    );
    _totalContentHeight = y - baseY + _scrollbar.ScrollOffset + linePadding;
    return;  // Early return, skip rendering stats
}
```

**Visual Result**:
```
┌─────────────────────────────────────┐
│ [5px]                               │
│     Stats provider not configured.  │  ← theme.TextDim
│     Waiting for stats data...       │  ← theme.TextDim
│                                     │
└─────────────────────────────────────┘
```

### 9.2 EmptyStateComponent Pattern

```csharp
public static float DrawLeftAligned(
    UIRenderer renderer,
    UITheme theme,
    float x,
    float y,
    string title,
    string? description = null
)
{
    renderer.DrawText(title, x, y, theme.TextDim);
    y += lineHeight;
    if (description != null)
    {
        renderer.DrawText(description, x, y, theme.TextDim);
        y += lineHeight;
    }
    return y;  // Returns final Y position for layout chain
}
```

---

## 10. Performance & Optimization Patterns

### 10.1 Time-Based Refresh

```csharp
private double _refreshIntervalSeconds = 0.033;  // ~30fps updates by default

// In OnRender:
double currentTime = context.Input.GameTime.TotalGameTime.TotalSeconds;
if (currentTime - _lastUpdateTime >= _refreshIntervalSeconds)
{
    _lastUpdateTime = currentTime;
    RefreshStats();
    _gcDeltaTimer += _refreshIntervalSeconds;
}
```

**Benefits**:
- Frame-rate independent (doesn't refresh more on high-FPS systems)
- Configurable interval (16ms-1000ms range)
- Reduces stat provider calls from 60fps → 30fps by default

### 10.2 Cached Statistics

```csharp
private StatsData _cachedStats = new();

private void RefreshStats()
{
    if (_statsProvider != null)
    {
        _cachedStats = _statsProvider();  // Single call per refresh
    }
    _frameTimeSparkline.AddValue(_cachedStats.FrameTimeMs);
    // Calculate GC deltas...
}
```

**Pattern**: Fetch all stats once per refresh interval, cache results, render from cache.

### 10.3 Conditional Scrollbar

```csharp
bool needsScrollbar = _totalContentHeight > visibleHeight;
float contentWidth = Rect.Width - (linePadding * 2) - (needsScrollbar ? scrollbarWidth : 0);
```

**Optimization**: Only render scrollbar when content exceeds visible area, reclaim space otherwise.

---

## 11. Typography & Text Formatting

### 11.1 Number Formatting

| Format | Example | Purpose |
|--------|---------|---------|
| `:F1` | `59.8` | 1 decimal place (FPS) |
| `:F2` | `15.23` | 2 decimal places (milliseconds) |
| `:N0` | `1,234` | Comma-separated integers (frame number, entities) |
| `:F0` | `92` | Integer with no decimals (percentages) |

**Examples**:
```csharp
$"{_cachedStats.Fps:F1}"                    // "59.8"
$"{_cachedStats.FrameTimeMs:F2}ms"         // "15.23ms"
$"Frame: {_cachedStats.FrameNumber:N0}"    // "Frame: 12,345"
$"{budgetPercent:F0}% of {FrameTimeGood:F2}ms"  // "92% of 16.67ms"
```

### 11.2 Text Truncation

```csharp
string systemName = _cachedStats.SlowestSystemName;
systemName = systemName.Replace("System", "");  // Remove common suffix
if (systemName.Length > MaxSystemNameLength)    // 18 characters
{
    systemName = systemName.Substring(0, TruncatedNameLength) + "...";  // 15 + "..."
}
```

**Example**: `"CollisionDetectionSystem"` → `"CollisionDetec..."`

---

## 12. Health Status Indicators

### 12.1 Overall Health Calculation

```csharp
public (string indicator, Color color, bool isHealthy) GetOverallHealth(UITheme theme)
{
    bool isHealthy = _cachedStats.Fps >= FpsGood         // ≥55 FPS
        && _cachedStats.MemoryMB < MemoryWarning         // <512 MB
        && _gen2Delta < 1;                               // <1 Gen2 GC/sec

    bool isWarning = _cachedStats.Fps >= FpsFair         // ≥30 FPS
        && _cachedStats.MemoryMB < MemoryMax;           // <768 MB

    if (isHealthy)
        return (NerdFontIcons.StatusHealthy, theme.Success, true);
    if (isWarning)
        return (NerdFontIcons.StatusWarning, theme.Warning, false);
    return (NerdFontIcons.StatusError, theme.Error, false);
}
```

### 12.2 Health Icons

```csharp
// From NerdFontIcons class
StatusHealthy = "✓"    // Green checkmark
StatusWarning = "⚠"    // Yellow warning triangle
StatusError = "✗"      // Red X
```

### 12.3 Visual Indicators

**Small Status Square** (FPS row):
```csharp
renderer.DrawRectangle(new LayoutRect(valueX + 50, y + 4, 8, 8), fpsColor);
```
- **Size**: 8x8 pixels
- **Position**: 50px right of value, 4px down from baseline
- **Color**: Matches FPS color (Success/Warning/Error)

---

## 13. Section Breakdown

### 13.1 Performance Stats Section

```
Performance Stats                                    Frame: 12,345
─────────────────────────────────────────────────────────────────
FPS:        59.8 ■                                   Excellent
Frame Time: 15.23ms [████████████████          ]
Range:      14.2 - 18.7ms                            92% of 16.67ms
History:    [▁▁▂▂▃▃▄▄▅▅▆▆▇▇███████▇▇▆▆▅▅▄▄▃▃▂▂▁▁]
─────────────────────────────────────────────────────────────────
Memory:     384.5 MB [██████████████████          ]
GC:         G0: 1234   G1: 45    G2: 2             +12/2/0/s
```

**Rows**:
1. Header with frame counter (right-aligned)
2. Separator (1px line)
3. FPS with rating and status indicator
4. Frame time with progress bar and reference line
5. Frame time range with budget percentage
6. Sparkline history chart
7. Separator
8. Memory with progress bar
9. GC collections (3 columns) with delta rate

### 13.2 ECS World Section

```
─────────────────────────────────────────────────────────────────
ECS World
─────────────────────────────────────────────────────────────────
Entities:   1,234                                    Archetypes: 23
Systems:    42                                       Total: 8.45ms
Slowest:    CollisionDetec...                        2.34ms
Pools:      4 pools                                  1601 active  3500 avail
```

**Rows**:
1. Separator
2. Header
3. Separator
4. Entities count with archetypes (right-aligned)
5. Systems count with total time (right-aligned, color-coded)
6. Slowest system name (truncated) with time (right-aligned, color-coded)
7. Pool statistics (conditional - only if pools exist)

---

## 14. Measurement Summary

### Layout Constants
- **StandardLinePadding**: 5px
- **LabelWidth**: 100px
- **ValueOffset**: 80px
- **BarWidth**: 150px
- **RowSpacing**: 4px
- **SectionSpacing**: 8px (theme-dependent)
- **ScrollbarWidth**: 10px (theme-dependent)
- **BorderWidth**: 1px (theme-dependent)

### Color Scheme (OneDark)
- **Background**: #282c34 (40, 44, 52, 240)
- **BackgroundElevated**: #323842 (50, 56, 66)
- **TextPrimary**: #abb2bf (171, 178, 191)
- **TextSecondary**: #828997 (130, 137, 151)
- **TextDim**: #5c6370 (92, 99, 112)
- **Success**: #98c379 (152, 195, 121)
- **Warning**: #e5c07b (229, 192, 123)
- **Error**: #e06c75 (224, 108, 117)
- **Info**: #61afef (97, 175, 239)
- **BorderPrimary**: #3e4451 (62, 68, 81)

### Typography
- **FontSize**: 16px
- **LineHeight**: 20px (dynamic from font)
- **RowHeight**: lineHeight + 4px = 24px

### Thresholds
- **FPS Excellent**: ≥60
- **FPS Good**: ≥55
- **FPS Fair**: ≥30
- **Frame Time Good**: ≤16.67ms
- **Frame Time Warning**: ≤25ms
- **Frame Time Max**: ≤33.33ms
- **Memory Good**: <256 MB
- **Memory Warning**: <512 MB
- **Memory Max**: <768 MB
- **GC Gen0 Warning**: >10/sec
- **GC Gen1 Warning**: >2/sec
- **System Time Good**: ≤2ms
- **System Time Warning**: ≤5ms
- **Total System Time Good**: ≤10ms
- **Total System Time Warning**: ≤16ms

---

## 15. Key Takeaways for UX Consistency

### Design Principles
1. **Consistent Column Alignment**: All labels at contentX, all values at contentX + 100px
2. **Three-Tier Visual Hierarchy**: Headers (Info blue) > Labels (TextSecondary) > Details (TextDim)
3. **Color-Coded Health**: Green (good) → Yellow (warning) → Red (error)
4. **Progressive Disclosure**: Sections separated by visual dividers, important metrics first
5. **Right-Alignment for Context**: Ratings, percentages, and metadata on right edge
6. **Inline Visualizations**: Progress bars and sparklines at consistent positions
7. **Responsive Scrolling**: Only show scrollbar when needed, keyboard shortcuts supported
8. **Performance-First**: Time-based refresh, cached stats, conditional rendering

### Reusable Patterns
- **Label + Value + Bar**: Standard metric display
- **Label + Value + Right-Aligned Context**: Contextual information pattern
- **Multi-Column Data**: Fixed-width columns for tabular alignment
- **Section Header + Separator**: Visual grouping pattern
- **Empty State**: Consistent "no data" messaging
- **Health Indicators**: Color + icon + text rating system

### Common Measurements
- **Padding**: 5px standard, 8px section spacing
- **Bars**: 150px width, lineHeight - 4px height
- **Columns**: 100px labels, 80px value offset, 60px table columns
- **Colors**: 3-tier semantic (Success/Warning/Error)
- **Spacing**: 4px row spacing, 8px section spacing

---

**End of Analysis**
