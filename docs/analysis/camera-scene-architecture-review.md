# Camera and Scene Architecture Review

**Date:** December 5, 2025  
**Scope:** Camera systems, Scene management, and their integration with ECS

---

## Executive Summary

Your camera and scene systems have a **solid foundation** with good separation of concerns, but there are several **architectural issues** and **code smells** that deviate from industry standards. The main concerns are:

1. **Camera as a mutable struct** (major issue)
2. **Scene-World coupling** (architectural issue)
3. **Mixed responsibilities** in systems
4. **Lack of camera service abstraction**
5. **Viewport mutation from multiple sources**
6. **Scene lifecycle management issues**

---

## 1. Camera System Analysis

### 1.1 CRITICAL ISSUE: Camera as Mutable Struct

**Location:** `MonoBallFramework.Game/Engine/Rendering/Components/Camera.cs`

**The Problem:**
```csharp
public struct Camera  // ⚠️ Mutable struct anti-pattern
{
    public Vector2 Position { get; set; }
    public float Zoom { get; set; }
    public void Update(float deltaTime) { /* mutates state */ }
}
```

**Why This Is Bad:**
- **Value semantics + mutation = bugs**: When you query `ref Camera camera`, you're modifying a copy unless you use `ref` everywhere
- **Hidden copying**: Passing `Camera` by value creates silent copies, losing mutations
- **ECS anti-pattern**: Mutable structs with methods violate data-oriented design
- **Performance issues**: Large structs (yours is ~80+ bytes) cause stack copying overhead
- **Maintenance nightmare**: Easy to forget `ref` keyword and introduce bugs

**Industry Standard:**
```csharp
// Option 1: Immutable struct (pure data)
public readonly struct CameraData
{
    public readonly Vector2 Position { get; init; }
    public readonly float Zoom { get; init; }
    // No methods, no mutation
}

// Option 2: Reference type (if mutation needed)
public class Camera
{
    public Vector2 Position { get; set; }
    public float Zoom { get; set; }
    public void Update(float deltaTime) { }
}
```

**Evidence in Your Code:**
```csharp:44:66:MonoBallFramework.Game/Engine/Rendering/Systems/CameraFollowSystem.cs
public override void Update(World world, float deltaTime)
{
    if (!Enabled)
    {
        return;
    }

    EnsureInitialized();

    // Process each camera-equipped player
    world.Query(
        in _playerQuery,
        (ref Position position, ref Camera camera) =>  // ⚠️ Must use ref everywhere!
        {
            // Set follow target with offset to center on player sprite
            // Player position is tile top-left, so add half tile (8 pixels) for centering
            const float halfTile = 8f;
            camera.FollowTarget = new Vector2(
                position.PixelX + halfTile,
                position.PixelY + halfTile
            );
            camera.Update(deltaTime);  // Mutating a struct component
        }
    );
}
```

**Recommendation:** 
1. **Short-term fix:** Document that `Camera` must ALWAYS be used with `ref`
2. **Long-term fix:** Refactor to either:
   - Pure data struct + `CameraSystem` that handles all logic
   - Reference type (class) if you need methods

---

### 1.2 ISSUE: Camera Component Has Business Logic

**Problem:**
```csharp:299:347:MonoBallFramework.Game/Engine/Rendering/Components/Camera.cs
public void Update(float deltaTime)
{
    bool dirty = false;

    // 1. Smooth zoom transition
    if (Math.Abs(Zoom - TargetZoom) > 0.001f)
    {
        Zoom = MathHelper.Lerp(Zoom, TargetZoom, ZoomTransitionSpeed);
        dirty = true;
        // ... more logic
    }

    // 2. Follow target if set
    if (FollowTarget.HasValue)
    {
        Vector2 oldPosition = Position;
        Vector2 targetPosition = FollowTarget.Value;

        // Apply smoothing if enabled
        if (SmoothingSpeed > 0)
        {
            Position = Vector2.Lerp(Position, targetPosition, SmoothingSpeed);
        }
        // ... more logic
    }
}
```

**Why This Is Bad:**
- **ECS violation**: Components should be **pure data**, systems should contain **logic**
- **Testability**: Can't easily test camera logic without component instances
- **Reusability**: Logic is locked inside the component
- **Data-Oriented Design violation**: Logic interleaved with data hurts cache performance

