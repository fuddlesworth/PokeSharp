# Hello World Tutorial

Your first PokeSharp.Scripting script in 15 minutes! This tutorial walks you through creating a simple wandering NPC behavior from scratch.

## Prerequisites

- PokeSharp.Scripting installed (see [README.md](../../README.md))
- Basic C# knowledge
- A MonoGame project with ECS integration

## What You'll Build

A simple NPC that:
- Moves in a random direction every 2 seconds
- Logs its position to the console
- Can be edited and hot-reloaded while the game runs

## Step 1: Create the Script File

Create a new file: `Content/Scripts/WanderingNPC.cs`

```csharp
using Microsoft.Extensions.Logging;
using Microsoft.Xna.Framework;
using PokeSharp.Scripting;
using PokeSharp.Core.Components;

public class WanderingNPC : TypeScriptBase
{
    // TODO: We'll add code here!
}

new WanderingNPC()
```

**Important**: The last line `new WanderingNPC()` is required! This tells the script engine to return an instance.

## Step 2: Understanding TypeScriptBase

`TypeScriptBase` provides four lifecycle hooks:

```csharp
public abstract class TypeScriptBase
{
    // Called once when script loads
    protected virtual void OnInitialize(ScriptContext ctx) { }

    // Called when attached to an entity
    public virtual void OnActivated(ScriptContext ctx) { }

    // Called every frame
    public virtual void OnTick(ScriptContext ctx, float deltaTime) { }

    // Called when removed from entity
    public virtual void OnDeactivated(ScriptContext ctx) { }
}
```

For our wandering NPC, we'll use `OnActivated` (to log when attached) and `OnTick` (to move around).

## Step 3: Add Initialization

Override `OnActivated` to log when the script starts:

```csharp
public class WanderingNPC : TypeScriptBase
{
    public override void OnActivated(ScriptContext ctx)
    {
        ctx.Logger.LogInformation("Wandering NPC activated on entity {EntityId}", ctx.Entity?.Id);

        // Initialize wandering state
        ctx.SetState("timeSinceDirectionChange", 0.0f);
        ctx.SetState("currentDirection", Direction.Down);
    }
}
```

**Key Points**:
- `ctx.Logger` gives you logging (appears in console)
- `ctx.Entity?.Id` is the entity this script is attached to
- `ctx.SetState` stores per-entity data (critical for stateless scripts!)

## Step 4: Understanding ScriptContext

`ScriptContext` is your gateway to the ECS world:

```csharp
public sealed class ScriptContext
{
    // Access the ECS world
    public World World { get; }

    // The entity this script is running on (null for global scripts)
    public Entity? Entity { get; }

    // Logger for this script
    public ILogger Logger { get; }

    // Check if this is an entity-level script
    public bool IsEntityScript { get; }

    // Get/set per-entity state (CRITICAL for stateless scripts!)
    public void SetState<T>(string key, T value)
    public T GetState<T>(string key)
    public bool TryGetState<T>(string key, out T value)

    // Component access
    public ref T GetState<T>() where T : struct
    public bool TryGetState<T>(out T state) where T : struct

    // Convenience properties
    public ref Position Position { get; }
    public bool HasPosition { get; }
}
```

## Step 5: Implement Movement Logic

Add the `OnTick` method to make the NPC move:

```csharp
public class WanderingNPC : TypeScriptBase
{
    private const float DIRECTION_CHANGE_INTERVAL = 2.0f;  // Change direction every 2 seconds
    private const int MOVE_SPEED = 32;  // Pixels per second

    public override void OnActivated(ScriptContext ctx)
    {
        ctx.Logger.LogInformation("Wandering NPC activated on entity {EntityId}", ctx.Entity?.Id);
        ctx.SetState("timeSinceDirectionChange", 0.0f);
        ctx.SetState("currentDirection", Direction.Down);
    }

    public override void OnTick(ScriptContext ctx, float deltaTime)
    {
        // Check if entity has a Position component
        if (!ctx.HasPosition)
        {
            ctx.Logger.LogWarning("Entity {Id} has no Position component!", ctx.Entity?.Id);
            return;
        }

        // Get wandering state
        var timeSinceChange = ctx.GetState<float>("timeSinceDirectionChange");
        var currentDirection = ctx.GetState<Direction>("currentDirection");

        // Update timer
        timeSinceChange += deltaTime;

        // Change direction every DIRECTION_CHANGE_INTERVAL seconds
        if (timeSinceChange >= DIRECTION_CHANGE_INTERVAL)
        {
            currentDirection = PickRandomDirection();
            timeSinceChange = 0.0f;

            ctx.Logger.LogDebug("NPC {Id} changed direction to {Direction}",
                ctx.Entity?.Id, currentDirection);
        }

        // Save updated state
        ctx.SetState("timeSinceDirectionChange", timeSinceChange);
        ctx.SetState("currentDirection", currentDirection);

        // Move the entity
        MoveInDirection(ctx, currentDirection, MOVE_SPEED * deltaTime);
    }

    private Direction PickRandomDirection()
    {
        // Pick one of the four cardinal directions
        return RandomRange(0, 4) switch
        {
            0 => Direction.Up,
            1 => Direction.Down,
            2 => Direction.Left,
            _ => Direction.Right
        };
    }

    private void MoveInDirection(ScriptContext ctx, Direction direction, float distance)
    {
        // Get reference to Position component (zero-allocation!)
        ref var pos = ref ctx.Position;

        // Update position based on direction
        switch (direction)
        {
            case Direction.Up:
                pos.Y -= (int)distance;
                break;
            case Direction.Down:
                pos.Y += (int)distance;
                break;
            case Direction.Left:
                pos.X -= (int)distance;
                break;
            case Direction.Right:
                pos.X += (int)distance;
                break;
        }

        ctx.Logger.LogTrace("NPC at ({X}, {Y})", pos.X, pos.Y);
    }
}
```

## Step 6: Complete Script

Here's the full script in one piece:

```csharp
using Microsoft.Extensions.Logging;
using Microsoft.Xna.Framework;
using PokeSharp.Scripting;
using PokeSharp.Core.Components;

/// <summary>
/// Makes an NPC wander around randomly, changing direction every 2 seconds.
/// </summary>
public class WanderingNPC : TypeScriptBase
{
    private const float DIRECTION_CHANGE_INTERVAL = 2.0f;
    private const int MOVE_SPEED = 32;  // Pixels per second

    public override void OnActivated(ScriptContext ctx)
    {
        ctx.Logger.LogInformation("Wandering NPC activated on entity {EntityId}", ctx.Entity?.Id);

        // Initialize state
        ctx.SetState("timeSinceDirectionChange", 0.0f);
        ctx.SetState("currentDirection", Direction.Down);
    }

    public override void OnTick(ScriptContext ctx, float deltaTime)
    {
        if (!ctx.HasPosition)
        {
            ctx.Logger.LogWarning("Entity has no Position component!");
            return;
        }

        // Get state
        var timeSinceChange = ctx.GetState<float>("timeSinceDirectionChange");
        var currentDirection = ctx.GetState<Direction>("currentDirection");

        // Update timer
        timeSinceChange += deltaTime;

        // Change direction periodically
        if (timeSinceChange >= DIRECTION_CHANGE_INTERVAL)
        {
            currentDirection = PickRandomDirection();
            timeSinceChange = 0.0f;
            ctx.Logger.LogDebug("Changed direction to {Direction}", currentDirection);
        }

        // Save state
        ctx.SetState("timeSinceDirectionChange", timeSinceChange);
        ctx.SetState("currentDirection", currentDirection);

        // Move
        MoveInDirection(ctx, currentDirection, MOVE_SPEED * deltaTime);
    }

    private Direction PickRandomDirection()
    {
        return RandomRange(0, 4) switch
        {
            0 => Direction.Up,
            1 => Direction.Down,
            2 => Direction.Left,
            _ => Direction.Right
        };
    }

    private void MoveInDirection(ScriptContext ctx, Direction direction, float distance)
    {
        ref var pos = ref ctx.Position;

        switch (direction)
        {
            case Direction.Up:    pos.Y -= (int)distance; break;
            case Direction.Down:  pos.Y += (int)distance; break;
            case Direction.Left:  pos.X -= (int)distance; break;
            case Direction.Right: pos.X += (int)distance; break;
        }

        ctx.Logger.LogTrace("NPC at ({X}, {Y})", pos.X, pos.Y);
    }

    public override void OnDeactivated(ScriptContext ctx)
    {
        ctx.Logger.LogInformation("Wandering NPC deactivated");
    }
}

new WanderingNPC()
```

