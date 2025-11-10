# URGENT: ZOrderRenderSystem Fix Required

**Status:** ⚠️ BUILD FAILING - IMMEDIATE ACTION REQUIRED

## Problem

The ZOrderRenderSystem was not migrated to use the new IRenderSystem interface, causing build failures.

## Current Build Errors

```
error CS0535: 'ZOrderRenderSystem' does not implement interface member 'IRenderSystem.Render(World)'
error CS0535: 'ZOrderRenderSystem' does not implement interface member 'IRenderSystem.RenderOrder'
```

## Required Changes

**File:** `PokeSharp.Rendering/Systems/ZOrderRenderSystem.cs`

### Change 1: Class Declaration (Line 24-28)

**CURRENT (WRONG):**
```csharp
public class ZOrderRenderSystem(
    GraphicsDevice graphicsDevice,
    AssetManager assetManager,
    ILogger<ZOrderRenderSystem>? logger = null
) : BaseSystem
```

**CHANGE TO:**
```csharp
public class ZOrderRenderSystem(
    GraphicsDevice graphicsDevice,
    AssetManager assetManager,
    ILogger<ZOrderRenderSystem>? logger = null
) : IRenderSystem
```

### Change 2: Replace Priority Property (Line 106)

**CURRENT (WRONG):**
```csharp
public override int Priority => SystemPriority.Render;
```

**CHANGE TO:**
```csharp
// For IRenderSystem
public int RenderOrder => 1000;  // Render order (higher = render later)

// For ISystem (required by IRenderSystem inheritance)
public int Priority => 1000;
public bool Enabled { get; set; } = true;

// For ISystem (required by IRenderSystem inheritance)
public void Initialize(World world)
{
    // No initialization needed
}
```

### Change 3: Rename Update() to Render() (Line 109)

**CURRENT (WRONG):**
```csharp
public override void Update(World world, float deltaTime)
{
    try
    {
        EnsureInitialized();
        _frameCounter++;

        // Only run detailed profiling when explicitly enabled (adds overhead)
        if (_enableDetailedProfiling)
        {
            UpdateWithProfiling(world);
            return;
        }

        // Fast path - no profiling overhead
        UpdateCameraCache(world);

        _spriteBatch.Begin(
            SpriteSortMode.BackToFront,
            BlendState.AlphaBlend,
            SamplerState.PointClamp,
            transformMatrix: _cachedCameraTransform
        );

        var totalTilesRendered = 0;
        totalTilesRendered += RenderTileLayer(world, TileLayer.Ground);
        totalTilesRendered += RenderTileLayer(world, TileLayer.Object);

        // ... rest of rendering code ...

        _spriteBatch.End();
    }
    catch (Exception ex)
    {
        _logger?.LogError(ex, "Error in ZOrderRenderSystem.Update");
        throw;
    }
}
```

**CHANGE TO:**
```csharp
public void Render(World world)
{
    try
    {
        EnsureInitialized();
        _frameCounter++;

        // Only run detailed profiling when explicitly enabled (adds overhead)
        if (_enableDetailedProfiling)
        {
            RenderWithProfiling(world);
            return;
        }

        // Fast path - no profiling overhead
        UpdateCameraCache(world);

        _spriteBatch.Begin(
            SpriteSortMode.BackToFront,
            BlendState.AlphaBlend,
            SamplerState.PointClamp,
            transformMatrix: _cachedCameraTransform
        );

        var totalTilesRendered = 0;
        totalTilesRendered += RenderTileLayer(world, TileLayer.Ground);
        totalTilesRendered += RenderTileLayer(world, TileLayer.Object);

        // ... rest of rendering code ...

        _spriteBatch.End();
    }
    catch (Exception ex)
    {
        _logger?.LogError(ex, "Error in ZOrderRenderSystem.Render");
        throw;
    }
}
```

### Change 4: Rename UpdateWithProfiling (Line 199)

**CURRENT (WRONG):**
```csharp
private void UpdateWithProfiling(World world)
```

**CHANGE TO:**
```csharp
private void RenderWithProfiling(World world)
```

### Summary of Changes

1. **Line 28:** Change `BaseSystem` to `IRenderSystem`
2. **After line 29:** Add ISystem properties (Enabled, Initialize)
3. **Line 106:** Replace `Priority` with `RenderOrder` (add both for compatibility)
4. **Line 109:** Change method signature `Update(World, float deltaTime)` → `Render(World)`
5. **Line 119:** Change method call `UpdateWithProfiling` → `RenderWithProfiling`
6. **Line 189:** Change error message to say "Render" instead of "Update"
7. **Line 199:** Rename method `UpdateWithProfiling` → `RenderWithProfiling`

## Verification

After making changes, verify:
```bash
dotnet build PokeSharp.sln
# Should build successfully with no errors
```

## Why This Matters

Without this fix:
- ❌ Build fails completely
- ❌ Cannot run the game
- ❌ MonoGame violation still exists
- ❌ All other work is blocked

With this fix:
- ✅ Build succeeds
- ✅ Game runs correctly
- ✅ MonoGame violation is fixed
- ✅ Proper Update/Draw separation achieved

## Estimated Time

**15-30 minutes** - This is a straightforward refactor with no logic changes.
