# Example Mod Graphics Requirements Analysis

**Date:** 2025-12-02
**Location:** `/Users/ntomsic/Documents/PokeSharp`
**Analysis Scope:** Weather System, Enhanced Ledges, Quest System Mods

---

## Executive Summary

This document analyzes three example mods to extract exact graphics requirements, identify API gaps, and prioritize implementation phases. The analysis reveals **27 distinct visual effect types** across particle systems, animations, UI rendering, and post-processing effects.

**Key Findings:**
- **17 Critical** graphics features required for basic mod functionality
- **8 High Priority** features for enhanced user experience
- **12 Medium Priority** polish features
- **Estimated Total Complexity:** 890 implementation units (1 unit ≈ 1 dev-day)

---

## Section 1: Weather System Mod

**Location:** `/Mods/examples/weather-system/`

### 1.1 Visual Effects Inventory

#### Rain Effects (`rain_effects.csx`)
| Effect | Context | Complexity | Priority |
|--------|---------|------------|----------|
| **Rain Particle System** | Lines 118-134: 0-500 droplets based on intensity | **Complex** | **Critical** |
| **Splash Effects** | Line 121: Droplet ground impact | Medium | High |
| **Puddle Sprites** | Lines 190-210: Dynamic tile overlays | Medium | High |
| **Lighting Adjustment** | Line 131: Brightness reduction (0-30%) | Simple | High |
| **Puddle Evaporation** | Lines 248-271: Gradual fade-out | Medium | Medium |

**Rain Particle Details:**
```csharp
// Lines 126-131
int particleCount = (int)(intensity * 500); // 0-500 particles
// Needs: Velocity control, downward movement (-5 * intensity)
// Needs: Particle lifetime management
```

#### Thunder Effects (`thunder_effects.csx`)
| Effect | Context | Complexity | Priority |
|--------|---------|------------|----------|
| **Lightning Flash Overlay** | Lines 75-99: Full-screen white flash | Medium | **Critical** |
| **Lightning Bolt Sprite** | Lines 84-88: Sky-to-ground bolt rendering | **Complex** | **Critical** |
| **Camera Shake** | Line 89: Intensity-based shake (0-5 units) | Medium | High |
| **Scorch Mark Decals** | Lines 194-209: Persistent ground marks | Medium | Medium |
| **Flash Duration** | Line 92: 0-200ms based on intensity | Simple | High |
| **Fire Effects** | Lines 171-177: 20% chance fire start | **Complex** | Low |

**Lightning Rendering Details:**
```csharp
// Lines 84-89: Critical rendering requirements
// Renderer.DrawLightningBolt(
//     new Vector2(evt.StrikePosition.X, 0),  // Start: top of screen
//     new Vector2(evt.StrikePosition.X, evt.StrikePosition.Y),  // End: ground
//     evt.Intensity  // Thickness/brightness
// );
```

#### Weather Controller (`weather_controller.csx`)
| Effect | Context | Complexity | Priority |
|--------|---------|------------|----------|
| **Sky Darkening** | Implied during rain/thunder | Medium | High |
| **Fog Overlay** | Line 257: Not yet implemented | Medium | Low |

### 1.2 Graphics API Calls Needed

#### Essential Particle System API
```csharp
// Critical for rain/snow effects
ctx.Graphics.Particles.Create(string id, int count);
ctx.Graphics.Particles.SetVelocity(string id, Vector2 velocity);
ctx.Graphics.Particles.SetLifetime(string id, float seconds);
ctx.Graphics.Particles.Destroy(string id);
ctx.Graphics.Particles.SetGravity(string id, float gravity);

// Advanced particle control
ctx.Graphics.Particles.SetSpawnArea(string id, Rectangle area);
ctx.Graphics.Particles.SetColor(string id, Color color);
ctx.Graphics.Particles.SetSize(string id, float minSize, float maxSize);
ctx.Graphics.Particles.SetFadeOut(string id, float fadeTime);
```

#### Lighting & Post-Processing API
```csharp
// Critical for weather atmosphere
ctx.Graphics.Lighting.SetBrightness(float multiplier); // 0.0-2.0
ctx.Graphics.Lighting.SetTint(Color tint);
ctx.Graphics.PostFX.AddFlash(Color color, float intensity, float duration);
ctx.Graphics.PostFX.FadeOut(float duration);
```

