# Scene Management Implementation Plan

## Overview
This document outlines the step-by-step implementation plan for adding scene management to PokeSharp, including a loading scene that displays during async initialization.

## Prerequisites
- Review `docs/research/monogame-scene-management-research.md`
- Understand current initialization flow in `PokeSharpGame.cs`
- Familiarity with existing ECS architecture (World, SystemManager)

## Project Structure Decision

**Option A: New Project (Recommended)**
- Create `PokeSharp.Engine.Scenes` project
- Keeps scene management separate and reusable
- Can be shared across multiple game projects

**Option B: Add to Existing Project**
- Add scenes to `PokeSharp.Game` project
- Simpler, but less modular

**Decision: Option A** - Create new project for better separation of concerns.

## Implementation Phases

---

## Phase 1: Infrastructure Foundation

**Goal:** Create the core scene management infrastructure.

### Task 1.1: Create Scene Project
- [ ] Create new project `PokeSharp.Engine.Scenes`
- [ ] Add project reference to solution
- [ ] Add NuGet package references:
  - `Microsoft.Xna.Framework`
  - `Microsoft.Extensions.Logging.Abstractions`
- [ ] Add project references:
  - `PokeSharp.Engine.Core` (for ISystem, World, etc.)
  - `PokeSharp.Engine.Common` (for logging)

**Acceptance Criteria:**
- Project builds successfully
- Project structure matches other Engine projects

### Task 1.2: Create IScene Interface
**File:** `PokeSharp.Engine.Scenes/IScene.cs`

```csharp
public interface IScene : IDisposable
{
    bool IsDisposed { get; }
    bool IsInitialized { get; }
    bool IsContentLoaded { get; }
    
    void Initialize();
    void LoadContent();
    void UnloadContent();
    void Update(GameTime gameTime);
    void Draw(GameTime gameTime);
}
```

**Acceptance Criteria:**
- Interface defines all required lifecycle methods
- Follows MonoGame patterns (Initialize, LoadContent, Update, Draw)
- Includes disposal pattern

### Task 1.3: Create SceneBase Abstract Class
**File:** `PokeSharp.Engine.Scenes/SceneBase.cs`

**Requirements:**
- Implements `IScene`
- Provides `ContentManager` per scene (created with GraphicsDevice and root directory)
- Provides `GraphicsDevice` access
- Provides `IServiceProvider` for DI
- Implements basic lifecycle management
- Includes logging support
- Implements proper disposal pattern with finalizer (per MonoGame docs)

**Constructor:**
- `SceneBase(GraphicsDevice graphicsDevice, IServiceProvider services, ILogger logger, string contentRootDirectory = "Content")`
- Creates ContentManager with GraphicsDevice and contentRootDirectory
- Stores references to GraphicsDevice, Services, and Logger

**Key Properties:**
- `protected ContentManager Content { get; }` - Per-scene ContentManager instance
- `protected GraphicsDevice GraphicsDevice { get; }`
- `protected IServiceProvider Services { get; }`
- `protected ILogger Logger { get; }`
- `public bool IsDisposed { get; private set; }`
- `public bool IsInitialized { get; private set; }`
- `public bool IsContentLoaded { get; private set; }`

**Note on AssetManager vs ContentManager:**
- This implementation uses MonoGame's standard `ContentManager` for scene assets
- The existing `AssetManager` is used for runtime asset loading (textures, etc.)
- Scenes can use both: ContentManager for scene-specific content, AssetManager for runtime assets
- If needed, scenes can access AssetManager via IServiceProvider

**Key Methods:**
- `Initialize()` - Sets up scene (MonoGame will call LoadContent() automatically after)
- `LoadContent()` - Override to load scene assets (called automatically by MonoGame after Initialize())
- `UnloadContent()` - Unloads ContentManager
- `Dispose()` and `Dispose(bool disposing)` - Proper disposal pattern
- `~SceneBase()` - Finalizer for safety

**Note on ContentManager:**
- Each scene gets its own `ContentManager` instance
- Root directory should match game's `Content.RootDirectory` (typically "Content")
- ContentManager is created in constructor with GraphicsDevice and root directory
- ContentManager is disposed in UnloadContent() and Dispose()

