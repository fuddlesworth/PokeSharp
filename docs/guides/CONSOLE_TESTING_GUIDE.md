# Console Testing Guide

## Quick Start

```bash
# 1. Build (should have 0 errors)
dotnet build --no-restore

# 2. Run
dotnet run --project PokeSharp.Game

# 3. Test both consoles
Press ~       → OLD console (original)
Press Ctrl+~  → NEW console (our creation!)
```

---

## What to Test

### Test 1: New Console Opens with Content

**Action**: Press `Ctrl+~`

**Expected**:
```
=== PokeSharp Debug Console ===
Type 'help' for available commands
Press Ctrl+~ or type 'exit' to close

>
```

**Pass Criteria**:
- ✅ Console slides down from top
- ✅ Welcome message visible
- ✅ Input prompt visible
- ✅ Cursor blinking
- ✅ Game visible behind console

---

### Test 2: Basic Commands

**Actions**:
```
Type: help
Type: clear
Type: 2 + 2
Type: Math.PI
Type: DateTime.Now
```

**Expected**:
- `help`: Shows available commands
- `clear`: Clears output (welcome message stays)
- `2 + 2`: Shows "4"
- `Math.PI`: Shows "3.14159..."
- `DateTime.Now`: Shows current date/time

**Pass Criteria**:
- ✅ Commands execute
- ✅ Output appears in console
- ✅ No errors/crashes

---

### Test 3: Command History

**Actions**:
1. Type: `2 + 2` [Enter]
2. Type: `Math.PI` [Enter]
3. Type: `"Hello"` [Enter]
4. Press: Up Arrow (should show "Hello")
5. Press: Up Arrow (should show Math.PI)
6. Press: Up Arrow (should show 2 + 2)
7. Press: Down Arrow (should show Math.PI)

**Pass Criteria**:
- ✅ Up arrow navigates backwards through history
- ✅ Down arrow navigates forwards
- ✅ Commands are recalled correctly

---

### Test 4: Auto-Completion

**Actions**:
1. Type: `Mat` [Tab]
2. Type: `Date` [Tab]

**Expected**:
- Suggestions dropdown appears
- Matching items shown
- Can select with arrow keys or mouse

**Pass Criteria**:
- ✅ Tab shows completions
- ✅ Dropdown appears below input
- ✅ Can navigate with keyboard
- ✅ Can select with mouse

---

### Test 5: Console Closing

**Actions**:
1. Press `Ctrl+~` to open
2. Press `Escape` to close
3. Press `Ctrl+~` to open again
4. Type `exit` [Enter]
5. Press `Ctrl+~` to open again
6. Press `Ctrl+~` to close

**Pass Criteria**:
- ✅ Escape closes console
- ✅ `exit` command closes console
- ✅ `Ctrl+~` toggles console
- ✅ Console can be reopened multiple times

---

### Test 6: Game Rendering Behind Console

**Actions**:
1. Start game
2. Let game run for a few seconds (entities moving)
3. Press `Ctrl+~` to open console
4. Observe game behind console

**Expected**:
- Game continues running
- Game is visible through console background
- Entities/animations still moving

**Pass Criteria**:
- ✅ Game renders behind console
- ✅ Semi-transparent background
- ✅ Game not paused
- ✅ Can see game clearly enough

---

### Test 7: Multiple Toggles

**Actions**:
1. Press `~` (old console opens)
2. Press `Escape` (closes)
3. Press `Ctrl+~` (new console opens)
4. Press `Escape` (closes)
5. Press `~` (old console opens)
6. Press `Escape` (closes)
7. Press `Ctrl+~` (new console opens)

**Pass Criteria**:
- ✅ Consoles toggle independently
- ✅ No conflicts between old/new
- ✅ No crashes or errors
- ✅ Both consoles work after toggling

---

### Test 8: Text Scrolling

**Actions**:
```csharp
// Type in console:
for(int i = 0; i < 100; i++) { Console.WriteLine($"Line {i}"); }
```

**Expected**:
- 100 lines printed
- Scrollbar appears
- Can scroll up/down with mouse wheel
- Auto-scrolls to bottom

**Pass Criteria**:
- ✅ All lines visible (can scroll to see)
- ✅ Scrollbar appears when needed
- ✅ Mouse wheel scrolling works
- ✅ Auto-scroll to bottom on new output

---

### Test 9: Error Handling

**Actions**:
```
Type: 1 / 0
Type: undefined_variable
Type: throw new Exception("test")
```

**Expected**:
- Errors displayed in red
- Console doesn't crash
- Can continue using console after errors

**Pass Criteria**:
- ✅ Errors shown in red/error color
- ✅ Console stays functional
- ✅ Error messages are readable
- ✅ Can execute more commands after error

---

### Test 10: Performance

**Actions**:
1. Open console
2. Execute many commands quickly
3. Scroll through output
4. Open/close console repeatedly

**Expected**:
- No lag or stutter
- Smooth animations
- Responsive input
- 60 FPS maintained

**Pass Criteria**:
- ✅ Console responds immediately
- ✅ No frame drops
- ✅ Smooth scrolling
- ✅ No memory leaks (check with task manager)

---

## Regression Testing

### Old Console Still Works?

**Test**:
1. Press `~` (backtick alone)
2. Type: `2 + 2`
3. Verify it works

**Why**: Ensure new console didn't break old one

---

### Debug Overlays Still Work?

**Test**:
1. Press `F10` (debug overlay)
2. Press `F11` (UI test scene)
3. Press `F12` (advanced debug)

**Why**: Ensure other debug features unaffected

---

## Common Issues

### Console Opens But Is Empty
**Fix**: Already applied! Welcome message now shows.

### Can't See Game Behind Console
**Fix**: Already applied! `RenderScenesBelow = true`

### Ctrl+~ Not Working
**Fix**: Already applied! Check order fixed.

### Font Not Loading
**Solution**: Install a monospace font:
- macOS: Monaco (usually pre-installed)
- Windows: Consolas (usually pre-installed)
- Linux: Install `fonts-liberation` or `fonts-dejavu`

---

## Test Checklist

Copy this and check off as you test:

```
[ ] Test 1: Console opens with content
[ ] Test 2: Basic commands work
[ ] Test 3: Command history works
[ ] Test 4: Auto-completion works
[ ] Test 5: Console closing works
[ ] Test 6: Game renders behind console
[ ] Test 7: Multiple toggles work
[ ] Test 8: Text scrolling works
[ ] Test 9: Error handling works
[ ] Test 10: Performance is good
[ ] Regression: Old console works
[ ] Regression: Debug overlays work
```

---

## Success Criteria

**All Tests Passed**: Console migration complete! 🎉

**Some Tests Failed**: Check the specific test section for debugging steps

**Build Errors**: Check `CONSOLE_FIXES_APPLIED.md` for troubleshooting

---

## Reporting Issues

If you find issues, note:
1. Which test failed
2. What you expected
3. What actually happened
4. Any error messages
5. Build warnings/errors

---

**Ready to test?** Run the game and press `Ctrl+~`! 🚀