#### Sprite & Decal API
```csharp
// High priority for puddles/scorch marks
ctx.Graphics.Sprite.DrawAt(string spriteId, int x, int y, float alpha);
ctx.Graphics.Decal.Add(string decalId, int x, int y, float duration);
ctx.Graphics.Decal.Remove(string decalId, int x, int y);
```

#### Camera Effects API
```csharp
// High priority for impact feedback
ctx.Graphics.Camera.Shake(float intensity, float duration);
ctx.Graphics.Camera.SetPosition(Vector2 position);
```

### 1.3 Asset Requirements

#### Textures/Sprites Needed
| Asset | Size | Format | Count | Priority |
|-------|------|--------|-------|----------|
| Rain droplet particle | 2x4px | PNG | 1-3 variations | **Critical** |
| Lightning bolt segments | 16x16px | PNG | 5-8 segments | **Critical** |
| Puddle overlay | 16x16px | PNG | 3 stages | High |
| Scorch mark | 16x16px | PNG | 2-3 variations | Medium |
| Screen flash overlay | 1x1px white | PNG | 1 | **Critical** |
| Snow flake particle | 4x4px | PNG | 2-3 variations | Medium |

#### Animations Needed
| Animation | Frames | FPS | Priority |
|-----------|--------|-----|----------|
| Splash impact | 4-6 | 12 | High |
| Lightning strike | 3-5 | 24 | **Critical** |
| Puddle formation | 3 | 6 | Medium |
| Scorch fade | 8 | 4 | Low |

### 1.4 Performance Requirements

#### Per-Frame Draw Calls
- **Rain (light):** ~100 particles = 1 instanced draw call
- **Rain (heavy):** ~500 particles = 1 instanced draw call
- **Puddles:** 0-20 sprites = 1 batched call
- **Lightning flash:** 1 fullscreen quad + 1 bolt sprite
- **Total:** ~3-5 draw calls per frame (acceptable)

#### Update Frequency
- **Particle positions:** Every frame (60 FPS)
- **Puddle checks:** Every 5 seconds
- **Weather transitions:** Every 5-10 minutes
- **Lightning strikes:** Random (10% per loop = ~every 100 seconds during rain)

#### Memory Footprint
- **Particle system:** ~50 KB (500 particles × 100 bytes)
- **Puddle state:** ~800 bytes (20 puddles × 40 bytes)
- **Textures:** ~10 KB (8 small sprites)
- **Total:** **~60 KB** (negligible)

### 1.5 Priority Classification

#### Critical (Mod Won't Work Without)
1. ✅ Particle system creation/destruction
2. ✅ Particle velocity/gravity control
3. ✅ Lightning flash overlay
4. ✅ Lightning bolt rendering

#### High (Major Feature Missing)
1. Screen brightness adjustment
2. Puddle sprite rendering
3. Camera shake effect
4. Splash particle effects

#### Medium (Nice to Have)
1. Puddle evaporation fade
2. Scorch mark decals
3. Weather-based sky tinting

#### Low (Polish/Enhancement)
1. Fire system integration
2. Fog overlay rendering

---

## Section 2: Enhanced Ledges Mod

**Location:** `/Mods/examples/enhanced-ledges/`

### 2.1 Visual Effects Inventory

#### Ledge Jump Effects (`visual_effects.csx`)
| Effect | Context | Complexity | Priority |
|--------|---------|------------|----------|
| **Jump Arc Animation** | Lines 91-104: Parabolic curve | **Complex** | **Critical** |
| **Dust Particles (Landing)** | Lines 117-125: 5 particles on impact | Medium | High |
| **Boost Glow Effect** | Lines 149-157: Pulsing outline | Medium | High |
| **Boost Aura** | Lines 159-167: Rotating energy ring | **Complex** | Medium |
| **Camera Shake** | Lines 180-187: Intensity-based shake | Medium | Medium |
| **Boost Status Icon** | Lines 170-177: UI countdown timer | Medium | High |

#### Crumble Effects (`ledge_crumble.csx`)
| Effect | Context | Complexity | Priority |
|--------|---------|------------|----------|
| **Crack Visuals** | Lines 106-112: Progressive damage | **Complex** | **Critical** |
| **Crumble Animation** | Lines 129-135: Multi-stage destruction | **Complex** | **Critical** |
| **Falling Debris** | Lines 139-147: 12 gravity-affected particles | **Complex** | High |
| **Tile State Update** | Lines 155-157: Visual state change | Medium | **Critical** |

### 2.2 Graphics API Calls Needed

