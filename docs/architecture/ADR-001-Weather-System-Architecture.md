# ADR-001: Weather System Architecture

**Status:** Accepted
**Date:** 2025-11-06
**Decision Makers:** System Architecture Designer
**Related:** ScriptContext Unified Pattern, NpcBehaviorSystem, TypeRegistry

---

## Context

PokeSharp requires a flexible, moddable weather system that can apply global environmental effects to the game world. The system must support:

- Multiple weather types (rain, snow, sandstorm, hail, etc.)
- Dynamic weather transitions and intensity changes
- Per-entity weather effects (speed reduction, visibility, damage)
- Script-based weather behaviors for modding
- Performance efficiency with zero GC pressure

The weather system must integrate with the existing ECS architecture and follow the established ScriptContext pattern used by NPC behaviors.

---

## Decision

We will implement a **global script-based weather system** that follows these architectural principles:

### 1. **Weather Scripts are Global Scripts**

Weather scripts inherit from `TypeScriptBase` but receive a `ScriptContext` with `entity=null`:

```csharp
public class RainWeather : TypeScriptBase
{
    public override void OnTick(ScriptContext ctx, float deltaTime)
    {
        // ctx.IsGlobalScript == true
        // ctx.Entity == null

        // Query for weather state entity
        var query = new QueryDescription().WithAll<WeatherState>();
        ctx.World.Query(in query, (Entity weatherEntity, ref WeatherState state) =>
        {
            // Update weather state
            state.Intensity += deltaTime * 0.1f;

            // Apply effects to world entities
            ApplyRainEffects(ctx.World, state);
        });
    }
}
```

### 2. **State Storage in ECS Components**

Weather state is stored in ECS components, NOT in script instance fields:

- **`WeatherState`**: Global weather configuration (attached to ONE weather entity)
- **`WeatherEffectComponent`**: Per-entity weather effects (speed, visibility, damage)
- **`WeatherTransitionComponent`**: Smooth fade-in/fade-out transitions

### 3. **WeatherSystem Lifecycle Management**

`WeatherSystem.cs` manages weather lifecycle similar to `NpcBehaviorSystem`:

- **Activation**: Creates weather entity with `WeatherState` component
- **Tick**: Executes weather script with global `ScriptContext`
- **Deactivation**: Calls `OnDeactivated` and destroys weather entity
- **Transitions**: Handles fade-in/fade-out by modifying intensity over time

### 4. **ONE Entity Per Active Weather**

Each active weather type creates exactly ONE entity:

```csharp
// WeatherSystem.ActivateWeather("rain", duration: 300f)
Entity rainEntity = world.Create(new WeatherState
{
    WeatherTypeId = "rain",
    Intensity = 0f,  // Start at 0 for fade-in
    Duration = 300f,
    IsActive = true
});
```

Weather scripts query for this entity to access/modify state.

### 5. **TypeRegistry Integration**

Weather definitions are loaded from JSON and scripts are compiled via `TypeRegistry<WeatherDefinition>`:

```json
{
  "typeId": "rain",
  "name": "Rain",
  "description": "Light to heavy rainfall",
  "scriptPath": "Scripts/Weather/rain.csx",
  "defaultDuration": 300.0,
  "defaultIntensity": 1.0,
  "canTransition": true
}
```

---

## Consequences

### Positive

✅ **Consistency**: Weather system follows the same pattern as NPC behaviors
✅ **Zero GC Pressure**: All state stored in struct components
✅ **Type Safety**: `ScriptContext` provides type-safe component access
✅ **Moddability**: Weather types defined in JSON + C# scripts
✅ **Clean Separation**: System handles lifecycle, scripts handle logic
✅ **Global Scope**: Weather scripts can query entire world, not just one entity
✅ **Smooth Transitions**: Built-in fade-in/fade-out support
✅ **Isolation**: Script errors don't crash the system

### Negative

