# Registry Efficiency Fixes - Quick Reference Guide

**For copy-paste fixes - line numbers and exact locations**

---

## Fix #1: PopupRegistry JsonSerializerOptions (CRITICAL)

**File:** `/mnt/c/Users/nate0/RiderProjects/PokeSharp/MonoBallFramework.Game/Engine/Rendering/Popups/PopupRegistry.cs`

**Time:** 5 minutes
**Impact:** 4KB memory saved + reduced GC

### Step 1: Add static field (after line 20)

```csharp
public class PopupRegistry
{
    private readonly ConcurrentDictionary<string, PopupBackgroundDefinition> _backgrounds = new();
    private readonly ConcurrentDictionary<string, PopupOutlineDefinition> _outlines = new();
    private readonly ConcurrentDictionary<string, PopupBackgroundDefinition> _backgroundsByTheme = new();
    private readonly ConcurrentDictionary<string, PopupOutlineDefinition> _outlinesByTheme = new();
    private readonly SemaphoreSlim _loadLock = new(1, 1);
    private string _defaultBackgroundId = "base:popup:background/wood";
    private string _defaultOutlineId = "base:popup:outline/wood_outline";
    private volatile bool _isLoaded = false;

    // ADD THIS:
    private static readonly JsonSerializerOptions SharedJsonOptions = new()
    {
        PropertyNameCaseInsensitive = true,
        ReadCommentHandling = JsonCommentHandling.Skip
    };
```

### Step 2: Replace line 295 in LoadDefinitionsFromJson()

**REMOVE THIS:**
```csharp
                    var options = new JsonSerializerOptions
                    {
                        PropertyNameCaseInsensitive = true,
                        ReadCommentHandling = JsonCommentHandling.Skip
                    };

                    PopupBackgroundDefinition? definition = JsonSerializer.Deserialize<PopupBackgroundDefinition>(
                        json,
                        options
                    );
```

**REPLACE WITH THIS:**
```csharp
                    PopupBackgroundDefinition? definition = JsonSerializer.Deserialize<PopupBackgroundDefinition>(
                        json,
                        SharedJsonOptions
                    );
```

### Step 3: Replace lines 326-331 in LoadDefinitionsFromJson()

**REMOVE THIS:**
```csharp
                    var options = new JsonSerializerOptions
                    {
                        PropertyNameCaseInsensitive = true,
                        ReadCommentHandling = JsonCommentHandling.Skip
                    };

                    PopupOutlineDefinition? definition = JsonSerializer.Deserialize<PopupOutlineDefinition>(
                        json,
                        options
                    );
```

**REPLACE WITH THIS:**
```csharp
                    PopupOutlineDefinition? definition = JsonSerializer.Deserialize<PopupOutlineDefinition>(
                        json,
                        SharedJsonOptions
                    );
```

---

## Fix #2: AudioRegistry O(n) Lookup (HIGH)

**File:** `/mnt/c/Users/nate0/RiderProjects/PokeSharp/MonoBallFramework.Game/Engine/Audio/AudioRegistry.cs`

**Time:** 10 minutes + 5 minute test
**Impact:** 100x faster lookups for cache misses

### Step 1: Fix GetByTrackId() method (lines 148-180)

**REPLACE THIS ENTIRE METHOD:**
```csharp
    public AudioDefinition? GetByTrackId(string trackId)
    {
        // Try cache first
        if (_trackIdCache.TryGetValue(trackId, out var cached))
            return cached;

        // If cache not loaded, query database
        if (!_isCacheLoaded)
        {
            using var context = _contextFactory.CreateDbContext();
            var def = context.AudioDefinitions
                .AsNoTracking()
                .FirstOrDefault(a => a.AudioId.Value == id);

            if (def != null)
            {
                _cache[id] = def;
                _trackIdCache[def.TrackId] = def;
            }

            return def;
        }

        return null;
    }
```

