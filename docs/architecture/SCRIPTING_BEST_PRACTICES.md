# PokeSharp Scripting - Best Practices & Guidelines

## Quick Reference

### ✅ DO: Current Architecture Best Practices

```csharp
// ✅ Scripts depend on Core abstractions
public class MyScript : TypeScriptBase
{
    public override void OnTick(ScriptContext ctx, float deltaTime)
    {
        // Access APIs through context
        ctx.Player.GetMoney();
        ctx.Npc.MoveNPC(entity, Direction.Up);
        ctx.Map.IsPositionWalkable(map, x, y);
    }
}
```

### ❌ DON'T: Anti-Patterns to Avoid

```csharp
// ❌ Don't access World directly in scripts
public class BadScript : TypeScriptBase
{
    private World _world;  // DON'T STORE STATE!

    public override void OnTick(ScriptContext ctx, float deltaTime)
    {
        // ❌ Don't access World directly
        _world.Get<Position>(entity);  // Use ctx.GetState<Position>() instead

        // ❌ Don't store instance state
        _counter++;  // Scripts are shared! Use ctx.GetState/SetState
    }
}
```

---

## Architecture Principles

### 1. Dependency Flow (One-Way Only)

```
Scripts → ScriptContext → IScriptingApiProvider → API Services → Components
```

**Rule**: All arrows point downward. Never introduce reverse dependencies.

### 2. Layer Responsibilities

| Layer | Responsibility | Examples |
|-------|----------------|----------|
| **Core** | Domain logic, ECS components, API interfaces & implementations | `PlayerApiService`, `IScriptingApiProvider`, `Position` component |
| **Scripting** | Script infrastructure, compilation, execution | `ScriptService`, `TypeScriptBase`, `ScriptContext` |
| **Game** | Orchestration, initialization, MonoGame integration | `PokeSharpGame`, `ServiceCollectionExtensions` |

### 3. Interface Placement

**Rule**: Place interfaces in the **lowest layer** that defines the abstraction.

```
✅ CORRECT:
IScriptingApiProvider → PokeSharp.Core/ScriptingApi/
(Core defines the scripting abstraction)

✅ CORRECT:
IPlayerApi → PokeSharp.Core/ScriptingApi/
(Core defines player domain abstraction)

❌ WRONG:
IScriptingApiProvider → PokeSharp.Scripting/
(Would force Core to depend on Scripting - circular!)
```

---

## Extending the Scripting System

### Adding a New API Method

**Example**: Add `GetPlayerLevel()` to Player API

#### Step 1: Add to Interface (Core)
```csharp
// PokeSharp.Core/ScriptingApi/IPlayerApi.cs
public interface IPlayerApi
{
    // ... existing methods
    int GetPlayerLevel();  // ← New method
}
```

#### Step 2: Implement in Service (Core)
```csharp
// PokeSharp.Core/Scripting/Services/PlayerApiService.cs
public class PlayerApiService : IPlayerApi
{
    public int GetPlayerLevel()
    {
        var player = GetPlayerEntity();
        if (_world.Has<Level>(player))
        {
            ref var level = ref _world.Get<Level>(player);
            return level.Current;
        }
        return 1;  // Default level
    }
}
```

#### Step 3: Use in Scripts (Automatic!)
```csharp
// Scripts/behaviors/level_check.csx
public class LevelCheck : TypeScriptBase
{
    public override void OnTick(ScriptContext ctx, float deltaTime)
    {
        var level = ctx.Player.GetPlayerLevel();  // ← Available immediately!
        if (level >= 10)
        {
            ctx.Dialogue.ShowMessage("You're a strong trainer!");
        }
    }
}
```

**No changes needed to**:
- ❌ `ScriptService`
- ❌ `ScriptContext`
- ❌ `IScriptingApiProvider`
- ❌ `ScriptingApiProvider`

**Why**: Facade pattern enables extension without modification (Open/Closed Principle) ✅

---

### Adding a New API Category

**Example**: Add `IBattleApi` for battle-related operations

#### Step 1: Create Interface (Core)
```csharp
// PokeSharp.Core/ScriptingApi/IBattleApi.cs
namespace PokeSharp.Core.ScriptingApi;

public interface IBattleApi
{
    bool IsBattleActive();
    void StartWildBattle(int pokemonId, int level);
    void StartTrainerBattle(Entity trainer);
    bool CanFlee();
}
```

