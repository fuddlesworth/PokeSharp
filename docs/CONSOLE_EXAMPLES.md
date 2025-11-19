# Debug Console - Example Commands

This document provides practical examples of using the debug console for debugging and testing PokeSharp.

## Table of Contents
- [Basic Commands](#basic-commands)
- [Built-in Console Commands](#built-in-console-commands)
- [Global Objects](#global-objects)
- [Helper Methods](#helper-methods)
- [Entity Queries](#entity-queries)
- [Component Access](#component-access)
- [System Access](#system-access)
- [Scripting API Usage](#scripting-api-usage)
- [Advanced Examples](#advanced-examples)

> **💡 For detailed API usage, see [Console API Guide](CONSOLE_API_GUIDE.md)**

## Basic Commands

### Console Control
```csharp
help       // Show console commands
Help()     // Show global helper methods
clear      // Clear console output
reset      // Reset script state (clears all variables)
```

## Built-in Console Commands

### Special Commands
- `help` - Shows available console commands
- `clear` - Clears the console output
- `reset` - Resets the Roslyn script state (clears variables between executions)

## Global Objects

The console provides access to these global objects:

### World
```csharp
// The ECS World instance
World.CountEntities()  // Count all entities
```

### Systems
```csharp
// The SystemManager instance
Systems.GetUpdateSystems()  // Get all update systems
Systems.GetRenderSystems()  // Get all render systems
```

### Api
```csharp
// Scripting API provider
Api.Player.GetEntity()    // Get player entity
Api.World.GetEntities()   // Get all entities
```

### Graphics
```csharp
// MonoGame GraphicsDevice
Graphics.Viewport.Width   // Get viewport width
Graphics.Viewport.Height  // Get viewport height
```

## Helper Methods

### Print
```csharp
Print("Hello, Console!")                    // Print string
Print(CountEntities())                      // Print number
Print(GetPlayer())                          // Print entity
```

### Entity Counting
```csharp
CountEntities()                             // Count all entities
CountEntitiesWith<Position>()               // Count entities with Position component
```

### Entity Retrieval
```csharp
GetPlayer()                                 // Get player entity (returns Entity?)
GetAllEntities()                            // Get all entities (returns IEnumerable<Entity>)
GetEntitiesWith<Position>()                 // Get entities with specific component
```

### Entity Inspection
```csharp
var player = GetPlayer();
Inspect(player.Value)                       // Show entity's components

ListEntities()                              // List all entities with IDs
```

### Performance Info
```csharp
GetPerformanceInfo()                        // Get viewport and performance info
```

## Entity Queries

### Finding Entities by Component
```csharp
// Find all entities with Position component
var positioned = GetEntitiesWith<PokeSharp.Game.Components.Movement.Position>();
Print($"Found {positioned.Count()} entities with Position");

// Find all entities with Sprite component
var sprites = GetEntitiesWith<PokeSharp.Game.Components.Rendering.Sprite>();
Print($"Found {sprites.Count()} sprite entities");
```

### Counting Entities
```csharp
// Count total entities
var total = CountEntities();
Print($"Total entities: {total}");

// Count entities with specific component
var playerCount = CountEntitiesWith<PokeSharp.Game.Components.Player.Player>();
Print($"Player entities: {playerCount}");
```

## Component Access

### Reading Component Data
```csharp
// Get player and access component
var player = GetPlayer();
if (player.HasValue)
{
    if (player.Value.TryGet<PokeSharp.Game.Components.Movement.Position>(out var pos))
    {
        Print($"Player position: ({pos.X}, {pos.Y})");
    }
}
```

### Modifying Component Data
```csharp
// Modify player position
var player = GetPlayer();
if (player.HasValue)
{
    ref var pos = ref player.Value.Get<PokeSharp.Game.Components.Movement.Position>();
    pos.X = 100;
    pos.Y = 100;
    Print("Player teleported to (100, 100)");
}
```

## System Access

### Getting Systems
```csharp
// Access specific systems through SystemManager
var updateSystems = Systems.GetUpdateSystems();
Print($"Update systems: {updateSystems.Count()}");

var renderSystems = Systems.GetRenderSystems();
Print($"Render systems: {renderSystems.Count()}");
```

## Scripting API Usage

Your game's Roslyn scripting API is available through the **`Api`** global object.

### Quick Examples

```csharp
// Player operations
var name = Api.Player.GetPlayerName();
Print($"Player: {name}");

Api.Player.GiveMoney(1000);
Print($"Money: ${Api.Player.GetMoney()}");

// Game state flags
Api.GameState.SetFlag("test_flag", true);
bool hasFlag = Api.GameState.GetFlag("test_flag");
Print($"Flag set: {hasFlag}");

// Show dialogue
Api.Dialogue.ShowMessage("Hello from console!", "Debug");

// Random numbers
var roll = Api.GameState.RandomRange(1, 7);
Print($"Dice roll: {roll}");
```

### Available APIs

- **`Api.Player`** - Player operations (money, position, movement)
- **`Api.GameState`** - Flags, variables, and random numbers
- **`Api.Dialogue`** - Show messages and dialogue
- **`Api.Map`** - Map queries and transitions
- **`Api.Npc`** - NPC management
- **`Api.Effects`** - Visual effects

> **📖 For comprehensive API documentation with examples, see [Console API Guide](CONSOLE_API_GUIDE.md)**

## Advanced Examples

### Complex Queries
```csharp
// Find all entities at a specific position
var entities = GetAllEntities();
foreach (var entity in entities)
{
    if (entity.TryGet<PokeSharp.Game.Components.Movement.Position>(out var pos))
    {
        if (pos.X == 10 && pos.Y == 10)
        {
            Print($"Entity {entity.Id} is at (10, 10)");
            Inspect(entity);
        }
    }
}
```

### Creating Entities
```csharp
// Create a new entity with components
using PokeSharp.Game.Components.Movement;
using PokeSharp.Game.Components.Rendering;

var newEntity = World.Create(
    new Position { X = 50, Y = 50 },
    new Velocity { X = 1, Y = 0 }
);
Print($"Created entity {newEntity.Id}");
```

### Removing Entities
```csharp
// Find and destroy entities
var enemies = GetEntitiesWith<PokeSharp.Game.Components.Enemy>();
foreach (var enemy in enemies)
{
    World.Destroy(enemy);
}
Print($"Destroyed all enemies");
```

### Debugging Movement
```csharp
// Track player movement
var player = GetPlayer();
if (player.HasValue)
{
    var pos = player.Value.Get<PokeSharp.Game.Components.Movement.Position>();
    var vel = player.Value.Get<PokeSharp.Game.Components.Movement.Velocity>();
    Print($"Position: ({pos.X}, {pos.Y})");
    Print($"Velocity: ({vel.X}, {vel.Y})");
}
```

### State Inspection
```csharp
// Inspect game state
Print("=== Game State ===");
Print($"Entities: {CountEntities()}");
Print($"Players: {CountEntitiesWith<PokeSharp.Game.Components.Player.Player>()}");
Print($"Viewport: {Graphics.Viewport.Width}x{Graphics.Viewport.Height}");
Print("==================");
```

### Multi-line Scripts
```csharp
// You can write multi-line scripts
var totalEntities = CountEntities();
var playerCount = CountEntitiesWith<PokeSharp.Game.Components.Player.Player>();
var ratio = (float)playerCount / totalEntities * 100;
Print($"Players: {playerCount}/{totalEntities} ({ratio:F2}%)");
```

### Using Variables Across Commands
```csharp
// First command - store entity
var myEntity = GetPlayer();

// Second command - use stored entity
Inspect(myEntity.Value);

// Third command - modify stored entity
ref var pos = ref myEntity.Value.Get<PokeSharp.Game.Components.Movement.Position>();
pos.X += 10;
Print($"Moved entity to {pos.X}, {pos.Y}");

// Reset when done
reset  // Clears myEntity variable
```

## Tips & Tricks

### Keyboard Shortcuts

**Console Control:**
- **`` ` ``** or **`~`** - Toggle console
- **`Enter`** - Execute command
- **`Escape`** - Close console

**Scrolling Output:**
- **`PageUp`** - Scroll output up one page
- **`PageDown`** - Scroll output down one page
- **`Ctrl+Home`** - Jump to top of output
- **`Ctrl+End`** - Jump to bottom of output

**Command History:**
- **`Up Arrow`** - Previous command in history
- **`Down Arrow`** - Next command in history

**Text Editing:**
- **`Left/Right Arrow`** - Move cursor
- **`Home/End`** - Jump to start/end of input line
- **`Backspace/Delete`** - Remove characters

### Color Coding
- **Cyan** - Your input commands
- **Light Green** - Successful output / Print() output
- **Red** - Error messages
- **Yellow** - Help headers
- **Light Gray** - Help descriptions
- **White** - Default text

### Common Patterns

#### Quick Entity Count
```csharp
CountEntities()
```

#### Find Player Quickly
```csharp
var p = GetPlayer(); Inspect(p.Value);
```

#### List All Component Types
```csharp
var player = GetPlayer();
foreach (var comp in player.Value.GetAllComponents())
{
    Print(comp.GetType().Name);
}
```

#### Teleport Player
```csharp
ref var pos = ref GetPlayer().Value.Get<PokeSharp.Game.Components.Movement.Position>();
pos.X = 100; pos.Y = 100;
```

## Troubleshooting

### "Entity is not alive"
The entity has been destroyed. Use `GetPlayer()` or `GetAllEntities()` to get a fresh reference.

### "Cannot resolve type"
Use fully qualified type names:
```csharp
GetEntitiesWith<PokeSharp.Game.Components.Movement.Position>()
```

### "The name 'X' does not exist in the current context"
Use `reset` to clear script state, then redefine your variables.

### Script Errors
Check for:
- Missing semicolons
- Incorrect type names
- Trying to access components that don't exist on an entity

## See Also
- [Console Implementation Guide](QUAKE_CONSOLE_IMPLEMENTATION.md)
- [Console Troubleshooting](CONSOLE_TROUBLESHOOTING.md)