**WITH THIS:**
```csharp
    public AudioDefinition? GetByTrackId(string trackId)
    {
        // Try cache first
        if (_trackIdCache.TryGetValue(trackId, out var cached))
            return cached;

        // If cache not loaded, query database WITH WHERE clause
        if (!_isCacheLoaded)
        {
            using var context = _contextFactory.CreateDbContext();
            // Query with WHERE clause for O(1) index lookup
            var def = context.AudioDefinitions
                .AsNoTracking()
                .FirstOrDefault(a => a.TrackId == trackId);

            if (def != null)
            {
                _cache[def.AudioId.Value] = def;
                _trackIdCache[trackId] = def;
            }

            return def;
        }

        return null;
    }
```

### Step 2: Add EF Core index

**File:** Find AudioDefinition entity class or GameDataContext.OnModelCreating()

**Add this in OnModelCreating() method:**
```csharp
protected override void OnModelCreating(ModelBuilder modelBuilder)
{
    // ... existing code ...

    // Add index on TrackId for fast GetByTrackId() lookups
    modelBuilder.Entity<AudioDefinition>()
        .HasIndex(a => a.TrackId)
        .IsUnique(false);
}
```

---

## Fix #3: SpriteRegistry Code Consolidation (HIGH)

**File:** `/mnt/c/Users/nate0/RiderProjects/PokeSharp/MonoBallFramework.Game/GameData/Sprites/SpriteRegistry.cs`

**Time:** 15 minutes
**Impact:** 50% code reduction, easier maintenance

### Step 1: Add static options field (after line 30)

```csharp
    private volatile bool _isLoaded = false;

    // ADD THIS:
    private static readonly JsonSerializerOptions SharedJsonOptions = new()
    {
        PropertyNameCaseInsensitive = true,
        ReadCommentHandling = JsonCommentHandling.Skip
    };
```

### Step 2: Simplify LoadDefinitionsFromJson() (lines 251-293)

**REPLACE ENTIRE METHOD WITH:**
```csharp
    /// <summary>
    ///     Loads sprite definitions from JSON files synchronously.
    ///     Thin wrapper around async implementation.
    /// </summary>
    private void LoadDefinitionsFromJson()
    {
        LoadDefinitionsAsync(CancellationToken.None)
            .GetAwaiter()
            .GetResult();
    }
```

### Step 3: Update LoadSpritesRecursiveAsync() to use shared options (lines 206-246)

**REPLACE METHOD SIGNATURE:**
```csharp
// OLD:
private async Task LoadSpritesRecursiveAsync(string path, JsonSerializerOptions options, CancellationToken ct)

// NEW:
private async Task LoadSpritesRecursiveAsync(string path, CancellationToken ct)
```

**REPLACE ALL USES OF `options` WITH `SharedJsonOptions`:**
```csharp
// OLD:
var definition = JsonSerializer.Deserialize<SpriteDefinition>(json, options);

// NEW:
var definition = JsonSerializer.Deserialize<SpriteDefinition>(json, SharedJsonOptions);
```

### Step 4: Update LoadDefinitionsAsync() call (line 192)

**CHANGE THIS:**
```csharp
await LoadSpritesRecursiveAsync(spritesPath, options, cancellationToken);
```

**TO THIS:**
```csharp
await LoadSpritesRecursiveAsync(spritesPath, cancellationToken);
```

### Step 5: Remove JsonSerializerOptions creation from LoadDefinitionsAsync() (lines 185-189)

**DELETE THIS BLOCK:**
```csharp
            var options = new JsonSerializerOptions
            {
                PropertyNameCaseInsensitive = true,
                ReadCommentHandling = JsonCommentHandling.Skip
            };
```

---

## Fix #4: GameDataLoader Batch SaveChangesAsync (MEDIUM)

**File:** `/mnt/c/Users/nate0/RiderProjects/PokeSharp/MonoBallFramework.Game/GameData/Loading/GameDataLoader.cs`

