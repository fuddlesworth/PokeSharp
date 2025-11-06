# MonoGame Integration Guide

This guide shows you how to integrate PokeSharp.Scripting into your MonoGame project for hot-reloadable game behaviors.

## Table of Contents

1. [Prerequisites](#prerequisites)
2. [Project Setup](#project-setup)
3. [Dependency Injection Configuration](#dependency-injection-configuration)
4. [ScriptService Integration](#scriptservice-integration)
5. [Game Loop Integration](#game-loop-integration)
6. [Hot-Reload Setup](#hot-reload-setup)
7. [Example: Item Effect Script](#example-item-effect-script)
8. [Example: Custom Battle Logic](#example-custom-battle-logic)
9. [Troubleshooting](#troubleshooting)
10. [Performance Optimization](#performance-optimization)
11. [Best Practices](#best-practices)

## Prerequisites

- .NET 9.0 SDK or later
- MonoGame 3.8.2 or later
- Visual Studio 2022 or Rider 2024.2+
- Basic understanding of MonoGame and ECS patterns

### Required NuGet Packages

```xml
<PackageReference Include="MonoGame.Framework.DesktopGL" Version="3.8.2.1105" />
<PackageReference Include="Arch" Version="2.1.0" />
<PackageReference Include="Microsoft.Extensions.DependencyInjection" Version="9.0.0" />
<PackageReference Include="Microsoft.Extensions.Logging" Version="9.0.0" />
<PackageReference Include="Microsoft.Extensions.Logging.Console" Version="9.0.0" />
<PackageReference Include="PokeSharp.Scripting" Version="1.0.0" />
```

## Project Setup

### 1. Add PokeSharp.Scripting to Your Project

```bash
cd YourMonoGameProject
dotnet add package PokeSharp.Scripting
```

### 2. Create Scripts Directory

```bash
mkdir -p Content/Scripts
```

Your project structure should look like:

```
YourMonoGameProject/
├── Content/
│   ├── Scripts/          ← Your .cs script files go here
│   │   ├── Items/
│   │   ├── NPCs/
│   │   └── Battle/
│   ├── Graphics/
│   └── Audio/
├── Game1.cs
└── Program.cs
```

## Dependency Injection Configuration

Set up a DI container to manage services and dependencies.

### Step 1: Create ServiceProvider in Game1

```csharp
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Xna.Framework;
using Arch.Core;
using PokeSharp.Scripting;
using PokeSharp.Scripting.HotReload;
using PokeSharp.Scripting.HotReload.Backup;
using PokeSharp.Scripting.HotReload.Cache;
using PokeSharp.Scripting.HotReload.Notifications;
using PokeSharp.Scripting.HotReload.Watchers;

namespace YourGame
{
    public class Game1 : Game
    {
        private GraphicsDeviceManager _graphics;
        private SpriteBatch _spriteBatch;
        private ServiceProvider _services;
        private World _world;

        public Game1()
        {
            _graphics = new GraphicsDeviceManager(this);
            Content.RootDirectory = "Content";
            IsMouseVisible = true;
        }

        protected override void Initialize()
        {
            // Create ECS world
            _world = World.Create();

            // Configure dependency injection
            var services = new ServiceCollection();
            ConfigureServices(services);
            _services = services.BuildServiceProvider();

            base.Initialize();
        }

        private void ConfigureServices(IServiceCollection services)
        {
            // Logging
            services.AddLogging(builder =>
            {
                builder.AddConsole();
                builder.SetMinimumLevel(LogLevel.Debug);
            });

            // ECS World (singleton)
            services.AddSingleton(_world);

            // ScriptService (singleton)
            services.AddSingleton(sp =>
            {
                var logger = sp.GetRequiredService<ILogger<ScriptService>>();
                return new ScriptService(
                    scriptsBasePath: Path.Combine(Content.RootDirectory, "Scripts"),
                    logger: logger
                );
            });

            // Hot-Reload Components
            services.AddSingleton<VersionedScriptCache>();
            services.AddSingleton<ScriptBackupManager>();
            services.AddSingleton<WatcherFactory>();
            services.AddSingleton<IHotReloadNotificationService, ConsoleHotReloadNotificationService>();
            services.AddSingleton<IScriptCompiler, RoslynScriptCompiler>();

            // Hot-Reload Service (singleton)
            services.AddSingleton<ScriptHotReloadServiceEnhanced>();
        }

        protected override void Dispose(bool disposing)
        {
            if (disposing)
            {
                _services?.Dispose();
                _world?.Dispose();
            }
            base.Dispose(disposing);
        }
    }
}
```

### Step 2: Implement Notification Service

Create a simple console-based notification service:

```csharp
using PokeSharp.Scripting.HotReload.Notifications;
using Microsoft.Extensions.Logging;

public class ConsoleHotReloadNotificationService : IHotReloadNotificationService
{
    private readonly ILogger<ConsoleHotReloadNotificationService> _logger;

    public ConsoleHotReloadNotificationService(ILogger<ConsoleHotReloadNotificationService> logger)
    {
        _logger = logger;
    }

    public void ShowNotification(HotReloadNotification notification)
    {
        var logLevel = notification.Type switch
        {
            NotificationType.Success => LogLevel.Information,
            NotificationType.Warning => LogLevel.Warning,
            NotificationType.Error => LogLevel.Error,
            _ => LogLevel.Information
        };

        _logger.Log(logLevel, "[Hot-Reload] {Message}", notification.Message);

        if (!string.IsNullOrEmpty(notification.Details))
        {
            _logger.Log(logLevel, "  Details: {Details}", notification.Details);
        }
    }
}
```

### Step 3: Implement Script Compiler

```csharp
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Scripting;
using Microsoft.CodeAnalysis.Scripting;
using PokeSharp.Scripting;

public interface IScriptCompiler
{
    Task<CompilationResult> CompileScriptAsync(string filePath);
}

public class RoslynScriptCompiler : IScriptCompiler
{
    private readonly ScriptOptions _defaultOptions;

    public RoslynScriptCompiler()
    {
        _defaultOptions = ScriptCompilationOptions.GetDefaultOptions();
    }

    public async Task<CompilationResult> CompileScriptAsync(string filePath)
    {
        try
        {
            var code = await File.ReadAllTextAsync(filePath);
            var script = CSharpScript.Create<object>(code, _defaultOptions);

            var diagnostics = script.Compile();
            var errors = diagnostics
                .Where(d => d.Severity == DiagnosticSeverity.Error)
                .Select(d => d.GetMessage())
                .ToList();

            if (errors.Any())
            {
                return new CompilationResult
                {
                    Success = false,
                    HasErrors = true,
                    Errors = errors,
                    Diagnostics = ConvertDiagnostics(diagnostics)
                };
            }

            var result = await script.RunAsync();
            var instance = result.ReturnValue;

            if (instance is not TypeScriptBase)
            {
                return new CompilationResult
                {
                    Success = false,
                    HasErrors = true,
                    Errors = new[] { $"Script must return TypeScriptBase instance, got {instance?.GetType().Name ?? "null"}" }
                };
            }

            return new CompilationResult
            {
                Success = true,
                HasErrors = false,
                CompiledType = instance.GetType(),
                Instance = instance
            };
        }
        catch (Exception ex)
        {
            return new CompilationResult
            {
                Success = false,
                HasErrors = true,
                Errors = new[] { ex.Message }
            };
        }
    }

    private List<CompilationDiagnostic> ConvertDiagnostics(ImmutableArray<Diagnostic> diagnostics)
    {
        return diagnostics.Select(d =>
        {
            var lineSpan = d.Location.GetLineSpan();
            return new CompilationDiagnostic
            {
                Severity = d.Severity == DiagnosticSeverity.Error ? DiagnosticSeverity.Error : DiagnosticSeverity.Warning,
                Message = d.GetMessage(),
                Code = d.Id,
                Line = lineSpan.StartLinePosition.Line + 1,
                Column = lineSpan.StartLinePosition.Character + 1
            };
        }).ToList();
    }
}

public class CompilationResult
{
    public bool Success { get; set; }
    public bool HasErrors { get; set; }
    public Type? CompiledType { get; set; }
    public object? Instance { get; set; }
    public IList<string> Errors { get; set; } = new List<string>();
    public IList<CompilationDiagnostic> Diagnostics { get; set; } = new List<CompilationDiagnostic>();

    public string GetErrorSummary()
    {
        if (!HasErrors) return string.Empty;
        return string.Join("\n", Errors);
    }
}
```

## ScriptService Integration

### Loading Scripts at Startup

```csharp
public class Game1 : Game
{
    private ScriptService _scriptService;
    private Dictionary<string, object> _loadedScripts = new();

    protected override async void Initialize()
    {
        // ... (previous initialization code)

        _scriptService = _services.GetRequiredService<ScriptService>();

        // Load all scripts from directory
        await LoadScriptsAsync();

        base.Initialize();
    }

    private async Task LoadScriptsAsync()
    {
        var scriptsPath = Path.Combine(Content.RootDirectory, "Scripts");
        var scriptFiles = Directory.GetFiles(scriptsPath, "*.cs", SearchOption.AllDirectories);

        foreach (var scriptFile in scriptFiles)
        {
            var relativePath = Path.GetRelativePath(scriptsPath, scriptFile);
            var script = await _scriptService.LoadScriptAsync(relativePath);

            if (script != null)
            {
                _loadedScripts[relativePath] = script;
                Console.WriteLine($"Loaded script: {relativePath}");
            }
        }
    }
}
```

## Game Loop Integration

### Update Method

```csharp
public class Game1 : Game
{
    private ILogger<Game1> _logger;

    protected override void Update(GameTime gameTime)
    {
        if (GamePad.GetState(PlayerIndex.One).Buttons.Back == ButtonState.Pressed
            || Keyboard.GetState().IsKeyDown(Keys.Escape))
            Exit();

        float deltaTime = (float)gameTime.ElapsedGameTime.TotalSeconds;

        // Update all scripted entities
        UpdateScriptedEntities(deltaTime);

        base.Update(gameTime);
    }

    private void UpdateScriptedEntities(float deltaTime)
    {
        // Query all entities with a ScriptComponent
        var query = new QueryDescription().WithAll<ScriptComponent>();

        _world.Query(in query, (Entity entity, ref ScriptComponent scriptComp) =>
        {
            if (_loadedScripts.TryGetValue(scriptComp.ScriptPath, out var scriptObj))
            {
                if (scriptObj is TypeScriptBase script)
                {
                    var ctx = new ScriptContext(_world, entity, _logger);
                    script.OnTick(ctx, deltaTime);
                }
            }
        });
    }
}

// Component to attach scripts to entities
public struct ScriptComponent
{
    public string ScriptPath;
    public bool IsActive;
}
```

### Draw Method

```csharp
protected override void Draw(GameTime gameTime)
{
    GraphicsDevice.Clear(Color.CornflowerBlue);

    _spriteBatch.Begin();

    // Draw all entities with Position and Sprite components
    var query = new QueryDescription().WithAll<Position, Sprite>();

    _world.Query(in query, (ref Position pos, ref Sprite sprite) =>
    {
        _spriteBatch.Draw(
            sprite.Texture,
            new Vector2(pos.X, pos.Y),
            Color.White
        );
    });

    _spriteBatch.End();

    base.Draw(gameTime);
}
```

## Hot-Reload Setup

### Enable Hot-Reload for Development

```csharp
public class Game1 : Game
{
    private ScriptHotReloadServiceEnhanced _hotReloadService;

    protected override async void Initialize()
    {
        // ... (previous initialization)

        // Enable hot-reload in debug mode
        #if DEBUG
        await StartHotReloadAsync();
        #endif

        base.Initialize();
    }

    private async Task StartHotReloadAsync()
    {
        _hotReloadService = _services.GetRequiredService<ScriptHotReloadServiceEnhanced>();

        // Subscribe to events
        _hotReloadService.CompilationSucceeded += OnScriptReloaded;
        _hotReloadService.CompilationFailed += OnScriptFailed;
        _hotReloadService.RollbackPerformed += OnScriptRolledBack;

        var scriptsPath = Path.Combine(Content.RootDirectory, "Scripts");
        await _hotReloadService.StartAsync(scriptsPath);

        Console.WriteLine("Hot-reload enabled. Edit scripts and save to see changes!");
    }

    private void OnScriptReloaded(object? sender, CompilationEventArgs e)
    {
        Console.WriteLine($"✓ Script reloaded: {e.TypeId}");

        // Update the loaded scripts dictionary
        if (e.CompiledType != null)
        {
            var instance = Activator.CreateInstance(e.CompiledType);
            if (instance != null)
            {
                _loadedScripts[e.TypeId] = instance;
            }
        }
    }

    private void OnScriptFailed(object? sender, CompilationEventArgs e)
    {
        Console.WriteLine($"✗ Script compilation failed: {e.TypeId}");
        if (e.Result != null)
        {
            foreach (var error in e.Result.Errors)
            {
                Console.WriteLine($"  - {error}");
            }
        }
    }

    private void OnScriptRolledBack(object? sender, CompilationEventArgs e)
    {
        Console.WriteLine($"↶ Script rolled back: {e.TypeId}");
    }
}
```

## Example: Item Effect Script

Create `Content/Scripts/Items/HealthPotion.cs`:

```csharp
using Microsoft.Extensions.Logging;
using Microsoft.Xna.Framework;
using PokeSharp.Scripting;
using PokeSharp.Core.Components;

public class HealthPotionEffect : TypeScriptBase
{
    private const int HEAL_AMOUNT = 20;

    public override void OnActivated(ScriptContext ctx)
    {
        ctx.Logger.LogInformation("Health Potion used on entity {EntityId}", ctx.Entity?.Id);

        if (!ctx.TryGetState<Health>(out var health))
        {
            ctx.Logger.LogWarning("Entity has no Health component!");
            return;
        }

        // Calculate actual healing (don't overheal)
        int actualHeal = Math.Min(HEAL_AMOUNT, health.MaxHP - health.CurrentHP);

        if (actualHeal <= 0)
        {
            ShowMessage(ctx, "HP is already full!");
            return;
        }

        // Apply healing
        ref var healthRef = ref ctx.GetState<Health>();
        healthRef.CurrentHP += actualHeal;

        // Show message and effect
        ShowMessage(ctx, $"Restored {actualHeal} HP!");
        PlaySound(ctx, "heal");

        if (ctx.HasPosition)
        {
            SpawnEffect(ctx, "heal_sparkle", new Point(ctx.Position.X, ctx.Position.Y));
        }

        ctx.Logger.LogInformation("Healed {Amount} HP. New HP: {Current}/{Max}",
            actualHeal, healthRef.CurrentHP, healthRef.MaxHP);
    }
}

new HealthPotionEffect()
```

### Using the Item Script

```csharp
// When player uses item
public void UseItem(string itemScriptPath, Entity target)
{
    if (_loadedScripts.TryGetValue(itemScriptPath, out var scriptObj))
    {
        if (scriptObj is TypeScriptBase script)
        {
            var ctx = new ScriptContext(_world, target, _logger);
            script.OnActivated(ctx);
        }
    }
}

// Example usage
UseItem("Items/HealthPotion.cs", playerEntity);
```

## Example: Custom Battle Logic

Create `Content/Scripts/Battle/CriticalHitBoost.cs`:

```csharp
using Microsoft.Extensions.Logging;
using PokeSharp.Scripting;
using PokeSharp.Core.Components;

public class CriticalHitBoostAbility : TypeScriptBase
{
    private const float CRIT_BOOST = 0.125f; // +12.5% crit chance

    public override void OnTick(ScriptContext ctx, float deltaTime)
    {
        // Only active during battle
        if (!ctx.TryGetState<BattleState>(out var battleState))
            return;

        if (!battleState.IsInBattle)
            return;

        // Get or create ability state
        ref var abilityState = ref ctx.GetOrAddState<AbilityState>();

        // Apply crit boost every turn
        if (battleState.TurnNumber > abilityState.LastProcessedTurn)
        {
            ref var stats = ref ctx.GetState<BattleStats>();
            stats.CriticalHitChance += CRIT_BOOST;

            abilityState.LastProcessedTurn = battleState.TurnNumber;

            ctx.Logger.LogDebug("Critical Hit Boost active! Crit chance: {Chance:P}",
                stats.CriticalHitChance);
        }
    }

    public override void OnDeactivated(ScriptContext ctx)
    {
        // Remove crit boost when ability deactivates
        if (ctx.TryGetState<BattleStats>(out var stats))
        {
            ref var statsRef = ref ctx.GetState<BattleStats>();
            statsRef.CriticalHitChance = Math.Max(0, statsRef.CriticalHitChance - CRIT_BOOST);
        }
    }
}

// Custom component for ability state
public struct AbilityState
{
    public int LastProcessedTurn;
}

new CriticalHitBoostAbility()
```

## Troubleshooting

### Common Issues

#### 1. Script Not Loading

**Symptom**: `LoadScriptAsync` returns null

**Solution**:
- Check the file path is correct and relative to `scriptsBasePath`
- Ensure the script file ends with `new YourScriptClass()`
- Check the console for compilation errors

#### 2. Component Not Found Errors

**Symptom**: `InvalidOperationException: Entity does not have component`

**Solution**:
```csharp
// Always use TryGetState for optional components
if (ctx.TryGetState<Health>(out var health))
{
    // Safe to use health
}

// Or check first
if (ctx.HasState<Health>())
{
    ref var health = ref ctx.GetState<Health>();
}
```

#### 3. Hot-Reload Not Working

**Symptom**: Changes to scripts don't apply

**Solution**:
- Check hot-reload service is started: `await _hotReloadService.StartAsync(scriptsPath)`
- Verify the file watcher is monitoring the correct directory
- Check console for watcher errors
- On macOS, ensure you're not hitting file descriptor limits

#### 4. Shared State Between Entities

**Symptom**: All entities using the same script share data

**Solution**:
```csharp
// ❌ WRONG
private int counter = 0;  // Shared across all entities!

// ✅ CORRECT
public override void OnTick(ScriptContext ctx, float deltaTime)
{
    var counter = ctx.GetState<int>("counter");  // Per-entity state
    counter++;
    ctx.SetState("counter", counter);
}
```

## Performance Optimization

### 1. Cache Component References

Instead of calling `GetState<T>()` every frame:

```csharp
// ❌ Less efficient
public override void OnTick(ScriptContext ctx, float deltaTime)
{
    ref var pos = ref ctx.GetState<Position>();
    pos.X += 1;
}

// ✅ More efficient (use ref)
public override void OnTick(ScriptContext ctx, float deltaTime)
{
    ref var pos = ref ctx.Position;  // Convenience property
    pos.X += 1;
}
```

### 2. Use Queries for Batch Operations

For operations affecting multiple entities:

```csharp
// Global script for batch processing
public class GlobalSpeedBoost : TypeScriptBase
{
    public override void OnTick(ScriptContext ctx, float deltaTime)
    {
        var query = new QueryDescription()
            .WithAll<Speed, ActiveBoost>();

        ctx.World.Query(in query, (Entity entity, ref Speed speed) =>
        {
            speed.Current *= 1.5f;
        });
    }
}
```

### 3. Conditional Script Execution

Only run scripts when needed:

```csharp
public struct ScriptComponent
{
    public string ScriptPath;
    public bool IsActive;
    public float UpdateInterval;  // Run every N seconds
    public float TimeSinceUpdate;
}

private void UpdateScriptedEntities(float deltaTime)
{
    var query = new QueryDescription().WithAll<ScriptComponent>();

    _world.Query(in query, (Entity entity, ref ScriptComponent scriptComp) =>
    {
        if (!scriptComp.IsActive)
            return;

        scriptComp.TimeSinceUpdate += deltaTime;

        if (scriptComp.TimeSinceUpdate < scriptComp.UpdateInterval)
            return;

        scriptComp.TimeSinceUpdate = 0;

        // Execute script...
    });
}
```

### 4. Async Loading for Startup

Load scripts asynchronously to avoid blocking:

```csharp
protected override async void Initialize()
{
    base.Initialize();

    // Load scripts in background
    _ = Task.Run(async () =>
    {
        await LoadScriptsAsync();
        Console.WriteLine("All scripts loaded!");
    });
}
```

## Best Practices

### 1. Script Organization

Organize scripts by functionality:

```
Content/Scripts/
├── Items/
│   ├── Consumables/
│   │   ├── HealthPotion.cs
│   │   └── ManaPotion.cs
│   └── Equipment/
│       ├── Sword.cs
│       └── Shield.cs
├── NPCs/
│   ├── Behaviors/
│   │   ├── WanderBehavior.cs
│   │   └── ChaseBehavior.cs
│   └── Dialogue/
│       └── ShopkeeperDialogue.cs
└── Battle/
    ├── Abilities/
    │   └── CriticalHitBoost.cs
    └── StatusEffects/
        ├── Poison.cs
        └── Burn.cs
```

### 2. Use Meaningful Names

```csharp
// ❌ Vague
public class Script1 : TypeScriptBase { }

// ✅ Descriptive
public class WildPokemonEncounterBehavior : TypeScriptBase { }
```

### 3. Document Your Scripts

```csharp
/// <summary>
/// NPC behavior that makes the entity wander randomly.
/// Checks for obstacles and changes direction every 2-5 seconds.
/// </summary>
public class WanderBehavior : TypeScriptBase
{
    /// <summary>
    /// Minimum time between direction changes (in seconds).
    /// </summary>
    private const float MIN_CHANGE_INTERVAL = 2.0f;

    // ... implementation
}
```

### 4. Handle Errors Gracefully

```csharp
public override void OnActivated(ScriptContext ctx)
{
    try
    {
        if (!ctx.HasState<Health>())
        {
            ctx.Logger.LogWarning("Entity {Id} has no Health component, cannot apply potion",
                ctx.Entity?.Id);
            return;
        }

        // ... rest of logic
    }
    catch (Exception ex)
    {
        ctx.Logger.LogError(ex, "Error in potion script");
    }
}
```

### 5. Use Constants for Magic Numbers

```csharp
public class SpeedBoostAbility : TypeScriptBase
{
    private const float BOOST_MULTIPLIER = 1.5f;
    private const float DURATION_SECONDS = 10.0f;
    private const string SOUND_EFFECT_ID = "speed_boost";

    // ... implementation uses constants
}
```

---

**Next Steps**: Check out the [Hello World Tutorial](tutorials/01-HELLO-WORLD.md) for a hands-on introduction to scripting!
