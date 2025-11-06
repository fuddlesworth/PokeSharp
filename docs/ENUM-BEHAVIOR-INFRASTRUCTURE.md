# Type ID System: Strings vs Enums

**Flexible Type System Architecture for Moddable Game Data**

## Table of Contents

- [Overview](#overview)
- [String vs Enum Approach](#string-vs-enum-approach)
- [When to Use Each](#when-to-use-each)
- [Architecture Layers](#architecture-layers)
- [Type Systems](#type-systems)
- [Weather Type System](#weather-type-system)
- [Integration Patterns](#integration-patterns)
- [Extension Guide](#extension-guide)

## Overview

PokeSharp uses a **flexible type ID system** to manage game data types (behaviors, weather, terrain, items, etc.). This infrastructure allows modders to add new content without recompiling the engine.

### Two Approaches: Strings vs Enums

PokeSharp supports **both string-based and enum-based** type identification:

- **String Type IDs** (Recommended for Moddable Systems) - Unlimited extensibility, no recompilation needed
- **Enums** (Optional) - Compile-time safety, IntelliSense support for core types

### Key Features

- **String + Behavior Pattern** - String IDs for unlimited moddability
- **Discovery-Based** - Types discovered from file system, not defined in code
- **Hot-reloadable** - Change behavior without restarting
- **Fully Moddable** - Add custom types via scripts, no engine changes
- **Extensible** - Add new type systems easily

### Design Philosophy

```
String TypeId defines WHAT the type is (identity) - UNLIMITED moddability
Optional Enum/Constants provide IntelliSense (convenience) - Core types only
Script defines HOW the type behaves (behavior)
Component stores WHERE the type's data lives (state)
System processes WHEN the type updates (execution)
```

---

## String vs Enum Approach

### ✅ String-Based Type IDs (Recommended for Moddable Systems)

**Example: Weather System - File-Based Discovery**

```csharp
// Component uses string for type identity
public struct WeatherState
{
    public string WeatherTypeId;  // ✅ From file name: weather_rain.csx → "rain"
    public float Duration;
    // ...
}

// Type IDs come from file names:
// weather_rain.csx           → "rain"
// weather_volcanic_ash.csx   → "volcanic_ash"
// weather_mymod_custom.csx   → "mymod_custom"

// Modder creates custom weather - NO engine changes!
// File: weather_volcanic_ash.csx
public class VolcanicAshWeather : TypeScriptBase
{
    public override void OnActivated(ScriptContext ctx)
    {
        ctx.World.Create(new WeatherState
        {
            WeatherTypeId = "volcanic_ash",  // ✅ From file name!
            // ...
        });
    }
}
```

**Benefits:**
- ✅ Unlimited moddability - just create a file!
- ✅ No conflicts - unique file names
- ✅ Hot-reload friendly
- ✅ Pure data-driven (NO static classes needed)
- ✅ Consistent with TypeEvents system

**See:** [WEATHER-TYPE-IDS.md](WEATHER-TYPE-IDS.md) for complete guide.

### ⚠️ Enum-Based Type IDs (Legacy/Entity-Specific Systems)

**Example: Behavior System (Entity-Specific)**

```csharp
// Enum for NPC behaviors (limited set, entity-specific)
public enum BehaviorType
{
    Idle,
    Patrol,
    Guard,
    Wander
}

// Component uses enum
public struct BehaviorState
{
    public BehaviorType BehaviorType;  // ⚠️ Limited to predefined values
    public int CurrentWaypoint;
    // ...
}

// ❌ Modder CANNOT add custom behavior without recompiling
// ❌ Must edit enum, rebuild engine
```

**Limitations:**
- ❌ Requires recompilation to add new types
- ❌ Mod conflicts if multiple mods add same enum value
- ❌ Limited to ~65,535 values
- ❌ Cannot hot-reload new types

**When Acceptable:**
- ✅ Entity-specific systems (one behavior per NPC)
- ✅ Internal engine systems (not exposed to modders)
- ✅ Fixed set of types unlikely to change

---

## When to Use Each

### Use String Type IDs When:

1. **Global Systems** - Weather, terrain, time-of-day
2. **Unlimited Moddability** - Modders should add custom types
3. **Data-Driven** - Types loaded from JSON/config files
4. **Hot-Reload Required** - Types change without restart
5. **Cross-Mod Compatibility** - Multiple mods adding types

**Examples:**
- ✅ Weather System (`"rain"`, `"mymod:volcanic_ash"`)
- ✅ Terrain Types (`"grass"`, `"lava"`, `"ice"`)
- ✅ Status Effects (`"burn"`, `"poison"`, `"frozen"`)
- ✅ Move Types (if moddable moves allowed)

### Use Enums When:

1. **Entity-Specific** - One type per entity instance
2. **Internal Systems** - Not exposed to modders
3. **Compile-Time Safety Needed** - Critical engine code
4. **Fixed Set** - Types rarely/never change

**Examples:**
- ⚠️ NPC Behaviors (entity-specific, internal)
- ⚠️ Internal ECS component types
- ⚠️ Engine-level type identifiers

### File-Based Discovery (Pure Data-Driven)

```csharp
// String ID for unlimited extensibility
public struct WeatherState
{
    public string WeatherTypeId;  // ✅ From file name
    // ...
}

// Type IDs discovered from file system:
// weather_rain.csx → "rain"
// weather_sunny.csx → "sunny"
// weather_mymod_volcanic.csx → "mymod_volcanic"

// NO static class needed!
// NO enum definitions!
// Just create the file and it works!

// Usage:
WeatherTypeId = "rain";              // ✅ From weather_rain.csx
WeatherTypeId = "mymod_volcanic";    // ✅ From weather_mymod_volcanic.csx
```

## Architecture Layers

### Layer 1: Type Definition (Enum)

Enums provide compile-time type safety and identity.

```csharp
// Identity layer - What types exist?
public enum WeatherType
{
    None,
    Rain,
    Sunny,
    Sandstorm,
    Hail
}

public enum BehaviorType
{
    Idle,
    Patrol,
    Guard,
    Follow
}

public enum TerrainType
{
    Grass,
    Water,
    Sand,
    Rock
}
```

**Purpose:**
- Type identity and categorization
- Compile-time safety
- Switch statement support
- Serialization keys

### Layer 2: Behavior Implementation (Scripts)

Scripts implement behavior for each type.

```csharp
// Behavior layer - How does this type behave?
public class RainWeather : TypeScriptBase
{
    public override void OnActivated(ScriptContext ctx)
    {
        // Rain behavior: apply blue tint, spawn particles
    }
}

public class PatrolBehavior : TypeScriptBase
{
    public override void OnTick(ScriptContext ctx, float deltaTime)
    {
        // Patrol behavior: move between waypoints
    }
}
```

**Purpose:**
- Hot-reloadable behavior
- Moddable logic
- Per-type customization
- Event handling

### Layer 3: State Storage (Components)

ECS components store per-entity or global state.

```csharp
// State layer - Where is the type's data stored?
public struct WeatherState
{
    public WeatherType WeatherType;
    public float Duration;
    public float RemainingTime;
    public Color TintColor;
    // Battle modifiers, etc.
}

public struct BehaviorState
{
    public BehaviorType BehaviorType;
    public int CurrentWaypoint;
    public float Timer;
    // Behavior-specific state
}
```

**Purpose:**
- Per-entity state isolation
- Hot-reload safety
- Memory efficiency
- Query optimization

### Layer 4: Execution Logic (Systems)

Systems process entities with components.

```csharp
// Execution layer - When does the type update?
public class WeatherSystem : ISystem
{
    public void Update(World world, float deltaTime)
    {
        // Find all weather scripts and execute OnTick
        var query = world.Query<WeatherState>();
        foreach (ref var state in query)
        {
            var script = GetScriptForWeather(state.WeatherType);
            script?.OnTick(ctx, deltaTime);
        }
    }
}

public class BehaviorSystem : ISystem
{
    public void Update(World world, float deltaTime)
    {
        // Execute all NPC behaviors
        var query = world.Query<BehaviorState, Position>();
        foreach (ref var state in query)
        {
            var script = GetScriptForBehavior(state.BehaviorType);
            script?.OnTick(ctx, deltaTime);
        }
    }
}
```

**Purpose:**
- Frame-by-frame execution
- Query optimization
- Batch processing
- System coordination

## Type Systems

### 1. Behavior Type System

**Purpose:** NPC AI behaviors (patrol, guard, wander, etc.)

**Enum:** `BehaviorType`
```csharp
public enum BehaviorType
{
    Idle,
    Patrol,
    Guard,
    Wander,
    Follow,
    Flee
}
```

**Script Pattern:** Entity-based (one instance per NPC)
```csharp
public class PatrolBehavior : TypeScriptBase
{
    public override void OnActivated(ScriptContext ctx)
    {
        // ctx.Entity is the NPC
        ctx.World.Add(ctx.Entity.Value, new PatrolState { ... });
    }

    public override void OnTick(ScriptContext ctx, float deltaTime)
    {
        // Update this NPC's patrol
        ref var state = ref ctx.GetState<PatrolState>();
        // Move to next waypoint...
    }
}
```

**Component:** `BehaviorState`
```csharp
public struct BehaviorState
{
    public BehaviorType BehaviorType;
    public int CurrentWaypoint;
    public float Timer;
}
```

**Usage:**
```csharp
// Assign behavior to NPC
var npc = world.Create(
    new BehaviorState { BehaviorType = BehaviorType.Patrol },
    new Position { X = 10, Y = 20 }
);

// System executes behavior
var script = behaviorRegistry.GetScript(BehaviorType.Patrol);
script.OnTick(new ScriptContext(world, npc, logger), deltaTime);
```

### 2. Weather Type System (File-Based Discovery)

**Purpose:** Global weather effects (rain, snow, sandstorm, etc.)

**Type ID:** String-based, discovered from file names

```csharp
// ✅ String-based type ID from file names
public struct WeatherState
{
    public string WeatherTypeId;  // From file: weather_rain.csx → "rain"
    public float Duration;
    public float RemainingTime;
    public bool IsActive;
    public Color TintColor;
    public float WaterMoveMultiplier;
    public float FireMoveMultiplier;
}

// ✅ NO static class needed!
// Type IDs come from file names:
// weather_rain.csx → "rain"
// weather_sunny.csx → "sunny"
// weather_sandstorm.csx → "sandstorm"
```

**Script Pattern:** Global (no entity, affects entire world)
```csharp
// File: weather_rain.csx → Type ID: "rain"
public class RainWeather : TypeScriptBase
{
    public override void OnActivated(ScriptContext ctx)
    {
        // ctx.Entity is NULL (global script)
        // Create weather state entity
        var weatherEntity = ctx.World.Create(new WeatherState
        {
            WeatherTypeId = "rain",  // ✅ From weather_rain.csx file name
            Duration = 10.0f,
            // ...
        });
    }

    public override void OnTick(ScriptContext ctx, float deltaTime)
    {
        // Apply rain effects to ALL entities
        var query = ctx.World.Query<Sprite>();
        query.ForEach((ref Sprite sprite) =>
        {
            sprite.Tint = Color.LightBlue;
        });
    }
}
```

**Usage:**
```csharp
// Activate weather globally
var weatherScript = scriptService.LoadScript("weather_rain.csx");
weatherScript.OnActivated(new ScriptContext(world, entity: null, logger));

// Battle system reads weather
var query = world.Query<WeatherState>();
foreach (ref var chunk in query)
{
    ref var weather = ref chunk.GetFirst<WeatherState>()[0];

    // ✅ String comparison
    if (weather.IsActive && weather.WeatherTypeId == "rain")
    {
        if (moveType == MoveType.Water)
        {
            damage *= weather.WaterMoveMultiplier;
        }
    }
}
```

**Why File-Based?**
- ✅ Modders just create `weather_<type_id>.csx` file
- ✅ No enum conflicts between mods
- ✅ No static classes needed
- ✅ Pure data-driven architecture
- ✅ Hot-reload friendly
- ✅ Consistent with TypeEvents system

**See:** [WEATHER-TYPE-IDS.md](WEATHER-TYPE-IDS.md) for complete guide.

### 3. Terrain Type System (Future)

**Purpose:** Ground tile effects (grass encounters, water swimming, lava damage)

**Enum:** `TerrainType`
```csharp
public enum TerrainType
{
    Normal,
    Grass,      // Random encounters
    Water,      // Requires surf
    Sand,       // Speed reduction
    Ice,        // Slippery movement
    Lava        // Damage over time
}
```

**Script Pattern:** Tile-based (attached to map tiles)
```csharp
public class GrassTerrainBehavior : TypeScriptBase
{
    public override void OnPlayerEnter(ScriptContext ctx)
    {
        // Random encounter check
        if (Random() < 0.05f) // 5% chance
        {
            TriggerWildEncounter(ctx);
        }
    }
}
```

**Component:** `TerrainState`
```csharp
public struct TerrainState
{
    public TerrainType TerrainType;
    public float EncounterRate;
    public float SpeedMultiplier;
    public float DamagePerStep;
}
```

### 4. Item Type System (Future)

**Purpose:** Usable items (potions, TMs, key items)

**Enum:** `ItemType`
```csharp
public enum ItemType
{
    Potion,
    SuperPotion,
    Pokeball,
    TM_Thunderbolt,
    KeyCard
}
```

**Script Pattern:** Entity-based (item in inventory)
```csharp
public class PotionBehavior : TypeScriptBase
{
    public override void OnUse(ScriptContext ctx, Entity target)
    {
        // Heal target by 20 HP
        ref var health = ref ctx.World.Get<Health>(target);
        health.Current = Math.Min(health.Current + 20, health.Max);
    }
}
```

## Weather Type System

### Detailed Architecture

The Weather Type System demonstrates the full 4-layer pattern:

```
┌─────────────────────────────────────────────────────────────┐
│ Layer 1: WeatherType Enum                                  │
│ - Defines available weather types                          │
│ - Compile-time type safety                                 │
│ - Rain, Sunny, Sandstorm, Hail, etc.                       │
└─────────────────────────────────────────────────────────────┘
                         ↓
┌─────────────────────────────────────────────────────────────┐
│ Layer 2: Weather Scripts (.csx)                            │
│ - TypeScriptBase implementations                           │
│ - Global script pattern (ctx.Entity == null)               │
│ - OnActivated, OnTick, OnDeactivated                       │
│ - Hot-reloadable behavior                                  │
└─────────────────────────────────────────────────────────────┘
                         ↓
┌─────────────────────────────────────────────────────────────┐
│ Layer 3: WeatherState Component                            │
│ - ECS component on dedicated weather entity                │
│ - Duration, intensity, battle modifiers                    │
│ - Survives hot-reload                                      │
└─────────────────────────────────────────────────────────────┘
                         ↓
┌─────────────────────────────────────────────────────────────┐
│ Layer 4: WeatherSystem / BattleSystem                      │
│ - Queries WeatherState components                          │
│ - Executes weather scripts every frame                     │
│ - Applies battle modifiers                                 │
└─────────────────────────────────────────────────────────────┘
```

### Weather vs Behavior Differences

| Aspect | Weather System | Behavior System |
|--------|---------------|-----------------|
| **Script Type** | Global | Entity |
| **ctx.Entity** | null | Has value |
| **State Storage** | Dedicated weather entity | Per-NPC component |
| **Execution** | Once per frame | Once per entity |
| **Scope** | Entire world | Single entity |
| **Use Cases** | Rain, sandstorm, day/night | Patrol, guard, follow |

### Weather Integration Points

#### 1. Visual System

```csharp
// Rendering system reads WeatherState
var weatherQuery = world.Query<WeatherState>();
foreach (ref var weather in weatherQuery)
{
    if (weather.IsActive)
    {
        // Apply screen tint
        spriteBatch.Draw(tintOverlay, fullScreen, weather.TintColor);

        // Spawn weather particles
        particleSystem.Emit(weather.WeatherType);
    }
}
```

#### 2. Battle System

```csharp
// Battle system applies weather modifiers
var weatherQuery = world.Query<WeatherState>();
foreach (ref var weather in weatherQuery)
{
    if (weather.IsActive)
    {
        switch (moveType)
        {
            case MoveType.Water:
                damage *= weather.WaterMoveMultiplier;
                break;
            case MoveType.Fire:
                damage *= weather.FireMoveMultiplier;
                break;
        }

        // Residual damage (sandstorm, hail)
        if (weather.DamagePerTurn > 0)
        {
            ApplyResidualDamage(target, weather.DamagePerTurn);
        }
    }
}
```

#### 3. Audio System

```csharp
// Audio system plays weather sounds
var weatherQuery = world.Query<WeatherState>();
foreach (ref var weather in weatherQuery)
{
    if (weather.IsActive)
    {
        switch (weather.WeatherType)
        {
            case WeatherType.Rain:
                audioSystem.PlayLoop("rain_ambient.wav");
                break;
            case WeatherType.Sandstorm:
                audioSystem.PlayLoop("sandstorm_wind.wav");
                break;
        }
    }
}
```

## Integration Patterns

### Pattern 1: Type → Script Lookup

```csharp
// Registry maps enum to script instance
public class WeatherRegistry
{
    private Dictionary<WeatherType, TypeScriptBase> _scripts = new();

    public void Register(WeatherType type, TypeScriptBase script)
    {
        _scripts[type] = script;
    }

    public TypeScriptBase? GetScript(WeatherType type)
    {
        return _scripts.TryGetValue(type, out var script) ? script : null;
    }
}

// Usage
var script = weatherRegistry.GetScript(WeatherType.Rain);
script?.OnActivated(ctx);
```

### Pattern 2: Component → Script Execution

```csharp
// System queries components and executes scripts
public class WeatherSystem : ISystem
{
    private WeatherRegistry _registry;

    public void Update(World world, float deltaTime)
    {
        var query = world.Query<WeatherState>();

        foreach (ref var chunk in query)
        {
            ref var state = ref chunk.GetFirst<WeatherState>()[0];

            if (!state.IsActive) continue;

            // Get script for this weather type
            var script = _registry.GetScript(state.WeatherType);
            if (script == null) continue;

            // Execute script
            var ctx = new ScriptContext(world, entity: null, logger);
            script.OnTick(ctx, deltaTime);
        }
    }
}
```

### Pattern 3: Event-Driven Activation

```csharp
// Activate weather based on events
public void OnWeatherChange(WeatherType newWeather)
{
    // Deactivate old weather
    var oldQuery = world.Query<WeatherState>();
    foreach (ref var chunk in oldQuery)
    {
        ref var state = ref chunk.GetFirst<WeatherState>()[0];
        if (state.IsActive)
        {
            var oldScript = weatherRegistry.GetScript(state.WeatherType);
            oldScript?.OnDeactivated(ctx);
        }
    }

    // Activate new weather
    var newScript = weatherRegistry.GetScript(newWeather);
    newScript?.OnActivated(ctx);
}
```

### Pattern 4: Cross-System Communication

```csharp
// Battle system queries weather for modifiers
public float CalculateDamage(Move move, WeatherState weather)
{
    float baseDamage = move.Power;

    // Apply weather modifiers
    if (weather.IsActive)
    {
        baseDamage *= move.Type switch
        {
            MoveType.Water => weather.WaterMoveMultiplier,
            MoveType.Fire => weather.FireMoveMultiplier,
            _ => 1.0f
        };
    }

    return baseDamage;
}
```

## Extension Guide

### Adding a New Type System

Follow these steps to add a new type system (e.g., StatusEffect):

#### Step 1: Define Enum

```csharp
// File: PokeSharp.Core/Types/StatusEffectType.cs
namespace PokeSharp.Core.Types;

public enum StatusEffectType
{
    None,
    Burn,
    Paralyze,
    Poison,
    Sleep,
    Freeze
}
```

#### Step 2: Create Component

```csharp
// File: PokeSharp.Core/Components/StatusEffectState.cs
namespace PokeSharp.Core.Components;

public struct StatusEffectState
{
    public StatusEffectType EffectType;
    public float Duration;
    public float RemainingTime;
    public bool IsActive;
    public float DamagePerTurn;
    public float SpeedMultiplier;
}
```

#### Step 3: Create Script Template

```csharp
// File: Assets/Scripts/StatusEffects/burn.csx
using Arch.Core;
using PokeSharp.Core.Components;
using PokeSharp.Scripting;

public class BurnEffect : TypeScriptBase
{
    public override void OnActivated(ScriptContext ctx)
    {
        // Entity-based script (attached to Pokemon)
        ctx.World.Add(ctx.Entity.Value, new StatusEffectState
        {
            EffectType = StatusEffectType.Burn,
            Duration = float.MaxValue, // Infinite
            IsActive = true,
            DamagePerTurn = 1.0f // 1 HP per turn
        });
    }

    public override void OnTick(ScriptContext ctx, float deltaTime)
    {
        ref var state = ref ctx.GetState<StatusEffectState>();

        // Burn: 50% attack reduction
        if (ctx.HasState<BattleStats>())
        {
            ref var stats = ref ctx.GetState<BattleStats>();
            stats.AttackMultiplier = 0.5f;
        }
    }

    public override void OnDeactivated(ScriptContext ctx)
    {
        ctx.RemoveState<StatusEffectState>();
    }
}

return new BurnEffect();
```

#### Step 4: Create System

```csharp
// File: PokeSharp.Core/Systems/StatusEffectSystem.cs
public class StatusEffectSystem : ISystem
{
    private ScriptRegistry<StatusEffectType> _registry;

    public void Update(World world, float deltaTime)
    {
        var query = world.Query<StatusEffectState>();

        foreach (ref var chunk in query)
        {
            var entities = chunk.Entities;
            ref var states = ref chunk.GetFirst<StatusEffectState>();

            for (int i = 0; i < chunk.Size; i++)
            {
                ref var state = ref states[i];
                if (!state.IsActive) continue;

                // Execute status effect script
                var script = _registry.GetScript(state.EffectType);
                var ctx = new ScriptContext(world, entities[i], logger);
                script?.OnTick(ctx, deltaTime);

                // Update duration
                state.RemainingTime -= deltaTime;
                if (state.RemainingTime <= 0)
                {
                    state.IsActive = false;
                    script?.OnDeactivated(ctx);
                }
            }
        }
    }
}
```

#### Step 5: Register System

```csharp
// File: PokeSharp.Game/Program.cs
var statusEffectRegistry = new ScriptRegistry<StatusEffectType>(scriptService);
await statusEffectRegistry.LoadAllFromDirectory("Assets/Scripts/StatusEffects/");

var statusEffectSystem = new StatusEffectSystem(statusEffectRegistry, logger);
systemManager.AddSystem(statusEffectSystem);
```

### Best Practices

1. **Enum First** - Define enum before implementing scripts
2. **Component Design** - Keep components small and focused
3. **Script Pattern** - Choose entity vs global based on scope
4. **System Coordination** - Systems should query components, not call each other
5. **Hot-Reload Safety** - Store state in components, never in instance fields
6. **Documentation** - Document each layer clearly

## See Also

- [WEATHER-SYSTEM-GUIDE.md](WEATHER-SYSTEM-GUIDE.md) - Weather architecture
- [TYPE-SYSTEM.md](TYPE-SYSTEM.md) - TypeRegistry implementation
- [NPC-BEHAVIOR-SYSTEM.md](NPC-BEHAVIOR-SYSTEM.md) - Behavior system details
- [SCRIPT-CONTEXT-GUIDE.md](SCRIPT-CONTEXT-GUIDE.md) - ScriptContext API

---

**Document Version:** 1.0
**Last Updated:** 2025-01-06
**Status:** Current
