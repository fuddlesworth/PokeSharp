# Event Inspector Panel - UX Analysis & Fixes

**Date:** December 3, 2025  
**Status:** 🔴 Issues Identified - Fixes In Progress

---

## Executive Summary

The EventInspectorContent panel has several UX issues that impact usability, particularly with responsive column widths causing text bleeding and overlap at narrow panel widths.

---

## 🔴 CRITICAL ISSUES

### 1. Column Width Not Responsive (CRITICAL)

**Location:** `EventInspectorContent.cs` lines 403-412

**Problem:**
```csharp
float fixedColumnsWidth = EventNameColWidth + SubsColWidth + CountColWidth + TimeColWidth;
float barColWidth = width - fixedColumnsWidth; // Can be negative or too small!
```

**Issue:** When panel width < 420px, `barColWidth` becomes negative, causing:
- Columns to overlap and bleed into each other
- "Avg/Max" column bleeding into "Count" column (user-reported issue)
- Performance bar rendering at wrong position
- Text overlapping and becoming unreadable

**Impact:** HIGH - Makes panel unusable at narrow widths

**Solution:** 
- Add minimum panel width enforcement
- Make columns responsive with minimum widths
- Hide less important columns when space is constrained
- Add proper overflow handling

---

### 2. No Column Width Validation

**Location:** `EventInspectorContent.cs` lines 403-412, 494-516

**Problem:** No validation that columns fit within available space

**Issues:**
- No check if `barColWidth > 0`
- No check if columns will fit
- No graceful degradation for narrow widths
- Text rendering outside column boundaries

**Impact:** MEDIUM-HIGH - Causes visual glitches and poor UX

---

## ⚠️ UX PROBLEMS

### 3. Hardcoded Layout Values

**Location:** Throughout `EventInspectorContent.cs`

**Hardcoded Values Found:**
```csharp
// Line 405: Fixed column widths (from PanelConstants, but not responsive)
EventNameColWidth = 200f  // Fixed, no min/max
SubsColWidth = 50f        // Too narrow for "Subs" + number
CountColWidth = 70f       // Can't fit large numbers (1.2M)
TimeColWidth = 100f       // Barely fits "99.99/99.99"

// Lines 48-50: Performance thresholds
MaxBarTimeMs = 1.0f       // Not configurable per-user
WarningThresholdMs = 0.5f
ErrorThresholdMs = 1.0f

// Line 43: Row height
RowHeight = 22f           // Fixed, not based on font size

// Line 44: Section spacing
SectionSpacing = 12f      // Fixed
```

**Impact:** MEDIUM - Reduces flexibility and makes theming harder

**Solution:**
- Make thresholds configurable
- Add responsive min/max widths
- Use theme-relative sizing where possible

---

### 4. Complex Column Layout Logic

**Location:** `EventInspectorContent.cs` lines 403-456

**Problem:** Column position calculation is complex and error-prone:
```csharp
float barColStart = x + EventNameColWidth;
float subsColStart = barColStart + barColWidth;
float countColStart = subsColStart + SubsColWidth;
float timeColStart = countColStart + CountColWidth;
```

**Issues:**
- Hard to understand left-to-right accumulation
- Easy to make off-by-one errors
- No encapsulation of column layout
- Repeated in multiple places (header + rows)

**Impact:** MEDIUM - Makes code hard to maintain and extend

**Solution:** Extract to `ColumnLayout` helper class

---

### 5. Text Truncation Issues

**Location:** Lines 484-488, 499-501, 506-509

**Problems:**
```csharp
// Event name truncation works well
string displayName = renderer.TruncateWithEllipsis(
    $"{statusIcon} {eventInfo.EventTypeName}",
    EventNameColWidth - 8);

// BUT: Number columns don't truncate or handle overflow
// If count is "1.2M", it can overflow into next column
// If time is "999.99/999.99", it overflows
```

**Impact:** MEDIUM - Numbers can bleed into adjacent columns

**Solution:**
- Add truncation for number columns
- Use scientific notation for very large numbers
- Add overflow indicators (e.g., "99.9+")

---

### 6. No Minimum Panel Width

**Location:** No validation in `EventInspectorContent` or `EventInspectorPanel`

**Problem:** Panel can be resized to unusably narrow widths

**Impact:** MEDIUM - Poor UX when panel is too narrow

**Solution:** Add minimum width constraint (e.g., 500px)

---

## 🟡 CODE SMELLS

### 7. Magic Numbers

**Location:** Throughout file

**Examples:**
```csharp
// Line 476: Highlight rect padding
new LayoutRect(x - 4, y - 2, width + 8, RowHeight)  // -4, -2, +8 magic numbers

// Line 487: Column padding
EventNameColWidth - 8  // Why 8?

// Line 491: Bar width padding
barColWidth - 8  // Why 8?

// Line 496: Column padding
subsColStart + SubsColWidth - subsWidth - 4  // Why 4?

// Lines 597-598: Tree indent
x + 24  // Why 24?
x + 70  // Why 70?
```