#### Animation System API
```csharp
// Critical for jump arcs and crumble sequences
ctx.Graphics.Animation.Play(Entity entity, string animId, float duration);
ctx.Graphics.Animation.InterpolatePath(Entity entity, Vector2[] path, float duration);
ctx.Graphics.Animation.SetEasing(string animId, EasingFunction easing);
ctx.Graphics.Animation.OnComplete(string animId, Action callback);

// Parabolic arc calculation helper
Vector2[] ctx.Graphics.Animation.CalculateParabolicArc(
    Vector2 start,
    Vector2 end,
    float height,
    int segments
);
```

#### Particle System API (Extended)
```csharp
// For dust and debris effects
ctx.Graphics.Particles.SpawnBurst(
    string id,
    Vector2 position,
    int count,
    float velocity,
    float spread
);
ctx.Graphics.Particles.SetRandomVelocity(string id, Vector2 min, Vector2 max);
ctx.Graphics.Particles.SetTexture(string id, string texturePath);
```

#### Sprite Effects API
```csharp
// For cracks, outlines, auras
ctx.Graphics.Sprite.AddOverlay(int x, int y, string overlayId, float alpha);
ctx.Graphics.Sprite.RemoveOverlay(int x, int y, string overlayId);
ctx.Graphics.Sprite.UpdateOverlay(int x, int y, string overlayId, float alpha);

// Entity glow/outline effects
ctx.Graphics.Entity.SetOutline(Entity entity, Color color, float thickness);
ctx.Graphics.Entity.ClearOutline(Entity entity);
ctx.Graphics.Entity.SetGlow(Entity entity, Color color, float intensity, float pulseSpeed);
```

#### UI Icon API
```csharp
// For status icons and timers
ctx.Graphics.UI.ShowStatusIcon(string iconId, Vector2 position, float duration);
ctx.Graphics.UI.UpdateStatusIcon(string iconId, string text);
ctx.Graphics.UI.HideStatusIcon(string iconId);
```

### 2.3 Asset Requirements

#### Textures/Sprites Needed
| Asset | Size | Format | Count | Priority |
|-------|------|--------|-------|----------|
| Dust particle | 4x4px | PNG | 2-3 variations | High |
| Crack overlay stages | 16x16px | PNG | 3 stages (25%, 50%, 75% damage) | **Critical** |
| Debris fragments | 4x4px | PNG | 5-6 variations | High |
| Boost glow outline | 32x32px | PNG | 1 (reusable) | Medium |
| Boost aura ring | 48x48px | PNG | 4 frames | Medium |
| Jump boost icon | 16x16px | PNG | 1 | High |

#### Animations Needed
| Animation | Frames | FPS | Priority |
|-----------|--------|-----|----------|
| Jump arc | Calculated | 60 | **Critical** |
| Crumble sequence | 6-8 | 12 | **Critical** |
| Dust burst | 4 | 10 | High |
| Boost pulse | 8 (loop) | 12 | Medium |
| Aura rotation | 12 (loop) | 24 | Medium |

### 2.4 Performance Requirements

#### Per-Frame Draw Calls
- **Jump arc:** 1 sprite transform per frame
- **Dust particles:** 5 particles = 1 instanced call
- **Debris particles:** 12 particles = 1 instanced call
- **Crack overlay:** 1 sprite per cracked tile
- **Boost effects:** 2 draw calls (outline + aura)
- **Total:** ~5-7 draw calls during active effects

#### Update Frequency
- **Jump animation:** 60 FPS for 0.3 seconds
- **Particle physics:** 60 FPS for 0.5-1.0 seconds
- **Crack state:** Once per jump (state check)
- **Boost timer:** 1 FPS (UI update)

#### Memory Footprint
- **Animation path:** ~200 bytes (10 waypoints × 20 bytes)
- **Particle system:** ~2 KB (17 particles × 120 bytes)
- **Textures:** ~15 KB (10 small sprites)
- **Total:** **~17 KB**

### 2.5 Priority Classification

#### Critical (Mod Won't Work Without)
1. ✅ Parabolic arc animation system
2. ✅ Crack overlay rendering (3 stages)
3. ✅ Crumble animation sequence
4. ✅ Tile state visual updates

#### High (Major Feature Missing)
1. Dust particle burst on landing
2. Debris particle physics
3. Boost status icon display
4. Boost glow outline effect

#### Medium (Nice to Have)
1. Boost aura animation
2. Camera shake on crumble
3. Animation easing functions

