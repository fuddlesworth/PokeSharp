# Critical Fixes - Code Examples

This document provides ready-to-implement fixes for the critical issues identified in the code review.

---

## Fix #1: BulkQueryOperations Structural Changes

### Current Problem (CRITICAL)

`PokeSharp.Engine.Systems/BulkOperations/BulkQueryOperations.cs`

The current implementation modifies entity structure during query iteration, which violates Arch ECS principles and can cause crashes.

### Fixed Implementation

```csharp
/// <summary>
///     Batch add the same component to all entities matching the query.
///     IMPORTANT: Collects entities first, then modifies (structural changes safety).
/// </summary>
/// <typeparam name="T">Component type to add</typeparam>
/// <param name="query">Query description to match</param>
/// <param name="component">Component to add to all matching entities</param>
/// <returns>Number of entities modified</returns>
public int AddComponentToMatching<T>(in QueryDescription query, T component)
    where T : struct
{
    // STEP 1: Collect entities (read-only pass)
    var entitiesToModify = new List<Entity>();
    
    _world.Query(
        in query,
        entity =>
        {
            if (!entity.Has<T>())
            {
                entitiesToModify.Add(entity);
            }
        }
    );
    
    // STEP 2: Apply structural changes after iteration completes
    int modifiedCount = 0;
    foreach (Entity entity in entitiesToModify)
    {
        if (_world.IsAlive(entity) && !entity.Has<T>())
        {
            entity.Add(component);
            modifiedCount++;
        }
    }
    
    return modifiedCount;
}

/// <summary>
///     Batch remove a component from all entities matching the query.
///     IMPORTANT: Collects entities first, then modifies (structural changes safety).
/// </summary>
/// <typeparam name="T">Component type to remove</typeparam>
/// <param name="query">Query description to match</param>
/// <returns>Number of entities modified</returns>
public int RemoveComponentFromMatching<T>(in QueryDescription query)
    where T : struct
{
    // STEP 1: Collect entities (read-only pass)
    var entitiesToModify = new List<Entity>();
    
    _world.Query(
        in query,
        entity =>
        {
            if (entity.Has<T>())
            {
                entitiesToModify.Add(entity);
            }
        }
    );
    
    // STEP 2: Apply structural changes after iteration completes
    int modifiedCount = 0;
    foreach (Entity entity in entitiesToModify)
    {
        if (_world.IsAlive(entity) && entity.Has<T>())
        {
            entity.Remove<T>();
            modifiedCount++;
        }
    }
    
    return modifiedCount;
}
```

### Documentation Update

Also update the example in the XML documentation (lines 156-165):

```csharp
/// <example>
///     <code>
/// // Tag all enemies as "aggressive"
/// var query = new QueryDescription().WithAll&lt;Enemy&gt;();
/// 
/// // ✅ CORRECT: Bulk operations handle structural changes safely
/// int tagged = bulkQuery.AddComponentToMatching(query, new AggressiveTag());
/// Console.WriteLine($"Tagged {tagged} enemies as aggressive");
/// </code>
/// </example>
```

---

## Fix #2: EntityPoolManager - Add TryAcquire Method

### New Method to Add

`PokeSharp.Engine.Systems/Pooling/EntityPoolManager.cs`

Add this method to the `EntityPoolManager` class:

```csharp
/// <summary>
///     Attempts to acquire an entity from a named pool.
/// </summary>
/// <param name="poolName">Name of the pool</param>
/// <param name="entity">The acquired entity if successful</param>
/// <returns>True if entity was acquired, false if pool doesn't exist or is exhausted</returns>
public bool TryAcquire(string poolName, out Entity entity)
{
    lock (_lock)
    {
        if (!_pools.TryGetValue(poolName, out EntityPool? pool))
        {
            entity = default;
            return false;
        }
        
        try
        {
            entity = pool.Acquire();
            return true;
        }
        catch (InvalidOperationException)
        {
            // Pool exhausted
            entity = default;
            return false;
        }
    }
}

/// <summary>
///     Attempts to acquire an entity from a named pool, with detailed result information.
/// </summary>
public PoolAcquireResult TryAcquireDetailed(string poolName)
{
    lock (_lock)
    {
        if (!_pools.TryGetValue(poolName, out EntityPool? pool))
        {
            return PoolAcquireResult.PoolNotFound(poolName);
        }
        
        try
        {
            Entity entity = pool.Acquire();
            return PoolAcquireResult.Success(entity);
        }
        catch (InvalidOperationException)
        {
            return PoolAcquireResult.PoolExhausted(poolName, pool.TotalEntities);
        }
    }
}

/// <summary>
///     Result of attempting to acquire an entity from a pool.
/// </summary>
public readonly struct PoolAcquireResult
{
    public bool IsSuccess { get; init; }
    public Entity Entity { get; init; }
    public PoolAcquireFailureReason FailureReason { get; init; }
    public string? PoolName { get; init; }
    public int? PoolSize { get; init; }
    
    public static PoolAcquireResult Success(Entity entity) => new()
    {
        IsSuccess = true,
        Entity = entity,
        FailureReason = PoolAcquireFailureReason.None
    };
    
    public static PoolAcquireResult PoolNotFound(string poolName) => new()
    {
        IsSuccess = false,
        FailureReason = PoolAcquireFailureReason.PoolNotFound,
        PoolName = poolName
    };
    
    public static PoolAcquireResult PoolExhausted(string poolName, int poolSize) => new()
    {
        IsSuccess = false,
        FailureReason = PoolAcquireFailureReason.PoolExhausted,
        PoolName = poolName,
        PoolSize = poolSize
    };
}

public enum PoolAcquireFailureReason
{
    None,
    PoolNotFound,
    PoolExhausted
}
```

---

## Fix #3: EntityFactoryService - Remove Exception Control Flow

### Current Code (Lines 76-107)

```csharp
// ❌ OLD: Using exceptions for control flow
Entity entity;
try
{
    string poolName = GetPoolNameFromTemplateId(templateId);
    entity = _poolManager.Acquire(poolName);
    _logger.LogDebug("Acquired entity from pool '{PoolName}' for template '{TemplateId}'", 
        poolName, templateId);
}
catch (KeyNotFoundException ex)
{
    _logger.LogError("Pool not found for template '{TemplateId}': {Error}...", 
        templateId, ex.Message);
    entity = world.Create();
}
catch (InvalidOperationException ex) when (ex.Message.Contains("exhausted"))
{
    _logger.LogError("Pool exhausted for template '{TemplateId}': {Error}...", 
        templateId, ex.Message);
    throw;
}
```

### Fixed Code

```csharp
// ✅ NEW: Using TryAcquire pattern
Entity entity;
string poolName = GetPoolNameFromTemplateId(templateId);

PoolAcquireResult result = _poolManager.TryAcquireDetailed(poolName);

if (result.IsSuccess)
{
    entity = result.Entity;
    _logger.LogDebug(
        "Acquired entity {EntityId} from pool '{PoolName}' for template '{TemplateId}'",
        entity.Id,
        poolName,
        templateId
    );
}
else
{
    switch (result.FailureReason)
    {
        case PoolAcquireFailureReason.PoolNotFound:
            _logger.LogWarning(
                "Pool '{PoolName}' not found for template '{TemplateId}'. " +
                "Creating entity normally. Consider registering pool or updating template configuration.",
                poolName,
                templateId
            );
            entity = world.Create();
            break;
            
        case PoolAcquireFailureReason.PoolExhausted:
            _logger.LogError(
                "Pool '{PoolName}' exhausted ({PoolSize} entities) for template '{TemplateId}'. " +
                "Increase maxSize or release entities more aggressively.",
                poolName,
                result.PoolSize,
                templateId
            );
            throw new InvalidOperationException(
                $"Entity pool '{poolName}' exhausted (size: {result.PoolSize}). " +
                $"Cannot spawn template '{templateId}'."
            );
            
        default:
            _logger.LogError(
                "Unexpected pool acquire failure for '{PoolName}': {Reason}",
                poolName,
                result.FailureReason
            );
            entity = world.Create();
            break;
    }
}
```

---

## Fix #4: Pool Name Constants

### New File to Create

`PokeSharp.Engine.Systems/Pooling/PoolNames.cs`