**Industry Standard (ECS):**
```csharp
// Component: Pure data only
public struct CameraData
{
    public Vector2 Position;
    public float Zoom;
    public float TargetZoom;
    public Vector2? FollowTarget;
}

// System: All logic lives here
public class CameraUpdateSystem : IUpdateSystem
{
    public void Update(World world, float deltaTime)
    {
        world.Query((ref CameraData camera) =>
        {
            // Zoom logic
            if (Math.Abs(camera.Zoom - camera.TargetZoom) > 0.001f)
            {
                camera.Zoom = MathHelper.Lerp(camera.Zoom, camera.TargetZoom, 0.1f);
            }
            
            // Follow logic
            if (camera.FollowTarget.HasValue)
            {
                camera.Position = Vector2.Lerp(camera.Position, camera.FollowTarget.Value, 0.2f);
            }
        });
    }
}
```

**Recommendation:** Extract all camera logic into dedicated systems

---

### 1.3 ISSUE: Tight Coupling to Player Component

**Problem:**
```csharp:69:69:MonoBallFramework.Game/Engine/Rendering/Systems/ElevationRenderSystem.cs
private readonly QueryDescription _cameraQuery = QueryCache.Get<Player, Camera>();
```

**Why This Is Bad:**
- **Inflexible**: Can't have cameras without players (security cameras, cutscenes, replays)
- **Single Responsibility violation**: Camera depends on player concept
- **Hard to test**: Must create player entities to test camera

**Industry Standard:**
```csharp
// Cameras are independent entities
var cameraQuery = QueryCache.Get<Camera, Active>(); // Any active camera

// Or use a specific marker for the main camera
public struct MainCamera { } // Tag component
var mainCameraQuery = QueryCache.Get<Camera, MainCamera>();
```

**Recommendation:** Decouple camera from player; use tag components for main/active cameras

---

### 1.4 ISSUE: No Camera Service/Manager

**Problem:**
Multiple systems directly manipulate the camera:
- `CameraFollowSystem` - sets `FollowTarget` and calls `Update()`
- `CameraViewportSystem` - handles resize
- `ElevationRenderSystem` - reads and caches transform
- Systems directly query and modify camera state

**Why This Is Bad:**
- **Scattered responsibility**: No single source of truth
- **Race conditions**: Multiple systems touching same data
- **Hard to debug**: Camera state changes from many places
- **No validation**: No central place to validate camera constraints

**Industry Standard:**
```csharp
public interface ICameraService
{
    void SetFollowTarget(Vector2 target);
    void SetZoom(float zoom);
    Matrix GetViewMatrix();
    Vector2 ScreenToWorld(Vector2 screenPos);
    Vector2 WorldToScreen(Vector2 worldPos);
}

public class CameraService : ICameraService
{
    private World _world;
    private QueryDescription _cameraQuery;
    
    // Centralized camera access and validation
    public void SetFollowTarget(Vector2 target)
    {
        _world.Query(_cameraQuery, (ref Camera camera) =>
        {
            ValidateTarget(target); // Validation in one place
            camera.FollowTarget = target;
        });
    }
}
```

**Recommendation:** Create a `CameraService` to centralize camera operations

---

### 1.5 ISSUE: Transform Caching Logic in Render System

**Problem:**
```csharp:510:555:MonoBallFramework.Game/Engine/Rendering/Systems/ElevationRenderSystem.cs
private void UpdateCameraCache(World world)
{
    world.Query(
        in _cameraQuery,
        (ref Camera camera) =>
        {
            // Only recalculate if camera changed (dirty flag optimization)
            if (!camera.IsDirty && _cachedCameraTransform != Matrix.Identity)
            {
                return;
            }

            _cachedCameraTransform = camera.GetTransformMatrix();

            // Calculate camera bounds for culling
            int left =
                (int)(camera.Position.X / TileSize)
                - (camera.Viewport.Width / 2 / TileSize / (int)camera.Zoom)
                - CameraViewportMarginTiles;
            // ... more calculation logic
            
            // Reset dirty flag after recalculation
            camera.IsDirty = false;  // ⚠️ Render system modifying camera state!
        }
    );
}
```

**Why This Is Bad:**
- **Separation of Concerns violation**: Render system shouldn't modify camera state
- **Hidden side effects**: Rendering now has side effects on game state
- **Order dependency**: Must render before other systems can see dirty flag
- **Testing difficulty**: Can't test rendering without affecting state

