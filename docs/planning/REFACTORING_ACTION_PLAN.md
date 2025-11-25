# Refactoring Action Plan - Updated Post-Console Removal

**Date:** November 22, 2025
**Last Updated:** After old console removal
**Status:** Ready for implementation

---

## 🎯 Progress Update

### ✅ COMPLETED: Critical Issue #3 - Duplicate Console Systems
**Status:** **FULLY RESOLVED** 🎉

We successfully:
- ✅ Removed old `QuakeConsole` implementation (12 UI files deleted)
- ✅ Removed old `ConsoleScene` (1 file deleted)
- ✅ Removed old console services (6 files deleted)
- ✅ Cleaned up `ConsoleSystem` (~323 lines removed)
- ✅ Updated DI registrations
- ✅ All builds passing

**Impact:**
- ~3,000 lines of code removed
- Single console implementation (no more confusion)
- Cleaner architecture
- Easier maintenance going forward

---

## 🔴 REMAINING CRITICAL ISSUES

### Issue #1: Dual Theme Systems (HIGHEST PRIORITY)

**Problem:** Two separate color/theme systems still exist:
1. `ConsoleColors.cs` (old static class) - 70+ color constants
2. `UITheme.cs` (new theme system) - Modern structured approach

**Current State:**
- ✅ Old console removed (no longer uses ConsoleColors directly for UI)
- ❌ `ConsoleColors` still used throughout `ConsoleSystem` for output colors
- ❌ `UITheme` used by new UI components
- ❌ Components still have hardcoded colors mixed with theme usage

**Files Affected:**
- `/PokeSharp.Engine.Debug/Console/Configuration/ConsoleColors.cs`
- `/PokeSharp.Engine.UI.Debug/Core/UITheme.cs`
- `/PokeSharp.Engine.Debug/Systems/ConsoleSystem.cs` (uses ConsoleColors)
- Various UI components (mix of both)

**Action Required:**
```csharp
// STEP 1: Extend UITheme with console-specific colors
// Location: PokeSharp.Engine.UI.Debug/Core/UITheme.cs

public class UITheme
{
    // Existing colors...

    // Add console semantic colors
    public Color ConsolePrimary { get; init; }
    public Color ConsoleSuccess { get; init; }
    public Color ConsoleError { get; init; }
    public Color ConsoleWarning { get; init; }
    public Color ConsoleInfo { get; init; }

    // Add console text colors
    public Color ConsoleTextPrimary { get; init; }
    public Color ConsoleTextSecondary { get; init; }
    public Color ConsoleTextDim { get; init; }

    // Add syntax highlighting
    public Color SyntaxKeyword { get; init; }
    public Color SyntaxString { get; init; }
    public Color SyntaxNumber { get; init; }
    public Color SyntaxComment { get; init; }
    public Color SyntaxType { get; init; }
    public Color SyntaxMethod { get; init; }
    public Color SyntaxOperator { get; init; }
    public Color SyntaxBracket { get; init; }

    // Update Dark theme preset
    public static UITheme Dark { get; } = new UITheme
    {
        // ... existing values ...

        // Console colors
        ConsolePrimary = new Color(100, 200, 255),
        ConsoleSuccess = new Color(150, 255, 150),
        ConsoleError = new Color(255, 100, 100),
        ConsoleWarning = new Color(255, 200, 100),
        ConsoleInfo = new Color(150, 200, 255),

        // Syntax highlighting
        SyntaxKeyword = new Color(100, 150, 255),
        SyntaxString = new Color(255, 180, 100),
        SyntaxNumber = new Color(180, 255, 180),
        SyntaxComment = new Color(100, 150, 100),
        SyntaxType = new Color(100, 255, 200),
        SyntaxMethod = new Color(255, 255, 150),
        SyntaxOperator = new Color(200, 200, 200),
        SyntaxBracket = new Color(255, 215, 0)
    };
}

// STEP 2: Create backward compatibility bridge
// Location: PokeSharp.Engine.Debug/Console/Configuration/ConsoleColors.cs

public static class ConsoleColors
{
    private static UITheme Theme => UITheme.Dark;

    // Bridge old names to new theme (mark as obsolete)
    [Obsolete("Use UITheme.ConsolePrimary instead")]
    public static Color Primary => Theme.ConsolePrimary;

    [Obsolete("Use UITheme.ConsoleSuccess instead")]
    public static Color Success => Theme.ConsoleSuccess;

    [Obsolete("Use UITheme.ConsoleError instead")]
    public static Color Error => Theme.ConsoleError;

    // ... etc for all 70+ colors
}

// STEP 3: Update ConsoleSystem to use theme
// Location: PokeSharp.Engine.Debug/Systems/ConsoleSystem.cs

// OLD:
using static PokeSharp.Engine.Debug.Console.Configuration.ConsoleColors;
_consoleScene?.AppendOutput("Success!", Success);

// NEW:
_consoleScene?.AppendOutput("Success!", _consoleScene.Theme.ConsoleSuccess);
// OR pass theme reference to console system
```