## Step 7: Testing Your Script

### Load the Script in Your Game

```csharp
// In your Game1.cs Initialize method
var scriptService = _services.GetRequiredService<ScriptService>();
var wanderingScript = await scriptService.LoadScriptAsync("WanderingNPC.cs");

if (wanderingScript != null)
{
    Console.WriteLine("Wandering NPC script loaded successfully!");
}
```

### Attach to an Entity

```csharp
// Create an NPC entity
var npc = _world.Create(
    new Position { X = 100, Y = 100 },
    new ScriptComponent { ScriptPath = "WanderingNPC.cs", IsActive = true }
);
```

### Run the Game

Press F5 and watch the console! You should see:

```
Wandering NPC activated on entity 1
Changed direction to Right
Changed direction to Up
Changed direction to Down
...
```

## Step 8: Hot-Reload Demonstration

Now for the magic! While your game is running:

1. Open `WanderingNPC.cs` in your editor
2. Change `DIRECTION_CHANGE_INTERVAL` to `0.5f` (faster direction changes)
3. Save the file

Watch the console - you'll see:

```
✓ Script reloaded: WanderingNPC v2 (compile: 87ms, total: 123ms)
```

The NPC will immediately start changing direction faster! No game restart needed.

### Try Breaking It

1. Add a syntax error (like removing a semicolon)
2. Save the file

You'll see:

```
✗ Script compilation FAILED: WanderingNPC (72ms)
  Line 45, Col 5: ; expected [CS1002]
↶ Rolled back WanderingNPC to version 1 via cache
⚠ Compilation failed - rolled back WanderingNPC
```

The NPC keeps working with the old version! Fix the error and save again - it'll reload successfully.

## Understanding Key Concepts

### 1. Stateless Scripts

**Why are scripts stateless?**

```csharp
// ❌ WRONG - This would be shared across ALL NPCs!
public class WanderingNPC : TypeScriptBase
{
    private float timeSinceChange = 0.0f;  // Shared state!
}

// ✅ CORRECT - Each NPC has its own state
public override void OnTick(ScriptContext ctx, float deltaTime)
{
    var timeSinceChange = ctx.GetState<float>("timeSinceDirectionChange");
}
```

**Why?**
- Single script instance is reused across all entities
- Allows hot-reloading to work seamlessly
- Better memory efficiency
- Thread-safe by design

### 2. ScriptContext State vs Components

**When to use `ctx.SetState` vs components?**

```csharp
// Use ctx.SetState for temporary, script-specific data
ctx.SetState("timeSinceDirectionChange", 2.5f);

// Use components for data shared with other systems
ref var health = ref ctx.GetState<Health>();
health.Current -= 10;
```

**Rule of thumb**:
- Script-only data → `ctx.SetState`
- Shared game data → ECS components

### 3. Component Access Patterns

```csharp
// Pattern 1: Safe access (returns false if missing)
if (ctx.TryGetState<Health>(out var health))
{
    // Use health
}

// Pattern 2: Assume exists (throws if missing)
ref var health = ref ctx.GetState<Health>();

// Pattern 3: Get or create
ref var timer = ref ctx.GetOrAddState<Timer>();

// Pattern 4: Check first
if (ctx.HasState<Health>())
{
    ref var health = ref ctx.GetState<Health>();
}
```

