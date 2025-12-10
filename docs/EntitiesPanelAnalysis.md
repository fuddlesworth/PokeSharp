# EntitiesPanel Dual-Pane Redesign Analysis

## Executive Summary

The EntitiesPanel.cs implementation (currently ~2900 lines) contains robust entity list rendering and virtual scrolling infrastructure that should be preserved. The dual-pane redesign requires splitting the component inspection logic into a new right-pane component while maintaining the existing list view behavior with a crucial change: **single-click to select instead of double-click to expand**.

---

## 1. Current Click Handling (Lines 1100-1161)

### Existing Behavior
```csharp
// Lines 1116-1143
DateTime now = DateTime.Now;
double timeSinceLastClick = (now - _lastClickTime).TotalSeconds;
bool isDoubleClick =
    timeSinceLastClick < ThemeManager.Current.DoubleClickThreshold
    && _lastClickedEntityId == clickedEntityId.Value;

_lastClickTime = now;
_lastClickedEntityId = clickedEntityId.Value;

if (isDoubleClick)
{
    // Double-click: toggle expand/collapse (SAME in both views)
    ToggleEntity(clickedEntityId.Value);
}
else
{
    // Single click: select entity and move cursor
    _selectedEntityId = clickedEntityId.Value;
    if (_navigableEntityIds.Contains(clickedEntityId.Value))
    {
        _selectedIndex = _navigableEntityIds.IndexOf(clickedEntityId.Value);
    }
    _entityListBuffer.CursorLine = clickedLine;
    UpdateStatusBar();
}
```

### What Needs to Change

**REMOVE:**
- Double-click detection logic (lines 1117-1121)
- `_lastClickTime` field (line 76)
- `_lastClickedEntityId` field (line 73)
- The `ToggleEntity()` call on double-click (line 1129)

**KEEP:**
- Single-click selection logic (lines 1133-1143)
- Right-click pin toggle (lines 1149-1161)

**NEW BEHAVIOR:**
```csharp
// Simplified single-click handler
if (input.IsMouseButtonPressed(MouseButton.Left))
{
    int clickedLine = GetLineAtMousePosition(input.MousePosition, contentBounds);
    if (clickedLine < 0) return;

    int? clickedEntityId = GetEntityIdAtLine(clickedLine);
    if (!clickedEntityId.HasValue) return;

    // Single click: select entity and notify inspector
    _selectedEntityId = clickedEntityId.Value;
    if (_navigableEntityIds.Contains(clickedEntityId.Value))
    {
        _selectedIndex = _navigableEntityIds.IndexOf(clickedEntityId.Value);
    }

    _entityListBuffer.CursorLine = clickedLine;

    // NEW: Notify inspector panel of selection
    OnEntitySelected?.Invoke(clickedEntityId.Value);

    UpdateStatusBar();
    input.ConsumeMouseButton(MouseButton.Left);
}
```

---

## 2. Entity Rendering (Lines 1707-1836)

### Current Structure

The `RenderEntity()` method handles:
1. **Entity header line** (lines 1735-1779) - Shows ID, name, tag, component count
2. **Expansion state check** (line 1782) - If expanded, renders components
3. **Component details** (lines 1798-1834) - Renders all components with properties

### What Stays in Left Pane (Entity List)

```csharp
// Lines 1735-1779 - KEEP THIS
private void RenderEntityInList(EntityInfo entity)
{
    bool isSelected = _selectedEntityId == entity.Id;
    bool isNew = _newEntityIds.Contains(entity.Id);

    string selectedMarker = isSelected
        ? NerdFontIcons.SelectedWithSpace
        : NerdFontIcons.UnselectedSpace;
    string newMarker = isNew ? "* " : "";

    // Determine color based on state
    Color statusColor;
    if (isSelected)
    {
        statusColor = ThemeManager.Current.Info; // Blue for selected
    }
    else if (isNew)
    {
        statusColor = ThemeManager.Current.SuccessDim; // Bright green
    }
    else if (!entity.IsActive)
    {
        statusColor = ThemeManager.Current.TextDim;
    }
    else
    {
        statusColor = ThemeManager.Current.Success;
    }

    string headerLine = $"{selectedMarker}{newMarker}[{entity.Id}] {entity.Name}";
    if (entity.Tag != null && entity.Tag != entity.Name)
    {
        headerLine += $" ({entity.Tag})";
    }

    headerLine += $" - {entity.Components.Count} components";
    if (isNew)
    {
        headerLine += " [NEW]";
    }

    _entityListBuffer.AppendLine(headerLine, statusColor);
}
```

