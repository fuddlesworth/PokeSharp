# Console Troubleshooting Guide

## Testing the Console

### Step 1: Run the Game in DEBUG Mode
```bash
cd /Users/ntomsic/Documents/PokeSharp
dotnet run --project PokeSharp.Game --configuration Debug
```

### Step 2: Check the Logs
Look for these messages in the console output or `game_run.log`:

```
[INFO] DEBUG BUILD: Initializing debug console...
[INFO] ConsoleSystem created, registering...
[INFO] Debug console initialized successfully
[INFO] === DEBUG CONSOLE READY === Press ~ or ` to toggle
```

If you see `"FAILED to initialize debug console"`, check the exception details.

### Step 3: Try Opening the Console

Once the game window is open and you see the player, try these keys:

1. **Tilde key** (`~`) - The key to the left of '1' (Shift + `)
2. **Grave accent** (`` ` ``) - Same key without Shift
3. **Quote key** - Sometimes mapped differently on non-US keyboards

### Step 4: If Console Opens
You should see:
- A black/dark overlay covering the screen
- Text at the top: "PokeSharp Debug Console"
- A `>` prompt at the bottom
- Input field where you can type

### Step 5: Test Commands
Try these:
```csharp
> 2 + 2
Result: 4

> World.CountEntities()
Result: [number of entities]

> var x = 10
> x + 5
Result: 15
```

## Common Issues

### Issue 1: Console Doesn't Open

**Symptoms:** Pressing ` or ~ does nothing

**Causes:**
1. Console not initialized (check logs for initialization messages)
2. Keyboard layout issue (try both ` and ~)
3. Window doesn't have focus

**Solutions:**
1. Check `game_run.log` for "DEBUG BUILD" messages
2. Try clicking on the game window first
3. Try all variations: `, ~, Shift+`, Shift+~

### Issue 2: Console Opens But Is Blank

**Symptoms:** Dark overlay appears but no text

**Causes:**
1. Font rendering issue (we're using fallback rendering)
2. Text color matches background

**Solutions:**
1. Type blindly and press Enter to see if commands execute
2. Check logs for script execution messages

### Issue 3: Console Opens But Commands Don't Execute

**Symptoms:** Can type but nothing happens when pressing Enter

**Causes:**
1. Script evaluator not initialized
2. Exception in command execution

**Solutions:**
1. Check logs for "Evaluating code" messages
2. Look for exceptions in the log

### Issue 4: Keyboard Layout Issues

Different keyboards map the tilde/grave key differently:

**US Keyboard:** `` ` `` is below Esc, Shift+`` ` `` = `~`
**UK Keyboard:** `` ` `` might be different location
**International:** May vary

**Workaround:**
We've added support for multiple keys. Try:
- `` ` `` (grave/backtick)
- `~` (Shift + grave)
- `'` (single quote - added as backup)

## Debug Logging

### Enable Detailed Logging

To see what keys are being pressed, you can add this temporarily:

1. Open `ConsoleSystem.cs`
2. In `HandleInput()`, add at the top:
```csharp
_logger.LogDebug("Pressed keys: {Keys}",
    string.Join(", ", currentKeyboard.GetPressedKeys()));
```

3. Run again and check logs to see what key codes are detected

### Check Console State

The console logs its state changes:
```
[INFO] Console toggled: True   // Console opened
[INFO] Console toggled: False  // Console closed
```

## Manual Test Checklist

- [ ] Game starts in DEBUG mode
- [ ] Log shows "DEBUG BUILD: Initializing debug console..."
- [ ] Log shows "=== DEBUG CONSOLE READY ==="
- [ ] Pressing ` or ~ toggles a dark overlay
- [ ] Can see "PokeSharp Debug Console" text at top
- [ ] Can see `>` prompt at bottom
- [ ] Can type characters (cursor moves)
- [ ] Pressing Enter executes command
- [ ] Output appears above input line
- [ ] Up arrow recalls previous command
- [ ] Escape closes console

## Still Not Working?

1. **Check you're running DEBUG build:**
   ```bash
   dotnet run --project PokeSharp.Game --configuration Debug
   ```
   NOT Release!

2. **Verify console system is compiled in:**
   ```bash
   strings bin/Debug/net9.0/PokeSharp.Game.dll | grep "DEBUG BUILD"
   ```
   Should show the message.

3. **Check for exceptions in logs:**
   ```bash
   grep -i "exception\|error" game_run.log | grep -i console
   ```

4. **Try the alternative keys:**
   - Regular backtick: `` ` ``
   - With Shift: `~`
   - Single quote: `'`
   - Try Fn key combinations if on laptop

5. **Verify window has focus:**
   Click on the game window before pressing the key.

## Next Steps

If still not working, please share:
1. Full output from `game_run.log` (especially around initialization)
2. Screenshot of the game window
3. Your keyboard layout (US, UK, etc.)
4. Operating system (though we know it's macOS)

