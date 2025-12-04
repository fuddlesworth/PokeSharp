# PokeSharp Graphics & Rendering Architecture Analysis

**Date**: 2025-12-03  
**Purpose**: Comprehensive analysis of existing graphics systems for mod API design  
**Project**: PokeSharp (Pokemon Emerald reimplementation in C#)

---

## Executive Summary

PokeSharp uses **MonoGame 3.8.4.1 (DesktopGL)** with a custom **ECS-based rendering pipeline** built on the Arch ECS library. The architecture emphasizes performance with elevation-based sprite sorting, lazy asset loading, and memory-constrained caching. No shader/post-processing infrastructure exists currently.

**Key Finding**: The rendering system is well-architected but lacks extensibility points needed for mod support.

---

## 1. Core Rendering Systems

### 1.1 ElevationRenderSystem (Primary Renderer)
**Location**: `/PokeSharp.Engine.Rendering/Systems/ElevationRenderSystem.cs` (1,329 lines)

**Architecture**:
- Uses MonoGame's SpriteBatch with SpriteSortMode.BackToFront
- Implements Pokemon Emerald's elevation model (0-15 levels)
- Depth formula: layerDepth = 1.0 - ((elevation * 16) + (mapId * 0.1) + (y / mapHeight)) / 251.0
- Supports multi-map rendering with world coordinate system
- Implements viewport culling for performance

**Key Features**:
- Elevation-based Z-sorting (primary) + Y-position sorting (secondary)
- Pokemon Emerald-style border rendering (2x2 tiling pattern)
- Lazy sprite loading support via cached delegate pattern
- Reusable Vector2/Rectangle instances to eliminate 400-600 allocations/frame
- Frame-based performance profiling (optional, disabled by default)

**Render Order**:
1. ImageLayers (backgrounds with custom Z-depth)
2. Border tiles (areas outside map bounds)
3. All tiles (with optional LayerOffset for parallax)
4. All sprites (static + moving, unified query)

---

## 2. Visual Components (ECS)

| Component | Description | Key Fields |
|-----------|-------------|------------|
| Sprite | Sprite rendering data | TextureKey, ManifestKey, SourceRect, Origin, Tint, Scale |
| Animation | Animation state tracker | CurrentAnimation, CurrentFrame, FrameTimer, IsPlaying |
| Camera | Camera transform & viewport | Position, Zoom, Rotation, Viewport, FollowTarget |
| Elevation | Z-ordering value (0-15) | Pokemon Emerald elevation levels |
| TileSprite | Tile rendering data | TilesetId, SourceRect, FlipHorizontally |
| ImageLayer | Background/overlay images | TextureId, Position, TintColor, LayerDepth |

---

## 3. Asset Management

### 3.1 AssetManager
**Location**: `/PokeSharp.Engine.Rendering/Assets/AssetManager.cs` (266 lines)

**Architecture**:
- Runtime PNG loading without MonoGame Content Pipeline
- LRU cache with 50MB memory budget
- Automatic texture eviction based on memory pressure
- Fallback texture path resolution for missing assets

### 3.2 SpriteTextureLoader
**Location**: `/PokeSharp.Game/Systems/Rendering/SpriteTextureLoader.cs` (359 lines)

**Architecture**:
- Lazy sprite loading with per-map tracking
- Reference counting for shared sprites
- Persistent sprite management (e.g., UI elements, player sprites)

**Loading Strategies**:
1. **Eager Loading**: LoadAllSpriteTexturesAsync() - loads all sprites at startup
2. **Lazy Loading**: LoadSpriteTexture() - on-demand loading during rendering
3. **Per-Map Loading**: LoadSpritesForMapAsync() - map-specific sprite loading

---

## 4. Graphics Framework & Dependencies

### 4.1 MonoGame (3.8.4.1)
**Package**: MonoGame.Framework.DesktopGL

**Key Classes Used**:
- GraphicsDevice - Low-level graphics API
- SpriteBatch - 2D sprite rendering
- Texture2D - Texture storage & loading
- SamplerState.PointClamp - Pixel-perfect rendering (no filtering)
- BlendState.NonPremultiplied - PNG transparency handling

**Rendering State**:
```csharp
SpriteBatch.Begin(
    SpriteSortMode.BackToFront,      // Z-sorting
    BlendState.NonPremultiplied,     // PNG alpha
    SamplerState.PointClamp,         // No texture filtering
    effect: null                     // No custom shaders
);
```

---

## 5. Performance Characteristics

### 5.1 Rendering Performance
- **Target**: 60 FPS at 1920x1080 with 1000+ entities
- **Optimizations**:
  - Viewport culling (reduces tiles rendered by 80-90%)
  - Unified queries (eliminates duplicate iteration)
  - Cached delegates (60% faster lazy loading)
  - String key caching (192-384 KB/sec allocation reduction)

### 5.2 Memory Profile
- **Texture Cache**: 50MB LRU cache (auto-eviction)
- **Per-Map Sprites**: 75% memory reduction via lazy loading
- **GC Pressure**: 50-60% reduction via cached keys

---

## 6. Gaps for Mod API Support

### 6.1 Critical Missing Infrastructure

**❌ No Render Hooks**:
- No pre-render/post-render hook system
- No per-entity render override callbacks
- No layer-based rendering groups

**❌ No Effects & Shaders**:
- No shader loading/compilation support
- No post-processing pipeline
- No render target management for effects

**❌ No Particles & VFX**:
- No particle system
- No particle emitter components
- No texture atlas for VFX

**❌ No Custom Drawing**:
- No SpriteBatch access for mods
- No primitive drawing API (lines, circles, polygons)
- No depth buffer for 3D effects

### 6.2 Recommended Extensions

**High Priority**:
1. **ModRenderSystem interface** - allows mods to inject custom rendering
2. **RenderContext API** - provides controlled SpriteBatch access
3. **ParticleSystem** - basic 2D particle effects
4. **ShaderManager** - load/apply custom HLSL shaders
5. **RenderTargetPool** - for post-processing effects

---

## 7. API Design Recommendations

### 7.1 Mod Rendering Interface
```csharp
public interface IModRenderSystem
{
    int RenderOrder { get; }
    void Render(World world, RenderContext context);
}

public class RenderContext
{
    public SpriteBatch SpriteBatch { get; }
    public Camera Camera { get; }
    public AssetManager Assets { get; }
    
    public void DrawSprite(Texture2D texture, Vector2 position, ...);
    public void DrawParticle(ParticleEmitter emitter);
    public void ApplyShader(Effect shader);
}
```

---

## 8. File Structure

### 8.1 Rendering Engine (PokeSharp.Engine.Rendering/)
```
Animation/
├── AnimationDefinition.cs     - Animation sequence data
├── AnimationEvent.cs          - Frame-based event triggers
└── AnimationEventTypes.cs     - Event type definitions

Assets/
├── AssetManager.cs            - Texture loading & LRU cache
├── IAssetProvider.cs          - Asset access interface
└── LruCache.cs                - Memory-constrained cache

Components/
├── Camera.cs                  - Camera transform & viewport
└── RectangleF.cs              - Floating-point rectangle

Systems/
├── ElevationRenderSystem.cs   - Main renderer (1,329 lines)
└── CameraFollowSystem.cs      - Camera following logic
```

---

## 9. Conclusion

PokeSharp has a **solid foundation** for 2D sprite rendering with excellent performance characteristics and Pokemon Emerald accuracy. However, it **lacks extensibility infrastructure** needed for robust mod support.

### 9.1 Immediate Needs for Mod API
1. **RenderContext API** - controlled access to rendering pipeline
2. **Shader support** - load/apply custom HLSL shaders
3. **Particle system** - basic VFX infrastructure
4. **Render hooks** - pre/post-render callbacks
5. **Documentation** - mod developer guide for rendering

### 9.2 Next Steps
1. Design ModRenderSystem interface
2. Implement RenderContext with controlled SpriteBatch access
3. Add shader loading/compilation support
4. Create particle system prototype
5. Document rendering extension points
6. Build example mod using new APIs

---

## Key File Paths

**Rendering Systems**:
- /PokeSharp.Engine.Rendering/Systems/ElevationRenderSystem.cs
- /PokeSharp.Engine.Rendering/Systems/CameraFollowSystem.cs
- /PokeSharp.Game/Systems/Rendering/SpriteAnimationSystem.cs

**Asset Management**:
- /PokeSharp.Engine.Rendering/Assets/AssetManager.cs
- /PokeSharp.Game/Systems/Rendering/SpriteTextureLoader.cs

**Components**:
- /PokeSharp.Game.Components/Components/Rendering/Sprite.cs
- /PokeSharp.Game.Components/Components/Rendering/Animation.cs
- /PokeSharp.Engine.Rendering/Components/Camera.cs

**Animation**:
- /PokeSharp.Engine.Rendering/Animation/AnimationDefinition.cs

---

**End of Report**
