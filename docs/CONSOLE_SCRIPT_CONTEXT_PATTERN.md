# Console Scripts - ScriptContext Pattern

## Overview

Console scripts now use the **ScriptContext pattern** - the same pattern used by NPC behavior scripts. This provides consistency across all scripting in the game.

## What Changed

### ❌ Old Pattern (Before)
```csharp
// Access APIs through nested provider
var money = Api.Player.GetMoney();
Api.Map.TransitionToMap(1, 10, 10);
Api.GameState.SetFlag("test", true);

// World.CountEntities() required parameters
var count = World.CountEntities(); // ❌ ERROR: Missing QueryDescription
```

### ✅ New Pattern (Now)
```csharp
// Direct access to service classes (same as NPC behaviors)
var money = Player.GetMoney();
Map.TransitionToMap(1, 10, 10);
GameState.SetFlag("test", true);

// Use helper methods for World queries
var count = CountEntities(); // ✅ Works correctly
```

## Why This Change?

1. **Consistency**: NPC behavior scripts already use this pattern
2. **Simplicity**: Less typing, cleaner code
3. **Correctness**: Helper methods handle complex ECS queries properly
4. **Discoverability**: Auto-complete shows methods directly

## Available Globals

Console scripts now have access to:

```csharp
// Core Objects
World       // Arch.Core.World - ECS World instance
Systems     // SystemManager
Graphics    // GraphicsDevice
Logger      // ILogger

// API Services (Direct Access - ScriptContext Pattern)
Player      // PlayerApiService
Npc         // NpcApiService
Map         // MapApiService
GameState   // GameStateApiService
Dialogue    // DialogueApiService
Effects     // EffectApiService

// Helper Methods
Print(msg)              // Output to console
CountEntities()         // Count all entities (handles QueryDescription)
GetPlayer()             // Get player entity
GetEntitiesWith<T>()    // Get entities with component
Inspect(entity)         // Show entity components
ListEntities()          // List all entities
Help()                  // Show help
```

## Migration Guide

If you have existing console scripts, update them:

### Player API
```csharp
// Before
Api.Player.GetMoney()
Api.Player.GetPlayerPosition()
Api.Player.SetPlayerMovementLocked(true)

// After
Player.GetMoney()
Player.GetPlayerPosition()
Player.SetPlayerMovementLocked(true)
```

### Map API
```csharp
// Before
Api.Map.GetCurrentMapId()
Api.Map.TransitionToMap(1, 10, 10)

// After
Map.GetCurrentMapId()
Map.TransitionToMap(1, 10, 10)
```

### GameState API
```csharp
// Before
Api.GameState.SetFlag("test", true)
Api.GameState.GetVariable("score")

// After
GameState.SetFlag("test", true)
GameState.GetVariable("score")
```

### World Queries
```csharp
// Before (BROKEN)
var count = World.CountEntities(); // ❌ Missing parameter

// After (WORKS)
var count = CountEntities(); // ✅ Uses helper method
```

## Example Scripts

All example scripts have been updated:
- `example.csx` - Auto-generated, uses new pattern
- `debug-info.csx` - System diagnostics
- `teleport-player.csx` - Teleport template
- `quick-test.csx` - Money testing

Load any of these to see the new pattern in action!

## Technical Details

### ConsoleGlobals Class

The `ConsoleGlobals` class now matches `ScriptContext` from NPC behaviors:

```csharp
public class ConsoleGlobals
{
    private readonly IScriptingApiProvider _apis;

    public ConsoleGlobals(
        IScriptingApiProvider apis,
        World world,
        SystemManager systems,
        GraphicsDevice graphics,
        ILogger logger)
    {
        _apis = apis;
        World = world;
        Systems = systems;
        Graphics = graphics;
        Logger = logger;
    }

    // Direct API access (ScriptContext pattern)
    public PlayerApiService Player => _apis.Player;
    public NpcApiService Npc => _apis.Npc;
    public MapApiService Map => _apis.Map;
    public GameStateApiService GameState => _apis.GameState;
    public DialogueApiService Dialogue => _apis.Dialogue;
    public EffectApiService Effects => _apis.Effects;

    // Helper methods
    public void Print(object obj) { ... }
    public int CountEntities() { ... }
    // ... more helpers ...
}
```

### Why Helper Methods?

Some World methods require complex parameters:

```csharp
// Direct World call (complex)
var query = new QueryDescription();
var count = World.CountEntities(in query); // Requires QueryDescription

// Helper method (simple)
var count = CountEntities(); // Handles QueryDescription internally
```

Helper methods wrap these complex calls for convenience.

## Benefits

1. **Same as NPC Behaviors**: One pattern to learn
2. **Less Typing**: `Player.GetMoney()` vs `Api.Player.GetMoney()`
3. **Better Auto-Complete**: Direct access to service methods
4. **Correct by Default**: Helper methods handle ECS complexity
5. **More Discoverable**: Type `Player.` and see all methods

## Related Documentation

- `/Scripts/API_REFERENCE.md` - Complete API reference
- `/docs/CONSOLE_SCRIPT_LOADING.md` - Script loading/saving guide
- `PokeSharp.Game.Scripting/Runtime/ScriptContext.cs` - NPC behavior pattern

## Questions?

Type `Help()` in the console to see all available methods and examples!

