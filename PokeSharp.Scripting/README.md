# PokeSharp.Scripting

A powerful C# scripting system for PokeSharp, enabling hot-reloadable game behavior through Roslyn compilation. Create custom NPC behaviors, item effects, and game logic without recompiling your game.

## Features

- **Hot-Reload Support**: Edit scripts while the game is running and see changes instantly
- **Automatic Rollback**: Failed compilations automatically revert to the last working version
- **Type-Safe ECS Integration**: Direct access to Arch ECS components with full IntelliSense
- **Stateless Architecture**: Scripts are reused across entities with per-entity state management
- **MonoGame Integration**: Seamless integration with MonoGame's Update/Draw loop
- **Cross-Platform**: Works on Windows, Linux, and macOS via .NET 9.0
- **Comprehensive Diagnostics**: Detailed compilation errors with line numbers

## Quick Start (5 Minutes)

### Installation

```bash
dotnet add package PokeSharp.Scripting
```

### Your First Script

Create a file `scripts/MyFirstScript.cs`:

```csharp
using Microsoft.Extensions.Logging;
using PokeSharp.Scripting;

public class MyFirstScript : TypeScriptBase
{
    protected override void OnActivated(ScriptContext ctx)
    {
        ctx.Logger.LogInformation("Script activated on entity {EntityId}", ctx.Entity?.Id);
    }

    public override void OnTick(ScriptContext ctx, float deltaTime)
    {
        // Get or create a counter for this entity
        var counter = ctx.GetState<int>("tickCounter");
        counter++;
        ctx.SetState("tickCounter", counter);

        if (counter % 60 == 0)  // Every 60 frames
        {
            ctx.Logger.LogInformation("Entity has been alive for {Seconds} seconds", counter / 60);
        }
    }
}

// Return an instance for the script engine
new MyFirstScript()
```

### Loading the Script

```csharp
// In your MonoGame game class
using PokeSharp.Scripting;
using Microsoft.Extensions.Logging;

public class Game1 : Game
{
    private ScriptService _scriptService;
    private object _myScript;

    protected override void Initialize()
    {
        base.Initialize();

        _scriptService = new ScriptService(
            scriptsBasePath: "Content/Scripts",
            logger: loggerFactory.CreateLogger<ScriptService>()
        );

        _myScript = await _scriptService.LoadScriptAsync("MyFirstScript.cs");
    }

    protected override void Update(GameTime gameTime)
    {
        if (_myScript is TypeScriptBase script)
        {
            var ctx = new ScriptContext(world, entity, logger);
            script.OnTick(ctx, (float)gameTime.ElapsedGameTime.TotalSeconds);
        }
    }
}
```

## Architecture Overview

```
┌─────────────────────────────────────────────────────────────┐
│                        Your Game                            │
│  ┌─────────────┐         ┌──────────────┐                  │
│  │   MonoGame  │────────▶│ ScriptService│                  │
│  │  Game Loop  │         └──────┬───────┘                  │
│  └─────────────┘                │                           │
│                                  │ Compiles & Caches        │
│                                  ▼                           │
│  ┌─────────────────────────────────────────────────────┐   │
│  │           Roslyn Script Engine (.csx)                │   │
│  │  ┌──────────────┐  ┌──────────────┐                 │   │
│  │  │ Script A.cs  │  │ Script B.cs  │  ← Hot-Reload   │   │
│  │  └──────┬───────┘  └──────┬───────┘                 │   │
│  └─────────┼──────────────────┼─────────────────────────┘   │
│            │                  │                             │
│            ▼                  ▼                             │
│  ┌──────────────────────────────────────┐                  │
│  │       TypeScriptBase Instances       │                  │
│  │  (OnActivated, OnTick, OnDeactivated)│                  │
│  └────────────────┬─────────────────────┘                  │
│                   │ Uses                                    │
│                   ▼                                         │
│  ┌──────────────────────────────────────┐                  │
│  │         ScriptContext                │                  │
│  │  • ECS World Access                  │                  │
│  │  • Entity Components (Position, etc) │                  │
│  │  • State Management (GetState/SetState)                 │
│  │  • Logger Integration                │                  │
│  └────────────────┬─────────────────────┘                  │
│                   │                                         │
│                   ▼                                         │
│  ┌──────────────────────────────────────┐                  │
│  │          Arch ECS World              │                  │
│  │  • Entities                          │                  │
│  │  • Components (Position, Health, etc)│                  │
│  │  • Queries                           │                  │
│  └──────────────────────────────────────┘                  │
└─────────────────────────────────────────────────────────────┘
```