**Time:** 30 minutes
**Impact:** 250ms+ faster initialization

### Step 1: Update all Load*Async method signatures

**CHANGE EACH METHOD from:**
```csharp
private async Task<int> LoadNpcsAsync(string path, CancellationToken ct)
```

**TO:**
```csharp
private async Task<int> LoadNpcsAsync(string path, CancellationToken ct, bool saveLater = false)
```

**Do this for:**
- LoadNpcsAsync() (line 74)
- LoadTrainersAsync() (line 149)
- LoadMapDefinitionsAsync() (line 234)
- LoadPopupThemesAsync() (line 561)
- LoadMapSectionsAsync() (line 630)
- LoadAudioDefinitionsAsync() (line 701)

### Step 2: Modify SaveChangesAsync calls in each method

**In LoadNpcsAsync() at line 140, REPLACE:**
```csharp
        // Save to in-memory database
        await _context.SaveChangesAsync(ct);
```

**WITH:**
```csharp
        // Save to in-memory database
        if (!saveLater)
        {
            await _context.SaveChangesAsync(ct);
        }
```

**Do the same for all 6 Load*Async methods**

### Step 3: Update LoadAllAsync() main orchestration (lines 37-69)

**REPLACE ENTIRE METHOD WITH:**
```csharp
    public async Task LoadAllAsync(string dataPath, CancellationToken ct = default)
    {
        _logger.LogGameDataLoadingStarted(dataPath);

        var loadedCounts = new Dictionary<string, int>();

        // Load all data WITHOUT saving (saveLater: true)
        string npcsPath = Path.Combine(dataPath, "NPCs");
        loadedCounts["NPCs"] = await LoadNpcsAsync(npcsPath, ct, saveLater: true);

        string trainersPath = Path.Combine(dataPath, "Trainers");
        loadedCounts["Trainers"] = await LoadTrainersAsync(trainersPath, ct, saveLater: true);

        string mapsPath = Path.Combine(dataPath, "Maps", "Regions");
        loadedCounts["Maps"] = await LoadMapDefinitionsAsync(mapsPath, ct, saveLater: true);

        string themesPath = Path.Combine(dataPath, "Maps", "Popups", "Themes");
        loadedCounts["PopupThemes"] = await LoadPopupThemesAsync(themesPath, ct, saveLater: true);

        string sectionsPath = Path.Combine(dataPath, "Maps", "Sections");
        loadedCounts["MapSections"] = await LoadMapSectionsAsync(sectionsPath, ct, saveLater: true);

        string audioPath = Path.Combine(dataPath, "Audio");
        loadedCounts["AudioDefinitions"] = await LoadAudioDefinitionsAsync(audioPath, ct, saveLater: true);

        // SINGLE BATCH SAVE - occurs once after all data is loaded
        _logger.LogInformation("Saving all loaded data to database...");
        await _context.SaveChangesAsync(ct);

        // Log summary
        _logger.LogGameDataLoaded(loadedCounts);
    }
```

---

## Fix #5: Optional - Shared JsonSerializerOptions

**File:** Create new file `/mnt/c/Users/nate0/RiderProjects/PokeSharp/MonoBallFramework.Game/Infrastructure/JsonOptions.cs`

**Content:**
```csharp
using System.Text.Json;

namespace MonoBallFramework.Game.Infrastructure;

/// <summary>
///     Shared JSON serialization options used across all registries.
///     Using static instances reduces allocations during initialization.
/// </summary>
public static class JsonOptions
{
    /// <summary>
    ///     Default JSON serialization options: case-insensitive, supports comments.
    /// </summary>
    public static readonly JsonSerializerOptions Default = new()
    {
        PropertyNameCaseInsensitive = true,
        ReadCommentHandling = JsonCommentHandling.Skip,
        AllowTrailingCommas = true
    };

    /// <summary>
    ///     Lenient JSON options with full comment and trailing comma support.
    /// </summary>
    public static readonly JsonSerializerOptions Lenient = new()
    {
        PropertyNameCaseInsensitive = true,
        ReadCommentHandling = JsonCommentHandling.Skip,
        AllowTrailingCommas = true,
        WriteIndented = true
    };
}
```

