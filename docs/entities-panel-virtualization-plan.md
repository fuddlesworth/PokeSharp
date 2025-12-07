# EntitiesPanel Virtualization Implementation Plan

## Overview

This document outlines the step-by-step implementation plan for adding UI virtualization to `EntitiesPanel.cs` to fix the freeze when displaying 1 million+ entities.

**Target File:** `MonoBallFramework.Game/Engine/UI/Components/Debug/EntitiesPanel.cs`

**Estimated Effort:** 4-6 hours implementation + 2 hours testing

---

## Phase 1: Add Virtual Height Tracking Infrastructure

### Step 1.1: Add New Class Fields

**Location:** After line ~45 (existing field declarations)

```csharp
// Virtual scrolling infrastructure
private readonly List<int> _entityHeights = new();      // Height (lines) per entity in _filteredEntities
private readonly List<int> _cumulativeHeights = new(); // Cumulative heights for binary search
private int _totalVirtualHeight;                        // Total lines if all entities were rendered
private int _lastScrollOffset = -1;                     // Track scroll changes for re-render
private int _visibleStartIndex;                         // First visible entity index (cached)
private int _visibleEndIndex;                           // Last visible entity index (cached)

// Performance: avoid recalculating heights every frame
private bool _heightsNeedRecalculation = true;
```

### Step 1.2: Create CalculateEntityHeight Method

**Location:** After existing helper methods (~line 1200)

```csharp
/// <summary>
/// Calculates the display height (in lines) for a single entity.
/// Collapsed entities = 1 line, expanded entities = header + components + relationships.
/// </summary>
private int CalculateEntityHeight(EntityInfo entity)
{
    // Collapsed: just the header line
    if (!_expandedEntities.Contains(entity.Id))
    {
        return 1;
    }

    // Expanded: count all rendered lines
    int height = 1; // Header line

    // Relationships section (if any)
    if (entity.Relationships.Count > 0)
    {
        height += 1; // "Relationships:" header
        foreach (var relType in entity.Relationships)
        {
            if (relType.Value.Count > 0)
            {
                height += 1; // Relationship type header
                height += Math.Min(relType.Value.Count, 10); // Cap displayed relationships
            }
        }
        height += 1; // Blank line after relationships
    }

    // Components section
    height += 1; // "Components:" header
    int componentCount = Math.Min(entity.Components.Count, MaxComponentsToShow);

    foreach (string componentName in entity.Components.Take(componentCount))
    {
        height += 1; // Component name line

        // Component data lines (if ShowComponentData is enabled)
        if (_showComponentData && entity.ComponentData.TryGetValue(componentName, out var fields))
        {
            height += Math.Min(fields.Count, 20); // Cap fields per component
        }
    }

    // "More components..." line if truncated
    if (entity.Components.Count > MaxComponentsToShow)
    {
        height += 1;
    }

    height += 1; // Blank line after entity

    return height;
}
```

### Step 1.3: Create Height Recalculation Method

**Location:** After CalculateEntityHeight

```csharp
/// <summary>
/// Rebuilds the height cache for all filtered entities.
/// Called when filters change, entities expand/collapse, or data refreshes.
/// </summary>
private void RecalculateEntityHeights()
{
    _entityHeights.Clear();
    _cumulativeHeights.Clear();
    _totalVirtualHeight = 0;

    foreach (EntityInfo entity in _filteredEntities)
    {
        int height = CalculateEntityHeight(entity);
        _entityHeights.Add(height);
        _cumulativeHeights.Add(_totalVirtualHeight);
        _totalVirtualHeight += height;
    }

    _heightsNeedRecalculation = false;
}
```

### Step 1.4: Create Binary Search for Visible Range

**Location:** After RecalculateEntityHeights

```csharp
/// <summary>
/// Finds the entity index at a given line offset using binary search.
/// Returns the index of the entity that contains the given line.
/// </summary>
private int FindEntityIndexAtLine(int lineOffset)
{
    if (_cumulativeHeights.Count == 0)
        return 0;

    // Binary search for the entity containing this line
    int left = 0;
    int right = _cumulativeHeights.Count - 1;

    while (left < right)
    {
        int mid = (left + right + 1) / 2;
        if (_cumulativeHeights[mid] <= lineOffset)
            left = mid;
        else
            right = mid - 1;
    }

    return left;
}

/// <summary>
/// Calculates the visible entity range based on scroll position.
/// </summary>
private (int startIndex, int endIndex) CalculateVisibleRange(int scrollOffset, int visibleLines)
{
    if (_filteredEntities.Count == 0)
        return (0, 0);

    // Find first visible entity
    int startIndex = FindEntityIndexAtLine(scrollOffset);

    // Find last visible entity (with buffer for smooth scrolling)
    int targetEndLine = scrollOffset + visibleLines + 20; // 20 line buffer
    int endIndex = FindEntityIndexAtLine(targetEndLine);
    endIndex = Math.Min(endIndex + 1, _filteredEntities.Count);

    return (startIndex, endIndex);
}
```

