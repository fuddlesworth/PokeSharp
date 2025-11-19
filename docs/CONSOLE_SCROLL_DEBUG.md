# Debug Console - Scrolling Troubleshooting

If scrolling isn't working, follow these steps:

## Step 1: Verify You Have Enough Content

The console **only scrolls if there's more content than can fit on screen**.

Test by generating lots of output:

```csharp
// Open console with ` or ~
// Type this command:
for (int i = 0; i < 100; i++) { Print($"Line {i}"); }

// Now try PageUp - you should scroll back
// Try PageDown - you should scroll forward
```

## Step 2: Check the Logs

When you press PageUp or PageDown, the logs should show:
```
[TIME] [INFOR] "ConsoleSystem": Scrolled up: offset=X, total=Y
[TIME] [INFOR] "ConsoleSystem": Scrolled down: offset=X, total=Y
```

If you DON'T see these messages:
- The key press isn't being detected
- Try pressing the key harder or check your keyboard
- Make sure the console is open and visible

## Step 3: Check Key Detection

Type this in the console to see all pressed keys:

```csharp
// The console logs all key presses
// Watch the terminal/logs when you press PageUp
```

Look for messages like:
```
[TIME] [INFOR] "ConsoleSystem": Keys pressed: PageUp
```

## Step 4: Verify Visual Updates

If scrolling works but you don't see changes:
1. Make sure there's a **scrollbar** on the right side
2. The scrollbar should move when you scroll
3. The output text should change

## Common Issues

### Issue: "Nothing happens when I press PageUp/PageDown"

**Cause 1**: Not enough content to scroll
- **Solution**: Generate more output (see Step 1)

**Cause 2**: Console not visible
- **Solution**: Press `` ` `` or `~` to toggle console

**Cause 3**: Wrong key
- **Solution**: Make sure you're pressing PageUp/PageDown, not Up/Down arrows
  - **Up/Down arrows** = command history
  - **PageUp/PageDown** = scroll output

### Issue: "Scrollbar doesn't appear"

**Cause**: Not enough lines to scroll
- **Solution**: The scrollbar only appears when `TotalLines > VisibleLines`
- Generate more output with the for loop above

### Issue: "Output doesn't change when scrolling"

**Cause**: Already at top or bottom
- **Solution**:
  - If at top, use PageDown or Ctrl+End
  - If at bottom, use PageUp or Ctrl+Home
  - Check scrollbar thumb color (blue = at edge)

### Issue: "Scroll jumps back to bottom immediately"

**Cause**: New output was added (auto-scroll behavior)
- **Solution**: This is normal. The console auto-scrolls to bottom when:
  - You execute a command
  - New output is generated
- Scroll back up after command completes

## Manual Test Procedure

1. **Start the game**
2. **Open console**: Press `` ` `` or `~`
3. **Generate content**: Type: `for (int i = 0; i < 100; i++) { Print($"Line {i}"); }`
4. **Press Enter** to execute
5. **Wait** for all 100 lines to appear
6. **Press PageUp** - You should see "Line 0" or earlier lines
7. **Press PageDown** - You should see later lines
8. **Press Ctrl+Home** - Should jump to "Line 0"
9. **Press Ctrl+End** - Should jump to "Line 99"

## Debug Mode

To see detailed scrolling info, watch the logs while pressing keys:

```bash
# In terminal, tail the logs:
tail -f logs/pokesharp-*.log | grep -i "scroll\|pageup\|pagedown"
```

## Still Not Working?

Check these:

1. **Build succeeded?**
   ```bash
   cd /Users/ntomsic/Documents/PokeSharp
   dotnet build PokeSharp.Game/PokeSharp.Game.csproj --configuration Debug
   ```

2. **Console system registered?**
   - Look for log: `"=== DEBUG CONSOLE READY ==="`
   - If missing, console didn't initialize

3. **Running Debug build?**
   - Console only works in Debug builds (`#if DEBUG`)
   - Check you're running with `--configuration Debug`

4. **Keys being detected?**
   - Look for log: `"Keys pressed: PageUp"`
   - If missing, keyboard input isn't working

## Report Bug

If none of the above helps, provide:
1. Log output when pressing PageUp
2. Output of `dotnet build`
3. Screenshot of console with content
4. Keyboard layout (US/UK/etc)