⚠️ **Script Complexity**: Weather script authors must understand ECS queries
⚠️ **Global Context**: Scripts receive `ctx.Entity == null`, must check `IsGlobalScript`
⚠️ **Single Weather Limitation**: Only one weather type active at a time (by design)

### Neutral

🔹 **Learning Curve**: Developers must understand the distinction between entity and global scripts
🔹 **Query Performance**: Weather scripts must query for their state entity each tick

---

## Alternatives Considered

### Alternative 1: Weather as Entity Script

**Rejected**: Would require attaching weather to a specific entity (e.g., player), which doesn't represent the global nature of weather.

### Alternative 2: Singleton Weather Manager

**Rejected**: Would break ECS architecture and introduce GC pressure with reference types.

### Alternative 3: Multiple Active Weather Types

**Rejected**: Added complexity for minimal benefit. Cross-fade transitions can simulate this.

---

## Implementation Details

### Component Structure

```csharp
// Global weather state (one entity per active weather)
public struct WeatherState
{
    public string WeatherTypeId;
    public float Intensity;           // 0.0 to 1.0
    public float ElapsedTime;
    public float Duration;            // -1 for infinite
    public bool IsActive;
    public Dictionary<string, float>? CustomData;
}

// Per-entity weather effects
public struct WeatherEffectComponent
{
    public string WeatherTypeId;
    public float SpeedMultiplier;     // 1.0 = normal
    public float VisibilityMultiplier;
    public float DamagePerTick;
}

// Smooth transitions
public struct WeatherTransitionComponent
{
    public WeatherTransitionType TransitionType;  // FadeIn, FadeOut, CrossFade
    public float TransitionProgress;              // 0.0 to 1.0
    public float TransitionDuration;
}
```

### System Execution Order

```
SystemPriority:
  Input          = 0
  SpatialHash    = 25
  AI             = 50
  NpcBehavior    = 75
  Weather        = 90   ← Weather system runs here
  Movement       = 100
  Collision      = 200
  Animation      = 800
  Render         = 1000
```

Weather runs after NPC behaviors but before movement, allowing weather effects to modify speed multipliers that movement system reads.

### Script Lifecycle Hooks

```csharp
public abstract class TypeScriptBase
{
    // Called once when weather activated
    protected virtual void OnActivated(ScriptContext ctx);

    // Called every frame
    public virtual void OnTick(ScriptContext ctx, float deltaTime);

    // Called when weather deactivated
    protected virtual void OnDeactivated(ScriptContext ctx);
}
```

---

## References

- **NpcBehaviorSystem.cs**: Entity-level script execution pattern
- **ScriptContext.cs**: Unified context API for scripts
- **BehaviorStates.cs**: Component state storage pattern
- **TypeRegistry.cs**: Script registration and caching

---

## Migration Path

No migration required. This is a new system that follows established patterns.

---

## Verification

✅ Weather scripts inherit from `TypeScriptBase`
✅ Weather scripts receive `ScriptContext` with `entity=null`
✅ State stored in `WeatherState` component, not instance fields
✅ `WeatherSystem` manages lifecycle (OnActivated, OnTick, OnDeactivated)
✅ System integrates with `TypeRegistry<WeatherDefinition>`
✅ Smooth transitions via `WeatherTransitionComponent`
✅ System priority set to 90 (after behaviors, before movement)

---

## Decision Rationale

This architecture was chosen because it:

1. **Reuses Proven Patterns**: Same approach as NPC behaviors (already validated)
2. **Maintains ECS Purity**: All state in components, zero GC pressure
3. **Enables Modding**: Scripts are external .csx files compiled at runtime
4. **Scales Efficiently**: Query-based approach handles thousands of entities
5. **Provides Flexibility**: Custom weather behaviors via scripting
6. **Ensures Robustness**: Error isolation prevents script bugs from crashing system

The global script pattern (entity=null) perfectly represents weather's world-wide nature, distinguishing it from entity-bound behaviors like NPC patrol patterns.