---

## Phase 2: Modify UpdateDisplay for Virtual Rendering

### Step 2.1: Refactor UpdateDisplay Method

**Location:** Replace existing UpdateDisplay method (~lines 1227-1369)

```csharp
/// <summary>
/// Updates the display with only the visible entities (virtualized rendering).
/// </summary>
private void UpdateDisplay()
{
    // Recalculate heights if needed
    if (_heightsNeedRecalculation)
    {
        RecalculateEntityHeights();
    }

    // Get scroll position and visible area
    int scrollOffset = _entityListBuffer.ScrollOffset;
    int visibleLines = Math.Max(_entityListBuffer.VisibleLineCount, 30);

    // Calculate which entities are visible
    (int startIndex, int endIndex) = CalculateVisibleRange(scrollOffset, visibleLines);
    _visibleStartIndex = startIndex;
    _visibleEndIndex = endIndex;

    // Clear buffer and mappings for fresh render
    _entityListBuffer.Clear();
    _lineToEntityId.Clear();
    _navigableEntityIds.Clear();

    // Handle empty state
    if (_filteredEntities.Count == 0)
    {
        _entityListBuffer.AppendLine("No entities match the current filter.", _theme.TextSecondary);
        UpdateStatusBar();
        return;
    }

    // Render header showing virtual position
    if (startIndex > 0)
    {
        _entityListBuffer.AppendLine(
            $"... {startIndex} entities above (scroll up to see) ...",
            _theme.TextDim
        );
    }

    // Render ONLY visible entities
    for (int i = startIndex; i < endIndex && i < _filteredEntities.Count; i++)
    {
        EntityInfo entity = _filteredEntities[i];
        _navigableEntityIds.Add(entity.Id);

        int lineNum = _entityListBuffer.TotalLines;
        RenderEntity(entity);
        _lineToEntityId[lineNum] = entity.Id;
    }

    // Render footer showing entities below
    int entitiesBelow = _filteredEntities.Count - endIndex;
    if (entitiesBelow > 0)
    {
        _entityListBuffer.AppendLine(
            $"... {entitiesBelow} entities below (scroll down to see) ...",
            _theme.TextDim
        );
    }

    // Set virtual total for proper scrollbar sizing
    // This tells the TextBuffer how tall the content "would be" if fully rendered
    _entityListBuffer.SetVirtualTotalLines(_totalVirtualHeight);

    UpdateStatusBar();
}
```

### Step 2.2: Update StatusBar to Show Virtual Info

**Location:** Modify UpdateStatusBar method

```csharp
private void UpdateStatusBar()
{
    int totalEntities = _entities.Count;
    int filteredCount = _filteredEntities.Count;
    int visibleCount = _visibleEndIndex - _visibleStartIndex;

    string filterInfo = totalEntities != filteredCount
        ? $" (showing {filteredCount} filtered)"
        : "";

    string virtualInfo = filteredCount > visibleCount
        ? $" | Viewing {_visibleStartIndex + 1}-{_visibleEndIndex} of {filteredCount}"
        : "";

    _statusBar.SetText($"Entities: {totalEntities}{filterInfo}{virtualInfo}");
}
```

---

## Phase 3: Add Scroll Change Detection

### Step 3.1: Modify OnRenderContainer

**Location:** In OnRenderContainer method (~line 287)

Add scroll detection at the beginning of the method:

```csharp
protected override void OnRenderContainer(UIContext context)
{
    // Detect scroll position changes and re-render visible window
    int currentScrollOffset = _entityListBuffer.ScrollOffset;
    if (_lastScrollOffset != currentScrollOffset)
    {
        _lastScrollOffset = currentScrollOffset;
        // Only update display, don't refresh data
        UpdateDisplay();
    }

    // ... rest of existing OnRenderContainer code
}
```

---