#### Low (Polish/Enhancement)
1. Advanced particle randomization
2. Particle fade-out tuning

---

## Section 3: Quest System Mod

**Location:** `/Mods/examples/quest-system/`

### 3.1 Visual Effects Inventory

#### Quest Tracker UI (`quest_tracker_ui.csx`)
| Effect | Context | Complexity | Priority |
|--------|---------|------------|----------|
| **Quest List Display** | Lines 105-136: Active quests overlay | **Complex** | **Critical** |
| **Progress Bar Rendering** | Lines 138-145: ASCII-style bars | Medium | **Critical** |
| **New Quest Badge** | Lines 48-60: "[NEW]" indicator | Simple | High |
| **Quest Notification Popup** | Lines 148-162: Toast messages | Medium | **Critical** |
| **Completion Animation** | Lines 85-96: Quest complete effect | Medium | High |

#### NPC Quest Giver (`npc_quest_giver.csx`)
| Effect | Context | Complexity | Priority |
|--------|---------|------------|----------|
| **Quest Indicator (!)** | Lines 88-108: Exclamation mark sprite | Simple | **Critical** |
| **Indicator Color States** | Gray (available), Gold (complete) | Simple | High |
| **Quest Available Marker** | Lines 93-97: Floating sprite above NPC | Medium | **Critical** |
| **Quest Complete Marker** | Lines 98-102: Gold ! above NPC | Medium | High |

### 3.2 Graphics API Calls Needed

#### UI Rendering API
```csharp
// Critical for quest system UI
ctx.Graphics.UI.BeginPanel(string panelId, Vector2 position, Vector2 size);
ctx.Graphics.UI.DrawText(string text, Vector2 position, Color color, float scale);
ctx.Graphics.UI.DrawProgressBar(Vector2 position, Vector2 size, float progress, Color fillColor);
ctx.Graphics.UI.DrawIcon(string iconId, Vector2 position, float scale);
ctx.Graphics.UI.EndPanel();

// Quest notification system
ctx.Graphics.UI.ShowNotification(string message, float duration, NotificationStyle style);
ctx.Graphics.UI.HideNotification(string notificationId);
```

#### World Marker API
```csharp
// Critical for NPC quest indicators
ctx.Graphics.WorldMarker.Add(
    Entity entity,
    string markerId,
    Vector2 offset,
    Color tint
);
ctx.Graphics.WorldMarker.SetColor(Entity entity, string markerId, Color color);
ctx.Graphics.WorldMarker.Remove(Entity entity, string markerId);
ctx.Graphics.WorldMarker.SetAnimation(Entity entity, string markerId, string animId);
```

#### Text Rendering API
```csharp
// High priority for UI
ctx.Graphics.Text.Measure(string text, float scale);
ctx.Graphics.Text.Draw(string text, Vector2 position, Color color, float scale);
ctx.Graphics.Text.DrawShadowed(string text, Vector2 position, Color textColor, Color shadowColor);
ctx.Graphics.Text.SetFont(string fontId);
```

### 3.3 Asset Requirements

#### Textures/Sprites Needed
| Asset | Size | Format | Count | Priority |
|-------|------|--------|-------|----------|
| Quest marker (!) | 8x16px | PNG | 2 colors (gray/gold) | **Critical** |
| UI panel background | 9-slice 32x32px | PNG | 1 | **Critical** |
| Progress bar frame | 9-slice 16x8px | PNG | 1 | **Critical** |
| Progress bar fill | 1x8px | PNG | 1 | **Critical** |
| Quest icons | 16x16px | PNG | 5 types (catch, battle, fetch, etc.) | High |
| Notification background | 9-slice 24x24px | PNG | 1 | High |
| Checkmark icon | 8x8px | PNG | 1 | High |

#### UI Elements Needed
| Element | Type | Priority |
|---------|------|----------|
| Quest tracker panel | Overlay UI | **Critical** |
| Progress bars | Dynamic fill | **Critical** |
| Notification toasts | Popup UI | **Critical** |
| Quest log screen | Full-screen UI | Medium |
| Quest details panel | Overlay UI | Medium |

### 3.4 Performance Requirements

#### Per-Frame Draw Calls
- **Quest tracker panel:** 1 panel + 5 text draws + 5 progress bars = **~11 calls**
- **Quest markers:** 1 sprite per NPC = **~1-5 calls**
- **Notifications:** 1 panel + 1 text = **~2 calls** (when visible)
- **Total:** **~14-18 draw calls** (acceptable for UI)

