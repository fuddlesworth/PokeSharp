# Console Phase 2: Migration Guide

## 🎉 All Phase 2 Features Implemented!

All 6 Phase 2 features are complete and ready to use. Here's how to integrate them:

---

## 🔄 Quick Integration (3 Steps)

### Step 1: Update ConsoleSystem to use QuakeConsoleV2

```csharp
// In ConsoleSystem.cs, line ~28
- private QuakeConsole _console = null!;
+ private QuakeConsoleV2 _console = null!;
+ private ConsoleHistoryPersistence _persistence = null!;
+ private ConsoleAutoComplete _autoComplete = null!;
```

### Step 2: Initialize with Phase 2 features

```csharp
public void Initialize(World world)
{
    try
    {
        // Create console configuration
        var config = new ConsoleConfig
        {
            Size = ConsoleSize.Medium,
            FontSize = 16,
            SyntaxHighlightingEnabled = true,
            AutoCompleteEnabled = true,
            PersistHistory = true
        };

        // Create console UI with Phase 2
        var viewport = _graphicsDevice.Viewport;
        _console = new QuakeConsoleV2(_graphicsDevice, viewport.Width, viewport.Height, config);

        // Create script evaluator
        _evaluator = new ConsoleScriptEvaluator(_logger);

        // Create command history
        _history = new ConsoleCommandHistory();

        // Initialize history persistence
        _persistence = new ConsoleHistoryPersistence(_logger);
        var savedCommands = _persistence.LoadHistory();
        _history.LoadHistory(savedCommands);

        // Initialize auto-complete
        _autoComplete = new ConsoleAutoComplete();

        // Create globals (same as before)
        _globals = new ConsoleGlobals
        {
            World = _world,
            Systems = _systemManager,
            Api = _apiProvider,
            Graphics = _graphicsDevice,
            OutputAction = text => _console.AppendOutput(text, new Color(200, 255, 200))
        };

        _console.AppendOutput("=== Debug Console v2.0 Initialized ===", new Color(255, 255, 100));
        _console.AppendOutput("New Phase 2 Features:", Color.LightGray);
        _console.AppendOutput("  ✅ TrueType font rendering", new Color(100, 255, 100));
        _console.AppendOutput("  ✅ Console sizing (small/medium/full)", new Color(100, 255, 100));
        _console.AppendOutput("  ✅ Multi-line input (Shift+Enter)", new Color(100, 255, 100));
        _console.AppendOutput("  ✅ Syntax highlighting", new Color(100, 255, 100));
        _console.AppendOutput("  ✅ Auto-completion", new Color(100, 255, 100));
        _console.AppendOutput("  ✅ History persistence", new Color(100, 255, 100));
        _console.AppendOutput("");
        _console.AppendOutput("Type 'help' for commands", Color.LightGray);
        _console.AppendOutput("");

        _logger.LogInformation("Debug console v2.0 initialized successfully");
    }
    catch (Exception ex)
    {
        _logger.LogError(ex, "Failed to initialize debug console v2.0");
        throw;
    }
}
```

### Step 3: Update input handling for multi-line and auto-complete

```csharp
private bool HandleInput()
{
    var currentKeyboard = Keyboard.GetState();
    var currentMouse = Mouse.GetState();
    var isShiftPressed = currentKeyboard.IsKeyDown(Keys.LeftShift) || currentKeyboard.IsKeyDown(Keys.RightShift);

    // ... (toggle console logic - same as before) ...

    if (!_console.IsVisible)
    {
        _previousKeyboardState = currentKeyboard;
        _previousMouseState = currentMouse;
        return false;
    }

    // Handle Enter key - execute command (NOT in multi-line mode with Shift)
    if (WasKeyJustPressed(Keys.Enter, currentKeyboard) && !isShiftPressed)
    {
        // Save history before executing
        if (_console.Config.PersistHistory)
        {
            _persistence.SaveHistory(_history.GetAll());
        }

        ExecuteCommand();
        _previousKeyboardState = currentKeyboard;
        _previousMouseState = currentMouse;
        return true;
    }

    // Handle Tab - accept auto-complete suggestion
    if (WasKeyJustPressed(Keys.Tab, currentKeyboard) && _console.Config.AutoCompleteEnabled)
    {
        var suggestion = _console.GetSelectedSuggestion();
        if (suggestion != null)
        {
            _console.Input.SetText(suggestion);
            _console.ClearAutoCompleteSuggestions();
        }
        _previousKeyboardState = currentKeyboard;
        _previousMouseState = currentMouse;
        return true;
    }

    // Handle Ctrl+Space - trigger auto-complete
    if (WasKeyJustPressed(Keys.Space, currentKeyboard) && 
        (currentKeyboard.IsKeyDown(Keys.LeftControl) || currentKeyboard.IsKeyDown(Keys.RightControl)) &&
        _console.Config.AutoCompleteEnabled)
    {
        await TriggerAutoComplete();
        _previousKeyboardState = currentKeyboard;
        _previousMouseState = currentMouse;
        return true;
    }

    // ... (rest of input handling - PageUp/Down, arrows, etc.) ...

    // Handle text input (now with Shift detection for multi-line)
    foreach (var key in currentKeyboard.GetPressedKeys())
    {
        if (!_previousKeyboardState.IsKeyDown(key))
        {
            var character = GetCharFromKey(key, currentKeyboard);
            _console.Input.HandleKeyPress(key, character, isShiftPressed);

            // Trigger auto-complete on typing
            if (_console.Config.AutoCompleteEnabled && char.IsLetterOrDigit(character ?? '\0'))
            {
                await TriggerAutoComplete();
            }
        }
    }

    _previousKeyboardState = currentKeyboard;
    _previousMouseState = currentMouse;
    return true;
}

private async Task TriggerAutoComplete()
{
    var code = _console.GetInputText();
    var cursorPos = _console.Input.CursorPosition;
    var suggestions = await _autoComplete.GetCompletionsAsync(code, cursorPos);
    _console.SetAutoCompleteSuggestions(suggestions);
}
```

