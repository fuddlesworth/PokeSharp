# MonoGame Scene Management Research

## Overview
This document outlines best practices for implementing scene management in MonoGame, specifically tailored for PokeSharp's architecture which uses Arch ECS (Entity Component System).

## Current State
- **No scene support**: All game logic is currently in `PokeSharpGame.cs`
- **ECS Architecture**: Uses Arch ECS with `World` and `SystemManager`
- **Initialization**: Complex async initialization process
- **Systems**: Multiple systems managed via `SystemManager`

## Best Practices for MonoGame Scene Management

### 1. Base Scene Class Pattern

The foundation of scene management is an abstract base class that all scenes inherit from. This provides a consistent interface for lifecycle management.

**Key Methods:**
- `Initialize()` - Setup scene-specific logic
- `LoadContent()` - Load scene assets
- `UnloadContent()` - Unload scene assets
- `Update(GameTime)` - Update scene logic
- `Draw(GameTime)` - Render scene
- `Dispose()` - Clean up resources

**Benefits:**
- Consistent lifecycle management
- Proper resource cleanup
- Easy to extend for new scenes

### 2. Scene Manager Pattern

A centralized manager handles scene transitions and maintains the current scene state.

**Responsibilities:**
- Track current scene
- Handle scene transitions
- Ensure proper cleanup of previous scene
- Initialize new scene before switching

**Key Features:**
- Scene stack support (for overlays like pause menus)
- Transition effects (fade, slide, etc.)
- Async scene loading support

### 3. Content Management Per Scene

Each scene should have its own `ContentManager` instance to:
- Isolate assets per scene
- Enable proper unloading when scene changes
- Prevent memory leaks
- Support parallel loading

**Pattern:**
```csharp
public abstract class Scene
{
    protected ContentManager Content { get; }
    
    public Scene(IServiceProvider serviceProvider, string contentRoot)
    {
        Content = new ContentManager(serviceProvider, contentRoot);
    }
}
```

### 4. Integration with ECS Architecture

For PokeSharp's ECS architecture, scenes should:

**Option A: Scene-Owned World (Recommended for Isolation)**
- Each scene has its own `World` instance
- Systems are registered per scene
- Complete isolation between scenes
- More memory overhead but cleaner separation

**Option B: Shared World with Scene Context (Recommended for Performance)**
- Single shared `World` across all scenes
- Scenes manage which entities/systems are active
- Use scene tags/components to filter entities
- More efficient but requires careful entity management

**Option C: Hybrid Approach**
- Core systems shared (rendering, input)
- Scene-specific systems per scene
- Entities tagged with scene identifier

### 5. System Management Per Scene

**Considerations:**
- Some systems are scene-agnostic (input, rendering pipeline)
- Some systems are scene-specific (gameplay, menus)
- System registration/unregistration on scene change
- System priority management per scene

**Pattern:**
```csharp
public abstract class Scene
{
    protected SystemManager SystemManager { get; }
    protected World World { get; }
    
    protected abstract void RegisterSystems();
    protected abstract void UnregisterSystems();
}
```

### 6. Async Scene Loading

MonoGame's content loading can be slow. Consider:
- Async scene initialization
- Loading screens during transitions
- Preloading next scene while current scene is active
- Progress reporting for long loads

### 6.1. Loading Scene Implementation

A loading scene is essential for games with complex async initialization. It provides visual feedback during the loading process and ensures the game remains responsive.

#### Purpose
- Display progress while services and data load
- Show loading status messages
- Provide visual feedback (progress bar, spinner, etc.)
- Transition to gameplay scene once initialization completes

#### Implementation Pattern

**Loading Progress Tracker:**
```csharp
public class LoadingProgress
{
    public float Progress { get; set; } // 0.0 to 1.0
    public string CurrentStep { get; set; } = string.Empty;
    public bool IsComplete { get; set; }
    public Exception? Error { get; set; }
}
```

