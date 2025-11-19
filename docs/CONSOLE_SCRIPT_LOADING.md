# Console Script Loading & Saving (Phase 3)

## Overview

The debug console now supports loading and saving C# script files (`.csx`), allowing you to:
- **Save repetitive debugging tasks** as reusable scripts
- **Build a library** of common debug operations
- **Share scripts** with team members
- **Quickly iterate** on complex debugging scenarios

## Commands

### `load <filename> [args...]`
Loads and executes a `.csx` script file from the Scripts directory. Optionally pass arguments to the script.

**Examples:**
```
load example.csx
load debug-info
load teleport 10 20              # Pass x, y coordinates
load give-money 1000             # Pass amount
load teleport-player.csx
```

**Note:** The `.csx` extension is optional.

**Script Arguments:**
Scripts can access arguments via the `Args` array:
```csharp
// In your script:
if (Args.Length < 2)
{
    Print("Usage: load script.csx <x> <y>");
    return;
}

int x = int.Parse(Args[0]);
int y = int.Parse(Args[1]);
Map.TransitionToMap(Map.GetCurrentMapId(), x, y);
Print($"Teleported to ({x}, {y})");
```

### `save <filename>`
Saves the current console input to a `.csx` file.

**Examples:**
```
save my-debug-script.csx
save quick-teleport
```

**Workflow:**
1. Type your C# code in the console (use `Shift+Enter` for multi-line)
2. Test it by pressing `Enter` to execute
3. If it works, press `Up` to recall it
4. Type `save my-script` to save it

### `scripts`
Lists all available script files in the Scripts directory.

**Example output:**
```
=== Available Scripts ===
Directory: /path/to/game/Scripts

  📄 example.csx
  📄 debug-info.csx
  📄 teleport-player.csx
  📄 quick-test.csx
  📄 startup.csx

Found 5 script(s)
Load with: load <filename>
```

### `startup.csx` (Auto-Load)
A special script that runs automatically when the console initializes.

**Features:**
- Runs silently on console startup (no output unless the script uses `Print()`)
- Perfect for setting up common variables, helper functions, or debugging shortcuts
- Define once, available in every console session
- Can be disabled: Set `console.Config.AutoLoadStartupScript = false`

**Example use cases:**
```csharp
// Define helper functions
void TP(int x, int y) {
    Map.TransitionToMap(Map.GetCurrentMapId(), x, y);
    Print($"Teleported to ({x}, {y})");
}

void GM(int amount) {
    Player.GiveMoney(amount);
    Print($"Gave ${amount}");
}

// Set up common variables
var currentMap = Map.GetCurrentMapId();
var playerPos = Player.GetPlayerPosition();

// Auto-enable logging
// Config.LoggingEnabled = true;
```

Then in the console, just type `TP(10, 10)` or `GM(1000)`!

## Parameterized Scripts

Scripts can accept command-line style arguments for flexible, reusable debugging tools.

### Creating a Parameterized Script

```csharp
// teleport.csx - A parameterized teleport script
// Usage: load teleport.csx <x> <y> [mapId]

// Check arguments
if (Args.Length < 2)
{
    Print("Usage: load teleport <x> <y> [mapId]");
    return;
}

// Parse arguments
int x = int.Parse(Args[0]);
int y = int.Parse(Args[1]);
int mapId = Args.Length >= 3 ? int.Parse(Args[2]) : Map.GetCurrentMapId();

// Execute
Map.TransitionToMap(mapId, x, y);
Print($"✨ Teleported to ({x}, {y}) on map {mapId}");
```

### Using Parameterized Scripts

```bash
# Load with arguments
load teleport 10 20          # Teleport to (10, 20) on current map
load teleport 15 25 2        # Teleport to (15, 25) on map 2
load give-money 1000         # Give $1000
```

### Best Practices

1. **Validate arguments**: Always check `Args.Length` and parse safely
2. **Show usage**: Print help message if arguments are missing
3. **Provide defaults**: Use optional arguments with sensible defaults
4. **Error handling**: Use `TryParse` to handle invalid inputs gracefully

**Example with validation:**
```csharp
// give-money.csx
if (Args.Length < 1)
{
    Print("Usage: load give-money <amount>");
    return;
}

if (!int.TryParse(Args[0], out int amount) || amount <= 0)
{
    Print($"Error: Invalid amount '{Args[0]}'");
    return;
}

Player.GiveMoney(amount);
Print($"✅ Gave ${amount}");
```

## Scripts Directory

Scripts are stored in **two locations**:

### 1. **Source Location** (for version control)
Store your scripts in the repo root for version control:
```
PokeSharp/
├── Scripts/              ← Store scripts here (version controlled)
│   ├── example.csx
│   ├── debug-info.csx
│   ├── teleport-player.csx
│   └── quick-test.csx
├── PokeSharp.Game/
└── ...
```

### 2. **Runtime Location** (where the game reads them)
The build process automatically copies scripts to:
```
PokeSharp.Game/bin/Debug/net9.0/Scripts/
```

**Workflow:**
1. Create/edit scripts in `PokeSharp/Scripts/` (repo root)
2. Build the project (scripts are auto-copied)
3. Or edit directly in the `bin/Debug/net9.0/Scripts/` folder for quick testing
4. Use `save` command in the console to create new scripts at runtime