#### Update Frequency
- **Quest tracker:** Every frame (60 FPS) when visible
- **Progress updates:** As needed (event-driven)
- **Quest markers:** Every frame (60 FPS)
- **Notification fade:** 60 FPS for 2-3 seconds

#### Memory Footprint
- **Quest data (5 quests):** ~2 KB (400 bytes per quest)
- **UI textures:** ~20 KB (9-slice panels, icons)
- **Text rendering:** ~10 KB (glyph cache)
- **Total:** **~32 KB**

### 3.5 Priority Classification

#### Critical (Mod Won't Work Without)
1. ✅ UI panel rendering system
2. ✅ Text rendering API
3. ✅ Progress bar rendering
4. ✅ Quest marker sprites (!)
5. ✅ Notification popup system

#### High (Major Feature Missing)
1. 9-slice panel scaling
2. World marker positioning above entities
3. Quest icon display
4. Notification animation (slide in/out)
5. New quest badge display

#### Medium (Nice to Have)
1. Quest completion animation
2. Quest log full-screen UI
3. Hoverable quest tooltips
4. Quest marker animations (bob/pulse)

#### Low (Polish/Enhancement)
1. Quest category icons
2. XP/reward preview display
3. Quest difficulty indicators

---

## Section 4: Consolidated Requirements

### 4.1 Unique Graphics Features Summary

**Total Distinct Features: 27**

#### Rendering Systems (9 features)
1. ✅ **Particle System** - Creation, destruction, velocity, gravity, lifetime
2. ✅ **Sprite Rendering** - Static sprites, overlays, tile decals
3. ✅ **Animation System** - Path interpolation, easing, callbacks
4. ✅ **UI Rendering** - Panels, text, progress bars, icons
5. ✅ **Text Rendering** - Multi-font, scaling, shadows
6. ✅ **Lighting System** - Brightness, tinting, dynamic lights
7. ✅ **Post-Processing** - Flash overlays, fade effects
8. ✅ **Camera Effects** - Shake, position control
9. ✅ **World Markers** - Entity-attached sprites

#### Visual Effects (12 features)
10. Rain/snow particle systems
11. Lightning bolt rendering
12. Puddle/decal system
13. Crack overlay stages
14. Dust/debris burst particles
15. Entity glow/outline effects
16. Jump arc animation
17. Crumble sequence animation
18. Screen flash effects
19. Boost aura effects
20. Quest notification popups
21. Progress bar animations

#### UI Components (6 features)
22. Quest tracker overlay panel
23. Progress bars with fill
24. World markers (quest indicators)
25. Status icons with timers
26. Notification toast system
27. 9-slice panel scaling

### 4.2 Minimum Viable API (Phase 1)

**Target: Enable all 3 example mods to function**

#### Core Graphics Context (`ScriptContext.Graphics`)
```csharp
// Particle System (Priority: Critical)
public interface IParticleSystem
{
    void Create(string id, int count, Vector2 position);
    void Destroy(string id);
    void SetVelocity(string id, Vector2 velocity);
    void SetGravity(string id, float gravity);
    void SetLifetime(string id, float seconds);
    void SpawnBurst(string id, Vector2 pos, int count, float velocity, float spread);
}

// Sprite Rendering (Priority: Critical)
public interface ISpriteRenderer
{
    void Draw(string spriteId, Vector2 position, Color tint, float alpha = 1.0f);
    void DrawAt(string spriteId, int tileX, int tileY, float alpha = 1.0f);
    void AddOverlay(int tileX, int tileY, string overlayId, float alpha);
    void RemoveOverlay(int tileX, int tileY, string overlayId);
}

// Animation System (Priority: Critical)
public interface IAnimationSystem
{
    void Play(Entity entity, string animId, float duration, Action? onComplete = null);
    void InterpolatePath(Entity entity, Vector2[] path, float duration);
    Vector2[] CalculateParabolicArc(Vector2 start, Vector2 end, float height, int segments);
}

// UI Rendering (Priority: Critical)
public interface IUIRenderer
{
    void BeginPanel(string id, Vector2 pos, Vector2 size, Color? bgColor = null);
    void EndPanel();
    void DrawText(string text, Vector2 pos, Color color, float scale = 1.0f);
    void DrawProgressBar(Vector2 pos, Vector2 size, float progress, Color fillColor);
    void ShowNotification(string message, float duration);
}

// Lighting & Camera (Priority: High)
public interface ILighting
{
    void SetBrightness(float multiplier); // 0.0-2.0
    void SetTint(Color tint);
}

public interface ICameraEffects
{
    void Shake(float intensity, float duration);
}

// Post-Processing (Priority: High)
public interface IPostFX
{
    void AddFlash(Color color, float intensity, float duration);
    void FadeOut(float duration);
}
```

