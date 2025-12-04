# Event Inspector UX Consistency Analysis Report

## Executive Summary

Comparison of Event Inspector tab implementation against Profiler and Stats tabs reveals **significant UX consistency issues**. While Event Inspector follows similar structural patterns, it has **numerous hardcoded values** and **inconsistent theming** that violate the established design system used by Profiler and Stats tabs.

**Overall Assessment**: ❌ **INCONSISTENT** - Requires refactoring for UX parity

---

## 1. Hardcoded Values Found

### 1.1 EventInspectorContent.cs - CRITICAL VIOLATIONS

#### ❌ Hardcoded Spacing/Sizing Values
| Line | Code | Issue | Should Use |
|------|------|-------|-----------|
| 308 | `renderer.DrawRectangle(new LayoutRect(valueX + 50, y + 4, 8, 8), fpsColor);` | Hardcoded offset `+50` and size `8, 8` | `theme.PaddingMedium`, calculated width |
| 329 | `renderer.DrawRectangle(new LayoutRect(budgetX, y, 2, lineHeight), theme.Warning);` | Hardcoded border width `2` | `theme.BorderWidth` |
| 848 | `renderer.DrawText(timestamp, x, y, theme.TextDim);` followed by `x + 85` | Hardcoded offset `85` | Measured text width |
| 849 | `renderer.DrawText($" {operationIcon} ", x + 105, y, theme.TextSecondary);` | Hardcoded offset `105` | Calculated from previous columns |

#### ❌ Color Multiplication Without Theme Support
| Line | Code | Issue |
|------|------|-------|
| 746 | `theme.Warning * 0.5f` | Hardcoded alpha multiplication (no theme variable) |

**Pattern Violation**: Profiler and Stats use `theme.ProfilerBudgetLineOpacity` instead of magic numbers.

---

### 1.2 EventInspectorContent.cs - Theme Constant Misuse

#### ⚠️ Correct Theme Usage (Good Examples)
- ✅ Line 79: `PanelConstants.EventInspector.ColumnPadding`
- ✅ Line 551: `theme.SpacingTight`
- ✅ Line 656: `theme.InputSelection`