**Estimated Effort:** 4-6 hours
**Priority:** 🔴 Critical - Do this first

---

### Issue #2: Hardcoded Colors in Components (HIGH PRIORITY)

**Problem:** Components still hardcode colors instead of using theme.

**Files with Hardcoded Colors:**

1. **ConsolePanel.cs**
```csharp
// Line 55
BackgroundColor = new Color(20, 20, 30, 240),  // HARDCODED!

// Line 68
BackgroundColor = new Color(15, 15, 25, 255),  // HARDCODED!

// Line 82
BackgroundColor = new Color(25, 25, 35, 255),  // HARDCODED!

// Line 165
OffsetX = -20,  // Magic number
```

2. **CommandInput.cs**
```csharp
// Lines 43-54 - Defaults from UITheme.Dark (better, but still static)
public Color BackgroundColor { get; set; } = UITheme.Dark.InputBackground;
public Color TextColor { get; set; } = UITheme.Dark.InputText;
public Color CursorColor { get; set; } = UITheme.Dark.InputCursor;
```

3. **NewConsoleScene.cs**
```csharp
// Line 189 (if still exists)
AppendOutput("=== PokeSharp Debug Console ===", new Color(100, 200, 255));  // HARDCODED!
```

**Action Required:**

```csharp
// BEFORE (BAD)
BackgroundColor = new Color(20, 20, 30, 240);

// AFTER (GOOD)
BackgroundColor = Theme.BackgroundPrimary;

// OR for components: Don't store color, read from theme each render
protected override void OnRender(UIContext context)
{
    var bgColor = Theme.BackgroundPrimary;
    Renderer.DrawRectangle(Rect, bgColor);
}
```

**Files to Update:**
- `ConsolePanel.cs` - 3 instances
- `CommandInput.cs` - 6 property defaults
- `NewConsoleScene.cs` - 1 instance
- `ConsoleSystem.cs` - All `AppendOutput` calls with hardcoded colors

**Estimated Effort:** 2-3 hours
**Priority:** 🟡 High - Do after Issue #1

---

### Issue #3: Magic Numbers (MEDIUM PRIORITY)

**Problem:** Numeric values scattered without named constants.

**Examples Found:**
```csharp
// ConsolePanel.cs
private const float InputMinHeight = 35f;  // Why 35?
private const float SuggestionsMaxHeight = 300f;  // Why 300?
private const float Padding = 10f;  // Should be in theme

// Line 813
var calculatedHeight = itemCount * (int)_suggestionsDropdown.ItemHeight + 20; // Magic 20!

// Line 660
_parameterHints.Constraint.OffsetY = -(inputHeight + hintHeight + 5); // Magic 5!

// Line 165
OffsetX = -20, // Magic 20!
```

**Action Required:**

Add to `UITheme`:
```csharp
public class UITheme
{
    // ... existing ...

    // Spacing/Gaps
    public float TooltipGap { get; init; } = 5f;
    public float ComponentGap { get; init; } = 10f;
    public float PanelEdgeGap { get; init; } = 20f;

    // Component sizes
    public float MinInputHeight { get; init; } = 35f;
    public float MaxSuggestionsHeight { get; init; } = 300f;

    // Interactive thresholds
    public float DragThreshold { get; init; } = 5f;
    public float DoubleClickMaxDistance { get; init; } = 5f;
}

// Usage
_parameterHints.Constraint.OffsetY = -(inputHeight + hintHeight + Theme.TooltipGap);
```