**Acceptance Criteria:**
- Base class compiles
- Can create derived scene classes with required constructor parameters
- ContentManager is created with correct GraphicsDevice and root directory
- ContentManager is properly disposed in UnloadContent() and Dispose()
- Finalizer ensures cleanup if Dispose() not called
- Logging is available to derived classes
- IsInitialized and IsContentLoaded properties track lifecycle correctly
- Follows MonoGame disposal pattern exactly
- Can access AssetManager via IServiceProvider if needed

### Task 1.4: Create LoadingProgress Class
**File:** `PokeSharp.Engine.Scenes/LoadingProgress.cs`

**Requirements:**
- Thread-safe progress tracking
- Current step description
- Progress value (0.0 to 1.0)
- Completion status
- Error tracking

**Properties:**
- `float Progress { get; }` (0.0 to 1.0) - Thread-safe getter/setter using `Interlocked` or lock
- `string CurrentStep { get; }` - Thread-safe getter/setter using lock
- `bool IsComplete { get; }` - Thread-safe getter/setter using lock
- `Exception? Error { get; }` - Thread-safe getter/setter using lock

**Thread Safety Implementation:**
- Use `lock` statements for all property access
- Progress value should be clamped to 0.0-1.0 range in setter
- All properties use backing fields with lock protection

**Acceptance Criteria:**
- Thread-safe for async updates (verified with concurrent access tests)
- Can be read from UI thread without blocking
- Progress clamped to 0.0-1.0 range
- No race conditions in concurrent read/write scenarios

### Task 1.5: Create SceneManager Class
**File:** `PokeSharp.Engine.Scenes/SceneManager.cs`

**Requirements:**
- Manages current scene
- Handles scene transitions
- Supports scene stack (for overlays)
- Proper cleanup of previous scene
- Logging support
- Requires `GraphicsDevice` for creating scene ContentManager instances

**Constructor:**
- `SceneManager(GraphicsDevice graphicsDevice, IServiceProvider services, ILogger<SceneManager> logger)`
- GraphicsDevice is needed to create ContentManager instances for each scene
- Services is used for dependency injection in scenes
- Logger for transition logging

**Key Methods:**
- `void ChangeScene(IScene newScene)` - Queues scene change (two-step pattern)
- `void PushScene(IScene scene)` (for overlays) - Uses same two-step pattern with stack
- `void PopScene()` - Pops from stack, uses two-step pattern
- `IScene? CurrentScene { get; }` - Returns top of stack if stack has items, otherwise current scene
- `void Update(GameTime gameTime)` - Handles scene transition at start, then updates current scene
- `void Draw(GameTime gameTime)` - Draws current scene (always draws, even during initialization)

**Scene Stack Implementation:**
- Use `Stack<IScene>` for overlay scenes
- When stack has items, CurrentScene returns top of stack
- PushScene adds to stack and transitions using two-step pattern
- PopScene removes from stack and transitions back using two-step pattern
- Stack operations also use `_nextScene` pattern to defer transitions

**Important: Two-Step Transition Pattern (per MonoGame docs)**
- Use `_nextScene` field to defer transition until start of next Update cycle
- Prevents mid-frame scene changes
- Ensures current scene completes its cycle before disposal
- Pattern:
  ```csharp
  private IScene? _nextScene;
  
  public void ChangeScene(IScene newScene)
  {
      _nextScene = newScene; // Defer until Update()
  }
  
  public void Update(GameTime gameTime)
  {
      // Handle transition at START of update cycle
      if (_nextScene != null)
      {
          try
          {
              _currentScene?.Dispose();
              _currentScene = _nextScene;
              _currentScene.Initialize();
              // MonoGame will call LoadContent() automatically after Initialize()
              _nextScene = null;
              _logger?.LogInformation("Scene transitioned to {SceneType}", _currentScene.GetType().Name);
          }
          catch (Exception ex)
          {
              _logger?.LogError(ex, "Failed to transition to new scene");
              _nextScene = null;
              // Keep current scene active on error
          }
      }
      
      _currentScene?.Update(gameTime);
  }
  ```

**Error Handling:**
- Wrap scene transitions in try-catch
- Log errors with full exception details
- On transition failure, keep current scene active
- Never leave scene in undefined state

**Acceptance Criteria:**
- Can switch between scenes
- Uses two-step transition pattern (prevents mid-frame changes)
- Previous scene is properly disposed
- New scene is initialized before use (MonoGame calls LoadContent() automatically)
- Stack operations work correctly with two-step pattern
- Logs scene transitions with error handling
- Follows MonoGame's recommended transition pattern
- Handles transition errors gracefully
- Always has a valid current scene (never null after first scene is set)