**REMOVE from left pane:**
- `expandIndicator` (lines 1736-1738) - No longer needed
- Expansion check (line 1782)
- Component rendering (lines 1798-1834)
- Relationship rendering in normal view

### What Moves to Right Pane (New Inspector Component)

```csharp
// NEW COMPONENT: EntityInspectorPanel.cs
public class EntityInspectorPanel : DebugPanelBase
{
    private EntityInfo? _inspectedEntity;

    public void InspectEntity(EntityInfo entity)
    {
        _inspectedEntity = entity;

        // Lines 1792-1834: Move this logic here
        if (entity.Relationships.Count > 0)
        {
            RenderRelationships(entity);
        }

        _buffer.AppendLine("Components:", ThemeManager.Current.Info);
        foreach (string component in entity.Components.Take(MaxComponentsToShow))
        {
            Color componentColor = GetComponentColor(component);
            _buffer.AppendLine($"  - {component}", componentColor);

            // Render component field values
            if (entity.ComponentData.TryGetValue(component, out var fields))
            {
                var sortedFields = fields.OrderBy(kvp => kvp.Key).ToList();
                foreach ((string fieldName, string fieldValue) in sortedFields)
                {
                    RenderPropertyValue(fieldName, fieldValue, "      ");
                }
            }
        }
    }
}
```

**Methods to extract to new component:**
- `RenderRelationships()` (lines 2259-2290)
- `RenderPropertyValue()` (lines 2296-2344)
- `RenderProperty()` (lines 2350-2355)
- `GetPropertyValueColor()` (lines 2360-2420)
- `GetComponentColor()` (needs to be located)

---

## 3. The _expandedEntities HashSet

### Current Usage

```csharp
// Line 40: Field declaration
private readonly HashSet<int> _expandedEntities = new();

// Line 1709: Expansion check
bool isExpanded = _expandedEntities.Contains(entity.Id);

// Lines 719-776: Expansion methods
public void ExpandEntity(int entityId)
{
    _expandedEntities.Add(entityId);
    _heightsNeedRecalculation = true;
    UpdateDisplay();
}

public void CollapseEntity(int entityId)
{
    _expandedEntities.Remove(entityId);
    _heightsNeedRecalculation = true;
    UpdateDisplay();
}

public bool ToggleEntity(int entityId)
{
    _heightsNeedRecalculation = true;
    if (_expandedEntities.Contains(entityId))
    {
        _expandedEntities.Remove(entityId);
        UpdateDisplay();
        return false;
    }
    _expandedEntities.Add(entityId);
    UpdateDisplay();
    return true;
}
```

### What Needs to Change

**DECISION NEEDED:** Should this be removed entirely or repurposed?

**Option A: Remove Completely**
- Delete `_expandedEntities` field
- Delete expansion/collapse methods
- Remove from `IEntityOperations` interface
- Remove from command system

**Option B: Repurpose for Tree View**
- Keep for TreeView mode only (entity hierarchy)
- Remove from Normal view mode
- Only affects visual tree expansion, not inspector

**RECOMMENDATION: Option A** - Complete removal simplifies the dual-pane model

---

## 4. Virtual Scrolling Infrastructure (Lines 100-107, 2816-2950)

### Critical Components to PRESERVE

```csharp
// Line 100-107: Virtual scrolling state
private readonly List<int> _entityHeights = new();
private readonly List<int> _cumulativeHeights = new();
private int _totalVirtualHeight;
private int _lastScrollOffset = -1;
private int _visibleStartIndex;
private int _visibleEndIndex;
private bool _heightsNeedRecalculation = true;

// Lines 2816-2903: Height calculation
private int CalculateEntityHeight(EntityInfo entity)
{
    // Currently considers expansion - needs update
    if (!_expandedEntities.Contains(entity.Id))
    {
        return 1; // Collapsed
    }

    // Expanded height calculation...
}

private void RecalculateEntityHeights()
{
    _entityHeights.Clear();
    _cumulativeHeights.Clear();
    _totalVirtualHeight = 0;

    foreach (EntityInfo entity in _filteredEntities)
    {
        int entityHeight = CalculateEntityHeight(entity);
        _entityHeights.Add(entityHeight);
        _cumulativeHeights.Add(_totalVirtualHeight);
        _totalVirtualHeight += entityHeight;
    }

    _heightsNeedRecalculation = false;
}

// Lines 2908-2950: Binary search for visible range
private int FindEntityIndexAtLine(int lineOffset)
{
    // Binary search in cumulative heights
}
```

