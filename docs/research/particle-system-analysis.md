# Particle System Implementation Research for PokeSharp Mods

**Research Date:** 2025-12-02
**Project:** PokeSharp Game Engine
**Focus:** Particle system design for modding support
**Location:** `/Users/ntomsic/Documents/PokeSharp`

---

## Executive Summary

This document analyzes particle system implementation options for PokeSharp's modding system. After examining the existing codebase, MonoGame ecosystem, and example mod requirements, **we recommend building a custom CPU-based particle system** integrated with the existing ECS architecture and event-driven design. This approach provides the best balance of simplicity, performance, and modder-friendly API design.

### Key Recommendations
1. **Custom CPU particle system** (not GPU-based for simplicity)
2. **ECS-based architecture** using Arch components
3. **Event-driven spawning** via existing `EffectRequestedEvent`
4. **Preset effects library** with advanced customization options
5. **Target performance:** 2000-5000 simultaneous particles at 60fps

---

## Current Codebase Analysis

### 1. Existing Effect System Infrastructure

PokeSharp already has foundational infrastructure for visual effects:

#### **Effect API (Script-facing)**
- **Location:** `/PokeSharp.Game.Scripting/Api/IEffectApi.cs`
- **Purpose:** Script API for spawning visual effects
- **Current API:**
```csharp
void SpawnEffect(
    string effectId,
    Point position,
    float duration = 0.0f,
    float scale = 1.0f,
    Color? tint = null
);
void ClearEffects();
bool HasEffect(string effectId);
```

#### **Effect Event System**
- **Location:** `/PokeSharp.Engine.Core/Types/Events/EffectRequestedEvent.cs`
- **Architecture:** Event-driven (publishes to event bus)
- **Current Implementation:** Event publishing only (no rendering implementation yet)

```csharp
public sealed record EffectRequestedEvent : TypeEventBase
{
    public required string EffectId { get; init; }
    public required Point Position { get; init; }
    public float Duration { get; init; } = 0.0f;
    public float Scale { get; init; } = 1.0f;
    public Color? Tint { get; init; }
}
```

**Analysis:** The groundwork is excellent. We need to build the actual particle system that responds to these events.

---

### 2. Rendering Pipeline Architecture

#### **ElevationRenderSystem**
- **Location:** `/PokeSharp.Engine.Rendering/Systems/ElevationRenderSystem.cs`
- **Key Details:**
  - Uses MonoGame `SpriteBatch` with `SpriteSortMode.BackToFront`
  - Elevation-based depth sorting (0-15 levels)
  - Layer depth formula: `1.0 - ((elevation * 16) + (y / mapHeight) + (mapId * 0.1))`
  - Renders tiles, sprites, and image layers
  - **Performance:** Logs every 600 frames, handles 200+ entities efficiently

**Integration Point:** Particles should render in the same `SpriteBatch` pass for proper z-ordering with tiles/sprites.

#### **Rendering Flow**
```
1. UpdateCameraCache() - Calculate view frustum
2. _spriteBatch.Begin(BackToFront sorting)
3. RenderImageLayers() - Background images
4. RenderBorders() - Map borders
5. RenderAllTiles() - Tile rendering with elevation
6. RenderAllSprites() - Entity sprites with elevation
   -> [PARTICLE RENDERING WOULD GO HERE]
7. _spriteBatch.End()
```

---

### 3. ECS Component Architecture

**Existing Component Pattern:**
```csharp
// Components are simple data structs
public struct Position : IComponent
{
    public int X, Y;
    public float PixelX, PixelY;
    public MapRuntimeId MapId;
}

public struct Elevation : IComponent
{
    public byte Value; // 0-15
}
```

**For Particles, We'd Add:**
```csharp
public struct ParticleEmitter : IComponent
{
    public string EffectId;
    public Point Position;
    public float EmissionRate;
    public bool IsActive;
    // ... particle properties
}

public struct Particle : IComponent
{
    public Vector2 Position;
    public Vector2 Velocity;
    public float Lifetime;
    public float Age;
    public Color Tint;
    public float Scale;
    // ... physics properties
}
```

---

### 4. Example Mod Requirements Analysis

#### **Weather System Mod** (`/Mods/examples/weather-system`)
- **Needs:** Rain particles, lightning flashes, snow particles
- **Complexity:** Medium (100-300 particles for rain effect)
- **Priority:** High (core visual feature)