**Loading Scene Structure:**
```csharp
public class LoadingScene : SceneBase
{
    private readonly Task<GameplayScene> _initializationTask;
    private readonly LoadingProgress _progress;
    private SpriteFont? _font;
    private Texture2D? _loadingBarBackground;
    private Texture2D? _loadingBarFill;
    
    public LoadingScene(
        IServiceProvider serviceProvider,
        Task<GameplayScene> initializationTask,
        LoadingProgress progress
    ) : base(serviceProvider)
    {
        _initializationTask = initializationTask;
        _progress = progress;
    }
    
    public override void LoadContent()
    {
        // Load minimal assets needed for loading screen
        _font = Content.Load<SpriteFont>("Fonts/Default");
        // Create simple loading bar textures programmatically if needed
    }
    
    public override void Update(GameTime gameTime)
    {
        // Check if initialization is complete
        if (_initializationTask.IsCompleted)
        {
            if (_initializationTask.IsFaulted)
            {
                _progress.Error = _initializationTask.Exception;
                _progress.IsComplete = true;
            }
            else if (_initializationTask.IsCompletedSuccessfully)
            {
                _progress.IsComplete = true;
                _progress.Progress = 1.0f;
                // SceneManager will handle transition
            }
        }
    }
    
    public override void Draw(GameTime gameTime)
    {
        var spriteBatch = new SpriteBatch(GraphicsDevice);
        spriteBatch.Begin();
        
        // Draw background
        spriteBatch.Draw(/* background texture */, Vector2.Zero, Color.White);
        
        // Draw loading text
        var loadingText = $"Loading... {_progress.CurrentStep}";
        var textSize = _font?.MeasureString(loadingText) ?? Vector2.Zero;
        var textPosition = new Vector2(
            (GraphicsDevice.Viewport.Width - textSize.X) / 2,
            GraphicsDevice.Viewport.Height / 2 - 50
        );
        spriteBatch.DrawString(_font, loadingText, textPosition, Color.White);
        
        // Draw progress bar
        DrawProgressBar(spriteBatch, _progress.Progress);
        
        // Draw error if initialization failed
        if (_progress.Error != null)
        {
            var errorText = $"Error: {_progress.Error.Message}";
            spriteBatch.DrawString(_font, errorText, 
                new Vector2(10, 10), Color.Red);
        }
        
        spriteBatch.End();
    }
    
    private void DrawProgressBar(SpriteBatch spriteBatch, float progress)
    {
        // Draw progress bar implementation
        var barWidth = 400;
        var barHeight = 20;
        var barX = (GraphicsDevice.Viewport.Width - barWidth) / 2;
        var barY = GraphicsDevice.Viewport.Height / 2 + 20;
        
        // Background
        spriteBatch.Draw(_loadingBarBackground, 
            new Rectangle(barX, barY, barWidth, barHeight), 
            Color.Gray);
        
        // Fill
        var fillWidth = (int)(barWidth * progress);
        spriteBatch.Draw(_loadingBarFill, 
            new Rectangle(barX, barY, fillWidth, barHeight), 
            Color.Green);
    }
}
```

#### Integration with Async Initialization

**Modified PokeSharpGame.cs Pattern:**
```csharp
public class PokeSharpGame : Game
{
    private SceneManager _sceneManager = null!;
    private LoadingProgress _loadingProgress = new();
    private Task<GameplayScene>? _initializationTask;
    
    protected override void Initialize()
    {
        base.Initialize();
        
        // Create scene manager
        _sceneManager = new SceneManager(Services);
        
        // Create loading scene immediately
        _loadingProgress = new LoadingProgress();
        _initializationTask = InitializeGameplaySceneAsync(_loadingProgress);
        
        var loadingScene = new LoadingScene(
            Services,
            _initializationTask,
            _loadingProgress
        );
        
        _sceneManager.ChangeScene(loadingScene);
    }
    
    private async Task<GameplayScene> InitializeGameplaySceneAsync(
        LoadingProgress progress)
    {
        try
        {
            // Step 1: Load game data
            progress.CurrentStep = "Loading game data...";
            progress.Progress = 0.1f;
            await _dataLoader.LoadAllAsync("Assets/Data");
            
            // Step 2: Initialize template cache
            progress.CurrentStep = "Loading templates...";
            progress.Progress = 0.2f;
            await _templateCacheInitializer.InitializeAsync();
            
            // Step 3: Create asset manager
            progress.CurrentStep = "Initializing assets...";
            progress.Progress = 0.3f;
            var assetManager = new AssetManager(GraphicsDevice, "Assets", ...);
            
            // Step 4: Load sprite manifests
            progress.CurrentStep = "Loading sprites...";
            progress.Progress = 0.5f;
            await _spriteLoader.LoadAllSpritesAsync();
            
            // Step 5: Initialize game systems
            progress.CurrentStep = "Initializing systems...";
            progress.Progress = 0.7f;
            _gameInitializer.Initialize(GraphicsDevice);
            
            // Step 6: Load map
            progress.CurrentStep = "Loading map...";
            progress.Progress = 0.9f;
            await _mapInitializer.LoadMap("test-map");
            
            // Step 7: Create player
            progress.CurrentStep = "Finalizing...";
            progress.Progress = 0.95f;
            _playerFactory.CreatePlayer(...);
            
            progress.Progress = 1.0f;
            progress.CurrentStep = "Complete!";
            
            // Create and return gameplay scene
            return new GameplayScene(Services, _world, _systemManager, ...);
        }
        catch (Exception ex)
        {
            progress.Error = ex;
            throw;
        }
    }
    
    protected override void Update(GameTime gameTime)
    {
        // Check if loading is complete and transition
        if (_sceneManager.CurrentScene is LoadingScene loadingScene)
        {
            if (loadingScene.IsComplete && _initializationTask?.IsCompletedSuccessfully == true)
            {
                var gameplayScene = _initializationTask.Result;
                _sceneManager.ChangeScene(gameplayScene);
            }
        }
        
        _sceneManager.Update(gameTime);
        base.Update(gameTime);
    }
    
    protected override void Draw(GameTime gameTime)
    {
        GraphicsDevice.Clear(Color.CornflowerBlue);
        _sceneManager.Draw(gameTime);
        base.Draw(gameTime);
    }
}
```