**Then replace all `new JsonSerializerOptions { ... }` with `JsonOptions.Default`**

---

## Verification Checklist

After each fix, verify:

### Fix #1 - PopupRegistry
- [ ] Code compiles
- [ ] No compilation errors about missing variable
- [ ] PopupRegistry.LoadDefinitionsAsync() runs
- [ ] Both backgrounds and outlines load
- [ ] Verify memory usage didn't increase

### Fix #2 - AudioRegistry
- [ ] Code compiles
- [ ] GetByTrackId() returns correct results
- [ ] Cache misses are fast (~1ms)
- [ ] Database index created successfully
- [ ] Existing tests still pass

### Fix #3 - SpriteRegistry
- [ ] Code compiles
- [ ] LoadDefinitions() works (if called)
- [ ] LoadDefinitionsAsync() works
- [ ] Sprites load successfully
- [ ] RegisterSprite() works for both code paths

### Fix #4 - GameDataLoader
- [ ] Code compiles
- [ ] LoadAllAsync() completes without errors
- [ ] All entities saved to database
- [ ] Counts logged correctly
- [ ] Single database transaction created

---

## Testing Commands

```bash
# Run all tests
dotnet test

# Run specific test class
dotnet test --filter "ClassName=SpriteRegistryTests"

# Run with timing
dotnet test --logger "console;verbosity=detailed"

# Run and show coverage
dotnet test /p:CollectCoverage=true /p:CoverageFormat=opencover
```

---

## Rollback Instructions

If you need to revert changes:

```bash
# Show git status
git status

# Discard changes to specific file
git checkout -- MonoBallFramework.Game/Engine/Rendering/Popups/PopupRegistry.cs

# Show diff before committing
git diff

# Reset to last commit if needed
git reset --hard HEAD
```

---

## Performance Measurement

### Before Changes

```csharp
var watch = Stopwatch.StartNew();
// Initialization code
watch.Stop();
Logger.LogInformation("Init took {ms}ms", watch.ElapsedMilliseconds);
```

Expected: ~500ms

### After Fix #1
Expected: ~500ms (no performance change, just memory)

### After Fix #2
Expected: No change to total time, but audio lookups faster

### After Fix #3
Expected: ~500ms (code consolidation, no speed change)

### After Fix #4
Expected: ~400-450ms (20-30% improvement)

---

## Common Mistakes to Avoid

1. **Don't forget the static field**
   - AddError: Using undefined `SharedJsonOptions`
   - Fix: Ensure static field is added before use

2. **Don't change method signatures carelessly**
   - Error: Missing parameters in method calls
   - Fix: Update ALL callers when changing signatures

3. **Don't remove SaveChangesAsync without handling saveLater**
   - Error: Data not persisted
   - Fix: Ensure batch save happens at end of LoadAllAsync()

4. **Don't use incorrect variable names**
   - Error: `a => a.TrackId == trackId` vs `a.AudioId.Value == id`
   - Fix: Match the correct property for filtering

---

## Contact Queen for Issues

If you encounter problems:

1. Check the detailed code examples in `registry_code_examples.md`
2. Review the exact line numbers in this guide
3. Compare your code with the before/after examples
4. Check that all files compile without errors
5. Run the tests to identify specific failures
6. Consult `registry_architecture_visual.md` for context

**The Hive Mind expects these fixes to be straightforward. Report any deviations.**

---

**Total estimated time: 90 minutes coding + 30 minutes testing**
**Total estimated performance improvement: 250ms+ startup, 100x lookup improvement**