```json
{
  "name": "Weather System",
  "description": "Dynamic weather changes with visual effects",
  "scripts": [
    "weather_controller.csx",
    "rain_effects.csx",  // <-- Needs particle system
    "thunder_effects.csx"
  ]
}
```

#### **Enhanced Ledges Mod** (`/Mods/examples/enhanced-ledges`)
- **Needs:** Dust particles on landing, sparkle effects
- **Complexity:** Low (5-20 particles per effect)
- **Priority:** Medium (polish feature)

```json
{
  "scripts": [
    "visual_effects.csx"  // <-- Needs particle system
  ]
}
```

#### **Quest System Mod** (`/Mods/examples/quest-system`)
- **Needs:** Sparkle effects on quest items/objectives
- **Complexity:** Low (10-30 particles, persistent)
- **Priority:** Medium (visual feedback)

**Analysis:** Most mods need simple, preset effects rather than complex particle simulations. A preset-focused API with customization options is ideal.

---

## MonoGame Particle System Survey

### Option 1: Mercury Particle Engine ⭐ (Recommended for reference)
- **GitHub:** https://github.com/MercuryEngine/MercuryParticleEngine
- **Status:** Archived, but excellent design patterns
- **Features:**
  - CPU-based particle simulation
  - Modifiers: gravity, drag, rotation, scale, color fading
  - Emitters: point, line, ring, box
  - Texture support with blending modes

**Pros:**
- Clean, modular architecture
- Well-documented code
- Suitable for 2D games

**Cons:**
- No longer maintained
- Would require integration work
- Overkill for simple effects

### Option 2: Custom CPU Particle System ⭐⭐⭐ (Recommended)
Build a lightweight system tailored to PokeSharp's needs.

**Pros:**
- Full control over features
- Perfect ECS integration
- Minimal dependencies
- Modder-friendly API

**Cons:**
- Development time
- Need to implement common features

### Option 3: GPU Particle Systems (HLSL/Compute Shaders)
Use GPU compute for massive particle counts.

**Pros:**
- Can handle 10,000+ particles
- Minimal CPU overhead

**Cons:**
- Much more complex implementation
- Overkill for Pokemon-style 2D game
- Harder for modders to customize
- May not work on all platforms

### Option 4: Simple Sprite Animation System
Use animated sprites instead of particles.

**Pros:**
- Very simple
- Easy for modders

**Cons:**
- Less flexible
- Doesn't scale (many simultaneous effects)
- Not suitable for dynamic effects (rain, snow)

---

## Recommended Architecture

### Design Philosophy
1. **Simplicity First:** CPU-based, easy to understand
2. **ECS Native:** Components + Systems pattern
3. **Event-Driven:** Integrates with existing event bus
4. **Preset Library:** Common effects out-of-the-box
5. **Extensible:** Advanced users can customize

### Component Design

```csharp
/// <summary>
/// Particle emitter - spawns particles over time
/// </summary>
public struct ParticleEmitter : IComponent
{
    public string EffectId;           // "rain", "sparkle", "dust"
    public Point GridPosition;        // Spawn position (grid coords)
    public byte Elevation;            // For depth sorting

    // Emission properties
    public float EmissionRate;        // Particles per second (0 = burst)
    public int BurstCount;            // Particles in single burst
    public float Duration;            // Emitter lifetime (0 = infinite)
    public float Age;                 // Current age
    public bool IsActive;

    // Spawn area
    public EmitterShape Shape;        // Point, Circle, Rectangle, Line
    public Vector2 SpawnArea;         // Size of spawn area

    // Particle initial properties
    public ParticleTemplate Template; // Reference to particle settings
}

/// <summary>
/// Individual particle instance
/// </summary>
public struct Particle : IComponent
{
    // Transform
    public Vector2 Position;          // World pixel position
    public Vector2 Velocity;
    public float Rotation;
    public float AngularVelocity;

    // Lifetime
    public float MaxLifetime;
    public float Age;

    // Rendering
    public string TextureKey;
    public Rectangle SourceRect;
    public Color StartColor;
    public Color EndColor;           // For color interpolation
    public float StartScale;
    public float EndScale;           // For scale interpolation
    public byte Elevation;           // For depth sorting

    // Physics (optional)
    public Vector2 Acceleration;     // Gravity, wind, etc.
    public float Drag;               // Air resistance
}

/// <summary>
/// Template for particle properties (shared data)
/// </summary>
public struct ParticleTemplate
{
    public string TextureKey;
    public Rectangle SourceRect;

    // Lifetime range
    public float MinLifetime;
    public float MaxLifetime;

    // Velocity range
    public Vector2 MinVelocity;
    public Vector2 MaxVelocity;

    // Visual range
    public Color StartColor;
    public Color EndColor;
    public float MinScale;
    public float MaxScale;

    // Physics
    public Vector2 Gravity;
    public float Drag;
    public float RotationSpeed;
}
```