## Core Components

### TypeScriptBase

The base class for all scripts. Provides lifecycle hooks:

```csharp
public abstract class TypeScriptBase
{
    // Called once when script is first loaded
    protected virtual void OnInitialize(ScriptContext ctx) { }

    // Called when activated on an entity
    public virtual void OnActivated(ScriptContext ctx) { }

    // Called every frame while active
    public virtual void OnTick(ScriptContext ctx, float deltaTime) { }

    // Called when deactivated
    public virtual void OnDeactivated(ScriptContext ctx) { }
}
```

### ScriptContext

Your gateway to the ECS world and entity components:

```csharp
public sealed class ScriptContext
{
    // Core access
    public World World { get; }           // ECS world instance
    public Entity? Entity { get; }        // Target entity (null for global scripts)
    public ILogger Logger { get; }        // Script-specific logger

    // Type checking
    public bool IsEntityScript { get; }   // True if attached to an entity
    public bool IsGlobalScript { get; }   // True if global (no entity)

    // Component access (type-safe, zero-allocation)
    public ref T GetState<T>() where T : struct
    public bool TryGetState<T>(out T state) where T : struct
    public ref T GetOrAddState<T>() where T : struct
    public bool HasState<T>() where T : struct
    public bool RemoveState<T>() where T : struct

    // Convenience properties
    public ref Position Position { get; }
    public bool HasPosition { get; }
}
```

### ScriptService

Manages script compilation, caching, and hot-reload:

```csharp
public class ScriptService
{
    // Load and compile a script
    public async Task<object?> LoadScriptAsync(string scriptPath)

    // Hot-reload a modified script
    public async Task<object?> ReloadScriptAsync(string scriptPath)

    // Get cached script instance
    public object? GetScriptInstance(string scriptPath)

    // Initialize script with world/entity/logger
    public void InitializeScript(object scriptInstance, World world,
                                  Entity? entity = null, ILogger? logger = null)

    // Check if script is loaded
    public bool IsScriptLoaded(string scriptPath)

    // Clear all cached scripts
    public void ClearCache()
}
```

## Hot-Reload System

The hot-reload system watches your scripts and automatically recompiles them when you save changes.

### How It Works

1. **File Watcher**: Monitors script directory for changes
2. **Debouncing**: Groups rapid changes (300ms window)
3. **Compilation**: Roslyn compiles the modified script
4. **Validation**: Checks for errors with detailed diagnostics
5. **Rollback**: On failure, automatically reverts to last working version
6. **Cache Update**: On success, updates the script instance cache

### Features

- **100% Uptime Goal**: Compilation failures never crash your game
- **Automatic Backups**: Previous versions are stored before compilation
- **Detailed Errors**: Line numbers, column positions, and error codes
- **Performance**: Average compilation time < 100ms
- **Cross-Platform**: FileSystemWatcher (Windows/Linux) or Polling (macOS)

### Statistics

```csharp
public class HotReloadStatisticsEnhanced
{
    public int TotalReloads { get; }
    public int SuccessfulReloads { get; }
    public int FailedReloads { get; }
    public int RollbacksPerformed { get; }
    public double SuccessRate { get; }          // % successful reloads
    public double RollbackRate { get; }         // % failures rolled back
    public double UptimeRate { get; }           // % uptime (100% if all failures rolled back)
    public double AverageCompilationTimeMs { get; }
    public double AverageReloadTimeMs { get; }
}
```

## Stateless Design Pattern

**CRITICAL**: Scripts are stateless! All instances are reused across entities.

### ❌ WRONG - Instance Fields

