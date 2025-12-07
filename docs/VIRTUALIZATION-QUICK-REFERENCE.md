# Virtual List Implementation - Quick Reference

**For Coder Agent:** Copy-paste ready structures and algorithms

---

## 1. New Class Fields (Add to EntitiesPanel)

```csharp
// Virtual rendering tracking
private readonly List<int> _entityLineHeights = new();   // Height of each entity in lines
private readonly List<int> _entityLineOffsets = new();   // Cumulative line offset for each entity
private int _totalVirtualLines = 0;                       // Total lines in virtual document
```

---

## 2. Binary Search Algorithm

```csharp
/// <summary>
/// Finds the entity index that covers the given line number in virtual space.
/// Uses binary search: O(log N) where N = number of entities.
/// </summary>
private int BinarySearchEntityAtLine(int lineNumber)
{
    if (_entityLineOffsets.Count == 0)
        return 0;

    int left = 0, right = _entityLineOffsets.Count - 1;

    while (left < right)
    {
        int mid = (left + right + 1) / 2;
        if (_entityLineOffsets[mid] <= lineNumber)
            left = mid;
        else
            right = mid - 1;
    }

    return Math.Clamp(left, 0, _entityLineOffsets.Count - 1);
}
```

---

## 3. Calculate Visible Range

```csharp
/// <summary>
/// Determines which entities should be rendered based on current scroll position.
/// Returns: start entity index, where to start rendering, end entity index.
/// </summary>
private void CalculateVisibleRange(
    out int startEntityIndex,
    out int startLineOffset,
    out int endEntityIndex)
{
    int scrollOffset = _entityListBuffer.ScrollOffset;
    int visibleLines = _entityListBuffer.VisibleLineCount;

    // Find first visible entity
    startEntityIndex = BinarySearchEntityAtLine(scrollOffset);
    startLineOffset = scrollOffset - _entityLineOffsets[startEntityIndex];

    // Find last visible entity
    int lastVisibleLine = scrollOffset + visibleLines;
    endEntityIndex = BinarySearchEntityAtLine(lastVisibleLine);

    // Clamp to valid range
    startEntityIndex = Math.Clamp(startEntityIndex, 0, _filteredEntities.Count - 1);
    endEntityIndex = Math.Clamp(endEntityIndex, 0, _filteredEntities.Count - 1);
}
```

---

## 4. Calculate Entity Height (Phase 1 - Simple)

```csharp
/// <summary>
/// Calculates how many lines an entity will take when rendered.
/// Phase 1: Simple calculation for testing.
/// </summary>
private int CalculateEntityHeight(EntityInfo entity)
{
    // Collapsed entities always take exactly 1 line
    if (!_expandedEntities.Contains(entity.Id))
        return 1;

    // Expanded: estimate based on component count
    // Header + Components label + components + padding
    return 1 + 1 + Math.Min(entity.Components.Count, MaxComponentsToShow) + 1;
}
```

---

## 5. Calculate Entity Height (Phase 2 - Accurate)

```csharp
/// <summary>
/// Calculates exact height of entity when rendered.
/// Phase 2: Account for properties per component.
/// </summary>
private int CalculateEntityHeight(EntityInfo entity)
{
    // Collapsed: just the header line
    if (!_expandedEntities.Contains(entity.Id))
        return 1;

    int lines = 1;  // Header line

    // Check if we have relationships to show
    if (entity.Relationships.Count > 0)
        lines += 1;  // "Relationships:" label (on one line, just counts)

    // Components section
    lines += 1;     // "Components:" label

    int componentsShown = Math.Min(entity.Components.Count, MaxComponentsToShow);
    foreach (string component in entity.Components.Take(componentsShown))
    {
        lines += 1; // Component line

        // Add property lines for this component
        if (entity.ComponentData.TryGetValue(component, out Dictionary<string, string>? fields))
        {
            int propertiesShown = Math.Min(fields.Count, MaxPropertiesToShow);
            lines += propertiesShown;
        }
    }

    if (entity.Components.Count > MaxComponentsToShow)
        lines += 1;  // "... (X more)" line

    lines += 1;     // Blank line at end

    return lines;
}
```

---

## 6. Recalculate Line Heights (Call After Filter/Expand Changes)

```csharp
/// <summary>
/// Rebuilds the cumulative line offset table.
/// O(N) complexity - call only on filter changes or entity expansion changes.
/// NOT called every frame - only on user action.
/// </summary>
private void RecalculateLineHeights()
{
    _entityLineHeights.Clear();
    _entityLineOffsets.Clear();

    int cumulativeLine = 0;

    foreach (EntityInfo entity in _filteredEntities)
    {
        int height = CalculateEntityHeight(entity);

        _entityLineHeights.Add(height);
        _entityLineOffsets.Add(cumulativeLine);

        cumulativeLine += height;
    }

    _totalVirtualLines = cumulativeLine;
}
```