**Industry Standard:**
```csharp
// Camera system manages dirty flags
public class CameraUpdateSystem
{
    public void Update(World world, float deltaTime)
    {
        world.Query((ref Camera camera) =>
        {
            camera.Update(deltaTime);
            camera.IsDirty = false; // Clear after updating
        });
    }
}

// Render system just reads
public class RenderSystem
{
    private Matrix _cachedTransform = Matrix.Identity;
    
    public void Render(World world)
    {
        world.Query((in Camera camera) =>  // Read-only
        {
            _cachedTransform = camera.GetTransformMatrix();
        });
    }
}
```

**Recommendation:** Separate state modification from rendering; let update systems manage dirty flags

---

## 2. Scene System Analysis

### 2.1 ISSUE: Scene Owns World Reference

**Problem:**
```csharp:21:78:MonoBallFramework.Game/Scenes/GameplayScene.cs
public class GameplayScene : SceneBase
{
    private readonly World _world;  // ⚠️ Scene owns World reference
    private readonly SystemManager _systemManager;
    
    public GameplayScene(
        GraphicsDevice graphicsDevice,
        IServiceProvider services,
        ILogger<GameplayScene> logger,
        World world,  // World passed in constructor
        SystemManager systemManager,
        // ... 8 more dependencies
    )
        : base(graphicsDevice, services, logger)
    {
        _world = world;
        _systemManager = systemManager;
        // ...
    }
}
```

**Why This Is Bad:**
- **Lifetime mismatch**: `World` is singleton, scenes are transient
- **Ownership confusion**: Who owns the World? Scene or DI container?
- **Resource leaks**: When scene disposes, World still exists
- **Multi-scene complexity**: What if two scenes need different Worlds?
- **Dependency injection violation**: Scene depends on global state

**Current Flow:**
```
Program.cs creates World (singleton)
  → World injected into GameplayScene constructor
  → Scene uses World for updates
  → Scene disposes, World still alive (leak potential)
```

**Industry Standard Pattern 1 (Game Owns World):**
```csharp
public class MonoBallFrameworkGame
{
    private World _world;  // Game owns the World
    private IScene _currentScene;
    
    protected override void Update(GameTime gameTime)
    {
        // Scene doesn't own World, just receives it
        _currentScene.Update(_world, gameTime);
    }
}

public interface IScene
{
    void Update(World world, GameTime gameTime);  // World passed per-frame
    void Draw(World world, GameTime gameTime);
}
```

**Industry Standard Pattern 2 (Scene Manager Owns World):**
```csharp
public class SceneManager
{
    private readonly World _world;  // SceneManager owns World
    private IScene _currentScene;
    
    public void Update(GameTime gameTime)
    {
        _currentScene.Update(_world, gameTime);
    }
}

// Scene is stateless, receives World on each call
public class GameplayScene : IScene
{
    public void Update(World world, GameTime gameTime)
    {
        // Use world but don't store it
    }
}
```

**Recommendation:** Refactor so scenes don't own World reference; either Game or SceneManager should own it

---

### 2.2 ISSUE: Scene Manager Manually Calls Initialize/LoadContent

**Problem:**
```csharp:161:210:MonoBallFramework.Game/Engine/Scenes/SceneManager.cs
public void Update(GameTime gameTime)
{
    // Handle scene transition at START of update cycle (two-step pattern)
    if (_nextScene != null)
    {
        try
        {
            IScene? sceneToTransition = _nextScene;
            bool isPush = _isPushOperation;
            _nextScene = null;
            _isPushOperation = false;

            if (isPush)
            {
                // Push to stack
                _sceneStack.Push(sceneToTransition);
                sceneToTransition.Initialize();
                // Manually call LoadContent() since MonoGame only does this for the main Game class
                sceneToTransition.LoadContent();  // ⚠️ Manual lifecycle management
                // ...
            }
            else
            {
                // Clear stack when changing base scene
                while (_sceneStack.Count > 0)
                {
                    IScene stackedScene = _sceneStack.Pop();
                    stackedScene.Dispose();
                }

                // Dispose current scene if it exists
                if (_currentScene != null)
                {
                    _currentScene.Dispose();
                    _currentScene = null;
                }

                // Set new scene and initialize it
                _currentScene = sceneToTransition;
                _currentScene.Initialize();
                // Manually call LoadContent() since MonoGame only does this for the main Game class
                _currentScene.LoadContent();  // ⚠️ Manual lifecycle management
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to transition to new scene");
            // ...
        }
    }
}
```