#### Step 2: Create Service (Core)
```csharp
// PokeSharp.Core/Scripting/Services/BattleApiService.cs
namespace PokeSharp.Core.Scripting.Services;

public class BattleApiService(World world, ILogger logger) : IBattleApi
{
    private readonly World _world = world;
    private readonly ILogger _logger = logger;

    public bool IsBattleActive()
    {
        // Implementation...
    }

    // ... other methods
}
```

#### Step 3: Add to Facade Interface (Core)
```csharp
// PokeSharp.Core/ScriptingApi/IScriptingApiProvider.cs
public interface IScriptingApiProvider
{
    PlayerApiService Player { get; }
    NpcApiService Npc { get; }
    MapApiService Map { get; }
    GameStateApiService GameState { get; }
    DialogueApiService Dialogue { get; }
    EffectApiService Effects { get; }
    BattleApiService Battle { get; }  // ← New API
}
```

#### Step 4: Add to Facade Implementation (Core)
```csharp
// PokeSharp.Core/Scripting/Services/ScriptingApiProvider.cs
public class ScriptingApiProvider(
    PlayerApiService playerApi,
    NpcApiService npcApi,
    MapApiService mapApi,
    GameStateApiService gameStateApi,
    DialogueApiService dialogueApi,
    EffectApiService effectApi,
    BattleApiService battleApi  // ← New parameter
) : IScriptingApiProvider
{
    private readonly BattleApiService _battleApi = battleApi;

    public BattleApiService Battle => _battleApi;  // ← New property
}
```

#### Step 5: Register in DI (Game)
```csharp
// PokeSharp.Game/ServiceCollectionExtensions.cs
public static IServiceCollection AddScriptingServices(this IServiceCollection services)
{
    // Register API services
    services.AddSingleton<PlayerApiService>();
    services.AddSingleton<NpcApiService>();
    services.AddSingleton<MapApiService>();
    services.AddSingleton<GameStateApiService>();
    services.AddSingleton<DialogueApiService>();
    services.AddSingleton<EffectApiService>();
    services.AddSingleton<BattleApiService>();  // ← New registration

    // Register facade (DI container will auto-resolve new parameter)
    services.AddSingleton<IScriptingApiProvider, ScriptingApiProvider>();

    // ... other registrations
}
```

#### Step 6: Update ScriptContext (Scripting)
```csharp
// PokeSharp.Scripting/Runtime/ScriptContext.cs
public sealed class ScriptContext
{
    // ... existing properties

    /// <summary>
    /// Gets the Battle API service for battle-related operations.
    /// </summary>
    public BattleApiService Battle => _apis.Battle;
}
```

#### Step 7: Use in Scripts
```csharp
// Scripts/triggers/grass_encounter.csx
public class GrassEncounter : TypeScriptBase
{
    public override void OnTick(ScriptContext ctx, float deltaTime)
    {
        if (ShouldTriggerBattle())
        {
            // New API available!
            ctx.Battle.StartWildBattle(pokemonId: 16, level: 5);
        }
    }
}
```

---

## Testing Best Practices

### Unit Testing API Services

```csharp
// Test individual API service in isolation
[Fact]
public void PlayerApiService_GetMoney_ReturnsBalance()
{
    // Arrange
    var world = World.Create();
    var player = world.Create();
    world.Add(player, new Player());
    world.Add(player, new Wallet { Balance = 500 });

    var logger = NullLogger<PlayerApiService>.Instance;
    var api = new PlayerApiService(world, logger);

    // Act
    var money = api.GetMoney();

    // Assert
    Assert.Equal(500, money);
}
```

### Integration Testing Scripts

```csharp
// Test script with mocked API provider
[Fact]
public async Task Script_OnTick_CallsPlayerApi()
{
    // Arrange
    var mockApis = new Mock<IScriptingApiProvider>();
    var mockPlayer = new Mock<IPlayerApi>();
    mockApis.Setup(a => a.Player).Returns(mockPlayer.Object);

    var scriptService = new ScriptService(
        scriptsBasePath: "TestScripts/",
        logger: NullLogger<ScriptService>.Instance,
        apis: mockApis.Object
    );

    // Act
    var script = await scriptService.LoadScriptAsync("test_script.csx");
    var context = new ScriptContext(world, entity, logger, mockApis.Object);
    (script as TypeScriptBase)?.OnTick(context, 0.016f);

    // Assert
    mockPlayer.Verify(p => p.GetMoney(), Times.Once);
}
```