**Files to Update:**
- `ConsolePanel.cs` - ~5 instances
- `ConsoleInputHandler.cs` (if any remain after old console removal)

**Estimated Effort:** 1-2 hours
**Priority:** 🟢 Medium

---

## 🟡 HIGH PRIORITY ISSUES

### Issue #4: Inconsistent Component APIs

**Problem:** Similar components have different method names/signatures for same functionality.

**Examples:**
```csharp
// Setting text
CommandInput.SetText(string text)
TextEditor.CompleteText(string text)  // Should be SetText?
TextBuffer.AppendLine(string text, Color color, string category)  // Different!

// Events
ConsolePanel.OnCommandSubmitted  // Action<string>
TextEditor.OnTextChanged  // Different event type?
```

**Action Required:**

Create standard interfaces:
```csharp
// PokeSharp.Engine.UI.Debug/Core/ITextInput.cs
public interface ITextInput
{
    string Text { get; }
    void SetText(string text);
    void Clear();
    void Focus();

    event Action<string>? OnTextChanged;
    event Action<string>? OnSubmit;
}

// PokeSharp.Engine.UI.Debug/Core/ITextDisplay.cs
public interface ITextDisplay
{
    void AppendLine(string text);
    void AppendLine(string text, Color color);
    void Clear();
    void ScrollToBottom();
}

// Then implement
public class CommandInput : UIComponent, ITextInput { ... }
public class TextEditor : UIComponent, ITextInput { ... }
public class TextBuffer : UIComponent, ITextDisplay { ... }
```

**Estimated Effort:** 3-4 hours
**Priority:** 🟡 High

---

### Issue #5: State Management Complexity

**Problem:** Multiple boolean flags track overlapping states.

**Current State (ConsolePanel.cs):**
```csharp
private bool _showSuggestions = false;
private bool _showSearch = false;
private bool _showParameterHints = false;
private bool _showDocumentation = false;
private bool _isCompletingText = false;
private bool _closeRequested = false;
```

**Action Required:**

```csharp
// Create state enum
public enum ConsoleOverlayMode
{
    None,           // No overlay showing
    Suggestions,    // Auto-complete suggestions
    Search,         // Search bar
    Documentation   // Documentation popup
}

public class ConsolePanel : Panel
{
    private ConsoleOverlayMode _overlayMode = ConsoleOverlayMode.None;
    private bool _isCompletingText = false;  // OK - different concern
    private bool _closeRequested = false;    // OK - different concern

    // Clean state checks
    public bool IsShowingSuggestions => _overlayMode == ConsoleOverlayMode.Suggestions;
    public bool IsShowingSearch => _overlayMode == ConsoleOverlayMode.Search;

    // Mutually exclusive operations
    public void ShowSuggestions()
    {
        _overlayMode = ConsoleOverlayMode.Suggestions;
        // Can't show search and suggestions at same time - enforced!
    }
}
```

**Estimated Effort:** 1-2 hours
**Priority:** 🟡 High

---

### Issue #6: UI Architecture - Immediate vs Retained Mode

**Problem:** Framework mixes immediate mode (resolve layout every frame) with retained mode (store children).

**Current State:**
```csharp
// Immediate: Layout resolved every frame
public void Render(UIContext context)
{
    Rect = LayoutResolver.Resolve(Constraint, context.CurrentContainer.ContentRect, GetContentSize());
    // ...
}

// Retained: Children stored in list
protected override void OnRenderContainer(UIContext context)
{
    foreach (var child in _children)  // Retained structure
    {
        child.Render(context);
    }
}
```

**Decision Required:** Pick ONE approach

**Recommendation:** Keep current **hybrid** approach but document it clearly:
- **Retained structure** (store component tree) for flexibility
- **Immediate layout** (resolve per-frame) for simplicity
- **Cached layout** (add dirty flag) for performance