### Task 1.6: Add Scene Services to DI
**File:** `PokeSharp.Game/Infrastructure/ServiceRegistration/SceneServicesExtensions.cs`

**Requirements:**
- Create extension method `AddSceneServices()` following existing pattern
- Register `SceneManager` as singleton (but note: requires GraphicsDevice, so may need factory pattern)
- Make `SceneManager` available via DI
- Call `AddSceneServices()` from `ServiceCollectionExtensions.AddGameServices()`

**Implementation Note:**
- SceneManager requires GraphicsDevice which isn't available during DI registration
- Options:
  1. Register SceneManager as singleton but create it in PokeSharpGame.Initialize() after GraphicsDevice is available
  2. Use factory pattern to create SceneManager when needed
  3. Register SceneManager in PokeSharpGame.Initialize() and store in a service locator pattern
- **Recommendation**: Create SceneManager in PokeSharpGame.Initialize() and store as field (simplest approach)

**Acceptance Criteria:**
- Extension method follows existing ServiceCollectionExtensions pattern
- SceneManager can be created when GraphicsDevice is available
- Services registration integrates with existing AddGameServices() method

**Phase 1 Deliverables:**
- ✅ Scene infrastructure project created
- ✅ IScene interface defined
- ✅ SceneBase class implemented
- ✅ LoadingProgress class implemented
- ✅ SceneManager class implemented
- ✅ DI registration complete
- ✅ All unit tests pass

**Estimated Time:** 4-6 hours

---

## Phase 2: Loading Scene Implementation

**Goal:** Implement loading scene that displays during async initialization.

### Task 2.1: Create LoadingScene Class
**File:** `PokeSharp.Engine.Scenes/Scenes/LoadingScene.cs`

**Requirements:**
- Inherits from `SceneBase`
- Takes `Task<GameplayScene>` and `LoadingProgress` in constructor
- Displays progress bar
- Shows current loading step
- Handles initialization completion
- Shows error state if initialization fails

**Key Features:**
- Minimal asset loading (use programmatic graphics if possible)
- Progress bar rendering
- Status text display
- Error message display
- Completion detection

**Acceptance Criteria:**
- Loading scene displays immediately
- Progress updates in real-time
- Shows current step description
- Handles completion correctly
- Displays errors if initialization fails

### Task 2.2: Create Simple Progress Bar Rendering
**Requirements:**
- Programmatically generate progress bar textures (or use simple rectangles)
- Draw progress bar centered on screen
- Show percentage or progress visually
- Optional: Add animation (pulsing, etc.)

**Acceptance Criteria:**
- Progress bar renders correctly
- Updates smoothly as progress changes
- Visually clear and readable

### Task 2.3: Refactor PokeSharpGame.InitializeAsync()
**File:** `PokeSharp.Game/PokeSharpGame.cs`

**Changes Required:**
1. Extract initialization logic to new method: `InitializeGameplaySceneAsync(LoadingProgress)`
2. Method should return `Task<GameplayScene>`
3. Add progress reporting at each initialization step:
   - Game data loading (0.0 → 0.2)
   - Template cache (0.2 → 0.35)
   - Asset manager (0.35 → 0.4)
   - Sprite manifests (0.4 → 0.75)
   - Game systems (0.75 → 0.85)
   - Map loading (0.85 → 0.95)
   - Player creation (0.95 → 1.0)

**Progress Reporting Pattern:**
```csharp
progress.CurrentStep = "Loading game data...";
progress.Progress = 0.1f;
await _dataLoader.LoadAllAsync("Assets/Data");
progress.Progress = 0.2f;
```

**Acceptance Criteria:**
- All initialization steps report progress
- Progress values are accurate
- Current step descriptions are clear
- Method returns fully initialized GameplayScene

### Task 2.4: Integrate Loading Scene in PokeSharpGame
**File:** `PokeSharp.Game/PokeSharpGame.cs`

**Changes Required:**
1. Add `SceneManager?` field (created in Initialize(), not injected)
2. Add `LoadingProgress?` field
3. Add `Task<GameplayScene>?` field for initialization task
4. Modify `Initialize()` to:
   - Call `base.Initialize()` first (ensures GraphicsDevice is available)
   - Create SceneManager with GraphicsDevice, Services, and Logger
   - Create LoadingProgress
   - Start initialization task (InitializeGameplaySceneAsync)
   - Create and show LoadingScene immediately via SceneManager