#### Progress Reporting Strategies

**Option 1: Simple Step-Based Progress**
- Report progress at major milestones
- Use fixed percentages (0.1, 0.2, 0.3, etc.)
- Simple to implement

**Option 2: Weighted Progress**
- Assign weights to different loading steps
- More accurate progress representation
- Example: Data loading (30%), Sprites (40%), Systems (20%), Map (10%)

**Option 3: Detailed Progress with Sub-Steps**
- Report progress for individual items
- Example: "Loading sprite 45/200..."
- More informative but requires more tracking

**Implementation Example:**
```csharp
public class WeightedLoadingProgress
{
    private readonly Dictionary<string, float> _stepWeights = new()
    {
        { "game_data", 0.2f },
        { "templates", 0.15f },
        { "sprites", 0.35f },
        { "systems", 0.15f },
        { "map", 0.10f },
        { "finalize", 0.05f }
    };
    
    private float _currentProgress = 0f;
    private string _currentStep = "Initializing...";
    
    public void ReportStep(string stepName, float subProgress = 1.0f)
    {
        if (_stepWeights.TryGetValue(stepName, out var weight))
        {
            _currentProgress += weight * subProgress;
            _currentStep = FormatStepName(stepName);
        }
    }
    
    public float Progress => Math.Min(_currentProgress, 1.0f);
    public string CurrentStep => _currentStep;
}
```

#### Visual Design Considerations

**Loading Screen Elements:**
- **Background**: Simple gradient or game-themed image
- **Progress Bar**: Visual indicator of loading progress
- **Status Text**: Current loading step description
- **Animated Elements**: Spinner, pulsing dots, or game-specific animation
- **Logo/Branding**: Game title or logo

**Best Practices:**
- Keep loading screen assets minimal (load quickly)
- Use programmatically generated graphics when possible
- Provide clear feedback on what's happening
- Consider showing tips or game information during loading
- Handle errors gracefully with clear messages

#### Transition from Loading to Gameplay

**Smooth Transition Pattern:**
```csharp
public class SceneManager
{
    public void ChangeScene(Scene newScene)
    {
        // Option 1: Instant transition
        _currentScene?.Dispose();
        _currentScene = newScene;
        _currentScene.Initialize();
        _currentScene.LoadContent();
        
        // Option 2: Fade transition
        StartFadeTransition(_currentScene, newScene);
    }
    
    private void StartFadeTransition(Scene from, Scene to)
    {
        // Implement fade-out/fade-in transition
        // During transition, both scenes may need to render
    }
}
```

**For Loading Scene Specifically:**
- Loading scene should transition immediately when complete
- No need for fade effects (user expects instant transition)
- Ensure gameplay scene is fully ready before transition
- Handle initialization errors by showing error state in loading scene

#### Refactoring Existing Initialization Code

**Current Pattern (PokeSharpGame.cs):**
The current initialization happens in `InitializeAsync()` and blocks game updates until complete. To integrate with a loading scene:

**Step 1: Extract Initialization to Separate Method**
```csharp
// Move initialization logic to a method that returns GameplayScene
private async Task<GameplayScene> InitializeGameplaySceneAsync(
    LoadingProgress progress)
{
    // All existing initialization code, but with progress reporting
    // Returns fully initialized GameplayScene
}
```