### Required Changes to Virtual Scrolling

**BEFORE (with expansion):**
```csharp
private int CalculateEntityHeight(EntityInfo entity)
{
    if (!_expandedEntities.Contains(entity.Id))
    {
        return 1; // Collapsed: just header
    }

    // Expanded: header + components + relationships
    int lines = 1; // Header
    if (_viewMode == EntityViewMode.Relationships)
    {
        // Complex relationship tree calculation
    }
    else
    {
        lines += 2; // "Components:" header + blank line
        lines += Math.Min(entity.Components.Count, MaxComponentsToShow);
        // ... property lines
    }
    return lines;
}
```

**AFTER (no expansion):**
```csharp
private int CalculateEntityHeight(EntityInfo entity)
{
    // All entities are now single-line
    return 1;
}
```

**Impact:**
- Virtual scrolling becomes MUCH simpler
- All entities are uniform height
- `RecalculateEntityHeights()` can be optimized:
  ```csharp
  private void RecalculateEntityHeights()
  {
      _entityHeights.Clear();
      _cumulativeHeights.Clear();

      // Uniform height optimization
      int count = _filteredEntities.Count;
      for (int i = 0; i < count; i++)
      {
          _entityHeights.Add(1);
          _cumulativeHeights.Add(i);
      }

      _totalVirtualHeight = count;
      _heightsNeedRecalculation = false;
  }
  ```

---

## 5. Code Reusability Matrix

### Can Be Reused As-Is (Left Pane)

| Component | Lines | Purpose |
|-----------|-------|---------|
| Virtual scrolling infrastructure | 100-107, 2816-2950 | Performance for 1M+ entities |
| Filtering logic | 609-692 | Tag/search/component filters |
| Entity provider/paging | 342-502 | Lazy loading support |
| Pinning logic | 785-800 | Pin entities to top |
| Mouse position calculation | 1167-1240 | Click handling |
| Keyboard navigation | 950-1085 | Arrow keys, Page Up/Down |
| Status bar updates | Helper methods | Entity counts, filters |
| Change tracking | 44-52, 454-488 | New/removed entities |

### Needs Modification (Left Pane)

| Component | Lines | Change Required |
|-----------|-------|-----------------|
| `RenderEntity()` | 1707-1836 | Remove expansion logic, components |
| Click handler | 1100-1146 | Remove double-click, add selection event |
| `CalculateEntityHeight()` | 2816-2886 | Always return 1 (no expansion) |
| Header rendering | 1735-1779 | Remove expand indicator |
| Interface methods | 207-235 | Remove Expand/Collapse/Toggle |

### Needs Extraction (Right Pane - New Component)

| Component | Lines | Purpose |
|-----------|-------|---------|
| Component rendering | 1798-1834 | Display component list |
| Relationship rendering | 2259-2290 | Display relationships |
| Property rendering | 2296-2344 | Display component fields |
| Property coloring | 2360-2420 | Syntax highlighting |
| Component coloring | (find location) | Component type colors |
| Lazy detail loading | 1713-1733 | On-demand component data |

### Shared Utilities (Both Panes)

| Component | Usage |
|-----------|-------|
| `ThemeManager` colors | Both panes use same theme |
| `NerdFontIcons` | Both need icons |
| `EntityInfo` model | Shared data structure |
| Component name registry | Filter autocomplete + inspector |

---

## 6. New Component Architecture

### Communication Flow

```
┌─────────────────────┐         ┌──────────────────────┐
│  EntitiesPanel      │         │ EntityInspectorPanel │
│  (Left Pane)        │         │ (Right Pane)         │
│                     │         │                      │
│  - Entity list      │         │ - Component details  │
│  - Virtual scroll   │ select  │ - Property values    │
│  - Filtering        │────────>│ - Relationships      │
│  - Pinning          │  event  │ - Scrollable view    │
│                     │         │                      │
│  Single-click:      │         │ Updates on:          │
│  fires OnEntitySel. │         │ - Selection change   │
└─────────────────────┘         └──────────────────────┘
```

### Event Interface