5. Modify `Update()` to:
   - Always call SceneManager.Update() (even during initialization)
   - Check if loading is complete
   - Transition to GameplayScene when ready
   - Handle errors
   - Remove the early return that blocks updates during initialization
6. Modify `Draw()` to:
   - Always call SceneManager.Draw() (even during initialization)
   - Remove the `if (_isInitialized)` check that prevents rendering
   - SceneManager will handle drawing the loading scene

**Critical Changes:**
- **Update()**: Remove early return during initialization - SceneManager.Update() must be called so LoadingScene can update
- **Draw()**: Remove `if (_isInitialized)` check - SceneManager.Draw() must always be called so LoadingScene can render
- SceneManager is created in Initialize() after base.Initialize() when GraphicsDevice is available

**Acceptance Criteria:**
- Loading scene appears immediately on game start
- Progress updates during initialization
- Smooth transition to gameplay when complete
- Errors are handled gracefully

### Task 2.5: Create GameplayScene Stub
**File:** `PokeSharp.Game/Scenes/GameplayScene.cs`

**Decision:** GameplayScene is game-specific, so it belongs in `PokeSharp.Game/Scenes/` not in the Engine.Scenes project.

**Initial Requirements:**
- Inherits from `SceneBase`
- Takes all necessary dependencies in constructor:
  - World
  - SystemManager
  - GameInitializer
  - MapInitializer
  - InputManager
  - PerformanceMonitor
  - GameTimeService
  - All other dependencies currently used in PokeSharpGame.Update() and Draw()
- For now, just a stub that shows it's working
- Full implementation will come in Phase 3

**Acceptance Criteria:**
- GameplayScene can be created
- Receives all necessary dependencies
- Can be transitioned to from LoadingScene
- Located in correct project (PokeSharp.Game/Scenes/)

**Phase 2 Deliverables:**
- ✅ LoadingScene implemented
- ✅ Progress reporting integrated
- ✅ Loading scene displays during initialization
- ✅ Smooth transition to gameplay
- ✅ Error handling works

**Estimated Time:** 6-8 hours

---

## Phase 3: Extract Gameplay to GameplayScene

**Goal:** Move all gameplay logic from PokeSharpGame to GameplayScene.

### Task 3.1: Analyze Current PokeSharpGame Logic
**File:** `PokeSharp.Game/PokeSharpGame.cs`

**Identify:**
- What logic belongs in GameplayScene
- What logic stays in PokeSharpGame (core game loop)
- Dependencies needed by GameplayScene
- Systems that need to be registered/unregistered

**Deliverable:** List of dependencies and logic to move

### Task 3.2: Create GameplayScene Class
**File:** `PokeSharp.Game/Scenes/GameplayScene.cs`

**Requirements:**
- Takes all game dependencies in constructor:
  - World
  - SystemManager
  - GameInitializer
  - MapInitializer
  - InputManager
  - PerformanceMonitor
  - GameTimeService
  - etc.
- Implements scene lifecycle
- Registers/unregisters scene-specific systems
- Manages gameplay entities

**Key Methods:**
- `Initialize()` - Register systems
- `Update()` - Update game logic
- `Draw()` - Render game (or delegate to SystemManager)
- `UnloadContent()` - Cleanup
- `Dispose()` - Unregister systems

**Acceptance Criteria:**
- GameplayScene compiles
- Can be instantiated with all dependencies
- Lifecycle methods are properly implemented

### Task 3.3: Move Update Logic to GameplayScene
**Requirements:**
- Move game update logic from `PokeSharpGame.Update()`
- Handle input processing
- Update systems via SystemManager
- Update performance monitoring
- Update game time service

**Acceptance Criteria:**
- All update logic moved to scene
- Game updates correctly when scene is active
- No logic left in PokeSharpGame.Update() (except scene management)

### Task 3.4: Move Draw Logic to GameplayScene
**Requirements:**
- Move rendering logic from `PokeSharpGame.Draw()`
- Delegate to SystemManager.Render()
- Handle GraphicsDevice.Clear()

**Acceptance Criteria:**
- Rendering works correctly
- No rendering logic in PokeSharpGame.Draw() (except scene delegation)

### Task 3.5: System Registration Management
**Requirements:**
- Identify which systems are gameplay-specific vs global
- **Analysis needed**: Review SystemManager to determine:
  - Which systems are registered in GameInitializer (likely gameplay-specific)
  - Which systems are registered globally (if any)
  - Whether SystemManager supports unregistering systems
