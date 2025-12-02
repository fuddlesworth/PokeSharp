# How Pokeemerald Handles Automatic Tile Animations

## Overview

Pokeemerald uses a **tile replacement system** where animation frames are **directly copied to VRAM** (video memory) every frame. This is different from sprite animations - the actual tile graphics data changes in memory.

## The Core System

### 1. Global Frame Counter

Every frame (60fps), pokeemerald increments a global counter:

```c
static u16 sPrimaryTilesetAnimCounter;      // 0-255, wraps to 0
static u16 sSecondaryTilesetAnimCounter;  // 0-255, wraps to 0
```

This counter is **shared by all animations** in a tileset, ensuring they stay synchronized.

### 2. Update Loop (Every Frame)

```c
void UpdateTilesetAnimations(void)
{
    // Increment counters (wrap at 256)
    if (++sPrimaryTilesetAnimCounter >= 256)
        sPrimaryTilesetAnimCounter = 0;
    if (++sSecondaryTilesetAnimCounter >= 256)
        sSecondaryTilesetAnimCounter = 0;

    // Call tileset-specific animation functions
    if (sPrimaryTilesetAnimCallback)
        sPrimaryTilesetAnimCallback(sPrimaryTilesetAnimCounter);
    if (sSecondaryTilesetAnimCallback)
        sSecondaryTilesetAnimCallback(sSecondaryTilesetAnimCounter);
}
```

### 3. Tileset-Specific Animation Function

Each tileset has a callback that schedules animations using **modulo checks**:

```c
static void TilesetAnim_General(u16 timer)
{
    // Flower: Updates every 16 frames (timer % 16 == 0)
    if (timer % 16 == 0)
        QueueAnimTiles_General_Flower(timer / 16);

    // Water: Updates every 16 frames, offset by 1 (timer % 16 == 1)
    if (timer % 16 == 1)
        QueueAnimTiles_General_Water(timer / 16);

    // Sand/Water Edge: Updates every 16 frames, offset by 2
    if (timer % 16 == 2)
        QueueAnimTiles_General_SandWaterEdge(timer / 16);

    // Waterfall: Updates every 16 frames, offset by 3
    if (timer % 16 == 3)
        QueueAnimTiles_General_Waterfall(timer / 16);

    // Land/Water Edge: Updates every 16 frames, offset by 4
    if (timer % 16 == 4)
        QueueAnimTiles_General_LandWaterEdge(timer / 16);
}
```

**Key Insight:** Animations are **staggered** across frames to spread out VRAM transfers. Only one animation updates per frame.

### 4. Queue Animation Function

Each animation type has a function that queues a DMA transfer. Here's the actual code from `tileset_anims.c`:

```c
static void QueueAnimTiles_General_Water(u16 timer)
{
    // Calculate current frame index (8 frames total, loops)
    u8 i = timer % ARRAY_COUNT(gTilesetAnims_General_Water);

    // Queue DMA transfer: Copy frame data from ROM to VRAM
    AppendTilesetAnimToBuffer(
        gTilesetAnims_General_Water[i],               // Source: ROM (frame graphics)
        (u16 *)(BG_VRAM + TILE_OFFSET_4BPP(432)),    // Dest: VRAM tile 432
        30 * TILE_SIZE_4BPP                           // Size: 30 tiles × 32 bytes = 960 bytes
    );
}

static void QueueAnimTiles_General_Flower(u16 timer)
{
    u16 i = timer % ARRAY_COUNT(gTilesetAnims_General_Flower);
    AppendTilesetAnimToBuffer(
        gTilesetAnims_General_Flower[i],
        (u16 *)(BG_VRAM + TILE_OFFSET_4BPP(508)),     // Dest: VRAM tile 508
        4 * TILE_SIZE_4BPP                            // Size: 4 tiles × 32 bytes = 128 bytes
    );
}
```

**What `AppendTilesetAnimToBuffer` does:**
- Adds a DMA transfer request to a buffer
- The transfer happens during VBlank (safe to modify VRAM)
- Copies frame graphics from ROM to VRAM at the specified tile offset

### 5. DMA Transfer (During VBlank)

The queued transfers are executed during vertical blank:

```c
void TransferTilesetAnimsBuffer(void)
{
    // Perform DMA transfers during VBlank (safe to modify VRAM)
    for (int i = 0; i < sTilesetDMA3TransferBufferSize; i++)
        DmaCopy16(3, buffer[i].src, buffer[i].dest, buffer[i].size);
    sTilesetDMA3TransferBufferSize = 0;
}
```

## How It Works: Step by Step

### Example: Water Animation

1. **Map Placement:**
   - Water metatiles are placed on the map
   - Each metatile references tiles 432-435 (water tiles)