### 4.3 Phase 1 vs Phase 2 Breakdown

#### Phase 1: Must-Have (Enable All Mods) - 17 Features
**Estimated Effort: 520 implementation units (~17 weeks)**

| Feature | Complexity | Units | Mods Requiring It |
|---------|------------|-------|-------------------|
| Particle system core | High | 80 | Weather, Enhanced Ledges |
| Sprite rendering | Medium | 40 | All 3 mods |
| Animation interpolation | High | 60 | Enhanced Ledges |
| UI panel rendering | Medium | 50 | Quest System |
| Text rendering | Medium | 50 | Quest System |
| Progress bars | Low | 20 | Quest System |
| Lightning flash overlay | Medium | 30 | Weather |
| Puddle overlays | Low | 25 | Weather |
| Crack overlays | Medium | 35 | Enhanced Ledges |
| Quest markers (!) | Low | 15 | Quest System |
| Notifications | Medium | 40 | Quest System |
| Lighting adjustment | Low | 20 | Weather |
| Camera shake | Low | 15 | Weather, Enhanced Ledges |
| World markers | Medium | 30 | Quest System |
| Particle burst spawn | Medium | 25 | Enhanced Ledges |
| Arc calculation | Low | 15 | Enhanced Ledges |
| Overlay management | Low | 20 | Enhanced Ledges |
| **TOTAL** | | **520** | |

#### Phase 2: Nice-to-Have (Polish) - 10 Features
**Estimated Effort: 370 implementation units (~12 weeks)**

| Feature | Complexity | Units | Purpose |
|---------|------------|-------|---------|
| 9-slice panel scaling | Medium | 40 | Better UI scaling |
| Entity glow/outline | Medium | 50 | Boost effects |
| Boost aura animation | High | 60 | Enhanced feedback |
| Advanced particle randomization | Low | 25 | Visual variety |
| Scorch mark decals | Medium | 35 | Environmental detail |
| Fire system integration | High | 80 | Environmental hazards |
| Fog overlay | Medium | 40 | Weather variety |
| Quest log full UI | Medium | 50 | Better UX |
| Animation easing functions | Low | 20 | Smooth animations |
| Particle fade control | Low | 20 | Polish |
| **TOTAL** | | **370** | |

### 4.4 Estimated Implementation Effort

#### Total Complexity Budget
- **Phase 1 (Critical):** 520 units ≈ **17 weeks** (1 developer)
- **Phase 2 (Polish):** 370 units ≈ **12 weeks** (1 developer)
- **Total:** 890 units ≈ **29 weeks** (7 months)

#### Parallel Development Strategy (3 Developers)
- **Developer A:** Particle & Animation Systems (140 units) → **5 weeks**
- **Developer B:** UI & Text Rendering (160 units) → **5 weeks**
- **Developer C:** Sprite & Overlay Systems (120 units) → **4 weeks**
- **Phase 1 Completion:** **~6 weeks** (with coordination overhead)

#### Quick Wins (Prototype in 2 Weeks)
1. Basic sprite rendering (40 units)
2. Simple particle system (50 units)
3. Text rendering (30 units)
4. Quest marker sprites (15 units)
5. **Total:** 135 units ≈ **4-5 days per developer**

---

## Section 5: Gap Analysis

### 5.1 What Exists vs What's Needed

#### Currently Available in Engine
✅ **Confirmed Working:**
- Entity rendering system
- Basic sprite drawing
- Component-based architecture
- Event system for coordination
- Map tile rendering

❌ **Currently Missing (All Required):**
- Particle system framework
- Animation interpolation system
- UI rendering layer
- Text rendering API
- Post-processing effects
- Camera shake support
- World marker system
- Overlay/decal management

### 5.2 Blockers for Example Mods

#### Weather System - **100% Blocked**
**Cannot function without:**
1. ❌ Particle system (rain droplets)
2. ❌ Lightning flash overlay (thunder effects)
3. ❌ Sprite overlays (puddles)
4. ❌ Lighting adjustment (atmosphere)