**Note:** Scripts saved via the console are created in the runtime location. Copy them to the repo root if you want to commit them to version control.

You can also edit `.csx` files directly in your favorite text editor (VS Code, Rider, etc.) and reload them in the console.

## Example Scripts

### 1. **example.csx** (Auto-generated)
Basic example showing player info and common API usage.

### 2. **debug-info.csx**
Comprehensive system information:
- Player position and money
- Entity counts
- Graphics device info

### 3. **teleport-player.csx**
Quick teleport template for common locations.

### 4. **quick-test.csx**
Template for rapid iteration on test code.

## Writing Your Own Scripts

Scripts have access to the same globals as the console:

```csharp
// Available globals:
World       // Arch ECS World
Systems     // SystemManager
Api         // IScriptingApiProvider (Player, GameState, Dialogue, etc.)
Graphics    // GraphicsDevice
Print()     // Output to console

// Example: Player info script
var player = Api.Player.GetEntity();
var transform = player.Get<TransformComponent>();
Print($"Player at ({transform.X}, {transform.Y})");

// Example: Entity query
var entityCount = World.CountEntities();
Print($"Total entities: {entityCount}");

// Example: Give items
Api.Player.GiveMoney(1000);
Print("Gave player $1000");
```

## Advanced Usage

### Multi-line Scripts with Variables

Scripts maintain **state** across executions. Variables you define persist until you run `reset`:

```csharp
// First execution
var player = Api.Player.GetEntity();

// Later executions can use 'player'
var transform = player.Get<TransformComponent>();
Print($"Position: {transform.X}, {transform.Y}");
```

Save this workflow as a script for quick re-execution!

### Script Templates

Create template scripts for common patterns:

**check-player.csx:**
```csharp
if (!Api.Player.GetEntity().IsAlive())
{
    Print("ERROR: Player not found!");
    return;
}
Print("✅ Player entity valid");
```

**test-api.csx:**
```csharp
// Template for testing API methods
Print($"Testing: {nameof(Api.Player.GetMoney)}");
var result = Api.Player.GetMoney();
Print($"Result: {result}");
```

### Parameterized Scripts

Since scripts are just C# code, you can set variables before loading:

**In console:**
```csharp
var teleportX = 100;
var teleportY = 200;
load teleport-to-xy.csx
```

**teleport-to-xy.csx:**
```csharp
// Uses variables from console scope
if (teleportX == 0 || teleportY == 0)
{
    Print("ERROR: Set teleportX and teleportY first!");
    return;
}

Api.Player.Teleport(teleportX, teleportY);
Print($"Teleported to ({teleportX}, {teleportY})");
```

## Tips & Best Practices

### 1. **Organize Your Scripts**
Create a naming convention:
- `test-*.csx` - Testing scripts
- `debug-*.csx` - Debug info scripts
- `util-*.csx` - Utility operations

### 2. **Add Comments**
Future you will thank present you:
```csharp
// Teleport to Professor's Lab
// Use this when testing lab interactions
Api.Player.Teleport(200, 150);
```

### 3. **Error Handling**
Always check for null/invalid entities:
```csharp
var player = Api.Player.GetEntity();
if (!player.IsAlive())
{
    Print("ERROR: Player not found!");
    return;
}
```

### 4. **Use Print() Liberally**
Good feedback makes debugging easier:
```csharp
Print("Starting teleport sequence...");
Api.Player.Teleport(100, 100);
Print("✅ Teleport complete!");
```

### 5. **Iterate Quickly**
Edit script in VS Code → Alt+Tab to game → `load script.csx` → Test → Repeat

### 6. **Version Control**
Commit useful scripts to your repo:
```bash
git add Scripts/
git commit -m "Add debug scripts for player testing"
```

## Integration with Console Features

Scripts work seamlessly with other console features:

- **Auto-complete**: Works in loaded scripts
- **Syntax highlighting**: Applied when editing multi-line before saving
- **Command history**: `load` commands are saved in history
- **Multi-line**: Use `Shift+Enter` before saving complex scripts
- **Logging**: Script execution is logged for debugging

## Keyboard Workflow

**Fast iteration loop:**
1. `Ctrl+V` - Paste code from clipboard
2. `Enter` - Test it
3. `Up` - Recall
4. `save my-script` - Save it
5. Edit in VS Code
6. `load my-script` - Reload

**Browse and execute:**
1. `scripts` - See what's available
2. Arrow keys - Navigate output
3. `load <name>` - Execute

## FAQ

**Q: Can I use `using` statements in scripts?**
A: Not currently. Scripts are executed as expressions/statements in the existing Roslyn context. However, all MonoGame and Arch types are already imported.

**Q: Can scripts call other scripts?**
A: Not directly, but you can define functions in one script and call them in another since script state persists.

**Q: What happens if a script throws an exception?**
A: The error is displayed in the console with a red color, and script state is preserved (not reset).

**Q: Can I delete scripts from the console?**
A: Not yet, but you can delete them from the file system directly.

**Q: Do scripts have access to private fields?**
A: No, only public APIs exposed through the `Api` provider and public members of globals.

## Next Steps

- Check out the example scripts in the `Scripts/` folder
- Try saving your next debugging session with `save`
- Build up a personal library of useful debugging tools

Happy debugging! 🚀

