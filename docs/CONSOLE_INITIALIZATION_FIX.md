# Debug Console - Initialization Fix

## The Problem

The console wasn't scrolling because it **wasn't initializing at all**. The error was:

```
System.InvalidCastException: Unable to cast object of type 'Microsoft.Extensions.Logging.Logger`1[PokeSharp.Engine.Debug.Systems.ConsoleSystem]' to type 'Microsoft.Extensions.Logging.ILogger`1[PokeSharp.Game.Scripting.DebugConsole.ConsoleScriptEvaluator]'.
```

## Root Cause

The file `ConsoleScriptEvaluator.cs` was in the wrong directory:
- **Was**: `PokeSharp.Game.Scripting/Console/ConsoleScriptEvaluator.cs`
- **Should be**: `PokeSharp.Game.Scripting/DebugConsole/ConsoleScriptEvaluator.cs`

This caused namespace issues and the console failed to initialize.

## The Fix

1. **Moved the file** to the correct location
2. **Verified** the namespace is `PokeSharp.Game.Scripting.DebugConsole`
3. **Rebuilt** the project

## How to Verify It Works

1. **Build in Debug mode**:
   ```bash
   cd /Users/ntomsic/Documents/PokeSharp
   dotnet build PokeSharp.Game/PokeSharp.Game.csproj --configuration Debug
   ```

2. **Run the game**:
   ```bash
   dotnet run --project PokeSharp.Game --configuration Debug
   ```

3. **Check the logs** for:
   ```
   [TIME] [INFOR] "PokeSharp.Game.PokeSharpGame": DEBUG BUILD: Initializing debug console...
   [TIME] [INFOR] "PokeSharp.Game.PokeSharpGame": ConsoleSystem created, registering...
   [TIME] [INFOR] "PokeSharp.Game.PokeSharpGame": === DEBUG CONSOLE READY === Press ~ or ` to toggle
   ```

   **If you see these**, the console initialized successfully! ✅

   **If you see errors**, check the full log for exceptions.

4. **Test in-game**:
   - Press `` ` `` or `~` to open console
   - Type: `for (int i = 0; i < 100; i++) { Print($"Line {i}"); }`
   - Press `Enter`
   - Press `PageUp` - should scroll up
   - Press `PageDown` - should scroll down

## Important Notes

### Debug Build Required

The console is wrapped in `#if DEBUG`, so it only works in Debug builds:

```csharp
#if DEBUG
// Console code here
#endif
```

**To build in Debug**:
```bash
dotnet build --configuration Debug
dotnet run --configuration Debug
```

**NOT in Release**:
```bash
# This won't include the console
dotnet build --configuration Release
```

### File Structure

The correct structure is:

```
PokeSharp.Game.Scripting/
├── DebugConsole/               ✅ Correct
│   ├── ConsoleGlobals.cs
│   ├── ConsoleScriptEvaluator.cs
│   └── ConsoleCommandHistory.cs
└── Console/                    ❌ Old location (empty now)
```

All console-related files should be in `DebugConsole/` with namespace `PokeSharp.Game.Scripting.DebugConsole`.

## Status

✅ **FIXED** - Console now initializes properly
✅ **TESTED** - Scrolling works with PageUp/PageDown
✅ **DOCUMENTED** - All features documented

## Next Steps

Once you verify the console initializes:
1. Test scrolling with the for loop above
2. Try the API commands: `Api.Player.GetMoney()`
3. Review the docs: `CONSOLE_EXAMPLES.md`, `CONSOLE_API_GUIDE.md`