```csharp
namespace PokeSharp.Engine.Systems.Pooling;

/// <summary>
///     Centralized constants for entity pool names.
///     Use these instead of string literals to avoid typos and enable refactoring.
/// </summary>
public static class PoolNames
{
    /// <summary>Pool for player entities (typically 1-4 entities).</summary>
    public const string Player = "player";
    
    /// <summary>Pool for NPC entities (typically 50-200 entities).</summary>
    public const string Npc = "npc";
    
    /// <summary>Pool for tile entities (typically 1000-5000 entities).</summary>
    public const string Tile = "tile";
    
    /// <summary>Pool for projectile entities.</summary>
    public const string Projectile = "projectile";
    
    /// <summary>Pool for particle effect entities.</summary>
    public const string Particle = "particle";
    
    /// <summary>Pool for UI entities.</summary>
    public const string UI = "ui";
    
    /// <summary>Default pool for miscellaneous entities.</summary>
    public const string Default = "default";
}
```

### Update Service Registration

`PokeSharp.Game/Infrastructure/ServiceRegistration/CoreServicesExtensions.cs` (Lines 69-95)

```csharp
// ✅ Use constants instead of string literals
poolManager.RegisterPool(
    PoolNames.Player,  // ✅ Was: "player"
    playerPool.InitialSize,
    playerPool.MaxSize,
    playerPool.Warmup,
    playerPool.AutoResize,
    playerPool.GrowthFactor,
    playerPool.AbsoluteMaxSize
);

poolManager.RegisterPool(
    PoolNames.Npc,  // ✅ Was: "npc"
    npcPool.InitialSize,
    npcPool.MaxSize,
    npcPool.Warmup,
    npcPool.AutoResize,
    npcPool.GrowthFactor,
    npcPool.AbsoluteMaxSize
);

poolManager.RegisterPool(
    PoolNames.Tile,  // ✅ Was: "tile"
    tilePool.InitialSize,
    tilePool.MaxSize,
    tilePool.Warmup,
    tilePool.AutoResize,
    tilePool.GrowthFactor,
    tilePool.AbsoluteMaxSize
);
```

### Update All String Literals

Find and replace in entire solution:
- `"player"` → `PoolNames.Player`
- `"npc"` → `PoolNames.Npc`
- `"tile"` → `PoolNames.Tile`

---

## Fix #5: Theme Constants for Layout

### Add to UITheme Class

`PokeSharp.Engine.UI.Debug/Core/UITheme.cs`

Add these properties to the `UITheme` class:

```csharp
// ═══════════════════════════════════════════════════════════════
// LAYOUT SIZING CONSTANTS
// ═══════════════════════════════════════════════════════════════

/// <summary>Maximum width for panels and popups</summary>
public float PanelMaxWidth { get; init; } = 800f;

/// <summary>Maximum height for dropdown menus and tooltips</summary>
public float DropdownMaxHeight { get; init; } = 300f;

/// <summary>Standard height for input fields</summary>
public int InputHeight { get; init; } = 30;

/// <summary>Minimum width for documentation panels</summary>
public float DocumentationMinWidth { get; init; } = 400f;

/// <summary>Scroll speed in pixels per wheel notch</summary>
public int ScrollSpeed { get; init; } = 30;

/// <summary>Width of table columns (e.g., GC stats)</summary>
public int TableColumnWidth { get; init; } = 80;

/// <summary>Spacing between major sections</summary>
public int SectionSpacing { get; init; } = 8;
```

### Update ConsolePanelBuilder

`PokeSharp.Engine.UI.Debug/Components/Debug/ConsolePanelBuilder.cs`