### System Design

```csharp
/// <summary>
/// Listens for EffectRequestedEvent and spawns emitters
/// Priority: Before ParticleUpdateSystem
/// </summary>
public class ParticleSpawnSystem : SystemBase, IUpdateSystem
{
    private readonly IEventBus _eventBus;
    private readonly Dictionary<string, ParticleEffectPreset> _presets;

    public override void Update(World world, float deltaTime)
    {
        // Subscribe to EffectRequestedEvent
        // Create ParticleEmitter component from preset
        // Handle both continuous and burst modes
    }
}

/// <summary>
/// Updates particle emitters and spawns new particles
/// Priority: Before ParticleUpdateSystem
/// </summary>
public class EmitterUpdateSystem : SystemBase, IUpdateSystem
{
    public override void Update(World world, float deltaTime)
    {
        // Query all ParticleEmitter components
        // Update emitter age, check if still active
        // Spawn new particles based on emission rate
        // Create Particle components for new particles
    }
}

/// <summary>
/// Updates individual particle physics and lifetime
/// Priority: After EmitterUpdateSystem, before rendering
/// </summary>
public class ParticleUpdateSystem : SystemBase, IUpdateSystem
{
    public override void Update(World world, float deltaTime)
    {
        // Query all Particle components
        // Update position: pos += velocity * dt
        // Apply acceleration: velocity += acceleration * dt
        // Apply drag: velocity *= (1 - drag * dt)
        // Update rotation
        // Increment age
        // Destroy particles that exceed lifetime
    }
}

/// <summary>
/// Renders all active particles
/// Integrates with ElevationRenderSystem's SpriteBatch
/// </summary>
public class ParticleRenderSystem : SystemBase, IRenderSystem
{
    private readonly SpriteBatch _spriteBatch;
    private readonly AssetManager _assets;

    public int RenderOrder => 1; // Same as sprites

    public void Render(World world)
    {
        // Query all Particle components
        // Calculate interpolated color (StartColor -> EndColor)
        // Calculate interpolated scale (StartScale -> EndScale)
        // Calculate layerDepth using elevation + Y position
        // Draw particle texture with SpriteBatch
    }
}
```

---

## Modder API Design

### Simple Preset API (Primary Use Case)

```csharp
// Weather System - Rain effect
ctx.Effects.Rain(position, intensity: 0.8f);

// Enhanced Ledges - Dust cloud on landing
ctx.Effects.Dust(position, direction: Direction.Down, amount: 10);

// Quest System - Sparkle on item
ctx.Effects.Sparkle(position, color: Color.Gold, duration: 2.0f);

// Explosion effect
ctx.Effects.Explosion(position, scale: 1.5f);

// Trail effect (follows entity)
var trail = ctx.Effects.Trail(entityId, color: Color.Blue, lifetime: 0.5f);
```

### Preset Library