2. **Frame Counter:**
   - Global counter increments: 0, 1, 2, 3, ... 255, 0, 1, ...
   - At frame 1, 17, 33, etc. (timer % 16 == 1), water animation updates

3. **Frame Calculation:**
   - `frameIndex = (timer / 16) % 8`
   - If timer = 17: frameIndex = (17 / 16) % 8 = 1 % 8 = 1
   - If timer = 33: frameIndex = (33 / 16) % 8 = 2 % 8 = 2

4. **VRAM Update:**
   - DMA copies `gTilesetAnims_General_Water[frameIndex]` to VRAM at tile 432
   - This overwrites the graphics data for tiles 432-461 (30 tiles)

5. **Visual Result:**
   - All water tiles on screen instantly show the new frame
   - They're perfectly synchronized because they all reference the same VRAM location

## Animation Parameters

| Animation | Base Tile | Tile Count | Update Interval | Frames | Frame Duration |
|-----------|-----------|------------|-----------------|--------|----------------|
| **Flower** | 508 | 4 tiles | 16 frames | 4 | ~267ms |
| **Water** | 432 | 30 tiles | 16 frames | 8 | ~267ms |
| **Waterfall** | 496 | 6 tiles | 16 frames | 4 | ~267ms |
| **Sand/Water Edge** | 464 | 10 tiles | 16 frames | 8 | ~267ms |
| **Land/Water Edge** | 480 | 10 tiles | 16 frames | 4 | ~267ms |

**Timing:**
- 16 frames @ 60fps = 16/60 = 0.267 seconds per frame
- Water loop: 8 frames × 0.267s = 2.13 seconds total

## Key Concepts

### 1. Tile Replacement, Not Palette Cycling

The **actual pixel data** in VRAM changes, not just colors. Each frame is a complete set of tile graphics.

### 2. Shared VRAM Location

All water tiles reference the **same VRAM location** (tile 432). When VRAM is updated, all water tiles instantly show the new frame. This ensures perfect synchronization.

### 3. Staggered Updates

Animations update on different frames to balance DMA load:
- Frame 0: Flower updates
- Frame 1: Water updates
- Frame 2: Sand/Water Edge updates
- Frame 3: Waterfall updates
- Frame 4: Land/Water Edge updates
- Frames 5-15: Other animations or idle

### 4. Frame Data Storage

Animation frames are stored in ROM as pre-rendered graphics:

```
data/tilesets/primary/general/anim/water/0.4bpp  (Frame 0)
data/tilesets/primary/general/anim/water/1.4bpp  (Frame 1)
data/tilesets/primary/general/anim/water/2.4bpp  (Frame 2)
...
```

Each `.4bpp` file contains multiple 8×8 tiles laid out horizontally.

### 5. Metatile Relationship

A water metatile is defined as:
```
Metatile_Water = {
    TopLeft:  Tile 432, Palette 0,
    TopRight: Tile 433, Palette 0,
    BotLeft:  Tile 434, Palette 0,
    BotRight: Tile 435, Palette 0
};
```

The metatile **always references tile 432**, but the graphics at VRAM offset 432 change each frame. This is why the animation works without changing map data.

## Comparison: Automatic vs Trigger Animations

| Aspect | Automatic (Water, Flowers) | Trigger (TV, Doors) |
|--------|---------------------------|---------------------|
| **Activation** | Time-based (every frame) | Event-based (script) |
| **State** | Always looping | State-driven (on/off) |
| **Synchronization** | Global counter (all sync) | Per-tile state |
| **VRAM Update** | Continuous (every N frames) | On-demand (when triggered) |
| **Example** | Water ripples, flowers swaying | TV turning on, door opening |

## Implementation in PokeSharp

Your `TileAnimationSystem` already implements this correctly:

1. ✅ **Global timer** - `_globalAnimationTimer` shared by all tiles
2. ✅ **Frame calculation** - Uses modulo to determine current frame
3. ✅ **Synchronization** - All tiles with same animation stay in sync
4. ✅ **Source rectangles** - Precalculated for performance

The key difference from pokeemerald is:
- **Pokeemerald:** Updates VRAM directly (DMA copy)
- **PokeSharp:** Updates sprite source rectangles (texture coordinates)

Both achieve the same visual result: synchronized, looping tile animations.

## Summary

Pokeemerald's automatic animations work by:
1. **Global counter** increments every frame (0-255, loops)
2. **Modulo checks** determine when each animation updates
3. **DMA transfers** copy frame data from ROM to VRAM
4. **Shared VRAM locations** ensure all tiles stay synchronized
5. **Staggered updates** spread load across frames

This is why all water tiles on screen animate in perfect sync - they all reference the same VRAM location that gets updated simultaneously!