- **Decision**: 
  - If SystemManager supports unregistration: Register in GameplayScene.Initialize(), unregister in Dispose()
  - If SystemManager doesn't support unregistration: Systems remain registered globally, but GameplayScene controls when they're active via Update/Draw
- Keep core systems (rendering pipeline) registered globally
- GameplayScene should not re-register systems that are already registered

**Implementation Approach:**
- Most systems are registered once in GameInitializer and remain active
- GameplayScene controls when systems run by calling SystemManager.Update() and SystemManager.Render()
- Systems don't need to be unregistered between scenes if they're gameplay-specific
- If pause menu is added later, systems can be disabled via a flag rather than unregistration

**Acceptance Criteria:**
- Systems are properly identified as gameplay-specific or global
- No duplicate system registration
- Systems run correctly when GameplayScene is active
- System lifecycle is clear and documented

### Task 3.6: Entity Management
**Requirements:**
- Ensure entities are created in correct scene
- Tag entities with scene identifier (optional)
- Clean up scene-specific entities on scene change
- Handle player entity lifecycle

**Acceptance Criteria:**
- Entities are properly managed
- No entity leaks
- Player entity persists correctly

### Task 3.7: Update PokeSharpGame to Use Scenes
**Requirements:**
- PokeSharpGame becomes thin wrapper
- Delegates Update/Draw to SceneManager
- Handles scene transitions
- Manages initialization flow

**Final PokeSharpGame Structure:**
```csharp
protected override void Initialize()
{
    // Create scene manager
    // Start initialization
    // Show loading scene
}

protected override void Update(GameTime gameTime)
{
    // Handle scene transitions
    // Delegate to scene manager
}

protected override void Draw(GameTime gameTime)
{
    // Clear screen
    // Delegate to scene manager
}
```

**Acceptance Criteria:**
- PokeSharpGame is simplified
- All gameplay logic in GameplayScene
- Game runs identically to before
- Scene transitions work

**Phase 3 Deliverables:**
- ✅ GameplayScene fully implemented
- ✅ All gameplay logic moved
- ✅ Systems properly managed
- ✅ Entities properly managed
- PokeSharpGame simplified
- ✅ Game runs correctly

**Estimated Time:** 8-10 hours

---

## Phase 4: Testing and Refinement

**Goal:** Ensure scene management works correctly and handle edge cases.

### Task 4.1: Unit Tests for Scene Infrastructure
**Files:** `tests/PokeSharp.Engine.Scenes.Tests/`

**Test Coverage:**
- SceneBase lifecycle
- SceneManager scene switching
- SceneManager scene stack
- LoadingProgress thread safety
- Error handling

**Acceptance Criteria:**
- All unit tests pass
- Good test coverage (>80%)

### Task 4.2: Integration Tests
**Requirements:**
- Test loading scene → gameplay transition
- Test error handling during initialization
- Test scene cleanup
- Test system registration/unregistration

**Acceptance Criteria:**
- Integration tests pass
- Edge cases handled

### Task 4.3: Performance Testing
**Requirements:**
- Measure scene transition time
- Check for memory leaks
- Verify proper disposal
- Profile initialization time

**Acceptance Criteria:**
- No memory leaks
- Fast scene transitions
- Proper resource cleanup

### Task 4.4: Error Handling Improvements
**Requirements:**
- Handle initialization failures gracefully
- Show user-friendly error messages
- Provide retry mechanism (optional)
- Log errors appropriately

**Acceptance Criteria:**
- Errors are handled gracefully
- User sees clear error messages
- Game doesn't crash on initialization failure

### Task 4.5: Polish Loading Screen
**Requirements:**
- Improve visual design
- Add animations (spinner, pulsing, etc.)
- Add loading tips (optional)
- Improve progress bar appearance

**Acceptance Criteria:**
- Loading screen looks professional
- Animations are smooth
- Progress is clearly visible

**Phase 4 Deliverables:**
- ✅ Comprehensive test coverage
- ✅ Performance verified
- ✅ Error handling robust
- ✅ Loading screen polished

**Estimated Time:** 6-8 hours

---

## Phase 5: Future Enhancements (Optional)

**Goal:** Add additional scenes and features.

### Task 5.1: Menu Scene
- Create main menu scene
- Add navigation
- Transition from menu → loading → gameplay