```csharp
public static class ParticleEffectPresets
{
    public static ParticleEffectPreset Rain => new()
    {
        EffectId = "rain",
        EmitterShape = EmitterShape.Rectangle,
        SpawnArea = new Vector2(320, 16), // Screen width
        EmissionRate = 50, // 50 particles/second
        Template = new ParticleTemplate
        {
            TextureKey = "particles/rain_drop",
            MinLifetime = 0.5f,
            MaxLifetime = 1.0f,
            MinVelocity = new Vector2(-10, 100),
            MaxVelocity = new Vector2(10, 150),
            StartColor = Color.White * 0.7f,
            EndColor = Color.Transparent,
            MinScale = 0.8f,
            MaxScale = 1.2f,
            Gravity = new Vector2(0, 50),
        }
    };

    public static ParticleEffectPreset Dust => new()
    {
        EffectId = "dust",
        EmitterShape = EmitterShape.Circle,
        SpawnArea = new Vector2(8, 8),
        BurstCount = 15,
        Template = new ParticleTemplate
        {
            TextureKey = "particles/dust",
            MinLifetime = 0.3f,
            MaxLifetime = 0.6f,
            MinVelocity = new Vector2(-30, -40),
            MaxVelocity = new Vector2(30, -20),
            StartColor = new Color(200, 180, 150, 200),
            EndColor = Color.Transparent,
            MinScale = 0.5f,
            MaxScale = 1.0f,
            Drag = 2.0f,
        }
    };

    public static ParticleEffectPreset Sparkle => new()
    {
        EffectId = "sparkle",
        EmitterShape = EmitterShape.Point,
        EmissionRate = 5,
        Template = new ParticleTemplate
        {
            TextureKey = "particles/star",
            MinLifetime = 0.5f,
            MaxLifetime = 1.0f,
            MinVelocity = new Vector2(-20, -30),
            MaxVelocity = new Vector2(20, -10),
            StartColor = Color.White,
            EndColor = Color.Transparent,
            MinScale = 0.5f,
            MaxScale = 1.5f,
            RotationSpeed = 5.0f,
        }
    };

    // Additional presets: snow, fire, smoke, leaves, bubbles, lightning, heal, etc.
}
```

### Advanced Customization API (Power Users)

```csharp
// Create custom emitter
var emitter = ctx.Effects.CreateEmitter(position);
emitter.Shape = EmitterShape.Ring;
emitter.SpawnArea = new Vector2(16, 16);
emitter.EmissionRate = 30;
emitter.Duration = 3.0f;

// Customize particle template
emitter.Template.TextureKey = "custom/my_particle";
emitter.Template.MinLifetime = 1.0f;
emitter.Template.MaxLifetime = 2.0f;
emitter.Template.MinVelocity = new Vector2(-50, -50);
emitter.Template.MaxVelocity = new Vector2(50, 50);
emitter.Template.StartColor = Color.Red;
emitter.Template.EndColor = Color.Yellow;
emitter.Template.Gravity = new Vector2(0, 100);
emitter.Template.Drag = 0.5f;

// Start emitting
emitter.Start();
```

---

## Performance Considerations

### Target Performance Metrics

| Scenario | Particle Count | Target FPS | Notes |
|----------|---------------|------------|-------|
| Rain (light) | 100-200 | 60 fps | Typical weather effect |
| Rain (heavy) | 300-500 | 60 fps | Storm conditions |
| Multiple effects | 500-1000 | 60 fps | Several mods active |
| Stress test | 2000-5000 | 30-60 fps | Maximum reasonable load |

### Optimization Strategies

#### 1. **Object Pooling**
```csharp
// Reuse Particle entities instead of creating/destroying
public class ParticlePool
{
    private readonly Queue<Entity> _pool = new();

    public Entity Acquire(World world)
    {
        if (_pool.Count > 0)
            return _pool.Dequeue();

        return world.Create<Particle>();
    }

    public void Release(Entity particle)
    {
        _pool.Enqueue(particle);
    }
}
```

#### 2. **Frustum Culling**
Only update/render particles visible to camera:
```csharp
Rectangle cameraBounds = GetCameraBounds();
if (!cameraBounds.Contains(particle.Position))
    continue; // Skip this particle
```

#### 3. **LOD (Level of Detail)**
Reduce particle count when far from camera:
```csharp
float distanceToCamera = Vector2.Distance(particle.Position, cameraPos);
if (distanceToCamera > lodDistance)
{
    emitter.EmissionRate *= 0.5f; // Half rate at distance
}
```

#### 4. **Batching**
Render all particles of same texture in one draw call:
```csharp
// Group particles by texture
var particlesByTexture = particles.GroupBy(p => p.TextureKey);

foreach (var group in particlesByTexture)
{
    Texture2D texture = assets.GetTexture(group.Key);
    foreach (var particle in group)
    {
        spriteBatch.Draw(texture, ...); // Batched
    }
}
```

#### 5. **Update Rate Throttling**
Update distant particles less frequently:
```csharp
if (distanceToCamera > threshold && frameCount % 2 != 0)
    continue; // Update every other frame
```

### Memory Budget

```
Single Particle Size:
- Position (Vector2): 8 bytes
- Velocity (Vector2): 8 bytes
- Acceleration (Vector2): 8 bytes
- Age, Lifetime, Rotation, Scale: 16 bytes
- Color: 4 bytes
- Texture references: 8 bytes
= ~60 bytes per particle

2000 particles = ~120 KB
5000 particles = ~300 KB
```