---

## 📝 Optional: Add Console Size Commands

Add these to the help command execution:

```csharp
if (command.Equals("size small", StringComparison.OrdinalIgnoreCase))
{
    _console.Config.Size = ConsoleSize.Small;
    _console.UpdateSize();
    _console.AppendOutput("Console size set to Small (25%)", new Color(100, 255, 100));
    _console.ClearInput();
    _isProcessingCommand = false;
    return;
}

if (command.Equals("size medium", StringComparison.OrdinalIgnoreCase))
{
    _console.Config.Size = ConsoleSize.Medium;
    _console.UpdateSize();
    _console.AppendOutput("Console size set to Medium (50%)", new Color(100, 255, 100));
    _console.ClearInput();
    _isProcessingCommand = false;
    return;
}

if (command.Equals("size full", StringComparison.OrdinalIgnoreCase))
{
    _console.Config.Size = ConsoleSize.Full;
    _console.UpdateSize();
    _console.AppendOutput("Console size set to Full (100%)", new Color(100, 255, 100));
    _console.ClearInput();
    _isProcessingCommand = false;
    return;
}
```

---

## 🎨 Customization

### Change Color Scheme (Optional)

Edit `ConsoleSyntaxHighlighter.cs` to customize colors:

```csharp
// Line ~15
private static readonly Color KeywordColor = new(86, 156, 214);    // Change to your preference
private static readonly Color StringColor = new(206, 145, 120);
private static readonly Color NumberColor = new(181, 206, 168);
// ... etc
```

### Add Custom Auto-Complete Suggestions

Edit `ConsoleAutoComplete.cs`, method `GetQuickCompletions`:

```csharp
// Add your custom API methods
var apiMethods = new[]
{
    "Api.Player.GetEntity()",
    "Api.Player.GetMoney()",
    "Api.MyCustom.DoSomething(",  // <-- Add here
    // ... etc
};
```

---

## 🔍 Troubleshooting

### Font Not Loading?

If you see bitmap font instead of TrueType:

1. **Check logs**: Look for font loading errors
2. **Verify font paths**: `ConsoleFontRenderer.cs` line ~35
3. **Add custom font**: Place a `.ttf` file in your project and update paths

```csharp
// In ConsoleFontRenderer.LoadFont(), add your custom path:
string[] fontPaths = new[]
{
    Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Fonts/CustomFont.ttf"),
    // ... existing paths ...
};
```

### History Not Persisting?

1. **Check permissions**: Ensure write access to `%LOCALAPPDATA%/PokeSharp/`
2. **Check logs**: Look for "Failed to save console history" errors
3. **Verify config**: Ensure `config.PersistHistory = true`

### Syntax Highlighting Not Working?

1. **Check config**: Ensure `config.SyntaxHighlightingEnabled = true`
2. **Test simple code**: Try `var x = 10;` to see colors
3. **Check rendering**: Ensure `QuakeConsoleV2.Update()` is being called

---

## ✅ Verification Checklist

After integration, verify:

- [ ] Console opens with `` ` `` key
- [ ] Font is TrueType (smooth, not pixelated)
- [ ] `Shift+Enter` adds newline
- [ ] Syntax highlighting colors appear
- [ ] Typing shows auto-complete suggestions
- [ ] Commands persist after restart
- [ ] Help command shows Phase 2 features

---

## 📊 Before & After

**Before (Phase 1)**:
```
> var x = 10;           (plain white text, bitmap font)
```

**After (Phase 2)**:
```
> var x = 10;           
  ^^^(blue) ^(white) ^^(cyan) ^^(white)
  (smooth TrueType font, syntax colored)
```

---

## 🎉 You're Done!

Phase 2 is now integrated. Enjoy your IDE-like debug console! 🚀

**Next Steps**:
- Customize colors to match your theme
- Add project-specific auto-complete suggestions
- Try multi-line scripts
- Explore Phase 3 features (coming soon)

