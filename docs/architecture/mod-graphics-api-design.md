# Mod Graphics API Design

**Document Version:** 1.0
**Date:** 2025-12-02
**Author:** System Architecture Designer
**Status:** Design Proposal

---

## Table of Contents

1. [API Overview and Philosophy](#1-api-overview-and-philosophy)
2. [Core Interfaces](#2-core-interfaces)
3. [ScriptContext Integration](#3-scriptcontext-integration)
4. [Asset Loading System](#4-asset-loading-system)
5. [Rendering Pipeline](#5-rendering-pipeline)
6. [Performance Optimization](#6-performance-optimization)
7. [Code Examples](#7-code-examples)
8. [Migration Path](#8-migration-path)

---

## 1. API Overview and Philosophy

### 1.1 Design Principles

The Mod Graphics API is designed with the following principles:

**Principle** | **Rationale**
--- | ---
**Immediate Mode** | Mods call draw commands each frame for simplicity and flexibility
**Component-Based** | Graphics rendered via ECS components, not service calls, for consistency
**Safe by Default** | Automatic validation, batching, and resource limits prevent mod abuse
**Asset Isolation** | Each mod's assets are sandboxed to prevent conflicts
**Performance First** | Automatic batching, culling, and pooling minimize overhead

### 1.2 API Philosophy

```
┌────────────────────────────────────────────────────────────┐
│  Mod Script (ScriptBase)                                   │
│    ↓ ctx.Graphics.DrawSprite()                            │
├────────────────────────────────────────────────────────────┤
│  GraphicsApiService (Immediate API)                        │
│    ↓ Creates/Updates Components                           │
├────────────────────────────────────────────────────────────┤
│  ECS Components (RenderComponent, SpriteComponent, etc.)   │
│    ↓ Read by Systems                                      │
├────────────────────────────────────────────────────────────┤
│  Render Systems (ElevationRenderSystem + ModRenderSystem)  │
│    ↓ SpriteBatch.Draw()                                   │
├────────────────────────────────────────────────────────────┤
│  MonoGame/XNA SpriteBatch                                  │
└────────────────────────────────────────────────────────────┘
```

**Hybrid Approach:**
- **Immediate API surface**: Mods call `ctx.Graphics.DrawX()` each frame
- **Component storage**: Creates/updates ECS components under the hood
- **System rendering**: Dedicated render systems read components and batch draws

This gives mods the simplicity of immediate mode while leveraging ECS performance.

### 1.3 Coordinate Systems

The API supports three coordinate systems:

**System** | **Units** | **Origin** | **Use Case**
--- | --- | --- | ---
**World Space** | Tiles (float) | Top-left of world | Entity positions, map layout
**Screen Space** | Pixels (int) | Top-left of viewport | HUD, overlays, debug
**UI Space** | Relative (0.0-1.0) | Top-left of viewport | Responsive UI, anchored elements

**Coordinate Conversion:**
```csharp
// World → Screen (handled by camera transform)
screenPos = camera.WorldToScreen(worldPos);

// Screen → World
worldPos = camera.ScreenToWorld(screenPos);

// UI → Screen (relative to viewport)
screenPos = new Vector2(
    viewport.Width * uiPos.X,
    viewport.Height * uiPos.Y
);
```

---

## 2. Core Interfaces

### 2.1 IGraphicsService

The main entry point for all graphics operations.

```csharp
namespace PokeSharp.Game.Scripting.Api;

/// <summary>
/// Graphics API service for mods to create visual effects, animations, and UI.
/// Accessed via ScriptContext.Graphics in mod scripts.
/// </summary>
public interface IGraphicsService
{
    // ===== SPRITE RENDERING =====

    /// <summary>
    /// Draws a static sprite at world coordinates.
    /// </summary>
    /// <param name="textureKey">Asset key (e.g., "my-mod/sprites/custom")</param>
    /// <param name="position">World position in tiles</param>
    /// <param name="options">Optional rendering parameters (color, scale, rotation, etc.)</param>
    /// <returns>Handle to the rendered sprite for later updates/removal</returns>
    SpriteHandle DrawSprite(string textureKey, Vector2 position, SpriteOptions? options = null);

    /// <summary>
    /// Draws a sprite at screen coordinates (HUD/overlay).
    /// </summary>
    SpriteHandle DrawSpriteScreen(string textureKey, Vector2 screenPos, SpriteOptions? options = null);

    /// <summary>
    /// Updates an existing sprite's properties.
    /// </summary>
    void UpdateSprite(SpriteHandle handle, SpriteOptions options);

    /// <summary>
    /// Removes a sprite from rendering.
    /// </summary>
    void RemoveSprite(SpriteHandle handle);

    // ===== ANIMATED SPRITES =====

    /// <summary>
    /// Plays an animation at world coordinates.
    /// </summary>
    /// <param name="animationKey">Animation manifest key (e.g., "my-mod/animations/explosion")</param>
    /// <param name="position">World position in tiles</param>
    /// <param name="options">Animation parameters (loop, speed, etc.)</param>
    AnimationHandle PlayAnimation(string animationKey, Vector2 position, AnimationOptions? options = null);

    /// <summary>
    /// Stops an animation and optionally removes it.
    /// </summary>
    void StopAnimation(AnimationHandle handle, bool remove = true);

    // ===== PARTICLE EFFECTS =====

    /// <summary>
    /// Spawns a particle effect at world coordinates.
    /// </summary>
    /// <param name="effectType">Effect type (e.g., "rain", "sparkle", "smoke")</param>
    /// <param name="position">World position in tiles</param>
    /// <param name="options">Particle system parameters</param>
    ParticleHandle SpawnParticles(string effectType, Vector2 position, ParticleOptions? options = null);

    /// <summary>
    /// Stops a particle effect.
    /// </summary>
    void StopParticles(ParticleHandle handle);

    // ===== TEXT RENDERING =====

    /// <summary>
    /// Draws text at screen coordinates.
    /// </summary>
    TextHandle DrawText(string text, Vector2 screenPos, TextOptions? options = null);

    /// <summary>
    /// Draws floating text at world coordinates (follows entity, fades out).
    /// </summary>
    TextHandle DrawFloatingText(string text, Vector2 worldPos, FloatingTextOptions? options = null);

    /// <summary>
    /// Updates text content and/or styling.
    /// </summary>
    void UpdateText(TextHandle handle, string? newText = null, TextOptions? options = null);

    /// <summary>
    /// Removes text from rendering.
    /// </summary>
    void RemoveText(TextHandle handle);

    // ===== SHAPE RENDERING (DEBUG/EFFECTS) =====

    /// <summary>
    /// Draws a line in world space.
    /// </summary>
    ShapeHandle DrawLine(Vector2 start, Vector2 end, Color color, float thickness = 1f);

    /// <summary>
    /// Draws a rectangle in world space.
    /// </summary>
    ShapeHandle DrawRectangle(Rectangle bounds, Color color, bool filled = false, float thickness = 1f);

    /// <summary>
    /// Draws a circle in world space.
    /// </summary>
    ShapeHandle DrawCircle(Vector2 center, float radius, Color color, bool filled = false, float thickness = 1f);

    /// <summary>
    /// Removes a shape from rendering.
    /// </summary>
    void RemoveShape(ShapeHandle handle);

    // ===== SCREEN EFFECTS =====

    /// <summary>
    /// Flashes the screen with a color (e.g., damage flash, heal flash).
    /// </summary>
    void FlashScreen(Color color, float duration, EasingFunction? easing = null);

    /// <summary>
    /// Shakes the camera (e.g., explosion, earthquake).
    /// </summary>
    void ShakeCamera(float intensity, float duration, EasingFunction? easing = null);

    /// <summary>
    /// Fades the screen to/from a color (e.g., screen transitions).
    /// </summary>
    void FadeScreen(Color color, float duration, bool fadeIn = false, EasingFunction? easing = null);

    // ===== ASSET MANAGEMENT =====

    /// <summary>
    /// Loads a texture from the mod's asset folder.
    /// </summary>
    /// <param name="relativePath">Path relative to mod's assets folder (e.g., "sprites/custom.png")</param>
    /// <returns>Asset key to use in Draw* methods</returns>
    string LoadTexture(string relativePath);

    /// <summary>
    /// Preloads multiple textures for better performance.
    /// </summary>
    void PreloadTextures(params string[] relativePaths);

    /// <summary>
    /// Unloads a texture from memory (hot-reload safe).
    /// </summary>
    void UnloadTexture(string assetKey);

    // ===== LAYERING AND Z-ORDER =====

    /// <summary>
    /// Sets the render layer for subsequent draw calls.
    /// </summary>
    /// <param name="layer">Layer type (Background, World, Effects, UI)</param>
    void SetLayer(RenderLayer layer);

    /// <summary>
    /// Sets explicit z-order within the current layer (0.0 = back, 1.0 = front).
    /// </summary>
    void SetDepth(float depth);

    // ===== CLEANUP =====

    /// <summary>
    /// Removes all graphics created by this mod.
    /// Called automatically on script unload.
    /// </summary>
    void ClearAll();
}
```

### 2.2 Handle Types

Handles are lightweight references to rendered objects:

```csharp
namespace PokeSharp.Game.Scripting.Api.Graphics;

/// <summary>
/// Base handle for all rendered objects.
/// </summary>
public abstract class RenderHandle
{
    public Entity Entity { get; internal set; }
    public string ModId { get; internal set; }
    public bool IsValid => Entity != default;
}

public sealed class SpriteHandle : RenderHandle { }
public sealed class AnimationHandle : RenderHandle { }
public sealed class ParticleHandle : RenderHandle { }
public sealed class TextHandle : RenderHandle { }
public sealed class ShapeHandle : RenderHandle { }
```

### 2.3 Options Structures

Options provide fine-grained control over rendering:

```csharp
/// <summary>
/// Options for sprite rendering.
/// </summary>
public record SpriteOptions
{
    public Color Color { get; init; } = Color.White;
    public Vector2 Scale { get; init; } = Vector2.One;
    public float Rotation { get; init; } = 0f;
    public Vector2 Origin { get; init; } = Vector2.Zero;
    public bool FlipHorizontal { get; init; } = false;
    public bool FlipVertical { get; init; } = false;
    public Rectangle? SourceRect { get; init; } = null; // Crop texture
    public float Depth { get; init; } = 0.5f; // Layer depth (0.0-1.0)
    public RenderLayer Layer { get; init; } = RenderLayer.World;
}

/// <summary>
/// Options for animation playback.
/// </summary>
public record AnimationOptions
{
    public bool Loop { get; init; } = false;
    public float Speed { get; init; } = 1.0f;
    public Color Color { get; init; } = Color.White;
    public Vector2 Scale { get; init; } = Vector2.One;
    public float Rotation { get; init; } = 0f;
    public Action<AnimationHandle>? OnComplete { get; init; } = null;
}

/// <summary>
/// Options for particle systems.
/// </summary>
public record ParticleOptions
{
    public int Count { get; init; } = 20;
    public float Duration { get; init; } = 2.0f;
    public Vector2 VelocityMin { get; init; } = new(-10, -20);
    public Vector2 VelocityMax { get; init; } = new(10, -5);
    public Color StartColor { get; init; } = Color.White;
    public Color EndColor { get; init; } = Color.Transparent;
    public float SizeMin { get; init; } = 2f;
    public float SizeMax { get; init; } = 4f;
    public bool Gravity { get; init; } = true;
}

/// <summary>
/// Options for text rendering.
/// </summary>
public record TextOptions
{
    public string Font { get; init; } = "default";
    public float FontSize { get; init; } = 16f;
    public Color Color { get; init; } = Color.White;
    public Color? OutlineColor { get; init; } = Color.Black;
    public float OutlineThickness { get; init; } = 1f;
    public TextAlignment Alignment { get; init; } = TextAlignment.Left;
    public float Depth { get; init; } = 0.1f;
}

/// <summary>
/// Options for floating text (damage numbers, etc.).
/// </summary>
public record FloatingTextOptions : TextOptions
{
    public Vector2 Velocity { get; init; } = new(0, -20); // Pixels per second
    public float Duration { get; init; } = 1.5f;
    public EasingFunction? FadeEasing { get; init; } = EasingFunction.EaseOut;
}
```

### 2.4 Enumerations

```csharp
/// <summary>
/// Render layers for z-ordering.
/// </summary>
public enum RenderLayer
{
    Background = 0,  // Image layers, backgrounds
    World = 1,       // Tiles, sprites, entities (uses elevation sorting)
    Effects = 2,     // Particles, screen effects
    UI = 3,          // HUD, text, overlays
    Debug = 4        // Debug shapes, profiling
}

/// <summary>
/// Text alignment options.
/// </summary>
public enum TextAlignment
{
    Left,
    Center,
    Right
}

/// <summary>
/// Easing functions for animations.
/// </summary>
public enum EasingFunction
{
    Linear,
    EaseIn,
    EaseOut,
    EaseInOut,
    EaseOutBounce
}
```

---

## 3. ScriptContext Integration

### 3.1 Graphics Property

The `IGraphicsService` is exposed on `ScriptContext`:

```csharp
// In ScriptContext.cs
public sealed class ScriptContext
{
    private readonly IScriptingApiProvider _apis;

    // Existing properties...
    public PlayerApiService Player => _apis.Player;
    public NpcApiService Npc => _apis.Npc;
    public MapApiService Map => _apis.Map;

    /// <summary>
    /// Gets the Graphics API service for rendering sprites, particles, text, and effects.
    /// </summary>
    /// <example>
    /// <code>
    /// ctx.Graphics.DrawSprite("my-mod/explosion", position);
    /// ctx.Graphics.PlayAnimation("my-mod/heal-effect", playerPos);
    /// </code>
    /// </example>
    public GraphicsApiService Graphics => _apis.Graphics;
}
```

### 3.2 API Provider Update

```csharp
// In IScriptingApiProvider.cs
public interface IScriptingApiProvider
{
    PlayerApiService Player { get; }
    NpcApiService Npc { get; }
    MapApiService Map { get; }
    GameStateApiService GameState { get; }
    DialogueApiService Dialogue { get; }
    EffectApiService Effects { get; }
    GraphicsApiService Graphics { get; } // NEW
}
```

### 3.3 Service Registration

```csharp
// In ScriptingServicesExtensions.cs
public static class ScriptingServicesExtensions
{
    public static IServiceCollection AddScriptingServices(this IServiceCollection services)
    {
        // Existing services...
        services.AddSingleton<PlayerApiService>();
        services.AddSingleton<NpcApiService>();

        // NEW: Graphics API
        services.AddSingleton<IGraphicsService, GraphicsApiService>();
        services.AddSingleton<GraphicsApiService>();

        services.AddSingleton<IScriptingApiProvider, ScriptingApiProvider>();
        return services;
    }
}
```

---

## 4. Asset Loading System

### 4.1 Asset Isolation

Each mod has its own asset namespace:

```
Mods/
  MyMod/
    manifest.json
    Assets/
      sprites/
        custom-tile.png
        custom-npc.png
      animations/
        explosion.json
      fonts/
        custom-font.ttf
```

**Asset Key Format:** `{mod-id}/relative/path`

Example: `"my-mod/sprites/custom-tile"` → `Mods/MyMod/Assets/sprites/custom-tile.png`

### 4.2 Asset Manifest

Animation manifests define frame-based animations:

```json
{
  "texture": "sprites/explosion-sheet.png",
  "frameWidth": 32,
  "frameHeight": 32,
  "animations": {
    "explode": {
      "frames": [0, 1, 2, 3, 4, 5],
      "frameDuration": 0.1,
      "loop": false
    }
  }
}
```

### 4.3 AssetManager Integration

The existing `AssetManager` is extended to support mod assets:

```csharp
// In AssetManager.cs (extend existing class)
public class AssetManager
{
    private readonly Dictionary<string, Texture2D> _textures = new();
    private readonly Dictionary<string, SpriteManifest> _animations = new();
    private readonly Dictionary<string, string> _modAssetPaths = new(); // mod-id → asset root

    /// <summary>
    /// Registers a mod's asset folder.
    /// Called by ModLoader during initialization.
    /// </summary>
    public void RegisterModAssets(string modId, string assetRootPath)
    {
        _modAssetPaths[modId] = assetRootPath;
    }

    /// <summary>
    /// Loads a texture from a mod's assets using the asset key format.
    /// </summary>
    public Texture2D LoadModTexture(string assetKey, GraphicsDevice graphicsDevice)
    {
        // Check cache
        if (_textures.TryGetValue(assetKey, out var cached))
            return cached;

        // Parse: "mod-id/relative/path" → "Mods/ModId/Assets/relative/path.png"
        var parts = assetKey.Split('/', 2);
        if (parts.Length != 2)
            throw new ArgumentException($"Invalid asset key: {assetKey}");

        var modId = parts[0];
        var relativePath = parts[1];

        if (!_modAssetPaths.TryGetValue(modId, out var assetRoot))
            throw new InvalidOperationException($"Mod '{modId}' not registered");

        var fullPath = Path.Combine(assetRoot, relativePath + ".png");

        // Load texture
        using var stream = File.OpenRead(fullPath);
        var texture = Texture2D.FromStream(graphicsDevice, stream);

        // Cache and return
        _textures[assetKey] = texture;
        return texture;
    }
}
```

### 4.4 Hot-Reload Support

Asset hot-reload is handled by `ScriptHotReloadService`:

```csharp
// Extend ScriptHotReloadService
public class ScriptHotReloadService
{
    private readonly AssetManager _assetManager;

    public void OnAssetChanged(string filePath)
    {
        // Detect asset type from extension
        var ext = Path.GetExtension(filePath);

        if (ext == ".png" || ext == ".jpg")
        {
            // Reload texture
            var assetKey = GetAssetKeyFromPath(filePath);
            _assetManager.UnloadTexture(assetKey);
            // Will be reloaded on next draw call
        }
        else if (ext == ".json")
        {
            // Reload animation manifest
            // Similar logic...
        }
    }
}
```

---

## 5. Rendering Pipeline

### 5.1 Component-Based Architecture

Graphics API creates ECS components that are read by render systems:

```csharp
// NEW: Components for mod graphics
namespace PokeSharp.Game.Components.Rendering;

/// <summary>
/// Component marking an entity as a mod-created render object.
/// </summary>
public struct ModRenderable
{
    public string ModId;
    public RenderLayer Layer;
    public float Depth; // Custom depth within layer
    public bool IsScreenSpace; // True = screen coords, False = world coords
}

/// <summary>
/// Component for mod-created sprites.
/// </summary>
public struct ModSprite
{
    public string TextureKey;
    public Rectangle? SourceRect;
    public Color Color;
    public Vector2 Scale;
    public float Rotation;
    public Vector2 Origin;
    public bool FlipHorizontal;
    public bool FlipVertical;
}

/// <summary>
/// Component for mod-created text.
/// </summary>
public struct ModText
{
    public string Content;
    public string Font;
    public float FontSize;
    public Color Color;
    public Color? OutlineColor;
    public float OutlineThickness;
    public TextAlignment Alignment;
}

/// <summary>
/// Component for floating text animation.
/// </summary>
public struct FloatingText
{
    public Vector2 Velocity; // Pixels per second
    public float Duration;
    public float Elapsed;
    public EasingFunction FadeEasing;
}

/// <summary>
/// Component for particle emitters.
/// </summary>
public struct ParticleEmitter
{
    public string EffectType;
    public int ParticleCount;
    public float Duration;
    public float Elapsed;
    public Vector2 VelocityMin;
    public Vector2 VelocityMax;
    public Color StartColor;
    public Color EndColor;
    public float SizeMin;
    public float SizeMax;
    public bool Gravity;
}

/// <summary>
/// Component for shape rendering (debug).
/// </summary>
public struct ModShape
{
    public ShapeType Type;
    public Vector2 Start;
    public Vector2 End;
    public float Radius;
    public Rectangle Bounds;
    public Color Color;
    public bool Filled;
    public float Thickness;
}

public enum ShapeType { Line, Rectangle, Circle }
```

### 5.2 GraphicsApiService Implementation

The service translates API calls into ECS components:

```csharp
namespace PokeSharp.Game.Scripting.Api;

public class GraphicsApiService : IGraphicsService
{
    private readonly World _world;
    private readonly AssetManager _assetManager;
    private readonly GraphicsDevice _graphicsDevice;
    private readonly ILogger<GraphicsApiService> _logger;
    private readonly string _currentModId;

    // Track entities created per mod for cleanup
    private readonly Dictionary<string, List<Entity>> _modEntities = new();

    public GraphicsApiService(
        World world,
        AssetManager assetManager,
        GraphicsDevice graphicsDevice,
        ILogger<GraphicsApiService> logger)
    {
        _world = world;
        _assetManager = assetManager;
        _graphicsDevice = graphicsDevice;
        _logger = logger;
    }

    /// <summary>
    /// Sets the current mod context. Called by ScriptService before script execution.
    /// </summary>
    internal void SetModContext(string modId)
    {
        _currentModId = modId;
    }

    public SpriteHandle DrawSprite(string textureKey, Vector2 position, SpriteOptions? options = null)
    {
        options ??= new SpriteOptions();

        // Validate asset exists
        if (!_assetManager.HasTexture(textureKey))
        {
            var loadedKey = LoadTexture(textureKey);
            textureKey = loadedKey;
        }

        // Create entity with components
        var entity = _world.Create(
            new Position { PixelX = position.X * 16, PixelY = position.Y * 16 },
            new Sprite
            {
                TextureKey = textureKey,
                SourceRect = options.SourceRect ?? Rectangle.Empty,
                Tint = options.Color,
                Scale = options.Scale,
                Rotation = options.Rotation,
                Origin = options.Origin,
                FlipHorizontal = options.FlipHorizontal
            },
            new ModRenderable
            {
                ModId = _currentModId,
                Layer = options.Layer,
                Depth = options.Depth,
                IsScreenSpace = false
            },
            new Elevation { Value = CalculateElevationFromLayer(options.Layer) }
        );

        // Track for cleanup
        if (!_modEntities.ContainsKey(_currentModId))
            _modEntities[_currentModId] = new();
        _modEntities[_currentModId].Add(entity);

        _logger.LogDebug("Mod '{ModId}' created sprite: {TextureKey} at {Position}",
            _currentModId, textureKey, position);

        return new SpriteHandle { Entity = entity, ModId = _currentModId };
    }

    public SpriteHandle DrawSpriteScreen(string textureKey, Vector2 screenPos, SpriteOptions? options = null)
    {
        options ??= new SpriteOptions();

        // Similar to DrawSprite but with IsScreenSpace = true
        var entity = _world.Create(
            new Position { PixelX = screenPos.X, PixelY = screenPos.Y },
            new Sprite { /* ... */ },
            new ModRenderable
            {
                ModId = _currentModId,
                Layer = options.Layer,
                Depth = options.Depth,
                IsScreenSpace = true // No camera transform
            }
        );

        TrackEntity(entity);
        return new SpriteHandle { Entity = entity, ModId = _currentModId };
    }

    public void UpdateSprite(SpriteHandle handle, SpriteOptions options)
    {
        if (!handle.IsValid) return;

        // Update components directly
        ref var sprite = ref _world.Get<Sprite>(handle.Entity);
        sprite.Tint = options.Color;
        sprite.Scale = options.Scale;
        sprite.Rotation = options.Rotation;
        // ... update other fields

        ref var renderable = ref _world.Get<ModRenderable>(handle.Entity);
        renderable.Layer = options.Layer;
        renderable.Depth = options.Depth;
    }

    public void RemoveSprite(SpriteHandle handle)
    {
        if (!handle.IsValid) return;

        _world.Destroy(handle.Entity);
        RemoveTrackedEntity(handle.ModId, handle.Entity);
    }

    public string LoadTexture(string relativePath)
    {
        var assetKey = $"{_currentModId}/{relativePath}";
        _assetManager.LoadModTexture(assetKey, _graphicsDevice);
        return assetKey;
    }

    public void ClearAll()
    {
        if (!_modEntities.TryGetValue(_currentModId, out var entities))
            return;

        foreach (var entity in entities)
        {
            if (_world.IsAlive(entity))
                _world.Destroy(entity);
        }

        entities.Clear();
        _logger.LogInformation("Cleared all graphics for mod '{ModId}'", _currentModId);
    }

    // Helper methods...
    private void TrackEntity(Entity entity)
    {
        if (!_modEntities.ContainsKey(_currentModId))
            _modEntities[_currentModId] = new();
        _modEntities[_currentModId].Add(entity);
    }

    private byte CalculateElevationFromLayer(RenderLayer layer)
    {
        return layer switch
        {
            RenderLayer.Background => 0,
            RenderLayer.World => 3,
            RenderLayer.Effects => 9,
            RenderLayer.UI => 15,
            RenderLayer.Debug => 15,
            _ => 3
        };
    }
}
```

### 5.3 ModRenderSystem

A new render system handles mod-created graphics:

```csharp
namespace PokeSharp.Engine.Rendering.Systems;

/// <summary>
/// Render system for mod-created graphics (sprites, text, shapes, particles).
/// Priority: 1050 (after ElevationRenderSystem:1000, before DebugRenderSystem:2000).
/// </summary>
public class ModRenderSystem : SystemBase, IRenderSystem
{
    private readonly SpriteBatch _spriteBatch;
    private readonly AssetManager _assetManager;
    private readonly ILogger<ModRenderSystem> _logger;

    // Queries
    private readonly QueryDescription _modSpritesQuery = QueryCache.Get<
        Position, Sprite, ModRenderable>();
    private readonly QueryDescription _modTextQuery = QueryCache.Get<
        Position, ModText, ModRenderable>();
    private readonly QueryDescription _modShapesQuery = QueryCache.Get<
        Position, ModShape, ModRenderable>();
    private readonly QueryDescription _modParticlesQuery = QueryCache.Get<
        Position, ParticleEmitter, ModRenderable>();

    public ModRenderSystem(
        GraphicsDevice graphicsDevice,
        AssetManager assetManager,
        ILogger<ModRenderSystem> logger)
    {
        _spriteBatch = new SpriteBatch(graphicsDevice);
        _assetManager = assetManager;
        _logger = logger;
    }

    public override int Priority => SystemPriority.ModRender; // 1050
    public int RenderOrder => 2; // After World (1), before UI (3)

    public void Render(World world)
    {
        // Render each layer in order
        RenderLayer(world, RenderLayer.Effects);
        RenderLayer(world, RenderLayer.UI);
        RenderLayer(world, RenderLayer.Debug);
    }

    private void RenderLayer(World world, RenderLayer layer)
    {
        _spriteBatch.Begin(
            SpriteSortMode.BackToFront,
            BlendState.AlphaBlend,
            SamplerState.PointClamp,
            null, null, null,
            layer == RenderLayer.UI ? Matrix.Identity : GetCameraTransform(world)
        );

        // Render sprites
        world.Query(in _modSpritesQuery, (ref Position pos, ref Sprite sprite, ref ModRenderable rend) =>
        {
            if (rend.Layer != layer) return;

            var texture = _assetManager.GetTexture(sprite.TextureKey);
            var sourceRect = sprite.SourceRect.IsEmpty
                ? new Rectangle(0, 0, texture.Width, texture.Height)
                : sprite.SourceRect;

            var position = rend.IsScreenSpace
                ? new Vector2(pos.PixelX, pos.PixelY)
                : new Vector2(pos.PixelX, pos.PixelY);

            _spriteBatch.Draw(
                texture,
                position,
                sourceRect,
                sprite.Tint,
                sprite.Rotation,
                sprite.Origin,
                sprite.Scale,
                sprite.FlipHorizontal ? SpriteEffects.FlipHorizontally : SpriteEffects.None,
                rend.Depth
            );
        });

        // Render text
        world.Query(in _modTextQuery, (ref Position pos, ref ModText text, ref ModRenderable rend) =>
        {
            if (rend.Layer != layer) return;

            var font = _assetManager.GetFont(text.Font);
            var position = new Vector2(pos.PixelX, pos.PixelY);

            // Draw outline if present
            if (text.OutlineColor.HasValue)
            {
                for (int dx = -1; dx <= 1; dx++)
                for (int dy = -1; dy <= 1; dy++)
                {
                    if (dx == 0 && dy == 0) continue;
                    _spriteBatch.DrawString(font, text.Content,
                        position + new Vector2(dx, dy) * text.OutlineThickness,
                        text.OutlineColor.Value, 0f, Vector2.Zero, 1f,
                        SpriteEffects.None, rend.Depth + 0.001f);
                }
            }

            // Draw main text
            _spriteBatch.DrawString(font, text.Content, position,
                text.Color, 0f, Vector2.Zero, 1f,
                SpriteEffects.None, rend.Depth);
        });

        // Render shapes (simplified for brevity)
        // Render particles (simplified for brevity)

        _spriteBatch.End();
    }

    public override void Update(World world, float deltaTime)
    {
        // Update floating text
        UpdateFloatingText(world, deltaTime);

        // Update particle systems
        UpdateParticles(world, deltaTime);
    }

    private void UpdateFloatingText(World world, float deltaTime)
    {
        var query = QueryCache.Get<Position, FloatingText, ModText>();
        world.Query(in query, (Entity entity, ref Position pos, ref FloatingText floating, ref ModText text) =>
        {
            floating.Elapsed += deltaTime;

            // Move text
            pos.PixelY += floating.Velocity.Y * deltaTime;

            // Fade out
            float alpha = 1f - (floating.Elapsed / floating.Duration);
            alpha = ApplyEasing(alpha, floating.FadeEasing);
            text.Color = text.Color * alpha;

            // Remove when done
            if (floating.Elapsed >= floating.Duration)
            {
                _world.Destroy(entity);
            }
        });
    }

    private void UpdateParticles(World world, float deltaTime)
    {
        // Particle update logic (spawn, update, destroy)
        // Simplified for brevity
    }
}
```

### 5.4 System Priorities

```csharp
// In SystemPriority.cs
public static class SystemPriority
{
    // Existing priorities
    public const int Render = 1000;

    // NEW: Mod rendering priorities
    public const int ModRender = 1050;        // Mod graphics (effects, UI, debug)
    public const int ModParticles = 1060;     // Particle systems
    public const int ModScreenEffects = 1070; // Screen effects (flash, shake, fade)
}
```

---

## 6. Performance Optimization

### 6.1 Automatic Batching

**Strategy:** Group draws by texture and layer to minimize state changes.

```csharp
// In ModRenderSystem
private void RenderLayer(World world, RenderLayer layer)
{
    // Group entities by texture
    var batches = new Dictionary<string, List<SpriteRenderData>>();

    world.Query(in _modSpritesQuery, (ref Position pos, ref Sprite sprite, ref ModRenderable rend) =>
    {
        if (rend.Layer != layer) return;

        if (!batches.ContainsKey(sprite.TextureKey))
            batches[sprite.TextureKey] = new();

        batches[sprite.TextureKey].Add(new SpriteRenderData
        {
            Position = new Vector2(pos.PixelX, pos.PixelY),
            SourceRect = sprite.SourceRect,
            Color = sprite.Tint,
            // ... other fields
        });
    });

    // Render each batch
    _spriteBatch.Begin(/* ... */);
    foreach (var (textureKey, sprites) in batches)
    {
        var texture = _assetManager.GetTexture(textureKey);
        foreach (var sprite in sprites)
        {
            _spriteBatch.Draw(texture, sprite.Position, /* ... */);
        }
    }
    _spriteBatch.End();
}
```

**Benefit:** Reduces texture swaps from 100+ to 5-10 per frame.

### 6.2 Culling Off-Screen Elements

**Strategy:** Skip rendering entities outside camera bounds.

```csharp
private bool IsInCameraBounds(Position pos, Rectangle cameraBounds)
{
    int tileX = (int)(pos.PixelX / 16);
    int tileY = (int)(pos.PixelY / 16);

    return tileX >= cameraBounds.Left
        && tileX < cameraBounds.Right
        && tileY >= cameraBounds.Top
        && tileY < cameraBounds.Bottom;
}

// In render loop
world.Query(in _modSpritesQuery, (ref Position pos, ref Sprite sprite, ref ModRenderable rend) =>
{
    if (!IsInCameraBounds(pos, cameraBounds)) return; // Cull

    // Render...
});
```

**Benefit:** Reduces draw calls by 60-80% on large maps.

### 6.3 Resource Pooling

**Strategy:** Reuse entities for particles and temporary effects.

```csharp
public class ParticlePool
{
    private readonly Queue<Entity> _pool = new();
    private readonly World _world;

    public Entity Acquire()
    {
        if (_pool.Count > 0)
        {
            var entity = _pool.Dequeue();
            // Reset components
            return entity;
        }

        return _world.Create(/* default components */);
    }

    public void Release(Entity entity)
    {
        if (_pool.Count < 100) // Max pool size
            _pool.Enqueue(entity);
        else
            _world.Destroy(entity);
    }
}
```

**Benefit:** Eliminates 1000+ allocations/sec for particle-heavy scenes.

### 6.4 Draw Call Limits

**Strategy:** Enforce per-mod limits to prevent abuse.

```csharp
// In GraphicsApiService
private readonly Dictionary<string, int> _modDrawCounts = new();
private const int MaxDrawCallsPerMod = 500;

public SpriteHandle DrawSprite(string textureKey, Vector2 position, SpriteOptions? options = null)
{
    if (!_modDrawCounts.ContainsKey(_currentModId))
        _modDrawCounts[_currentModId] = 0;

    if (_modDrawCounts[_currentModId] >= MaxDrawCallsPerMod)
    {
        _logger.LogWarning("Mod '{ModId}' exceeded draw call limit ({Limit})",
            _currentModId, MaxDrawCallsPerMod);
        return SpriteHandle.Invalid;
    }

    _modDrawCounts[_currentModId]++;

    // Create sprite...
}

// Reset counters each frame
public void ResetFrameCounters()
{
    _modDrawCounts.Clear();
}
```

**Benefit:** Prevents a single mod from causing performance issues.

### 6.5 Performance Metrics

| Metric | Target | Notes |
|--------|--------|-------|
| Draw calls/frame | < 100 | With batching |
| Entities culled | 60-80% | On large maps |
| Texture swaps/frame | < 10 | With batching |
| Memory overhead | < 5 MB per mod | Including textures |
| Frame time impact | < 2ms | For all mod graphics |

---

## 7. Code Examples

### 7.1 Simple Sprite

```csharp
public class CustomTileScript : ScriptBase
{
    public override void RegisterEventHandlers(ScriptContext ctx)
    {
        // Draw a custom sprite at the entity's position every frame
        ctx.On<RenderEvent>(evt =>
        {
            var pos = ctx.Position;
            ctx.Graphics.DrawSprite("my-mod/sprites/custom-tile",
                new Vector2(pos.X, pos.Y));
        });
    }
}
```

### 7.2 Animated Sprite

```csharp
public class HealingStationScript : ScriptBase
{
    private AnimationHandle _healEffect;

    public override void Initialize(ScriptContext ctx)
    {
        base.Initialize(ctx);

        // Play looping animation
        var pos = ctx.Position;
        _healEffect = ctx.Graphics.PlayAnimation(
            "my-mod/animations/heal-glow",
            new Vector2(pos.X, pos.Y),
            new AnimationOptions { Loop = true }
        );
    }

    public override void RegisterEventHandlers(ScriptContext ctx)
    {
        ctx.OnCollisionDetected(evt =>
        {
            if (evt.CollisionType == CollisionType.PlayerNPC)
            {
                // Heal player
                HealPlayer(evt.EntityA);

                // Play one-shot animation
                ctx.Graphics.PlayAnimation(
                    "my-mod/animations/heal-burst",
                    new Vector2(evt.ContactX, evt.ContactY),
                    new AnimationOptions
                    {
                        Loop = false,
                        OnComplete = handle => ctx.Graphics.StopAnimation(handle)
                    }
                );
            }
        });
    }

    public override void OnUnload()
    {
        ctx.Graphics.StopAnimation(_healEffect);
        base.OnUnload();
    }
}
```

### 7.3 Particle Effect

```csharp
public class TallGrassScript : ScriptBase
{
    public override void RegisterEventHandlers(ScriptContext ctx)
    {
        ctx.OnTileSteppedOn(evt =>
        {
            if (evt.TileType == "tall_grass")
            {
                // Spawn grass particles
                ctx.Graphics.SpawnParticles("grass-rustle",
                    new Vector2(evt.TileX, evt.TileY),
                    new ParticleOptions
                    {
                        Count = 10,
                        Duration = 0.5f,
                        VelocityMin = new Vector2(-5, -10),
                        VelocityMax = new Vector2(5, -2),
                        StartColor = new Color(100, 200, 100),
                        EndColor = Color.Transparent
                    });
            }
        });
    }
}
```

### 7.4 Floating Damage Number

```csharp
public class CombatScript : ScriptBase
{
    public override void RegisterEventHandlers(ScriptContext ctx)
    {
        ctx.On<DamageDealtEvent>(evt =>
        {
            var pos = ctx.World.Get<Position>(evt.Target);

            // Show floating damage number
            ctx.Graphics.DrawFloatingText(
                $"-{evt.Damage}",
                new Vector2(pos.X, pos.Y),
                new FloatingTextOptions
                {
                    Color = Color.Red,
                    FontSize = 20f,
                    OutlineColor = Color.Black,
                    Velocity = new Vector2(0, -30),
                    Duration = 1.5f,
                    FadeEasing = EasingFunction.EaseOut
                });
        });
    }
}
```

### 7.5 HUD Overlay

```csharp
public class QuestTrackerScript : ScriptBase
{
    private TextHandle _questText;

    public override void Initialize(ScriptContext ctx)
    {
        base.Initialize(ctx);

        // Draw HUD text at screen coordinates
        _questText = ctx.Graphics.DrawText(
            "Quest: Find the lost item",
            new Vector2(10, 10), // Top-left corner
            new TextOptions
            {
                Color = Color.White,
                FontSize = 14f,
                OutlineColor = Color.Black
            });
    }

    public override void RegisterEventHandlers(ScriptContext ctx)
    {
        ctx.On<QuestUpdatedEvent>(evt =>
        {
            // Update HUD text
            ctx.Graphics.UpdateText(_questText,
                $"Quest: {evt.QuestName} ({evt.Progress}/{evt.Total})");
        });
    }

    public override void OnUnload()
    {
        ctx.Graphics.RemoveText(_questText);
        base.OnUnload();
    }
}
```

### 7.6 Screen Effect

```csharp
public class BattleTransitionScript : ScriptBase
{
    public void StartBattle()
    {
        // Flash screen red
        Context.Graphics.FlashScreen(
            new Color(255, 0, 0, 128),
            duration: 0.2f);

        // Shake camera
        Context.Graphics.ShakeCamera(
            intensity: 5f,
            duration: 0.5f);

        // Fade to black
        Context.Graphics.FadeScreen(
            Color.Black,
            duration: 1.0f,
            fadeIn: false);
    }
}
```

### 7.7 Debug Visualization

```csharp
public class PathfindingDebugScript : ScriptBase
{
    private List<ShapeHandle> _pathLines = new();

    public void VisualizePath(List<Vector2> path)
    {
        // Clear old path
        foreach (var line in _pathLines)
            Context.Graphics.RemoveShape(line);
        _pathLines.Clear();

        // Draw new path
        for (int i = 0; i < path.Count - 1; i++)
        {
            var line = Context.Graphics.DrawLine(
                path[i],
                path[i + 1],
                Color.Yellow,
                thickness: 2f);
            _pathLines.Add(line);
        }

        // Draw start/end circles
        Context.Graphics.DrawCircle(path[0], 0.5f, Color.Green, filled: true);
        Context.Graphics.DrawCircle(path[^1], 0.5f, Color.Red, filled: true);
    }
}
```

### 7.8 Custom Weather Effect

```csharp
public class RainScript : ScriptBase
{
    private readonly List<ParticleHandle> _raindrops = new();

    public override void Initialize(ScriptContext ctx)
    {
        base.Initialize(ctx);

        // Spawn rain across the screen
        for (int i = 0; i < 100; i++)
        {
            var x = Random.Shared.Next(0, 800); // Screen width
            var y = Random.Shared.Next(-100, 0); // Start above screen

            var rain = ctx.Graphics.SpawnParticles("rain-drop",
                new Vector2(x, y),
                new ParticleOptions
                {
                    Count = 1,
                    Duration = 2.0f,
                    VelocityMin = new Vector2(0, 200),
                    VelocityMax = new Vector2(10, 250),
                    StartColor = new Color(150, 150, 255, 128),
                    EndColor = new Color(150, 150, 255, 0),
                    Gravity = true
                });

            _raindrops.Add(rain);
        }
    }

    public override void OnUnload()
    {
        foreach (var rain in _raindrops)
            Context.Graphics.StopParticles(rain);

        base.OnUnload();
    }
}
```

### 7.9 Dynamic UI Panel

```csharp
public class InventoryPanelScript : ScriptBase
{
    private SpriteHandle _panel;
    private TextHandle _title;
    private readonly List<TextHandle> _itemTexts = new();

    public override void Initialize(ScriptContext ctx)
    {
        base.Initialize(ctx);

        // Draw panel background
        _panel = ctx.Graphics.DrawSpriteScreen(
            "my-mod/ui/panel-background",
            new Vector2(100, 100));

        // Draw title
        _title = ctx.Graphics.DrawText(
            "Inventory",
            new Vector2(120, 110),
            new TextOptions
            {
                FontSize = 18f,
                Color = Color.Gold
            });
    }

    public void UpdateInventory(List<string> items)
    {
        // Clear old item texts
        foreach (var text in _itemTexts)
            Context.Graphics.RemoveText(text);
        _itemTexts.Clear();

        // Draw new item texts
        for (int i = 0; i < items.Count; i++)
        {
            var text = Context.Graphics.DrawText(
                $"- {items[i]}",
                new Vector2(120, 140 + i * 20),
                new TextOptions { FontSize = 14f });
            _itemTexts.Add(text);
        }
    }

    public override void OnUnload()
    {
        Context.Graphics.RemoveSprite(_panel);
        Context.Graphics.RemoveText(_title);
        foreach (var text in _itemTexts)
            Context.Graphics.RemoveText(text);

        base.OnUnload();
    }
}
```

### 7.10 Pre-loading Assets

```csharp
public class AssetHeavyModScript : ScriptBase
{
    public override void Initialize(ScriptContext ctx)
    {
        base.Initialize(ctx);

        // Preload all textures at startup to avoid loading spikes
        ctx.Graphics.PreloadTextures(
            "sprites/custom-tile-1",
            "sprites/custom-tile-2",
            "sprites/custom-npc",
            "sprites/effect-explosion",
            "ui/panel-background"
        );

        ctx.Logger.LogInformation("Preloaded 5 textures for smooth gameplay");
    }
}
```

---

## 8. Migration Path

### 8.1 Phase 1: Core Infrastructure (Week 1-2)

**Goal:** Implement basic API surface and component architecture.

**Tasks:**
1. Create `IGraphicsService` interface and core types
2. Implement `GraphicsApiService` with `DrawSprite` and `RemoveSprite`
3. Create `ModRenderable` and `ModSprite` components
4. Implement `ModRenderSystem` with basic sprite rendering
5. Integrate into `ScriptContext` via `IScriptingApiProvider`
6. Write unit tests for API and components

**Deliverables:**
- Mods can draw static sprites at world coordinates
- Sprites are rendered by `ModRenderSystem`
- Proper cleanup on script unload

**Validation:**
```csharp
[Test]
public void ModScript_CanDrawSprite()
{
    var script = new TestScript();
    script.Initialize(ctx);

    var handle = ctx.Graphics.DrawSprite("test-sprite", Vector2.Zero);

    Assert.IsTrue(handle.IsValid);
    Assert.IsTrue(_world.IsAlive(handle.Entity));
}
```

### 8.2 Phase 2: Asset Management (Week 2-3)

**Goal:** Enable mods to load custom assets from their folders.

**Tasks:**
1. Extend `AssetManager` with `RegisterModAssets` and `LoadModTexture`
2. Implement asset key parsing (`mod-id/path` format)
3. Add `LoadTexture` and `PreloadTextures` to `IGraphicsService`
4. Hook into `ModLoader` to register mod asset folders
5. Implement hot-reload for mod assets in `ScriptHotReloadService`
6. Write integration tests for asset loading

**Deliverables:**
- Mods can load textures from their `Assets/` folder
- Asset hot-reload works during development
- Asset isolation prevents mod conflicts

**Validation:**
```csharp
[Test]
public void ModAssets_AreIsolated()
{
    var assetKey1 = ctx.Graphics.LoadTexture("sprites/tile");
    var assetKey2 = ctx.Graphics.LoadTexture("sprites/tile");

    Assert.AreEqual("mod-a/sprites/tile", assetKey1);
    Assert.AreEqual("mod-b/sprites/tile", assetKey2); // Different mod
    Assert.AreNotEqual(assetKey1, assetKey2);
}
```

### 8.3 Phase 3: Animations and Particles (Week 3-4)

**Goal:** Add animated sprites and particle systems.

**Tasks:**
1. Implement `PlayAnimation` with animation manifest support
2. Create `Animation` and `ParticleEmitter` components
3. Add animation update logic to `ModRenderSystem`
4. Implement particle spawning, update, and destruction
5. Add `SpawnParticles` and `StopParticles` to API
6. Create example animation manifests

**Deliverables:**
- Mods can play frame-based animations
- Particle systems work with velocity, color, and gravity
- Animations can trigger callbacks on completion

**Validation:**
```csharp
[Test]
public void Animation_LoopsCorrectly()
{
    var handle = ctx.Graphics.PlayAnimation("test-anim", Vector2.Zero,
        new AnimationOptions { Loop = true });

    // Simulate 10 frames
    for (int i = 0; i < 10; i++)
        _system.Update(_world, 0.1f);

    // Animation should still be playing
    Assert.IsTrue(_world.IsAlive(handle.Entity));
}
```

### 8.4 Phase 4: Text and UI (Week 4-5)

**Goal:** Enable text rendering and HUD overlays.

**Tasks:**
1. Create `ModText` and `FloatingText` components
2. Implement `DrawText`, `DrawFloatingText`, and `UpdateText`
3. Add text rendering to `ModRenderSystem` with outline support
4. Implement floating text animation (movement, fade)
5. Add `DrawSpriteScreen` for screen-space rendering
6. Create example HUD and floating text scripts

**Deliverables:**
- Mods can draw text at screen and world coordinates
- Text supports outlines and alignment
- Floating text animates with velocity and fade

**Validation:**
```csharp
[Test]
public void FloatingText_FadesOut()
{
    var handle = ctx.Graphics.DrawFloatingText("Test", Vector2.Zero,
        new FloatingTextOptions { Duration = 1.0f });

    // Simulate 1 second
    _system.Update(_world, 1.0f);

    // Text should be destroyed
    Assert.IsFalse(_world.IsAlive(handle.Entity));
}
```

### 8.5 Phase 5: Screen Effects and Shapes (Week 5-6)

**Goal:** Add screen effects (flash, shake, fade) and debug shapes.

**Tasks:**
1. Implement `FlashScreen`, `ShakeCamera`, and `FadeScreen`
2. Create screen effect components and systems
3. Add `DrawLine`, `DrawRectangle`, `DrawCircle` for debugging
4. Create `ModShape` component and rendering logic
5. Add easing functions for smooth animations
6. Create example scripts demonstrating screen effects

**Deliverables:**
- Mods can flash, shake, and fade the screen
- Debug shapes work for visualizing hitboxes and paths
- Screen effects use easing for smooth animations

**Validation:**
```csharp
[Test]
public void ScreenFlash_AnimatesCorrectly()
{
    ctx.Graphics.FlashScreen(Color.Red, 0.5f);

    // Flash should fade out over 0.5 seconds
    _system.Update(_world, 0.25f);
    var alpha = GetFlashAlpha();
    Assert.Greater(alpha, 0f);

    _system.Update(_world, 0.25f);
    alpha = GetFlashAlpha();
    Assert.AreEqual(0f, alpha);
}
```

### 8.6 Phase 6: Performance and Polish (Week 6-7)

**Goal:** Optimize performance and add safety features.

**Tasks:**
1. Implement automatic batching by texture
2. Add viewport culling for off-screen entities
3. Create object pools for particles
4. Enforce per-mod draw call limits
5. Add performance metrics and logging
6. Write comprehensive documentation and examples

**Deliverables:**
- Graphics API achieves performance targets (< 2ms/frame)
- Automatic batching reduces draw calls by 90%
- Per-mod limits prevent abuse

**Validation:**
```csharp
[Benchmark]
public void RenderPerformance_1000Sprites()
{
    // Spawn 1000 mod sprites
    for (int i = 0; i < 1000; i++)
        ctx.Graphics.DrawSprite("test", new Vector2(i % 50, i / 50));

    // Measure render time
    var sw = Stopwatch.StartNew();
    _system.Render(_world);
    sw.Stop();

    Assert.Less(sw.ElapsedMilliseconds, 2); // < 2ms target
}
```

### 8.7 Integration Points

**ModLoader Integration:**
```csharp
// In ModLoader.cs
public void LoadMod(string modPath)
{
    var manifest = LoadManifest(modPath);

    // Register mod assets with AssetManager
    var assetPath = Path.Combine(modPath, "Assets");
    _assetManager.RegisterModAssets(manifest.Id, assetPath);

    // Load scripts...
}
```

**ScriptService Integration:**
```csharp
// In ScriptService.cs
public void ExecuteScript(ScriptBase script, ScriptContext ctx)
{
    // Set mod context for graphics API
    var modId = GetModIdForScript(script);
    _graphicsService.SetModContext(modId);

    // Execute script
    script.Initialize(ctx);
    script.RegisterEventHandlers(ctx);
}

public void UnloadScript(ScriptBase script)
{
    // Clear all graphics created by this mod
    var modId = GetModIdForScript(script);
    _graphicsService.SetModContext(modId);
    _graphicsService.ClearAll();

    script.OnUnload();
}
```

---

## Architecture Decisions

### Decision 1: Immediate API with Component Storage

**Options Considered:**
1. Pure immediate mode (draw calls each frame, no ECS)
2. Pure retained mode (create persistent objects, update them)
3. **Hybrid approach** (immediate API, component storage) ✓

**Decision:** Hybrid approach.

**Rationale:**
- Mods get simplicity of immediate mode API
- ECS systems leverage batching and culling
- Easy integration with existing ElevationRenderSystem
- Performance benefits of component iteration

### Decision 2: Component-Based vs Service-Based

**Options Considered:**
1. Service-based (Graphics.Draw() directly renders)
2. **Component-based** (Graphics.Draw() creates components) ✓

**Decision:** Component-based.

**Rationale:**
- Consistent with existing architecture
- Systems can batch and optimize rendering
- Easy to query and modify graphics via ECS
- Supports automatic cleanup on entity destruction

### Decision 3: Asset Isolation

**Options Considered:**
1. Shared asset pool (mods can use each other's assets)
2. **Isolated namespaces** (mods only access their own assets) ✓

**Decision:** Isolated namespaces with `mod-id/path` format.

**Rationale:**
- Prevents mod conflicts (two mods with "tile.png")
- Clear ownership of assets
- Easy to unload mod assets
- Supports hot-reload per mod

### Decision 4: Performance Limits

**Options Considered:**
1. No limits (trust mods)
2. **Hard limits** (500 draws/mod/frame) ✓
3. Soft limits (warnings only)

**Decision:** Hard limits with graceful degradation.

**Rationale:**
- Prevents single mod from ruining performance
- Clear expectations for mod authors
- Logs warnings before hitting limits
- Can be raised for trusted mods

---

## Summary

This design provides a **comprehensive, performant, and safe graphics API** for mods:

✅ **Immediate Mode API** - Simple to use, familiar to game devs
✅ **Component-Based Storage** - Leverages ECS performance
✅ **Asset Isolation** - Each mod has its own namespace
✅ **Automatic Optimization** - Batching, culling, pooling
✅ **Safety Features** - Per-mod limits, validation
✅ **Hot-Reload Support** - Assets reload during development
✅ **10+ Code Examples** - Real-world usage patterns
✅ **6-Phase Migration** - Incremental rollout over 6-7 weeks

**Key Files to Create:**
- `/PokeSharp.Game.Scripting/Api/IGraphicsService.cs`
- `/PokeSharp.Game.Scripting/Api/GraphicsApiService.cs`
- `/PokeSharp.Game.Components/Rendering/ModRenderable.cs`
- `/PokeSharp.Game.Components/Rendering/ModSprite.cs`
- `/PokeSharp.Engine.Rendering/Systems/ModRenderSystem.cs`
- `/PokeSharp.Engine.Rendering/Assets/AssetManager.ModSupport.cs` (extend existing)

**Performance Targets:**
- < 2ms/frame for all mod graphics
- 90% reduction in draw calls via batching
- 60-80% culling rate on large maps
- < 5 MB memory per mod

---

**End of Document**