## Common Mistakes

### Mistake 1: Forgetting to Return Instance

```csharp
public class MyScript : TypeScriptBase
{
    // ... implementation
}

// ❌ MISSING: new MyScript()
```

**Fix**: Always end with `new YourScriptClass()`

### Mistake 2: Using Instance Fields

```csharp
// ❌ WRONG
public class MyScript : TypeScriptBase
{
    private int counter = 0;  // Shared across all entities!
}

// ✅ CORRECT
public override void OnTick(ScriptContext ctx, float deltaTime)
{
    var counter = ctx.GetState<int>("counter");
    ctx.SetState("counter", counter + 1);
}
```

### Mistake 3: Not Checking for Components

```csharp
// ❌ WRONG - Will crash if Position is missing
ref var pos = ref ctx.GetState<Position>();

// ✅ CORRECT - Check first
if (ctx.HasPosition)
{
    ref var pos = ref ctx.Position;
}
```

### Mistake 4: Forgetting deltaTime

```csharp
// ❌ WRONG - Movement speed depends on frame rate!
pos.X += MOVE_SPEED;

// ✅ CORRECT - Frame-rate independent
pos.X += (int)(MOVE_SPEED * deltaTime);
```

## Next Steps

Congratulations! You've created your first hot-reloadable script. Here's what to explore next:

### Easy Enhancements

1. **Add boundaries**: Stop the NPC at screen edges
2. **Add animations**: Change sprite based on direction
3. **Add collision**: Stop when hitting walls
4. **Add interaction**: Make the NPC respond to player proximity

### Example: Add Boundaries

```csharp
private void MoveInDirection(ScriptContext ctx, Direction direction, float distance)
{
    ref var pos = ref ctx.Position;

    // Calculate new position
    var newX = pos.X;
    var newY = pos.Y;

    switch (direction)
    {
        case Direction.Up:    newY -= (int)distance; break;
        case Direction.Down:  newY += (int)distance; break;
        case Direction.Left:  newX -= (int)distance; break;
        case Direction.Right: newX += (int)distance; break;
    }

    // Clamp to screen bounds (0-800 width, 0-600 height)
    newX = Math.Clamp(newX, 0, 800 - 32);  // 32 = sprite width
    newY = Math.Clamp(newY, 0, 600 - 32);  // 32 = sprite height

    pos.X = newX;
    pos.Y = newY;
}
```

### Advanced Topics

- [MonoGame Integration Guide](../MONOGAME-INTEGRATION.md) - Full integration setup
- Global scripts (no entity)
- Complex state machines
- Inter-entity communication
- Event-driven behaviors

## Troubleshooting

**Script not loading?**
- Check the console for compilation errors
- Ensure the file path is correct
- Verify the script ends with `new YourScriptClass()`

**Hot-reload not working?**
- Make sure hot-reload service is started
- Check file watcher is monitoring the right directory
- Look for watcher errors in the console

**NPC not moving?**
- Check entity has `Position` component
- Verify `ScriptComponent.IsActive` is true
- Look for warnings in the logger

**All NPCs moving in sync?**
- You're using instance fields instead of `ctx.SetState`
- Review the "Stateless Scripts" section above

## Summary

You've learned:

- ✓ How to create a TypeScriptBase script
- ✓ How to use ScriptContext for ECS access
- ✓ How to maintain per-entity state
- ✓ How to implement frame-rate independent movement
- ✓ How hot-reload works with automatic rollback
- ✓ Common mistakes and how to avoid them

**Remember**: Scripts are stateless, use `ctx.SetState` for per-entity data!

---

Ready for more? Check out the [MonoGame Integration Guide](../MONOGAME-INTEGRATION.md) for production-ready setup!