**Why This Is Bad:**
- **Fragile**: Easy to forget to call methods in the right order
- **Duplication**: Same pattern repeated for push/change operations
- **Error-prone**: If you add new lifecycle methods, must update multiple places
- **Testing difficulty**: Hard to test lifecycle independently
- **Comment smell**: "Manually call X" indicates missing abstraction

**Industry Standard:**
```csharp
public class SceneManager
{
    private void TransitionTo(IScene scene, bool push)
    {
        // Template method pattern - encapsulates lifecycle
        InitializeScene(scene);
        
        if (push)
        {
            _sceneStack.Push(scene);
        }
        else
        {
            DisposeCurrentScene();
            _currentScene = scene;
        }
    }
    
    private void InitializeScene(IScene scene)
    {
        // All lifecycle steps in one place
        try
        {
            scene.Initialize();
            scene.LoadContent();
            OnSceneInitialized(scene);  // Hook for subclasses
        }
        catch (Exception ex)
        {
            // Centralized error handling
            _logger.LogError(ex, "Failed to initialize scene {SceneType}", scene.GetType().Name);
            scene.Dispose();  // Clean up on failure
            throw;
        }
    }
    
    protected virtual void OnSceneInitialized(IScene scene) { }
}
```

**Recommendation:** Encapsulate scene lifecycle into dedicated methods using Template Method pattern

---

### 2.3 ISSUE: Scene State Validation Using Boolean Flags

**Problem:**
```csharp:12:125:MonoBallFramework.Game/Engine/Scenes/SceneBase.cs
public abstract class SceneBase : IScene
{
    private readonly object _lock = new();
    private bool _disposed;
    private bool _isContentLoaded;
    private bool _isInitialized;
    
    public bool IsDisposed
    {
        get
        {
            lock (_lock)  // ⚠️ Locking for simple flag check?
            {
                return _disposed;
            }
        }
        private set
        {
            lock (_lock)
            {
                _disposed = value;
            }
        }
    }
    
    public virtual void Initialize()
    {
        if (IsDisposed)  // ⚠️ Check 1
        {
            throw new ObjectDisposedException(nameof(SceneBase));
        }

        if (IsInitialized)  // ⚠️ Check 2
        {
            return;
        }

        IsInitialized = true;
        Logger.LogDebug("Scene {SceneType} initialized", GetType().Name);
    }
    
    public virtual void LoadContent()
    {
        if (IsDisposed)  // ⚠️ Check 1 repeated
        {
            throw new ObjectDisposedException(nameof(SceneBase));
        }

        if (IsContentLoaded)  // ⚠️ Check 2 repeated
        {
            return;
        }

        IsContentLoaded = true;
        Logger.LogDebug("Scene {SceneType} content loaded", GetType().Name);
    }
}
```

**Why This Is Bad:**
- **State machine anti-pattern**: Boolean flags instead of explicit states
- **Validation duplication**: Same checks in every method
- **Race condition potential**: Locking hints at threading concerns but doesn't solve them
- **Unclear state transitions**: What states are valid? What order?
- **Hard to debug**: Which flags should be set when?

**Industry Standard (State Pattern):**
```csharp
public enum SceneState
{
    Uninitialized,
    Initialized,
    ContentLoaded,
    Running,
    Disposed
}

public abstract class SceneBase : IScene
{
    private SceneState _state = SceneState.Uninitialized;
    
    public SceneState State
    {
        get => _state;
        private set
        {
            ValidateTransition(_state, value);  // Centralized validation
            _state = value;
            Logger.LogDebug("Scene {SceneType} transitioned: {OldState} -> {NewState}", 
                GetType().Name, _state, value);
        }
    }
    
    public void Initialize()
    {
        if (State != SceneState.Uninitialized)
        {
            throw new InvalidOperationException(
                $"Cannot initialize scene in state {State}");
        }
        
        State = SceneState.Initialized;
    }
    
    public void LoadContent()
    {
        if (State != SceneState.Initialized)
        {
            throw new InvalidOperationException(
                $"Cannot load content in state {State}");
        }
        
        State = SceneState.ContentLoaded;
    }
    
    private void ValidateTransition(SceneState from, SceneState to)
    {
        // Centralized transition validation
        bool isValid = (from, to) switch
        {
            (SceneState.Uninitialized, SceneState.Initialized) => true,
            (SceneState.Initialized, SceneState.ContentLoaded) => true,
            (SceneState.ContentLoaded, SceneState.Running) => true,
            (_, SceneState.Disposed) => true,  // Can always dispose
            _ => false
        };
        
        if (!isValid)
        {
            throw new InvalidOperationException(
                $"Invalid state transition: {from} -> {to}");
        }
    }
}
```