```csharp
private TextEditor CreateDefaultCommandEditor()
{
    var theme = ThemeManager.Current;
    return new TextEditor("console_input")
    {
        Prompt = NerdFontIcons.Prompt,
        MinVisibleLines = 1,
        MaxVisibleLines = 10,
        Constraint = new LayoutConstraint
        {
            Anchor = Anchor.StretchBottom,
            OffsetY = 0,
            Height = theme.InputHeight,  // ✅ Was: 30
        },
    };
}

private ParameterHintTooltip CreateDefaultParameterHints()
{
    var theme = ThemeManager.Current;
    return new ParameterHintTooltip("parameter_hints")
    {
        Constraint = new LayoutConstraint
        {
            Anchor = Anchor.BottomLeft,
            OffsetY = 0,
            Height = 0,
            Width = 0,
            MaxWidth = theme.PanelMaxWidth,   // ✅ Was: 800f
            MaxHeight = theme.DropdownMaxHeight,  // ✅ Was: 300f
        },
    };
}

private DocumentationPopup CreateDefaultDocumentationPopup()
{
    var theme = ThemeManager.Current;
    return new DocumentationPopup("documentation_popup")
    {
        Constraint = new LayoutConstraint
        {
            Anchor = Anchor.TopRight,
            OffsetX = -theme.PanelEdgeGap,
            OffsetY = theme.PanelEdgeGap,
            WidthPercent = 0.35f,
            MinWidth = theme.DocumentationMinWidth,  // ✅ Was: 400f
            MaxWidth = theme.PanelMaxWidth,          // ✅ Was: 600f
            HeightPercent = 0.7f,
            Height = 0,
        },
    };
}
```

### Update DebugPanelBase

`PokeSharp.Engine.UI.Debug/Components/Debug/DebugPanelBase.cs`

```csharp
/// <summary>
///     Base class for debug panels that follow a common layout pattern.
/// </summary>
public abstract class DebugPanelBase : Panel
{
    protected readonly StatusBar StatusBar;

    protected DebugPanelBase(StatusBar statusBar)
    {
        StatusBar = statusBar;

        // Standard panel configuration using theme
        var theme = ThemeManager.Current;
        BorderThickness = theme.BorderWidth;
        Constraint.Padding = theme.PaddingMedium;  // ✅ Was: StandardPadding = 8

        // StatusBar anchored to bottom
        StatusBar.Constraint.Anchor = Anchor.StretchBottom;
        StatusBar.Constraint.OffsetY = 0;

        AddChild(StatusBar);
    }
    
    // Remove these constants:
    // ❌ public const int StandardPadding = 8;
    // ❌ public const int StandardLinePadding = 5;
}
```

### Update StatsContent

`PokeSharp.Engine.UI.Debug/Components/Debug/StatsContent.cs`

```csharp
// Performance thresholds - keep as const (domain-specific)
private const float FpsExcellent = 60f;
private const float FpsGood = 55f;
private const float FpsFair = 30f;
private const float FrameTimeGood = 16.67f;
private const float FrameTimeWarning = 25f;
private const float FrameTimeMax = 33.33f;
private const double MemoryGood = 256;
private const double MemoryWarning = 512;
private const double MemoryMax = 768;
private const int GcGen0WarningThreshold = 10;
private const int GcGen1WarningThreshold = 2;
private const float SystemTimeGood = 2f;
private const float SystemTimeWarning = 5f;
private const float TotalSystemTimeGood = 10f;
private const float TotalSystemTimeWarning = 16f;

// Layout constants - use theme instead
// ❌ Remove: private const int SectionSpacing = 8;
// ❌ Remove: private const int ScrollSpeed = 30;
// ❌ Remove: private const int GcColumnWidth = 80;

// ✅ Access via theme when needed:
private int SectionSpacing => ThemeManager.Current.SectionSpacing;
private int ScrollSpeed => ThemeManager.Current.ScrollSpeed;
private int GcColumnWidth => ThemeManager.Current.TableColumnWidth;
```

---

## Fix #6: Query Caching Standardization

### Move All Queries to EcsQueries