**Status:** Mod will only log events, no visual effects.

#### Enhanced Ledges - **100% Blocked**
**Cannot function without:**
1. ❌ Animation system (jump arcs)
2. ❌ Sprite overlays (cracks)
3. ❌ Particle system (dust/debris)
4. ❌ Camera shake (impact feedback)

**Status:** Mod will track state, but no visual feedback.

#### Quest System - **75% Blocked**
**Cannot function without:**
1. ❌ UI panel rendering (quest tracker)
2. ❌ Text rendering (quest names/progress)
3. ❌ World markers (NPC indicators)
4. ❌ Progress bars (completion tracking)

**Status:** Quest logic works, but completely invisible to player.

### 5.3 Workarounds Possible

#### Temporary Logging-Only Mode ✅ **Already Implemented**
All three mods already include extensive logging as a workaround:
```csharp
// Weather System (rain_effects.csx, line 123)
LogInfo($"Starting rain particle effects (intensity: {intensity:F2})");
// Instead of: ParticleSystem.Create("rain_droplets", particleCount);

// Enhanced Ledges (visual_effects.csx, line 103)
LogDebug($"Animating jump arc: height={arcHeight}px, duration={JUMP_DURATION}s");
// Instead of: Animation.Play(entity, arcAnimation);

// Quest System (quest_tracker_ui.csx, line 161)
Context.Logger.LogInformation("📜 {Message}", message);
// Instead of: UI.ShowNotification(message);
```

#### Console Visualization (Development Only)
```csharp
// ASCII art quest tracker (quest_tracker_ui.csx, lines 138-145)
private string RenderProgressBar(int progress, int target)
{
    const int barLength = 10;
    float percent = Math.Min(1.0f, (float)progress / target);
    int filled = (int)(percent * barLength);
    return "[" + new string('#', filled) + new string('-', barLength - filled) + "]";
}
// Output: [####------] 40%
```

**Limitation:** No actual in-game rendering, only useful for debugging.

#### External Asset Pipeline (No Workaround)
Cannot be simulated:
- Particle physics (requires render loop)
- Lightning bolt procedural generation
- UI layout and interaction
- Animation interpolation

### 5.4 Implementation Roadmap

#### Milestone 1: Prototype (2 Weeks)
**Goal:** Show something visible in all 3 mods
- [ ] Basic sprite rendering API
- [ ] Simple particle spawning
- [ ] Text rendering (monospace)
- [ ] Quest marker sprites

**Success Criteria:**
- See rain droplets on screen
- See quest "!" above NPCs
- See quest tracker text overlay

#### Milestone 2: Core Systems (6 Weeks)
**Goal:** Enable full functionality for all 3 mods
- [ ] Complete particle system (velocity, gravity, lifetime)
- [ ] Animation interpolation (jump arcs)
- [ ] UI panel system (quest tracker)
- [ ] Progress bars
- [ ] World markers
- [ ] Sprite overlays (cracks, puddles)
- [ ] Lightning flash effects
- [ ] Camera shake
- [ ] Lighting adjustment

**Success Criteria:**
- Weather System: Full rain/thunder effects
- Enhanced Ledges: Jump arcs, crumbling animation
- Quest System: Full UI with tracker and notifications

#### Milestone 3: Polish (4 Weeks)
**Goal:** Production-ready quality
- [ ] 9-slice UI scaling
- [ ] Entity glow/outline effects
- [ ] Advanced particle effects
- [ ] Scorch marks and decals
- [ ] Animation easing
- [ ] Performance optimization

**Success Criteria:**
- Smooth 60 FPS with all effects active
- Professional-looking UI
- Polished visual feedback

#### Milestone 4: Advanced Features (Optional)
**Goal:** Enhanced capabilities
- [ ] Fire system
- [ ] Fog effects
- [ ] Full quest log UI
- [ ] Custom particle textures

---

## Appendices

### Appendix A: API Usage Examples

#### Example 1: Rain Particle System
```csharp
// From weather-system/rain_effects.csx
public void StartRainEffects(float intensity)
{
    int particleCount = (int)(intensity * 500);

    // Create particle system
    Context.Graphics.Particles.Create("rain_droplets", particleCount);

    // Set particle properties
    Context.Graphics.Particles.SetVelocity("rain_droplets", new Vector2(0, -300)); // Fall speed
    Context.Graphics.Particles.SetGravity("rain_droplets", 9.8f);
    Context.Graphics.Particles.SetLifetime("rain_droplets", 2.0f);
    Context.Graphics.Particles.SetSpawnArea("rain_droplets", GetScreenBounds());

    // Adjust lighting
    Context.Graphics.Lighting.SetBrightness(1.0f - intensity * 0.3f);
}
```