**Recommendation:** Replace boolean flags with explicit state enum and state machine

---

### 2.4 ISSUE: Scene Coupling to SystemManager

**Problem:**
```csharp:21:141:MonoBallFramework.Game/Scenes/GameplayScene.cs
public class GameplayScene : SceneBase
{
    private readonly SystemManager _systemManager;  // ⚠️ Tight coupling
    
    public override void Update(GameTime gameTime)
    {
        // Scene directly manages systems
        _systemManager.Update(_world, _gameTime.DeltaTime);
    }
    
    public override void Draw(GameTime gameTime)
    {
        GraphicsDevice.Clear(Color.CornflowerBlue);
        
        // Scene directly manages rendering
        _systemManager.Render(_world);
        
        _performanceOverlay.Draw();
        _eventInspectorOverlay?.Draw(GraphicsDevice);
    }
}
```

**Why This Is Bad:**
- **Single Responsibility Violation**: Scene shouldn't manage systems
- **Tight Coupling**: Scene depends on concrete SystemManager
- **Hard to test**: Must mock SystemManager
- **Inflexible**: Can't swap system management strategy

**Industry Standard:**
```csharp
// Pattern 1: Game manages systems, scenes just define content
public class MonoBallFrameworkGame
{
    private World _world;
    private SystemManager _systems;
    private IScene _currentScene;
    
    protected override void Update(GameTime gameTime)
    {
        _currentScene.Update(gameTime);  // Scene-specific logic
        _systems.Update(_world, deltaTime);  // Game updates systems
    }
    
    protected override void Draw(GameTime gameTime)
    {
        _systems.Render(_world);  // Game renders systems
        _currentScene.Draw(gameTime);  // Scene draws UI/overlays
    }
}

// Pattern 2: Scene defines WHICH systems, game runs them
public interface IScene
{
    IEnumerable<IUpdateSystem> GetSystems();
}

public class GameplayScene : IScene
{
    public IEnumerable<IUpdateSystem> GetSystems()
    {
        yield return new MovementSystem();
        yield return new RenderSystem();
    }
}
```

**Recommendation:** Move system management responsibility out of scenes

---

### 2.5 ISSUE: Scene Constructor Has Too Many Dependencies

**Problem:**
```csharp:48:61:MonoBallFramework.Game/Scenes/GameplayScene.cs
public GameplayScene(
    GraphicsDevice graphicsDevice,
    IServiceProvider services,
    ILogger<GameplayScene> logger,
    World world,
    SystemManager systemManager,
    IGameInitializer gameInitializer,
    IMapInitializer mapInitializer,
    InputManager inputManager,
    PerformanceMonitor performanceMonitor,
    IGameTimeService gameTime,
    SceneManager? sceneManager = null  // 11 dependencies! ⚠️
)
```

**Why This Is Bad:**
- **God Object smell**: Class doing too much
- **Dependency explosion**: 11+ dependencies is a code smell
- **Hard to test**: Must mock 11 objects
- **Fragile**: Adding new dependency requires changing constructor
- **Single Responsibility violation**: Scene is responsible for too many things