```csharp
public class BadScript : TypeScriptBase
{
    private int counter = 0;  // ❌ WRONG! Shared across ALL entities!

    public override void OnTick(ScriptContext ctx, float deltaTime)
    {
        counter++;  // All entities share the same counter!
    }
}
```

### ✅ CORRECT - ScriptContext State

```csharp
public class GoodScript : TypeScriptBase
{
    public override void OnTick(ScriptContext ctx, float deltaTime)
    {
        // ✅ CORRECT: Each entity has its own counter
        var counter = ctx.GetState<int>("counter");
        counter++;
        ctx.SetState("counter", counter);
    }
}
```

### Why Stateless?

1. **Performance**: Single instance per script type (not per entity)
2. **Memory Efficiency**: No duplication of script code
3. **Hot-Reload Safety**: Replacing a single instance updates all entities
4. **Thread Safety**: No shared mutable state between entities

## API Reference

### Helper Methods (TypeScriptBase)

```csharp
// Calculate direction from one point to another
protected static Direction GetDirectionTo(Point from, Point to)

// Display a message (integrates with future dialogue system)
protected static void ShowMessage(ScriptContext ctx, string message)

// Play a sound effect (integrates with future audio system)
protected static void PlaySound(ScriptContext ctx, string soundId)

// Spawn a visual effect (integrates with future particle system)
protected static void SpawnEffect(ScriptContext ctx, string effectId, Point position)

// Random utilities
protected static float Random()                        // 0.0 to 1.0
protected static int RandomRange(int min, int max)     // [min, max)
```

## Examples

See the [tutorials directory](docs/tutorials/) for step-by-step guides:

- [Hello World Tutorial](docs/tutorials/01-HELLO-WORLD.md)
- [MonoGame Integration Guide](docs/MONOGAME-INTEGRATION.md)

## Performance Considerations

- **Script Compilation**: ~50-150ms (one-time per script)
- **Hot-Reload**: ~100-300ms (includes debouncing)
- **Runtime Overhead**: Near-zero (compiled to native code)
- **Memory**: Single instance per script type
- **GC Pressure**: Minimal (ref returns avoid allocations)

## Best Practices

1. **Use `ref` for Component Access**:
   ```csharp
   ref var position = ref ctx.GetState<Position>();
   position.X += 10;  // Direct modification
   ```

2. **Check Before Accessing**:
   ```csharp
   if (ctx.TryGetState<Health>(out var health))
   {
       // Safe to use health
   }
   ```

3. **Use GetOrAddState for Optional Components**:
   ```csharp
   ref var timer = ref ctx.GetOrAddState<Timer>();
   timer.Elapsed += deltaTime;
   ```

4. **Log Liberally**:
   ```csharp
   ctx.Logger.LogDebug("Processing entity {Id}", ctx.Entity?.Id);
   ```

5. **Keep Scripts Small**: < 300 lines per script for maintainability

## Troubleshooting

### Script Not Loading

- Check file path is relative to `scriptsBasePath`
- Ensure script inherits from `TypeScriptBase`
- Verify script returns an instance at the end: `new MyScript()`

### Compilation Errors

- Check the logger output for detailed diagnostics
- Errors include line numbers and column positions
- Hot-reload will automatically rollback on failure

### Hot-Reload Not Working

- Ensure hot-reload service is started
- Check file watcher is monitoring correct directory
- Verify file extension matches the pattern (`.cs` or `.csx`)

## Contributing

Contributions are welcome! Please follow these guidelines:

1. Keep scripts stateless
2. Add XML documentation for public APIs
3. Include unit tests for new features
4. Follow the existing code style
5. Update documentation for breaking changes

## License

MIT License - See LICENSE file for details

## Credits

- **Roslyn**: Microsoft's C# compiler platform
- **Arch ECS**: High-performance ECS framework
- **MonoGame**: Cross-platform game framework

## Support

- Issues: [GitHub Issues](https://github.com/yourusername/PokeSharp/issues)
- Documentation: [Wiki](https://github.com/yourusername/PokeSharp/wiki)
- Community: [Discord](https://discord.gg/pokesharp)

---

**Version**: 1.0.0
**Target Framework**: .NET 9.0
**Last Updated**: 2025-11-06