---

## Performance Guidelines

### 1. Use Ref Returns for Component Access

```csharp
// ✅ GOOD: Zero-allocation modification
public override void OnTick(ScriptContext ctx, float deltaTime)
{
    ref var pos = ref ctx.GetState<Position>();
    pos.X += 1;  // Modifies in-place
}

// ❌ AVOID: Creates copy, modification lost
public override void OnTick(ScriptContext ctx, float deltaTime)
{
    var pos = ctx.GetState<Position>();  // Copy
    pos.X += 1;  // Modifies copy, not original!
}
```

### 2. Cache Script Instances

```csharp
// ScriptService already handles this
private readonly ConcurrentDictionary<string, object> _scriptInstances = new();

// Scripts compiled once, reused across entities ✅
```

### 3. Prefer TryGetState for Optional Components

```csharp
// ✅ GOOD: No exception if component missing
if (ctx.TryGetState<Health>(out var health))
{
    health.Current -= 10;
}

// ❌ AVOID: Throws if component missing
try
{
    ref var health = ref ctx.GetState<Health>();
    health.Current -= 10;
}
catch (InvalidOperationException) { }
```

---

## Common Mistakes to Avoid

### Mistake 1: Storing State in Script Instance

```csharp
// ❌ WRONG: Instance fields break with multiple entities
public class BadScript : TypeScriptBase
{
    private int _counter = 0;  // Shared across ALL entities!

    public override void OnTick(ScriptContext ctx, float deltaTime)
    {
        _counter++;  // This will corrupt state!
    }
}

// ✅ CORRECT: Use ScriptContext state methods
public class GoodScript : TypeScriptBase
{
    public override void OnTick(ScriptContext ctx, float deltaTime)
    {
        var counter = ctx.GetState<ScriptTimer>().ElapsedTime;
        // Each entity has its own state ✅
    }
}
```

### Mistake 2: Direct World Manipulation

```csharp
// ❌ WRONG: Bypasses API layer
public override void OnTick(ScriptContext ctx, float deltaTime)
{
    var player = FindPlayer(ctx.World);  // Don't search manually
    ctx.World.Get<Wallet>(player).Balance += 100;  // Don't manipulate directly
}

// ✅ CORRECT: Use API services
public override void OnTick(ScriptContext ctx, float deltaTime)
{
    ctx.Player.GiveMoney(100);  // Encapsulated, validated, logged
}
```

### Mistake 3: Creating Circular Dependencies

```csharp
// ❌ WRONG: Core depending on Scripting
// File: PokeSharp.Core/Systems/SomeSystem.cs
using PokeSharp.Scripting.Services;  // ❌ Core → Scripting (wrong direction!)

public class SomeSystem
{
    private readonly ScriptService _scripts;  // ❌ Core shouldn't know about ScriptService
}

// ✅ CORRECT: Core depending on Core abstraction
// File: PokeSharp.Core/Systems/NPCBehaviorSystem.cs
using PokeSharp.Core.ScriptingApi;  // ✅ Core → Core (correct!)

public class NPCBehaviorSystem
{
    private readonly IScriptingApiProvider _apis;  // ✅ Depends on abstraction
}
```

---

## Code Review Checklist

When reviewing scripting-related code, check:

### Architecture
- [ ] New interfaces are in Core layer
- [ ] Implementations are in Core layer
- [ ] No Core → Scripting dependencies
- [ ] No Core → Game dependencies
- [ ] All dependencies point downward

### API Design
- [ ] New APIs added to appropriate interface (IPlayerApi, INPCApi, etc.)
- [ ] Methods documented with XML comments
- [ ] Methods follow single responsibility principle
- [ ] Return types are appropriate (ref for components, values for queries)

### Script Patterns
- [ ] Scripts inherit from `TypeScriptBase`
- [ ] Scripts don't store instance state
- [ ] Scripts use `ScriptContext` for all operations
- [ ] Scripts access APIs via `ctx.Player`, `ctx.Npc`, etc.

### Testing
- [ ] API services have unit tests
- [ ] Integration tests use mocked `IScriptingApiProvider`
- [ ] Tests don't depend on file system (use in-memory scripts)