#### ❌ Missing Theme Integration
The Event Inspector uses **PanelConstants** for layout, but **doesn't leverage theme properties** for:
- Bar opacity/alpha values (unlike Profiler's `ProfilerBudgetLineOpacity`)
- Interactive padding (theme has `InteractiveClickPadding`)
- Section spacing inconsistencies

---

## 2. Component Structure Comparison

### 2.1 File Organization Pattern

| Component | Content Class | Panel Class | Builder Class |
|-----------|--------------|-------------|---------------|
| **Profiler** | ProfilerContent.cs ✅ | ProfilerPanel.cs ✅ | ProfilerPanelBuilder.cs ✅ |
| **Stats** | StatsContent.cs ✅ | StatsPanel.cs ✅ | StatsPanelBuilder.cs ✅ |
| **Event Inspector** | EventInspectorContent.cs ✅ | EventInspectorPanel.cs ✅ | EventInspectorPanelBuilder.cs ✅ |

**Result**: ✅ **CONSISTENT** - All three tabs follow same 3-file pattern

---

### 2.2 Class Hierarchy Pattern

```
DebugPanelBase (base class)
├── ProfilerPanel : DebugPanelBase, IProfilerOperations ✅
├── StatsPanel : DebugPanelBase, IStatsOperations ✅
└── EventInspectorPanel : DebugPanelBase, IEventInspectorOperations ✅

UIComponent (base class)
├── ProfilerContent : UIComponent ✅
├── StatsContent : UIComponent ✅
└── EventInspectorContent : UIComponent ✅
```

**Result**: ✅ **CONSISTENT** - All follow same inheritance pattern

---

## 3. UX Pattern Analysis

### 3.1 Empty State Pattern

#### Profiler (ProfilerContent.cs:246-257)
```csharp
EmptyStateComponent.DrawLeftAligned(
    renderer, theme, contentX, y,
    "Profiler provider not configured.",
    "Waiting for system metrics..."
);
```

#### Stats (StatsContent.cs:250-259)
```csharp
EmptyStateComponent.DrawLeftAligned(
    renderer, theme, contentX, y,
    "Stats provider not configured.",
    "Waiting for stats data..."
);
```

#### Event Inspector (EventInspectorContent.cs:376-383)
```csharp
EmptyStateComponent.DrawLeftAligned(
    renderer, theme, contentX, y,
    $"{NerdFontIcons.Warning} No data provider configured.",
    "Waiting for event inspector data...");
```

**Analysis**:
- ❌ **INCONSISTENT** - Event Inspector adds icon to first line
- ✅ Pattern: All use `EmptyStateComponent.DrawLeftAligned`
- ❌ Message format differs (icons vs plain text)

---

### 3.2 Refresh Interval Pattern

| Tab | Method | Pattern |
|-----|--------|---------|
| **Profiler** | `SetRefreshInterval(float intervalSeconds)` | Time-based (seconds) ✅ |
| **Stats** | `SetRefreshInterval(int frameInterval)` + overload | Frame + Time dual support ✅ |
| **Event Inspector** | `SetRefreshInterval(int frameInterval)` | Frame-based only ⚠️ |

**Issue**: Event Inspector lacks seconds-based refresh method that Stats provides.

---

### 3.3 Scrollbar Implementation

#### Profiler (ProfilerContent.cs:356-374)
```csharp
// Pre-calculate scrollbar requirements
bool needsScrollbar = _cachedMetrics.Count > maxBarsVisible;
float tableContentWidth = needsScrollbar
    ? contentWidth - scrollbarWidth - theme.PaddingSmall
    : contentWidth;
```

#### Stats (StatsContent.cs:194-196)
```csharp
bool needsScrollbar = _totalContentHeight > visibleHeight;
float contentWidth = Rect.Width - (linePadding * 2)
    - (needsScrollbar ? scrollbarWidth : 0);
```

#### Event Inspector (EventInspectorContent.cs:402-405)
```csharp
bool needsScrollbar = _totalContentHeight > _visibleHeight;
float tableContentWidth = needsScrollbar
    ? contentWidth - scrollbarWidth - theme.PaddingSmall
    : contentWidth;
```

**Result**: ✅ **CONSISTENT** - All use same scrollbar calculation pattern

---

## 4. Theme Integration Comparison

### 4.1 Color Usage Pattern

#### Profiler - EXCELLENT Theme Integration
```csharp
// Line 454-469: GetBarColor uses theme colors with threshold multipliers
if (ms >= _warningThresholdMs) return theme.Error;
if (ms >= _warningThresholdMs * theme.ProfilerBarWarningThreshold)
    return theme.Warning;
if (ms >= _warningThresholdMs * theme.ProfilerBarMildThreshold)
    return theme.WarningMild;
return theme.Success;
```

#### Event Inspector - INCOMPLETE Theme Integration
```csharp
// Line 951-969: GetPerformanceColor (similar but has issues)
if (timeMs >= WarningThresholdMs)
    return theme.Error;
if (timeMs >= WarningThresholdMs * theme.ProfilerBarWarningThreshold)
    return theme.Warning;
if (timeMs >= WarningThresholdMs * theme.ProfilerBarMildThreshold)
    return theme.WarningMild;
return theme.Success;
```

**Analysis**:
- ✅ Both use same threshold multiplier pattern
- ⚠️ Event Inspector **reuses Profiler theme constants** (ProfilerBarWarningThreshold)
- ❌ Should have dedicated EventInspector theme constants for semantic clarity

---

### 4.2 Layout Constants - MAJOR DIFFERENCE

#### Profiler - Uses Theme Directly
```csharp
// ProfilerContent.cs:19-20
private const float NameColumnWidth = PanelConstants.Profiler.NameColumnWidth;
private const float MsColumnWidth = PanelConstants.Profiler.MsColumnWidth;

// Dynamic bar width calculation (line 305)
float barColWidth = tableContentWidth - nameColWidth - MsColumnWidth;
```

#### Event Inspector - Mixed Approach
```csharp
// EventInspectorContent.cs:199-203
private const float EventNameColWidth = PanelConstants.EventInspector.EventNameColumnWidth;
private const float SubsColWidth = PanelConstants.EventInspector.SubsColWidth;
// ... MORE constants

// PLUS dynamic content-aware layout (line 576)
var layout = ColumnLayout.CalculateFromContent(width, x, _sortedEvents, renderer);
```

**Issue**: Event Inspector has **TWO layout systems** (constants + dynamic), increasing complexity.

---

### 4.3 Spacing and Padding

| Tab | Line Padding | Section Spacing | Row Height |
|-----|--------------|-----------------|------------|
| **Profiler** | `DebugPanelBase.StandardLinePadding` (238) | `theme.SpacingRelaxed` (284) | `lineHeight + theme.SpacingNormal` (289) |
| **Stats** | `DebugPanelBase.StandardLinePadding` (188) | `SectionSpacing` (dynamic from theme, 40) | `lineHeight + PanelConstants.Stats.RowSpacing` (291) |
| **Event Inspector** | `DebugPanelBase.StandardLinePadding` (355) | `SectionSpacing` (const, 207) | `RowHeight` (const, 206) |

**Analysis**:
- ✅ All use `DebugPanelBase.StandardLinePadding` consistently
- ❌ Event Inspector uses **constants** instead of theme values for spacing
- ✅ Profiler uses **pure theme values** (best practice)
- ⚠️ Stats uses **mixed approach** (theme accessor + constants)

---

## 5. Accessibility & Interaction Patterns

### 5.1 Keyboard Navigation

| Feature | Profiler | Stats | Event Inspector |
|---------|----------|-------|-----------------|
| Up/Down arrows | ❌ No | ❌ No | ✅ Yes (483-490) |
| PageUp/PageDown | ❌ No | ✅ Yes (226-233) | ✅ Yes (499-506) |
| Home/End | ❌ No | ✅ Yes (234-241) | ✅ Yes (507-513) |
| Mouse wheel | ✅ Yes | ✅ Yes | ✅ Yes |
| Tab key | ❌ No | ❌ No | ✅ Yes (491-494) |

**Result**: ✅ Event Inspector has **superior keyboard support**

---

### 5.2 Selection/Highlight Pattern

#### Stats - NO Selection
- Read-only display only

#### Profiler - NO Selection
- Read-only display only

#### Event Inspector - HAS Selection
```csharp
// Line 649-657: Selection highlighting
if (isSelected)
{
    LayoutRect highlightRect = new(
        x - PanelConstants.EventInspector.HighlightPaddingH,
        y - PanelConstants.EventInspector.HighlightPaddingV,
        width + (PanelConstants.EventInspector.HighlightPaddingH * 2),
        RowHeight);
    renderer.DrawRectangle(highlightRect, theme.InputSelection);
}
```

**Analysis**: ✅ Uses theme color (`theme.InputSelection`) but ❌ hardcoded padding constants

---

## 6. Content-Specific UX Patterns

### 6.1 Table Headers

#### Profiler - SortableTableHeader
```csharp
// ProfilerContent.cs:49-50
private readonly SortableTableHeader<ProfilerSortMode> _tableHeader;
_tableHeader = new SortableTableHeader<ProfilerSortMode>(ProfilerSortMode.ByExecutionTime);
```

#### Event Inspector - SortableTableHeader
```csharp
// EventInspectorContent.cs:220, 246
private readonly SortableTableHeader<EventInspectorSortMode> _tableHeader;
_tableHeader = new SortableTableHeader<EventInspectorSortMode>(EventInspectorSortMode.BySubscribers);
```

**Result**: ✅ **CONSISTENT** - Both use `SortableTableHeader<T>`

---

### 6.2 Performance Bar Visualization

#### Profiler Bar (ProfilerContent.cs:388-418)
```csharp
// Bar background
renderer.DrawRectangle(barRect, theme.BackgroundElevated);

// Bar fill
float barPercent = Math.Min((float)(entry.LastMs / TargetFrameTimeMs),
                             theme.ProfilerBarMaxScale) / theme.ProfilerBarMaxScale;

// Budget line
float budgetLineX = barColStart + (barColWidth * 0.5f);
renderer.DrawRectangle(
    new LayoutRect(budgetLineX, rowY, 1, lineHeight),
    theme.Warning * theme.ProfilerBudgetLineOpacity  // ✅ THEME CONSTANT
);
```

#### Event Inspector Bar (EventInspectorContent.cs:717-748)
```csharp
// Bar background
renderer.DrawRectangle(barRect, theme.BackgroundElevated);

// Bar fill
float barPercent = Math.Min((float)(eventInfo.AverageTimeMs / MaxBarTimeMs),
                             BarMaxScale) / BarMaxScale;

// Warning threshold marker
renderer.DrawRectangle(
    new LayoutRect(warningX, barY, 1, barHeight),
    theme.Warning * 0.5f  // ❌ HARDCODED ALPHA
);
```

**Issues**:
1. ❌ Event Inspector uses **hardcoded `0.5f` alpha** vs Profiler's `theme.ProfilerBudgetLineOpacity`
2. ✅ Both use `theme.BackgroundElevated` for bar background
3. ⚠️ Similar calculation patterns but different constant sources

---

## 7. Missing Theme Constants

### Required Theme Properties (Not Currently Available)

The Event Inspector needs these theme properties for full consistency:

```csharp
// In UITheme.cs - EventInspector-specific constants
public float EventInspectorBarOpacity { get; init; } = 0.5f;
public int EventInspectorHighlightPadding { get; init; } = 4;
public int EventInspectorTreeIndent { get; init; } = 24;
public int EventInspectorColumnPadding { get; init; } = 8;
```

**Workaround Currently Used**: Event Inspector stores these in `PanelConstants.EventInspector` instead of theme.

---

## 8. Component Naming Consistency

### Comparison Matrix

| Aspect | Profiler | Stats | Event Inspector |
|--------|----------|-------|-----------------|
| Panel class suffix | `Panel` ✅ | `Panel` ✅ | `Panel` ✅ |
| Content class suffix | `Content` ✅ | `Content` ✅ | `Content` ✅ |
| Builder class suffix | `PanelBuilder` ✅ | `PanelBuilder` ✅ | `PanelBuilder` ✅ |
| Interface naming | `IProfilerOperations` ✅ | `IStatsOperations` ✅ | `IEventInspectorOperations` ✅ |
| Status bar ID | `"profiler_status"` | `"stats_status"` | `"event_inspector_status"` |
| Content ID | `"profiler_content"` | `"stats_content"` | `"event_inspector_content"` |

**Result**: ✅ **PERFECTLY CONSISTENT** naming conventions

---

## 9. Code Quality Metrics

### Lines of Code Comparison

| File | LOC | Complexity |
|------|-----|------------|
| ProfilerContent.cs | 486 | Medium |
| StatsContent.cs | 590 | Medium-High |
| EventInspectorContent.cs | 1038 | **High** ⚠️ |

**Issue**: Event Inspector is **2x the size** of Profiler, indicating:
- More features (good) ✅
- Potential over-complexity (needs review) ⚠️
- Higher maintenance burden ❌

---

### Cyclomatic Complexity Hotspots

#### EventInspectorContent.cs High-Complexity Methods
1. `RenderEventsTable()` (562-715) - **153 lines** ❌
2. `ColumnLayout.CalculateFromContent()` (73-171) - **99 lines** ❌
3. `RenderSubscriptionsSection()` (751-819) - **69 lines** ⚠️

**Recommendation**: Consider extracting into smaller, focused methods

---

## 10. Summary of Violations

### Critical Issues (Must Fix)

1. ❌ **Hardcoded alpha multiplier** (line 746): `theme.Warning * 0.5f`
   - **Impact**: Theme switching won't adjust this value
   - **Fix**: Add `theme.EventInspectorWarningLineOpacity`

2. ❌ **Hardcoded layout offsets** (lines 308, 848, 849)
   - **Impact**: Breaks with font size changes
   - **Fix**: Calculate from measured text widths

3. ❌ **Missing theme integration** for EventInspector-specific values
   - **Impact**: Inconsistent styling across theme switches
   - **Fix**: Add EventInspector section to UITheme

### Warning Issues (Should Fix)

4. ⚠️ **Reuses Profiler theme constants** (`ProfilerBarWarningThreshold`)
   - **Impact**: Semantic confusion, tight coupling
   - **Fix**: Create dedicated EventInspector constants

5. ⚠️ **Dual layout systems** (constants + dynamic calculation)
   - **Impact**: Increased complexity
   - **Fix**: Consolidate to single approach

6. ⚠️ **Method size** (RenderEventsTable at 153 lines)
   - **Impact**: Harder to maintain
   - **Fix**: Extract column rendering to helper methods

### Good Practices Found ✅

1. ✅ Consistent use of `DebugPanelBase.StandardLinePadding`
2. ✅ Proper theme color usage for status indicators
3. ✅ Superior keyboard navigation support
4. ✅ Follows established class hierarchy pattern
5. ✅ Uses `SortableTableHeader` like Profiler
6. ✅ Consistent naming conventions

---

## 11. Recommendations

### Immediate Actions (Priority 1)

1. **Add EventInspector theme constants** to UITheme.cs:
   ```csharp
   // In all theme definitions
   EventInspectorWarningLineOpacity = 0.5f,
   EventInspectorHighlightPaddingH = 4,
   EventInspectorHighlightPaddingV = 2,
   ```

2. **Replace hardcoded alpha** (line 746):
   ```csharp
   // Before:
   theme.Warning * 0.5f

   // After:
   theme.Warning * theme.EventInspectorWarningLineOpacity
   ```

3. **Fix hardcoded offsets** using text measurement:
   ```csharp
   // Before:
   renderer.DrawText(text, x + 85, y, color);

   // After:
   float prevWidth = renderer.MeasureText(prevText).X;
   renderer.DrawText(text, x + prevWidth + theme.PaddingSmall, y, color);
   ```

### Medium-Term Refactoring (Priority 2)

4. **Extract rendering methods**:
   - `RenderEventRow(...)` from `RenderEventsTable`
   - `RenderColumnHeaders()` from `RenderEventsTable`
   - `RenderPerformanceBar(...)` (already exists, good!)

5. **Consolidate layout approach**:
   - Choose either constants OR dynamic calculation
   - Recommendation: Keep dynamic for responsiveness
   - Remove unused constants

6. **Add theme switching tests**:
   - Verify all colors update correctly
   - Test with each available theme
   - Ensure no hardcoded colors remain

### Long-Term Improvements (Priority 3)

7. **Create shared table rendering utilities**:
   - Extract common patterns from Profiler + EventInspector
   - Build reusable `DataTableRenderer<T>` component
   - Reduce duplication

8. **Performance optimization**:
   - Profile `ColumnLayout.CalculateFromContent` performance
   - Consider caching layout calculations
   - Measure impact on large event lists

---

## 12. Test Coverage Gaps

### Required Tests (Not Currently Present)

1. **Theme Switching Tests**:
   - Verify all Event Inspector colors update on theme change
   - Test with all 7 available themes
   - Validate no hardcoded color remnants

2. **Responsive Layout Tests**:
   - Test with narrow panel widths (< MinPanelWidth)
   - Verify column hiding at breakpoints
   - Test text truncation at various widths

3. **Keyboard Navigation Tests**:
   - Test all keyboard shortcuts
   - Verify selection state updates
   - Test scroll position updates

---

## Conclusion

### Overall UX Consistency Score: 6.5/10

**Strengths**:
- ✅ Follows established component structure
- ✅ Uses standard base classes and patterns
- ✅ Better keyboard navigation than other tabs
- ✅ Consistent naming conventions

**Weaknesses**:
- ❌ Hardcoded values break theme consistency
- ❌ Missing EventInspector-specific theme constants
- ❌ Oversized methods reduce maintainability
- ❌ Dual layout system increases complexity

### Priority Action Items

1. **Fix hardcoded alpha** (Critical, 15 min)
2. **Add theme constants** (Critical, 30 min)
3. **Replace magic number offsets** (High, 1 hour)
4. **Extract rendering methods** (Medium, 2 hours)
5. **Add theme switching tests** (Medium, 1 hour)

**Estimated Total Refactoring Time**: 4-5 hours for full UX consistency parity

---

**Report Generated**: 2025-12-03
**Analyst**: UX Analysis Agent (Hive Mind Swarm)
**Files Analyzed**: 10 component files, 1 theme file, 1 constants file
**Total Lines Reviewed**: ~12,500 LOC