**Industry Standard:**
```csharp
// Pattern 1: Use Facade pattern
public class GameplaySceneContext
{
    public World World { get; init; }
    public SystemManager Systems { get; init; }
    public InputManager Input { get; init; }
    // ... group related dependencies
}

public class GameplayScene : SceneBase
{
    private readonly GameplaySceneContext _context;
    
    public GameplayScene(
        GraphicsDevice graphicsDevice,
        IServiceProvider services,
        ILogger<GameplayScene> logger,
        GameplaySceneContext context  // Single dependency
    )
        : base(graphicsDevice, services, logger)
    {
        _context = context;
    }
}

// Pattern 2: Use Service Locator (sparingly)
public class GameplayScene : SceneBase
{
    public GameplayScene(
        GraphicsDevice graphicsDevice,
        IServiceProvider services,  // Get dependencies on-demand
        ILogger<GameplayScene> logger
    )
        : base(graphicsDevice, services, logger)
    {
        // Lazy resolution
    }
    
    public override void Update(GameTime gameTime)
    {
        var systems = Services.GetRequiredService<SystemManager>();
        var world = Services.GetRequiredService<World>();
        systems.Update(world, deltaTime);
    }
}
```

**Recommendation:** Reduce constructor dependencies using Facade or Service Locator patterns

---

## 3. Integration Issues

### 3.1 ISSUE: Viewport Mutation from Multiple Sources

**Problem:**
- `Camera.UpdateViewportForResize()` mutates viewport
- `CameraViewportSystem.HandleResize()` calls above method
- `MonoBallFrameworkGame.OnClientSizeChanged()` calls `HandleResize()`
- `ElevationRenderSystem` sets viewport: `graphicsDevice.Viewport = new Viewport(virtualViewport)`

**Why This Is Bad:**
- **Side effects everywhere**: Viewport changes from multiple places
- **Order dependency**: Must resize before rendering
- **Race conditions**: Multiple threads could touch viewport
- **Hard to debug**: Where did viewport change?

**Recommendation:** Centralize viewport management in one place

---

### 3.2 ISSUE: Mixed Update/Render Responsibilities

**Problem:**
```csharp:40:44:MonoBallFramework.Game/Engine/Rendering/Systems/CameraViewportSystem.cs
public override void Update(World world, float deltaTime)
{
    // This system is event-driven and doesn't need per-frame updates
    // Camera viewport updates are triggered by window resize events via HandleResize
}
```

System implements `IUpdateSystem` but doesn't update! It's event-driven but forced into update loop.

**Why This Is Bad:**
- **Interface Segregation violation**: Implements interface but doesn't use it
- **Confusing**: Why is it an update system if it doesn't update?
- **Performance**: Added to update list but does nothing

**Recommendation:** Create `IEventDrivenSystem` interface for event-based systems

---

## 4. Code Smells Summary

### Critical Smells
1. ⚠️ **Mutable struct** - Camera is a mutable struct (major bug risk)
2. ⚠️ **Mixed responsibilities** - Components contain business logic
3. ⚠️ **Tight coupling** - Camera coupled to Player, Scene to SystemManager

### Major Smells
4. **Primitive obsession** - Boolean flags instead of state enum
5. **Feature envy** - Render system modifying camera state
6. **God object** - GameplayScene has 11+ dependencies
7. **Shotgun surgery** - Viewport mutation from multiple places

### Minor Smells
8. **Comments explaining code** - "Manually call LoadContent" indicates missing abstraction
9. **Long methods** - Scene drawing/updating does too much
10. **Magic numbers** - Hardcoded values (halfTile = 8f, CameraViewportMarginTiles = 2)

---

## 5. Recommended Refactoring Plan

### Phase 1: Critical Fixes (Week 1)
1. **Convert Camera to class** or make it immutable struct + extract logic to systems
2. **Create CameraService** to centralize camera operations
3. **Decouple camera from Player** using tag components

### Phase 2: Architectural Improvements (Week 2)
4. **Refactor scene lifecycle** using State Pattern
5. **Extract scene context** to reduce GameplayScene dependencies
6. **Move World ownership** to Game or SceneManager

### Phase 3: Code Quality (Week 3)
7. **Centralize viewport management** in one service
8. **Create IEventDrivenSystem** interface
9. **Extract magic numbers** to constants/configuration

### Phase 4: Testing & Documentation (Week 4)
10. Add unit tests for camera systems
11. Add unit tests for scene lifecycle
12. Document camera system architecture
13. Document scene stack behavior

---

## 6. Industry Standard Patterns to Adopt

### 6.1 ECS Best Practices
- ✅ **Pure data components** - No methods, no logic
- ✅ **Systems for logic** - All behavior in systems
- ✅ **Query-based** - Systems query for components they need
- ❌ **Avoid coupling** - Don't query Player+Camera, use tags