### Performance
- [ ] Component access uses `ref` when modifying
- [ ] Optional components use `TryGetState`
- [ ] No unnecessary allocations in hot paths

---

## Documentation Standards

### XML Documentation for API Methods

```csharp
/// <summary>
///     Gives money to the player (e.g., battle prize, quest reward).
/// </summary>
/// <param name="amount">Amount in Pokédollars (must be positive).</param>
/// <exception cref="ArgumentException">If amount is negative.</exception>
/// <remarks>
///     This method automatically logs the transaction and updates the UI.
///     Use <see cref="TakeMoney"/> to deduct money instead.
/// </remarks>
/// <example>
///     <code>
/// // Reward player for completing quest
/// ctx.Player.GiveMoney(500);
/// </code>
/// </example>
public void GiveMoney(int amount)
{
    // Implementation...
}
```

### Script File Comments

```csharp
// Scripts/behaviors/gym_guard.csx

/// <summary>
/// Gym guard behavior that blocks player until they have the required badge.
/// </summary>
/// <remarks>
/// This script:
/// - Checks for "has_boulder_badge" flag
/// - Blocks player within 2 tiles if no badge
/// - Moves aside if player has badge
/// - Uses patrol path for movement
/// </remarks>
public class GymGuard : TypeScriptBase
{
    // Implementation...
}
```

---

## Migration Guide (If Architecture Changes)

**Current Status**: No migration needed - architecture is optimal ✅

**If you ever need to migrate** (e.g., moving API services to different layer):

### Step 1: Create Interface in Target Layer
```csharp
// New location: PokeSharp.NewLayer/Api/IPlayerApi.cs
public interface IPlayerApi { ... }
```

### Step 2: Move Implementation
```csharp
// New location: PokeSharp.NewLayer/Services/PlayerApiService.cs
public class PlayerApiService : IPlayerApi { ... }
```

### Step 3: Update References
- Update `using` statements in all files
- Update project references if needed
- Update DI registration

### Step 4: Verify No Circular Dependencies
```bash
# Check project reference graph
dotnet list reference
```

### Step 5: Test
```bash
# Ensure all tests pass
dotnet test
```

---

## Quick Command Reference

### Building
```bash
dotnet build PokeSharp.sln
```

### Testing
```bash
# Run all tests
dotnet test

# Run specific test project
dotnet test tests/PokeSharp.Core.Tests/

# Run tests with coverage
dotnet test /p:CollectCoverage=true
```

### Project References
```bash
# Check references
dotnet list PokeSharp.Core/PokeSharp.Core.csproj reference
dotnet list PokeSharp.Scripting/PokeSharp.Scripting.csproj reference
dotnet list PokeSharp.Game/PokeSharp.Game.csproj reference
```

---

## Resources

### Key Files
- `/PokeSharp.Core/ScriptingApi/IScriptingApiProvider.cs` - Master facade interface
- `/PokeSharp.Core/Scripting/Services/ScriptingApiProvider.cs` - Facade implementation
- `/PokeSharp.Scripting/Runtime/ScriptContext.cs` - Script execution context
- `/PokeSharp.Scripting/Runtime/TypeScriptBase.cs` - Base class for scripts

### Documentation
- `/docs/research/SCRIPTING_ARCHITECTURE_RESEARCH.md` - Detailed architecture analysis
- `/docs/architecture/SCRIPTING_ARCHITECTURE_DIAGRAM.md` - Visual diagrams
- `/docs/research/SCRIPTING_RESEARCH_SUMMARY.md` - Executive summary

### Related Patterns
- `IGameServicesProvider` - Similar facade for game services
- `ILoggingProvider` - Similar facade for logging
- `IInitializationProvider` - Similar facade for initialization

---

## Summary

**Current Architecture Status**: ✅ OPTIMAL - No changes needed

**Key Principles**:
1. Scripts depend only on Core abstractions
2. All dependencies point downward (no circular references)
3. Facade pattern groups related APIs
4. API services encapsulate ECS operations
5. Scripts are stateless (use ScriptContext for state)

**When in Doubt**: Follow the existing pattern! ✅

---

**Document Version**: 1.0
**Last Updated**: 2024-01-10
**Status**: ACTIVE GUIDELINES