**Verdict:** Memory is not a concern. CPU time for updates is the bottleneck.

---

## Integration Roadmap

### Phase 1: Core Infrastructure (Week 1)
- [ ] Create `Particle` component
- [ ] Create `ParticleEmitter` component
- [ ] Implement `ParticleUpdateSystem`
- [ ] Implement `ParticleRenderSystem`
- [ ] Integrate with `ElevationRenderSystem` SpriteBatch
- [ ] Basic particle physics (velocity, lifetime)

### Phase 2: Emitter System (Week 1-2)
- [ ] Implement `EmitterUpdateSystem`
- [ ] Support emission modes (continuous, burst)
- [ ] Emitter shapes (point, circle, rectangle, line)
- [ ] Particle spawning from emitters

### Phase 3: Event Integration (Week 2)
- [ ] Implement `ParticleSpawnSystem`
- [ ] Listen to `EffectRequestedEvent`
- [ ] Create emitters from effect IDs
- [ ] Load particle templates from JSON

### Phase 4: Preset Library (Week 2-3)
- [ ] Rain effect preset
- [ ] Snow effect preset
- [ ] Dust/smoke preset
- [ ] Sparkle/star preset
- [ ] Explosion preset
- [ ] Create particle textures (16x16 simple sprites)

### Phase 5: Modder API (Week 3)
- [ ] Simple preset API in `IEffectApi`
- [ ] Advanced customization API
- [ ] Documentation with examples
- [ ] Sample mod demonstrating usage

### Phase 6: Optimization (Week 4)
- [ ] Object pooling for particles
- [ ] Frustum culling
- [ ] Texture batching
- [ ] Performance profiling
- [ ] LOD system if needed

---

## Example Implementation Snippets

### Particle Update Loop (Core Logic)

```csharp
public class ParticleUpdateSystem : SystemBase, IUpdateSystem
{
    private readonly QueryDescription _particleQuery =
        QueryCache.Get<Particle>();

    public override void Update(World world, float deltaTime)
    {
        world.Query(in _particleQuery, (Entity entity, ref Particle particle) =>
        {
            // Update age
            particle.Age += deltaTime;

            // Check if particle should be destroyed
            if (particle.Age >= particle.MaxLifetime)
            {
                world.Destroy(entity);
                return;
            }

            // Apply acceleration (gravity, wind, etc.)
            particle.Velocity += particle.Acceleration * deltaTime;

            // Apply drag
            particle.Velocity *= (1.0f - particle.Drag * deltaTime);

            // Update position
            particle.Position += particle.Velocity * deltaTime;

            // Update rotation
            particle.Rotation += particle.AngularVelocity * deltaTime;
        });
    }
}
```

### Particle Rendering (Integration with ElevationRenderSystem)

```csharp
public class ParticleRenderSystem : SystemBase, IRenderSystem
{
    private readonly SpriteBatch _spriteBatch;
    private readonly AssetManager _assets;
    private readonly QueryDescription _particleQuery =
        QueryCache.Get<Particle>();

    public int RenderOrder => 1; // Same as entity sprites

    public void Render(World world)
    {
        world.Query(in _particleQuery, (ref Particle particle) =>
        {
            // Skip if texture not loaded
            if (!_assets.HasTexture(particle.TextureKey))
                return;

            Texture2D texture = _assets.GetTexture(particle.TextureKey);

            // Calculate interpolated properties
            float t = particle.Age / particle.MaxLifetime;
            Color color = Color.Lerp(particle.StartColor, particle.EndColor, t);
            float scale = MathHelper.Lerp(particle.StartScale, particle.EndScale, t);

            // Calculate layer depth using elevation + Y position
            // Same formula as ElevationRenderSystem
            float normalizedY = particle.Position.Y / 1000.0f;
            float depth = (particle.Elevation * 16.0f) + normalizedY;
            float layerDepth = 1.0f - (depth / 251.0f);

            // Draw particle
            _spriteBatch.Draw(
                texture,
                particle.Position,
                particle.SourceRect,
                color,
                particle.Rotation,
                new Vector2(8, 8), // Center origin for 16x16 texture
                scale,
                SpriteEffects.None,
                layerDepth
            );
        });
    }
}
```