#### Example 2: Jump Arc Animation
```csharp
// From enhanced-ledges/visual_effects.csx
public void PlayJumpArcAnimation(Entity entity, int direction, float jumpHeight)
{
    Vector2 startPos = GetEntityPosition(entity);
    Vector2 endPos = GetTargetTile(startPos, direction);
    float arcHeight = 16.0f * jumpHeight;

    // Calculate parabolic path
    Vector2[] path = Context.Graphics.Animation.CalculateParabolicArc(
        startPos, endPos, arcHeight, segments: 20
    );

    // Play animation
    Context.Graphics.Animation.InterpolatePath(entity, path, duration: 0.3f);
}
```

#### Example 3: Quest Tracker UI
```csharp
// From quest-system/quest_tracker_ui.csx
public void RenderQuestTracker()
{
    Vector2 panelPos = new Vector2(10, 50);
    Vector2 panelSize = new Vector2(200, 150);

    // Begin UI panel
    Context.Graphics.UI.BeginPanel("quest_tracker", panelPos, panelSize);

    // Draw header
    Context.Graphics.UI.DrawText("Active Quests", new Vector2(15, 55), Color.Yellow);

    // Draw each quest
    for (int i = 0; i < activeQuests.Count; i++)
    {
        var quest = activeQuests[i];
        Vector2 questPos = new Vector2(15, 75 + i * 20);

        // Quest name
        Context.Graphics.UI.DrawText(quest.Name, questPos, Color.White, scale: 0.8f);

        // Progress bar
        Context.Graphics.UI.DrawProgressBar(
            new Vector2(questPos.X, questPos.Y + 10),
            new Vector2(180, 8),
            (float)quest.Progress / quest.Target,
            Color.Green
        );
    }

    Context.Graphics.UI.EndPanel();
}
```

### Appendix B: Asset Specifications

#### Sprite Atlas Layout (Recommended)
```
graphics/mods/atlas.png (256x256)
├─ [0,0]     Rain droplet (2x4)
├─ [2,0]     Snow flake (4x4)
├─ [6,0]     Dust particle (4x4)
├─ [10,0]    Debris 1-6 (4x4 each)
├─ [0,16]    Puddle stage 1 (16x16)
├─ [16,16]   Puddle stage 2 (16x16)
├─ [32,16]   Puddle stage 3 (16x16)
├─ [0,32]    Crack stage 1 (16x16)
├─ [16,32]   Crack stage 2 (16x16)
├─ [32,32]   Crack stage 3 (16x16)
├─ [0,48]    Quest marker (8x16)
├─ [8,48]    Quest marker gold (8x16)
└─ [0,64]    Lightning segments (64x64)
```

#### Performance Targets
- **Target FPS:** 60 (16.67ms frame time)
- **Graphics Budget:** 8ms per frame
- **Max Draw Calls:** 100 per frame
- **Max Particles:** 1000 simultaneous
- **UI Complexity:** 50 UI elements per frame

---

## Summary & Recommendations

### Critical Path Forward

**Immediate Next Steps:**
1. ✅ Implement basic `IParticleSystem` interface (3-5 days)
2. ✅ Implement basic `ISpriteRenderer` with overlay support (2-3 days)
3. ✅ Implement `IAnimationSystem` with arc calculations (4-5 days)
4. ✅ Implement `IUIRenderer` with text and panels (5-6 days)
5. ✅ Create asset pipeline for sprites (2 days)

**Total:** **~3 weeks for prototype** enabling visible effects in all 3 mods.

### Risk Mitigation
- **Performance:** Use instanced rendering for particles (1 draw call for 500+ particles)
- **Complexity:** Start with simple implementations, optimize later
- **Testing:** Each system should have unit tests + visual test scene

### Success Metrics
- ✅ All 3 example mods display visual effects
- ✅ 60 FPS maintained with all effects active
- ✅ Memory usage < 50 MB for graphics systems
- ✅ Mod API feels natural and intuitive

---

**Document Version:** 1.0
**Last Updated:** 2025-12-02
**Next Review:** After Phase 1 prototype completion