```csharp
// In EntitiesPanel
public event Action<int>? OnEntitySelected;

// In click handler
_selectedEntityId = clickedEntityId.Value;
OnEntitySelected?.Invoke(clickedEntityId.Value);

// In parent container (DebugConsole)
entitiesPanel.OnEntitySelected += inspectorPanel.InspectEntity;

// In EntityInspectorPanel
public void InspectEntity(int entityId)
{
    // Load entity details (may use detail loader)
    EntityInfo? entity = LoadEntityDetails(entityId);
    if (entity == null)
    {
        ShowEmptyState();
        return;
    }

    // Render components and relationships
    RenderInspector(entity);
}
```

---

## 7. Migration Checklist

### Phase 1: Cleanup EntitiesPanel
- [ ] Remove `_expandedEntities` HashSet
- [ ] Remove `_lastClickTime` and `_lastClickedEntityId`
- [ ] Remove double-click detection logic
- [ ] Remove `ExpandEntity()`, `CollapseEntity()`, `ToggleEntity()`
- [ ] Remove `ExpandAll()`, `CollapseAll()`
- [ ] Update `IEntityOperations` interface
- [ ] Simplify `CalculateEntityHeight()` to always return 1
- [ ] Remove expand indicator from header rendering
- [ ] Strip component rendering from `RenderEntity()`
- [ ] Add `OnEntitySelected` event

### Phase 2: Create EntityInspectorPanel
- [ ] Create new class inheriting `DebugPanelBase`
- [ ] Add TextBuffer for scrollable content
- [ ] Extract component rendering logic
- [ ] Extract relationship rendering logic
- [ ] Extract property rendering helpers
- [ ] Extract property coloring logic
- [ ] Implement `InspectEntity(int entityId)` method
- [ ] Add empty state when no entity selected
- [ ] Add error handling for deleted entities

### Phase 3: Integrate Dual-Pane Layout
- [ ] Modify DebugConsole to split Entities tab
- [ ] Create SplitPane container (70/30 or configurable)
- [ ] Wire up selection event
- [ ] Handle entity deletion gracefully
- [ ] Test with 1M+ entities
- [ ] Verify virtual scrolling still works
- [ ] Test lazy loading in inspector

### Phase 4: Testing
- [ ] Test single-click selection
- [ ] Test right-click pinning still works
- [ ] Test keyboard navigation
- [ ] Test filtering updates both panes
- [ ] Test component detail loading
- [ ] Test relationship display
- [ ] Test edge cases (empty, deleted, no components)

---

## 8. Performance Considerations

### Left Pane Improvements
- **Before:** Variable height entities (1-100+ lines when expanded)
- **After:** Uniform 1-line height
- **Impact:** Simpler virtual scrolling, faster rendering

### Right Pane Concerns
- **Challenge:** Component data loading for selected entity
- **Solution:** Existing lazy loader (`_entityDetailLoader`) can be reused
- **Optimization:** Cache last N inspected entities

### Memory Footprint
- **Before:** Expanded entities loaded component data
- **After:** Only selected entity loads component data
- **Savings:** Significant when multiple entities were expanded

---

## 9. Recommended Implementation Order

1. **Create EntityInspectorPanel stub** (empty component)
2. **Modify EntitiesPanel** (remove expansion, add event)
3. **Extract rendering methods** (move to inspector)
4. **Wire up dual-pane** (split layout, event connection)
5. **Test and refine** (edge cases, performance)

---

## 10. Open Questions

1. **Should TreeView mode still support expansion?**
   - Current behavior: Shows hierarchical relationships
   - Recommendation: Keep tree expansion for hierarchy only

2. **What happens when selected entity is deleted?**
   - Option A: Clear inspector
   - Option B: Show "Entity deleted" message
   - Recommendation: Option B with auto-clear after 2 seconds

3. **Should inspector auto-scroll to top on selection?**
   - Recommendation: Yes, always scroll to top

4. **Should there be a "lock inspector" feature?**
   - Use case: Compare two entities by switching selection
   - Recommendation: Future enhancement, not MVP

---

## Conclusion

The dual-pane redesign is **highly feasible** with **minimal disruption** to the existing codebase. The virtual scrolling infrastructure remains intact, and the separation of concerns actually **improves** the architecture. The key change is replacing double-click expansion with single-click selection and moving component inspection to a dedicated right pane.

**Estimated Effort:**
- EntitiesPanel modifications: 4-6 hours
- EntityInspectorPanel creation: 6-8 hours
- Integration and testing: 3-4 hours
- **Total: 13-18 hours**

**Risk Level:** Low (well-isolated changes, existing infrastructure reusable)