**Step 2: Modify Initialize() Method**
```csharp
protected override void Initialize()
{
    base.Initialize();
    
    // Create scene manager early
    _sceneManager = new SceneManager(Services);
    
    // Create loading progress tracker
    _loadingProgress = new LoadingProgress();
    
    // Start async initialization (returns GameplayScene when done)
    _initializationTask = InitializeGameplaySceneAsync(_loadingProgress);
    
    // Create and show loading scene immediately
    var loadingScene = new LoadingScene(
        Services,
        _initializationTask,
        _loadingProgress
    );
    loadingScene.Initialize();
    loadingScene.LoadContent();
    
    _sceneManager.ChangeScene(loadingScene);
}
```

**Step 3: Update Update() Method**
```csharp
protected override void Update(GameTime gameTime)
{
    // Handle loading scene transition
    if (_sceneManager.CurrentScene is LoadingScene loadingScene)
    {
        if (loadingScene.IsComplete)
        {
            if (_initializationTask?.IsCompletedSuccessfully == true)
            {
                var gameplayScene = _initializationTask.Result;
                _sceneManager.ChangeScene(gameplayScene);
            }
            else if (_initializationTask?.IsFaulted == true)
            {
                // Handle error - could show error scene or retry
                _loggerFactory
                    .CreateLogger<PokeSharpGame>()
                    .LogCritical(
                        _initializationTask.Exception,
                        "Game initialization failed"
                    );
                // Don't exit immediately - let loading scene show error
            }
        }
    }
    
    // Update current scene
    _sceneManager.Update(gameTime);
    base.Update(gameTime);
}
```

**Step 4: Add Progress Reporting to Initialization Steps**

Modify each initialization step to report progress:

```csharp
private async Task<GameplayScene> InitializeGameplaySceneAsync(
    LoadingProgress progress)
{
    try
    {
        // Step 1: Load game data (20% of total)
        progress.CurrentStep = "Loading game data definitions...";
        progress.Progress = 0.0f;
        try
        {
            await _dataLoader.LoadAllAsync("Assets/Data");
            progress.Progress = 0.2f;
            _loggerFactory
                .CreateLogger<PokeSharpGame>()
                .LogInformation("Game data definitions loaded successfully");
        }
        catch (Exception ex)
        {
            _loggerFactory
                .CreateLogger<PokeSharpGame>()
                .LogError(ex, "Failed to load game data definitions");
            // Continue with defaults
            progress.Progress = 0.2f;
        }

        // Step 2: Initialize template cache (15% of total)
        progress.CurrentStep = "Loading templates and mods...";
        await _templateCacheInitializer.InitializeAsync();
        progress.Progress = 0.35f;
        _loggerFactory
            .CreateLogger<PokeSharpGame>()
            .LogInformation("Template cache initialized successfully");

        // Step 3: Create asset manager (5% of total)
        progress.CurrentStep = "Initializing asset manager...";
        var assetManagerLogger = _loggerFactory.CreateLogger<AssetManager>();
        var assetManager = new AssetManager(GraphicsDevice, "Assets", assetManagerLogger);
        progress.Progress = 0.4f;

        // Step 4: Load sprite manifests (35% of total - largest step)
        progress.CurrentStep = "Loading sprite manifests...";
        await _spriteLoader.LoadAllSpritesAsync();
        progress.Progress = 0.75f;
        _loggerFactory
            .CreateLogger<PokeSharpGame>()
            .LogInformation("Sprite manifests loaded");

        // Step 5: Initialize core systems (10% of total)
        progress.CurrentStep = "Initializing game systems...";
        _gameInitializer.Initialize(GraphicsDevice);
        progress.Progress = 0.85f;

        // Step 6: Load map (10% of total)
        progress.CurrentStep = "Loading map...";
        await _mapInitializer.LoadMap("test-map");
        progress.Progress = 0.95f;

        // Step 7: Create player (5% of total)
        progress.CurrentStep = "Finalizing...";
        _playerFactory.CreatePlayer(10, 8, 
            _graphics.PreferredBackBufferWidth, 
            _graphics.PreferredBackBufferHeight);
        progress.Progress = 1.0f;
        progress.CurrentStep = "Complete!";

        // Create and return gameplay scene with all initialized components
        return new GameplayScene(
            Services,
            _world,
            _systemManager,
            _gameInitializer,
            _mapInitializer,
            // ... other dependencies
        );
    }
    catch (Exception ex)
    {
        progress.Error = ex;
        progress.IsComplete = true;
        throw;
    }
}
```

**Benefits of This Approach:**
- User sees immediate feedback (loading screen appears instantly)
- Progress is visible throughout initialization
- Game remains responsive (loading scene can animate)
- Clear separation of concerns (loading vs gameplay)
- Easy to add error handling and retry logic
- Can add loading tips or game information during load

### 7. Scene Transition Effects