### 6.2 Scene Management Patterns
- ✅ **State Pattern** - For scene lifecycle
- ✅ **Template Method** - For scene initialization
- ✅ **Facade Pattern** - To reduce dependencies
- ❌ **God Objects** - Keep scenes focused

### 6.3 Camera System Patterns
- ✅ **Service Pattern** - CameraService abstracts operations
- ✅ **Observer Pattern** - For camera change events
- ✅ **Command Pattern** - For camera actions (zoom, pan, shake)
- ❌ **Tight Coupling** - Camera should be standalone

---

## 7. Example Refactoring: Camera System

### Before (Current)
```csharp
// Component with logic
public struct Camera
{
    public Vector2 Position { get; set; }
    public void Update(float deltaTime) { /* logic */ }
}

// System mutates component
public class CameraFollowSystem
{
    public void Update(World world, float deltaTime)
    {
        world.Query((ref Camera camera, ref Position pos) =>
        {
            camera.FollowTarget = pos.PixelPosition;
            camera.Update(deltaTime);  // Component method
        });
    }
}
```

### After (Recommended)
```csharp
// Pure data component
public struct Camera
{
    public Vector2 Position;
    public float Zoom;
    public Vector2? FollowTarget;
}

public struct MainCamera { }  // Tag component

// Systems with focused responsibilities
public class CameraFollowSystem : IUpdateSystem
{
    public void Update(World world, float deltaTime)
    {
        // Just set the follow target
        world.Query((ref Camera camera, in Position pos, in Player _) =>
        {
            camera.FollowTarget = pos.PixelPosition;
        });
    }
}

public class CameraUpdateSystem : IUpdateSystem
{
    public void Update(World world, float deltaTime)
    {
        // Handle all camera logic
        world.Query((ref Camera camera) =>
        {
            // Zoom transition
            if (Math.Abs(camera.Zoom - camera.TargetZoom) > 0.001f)
            {
                camera.Zoom = MathHelper.Lerp(camera.Zoom, camera.TargetZoom, 0.1f);
            }
            
            // Follow target
            if (camera.FollowTarget.HasValue)
            {
                camera.Position = Vector2.Lerp(camera.Position, camera.FollowTarget.Value, 0.2f);
            }
        });
    }
}

// Service for high-level operations
public class CameraService
{
    private World _world;
    private QueryDescription _mainCameraQuery = QueryCache.Get<Camera, MainCamera>();
    
    public void Shake(float intensity, float duration)
    {
        _world.Query(_mainCameraQuery, (ref Camera camera) =>
        {
            // Apply camera shake
        });
    }
    
    public Matrix GetViewMatrix()
    {
        Matrix result = Matrix.Identity;
        _world.Query(_mainCameraQuery, (in Camera camera) =>
        {
            result = CalculateViewMatrix(camera);
        });
        return result;
    }
}
```

---

## 8. References & Resources

### ECS Architecture
- [Data-Oriented Design Book](https://www.dataorienteddesign.com/dodbook/)
- [Overwatch Gameplay Architecture](https://www.youtube.com/watch?v=W3aieHjyNvw)
- [Unity DOTS Best Practices](https://docs.unity3d.com/Packages/com.unity.entities@1.0/manual/ecs_components.html)

### Scene Management
- [Game Programming Patterns - State](https://gameprogrammingpatterns.com/state.html)
- [Unreal Engine Scene Management](https://docs.unrealengine.com/5.0/en-US/levels-in-unreal-engine/)

### Camera Systems
- [2D Camera Systems](https://www.gamedeveloper.com/design/scroll-back-the-theory-and-practice-of-cameras-in-side-scrollers)
- [Virtual Camera Systems](https://www.gdcvault.com/play/1023017/Advanced-Camera-Systems-in)

---

## Conclusion

Your camera and scene systems are **functional** but have several **architectural issues** that will cause maintenance problems as the codebase grows. The most critical issue is the **mutable struct Camera component**, which violates ECS principles and creates subtle bugs.

**Priority Fixes:**
1. 🔥 **Camera struct** → class or immutable struct
2. 🔴 **Extract camera logic** to systems
3. 🟠 **Create CameraService** for high-level operations
4. 🟠 **Refactor scene lifecycle** with State Pattern
5. 🟡 **Reduce GameplayScene dependencies** with Facade

By addressing these issues, you'll have a more maintainable, testable, and industry-standard architecture.