**Action Required:**

```csharp
public abstract class UIComponent
{
    private bool _layoutDirty = true;
    private Rectangle _cachedRect;

    public void Render(UIContext context)
    {
        // Only recalculate if layout is dirty
        if (_layoutDirty || ConstraintChanged)
        {
            _cachedRect = LayoutResolver.Resolve(Constraint, context.CurrentContainer.ContentRect, GetContentSize());
            _layoutDirty = false;
        }

        Rect = _cachedRect;
        // ...
    }

    protected void InvalidateLayout()
    {
        _layoutDirty = true;
    }
}
```

**Estimated Effort:** 2-3 hours (+ documentation)
**Priority:** 🟡 High

---

## 🟢 MEDIUM PRIORITY ISSUES

### Issue #7: Overly Large Classes

**Problem:** `ConsoleSystem.cs` still ~850 lines doing multiple responsibilities.

**Current Responsibilities:**
- Console lifecycle management
- Command execution
- Auto-completion coordination
- Parameter hint coordination
- Documentation generation
- Logging command handling
- Script loading
- Event handling

**Action Required:**

Split into focused classes:
```csharp
// Keep: ConsoleSystem.cs (~200 lines)
public class ConsoleSystem : IUpdateSystem
{
    public void Initialize(World world) { ... }
    public void Update(World world, float deltaTime) { ... }
    public void ToggleConsole() { ... }
}

// New: ConsoleCommandDispatcher.cs
public class ConsoleCommandDispatcher
{
    private readonly Dictionary<string, IConsoleCommand> _commands;

    public Task ExecuteAsync(string commandText) { ... }
}

// New: ConsoleCompletionProvider.cs
public class ConsoleCompletionProvider
{
    public Task<List<SuggestionItem>> GetCompletionsAsync(string text, int cursor) { ... }
}

// New: ConsoleDocumentationProvider.cs
public class ConsoleDocumentationProvider
{
    public DocInfo GenerateDocumentation(string symbol) { ... }
}

// New: Individual command classes
public class HelpCommand : IConsoleCommand { ... }
public class ClearCommand : IConsoleCommand { ... }
public class LoggingCommand : IConsoleCommand { ... }
// etc.
```

**Estimated Effort:** 4-6 hours
**Priority:** 🟢 Medium

---

### Issue #8: Input Handling Complexity

**Problem:** Input handling logic scattered (was worse before, but still needs improvement).

**Current State:**
- `InputState` - Base input tracking
- Component-level input handling in `OnRender`
- Event bubbling through `UIContext`

**Action Required:**

Document and simplify the input routing:
```csharp
// Create clear input priority system
public class UIContext
{
    private Stack<UIComponent> _inputStack = new();

    public void PushInputHandler(UIComponent component)
    {
        _inputStack.Push(component);
    }

    public void PopInputHandler(UIComponent component)
    {
        if (_inputStack.Peek() == component)
            _inputStack.Pop();
    }

    public bool HandleInput(InputState input)
    {
        // Process top of stack first (modal dialogs, etc.)
        foreach (var handler in _inputStack)
        {
            if (handler.HandleInput(input) == InputResult.Consumed)
                return true;  // Stop propagation
        }
        return false;
    }
}
```

**Estimated Effort:** 3-4 hours
**Priority:** 🟢 Medium

---

### Issue #9: String-Based Command System

**Problem:** Commands identified by strings, no type safety.

**Current State (ConsoleSystem.cs):**
```csharp
var cmd = parts[0].ToLower();
switch (cmd)
{
    case "help":
        // ...
    case "clear":
        // ...
    case "logging":
        // ...
    // etc.
}
```

**Action Required:**