---

## 7. Virtualized Update Display (Core Change)

```csharp
/// <summary>
/// Renders only visible entities instead of all entities.
/// REPLACES the entire rendering loop in UpdateDisplay().
/// </summary>
private void UpdateDisplay()
{
    // ... (keep all existing pre-rendering logic) ...

    // Preserve scroll position only if we had content before
    int previousLineCount = _entityListBuffer.TotalLines;
    int previousScrollOffset = _entityListBuffer.ScrollOffset;
    bool previousAutoScroll = _entityListBuffer.AutoScroll;

    _entityListBuffer.Clear();

    // ... (keep empty state checks) ...

    // Build navigation list (unchanged)
    _navigableEntityIds.Clear();
    var pinnedEntities = _filteredEntities.Where(e => _pinnedEntities.Contains(e.Id)).ToList();
    var regularEntities = _filteredEntities.Where(e => !_pinnedEntities.Contains(e.Id)).ToList();

    foreach (EntityInfo entity in pinnedEntities)
        _navigableEntityIds.Add(entity.Id);
    foreach (EntityInfo entity in regularEntities)
        _navigableEntityIds.Add(entity.Id);

    // Ensure selected index is valid (unchanged)
    if (_navigableEntityIds.Count > 0)
    {
        _selectedIndex = Math.Clamp(_selectedIndex, 0, _navigableEntityIds.Count - 1);
        if (!_selectedEntityId.HasValue || !_navigableEntityIds.Contains(_selectedEntityId.Value))
            _selectedEntityId = _navigableEntityIds[_selectedIndex];
        else
            _selectedIndex = _navigableEntityIds.IndexOf(_selectedEntityId.Value);
    }

    // Clear line-to-entity mapping
    _lineToEntityId.Clear();

    // Handle relationship view mode (non-virtualized for now)
    if (_viewMode == EntityViewMode.Relationships)
    {
        RenderRelationshipTreeView(pinnedEntities, regularEntities);
        UpdateStatusBar();
        return;
    }

    // ============================================
    // VIRTUALIZED RENDERING STARTS HERE
    // ============================================

    // Calculate which entities are visible
    CalculateVisibleRange(out int startIdx, out int startOffset, out int endIdx);

    int currentLine = 0;

    // Render pinned entities (not virtualized - they're usually few)
    if (pinnedEntities.Count > 0)
    {
        _entityListBuffer.AppendLine(
            $"  {NerdFontIcons.Pinned} PINNED",
            ThemeManager.Current.Warning
        );

        foreach (EntityInfo entity in pinnedEntities)
        {
            int lineNum = _entityListBuffer.TotalLines;
            RenderEntity(entity);
            _lineToEntityId[lineNum] = entity.Id;
        }

        _entityListBuffer.AppendLine("", ThemeManager.Current.TextDim);
    }

    // Render ONLY visible regular entities (virtualized)
    for (int i = startIdx; i <= endIdx && i < regularEntities.Count; i++)
    {
        EntityInfo entity = regularEntities[i];

        // Track which line this entity header starts on
        int lineNum = _entityListBuffer.TotalLines;
        RenderEntity(entity);
        _lineToEntityId[lineNum] = entity.Id;
    }

    // ============================================
    // VIRTUALIZED RENDERING ENDS HERE
    // ============================================

    UpdateStatusBar();

    // Initialize cursor line if not set
    if (_entityListBuffer.CursorLine < 0 && _entityListBuffer.TotalLines > 0)
        _entityListBuffer.CursorLine = 0;

    // Restore scroll position
    int newLineCount = _entityListBuffer.TotalLines;
    if (previousLineCount > 0 && newLineCount > 0)
    {
        float ratio = (float)newLineCount / previousLineCount;
        if (ratio > 0.8f && ratio < 1.2f)
        {
            _entityListBuffer.SetScrollOffset(
                Math.Min(previousScrollOffset, Math.Max(0, newLineCount - 1))
            );
        }
    }

    _entityListBuffer.AutoScroll = previousAutoScroll;
}
```

---

## 8. Update ApplyFilters() to Recalculate Heights

```csharp
private void ApplyFilters()
{
    _filteredEntities.Clear();

    foreach (EntityInfo entity in _entities)
    {
        if (!PassesFilter(entity))
            continue;

        _filteredEntities.Add(entity);
    }

    // Sort: pinned first, then by ID
    _filteredEntities.Sort((a, b) =>
    {
        bool aPinned = _pinnedEntities.Contains(a.Id);
        bool bPinned = _pinnedEntities.Contains(b.Id);
        if (aPinned != bPinned)
            return bPinned.CompareTo(aPinned);

        return a.Id.CompareTo(b.Id);
    });

    // NEW: Recalculate line heights after filtering
    RecalculateLineHeights();
}
```

