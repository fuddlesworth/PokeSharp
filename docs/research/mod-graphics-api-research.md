# Mod Graphics API Research

**Project:** PokeSharp
**Date:** 2025-12-02
**Research Focus:** Graphics API design patterns for moddable games

---

## Executive Summary

This research analyzes how successful moddable games expose graphics APIs to modders, evaluating safety, performance, and ease of use. Based on analysis of Minecraft, Terraria, Stardew Valley, Unity, and Godot, this document provides concrete recommendations for PokeSharp's mod graphics API.

**Key Findings:**
- **Declarative APIs** are safer and more maintainable than imperative rendering
- **Component-based patterns** align with PokeSharp's existing ECS architecture
- **Event-driven hooks** provide flexibility without exposing low-level rendering
- **Asset sandboxing** is critical for security and performance
- **Immediate mode APIs** (draw calls) should be avoided in favor of data-driven approaches

**Recommended Approach:**
A **hybrid declarative + event-driven API** that allows mods to:
1. Define sprite components through JSON/templates
2. React to rendering events without direct draw calls
3. Register custom effects/shaders through a sandboxed system
4. Query and modify visual state through safe abstractions

---

## 1. Industry Case Studies

### 1.1 Minecraft (Forge/Fabric)

**Architecture:**
- Block model system (JSON + Java)
- Entity rendering through registration system
- Shader-based effects (limited mod access)
- Resource pack overlay system

**API Pattern:**
```java
// Minecraft's approach: Registration + data-driven models
public class MyMod {
    @Override
    public void registerRenderers(BlockRenderRegistry registry) {
        // Register block renderer
        registry.register(MY_BLOCK, new MyBlockRenderer());
    }
}

// Block renderer interface
public interface BlockRenderer {
    void render(BlockState state, MatrixStack matrices,
                VertexConsumer vertices, int light, int overlay);
}
```

**Pros:**
✅ Clear separation between game logic and rendering
✅ Type-safe Java API with compile-time checks
✅ Extensive community documentation and examples
✅ Batching handled automatically by engine
✅ Resource packs allow texture replacement without code

**Cons:**
❌ Complex registration system with many callbacks
❌ Direct vertex buffer access can cause crashes
❌ Performance issues with poorly written renderers
❌ Breaking changes between Minecraft versions
❌ Steep learning curve for new modders

**Security/Safety:**
- Mods can crash rendering thread with invalid vertices
- No memory limits on vertex buffers
- Resource pack validation is minimal
- Shader access is restricted (good)

**Performance Considerations:**
- Automatic batching reduces draw calls
- Custom renderers can break batching
- VBO management is manual and error-prone
- Culling is automatic but can be overridden

