# 🎮 New Console Quick Start

## How to Open the New Console

### Step 1: Run the Game
```bash
dotnet run --project PokeSharp.Game
```

### Step 2: Press the Key Combo

**Press: `Ctrl + ~`** (Ctrl + Backtick/Tilde)

- On Windows/Linux: `Left Ctrl + ~`
- On Mac: `Cmd + ~` (or Ctrl + ~)

The tilde key (`~`) is:
- Above the Tab key
- Left of the number 1 key
- Same key as backtick (`)

✅ **FIX APPLIED**: The key handler now checks for `Ctrl+~` BEFORE checking for `~` alone, so it will no longer be overridden by the old console!

---

## Console Comparison

| Key | What Opens |
|-----|------------|
| **`~`** alone | OLD console (original, full Roslyn scripting) |
| **`Ctrl + ~`** | NEW console (new UI framework!) |

---

## What to Try

Once the new console opens:

### Basic Commands
```
help    → Show available commands
clear   → Clear the output
exit    → Close console
quit    → Also closes console
```

### C# Expressions
```csharp
2 + 2
Math.PI
DateTime.Now
"Hello".ToUpper()
```

### Navigation
- **Up/Down arrows** → Navigate command history
- **Tab** → Show auto-completions
- **Enter** → Execute command
- **Escape** → Close console
- **Ctrl + ~** → Close console

---

## Troubleshooting

### Console doesn't open?

1. **Check the logs** - Look for:
   ```
   "Opening new console"
   "New console opened successfully"
   ```

2. **Try the old console first** - Press `~` alone
   - If the old console works, the system is fine
   - If not, check ConsoleSystem initialization

3. **Check for errors**:
   ```bash
   # Look for errors in console output
   dotnet run --project PokeSharp.Game 2>&1 | grep -i error
   ```

### Font not loading?

The console needs a monospace font. It tries:
- **macOS**: Monaco, Menlo, Courier
- **Windows**: Consolas, Courier
- **Linux**: Liberation Mono, DejaVu Sans Mono

If you see font errors, install one of these fonts.

---

## What's Different?

### Old Console (`)
- ✅ Full Roslyn C# scripting
- ✅ Syntax highlighting
- ✅ Advanced auto-complete
- ✅ Multi-line editing
- ❌ Monolithic design
- ❌ Hard to extend

### New Console (Ctrl+~)
- ✅ Modular components
- ✅ Reusable UI pieces
- ✅ Event-driven architecture
- ✅ C# scripting (via Roslyn)
- ✅ Command history
- ✅ Basic auto-complete
- ⏳ Syntax highlighting (coming soon)
- ⏳ Advanced completion (coming soon)

---

## Quick Demo

```bash
# 1. Run game
dotnet run --project PokeSharp.Game

# 2. Press Ctrl+~

# 3. Type these commands:
help
2 + 2
Math.Pow(2, 10)
DateTime.Now.Year
clear

# 4. Test history:
#    - Press Up arrow
#    - Press Down arrow

# 5. Test completions:
#    - Type "hel"
#    - Press Tab

# 6. Close it:
exit
```

---

## Still Not Working?

If the new console still doesn't open after trying Ctrl+~:

1. **Check the build**:
   ```bash
   dotnet build --no-restore
   # Should show: Build succeeded. 0 Error(s)
   ```

2. **Check the integration**:
   - File exists: `PokeSharp.Engine.UI.Debug/Scenes/NewConsoleScene.cs` ✓
   - ConsoleSystem modified: `ToggleNewConsole()` method ✓
   - DI registration: `AddTransient<NewConsoleScene>()` ✓

3. **Try the test scenes** (to verify UI system works):
   - Press `F11` → Should show basic UI test
   - Press `F12` → Should show advanced debug tools
   - If these work, the new console should too

4. **Check for initialization errors**:
   Look in logs for:
   ```
   "NewConsoleScene UI context initialized"
   "NewConsoleScene content loaded successfully"
   ```

---

## Key Combo Not Working?

Try these alternatives:

### Alternative 1: Modify the key binding
Edit `ConsoleSystem.cs` line ~295:

```csharp
// Change from:
if (ctrlPressed && currentKeyboard.IsKeyDown(Keys.OemTilde) ...

// To use F9 instead:
if (currentKeyboard.IsKeyDown(Keys.F9) && _previousKeyboardState.IsKeyUp(Keys.F9))
{
    ToggleNewConsole();
}
```

### Alternative 2: Call it from old console

Open old console (`) and type:
```csharp
// This should work if everything is integrated
// (will need to expose the method)
```

---

## Success!

If you see this when you press Ctrl+~:

```
┌────────────────────────────────────┐
│ === PokeSharp New Console ===      │
│ Type 'help' for available commands │
│ Press Ctrl+~ to close              │
│                                    │
│ >                                  │
└────────────────────────────────────┘
```

**You did it!** 🎉

---

**TL;DR**: Press **`Ctrl + ~`** (that's Ctrl + the key above Tab)

