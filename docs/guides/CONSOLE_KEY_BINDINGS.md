# Console Key Bindings Reference

## Overview
PokeSharp has **two console systems** that can run side-by-side:

1. **Old Console** - Original Quake-style console with full scripting
2. **New Console** - Modern UI framework with modular components

---

## Key Bindings

| Key Combination | Action | Description |
|----------------|--------|-------------|
| **`~`** (backtick alone) | Open OLD console | Original console with full Roslyn scripting |
| **`Ctrl + ~`** | Open NEW console | Modern console with new UI framework |
| **`F10`** | Toggle debug overlay | Shows FPS, memory, entity count in corner |
| **`Ctrl + F10`** | Cycle overlay position | Moves overlay between corners |
| **`F11`** | Toggle UI test scene | Shows basic UI components demo |
| **`F12`** | Toggle advanced debug UI | Shows command palette & entity inspector |

---

## Key Handling Priority

The system checks keys in this order:

```
1. Ctrl+~ → New Console (highest priority)
2. ~      → Old Console (only if Ctrl NOT pressed)
3. F10    → Debug Overlay
4. F11    → UI Test Scene
5. F12    → Advanced Debug Scene
```

This ensures that **Ctrl+~** always opens the new console, even though both use the same base key.

---

## Implementation Details

### Code Location
File: `PokeSharp.Engine.Debug/Systems/ConsoleSystem.cs`

### Key Check Order (in Update method)
```csharp
// Line ~253: Check for Ctrl+~ FIRST (new console)
if (!_isConsoleOpen && !_isNewConsoleOpen)
{
    bool isCtrlPressed = /* ... */;
    bool newConsoleTogglePressed = isCtrlPressed &&
                                    currentKeyboard.IsKeyDown(Keys.OemTilde) &&
                                    _previousKeyboardState.IsKeyUp(Keys.OemTilde) &&
                                    !isShiftPressed;
    if (newConsoleTogglePressed)
        ToggleNewConsole();
}

// Line ~269: Check for ~ alone SECOND (old console)
if (!_isConsoleOpen && !_isNewConsoleOpen)
{
    bool togglePressed = currentKeyboard.IsKeyDown(Keys.OemTilde) &&
                         _previousKeyboardState.IsKeyUp(Keys.OemTilde) &&
                         !isShiftPressed &&
                         !isCtrlPressed; // Exclude if Ctrl pressed
    if (togglePressed)
        ToggleConsole();
}
```

### Why This Order Matters

If we checked `~` alone before `Ctrl+~`, the old console would consume the key press and the new console would never trigger. By checking `Ctrl+~` first:

1. If Ctrl is pressed → Open new console, skip old console check
2. If Ctrl is NOT pressed → Open old console

---

## Modifier Keys

### Ctrl Detection
```csharp
bool isCtrlPressed = currentKeyboard.IsKeyDown(Keys.LeftControl) ||
                     currentKeyboard.IsKeyDown(Keys.RightControl);
```

### Shift Detection (used to ignore Shift+~ which types ~)
```csharp
bool isShiftPressed = currentKeyboard.IsKeyDown(Keys.LeftShift) ||
                      currentKeyboard.IsKeyDown(Keys.RightShift);
```

---

## Console States

The system tracks two independent console states:

```csharp
private bool _isConsoleOpen;      // Old console open?
private bool _isNewConsoleOpen;   // New console open?
```

**Mutual Exclusion**: Only one console can be open at a time. Opening one automatically closes the other if needed.

---

## Keyboard Layout Reference

```
┌─────────────────────────────────────────┐
│  ~  │  1  │  2  │  3  │  4  │  5  │  6  │  ← OemTilde key (~ or `)
├─────┴──┬──┴──┬──┴──┬──┴──┬──┴──┬──┴──┬──┤
│  Tab   │  Q  │  W  │  E  │  R  │  T  │  Y│
├────────┴─────┴─────┴─────┴─────┴─────┴──┤
│  Ctrl  │  A  │  S  │  D  │  F  │  G  │  H│  ← Left Control
└────────┴─────┴─────┴─────┴─────┴─────┴──┘
```

### To Press Ctrl+~
1. **Hold** the Ctrl key (bottom left)
2. **Tap** the ~ key (top left, above Tab)
3. **Release** both keys

---

## Testing Key Bindings

### Quick Test Script
```bash
# 1. Run the game
dotnet run --project PokeSharp.Game

# 2. Test each key:
#    a) Press ~ alone → Should open OLD console
#    b) Press Escape → Close console
#    c) Press Ctrl+~ → Should open NEW console
#    d) Press Escape → Close console
#    e) Press F10 → Should show debug overlay
#    f) Press F10 → Should hide debug overlay
#    g) Press F11 → Should show UI test
#    h) Press F11 → Should hide UI test
#    i) Press F12 → Should show advanced debug
#    j) Press F12 → Should hide advanced debug
```

### Expected Behavior

| Test | Expected Result |
|------|----------------|
| Press `~` | Old console slides down with syntax highlighting |
| Press `Ctrl+~` | New console slides down with clean UI |
| Press `Shift+~` | Nothing (Shift produces `~` character) |
| Both consoles open? | Impossible - they're mutually exclusive |
| Press `F10` | Small stats panel appears in corner |
| Press `F11` | Two panels appear (stats + entity inspector) |
| Press `F12` | Full screen debug UI with command palette |

---

## Troubleshooting

### New Console Not Opening?

1. **Check Ctrl is being pressed**
   - Some keyboards have quirky Ctrl keys
   - Try both Left Ctrl and Right Ctrl

2. **Check for key conflicts**
   - Some apps intercept Ctrl+~
   - Try running in fullscreen mode

3. **Check the logs**
   ```bash
   dotnet run --project PokeSharp.Game 2>&1 | grep -i "console"
   ```
   Look for:
   - "Opening new console"
   - "New console opened successfully"

4. **Use alternative key binding**
   Edit `ConsoleSystem.cs` and change to `F9`:
   ```csharp
   if (currentKeyboard.IsKeyDown(Keys.F9) &&
       _previousKeyboardState.IsKeyUp(Keys.F9))
   {
       ToggleNewConsole();
   }
   ```

### Old Console Not Opening?

1. **Check Ctrl is NOT pressed**
   - Make sure you're not accidentally holding Ctrl

2. **Check console system is initialized**
   - Look for "Console system initialized" in logs

---

## Summary

✅ **Fixed**: New console now has priority when Ctrl is pressed
✅ **Old Console**: `~` alone
✅ **New Console**: `Ctrl + ~`
✅ **No Conflicts**: Checks happen in correct order

**Just remember**: Hold **Ctrl** for the new UI! 🎮