### Task 5.2: Pause Scene (Overlay)
- Implement scene stack for pause menu
- Pause gameplay scene
- Show pause menu overlay

### Task 5.3: Scene Transition Effects
- Fade transitions
- Slide transitions
- Custom transition effects

### Task 5.4: Scene Persistence
- Save/load scene state
- Persist player position
- Handle scene changes (map transitions)

---

## Implementation Checklist

### Phase 1: Infrastructure
- [ ] Create PokeSharp.Engine.Scenes project
- [ ] Implement IScene interface
- [ ] Implement SceneBase class
- [ ] Implement LoadingProgress class
- [ ] Implement SceneManager class
- [ ] Register services in DI

### Phase 2: Loading Scene
- [ ] Create LoadingScene class
- [ ] Implement progress bar rendering
- [ ] Refactor InitializeAsync() with progress reporting
- [ ] Integrate loading scene in PokeSharpGame
- [ ] Create GameplayScene stub

### Phase 3: Gameplay Scene
- [ ] Analyze current PokeSharpGame logic
- [ ] Create full GameplayScene class
- [ ] Move update logic
- [ ] Move draw logic
- [ ] Implement system registration
- [ ] Implement entity management
- [ ] Simplify PokeSharpGame

### Phase 4: Testing
- [ ] Write unit tests
- [ ] Write integration tests
- [ ] Performance testing
- [ ] Error handling improvements
- [ ] Polish loading screen

---

## Dependencies and Prerequisites

### External Dependencies
- MonoGame Framework
- Microsoft.Extensions.Logging
- Arch ECS (already in project)

### Internal Dependencies
- PokeSharp.Engine.Core
- PokeSharp.Engine.Common
- PokeSharp.Game (for GameplayScene)

### Knowledge Prerequisites
- Understanding of MonoGame lifecycle
- Understanding of ECS architecture
- Understanding of async/await patterns
- Understanding of dependency injection

---

## Risk Assessment

### High Risk
- **Breaking existing gameplay**: Moving logic from PokeSharpGame to GameplayScene could break things
  - **Mitigation**: Move incrementally, test after each change

### Medium Risk
- **System registration conflicts**: Systems might not register/unregister correctly
  - **Mitigation**: Clear documentation of which systems belong where, thorough testing

- **Entity lifecycle issues**: Entities might not be cleaned up properly
  - **Mitigation**: Tag entities with scene, clear on scene change

### Low Risk
- **Performance impact**: Scene transitions might be slow
  - **Mitigation**: Profile and optimize if needed

---

## Success Criteria

### Must Have
- ✅ Loading scene displays during initialization
- ✅ Progress is visible and accurate
- ✅ Smooth transition to gameplay
- ✅ Game runs identically to before
- ✅ No memory leaks
- ✅ Proper error handling

### Nice to Have
- Animated loading screen
- Loading tips
- Smooth transition effects
- Menu scene
- Pause menu overlay

---

## Timeline Estimate

- **Phase 1**: 4-6 hours
- **Phase 2**: 6-8 hours
- **Phase 3**: 8-10 hours
- **Phase 4**: 6-8 hours
- **Total**: 24-32 hours

**Note:** Times are estimates and may vary based on complexity and unexpected issues.

---

## Next Steps

1. Review and approve this implementation plan
2. Set up development branch: `feature/scene-management`
3. Begin Phase 1 implementation
4. Create GitHub issues for each phase (optional)
5. Start with Task 1.1: Create Scene Project

---

## Questions to Resolve

1. **Project Location**: Should scenes be in `PokeSharp.Engine.Scenes` or `PokeSharp.Game.Scenes`?
   - **Recommendation**: `PokeSharp.Engine.Scenes` for reusability

2. **GameplayScene Location**: Should it be in Engine.Scenes or Game project?
   - **Decision**: `PokeSharp.Game/Scenes/GameplayScene.cs` since it's game-specific

3. **System Registration**: Which systems are scene-specific vs global?
   - **Need to analyze**: Review SystemManager to identify

4. **Entity Cleanup**: How aggressive should entity cleanup be on scene change?
   - **Recommendation**: Only cleanup scene-tagged entities, keep player persistent

---

## References

- Research Document: `docs/research/monogame-scene-management-research.md`
- MonoGame Scene Tutorial: https://docs.monogame.net/articles/tutorials/building_2d_games/17_scenes/
- Current Game Class: `PokeSharp.Game/PokeSharpGame.cs`