Common transition patterns:
- **Fade**: Smooth alpha transition
- **Slide**: Horizontal/vertical slide
- **Instant**: Immediate switch (for testing)
- **Custom**: Scene-specific transitions

### 8. Scene Stack for Overlays

Support for scene stacking enables:
- Pause menus over gameplay
- Dialog boxes over any scene
- Settings menus
- Inventory screens

**Implementation:**
```csharp
public class SceneManager
{
    private readonly Stack<Scene> _sceneStack = new();
    
    public void PushScene(Scene scene) { }
    public void PopScene() { }
    public Scene? PeekScene() { }
}
```

## Recommended Architecture for PokeSharp

### Design Decision: Hybrid Approach

Given PokeSharp's current architecture, a hybrid approach is recommended:

1. **Shared Core Infrastructure:**
   - Single `World` instance (shared across scenes)
   - Core systems (rendering, input, asset management)
   - Shared services (logging, performance monitoring)

2. **Scene-Specific Systems:**
   - Each scene registers/unregisters its own systems
   - Systems can be enabled/disabled per scene
   - Scene-specific entity management

3. **Scene Lifecycle:**
   - `Initialize()` - Register scene systems
   - `LoadContent()` - Load scene assets
   - `Update()` - Update scene logic
   - `Draw()` - Render scene (or delegate to SystemManager)
   - `UnloadContent()` - Unload assets
   - `Dispose()` - Unregister systems, cleanup

### Implementation Structure

```
PokeSharp.Engine.Scenes/
├── IScene.cs                    # Scene interface
├── SceneBase.cs                 # Abstract base scene
├── SceneManager.cs              # Scene management
├── SceneTransition.cs           # Transition effects
├── LoadingProgress.cs           # Loading progress tracker
└── Scenes/
    ├── LoadingScene.cs          # Loading screen with progress
    ├── GameplayScene.cs         # Main gameplay
    ├── MenuScene.cs             # Main menu
    ├── PauseScene.cs            # Pause overlay
    └── BattleScene.cs           # Battle system (future)
```

### Integration Points

1. **PokeSharpGame.cs:**
   - Create `SceneManager` instance
   - Delegate `Update()` and `Draw()` to current scene
   - Handle scene transitions

2. **SystemManager:**
   - Support system registration/unregistration
   - Scene-aware system filtering
   - System enable/disable per scene

3. **World:**
   - Entity tagging with scene identifier
   - Scene-specific entity queries
   - Entity cleanup on scene change

## Key Considerations

### Memory Management
- Unload unused assets when switching scenes
- Dispose entities from previous scene
- Clear caches if needed

### Performance
- Minimize system overhead during transitions
- Batch entity cleanup operations
- Consider object pooling for frequently created/destroyed entities

### State Persistence
- Decide what state persists across scenes (player data, game flags)
- Use services for persistent state (already have `GameStateApiService`)
- Clear scene-specific state on transition

### Testing
- Easy to test individual scenes in isolation
- Mock scene transitions for unit tests
- Test scene lifecycle methods

## Migration Strategy

1. **Phase 1: Infrastructure**
   - Create `IScene` interface and `SceneBase` class
   - Implement `SceneManager`
   - Create `LoadingProgress` tracker class
   - Add to dependency injection

2. **Phase 2: Loading Scene**
   - Create `LoadingScene` with progress display
   - Refactor `PokeSharpGame.InitializeAsync()` to report progress
   - Implement progress reporting at each initialization step
   - Test loading scene displays correctly
   - Verify smooth transition to gameplay

3. **Phase 3: Extract Current Gameplay**
   - Create `GameplayScene` from current `PokeSharpGame` logic
   - Move initialization code to async method that returns `GameplayScene`
   - Move update/draw logic to scene
   - Test that gameplay still works after transition from loading scene

4. **Phase 4: Add Menu Scene**
   - Create basic `MenuScene`
   - Implement scene transitions
   - Add menu navigation
   - Add transition from menu to loading scene to gameplay

5. **Phase 5: Enhance**
   - Add pause menu overlay (scene stack)
   - Implement transition effects (fade, slide)
   - Enhance loading screen with animations
   - Add error handling and retry logic for failed loads

## References

- [MonoGame Scene Management Tutorial](https://docs.monogame.net/articles/tutorials/building_2d_games/17_scenes/)
- [Nez Framework Content Management](https://prime31.github.io/Nez/docs/features/ContentManagement/)
- [MonoGame Community Scene Patterns](https://community.monogame.net/t/minimizing-state-switches-with-large-scenes/7572)

## Next Steps

1. Review this research document
2. Decide on scene architecture (recommended: Hybrid)
3. Create implementation plan
4. Begin Phase 1 implementation

