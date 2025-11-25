# Console System TODO - Feature Parity & Tab Enhancements

**Last Updated:** November 25, 2025
**Status:** Tracking remaining features for complete console system

---

## 📊 Overall Status

| Tab | Core Features | Status |
|-----|---------------|--------|
| **Console** | Command execution, C# scripting | 🟡 85% Complete |
| **Watch** | Real-time monitoring | ✅ 100% Complete |
| **Logs** | Log viewing & filtering | 🟡 70% Complete |
| **Variables** | Script variable inspection | 🟢 60% Complete |

---

## 🎮 Console Tab - Missing Features

### ✅ Already Implemented
- [x] Multi-line text editor with dynamic height
- [x] Line numbers for multi-line mode
- [x] Syntax highlighting (C# keywords, types, strings, etc.)
- [x] Command history navigation (Up/Down, Ctrl+R for search)
- [x] Auto-completion with suggestions dropdown
- [x] Parameter hints
- [x] Documentation viewer
- [x] Output search (Ctrl+F)
- [x] Command bookmarks (F-key shortcuts)
- [x] Alias/macro system
- [x] Script management
- [x] Enter = submit, Shift+Enter = new line
- [x] Text selection (visual highlighting)
- [x] Scrolling in output buffer
- [x] Hover effects

---

### 🔴 HIGH PRIORITY - Essential Editing

#### 1. **Clipboard Operations** ⭐ CRITICAL
```
Status: NOT IMPLEMENTED
Complexity: Medium (2-4 hours)
```

**Missing:**
- [ ] Copy (Ctrl+C) - Copy selected text
- [ ] Cut (Ctrl+X) - Cut selected text
- [ ] Paste (Ctrl+V) - Paste from clipboard
- [ ] Copy entire line if no selection

**Why Critical:** Users expect this everywhere. Without it, editing is painful.

**Implementation Notes:**
- Use `TextCopy` library (already in project)
- Handle multi-line text properly
- Preserve formatting when pasting

---

#### 2. **Undo/Redo** ⭐ CRITICAL
```
Status: NOT IMPLEMENTED (infrastructure exists)
Complexity: Medium (3-5 hours)
```

**Missing:**
- [ ] Undo (Ctrl+Z) - Revert last change
- [ ] Redo (Ctrl+Y or Ctrl+Shift+Z) - Restore undone change
- [ ] Integrate with `TextEditor`

**Why Critical:** Prevents accidental loss of work, essential for productivity.

**Implementation Notes:**
- `UndoRedoStack` class already exists in old console
- Can be adapted or rewritten for new `TextEditor`
- Track text changes, cursor position, selection

---

#### 3. **Word Navigation** ⭐ HIGH
```
Status: NOT IMPLEMENTED
Complexity: Low (1-2 hours)
```

**Missing:**
- [ ] Ctrl+Left/Right - Jump between words
- [ ] Ctrl+Backspace - Delete word backward
- [ ] Ctrl+Delete - Delete word forward
- [ ] Smart word boundaries (stop at punctuation, camelCase)

**Why High:** Much faster editing of long commands.

**Implementation Notes:**
- Implement word boundary detection
- Consider CamelCase and snake_case as word separators
- Add to `TextEditor`'s input handling

---

#### 4. **Advanced Selection**
```
Status: PARTIAL (click-drag only)
Complexity: Medium (2-3 hours)
```

**Missing:**
- [ ] Shift+Arrow keys - Extend selection
- [ ] Shift+Home/End - Select to line start/end
- [ ] Shift+Ctrl+Arrow - Select by word
- [ ] Double-click - Select word
- [x] Click and drag (IMPLEMENTED)
- [x] Ctrl+A (IMPLEMENTED)

**Why High:** Keyboard selection is much faster for power users.

---

### 🟡 MEDIUM PRIORITY - Enhanced UX

#### 5. **Bracket Matching**
```
Status: NOT IMPLEMENTED
Complexity: Low (1-2 hours)
```

**Missing:**
- [ ] Highlight matching brackets when cursor is near one
- [ ] Visual indicator for bracket pairs: `()`, `[]`, `{}`
- [ ] Flash highlight when bracket is typed

**Why Medium:** Helps with complex nested expressions.

**Implementation Notes:**
- Add to `TextEditor` rendering
- Use `UITheme` colors for bracket highlighting

---

#### 6. **Output Formatting & Sections**
```
Status: BASIC IMPLEMENTED
Complexity: Medium (3-4 hours)
```

**Missing:**
- [ ] Group output by command execution
- [ ] Collapsible sections
- [ ] Visual separators between commands
- [ ] Timestamp per command
- [ ] Output line numbers (optional toggle)

**Why Medium:** Organizes output from multiple commands, easier to navigate.

---

#### 7. **Font Size Controls**
```
Status: NOT IMPLEMENTED
Complexity: Low (1-2 hours)
```

**Missing:**
- [ ] Ctrl+Plus - Increase font size
- [ ] Ctrl+Minus - Decrease font size
- [ ] Ctrl+0 - Reset to default
- [ ] Persist preference to disk

**Why Medium:** Accessibility and personal preference.

---

### 🟢 LOW PRIORITY - Nice to Have

#### 8. **Smart Code Features**
```
Status: NOT IMPLEMENTED
Complexity: High (5-8 hours)
```

**Missing:**
- [ ] Auto-close brackets/quotes
- [ ] Auto-indent multi-line code
- [ ] Snippet expansion (e.g., `for` -> `for (int i = 0; i < count; i++)`)
- [ ] Code formatting (Ctrl+Shift+F)

**Why Low:** Quality of life, but not essential for basic use.

---

#### 9. **Output Export**
```
Status: NOT IMPLEMENTED
Complexity: Low (1-2 hours)
```

**Missing:**
- [ ] Export output to file
- [ ] Copy all output to clipboard
- [ ] Export with or without colors
- [ ] Export selected output

**Why Low:** Useful for debugging sessions, but not frequently used.

---

#### 10. **Configurable Theme**
```
Status: PARTIAL (Dark theme only)
Complexity: Medium (3-4 hours)
```

**Missing:**
- [ ] Light theme preset
- [ ] High contrast theme preset
- [ ] Custom theme editor UI
- [ ] Save/load theme files
- [ ] Live theme preview
- [ ] Theme switching hotkey

**Why Low:** Current dark theme works well, not urgent.

---

## 📺 Watch Tab - Enhancements

### ✅ Fully Implemented Features
- [x] Real-time expression monitoring
- [x] Auto-update with configurable interval
- [x] Watch grouping and collapse/expand
- [x] Conditional watches (only evaluate when condition is true)
- [x] Watch history (track last 10 value changes)
- [x] Watch alerts/thresholds
- [x] Watch comparison (compare two expressions)
- [x] **Watch presets (save/load configurations)** ✨ NEW
- [x] Watch pinning (keep important ones at top)
- [x] Watch limit (max 50 watches)

---

### 🔴 HIGH PRIORITY Enhancements

#### 1. **Threaded Watch Evaluation** ⭐ PERFORMANCE CRITICAL
```
Status: NOT IMPLEMENTED
Complexity: High (6-10 hours)
Priority: HIGH
```

**Missing:**
- [ ] Move watch expression evaluation to background thread
- [ ] Thread-safe result queue/callback mechanism
- [ ] Main thread synchronization for game state access
- [ ] Cancellation support for long-running evaluations
- [ ] Error handling for thread exceptions
- [ ] Optional: Mark expressions as "main thread only" (for non-thread-safe APIs)

**Why High:** Expensive watch evaluations currently block the game thread, causing frame drops. With many watches or complex objects, this significantly impacts performance.

**Design Considerations:**
- **Challenge:** MonoGame/game objects may not be thread-safe
- **Solution 1:** Evaluate on background thread, queue results back to main thread
- **Solution 2:** Capture snapshot of data on main thread, evaluate snapshot on background thread
- **Solution 3:** Hybrid - simple watches on background thread, complex/game-state watches on main thread

**Implementation Notes:**
```csharp
// Option 1: Background thread with result queue
class WatchEvaluator
{
    private readonly Thread _workerThread;
    private readonly ConcurrentQueue<WatchEvaluation> _pendingEvaluations;
    private readonly ConcurrentQueue<WatchResult> _completedResults;

    public void QueueEvaluation(string expression, Func<object?> evaluator);
    public bool TryGetResult(out WatchResult result);
}

// Option 2: Task-based async evaluation
class AsyncWatchEvaluator
{
    public async Task<object?> EvaluateAsync(string expression, CancellationToken ct);
}
```

**Safety Measures:**
- Timeout for evaluations (5-10 seconds max)
- Cancellation on console close/watch remove
- Exception isolation (don't crash on evaluation error)
- Queue size limits (prevent memory issues)

---

### 🟡 MEDIUM Priority Enhancements

#### 2. **Watch Value Visualization**
```
Status: NOT IMPLEMENTED
Complexity: Medium (4-6 hours)
Priority: MEDIUM
```

**Ideas:**
- [ ] Mini sparkline graphs for numeric values
- [ ] Value trend indicators (↑ increasing, ↓ decreasing, → stable)
- [ ] Color gradient based on value magnitude
- [ ] Visual diff highlighting for changed values

**Why Medium:** Makes it easier to spot trends at a glance.

---

#### 2. **Watch Export/Import**
```
Status: PARTIAL (presets implemented)
Complexity: Low (1 hour)
Priority: LOW
```

**Missing:**
- [ ] Export watches to CSV
- [ ] Export watch history to CSV
- [ ] Import watches from CSV
- [ ] Export current values snapshot

**Why Low:** Presets already cover most use cases.

---

#### 3. **Watch Performance Stats**
```
Status: NOT IMPLEMENTED
Complexity: Low (1-2 hours)
Priority: LOW
```

**Missing:**
- [ ] Show evaluation time per watch
- [ ] Highlight slow evaluations (> 10ms)
- [ ] Total time spent on watch updates per frame
- [ ] Option to pause expensive watches

**Why Low:** Only useful if performance becomes an issue.

---

## 📋 Logs Tab - Missing Features

### ✅ Currently Implemented
- [x] Log message display with timestamps
- [x] Log level filtering (Trace, Debug, Info, Warning, Error, Critical)
- [x] Log category display
- [x] Text search/filtering
- [x] Auto-scroll toggle
- [x] Clear logs command

---

### 🔴 HIGH PRIORITY

#### 1. **Log Category Filtering**
```
Status: NOT IMPLEMENTED
Complexity: Low (1-2 hours)
Priority: HIGH
```

**Missing:**
- [ ] Filter by category (e.g., "Rendering", "Physics", "AI")
- [ ] Multi-select categories
- [ ] Category list dropdown
- [ ] Show category counts

**Why High:** Essential for focusing on specific subsystems.

**Implementation:**
```csharp
// Add to LogsPanel
public void SetCategoryFilter(string[]? categories);
public IEnumerable<string> GetAvailableCategories();
```

---

#### 2. **Log Timestamps & Time Filtering**
```
Status: PARTIAL (timestamps shown)
Complexity: Medium (2-3 hours)
Priority: HIGH
```

**Missing:**
- [ ] Filter by time range (last 1min, 5min, 1hour, etc.)
- [ ] Show time since previous log
- [ ] Time-based log grouping
- [ ] Custom time format settings

**Why High:** Useful for debugging time-sensitive issues.

---

### 🟡 MEDIUM PRIORITY

#### 3. **Log Export**
```
Status: NOT IMPLEMENTED
Complexity: Low (1-2 hours)
Priority: MEDIUM
```

**Missing:**
- [ ] Export logs to file
- [ ] Export with filters applied
- [ ] Auto-export on crash/error
- [ ] Export format options (plain text, JSON, CSV)

**Why Medium:** Useful for bug reports and analysis.

---

#### 4. **Log Highlighting & Patterns**
```
Status: NOT IMPLEMENTED
Complexity: Medium (3-4 hours)
Priority: MEDIUM
```

**Missing:**
- [ ] Highlight specific patterns (e.g., error codes, IDs)
- [ ] Custom highlight rules
- [ ] Regex pattern matching
- [ ] Color-code by pattern

**Why Medium:** Makes important logs stand out.

---

### 🟢 LOW PRIORITY

#### 5. **Log Statistics**
```
Status: NOT IMPLEMENTED
Complexity: Low (1-2 hours)
Priority: LOW
```

**Missing:**
- [ ] Log count by level
- [ ] Log count by category
- [ ] Logs per second rate
- [ ] Error/warning rate over time

**Why Low:** Nice to have, but not essential.

---

#### 6. **Log Bookmarks**
```
Status: NOT IMPLEMENTED
Complexity: Low (1-2 hours)
Priority: LOW
```

**Missing:**
- [ ] Bookmark important log entries
- [ ] Jump to bookmarked entries
- [ ] Add notes to bookmarks
- [ ] Export bookmarks

**Why Low:** Rarely needed with search functionality.

---

## 🔍 Variables Tab - Missing Features

### ✅ Currently Implemented
- [x] Display script-defined variables
- [x] Display built-in globals (Player, Game, World, etc.)
- [x] Show variable types
- [x] Show variable values with formatting
- [x] Collapsible variable groups
- [x] Auto-refresh on changes

---

### 🔴 HIGH PRIORITY

#### 1. **Variable Inspection**
```
Status: PARTIAL
Complexity: Medium (3-5 hours)
Priority: HIGH
```

**Missing:**
- [ ] Expand complex objects (show properties/fields)
- [ ] Navigate object hierarchy
- [ ] Expand collections (arrays, lists, dictionaries)
- [ ] Show collection count/size
- [ ] Recursive expansion with depth limit

**Why High:** Essential for debugging complex objects.

**Implementation:**
```csharp
// Add to VariablesPanel
public void ExpandVariable(string name);
public void CollapseVariable(string name);
public void SetExpansionDepth(int maxDepth);
```

---

#### 2. **Variable Editing**
```
Status: NOT IMPLEMENTED
Complexity: Medium (4-6 hours)
Priority: HIGH
```

**Missing:**
- [ ] Edit variable values inline
- [ ] Type validation
- [ ] Undo/Redo for edits
- [ ] Confirmation for dangerous edits
- [ ] Edit nested properties

**Why High:** Very useful for testing and tweaking values on the fly.

**Implementation:**
```csharp
// Add to VariablesPanel
public bool TrySetVariable(string name, string valueString);
public bool CanEditVariable(string name);
```

---

### 🟡 MEDIUM PRIORITY

#### 3. **Variable Search**
```
Status: NOT IMPLEMENTED
Complexity: Low (1-2 hours)
Priority: MEDIUM
```

**Missing:**
- [ ] Search by variable name
- [ ] Search by value
- [ ] Search by type
- [ ] Filter by namespace/category

**Why Medium:** Useful when there are many variables.

---

#### 4. **Variable Favorites/Pinning**
```
Status: NOT IMPLEMENTED
Complexity: Low (1-2 hours)
Priority: MEDIUM
```

**Missing:**
- [ ] Pin important variables to top
- [ ] Create favorites list
- [ ] Persist favorites between sessions
- [ ] Quick access to favorites

**Why Medium:** Faster access to frequently inspected variables.

---

### 🟢 LOW PRIORITY

#### 5. **Variable History**
```
Status: NOT IMPLEMENTED
Complexity: Medium (3-4 hours)
Priority: LOW
```

**Missing:**
- [ ] Track variable value changes over time
- [ ] Show change history
- [ ] Visualize value timeline
- [ ] Export history

**Why Low:** Watch tab already provides this functionality.

---

#### 6. **Variable Comparison**
```
Status: NOT IMPLEMENTED
Complexity: Medium (2-3 hours)
Priority: LOW
```

**Missing:**
- [ ] Compare two variables side-by-side
- [ ] Show differences
- [ ] Compare snapshots over time

**Why Low:** Nice to have, but rarely used.

---

## 🎯 Recommended Implementation Order

### Sprint 1: Console Essentials (3-5 days)
**Goal:** Match old console core functionality

1. **Clipboard Operations** ⭐ (Day 1)
   - Most requested, users expect this everywhere

2. **Undo/Redo** ⭐ (Day 1-2)
   - Critical for productivity, infrastructure exists

3. **Word Navigation** (Day 2)
   - Natural extension, quick win

4. **Advanced Selection** (Day 3)
   - Completes the editing experience

---

### Sprint 2: Logs Tab Essentials (2-3 days)
**Goal:** Make logs tab production-ready

1. **Log Category Filtering** (Day 1)
   - Essential for debugging specific systems

2. **Log Time Filtering** (Day 1-2)
   - Useful for time-sensitive issues

3. **Log Export** (Day 2)
   - Bug reporting and analysis

---

### Sprint 3: Variables Tab Core (3-4 days)
**Goal:** Make variables tab useful for debugging

1. **Variable Inspection** (Day 1-2)
   - Expand objects and collections

2. **Variable Editing** (Day 2-3)
   - Edit values on the fly

3. **Variable Search** (Day 3-4)
   - Find variables quickly

---

### Sprint 4: Performance & Threading (2-3 days)
**Goal:** Improve watch system performance

1. **Threaded Watch Evaluation** ⭐ (Day 1-3)
   - Move expensive evaluations off main thread
   - Prevent frame drops from complex watches
   - Critical for performance with many watches

---

### Sprint 5: Polish & Enhancements (2-3 days)
**Goal:** Nice-to-haves and UX improvements

1. **Bracket Matching** (Day 1)
   - Visual aid for code editing

2. **Font Size Controls** (Day 1)
   - Accessibility

3. **Output Sections** (Day 2)
   - Better command organization

4. **Log Highlighting** (Day 2-3)
   - Make important logs stand out

---

## 📝 Implementation Notes

### General Principles
- ✅ Use existing `UITheme` for all colors
- ✅ Follow established component patterns
- ✅ Maintain clean separation of concerns
- ✅ Write tests for complex features
- ✅ Update help documentation
- ✅ Add keyboard shortcuts to hint bars

### Code Organization
- Console editing features → `TextEditor.cs`, `CommandInput.cs`
- Watch features → `WatchPanel.cs`
- Logs features → `LogsPanel.cs`
- Variables features → `VariablesPanel.cs`
- Commands → `PokeSharp.Engine.Debug/Commands/BuiltIn/`

### Performance Considerations
- Use dirty flags for expensive operations
- Cache rendered text where possible
- Limit collection expansion depth
- Throttle variable updates
- Use lazy evaluation for complex objects

---

## 🎉 What's Already Great!

### Console Tab ✅
- Multi-line editing with line numbers
- Syntax highlighting
- Command history with search
- Auto-completion
- Parameter hints
- Documentation viewer
- Output search
- Bookmarks, aliases, scripts

### Watch Tab ✅
- **100% feature-complete!**
- Real-time monitoring
- Groups, conditions, history
- Alerts, comparisons, presets
- Professional debugging tool

### Logs Tab ✅
- Basic log viewing
- Level filtering
- Text search
- Auto-scroll toggle

### Variables Tab ✅
- Variable display
- Type information
- Value formatting
- Grouping

---

## 🤔 Questions for Prioritization

1. **What do you use most often?**
   - Clipboard? Undo? Word navigation?

2. **What's the most annoying thing missing right now?**
   - Let this guide priority

3. **Which tab do you use most?**
   - Console for commands?
   - Watch for monitoring?
   - Logs for debugging?
   - Variables for inspection?

4. **Are there features you never use?**
   - Can skip those entirely

---

## 📊 Summary Statistics

**Total Features:**
- ✅ Implemented: 45
- 🔴 High Priority: 13 (includes Threaded Watch Evaluation)
- 🟡 Medium Priority: 14
- 🟢 Low Priority: 11

**Estimated Implementation Time:**
- High Priority: 26-40 hours (1.5-2 weeks)
- Medium Priority: 25-35 hours (1.5-2 weeks)
- Low Priority: 15-20 hours (1 week)
- **Total:** 66-95 hours (3-5 weeks of focused work)

---

**The console system is already very capable! The watch system is 100% complete, and core functionality works great. These TODOs are for reaching 100% parity and adding polish.** 🚀