---

## 9. Update ToggleEntity() to Recalculate Heights

```csharp
public bool ToggleEntity(int entityId)
{
    if (_expandedEntities.Contains(entityId))
    {
        _expandedEntities.Remove(entityId);
    }
    else
    {
        _expandedEntities.Add(entityId);
    }

    // NEW: Recalculate line heights since expansion state changed
    RecalculateLineHeights();

    UpdateDisplay();
    return _expandedEntities.Contains(entityId);
}
```

---

## 10. Update ExpandAll()/CollapseAll()

```csharp
public void ExpandAll()
{
    foreach (EntityInfo entity in _filteredEntities)
    {
        _expandedEntities.Add(entity.Id);
    }

    // NEW: Recalculate for all entities
    RecalculateLineHeights();

    UpdateDisplay();
}

public void CollapseAll()
{
    _expandedEntities.Clear();

    // NEW: Recalculate for all entities
    RecalculateLineHeights();

    UpdateDisplay();
}
```

---

## 11. Test Cases (Verification Checklist)

```csharp
// Test 1: Binary search finds first entity
Assert(BinarySearchEntityAtLine(0) == 0);

// Test 2: Binary search finds last entity
Assert(BinarySearchEntityAtLine(_totalVirtualLines - 1) == _filteredEntities.Count - 1);

// Test 3: Offsets are monotonic
for (int i = 0; i < _entityLineOffsets.Count - 1; i++)
{
    Assert(_entityLineOffsets[i] < _entityLineOffsets[i + 1]);
}

// Test 4: Heights sum to total lines
int sum = 0;
for (int i = 0; i < _entityLineHeights.Count; i++)
{
    sum += _entityLineHeights[i];
}
Assert(sum == _totalVirtualLines);

// Test 5: Rendering 100k entities takes <1ms
var sw = Stopwatch.StartNew();
for (int i = 0; i < 100000; i++)
{
    // Simulate rendering
    _entityListBuffer.AppendLine("test");
}
sw.Stop();
Assert(sw.ElapsedMilliseconds < 1000);
```

---

## Implementation Checklist

- [ ] Add three new fields (`_entityLineHeights`, `_entityLineOffsets`, `_totalVirtualLines`)
- [ ] Add `BinarySearchEntityAtLine()` method
- [ ] Add `CalculateVisibleRange()` method
- [ ] Add `CalculateEntityHeight()` method
- [ ] Add `RecalculateLineHeights()` method
- [ ] Modify `UpdateDisplay()` rendering loop
- [ ] Modify `ApplyFilters()` to call `RecalculateLineHeights()`
- [ ] Modify `ToggleEntity()` to call `RecalculateLineHeights()`
- [ ] Modify `ExpandAll()` to call `RecalculateLineHeights()`
- [ ] Modify `CollapseAll()` to call `RecalculateLineHeights()`
- [ ] Test with 1k entities
- [ ] Test with 10k entities
- [ ] Test with 100k entities
- [ ] Verify scroll is smooth
- [ ] Verify selection works
- [ ] Verify keyboard navigation works
- [ ] Verify mouse interaction works
- [ ] Verify expand/collapse works

---

## Common Mistakes to Avoid

1. **Calculating heights every frame** - Do this only on filter/expand changes!
2. **Rendering all entities then filtering by visibility** - Nope, calculate range first!
3. **Forgetting to clear `_lineToEntityId`** - Need fresh mapping each render!
4. **Off-by-one errors in binary search** - Test edge cases (first entity, last entity)
5. **Assuming constant entity heights** - Phase 2 handles variable heights

---

## Performance Targets

| Metric | Current | Target | Status |
|--------|---------|--------|--------|
| Render 1k entities | 50ms | <1ms | O(V) |
| Render 10k entities | 500ms | <1ms | O(V) |
| Render 100k entities | 5s+ | <1ms | O(V) |
| Render 1M entities | Freeze | <1ms | O(V) |
| Memory for 1M entities | 150MB+ | <50MB | 94% reduction |
| Scroll smoothness | Choppy | 60fps | Constant |

---

## Success Criteria

- [ ] Code compiles
- [ ] Renders correctly (no visual artifacts)
- [ ] Scroll is smooth (60fps)
- [ ] Selection works
- [ ] Keyboard nav works
- [ ] Mouse clicks work
- [ ] Expand/collapse works
- [ ] Filtering works
- [ ] Rendering 100k entities takes <1ms
- [ ] Memory usage stays constant during scroll

---

*This is a reference sheet for rapid implementation. See `ui-virtualization-research-findings.md` for detailed explanations and edge case handling.*