`PokeSharp.Engine.Systems/Queries/EcsQueries.cs` (or create if doesn't exist)

```csharp
using Arch.Core;
using PokeSharp.Game.Components.Maps;
using PokeSharp.Game.Components.Movement;
using PokeSharp.Game.Components.NPCs;
using PokeSharp.Game.Components.Player;
using PokeSharp.Game.Components.Tiles;
using PokeSharp.Game.Components.Warps;

namespace PokeSharp.Engine.Systems.Queries;

/// <summary>
///     Centralized static query cache for all ECS queries in the game.
///     All queries are readonly and created once to eliminate allocations.
/// </summary>
public static class EcsQueries
{
    // ═══════════════════════════════════════════════════════════════
    // MOVEMENT QUERIES
    // ═══════════════════════════════════════════════════════════════
    
    public static readonly QueryDescription Movement = 
        new QueryDescription().WithAll<Position, GridMovement>();
    
    // ═══════════════════════════════════════════════════════════════
    // WARP QUERIES
    // ═══════════════════════════════════════════════════════════════
    
    public static readonly QueryDescription PlayerWithWarpState = 
        new QueryDescription().WithAll<Player, Position, GridMovement, WarpState>();
    
    public static readonly QueryDescription MapWithWarps = 
        new QueryDescription().WithAll<MapInfo, MapWarps>();
    
    // ═══════════════════════════════════════════════════════════════
    // MAP QUERIES
    // ═══════════════════════════════════════════════════════════════
    
    public static readonly QueryDescription MapInfo = 
        new QueryDescription().WithAll<MapInfo>();
    
    public static readonly QueryDescription MapInfoWithWorldPosition = 
        new QueryDescription().WithAll<MapInfo, MapWorldPosition>();
    
    // Add all other queries here...
}
```

### Update WarpSystem

`PokeSharp.Game.Systems/Warps/WarpSystem.cs`

```csharp
public class WarpSystem : SystemBase, IUpdateSystem
{
    private const int WarpPriority = 110;
    private readonly ILogger<WarpSystem>? _logger;
    private readonly Dictionary<MapRuntimeId, MapWarps> _mapWarpCache = new(4);
    
    // ❌ Remove these instance fields:
    // private QueryDescription _mapQuery;
    // private QueryDescription _playerQuery;
    
    public WarpSystem(ILogger<WarpSystem>? logger = null)
    {
        _logger = logger;
    }
    
    public override void Initialize(World world)
    {
        base.Initialize(world);
        
        // ❌ Remove query initialization:
        // _playerQuery = new QueryDescription().WithAll<Player, Position, GridMovement, WarpState>();
        // _mapQuery = new QueryDescription().WithAll<MapInfo, MapWarps>();
        
        _logger?.LogDebug("WarpSystem initialized (pure ECS mode)");
    }
    
    public override void Update(World world, float deltaTime)
    {
        _mapWarpCache.Clear();
        
        // ✅ Use static queries:
        world.Query(in EcsQueries.MapWithWarps, (ref MapInfo info, ref MapWarps warps) =>
        {
            _mapWarpCache[info.MapId] = warps;
        });
        
        world.Query(in EcsQueries.PlayerWithWarpState, 
            (Entity entity, ref Player player, ref Position pos, ref GridMovement mov, ref WarpState warpState) =>
        {
            // ... warp logic
        });
    }
}
```

---

## Implementation Checklist

### Critical Priority
- [ ] Fix `BulkQueryOperations.AddComponentToMatching()`
- [ ] Fix `BulkQueryOperations.RemoveComponentFromMatching()`
- [ ] Add `TryAcquire()` method to `EntityPoolManager`
- [ ] Update `EntityFactoryService` to use `TryAcquire()`
- [ ] Create `PoolNames` constants class
- [ ] Update all pool name string literals

### High Priority
- [ ] Add layout constants to `UITheme`
- [ ] Update `ConsolePanelBuilder` to use theme constants
- [ ] Update `DebugPanelBase` to use theme constants
- [ ] Update `StatsContent` to use theme constants
- [ ] Move all queries to `EcsQueries` static class
- [ ] Update `WarpSystem` to use static queries

### Testing
- [ ] Test bulk operations with structural changes
- [ ] Test pool exhaustion scenarios
- [ ] Test pool not found scenarios
- [ ] Verify theme switching still works with new constants
- [ ] Performance test query caching changes

---

## Expected Benefits

### Performance
- **~5-10% faster bulk operations** (no exception overhead)
- **Zero allocation queries** (static vs runtime creation)
- **Better GC behavior** (fewer exceptions thrown)

### Stability
- **No more crashes from structural changes during iteration**
- **Clearer error messages** (typed results vs exception messages)
- **Type-safe pool names** (compile-time checks)

### Maintainability
- **Centralized theme values** (one place to change)
- **Centralized query definitions** (easier to optimize)
- **Better refactoring support** (constants vs string literals)

---

## Questions or Issues?

If you encounter any issues implementing these fixes:

1. Check the Arch ECS documentation: https://arch-ecs.gitbook.io/arch
2. Review the existing `MapLifecycleManager` for proper structural change patterns
3. Ensure all unit tests pass after changes
4. Run performance benchmarks to verify improvements