```csharp
// Define command interface
public interface IConsoleCommand
{
    string Name { get; }
    string Description { get; }
    Task ExecuteAsync(IConsoleContext context, string[] args);
}

// Create command attribute
[AttributeUsage(AttributeTargets.Class)]
public class ConsoleCommandAttribute : Attribute
{
    public string Name { get; }
    public string Description { get; }

    public ConsoleCommandAttribute(string name, string description = "")
    {
        Name = name;
        Description = description;
    }
}

// Define commands
[ConsoleCommand("help", "Display available commands")]
public class HelpCommand : IConsoleCommand
{
    public string Name => "help";
    public string Description => "Display available commands";

    public Task ExecuteAsync(IConsoleContext context, string[] args)
    {
        context.WriteLine("Available commands:", context.Theme.ConsoleInfo);
        // ...
        return Task.CompletedTask;
    }
}

// Auto-discover commands
public class ConsoleCommandRegistry
{
    private readonly Dictionary<string, IConsoleCommand> _commands = new();

    public ConsoleCommandRegistry()
    {
        // Find all [ConsoleCommand] types via reflection
        var commandTypes = Assembly.GetExecutingAssembly()
            .GetTypes()
            .Where(t => t.GetCustomAttribute<ConsoleCommandAttribute>() != null);

        foreach (var type in commandTypes)
        {
            var command = (IConsoleCommand)Activator.CreateInstance(type)!;
            _commands[command.Name] = command;
        }
    }

    public async Task ExecuteAsync(string commandName, string[] args, IConsoleContext context)
    {
        if (_commands.TryGetValue(commandName.ToLower(), out var command))
        {
            await command.ExecuteAsync(context, args);
        }
        else
        {
            context.WriteLine($"Unknown command: {commandName}", context.Theme.ConsoleError);
        }
    }
}
```

**Estimated Effort:** 4-5 hours
**Priority:** 🟢 Medium

---

## 🟢 LOW PRIORITY / POLISH

### Issue #10: Missing Null Safety
- Add null checks consistently
- Use nullable reference types properly
- Add `required` properties where appropriate

**Estimated Effort:** 2 hours

---

### Issue #11: Inconsistent Naming Conventions
- Create `.editorconfig` with naming rules
- Fix inconsistent event naming (OnXxx vs Xxx)
- Enforce via analyzer

**Estimated Effort:** 1 hour

---

### Issue #12: Performance - String Operations
- Cache extracted words during completion
- Use `Span<char>` for substring operations
- Use `StringBuilder` for repeated concatenations

**Estimated Effort:** 2-3 hours

---

## 📋 Recommended Implementation Order

### Phase 1: Foundation (Week 1) - 10-14 hours
**Goal:** Establish solid theming foundation