### Rain Effect Usage in Mod Script

```csharp
// weather_system.csx
public class WeatherController : ScriptBase
{
    private bool _isRaining = false;
    private EntityId _rainEmitterId;

    public override void OnMapEnter(MapContext ctx)
    {
        // Check weather condition
        if (ctx.Map.Weather == WeatherType.Rain)
        {
            StartRain(ctx);
        }
    }

    private void StartRain(MapContext ctx)
    {
        // Simple API - preset effect
        _rainEmitterId = ctx.Effects.Rain(
            position: ctx.Camera.TopLeft,
            intensity: 0.8f
        );

        _isRaining = true;
    }

    private void StopRain(MapContext ctx)
    {
        ctx.Effects.Stop(_rainEmitterId);
        _isRaining = false;
    }

    public override void OnUpdate(MapContext ctx, float deltaTime)
    {
        // Move rain emitter with camera
        if (_isRaining)
        {
            ctx.Effects.SetPosition(_rainEmitterId, ctx.Camera.TopLeft);
        }
    }
}
```

---

## Particle Texture Requirements

### Texture Atlas Layout
Create a single `particles.png` texture atlas (512x512):

```
[dust] [smoke] [spark] [star] [rain] [snow]
[leaf] [fire] [bubble] [heart] [note] [lightning]
```

Each particle sprite: **16x16 pixels**

### Required Textures (MVP)
1. **rain_drop.png** - Simple raindrop (blue streak)
2. **snow_flake.png** - White snowflake (6-pointed)
3. **dust.png** - Gray cloud puff
4. **smoke.png** - White/gray wispy cloud
5. **sparkle.png** - Yellow/white star
6. **star.png** - 5-pointed star (quest markers)
7. **dot.png** - Simple white circle (versatile)

### Texture Format
- **Size:** 16x16 pixels per sprite
- **Format:** PNG with alpha channel
- **Style:** Soft-edged, suitable for additive blending
- **Colors:** Primarily white/gray (tinted at runtime)

---

## Comparison with Other Solutions

| Solution | Complexity | Features | Integration | Verdict |
|----------|-----------|----------|-------------|---------|
| **Custom CPU** | Medium | Tailored | Perfect | ✅ Recommended |
| **Mercury Engine** | Medium-High | Rich | Needs work | ⚠️ Reference only |
| **GPU Particles** | Very High | Massive scale | Complex | ❌ Overkill |
| **Sprite Animation** | Low | Limited | Easy | ❌ Too simple |

---

## Conclusion

**Recommended Approach:** Build a custom CPU-based particle system integrated with PokeSharp's ECS architecture.

### Why This Approach?

1. **Perfect Fit:** Designed specifically for PokeSharp's needs
2. **Simple for Modders:** Preset library covers 90% of use cases
3. **ECS Native:** Components + Systems pattern, no impedance mismatch
4. **Event-Driven:** Integrates seamlessly with existing infrastructure
5. **Performance:** 2000-5000 particles achievable with optimizations
6. **Extensible:** Advanced users can customize deeply

### What Modders Get

#### Simple API (Most Users)
```csharp
ctx.Effects.Rain(position, intensity: 0.8f);
ctx.Effects.Sparkle(position, color: Color.Gold);
```

#### Advanced API (Power Users)
```csharp
var emitter = ctx.Effects.CreateEmitter(position);
emitter.Template.Gravity = new Vector2(0, 100);
emitter.Template.StartColor = Color.Red;
emitter.Start();
```

### Next Steps

1. **Review this research** with team
2. **Approve architecture** and API design
3. **Create particle textures** (16x16 sprites)
4. **Implement Phase 1** (core infrastructure)
5. **Test with example mods** (weather-system, enhanced-ledges)
6. **Document modder API** with examples

---

## References

- **PokeSharp Rendering:** `/PokeSharp.Engine.Rendering/Systems/ElevationRenderSystem.cs`
- **Effect API:** `/PokeSharp.Game.Scripting/Api/IEffectApi.cs`
- **Event System:** `/PokeSharp.Engine.Core/Types/Events/EffectRequestedEvent.cs`
- **MonoGame SpriteBatch:** https://docs.monogame.net/api/Microsoft.Xna.Framework.Graphics.SpriteBatch.html
- **Mercury Particle Engine:** https://github.com/MercuryEngine/MercuryParticleEngine

---

**End of Research Document**