**Impact:** LOW - Makes code less maintainable

**Solution:** Extract to named constants

---

### 8. Long Methods

**Location:** Multiple methods > 50 lines

**Examples:**
- `RenderEventsTable` (124 lines, 399-523)
- `RenderSubscriptionsSection` (57 lines, 558-614)
- `RenderRecentEvents` (39 lines, 616-654)

**Impact:** LOW - Makes code harder to read and test

**Solution:** Extract sub-methods for rendering different parts

---

### 9. Repeated String Formatting

**Location:** Lines 499, 506

**Example:**
```csharp
// Count formatting
string countText = FormatCount(eventInfo.PublishCount);

// Time formatting - repeated logic
string timeText = $"{eventInfo.AverageTimeMs:F2}/{eventInfo.MaxTimeMs:F2}";
// Should be: FormatTimeRange(eventInfo.AverageTimeMs, eventInfo.MaxTimeMs)
```

**Impact:** LOW - Minor code duplication

---

## 📊 COMPARISON WITH PROFILER PANEL

ProfilerContent.cs handles similar layout much better:

**ProfilerContent Advantages:**
1. ✅ Simpler column layout (only 2 columns with fixed widths)
2. ✅ Bar column properly sized: `barColWidth = width - NameColumnWidth - MsColumnWidth`
3. ✅ Clear separation between fixed and dynamic columns
4. ✅ Proper scrollbar handling

**EventInspector Disadvantages:**
1. ❌ 5 columns makes layout more complex
2. ❌ No minimum width validation
3. ❌ No responsive column hiding
4. ❌ More complex layout calculations

---

## 🔧 RECOMMENDED FIXES (Priority Order)

### Priority 1: Critical Fixes (MUST FIX)

1. **Add Column Width Validation**
   ```csharp
   const float MinPanelWidth = 500f;
   const float MinBarColWidth = 100f;
   
   if (width < MinPanelWidth)
   {
       // Show warning or simplified layout
   }
   
   float barColWidth = Math.Max(MinBarColWidth, width - fixedColumnsWidth);
   ```

2. **Add Responsive Column Hiding**
   ```csharp
   bool showExecutionTimeBar = width >= 600f;
   bool showSubsColumn = width >= 500f;
   bool showCountColumn = width >= 400f;
   
   // Adjust layout based on available columns
   ```

3. **Fix Column Position Calculation**
   ```csharp
   class ColumnLayout
   {
       public float EventNameX, EventNameWidth;
       public float BarX, BarWidth;
       public float SubsX, SubsWidth;
       public float CountX, CountWidth;
       public float TimeX, TimeWidth;
       
       public static ColumnLayout Calculate(float width, float x)
       {
           // Centralized layout logic
       }
   }
   ```

### Priority 2: UX Improvements

4. **Add Text Overflow Handling**
5. **Extract Magic Numbers to Constants**
6. **Add Minimum Panel Width**

### Priority 3: Code Quality

7. **Refactor Long Methods**
8. **Extract Helper Methods**
9. **Add Unit Tests for Layout Calculations**

---

## 🎯 ACCEPTANCE CRITERIA

- [ ] Panel works correctly at widths from 500px to 1200px
- [ ] No column bleeding or text overlap at any width
- [ ] Columns hide gracefully when space is limited
- [ ] All magic numbers extracted to named constants
- [ ] Column layout logic centralized and testable
- [ ] Minimum panel width enforced
- [ ] Text truncation works for all columns
- [ ] No visual glitches or rendering artifacts

---

## 📝 IMPLEMENTATION NOTES

### Suggested Column Responsive Breakpoints:

- **1200px+**: All columns visible, generous spacing
- **800-1200px**: All columns visible, compact spacing
- **600-800px**: Hide execution time bar, show text values only
- **500-600px**: Hide Subs column, keep Count and Time
- **< 500px**: Show warning "Panel too narrow - resize to view"

### Suggested Constants Extraction:

```csharp
// Column layout
private const float MinPanelWidth = 500f;
private const float MinBarColWidth = 100f;
private const float ColumnPadding = 8f;
private const float ColumnRightPadding = 4f;
private const float HighlightPaddingH = 4f;
private const float HighlightPaddingV = 2f;

// Tree rendering
private const float TreeIndentLevel1 = 24f;
private const float TreeIndentLevel2 = 70f;

// Breakpoints
private const float BreakpointFullLayout = 800f;
private const float BreakpointNoBar = 600f;
private const float BreakpointNoSubs = 500f;
```

---

**Next Steps:**
1. Implement Priority 1 fixes (column validation and responsive hiding)
2. Test at various panel widths
3. Implement Priority 2 UX improvements
4. Code quality refactoring (Priority 3)