**Relevance to PokeSharp:**
- Registration pattern fits ECS architecture
- Avoid direct vertex access (CSX scripts can't enforce safety)
- Resource pack concept aligns with mod asset loading

---

### 1.2 Terraria (tModLoader)

**Architecture:**
- ModItem/ModNPC classes with sprite overrides
- DrawEffects hooks for custom rendering
- Shader system with predefined effects
- Tile frame system for animations

**API Pattern:**
```csharp
// Terraria's approach: Inheritance + virtual methods
public class MyItem : ModItem {
    public override bool PreDrawInWorld(SpriteBatch spriteBatch,
        Color lightColor, Color alphaColor, ref float rotation,
        ref float scale, int whoAmI) {

        // Custom draw logic here
        spriteBatch.Draw(texture, position, sourceRect, color,
            rotation, origin, scale, SpriteEffects.None, 0f);

        return false; // Skip default rendering
    }
}
```

**Pros:**
✅ Familiar C# syntax for modders
✅ Direct SpriteBatch access for flexibility
✅ Pre/Post hooks allow modification or replacement
✅ Inheritance-based pattern is intuitive
✅ Access to game's existing textures and shaders

**Cons:**
❌ Easy to break rendering with incorrect draw calls
❌ No batching guarantees (modders control SpriteBatch)
❌ Performance issues with excessive draw calls
❌ Difficult to debug rendering errors
❌ No memory/texture size limits

**Security/Safety:**
- Mods can infinite loop in draw hooks (freeze game)
- No validation of texture dimensions
- Direct GPU access through SpriteBatch
- Reflection can access private rendering code

**Performance Considerations:**
- Mods often break sprite batching
- Each PreDraw/PostDraw call adds overhead
- No automatic culling in custom renderers
- Texture atlas usage is manual

**Relevance to PokeSharp:**
- Hook pattern (Pre/Post) is powerful but dangerous
- CSX scripts need more guard rails than compiled C# mods
- SpriteBatch access should be restricted

---

### 1.3 Stardew Valley (SMAPI)

**Architecture:**
- Content pipeline with asset loaders
- Event-based rendering hooks
- Texture replacement through content patcher
- Limited direct rendering access

**API Pattern:**
```csharp
// Stardew's approach: Event-driven + asset replacement
public class MyMod : Mod {
    public override void Entry(IModHelper helper) {
        // Register asset loader
        helper.Content.AssetLoaders.Add(new MyAssetLoader());

        // Subscribe to render event
        helper.Events.Display.RenderedWorld += OnRenderedWorld;
    }

    private void OnRenderedWorld(object sender, RenderedWorldEventArgs e) {
        // Draw on top of world using SpriteBatch
        e.SpriteBatch.Draw(myTexture, position, Color.White);
    }
}
```

**Pros:**
✅ Event-driven API is safe and predictable
✅ Content patcher handles 90% of visual mods without code
✅ Asset replacement is validated and sandboxed
✅ Clear rendering order (RenderingWorld, RenderedWorld, etc.)
✅ Minimal impact on performance when not rendering

**Cons:**
❌ Limited flexibility compared to Terraria/Minecraft
❌ Cannot modify existing sprite rendering easily
❌ No custom shaders or effects
❌ SpriteBatch state can be corrupted by mods
❌ Event timing issues between mods

**Security/Safety:**
- Asset replacement is validated (format, dimensions)
- Events are fired at safe points in render loop
- No direct GPU access
- Still possible to freeze game with infinite draw loops

**Performance Considerations:**
- Events add minimal overhead when unused
- Mods render after main game (good layering)
- No automatic batching for mod draws
- Asset caching is automatic

**Relevance to PokeSharp:**
- **BEST MATCH**: Event-driven + asset replacement
- Aligns with PokeSharp's event bus architecture
- Content patcher concept for non-programmers
- Safe for CSX scripting environment

---

### 1.4 Unity Engine (Editor Extensions)

**Architecture:**
- Component-based GameObject system
- Custom inspectors and gizmos
- Shader Graph for visual shader creation
- ScriptableObject for data-driven content

**API Pattern:**
```csharp
// Unity's approach: Component composition + data binding
[CustomEditor(typeof(MyComponent))]
public class MyComponentEditor : Editor {
    public override void OnInspectorGUI() {
        // Draw custom inspector
        EditorGUILayout.PropertyField(serializedObject.FindProperty("sprite"));
    }
}

// Component with visual data
public class MyComponent : MonoBehaviour {
    public Sprite sprite;
    public Color tint = Color.white;

    void OnRenderImage(RenderTexture src, RenderTexture dest) {
        // Post-processing effect
        Graphics.Blit(src, dest, material);
    }
}
```

**Pros:**
✅ Component pattern is powerful and reusable
✅ Data-driven approach minimizes code
✅ Visual tools (Shader Graph) for non-programmers
✅ ScriptableObjects allow asset-based configuration
✅ Inspector validation prevents many errors

**Cons:**
❌ Complex for simple modifications
❌ Performance overhead from component lookups
❌ Shader compilation can be slow
❌ Memory leaks from improper resource cleanup
❌ Difficult to debug visual issues

**Security/Safety:**
- Components run in managed C# (safe)
- Resource limits can be enforced
- Shader validation at compile time
- Asset serialization is validated

**Performance Considerations:**
- Automatic batching for sprites with same material
- Component queries can be optimized
- Shader warmup can cause frame spikes
- Texture atlasing is manual but encouraged

**Relevance to PokeSharp:**
- **Component pattern aligns with ECS**
- Data-driven configuration fits mod system
- Visual tools concept for future tooling
- ScriptableObject equivalent = JSON templates

---

### 1.5 Godot Engine (GDScript)

**Architecture:**
- Node-based scene graph
- CanvasItem for 2D rendering
- Shader language (similar to GLSL)
- Signal system for events

**API Pattern:**
```gdscript
# Godot's approach: Node composition + signals
extends Sprite2D

func _ready():
    # Connect to render signal
    connect("draw", self, "_on_draw")

func _on_draw():
    # Custom draw commands
    draw_circle(Vector2(0, 0), 10, Color.RED)
    draw_texture(my_texture, Vector2(10, 10))

# Custom shader
shader_type canvas_item;
void fragment() {
    COLOR = texture(TEXTURE, UV) * modulate;
    COLOR.a *= sin(TIME * 2.0); // Pulsing effect
}
```

**Pros:**
✅ Simple scene graph for visual organization
✅ Signal system is safe and decoupled
✅ Shader language is sandboxed and validated
✅ draw_* functions are high-level and safe
✅ Automatic batching for nodes with same material

**Cons:**
❌ GDScript performance is slower than compiled code
❌ Custom draw can break batching
❌ Shader debugging is difficult
❌ Signal overhead for many connections
❌ No compile-time safety

**Security/Safety:**
- GDScript is sandboxed VM (safe)
- Shader compiler validates syntax
- draw_* API prevents invalid states
- Memory limits enforced by engine

**Performance Considerations:**
- Automatic batching when possible
- Custom draw breaks batching
- Shader compilation cached
- Node tree overhead for many children

**Relevance to PokeSharp:**
- Signal system = PokeSharp's event bus
- draw_* high-level API is safe for scripts
- Shader sandboxing approach is good
- CSX can be similar to GDScript

---

## 2. API Design Pattern Analysis

### 2.1 Immediate Mode vs Retained Mode

| Pattern | Description | Pros | Cons | Use Cases |
|---------|-------------|------|------|-----------|
| **Immediate Mode** | Mods call draw functions every frame | Simple API, Direct control | No batching, High CPU usage, Easy to misuse | Debug overlays, Temporary effects |
| **Retained Mode** | Mods define visual data, engine renders | Automatic batching, Optimized, Safe | Less flexible, Harder to debug | Sprites, UI, Tile graphics |

**Recommendation:** **Retained mode with event hooks**
- Mods define sprite components (data)
- Engine handles rendering (optimized)
- Events allow immediate mode for special cases

---

### 2.2 Component-Based vs Inheritance-Based

| Pattern | Description | Pros | Cons | Fit for PokeSharp |
|---------|-------------|------|------|-------------------|
| **Component-Based** | Attach visual components to entities | Reusable, Composable, ECS-aligned | More boilerplate | ✅ **PERFECT FIT** |
| **Inheritance-Based** | Subclass sprite/renderer | Familiar OOP, Less code | Rigid, Hard to mix behaviors | ❌ Conflicts with ECS |

**Recommendation:** **Component-based architecture**
- Already using Arch.Core ECS
- Mods add/modify components
- Systems process components automatically

---

### 2.3 Declarative vs Imperative

| Pattern | Description | Example | Safety | Performance |
|---------|-------------|---------|--------|-------------|
| **Declarative** | "What" to render (data) | `{ "sprite": "player", "tint": "red" }` | ✅ High | ✅ High (batched) |
| **Imperative** | "How" to render (code) | `spriteBatch.Draw(texture, pos, color)` | ❌ Low | ❌ Low (manual) |

**Recommendation:** **Declarative with imperative escape hatch**
- 90% of mods use declarative JSON templates
- 10% use event hooks for custom effects
- Best safety/performance balance

---

### 2.4 Event-Driven vs Polling

| Pattern | Description | Overhead | Flexibility | Safety |
|---------|-------------|----------|-------------|--------|
| **Event-Driven** | Hooks fire at render points | Low (only when subscribed) | High | High |
| **Polling** | Mods check state every frame | High (always runs) | Medium | Medium |

**Recommendation:** **Event-driven API**
- PokeSharp already has event bus
- Minimal overhead when unused
- Clear lifecycle hooks

---

## 3. Security and Sandboxing

### 3.1 Common Security Risks

| Risk | Description | Mitigation |
|------|-------------|------------|
| **Infinite Loops** | Freeze game in render hook | Timeout enforcement, Async cancellation |
| **Invalid Textures** | Corrupt GPU state | Validate dimensions, Format checking |
| **Memory Leaks** | Texture not disposed | Automatic disposal, LRU cache |
| **GPU Hangs** | Invalid shader/draw call | Sandbox shaders, Validate parameters |
| **Reflection Abuse** | Access private rendering code | Restrict CSX imports, Whitelist APIs |

### 3.2 Resource Limits

**Recommended Limits:**
```yaml
texture_limits:
  max_dimension: 2048  # Prevent massive textures
  max_file_size: 10MB  # Per texture
  max_total_memory: 50MB  # Per mod
  allowed_formats: [PNG, JPEG]  # Validated formats

draw_limits:
  max_sprites_per_frame: 1000  # Prevent spam
  max_draw_calls: 100  # Per mod per frame
  timeout_ms: 16  # 1 frame budget

shader_limits:
  max_instructions: 1000  # Prevent GPU hangs
  allowed_functions: [sin, cos, mix, texture]  # Whitelist
  compilation_timeout_ms: 5000
```

### 3.3 CSX Scripting Considerations

**PokeSharp uses CSX (C# scripting):**
- Compiled at runtime (not as safe as VM)
- Can access .NET reflection
- No memory limits by default
- Can block render thread

**Required Safeguards:**
1. **Whitelist API**: Only expose safe graphics APIs
2. **Timeout Enforcement**: Kill scripts that take too long
3. **Memory Tracking**: Monitor mod texture usage
4. **Async Rendering**: Render mod content in background
5. **Error Isolation**: One mod's crash doesn't break others

---

## 4. Performance Optimization Strategies

### 4.1 Batching Techniques

**Sprite Batching:**
```csharp
// Good: Batch all mod sprites together
public class ModSpriteRenderer {
    private SpriteBatch _batch;
    private List<ModSpriteComponent> _modSprites;

    public void Render() {
        _batch.Begin(SpriteSortMode.Texture);  // Auto-batch by texture
        foreach (var sprite in _modSprites) {
            _batch.Draw(sprite.Texture, sprite.Position, Color.White);
        }
        _batch.End();
    }
}
```

**Avoid Breaking Batches:**
- Group sprites by texture/material
- Minimize SpriteBatch.Begin/End calls
- Use texture atlases for mod assets

### 4.2 Culling and Visibility

**Frustum Culling:**
```csharp
// Only render sprites in camera view
public bool IsVisible(Position pos, Camera camera) {
    Rectangle cameraBounds = camera.GetBounds();
    return cameraBounds.Contains(new Point(pos.X, pos.Y));
}
```

**Spatial Partitioning:**
- Already have spatial hash in PokeSharp
- Use for mod sprite queries
- Skip culled sprites in render loop

### 4.3 Texture Management

**LRU Cache (Already Implemented):**
```csharp
// PokeSharp already has this in AssetManager
private readonly LruCache<string, Texture2D> _textures = new(
    50_000_000,  // 50MB budget
    texture => texture.Width * texture.Height * 4L
);
```

**Recommendations:**
- Separate cache for mod textures
- Lower priority than core game assets
- Automatic eviction when limit reached

### 4.4 Draw Call Optimization

**Current PokeSharp Pattern:**
```csharp
// ElevationRenderSystem already does this well
_spriteBatch.Begin(
    SpriteSortMode.BackToFront,  // GPU sorts
    BlendState.NonPremultiplied,
    SamplerState.PointClamp,
    DepthStencilState.None,
    RasterizerState.CullNone,
    null,
    cameraTransform
);

// All sprites drawn in single batch
RenderAllTiles(world);
RenderAllSprites(world);

_spriteBatch.End();  // Single flush to GPU
```

**Mod Integration:**
- Add mod rendering phase between tiles and sprites
- Use same SpriteBatch instance
- Let GPU handle depth sorting

---

## 5. Asset Management Best Practices

### 5.1 Asset Loading Strategies

**Lazy Loading (PokeSharp already does this):**
```csharp
private Texture2D? TryGetSpriteTexture(ref Sprite sprite) {
    if (AssetManager.HasTexture(sprite.TextureKey)) {
        return AssetManager.GetTexture(sprite.TextureKey);
    }

    // Lazy load on first use
    TryLazyLoadSprite(sprite.Category, sprite.SpriteName);

    return AssetManager.HasTexture(sprite.TextureKey)
        ? AssetManager.GetTexture(sprite.TextureKey)
        : null;
}
```

**For Mod Assets:**
- Load on first reference (not at mod load)
- Cache with lower priority
- Preload for critical assets

### 5.2 Hot-Reload Support

**Watch for Asset Changes:**
```csharp
public class ModAssetWatcher {
    private FileSystemWatcher _watcher;

    public void WatchModDirectory(string modPath) {
        _watcher = new FileSystemWatcher(modPath, "*.png");
        _watcher.Changed += (s, e) => {
            // Reload texture
            AssetManager.UnloadTexture(GetTextureId(e.FullPath));
            AssetManager.LoadTexture(GetTextureId(e.FullPath), e.FullPath);
        };
        _watcher.EnableRaisingEvents = true;
    }
}
```

**Benefits:**
- Fast iteration for mod developers
- No game restart needed
- Live preview of changes

### 5.3 Asset Naming and Organization

**Recommended Structure:**
```
Mods/
  my-mod/
    mod.json
    Assets/
      Sprites/
        player_custom.png
        enemy_boss.png
      Tilesets/
        grass_custom.png
      Shaders/
        water_effect.fx
    Scripts/
      custom_sprite.csx
```

**Naming Convention:**
```
mod-id:category/asset-name
Examples:
  "my-mod:sprites/player_custom"
  "my-mod:tilesets/grass_custom"
  "my-mod:shaders/water_effect"
```

---

## 6. Recommendations for PokeSharp

### 6.1 Proposed Graphics API Architecture

**Tier 1: Declarative Sprite Components (90% of mods)**
```csharp
// Mod defines sprite through JSON template
{
  "entity_templates": {
    "custom_npc": {
      "components": {
        "Sprite": {
          "category": "my-mod",
          "spriteName": "cool_npc",
          "scale": 1.5,
          "tint": [255, 200, 200, 255]
        },
        "Position": { "x": 10, "y": 20 },
        "Elevation": { "value": 3 }
      }
    }
  }
}
```

**Tier 2: Event-Driven Hooks (9% of mods)**
```csharp
// CSX script hooks into rendering events
public class CustomEffectScript : ScriptBase {
    public override void RegisterEventHandlers(ScriptContext ctx) {
        ctx.EventBus.Subscribe<PreRenderSpriteEvent>(OnPreRenderSprite);
        ctx.EventBus.Subscribe<PostRenderWorldEvent>(OnPostRenderWorld);
    }

    private void OnPreRenderSprite(PreRenderSpriteEvent evt) {
        // Modify sprite data before rendering
        if (evt.Sprite.Category == "player") {
            evt.Sprite.Tint = Color.Red;  // Make player red
        }
    }

    private void OnPostRenderWorld(PostRenderWorldEvent evt) {
        // Draw custom overlay (NOT SpriteBatch access!)
        evt.DrawTexture("my-mod:sprites/overlay", new Vector2(0, 0));
    }
}
```

**Tier 3: Custom Effects (1% of mods)**
```csharp
// Advanced: Register custom shader effect
public class WaterShaderScript : ScriptBase {
    public override void RegisterEventHandlers(ScriptContext ctx) {
        ctx.Graphics.RegisterEffect(new CustomEffectDefinition {
            Name = "my-mod:water-ripple",
            ShaderCode = LoadShaderCode(),
            ApplyToLayer = "water-tiles"
        });
    }
}
```

### 6.2 API Surface

**Safe Graphics API for CSX Scripts:**
```csharp
public interface IModGraphicsApi {
    // Asset management
    void LoadTexture(string modAssetPath);
    bool HasTexture(string textureId);
    Vector2 GetTextureDimensions(string textureId);

    // Sprite creation (adds component to entity)
    void CreateSprite(Entity entity, string textureId, Rectangle sourceRect);
    void UpdateSprite(Entity entity, Action<Sprite> modifier);
    void RemoveSprite(Entity entity);

    // Visual queries
    IEnumerable<Entity> GetVisibleSprites(Rectangle cameraBounds);
    bool IsSpriteVisible(Entity entity);

    // Effects (pre-defined safe effects)
    void ApplyEffect(Entity entity, string effectName, Dictionary<string, object> parameters);
    void RemoveEffect(Entity entity);

    // Event drawing (controlled)
    void DrawTexture(string textureId, Vector2 position, DrawOptions options);
    void DrawRectangle(Rectangle rect, Color color);
    void DrawText(string text, Vector2 position, string font);
}

public class DrawOptions {
    public Color Tint { get; set; } = Color.White;
    public float Rotation { get; set; } = 0f;
    public Vector2 Scale { get; set; } = Vector2.One;
    public Rectangle? SourceRect { get; set; }
    public float Depth { get; set; } = 0.5f;  // 0=front, 1=back
}
```

**NO DIRECT ACCESS TO:**
- SpriteBatch (can break batching)
- GraphicsDevice (can crash GPU)
- Shader compilation (security risk)
- Raw texture data (memory safety)

### 6.3 Implementation Plan

**Phase 1: Declarative Templates (Week 1-2)**
1. Extend entity template system to support mod assets
2. Add `mod-id:` prefix for mod texture paths
3. Validate mod sprite components on load
4. Integrate with existing ElevationRenderSystem

**Phase 2: Rendering Events (Week 3-4)**
5. Add `PreRenderSpriteEvent`, `PostRenderWorldEvent`
6. Create `IModGraphicsApi` interface
7. Implement safe draw methods (no SpriteBatch access)
8. Add timeout enforcement for event handlers

**Phase 3: Asset Management (Week 5-6)**
9. Separate LRU cache for mod textures
10. Hot-reload support for mod assets
11. Asset validation (dimensions, format, size)
12. Memory tracking per mod

**Phase 4: Advanced Features (Week 7-8)**
13. Pre-defined effect system (pulse, glow, shake)
14. Custom shader registration (sandboxed)
15. Animation data from mods
16. Performance profiling tools

### 6.4 Example Mod: Custom NPC Sprite

**mod.json:**
```json
{
  "id": "cool-npcs",
  "name": "Cool NPCs",
  "version": "1.0.0",
  "scripts": ["npc_renderer.csx"],
  "assets": {
    "sprites": ["Assets/Sprites/cool_npc.png"]
  }
}
```

**Assets/Sprites/cool_npc.png:**
- 64x64 sprite sheet
- 4 frames for walking animation

**npc_renderer.csx:**
```csharp
using PokeSharp.Game.Scripting.Runtime;

public class CoolNpcRenderer : ScriptBase {
    public override void RegisterEventHandlers(ScriptContext ctx) {
        // Load mod texture
        ctx.Graphics.LoadTexture("cool-npcs:sprites/cool_npc");

        // React to NPC spawn event
        ctx.EventBus.Subscribe<NpcSpawnedEvent>(OnNpcSpawned);

        // Add glow effect to custom NPCs
        ctx.EventBus.Subscribe<PreRenderSpriteEvent>(OnPreRenderSprite);
    }

    private void OnNpcSpawned(NpcSpawnedEvent evt) {
        if (evt.NpcType == "cool_type") {
            // Add custom sprite component
            ctx.Graphics.CreateSprite(
                evt.Entity,
                "cool-npcs:sprites/cool_npc",
                new Rectangle(0, 0, 16, 16)  // First frame
            );
        }
    }

    private void OnPreRenderSprite(PreRenderSpriteEvent evt) {
        // Add pulse effect to mod sprites
        if (evt.Sprite.Category == "cool-npcs") {
            float pulse = (float)Math.Sin(evt.GameTime.TotalGameTime.TotalSeconds * 2);
            evt.Sprite.Scale = 1.0f + (pulse * 0.1f);  // Gentle pulsing
        }
    }
}

return new CoolNpcRenderer();
```

### 6.5 Performance Targets

| Metric | Target | Max Acceptable |
|--------|--------|----------------|
| Mod sprite draw calls | <100 per frame | <500 |
| Mod texture memory | <20MB per mod | <50MB |
| Event handler time | <1ms per event | <5ms |
| Asset load time | <50ms per texture | <200ms |
| Frame time impact | <2ms with 10 mods | <10ms |

---

## 7. Code Examples: API Patterns

### 7.1 Safe Draw API (No Direct SpriteBatch)

**BAD (Dangerous):**
```csharp
// DON'T: Give mods direct SpriteBatch access
public void OnRender(SpriteBatch spriteBatch) {
    spriteBatch.Begin();  // Can break game's batching!
    spriteBatch.Draw(myTexture, position, Color.White);
    spriteBatch.End();  // Flushes immediately (slow)
}
```

**GOOD (Safe):**
```csharp
// DO: Provide high-level draw API
public void OnRender(IModDrawContext drawContext) {
    drawContext.DrawTexture(
        "my-mod:sprites/overlay",
        position,
        new DrawOptions {
            Tint = Color.White,
            Depth = 0.9f  // Behind UI
        }
    );
}

// Implementation batches all mod draws together
internal class ModDrawContext : IModDrawContext {
    private List<DrawCommand> _pendingDraws = new();

    public void DrawTexture(string textureId, Vector2 position, DrawOptions options) {
        // Queue draw for batching
        _pendingDraws.Add(new DrawCommand {
            TextureId = textureId,
            Position = position,
            Options = options
        });
    }

    internal void FlushBatch(SpriteBatch spriteBatch) {
        // Sort by texture for batching
        var sorted = _pendingDraws.OrderBy(d => d.TextureId);

        spriteBatch.Begin(SpriteSortMode.Texture);
        foreach (var cmd in sorted) {
            var texture = AssetManager.GetTexture(cmd.TextureId);
            spriteBatch.Draw(texture, cmd.Position, cmd.Options.Tint);
        }
        spriteBatch.End();

        _pendingDraws.Clear();
    }
}
```

### 7.2 Component-Based Sprite API

**Mod registers sprite through component:**
```csharp
public class ModSpriteComponent : IComponent {
    public string ModId { get; set; }
    public string TextureId { get; set; }
    public Rectangle SourceRect { get; set; }
    public Color Tint { get; set; } = Color.White;
    public float Scale { get; set; } = 1f;
}

// Mod adds component
public void AddCustomSprite(Entity entity) {
    world.Add(entity, new ModSpriteComponent {
        ModId = "my-mod",
        TextureId = "my-mod:sprites/custom",
        SourceRect = new Rectangle(0, 0, 16, 16)
    });
}

// Rendering system processes components
public class ModSpriteRenderSystem : IRenderSystem {
    private QueryDescription _query = QueryCache.Get<Position, ModSpriteComponent>();

    public void Render(World world) {
        world.Query(in _query, (ref Position pos, ref ModSpriteComponent sprite) => {
            var texture = AssetManager.GetTexture(sprite.TextureId);
            _spriteBatch.Draw(texture, pos.ToVector2(), sprite.SourceRect, sprite.Tint);
        });
    }
}
```

### 7.3 Event-Driven Modification

**Mod modifies existing sprites through events:**
```csharp
public class RainbowPlayerScript : ScriptBase {
    private float _hue = 0f;

    public override void RegisterEventHandlers(ScriptContext ctx) {
        ctx.EventBus.Subscribe<PreRenderSpriteEvent>(OnPreRenderSprite);
        ctx.EventBus.Subscribe<UpdateEvent>(OnUpdate);
    }

    private void OnUpdate(UpdateEvent evt) {
        _hue = (float)(evt.GameTime.TotalGameTime.TotalSeconds % 1.0);
    }

    private void OnPreRenderSprite(PreRenderSpriteEvent evt) {
        // Only affect player sprite
        if (!evt.Entity.Has<PlayerComponent>()) return;

        // Apply rainbow tint
        evt.Sprite.Tint = ColorFromHSV(_hue, 1f, 1f);
    }
}
```

### 7.4 Asset Validation

**Validate mod textures on load:**
```csharp
public class ModTextureValidator {
    private const int MaxDimension = 2048;
    private const long MaxFileSize = 10 * 1024 * 1024;  // 10MB

    public ValidationResult ValidateTexture(string filePath) {
        var fileInfo = new FileInfo(filePath);

        // Check file size
        if (fileInfo.Length > MaxFileSize) {
            return ValidationResult.Error($"Texture too large: {fileInfo.Length} bytes");
        }

        // Check dimensions
        using var stream = File.OpenRead(filePath);
        using var img = Image.Load(stream);

        if (img.Width > MaxDimension || img.Height > MaxDimension) {
            return ValidationResult.Error(
                $"Texture dimensions too large: {img.Width}x{img.Height}"
            );
        }

        // Check format
        if (img.PixelType.BitsPerPixel > 32) {
            return ValidationResult.Warning("Texture should be RGBA32 or less");
        }

        return ValidationResult.Success();
    }
}
```

---

## 8. Comparative Analysis Summary

| Game/Engine | API Style | Safety | Performance | Mod Complexity | Best For |
|-------------|-----------|--------|-------------|----------------|----------|
| Minecraft | Declarative + Registration | Medium | High | Medium | Block-based games |
| Terraria | Imperative + Hooks | Low | Medium | High | Action games |
| Stardew Valley | Event-driven + Assets | High | High | Low | **PokeSharp ✅** |
| Unity | Component-based | High | High | Medium | General purpose |
| Godot | Scene graph + Signals | High | Medium | Low | 2D games |

**Winner for PokeSharp: Hybrid of Stardew Valley + Unity**
- Event-driven hooks (like Stardew)
- Component-based architecture (like Unity)
- Declarative templates (like Minecraft resource packs)
- Asset sandboxing (like all of them)

---

## 9. Risk Analysis

### 9.1 Technical Risks

| Risk | Likelihood | Impact | Mitigation |
|------|------------|--------|------------|
| Mod crashes render thread | High | Critical | Event timeouts, error isolation |
| Memory leaks from textures | Medium | High | Automatic disposal, LRU cache |
| Performance degradation | Medium | High | Draw call limits, profiling tools |
| CSX security exploits | Low | Critical | API whitelist, no reflection |
| Breaking changes to API | Medium | Medium | Versioned API, deprecation warnings |

### 9.2 User Experience Risks

| Risk | Likelihood | Impact | Mitigation |
|------|------------|--------|------------|
| Complex API for beginners | High | Medium | Declarative templates for 90% of mods |
| Difficult to debug visual issues | High | Medium | Dev tools, logging, error messages |
| Mod conflicts (rendering) | Medium | Medium | Render order priorities, event sequencing |
| Asset load times | Medium | Low | Lazy loading, preload critical assets |

---

## 10. Implementation Checklist

### Phase 1: Foundation (Weeks 1-2)
- [ ] Design `IModGraphicsApi` interface
- [ ] Extend entity template system for mod assets
- [ ] Add mod asset path resolution (`mod-id:category/name`)
- [ ] Implement texture validation (dimensions, format, size)
- [ ] Create separate texture cache for mods
- [ ] Add mod sprite component to ECS

### Phase 2: Events (Weeks 3-4)
- [ ] Define rendering events (`PreRenderSprite`, `PostRenderWorld`)
- [ ] Implement `ModDrawContext` for safe drawing
- [ ] Add event timeout enforcement
- [ ] Create error isolation for mod crashes
- [ ] Add performance profiling hooks

### Phase 3: Asset Pipeline (Weeks 5-6)
- [ ] Implement hot-reload for mod assets
- [ ] Add asset preload system
- [ ] Create mod asset browser (debug tool)
- [ ] Implement LRU eviction for mod textures
- [ ] Add memory tracking per mod

### Phase 4: Advanced (Weeks 7-8)
- [ ] Design effect system (pulse, glow, shake)
- [ ] Implement shader sandboxing (if needed)
- [ ] Add animation data support for mods
- [ ] Create mod development guide
- [ ] Build example mods

### Phase 5: Polish (Week 9)
- [ ] Performance optimization
- [ ] API documentation
- [ ] Error message improvements
- [ ] Example mod templates
- [ ] Testing with community mods

---

## 11. Conclusion

Based on comprehensive analysis of 5 moddable games/engines, the **recommended approach for PokeSharp** is:

### Primary Design: Declarative + Event-Driven Hybrid

**Tier 1 (90% of mods): Declarative Templates**
- JSON-based sprite definitions
- No code required for simple visual mods
- Validated and sandboxed automatically

**Tier 2 (9% of mods): Event-Driven Hooks**
- Subscribe to rendering events
- Modify sprite data safely
- No direct SpriteBatch access

**Tier 3 (1% of mods): Advanced Effects**
- Pre-defined shader effects
- Custom animation systems
- Restricted to trusted mods

### Key Benefits:
✅ **Safe**: No direct GPU access, automatic validation
✅ **Performant**: Automatic batching, minimal overhead
✅ **Easy**: Declarative templates for beginners
✅ **Flexible**: Event hooks for advanced users
✅ **ECS-Aligned**: Component-based fits existing architecture

### Next Steps:
1. Review this document with team
2. Prototype `IModGraphicsApi` interface
3. Implement Phase 1 (foundation)
4. Create example mod for testing
5. Gather community feedback

---

**Research completed by:** AI Research Agent
**Architecture reviewed:** PokeSharp ECS + MonoGame rendering
**Key references:** 5 game engines + existing PokeSharp codebase
**Implementation priority:** High (mod system foundation)
