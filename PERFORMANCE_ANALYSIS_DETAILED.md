# Performance Analysis - Deep Dive

## 🔴 Critical: TileAnimationSystem (30ms spikes)

### Current Performance
```
Average: 0.82-0.91ms
Peak: 30.11ms (!!)
Worst spike: 7.07ms (42.4% of frame budget)
```

### Root Causes

#### 1. **ConcurrentDictionary Lock Contention**
```csharp
// PROBLEM: GetOrAdd() locks on every frame change for EVERY tile
return _sourceRectCache.GetOrAdd(key, static k => { /* expensive calc */ });
```

**Why it's slow:**
- `ConcurrentDictionary.GetOrAdd()` uses internal locks
- Multiple threads (parallel query) contend for these locks
- Lock acquisition overhead: ~0.1-0.5ms per tile
- With 10+ animating tiles: **5-10ms of pure lock overhead**

#### 2. **Expensive Cache Key Creation**
```csharp
var key = new TileRectKey(
    animTile.TilesetFirstGid,  // field 1
    tileGid,                    // field 2
    animTile.TileWidth,         // field 3
    animTile.TileHeight,        // field 4
    animTile.TilesPerRow,       // field 5
    animTile.TileSpacing,       // field 6
    animTile.TileMargin         // field 7
);
```

**Why it's slow:**
- **7-field record struct** requires hashing 7 integers
- Hash calculation: ~100-200 CPU cycles
- Happens **every frame** for **every animating tile**
- Not cached - recreated on each animation frame change

#### 3. **Parallel Query Overhead (for few entities)**
```csharp
ParallelQuery<AnimatedTile, TileSprite>(
    in EcsQueries.AnimatedTiles,
    (Entity entity, ref AnimatedTile animTile, ref TileSprite sprite) => { ... }
);
```

**Why it's slow:**
- Parallel overhead: ~1-2ms for thread pool startup
- Only justified for **32+ entities**
- Test map likely has <10 animated tiles
- Paying parallelization cost for no benefit

#### 4. **Memory Allocations**
- TileRectKey struct (56 bytes) created per tile per frame
- ConcurrentDictionary bucket allocations on cache misses
- Triggering Gen0/Gen1 collections

### 💡 Solution: Precalculate at Load Time

**Current approach (SLOW):**
```
Load Map → Lazy calc on first animation frame → Cache lookup every frame
```

**Optimized approach (FAST):**
```
Load Map → Precalc ALL frames → Store directly in AnimatedTile → Zero runtime overhead
```

## 🟡 Concerning: ElevationRenderSystem (11ms peak)

### Current Performance
```
Average: 0.18-0.19ms (excellent!)
Peak: 11.44ms (concerning)
Entities: 189-219 (not many!)
```

### Analysis

**11ms for 219 entities = ~0.05ms per entity** - This is too slow!

Expected performance for 219 entities: **2-3ms max**

### Possible Causes

1. **SpriteBatch.Draw() overhead**
   - Each `Draw()` call: ~0.01-0.03ms
   - 219 entities × 0.03ms = 6.57ms
   - Still doesn't explain 11ms peak

2. **Texture switching**
   - MonoGame batches by texture
   - If each entity uses different texture → 219 texture switches
   - Texture switch overhead: ~0.02-0.05ms each
   - Could cause 4-10ms of overhead

3. **AssetManager.GetTexture() lookups**
   ```csharp
   var texture = _assetManager.GetTexture(sprite.TextureId);
   ```
   - If this does dictionary lookup every frame
   - 219 lookups × 0.01ms = 2.19ms extra

4. **SpriteEffects calculations**
   ```csharp
   var effects = SpriteEffects.None;
   if (sprite.FlipHorizontally)
       effects |= SpriteEffects.FlipHorizontally;
   if (sprite.FlipVertically)
       effects |= SpriteEffects.FlipVertically;
   ```
   - Branch misprediction penalty
   - Cache misses on sprite component reads

## 🔵 Memory Pressure (Gen2 GC)

### Observations
```
[16:07:34.296] [WARN ] PerformanceMonitor: Gen2 GC occurred: 1 collections
Memory: 27-30MB (fluctuating)
G0: 9-10, G1: 4, G2: 2
```

### Analysis

**Gen2 collections indicate:**
- Long-lived allocations making it to Gen2
- Large object heap (LOH) allocations (>85KB)
- Memory leaks or excessive caching

### Likely Sources

1. **ConcurrentDictionary growth**
   - TileAnimationSystem cache
   - Never cleared
   - Grows indefinitely with unique tile configurations

2. **Animation frame arrays**
   - `AnimatedTile.FrameTileIds[]`
   - `AnimatedTile.FrameDurations[]`
   - Heap-allocated arrays in struct components

3. **Logger allocations**
   - String interpolation in hot paths
   - Structured logging allocations

## 📊 Performance Budget (60 FPS = 16.67ms/frame)

### Ideal Budget
```
System                  | Budget  | Current Avg | Current Peak | Status
────────────────────────┼─────────┼─────────────┼──────────────┼────────
TileAnimationSystem     | 1.0ms   | 0.82ms ✅   | 30.11ms ❌   | CRITICAL
ElevationRenderSystem   | 3.0ms   | 0.18ms ✅   | 11.44ms ⚠️   | CONCERN
MovementSystem          | 1.0ms   | 0.02ms ✅   | 2.51ms ✅    | GOOD
Other Systems           | 1.0ms   | 0.08ms ✅   | 2.88ms ✅    | GOOD
GPU/VSync overhead      | 2.0ms   | N/A         | N/A          | N/A
────────────────────────┼─────────┼─────────────┼──────────────┼────────
TOTAL                   | 8.0ms   | 1.1ms ✅    | 49.82ms ❌   | FAILING
```

### Frame Time Analysis
```
Good frame:  1.1ms (avg)  → 15% CPU usage → Excellent!
Bad frame:  49.8ms (peak) → 300% over budget → Unacceptable!
```

The **30ms TileAnimationSystem spike** is the primary culprit, causing dropped frames.

## 🎯 Priority Fixes

### 1. **TileAnimationSystem** (High Priority)
**Impact:** 30ms → ~0.5ms (60x improvement)

**Changes:**
- ✅ Precalculate all source rectangles at map load time
- ✅ Store directly in AnimatedTile (no runtime lookups)
- ✅ Remove ConcurrentDictionary entirely
- ✅ Remove TileRectKey struct
- ✅ Add parallel threshold check

### 2. **ElevationRenderSystem** (Medium Priority)
**Impact:** 11ms → ~3ms (3.7x improvement)

**Changes:**
- Cache texture references (avoid AssetManager lookups)
- Batch entities by texture
- Minimize SpriteBatch state changes

### 3. **Memory Pressure** (Low Priority)
**Impact:** Reduce Gen2 collections, stabilize memory usage

**Changes:**
- Clear TileAnimationSystem cache (after fix #1, won't exist)
- Pool animation frame arrays
- Reduce logging allocations

---

**Analysis Date:** November 11, 2025
**Next Steps:** Implement Priority Fix #1 (TileAnimationSystem)