## Phase 4: Mark Heights Dirty on State Changes

### Step 4.1: Update ToggleEntityExpansion

**Location:** Modify ToggleEntityExpansion method

```csharp
private void ToggleEntityExpansion(int entityId)
{
    if (_expandedEntities.Contains(entityId))
    {
        _expandedEntities.Remove(entityId);
    }
    else
    {
        _expandedEntities.Add(entityId);
    }

    // Mark heights for recalculation since expansion changes height
    _heightsNeedRecalculation = true;
    UpdateDisplay();
}
```

### Step 4.2: Update ApplyFilters

**Location:** Modify ApplyFilters method (~line 511)

Add at the end of ApplyFilters:

```csharp
private void ApplyFilters()
{
    // ... existing filter logic ...

    // Heights must be recalculated when filtered set changes
    _heightsNeedRecalculation = true;
}
```

### Step 4.3: Update RefreshEntities

**Location:** Modify RefreshEntities method (~line 336)

```csharp
public void RefreshEntities()
{
    // ... existing refresh logic ...

    // Mark heights dirty after data refresh
    _heightsNeedRecalculation = true;

    ApplyFilters();
    UpdateDisplay();
}
```

---

## Phase 5: TextBuffer Virtual Height Support

### Step 5.1: Check TextBuffer for SetVirtualTotalLines

**Action:** Verify if TextBuffer has `SetVirtualTotalLines` method

If not present, add to `TextBuffer.cs`:

```csharp
/// <summary>
/// Sets the virtual total lines for scrollbar calculation.
/// Used when content is virtualized and only a portion is actually rendered.
/// </summary>
private int _virtualTotalLines = -1;

public void SetVirtualTotalLines(int totalLines)
{
    _virtualTotalLines = totalLines;
}

/// <summary>
/// Gets the total lines for scrollbar calculation.
/// Returns virtual total if set, otherwise actual total.
/// </summary>
public int TotalLinesForScrollbar => _virtualTotalLines > 0 ? _virtualTotalLines : TotalLines;
```

### Step 5.2: Update TextBuffer Scrollbar Logic

Ensure scrollbar uses `TotalLinesForScrollbar` instead of `TotalLines` for:
- Scrollbar thumb size calculation
- Maximum scroll offset calculation

---

## Phase 6: Testing & Validation

### Test Cases

1. **Empty entity list** - No crash, shows "No entities" message
2. **Small list (10 entities)** - All entities visible, no virtualization needed
3. **Medium list (1,000 entities)** - Smooth scrolling, correct entity display
4. **Large list (100,000 entities)** - Fast initial render, smooth scrolling
5. **Massive list (1,000,000 entities)** - No freeze, responsive UI
6. **Expand/collapse entity** - Height recalculates, display updates correctly
7. **Filter change** - Filtered list updates, heights recalculate
8. **Scroll to top/bottom** - Edge entities render correctly
9. **Rapid scrolling** - No visual glitches or crashes

### Performance Metrics to Verify

| Metric | Before | Target |
|--------|--------|--------|
| Initial load (1M entities) | 10-30s freeze | <100ms |
| Scroll response | Frozen | <16ms (60fps) |
| Memory (TextBuffer) | 100MB+ | <5MB |
| Entities rendered per frame | 1,000,000 | 30-100 |

---

## Implementation Order Summary

1. **Step 1.1-1.4:** Add infrastructure (fields, height calculation, binary search)
2. **Step 2.1-2.2:** Refactor UpdateDisplay for virtual rendering
3. **Step 3.1:** Add scroll detection
4. **Step 4.1-4.3:** Mark heights dirty on state changes
5. **Step 5.1-5.2:** TextBuffer virtual height support (if needed)
6. **Phase 6:** Testing and validation

---

## Risk Mitigation

| Risk | Mitigation |
|------|------------|
| Off-by-one errors in binary search | Extensive edge case testing |
| Scrollbar jumps when heights change | Maintain scroll position relative to current entity |
| Line-to-entity mapping incorrect | Clear and rebuild mapping each render |
| Height calculation mismatch with RenderEntity | Extract shared logic or calculate from RenderEntity |

---

## Rollback Plan

If issues arise, the changes are isolated to:
- `EntitiesPanel.cs` - Can revert to non-virtualized UpdateDisplay
- `TextBuffer.cs` - Virtual height is additive, won't break existing code

Keep the original UpdateDisplay logic commented out during initial testing for quick rollback.