1. ✅ ~~Remove old console~~ (DONE!)
2. 🔴 **Consolidate theme systems** (Issue #1) - 4-6 hours
3. 🔴 **Remove all hardcoded colors** (Issue #2) - 2-3 hours
4. 🟢 **Move magic numbers to theme** (Issue #3) - 1-2 hours
5. 🟢 **Document UI architecture decision** (Issue #6) - 1 hour

**Deliverables:**
- Single `UITheme` system with all colors
- Zero hardcoded colors in components
- All spacing in theme
- Architecture documentation

---

### Phase 2: API Standardization (Week 2) - 8-10 hours
**Goal:** Consistent component APIs

1. 🟡 **Create standard interfaces** (Issue #4) - 3-4 hours
2. 🟡 **Simplify state management** (Issue #5) - 1-2 hours
3. 🟡 **Add layout caching** (Issue #6) - 2-3 hours
4. 🟢 **Add .editorconfig** (Issue #11) - 1 hour

**Deliverables:**
- `ITextInput`, `ITextDisplay` interfaces
- State enum in `ConsolePanel`
- Layout dirty flags
- Naming conventions enforced

---

### Phase 3: Architecture Cleanup (Week 3) - 12-16 hours
**Goal:** Better separation of concerns

1. 🟢 **Split ConsoleSystem** (Issue #7) - 4-6 hours
2. 🟢 **Improve input routing** (Issue #8) - 3-4 hours
3. 🟢 **Command pattern refactor** (Issue #9) - 4-5 hours
4. 🟢 **Add null safety** (Issue #10) - 2 hours

**Deliverables:**
- Focused classes (<300 lines each)
- Clear input priority system
- Type-safe command system
- Proper null annotations

---

### Phase 4: Performance & Polish (Week 4) - 4-6 hours
**Goal:** Optimize and polish

1. 🟢 **String operation optimization** (Issue #12) - 2-3 hours
2. 🟢 **Add integration tests** - 2-3 hours
3. 🟢 **Performance profiling** - 1 hour

**Deliverables:**
- No string allocations in hot paths
- Test coverage >70%
- Performance baselines documented

---

## ✅ Acceptance Criteria

### Phase 1 Complete When:
- [ ] All colors come from single `UITheme` class
- [ ] Zero `new Color(...)` in component code
- [ ] Can swap Dark/Light theme with one line
- [ ] All magic numbers in theme or named constants
- [ ] Architecture doc exists (Immediate vs Retained)

### Phase 2 Complete When:
- [ ] All text inputs implement `ITextInput`
- [ ] `ConsolePanel` uses state enum instead of 4 booleans
- [ ] Layout only recalculates when dirty
- [ ] `.editorconfig` enforces naming

### Phase 3 Complete When:
- [ ] No class >500 lines
- [ ] Commands use registry pattern
- [ ] Input routing is documented and simple
- [ ] All nullable refs properly annotated

### Phase 4 Complete When:
- [ ] No `Substring()` calls, use `Span<char>`
- [ ] Tests cover >70% of core components
- [ ] Performance profiling shows no hotspots

---

## 🎯 Quick Wins (Can Start Today)

1. **Add UITheme console colors** (30 min)
   - Extend `UITheme.Dark` with console-specific colors
   - No breaking changes yet

2. **Document current architecture** (30 min)
   - Add comments explaining immediate/retained hybrid
   - Create simple diagram

3. **Fix one component's hardcoded colors** (15 min)
   - Pick `ConsolePanel.cs`, replace 3 hardcoded colors
   - Verify it still works

4. **Create `.editorconfig`** (15 min)
   - Enforce naming conventions
   - Run on codebase, fix violations

5. **Create `ITextInput` interface** (30 min)
   - Define interface
   - Don't implement yet, just create

**Total: ~2 hours for quick wins**

---

## 📊 Progress Tracking

### Before Refactoring
- ❌ Theme Systems: 2
- ❌ Hardcoded Colors: ~30 instances
- ✅ Duplicate Consoles: 0 (FIXED!)
- ❌ Magic Numbers: ~15 instances
- ❌ Lines per Class (avg): ~500
- ❌ Boolean State Flags: 6 in ConsolePanel

### After Phase 1 Target
- ✅ Theme Systems: 1
- ✅ Hardcoded Colors: 0
- ✅ Duplicate Consoles: 0 (DONE!)
- ✅ Magic Numbers: 0
- ⏳ Lines per Class (avg): ~500 (unchanged)
- ⏳ Boolean State Flags: 6 (unchanged)

### After Phase 2 Target
- ✅ Theme Systems: 1
- ✅ Hardcoded Colors: 0
- ✅ Duplicate Consoles: 0
- ✅ Magic Numbers: 0
- ⏳ Lines per Class (avg): ~500 (unchanged)
- ✅ Boolean State Flags: 1 (enum-based)

### Final Target (After Phase 4)
- ✅ Theme Systems: 1
- ✅ Hardcoded Colors: 0
- ✅ Duplicate Consoles: 0
- ✅ Magic Numbers: 0
- ✅ Lines per Class (avg): <300
- ✅ Boolean State Flags: <3 per class

---

## 💡 Notes

### Why This Order?
1. **Theming first** - Foundation for everything else
2. **APIs second** - Makes refactoring easier
3. **Architecture third** - Depends on stable APIs
4. **Performance last** - Optimize after structure is correct

### Risk Mitigation
- Test after each phase
- Keep backward compatibility bridges during Phase 1
- Mark deprecated code with `[Obsolete]`
- Create feature flags for big changes

### Team Coordination
- Phase 1: Can be done solo
- Phase 2-3: May require pairing for large refactors
- Phase 4: Include QA for performance testing

---

**Status:** Ready to start Phase 1 🚀
**Next Action:** Extend UITheme with console colors

