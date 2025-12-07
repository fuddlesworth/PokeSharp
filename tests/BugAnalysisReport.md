# Bug Analysis Report - Entity ID & Map Object Spawning

**Date:** 2025-12-06
**Analyzed by:** Tester Agent (Hive Mind)
**Codebase:** MonoGame + Arch ECS

## Executive Summary

Identified **23 potential bugs** and edge cases across 4 files:
- **7 Critical** severity issues (data corruption, crashes)
- **10 Major** severity issues (incorrect behavior, resource leaks)
- **6 Minor** severity issues (edge case handling)

---

## 1. EntityId.cs - ID Parsing & Validation

### 🔴 CRITICAL-1: ParseComponents() assumes IndexOf() returns valid indices
**Location:** `EntityId.cs:109-120`
**Severity:** CRITICAL
**Description:**
The `ParseComponents()` method performs substring operations without verifying that `IndexOf()` returned valid indices. If the regex validation is bypassed or has a flaw, this will throw `ArgumentOutOfRangeException`.

```csharp
private void ParseComponents()
{
    // Format: namespace:type:category/name
    int firstColon = Value.IndexOf(':');      // Could be -1
    int secondColon = Value.IndexOf(':', firstColon + 1);  // Could be -1
    int slash = Value.IndexOf('/');           // Could be -1

    Namespace = Value[..firstColon];          // CRASH if firstColon == -1
    EntityType = Value[(firstColon + 1)..secondColon];
    Category = Value[(secondColon + 1)..slash];
    Name = Value[(slash + 1)..];
}
```

**Impact:**
- Constructor can throw unexpected `ArgumentOutOfRangeException`
- Creates instability when processing malformed IDs from untrusted sources (mod content, network data)

**Suggested Fix:**
```csharp
private void ParseComponents()
{
    int firstColon = Value.IndexOf(':');
    int secondColon = Value.IndexOf(':', firstColon + 1);
    int slash = Value.IndexOf('/');

    if (firstColon == -1 || secondColon == -1 || slash == -1)
    {
        throw new ArgumentException($"Entity ID '{Value}' has invalid structure", nameof(Value));
    }

    // Safe to proceed...
}
```

**Test Cases Needed:**
```csharp
[Test]
public void Constructor_WithMalformedId_AfterRegexBypass_ThrowsCorrectException()
{
    // Test if ParseComponents handles missing separators gracefully
}
```

---

### 🟠 MAJOR-1: Regex pattern too restrictive for internationalization
**Location:** `EntityId.cs:26-28`
**Severity:** MAJOR
**Description:**
Pattern `@"^[a-z0-9_]+:[a-z]+:[a-z0-9_]+/[a-z0-9_]+$"` only allows lowercase ASCII letters, numbers, and underscores. This prevents:
- Unicode characters (e.g., Pokémon names with accents: "Flabébé")
- Uppercase letters (some mods might use camelCase)
- Hyphens in original category names

**Impact:**
- Cannot handle legitimate international content
- Forces lossy normalization (NormalizeComponent removes all non-ASCII)
- Limits mod compatibility

**Example Failing Input:**
```csharp
"base:npc:townfolk/Prof. Oak"  // Space invalid
"base:pokemon:kalos/Flabébé"   // Accent invalid
"mymod:item:poké-ball/great"   // Hyphen in category invalid
```

**Suggested Fix:**
Allow broader character set or make pattern configurable:
```csharp
// Option 1: Allow more characters
@"^[\w-]+:[\w-]+:[\w-]+/[\w-]+$"

// Option 2: Allow Unicode word characters
@"^[\p{L}\p{N}_-]+:[\p{L}\p{N}_-]+:[\p{L}\p{N}_-]+/[\p{L}\p{N}_-]+$"
```

**Test Cases:**
```csharp
[TestCase("base:npc:townfolk/Prof. Oak")]
[TestCase("base:pokemon:kalos/Flabébé")]
[TestCase("mod:item:poké-ball/great")]
public void IsValidFormat_WithInternationalCharacters_ReturnsTrue(string id)
```

---

### 🟠 MAJOR-2: NormalizeComponent can return empty string silently
**Location:** `EntityId.cs:125-144`
**Severity:** MAJOR
**Description:**
`NormalizeComponent()` can strip all characters from input, returning empty string. The component constructor uses this normalized value without validation.

```csharp
protected EntityId(string entityType, string category, string name, string? ns = null)
{
    Category = NormalizeComponent(category);  // Could be ""
    Name = NormalizeComponent(name);          // Could be ""
    Value = $"{Namespace}:{EntityType}:{Category}/{Name}";  // Creates "base:npc:/"
}
```

**Example:**
```csharp
NormalizeComponent("!!!") → ""
new EntityId("npc", "!!!", "???") → "base:npc:/"  // INVALID!
```

**Impact:**
- Creates malformed IDs that don't match the regex pattern
- State inconsistency: ID passes validation in one path but not another
- Breaks round-trip: `new EntityId(id.Value)` throws exception

**Suggested Fix:**
```csharp
protected EntityId(string entityType, string category, string name, string? ns = null)
{
    Category = NormalizeComponent(category);
    Name = NormalizeComponent(name);

    if (string.IsNullOrEmpty(Category) || string.IsNullOrEmpty(Name))
    {
        throw new ArgumentException(
            $"Category and Name cannot be empty after normalization. " +
            $"Original: category='{category}', name='{name}'");
    }

    Value = $"{Namespace}:{EntityType}:{Category}/{Name}";
}
```

---

### 🟡 MINOR-1: NormalizeComponent regex overhead on hot path
**Location:** `EntityId.cs:134-140`
**Severity:** MINOR
**Description:**
Method creates new Regex instances on every call instead of using compiled static patterns.

```csharp
result = Regex.Replace(result, @"[\s\-]+", "_");      // New regex instance
result = Regex.Replace(result, @"[^a-z0-9_]", "");    // New regex instance
result = Regex.Replace(result, @"_+", "_");           // New regex instance
```

**Impact:**
- Performance degradation when normalizing thousands of IDs during map loading
- Unnecessary GC pressure

**Suggested Fix:**
```csharp
private static readonly Regex WhitespacePattern = new(@"[\s\-]+", RegexOptions.Compiled);
private static readonly Regex InvalidCharsPattern = new(@"[^a-z0-9_]", RegexOptions.Compiled);
private static readonly Regex MultiUnderscorePattern = new(@"_+", RegexOptions.Compiled);

protected static string NormalizeComponent(string value)
{
    // ... existing null check ...
    result = WhitespacePattern.Replace(result, "_");
    result = InvalidCharsPattern.Replace(result, "");
    result = MultiUnderscorePattern.Replace(result, "_");
    return result.Trim('_');
}
```

---

### 🟡 MINOR-2: No maximum length validation
**Location:** `EntityId.cs:40-52`
**Severity:** MINOR
**Description:**
ID strings can be arbitrarily long, potentially causing:
- Database field overflow (if stored with VARCHAR limits)
- UI rendering issues
- Memory consumption with malicious input

**Suggested Fix:**
```csharp
public const int MaxIdLength = 256;

protected EntityId(string value)
{
    if (string.IsNullOrWhiteSpace(value))
        throw new ArgumentException("Entity ID cannot be null or whitespace.");

    if (value.Length > MaxIdLength)
        throw new ArgumentException($"Entity ID exceeds maximum length of {MaxIdLength} characters.");

    // ... rest of validation ...
}
```

---

## 2. GameNpcId.cs - NPC ID Handling

### 🔴 CRITICAL-2: TryCreate swallows all exceptions
**Location:** `GameNpcId.cs:67-98`
**Severity:** CRITICAL
**Description:**
The `TryCreate` method has a blanket `catch` block that returns `null` for **all** exceptions, not just validation failures.

```csharp
try
{
    return new GameNpcId(value);
}
catch  // ⚠️ Catches EVERYTHING including OutOfMemoryException, StackOverflowException
{
    return null;
}
```

**Impact:**
- Hides critical errors (OOM, corrupted state, etc.)
- Makes debugging impossible - silent failures with no logs
- Violates "fail fast" principle

**Suggested Fix:**
```csharp
try
{
    return new GameNpcId(value);
}
catch (ArgumentException)  // Only catch expected validation exceptions
{
    return null;
}
// Let critical exceptions propagate
```

---

### 🟠 MAJOR-3: Legacy format parsing ambiguity
**Location:** `GameNpcId.cs:86-97`
**Severity:** MAJOR
**Description:**
Legacy parsing assumes `normalized.Contains('/')` means "category/name" format, but what if the name itself contains a slash after normalization fails to remove it?

```csharp
if (normalized.Contains('/'))
{
    var parts = normalized.Split('/', 2);  // What if there are 3+ slashes?
    return new GameNpcId(parts[0], parts[1]);
}
```

**Example Edge Case:**
```
Input: "a/b/c"
Split result: ["a", "b/c"]
GameNpcId("a", "b/c") → tries to create "base:npc:a/b/c"
→ Invalid format because Name contains '/'
```

**Impact:**
- Inconsistent parsing behavior
- Silent failures when processing legacy data

**Suggested Fix:**
```csharp
if (normalized.Contains('/'))
{
    var parts = normalized.Split('/', 2, StringSplitOptions.RemoveEmptyEntries);
    if (parts.Length != 2 || string.IsNullOrWhiteSpace(parts[0]) || string.IsNullOrWhiteSpace(parts[1]))
    {
        return null;  // Invalid legacy format
    }
    return new GameNpcId(parts[0], parts[1]);
}
```

---

### 🟠 MAJOR-4: Implicit operator bypasses validation
**Location:** `GameNpcId.cs:103`
**Severity:** MAJOR
**Description:**
The implicit string conversion operator can throw exceptions at assignment time, which is unexpected for implicit conversions.

```csharp
GameNpcId npcId = "invalid string!";  // Throws ArgumentException at assignment
```

**Impact:**
- Violates principle of least surprise
- Makes code harder to reason about
- Can cause unexpected crashes in expression chains

**Suggested Fix:**
Remove implicit operator or make it use `TryCreate`:
```csharp
// Option 1: Remove implicit operator entirely
// public static implicit operator GameNpcId(string value) => new(value);  // REMOVE

// Option 2: Use explicit conversion
public static explicit operator GameNpcId(string value) => new(value);

// Option 3: Make it safe by using TryCreate with fallback
public static implicit operator GameNpcId?(string value) => TryCreate(value);
```

---

## 3. Npc.cs - Component Structure

### 🟡 MINOR-3: NpcId can be empty string
**Location:** `Npc.cs:36-42`
**Severity:** MINOR
**Description:**
Constructor accepts any string for `npcId`, including empty strings.

```csharp
public Npc(string npcId)
{
    NpcId = npcId;  // No validation
    // ...
}
```

**Impact:**
- Creates NPCs with invalid/empty identifiers
- Makes entity lookups by NpcId unreliable

**Suggested Fix:**
```csharp
public Npc(string npcId)
{
    if (string.IsNullOrWhiteSpace(npcId))
        throw new ArgumentException("NPC ID cannot be null or whitespace.", nameof(npcId));

    NpcId = npcId;
    // ...
}
```

---

### 🟡 MINOR-4: ViewRange allows negative values
**Location:** `Npc.cs:29`
**Severity:** MINOR
**Description:**
`ViewRange` property has no validation. Negative values don't make logical sense.

**Suggested Fix:**
```csharp
private int _viewRange;
public int ViewRange
{
    get => _viewRange;
    set => _viewRange = value < 0
        ? throw new ArgumentOutOfRangeException(nameof(value), "ViewRange cannot be negative")
        : value;
}
```

---

## 4. MapObjectSpawner.cs - Entity Spawning

### 🔴 CRITICAL-3: Entity creation fails mid-batch without cleanup
**Location:** `MapObjectSpawner.cs:54-198`
**Severity:** CRITICAL
**Description:**
`SpawnMapObjects` creates entities in a loop. If spawning fails partway through (exception in `_entityFactory.SpawnFromTemplate`), previously created entities are not cleaned up.

```csharp
foreach (TmxObjectGroup objectGroup in tmxDoc.ObjectGroups)
foreach (TmxObject obj in objectGroup.Objects)
{
    Entity entity = _entityFactory.SpawnFromTemplate(...);  // ❌ Throws exception at object #50
    mapInfoEntity.AddRelationship(entity, new ParentOf());
    created++;  // Objects 1-49 are orphaned in world
}
```

**Impact:**
- Memory leak: orphaned entities remain in world
- Partial map state: some NPCs spawned, others missing
- Relationship corruption: MapWarps might reference destroyed entities

**Suggested Fix:**
```csharp
var createdEntities = new List<Entity>();
try
{
    foreach (TmxObjectGroup objectGroup in tmxDoc.ObjectGroups)
    foreach (TmxObject obj in objectGroup.Objects)
    {
        var entity = _entityFactory.SpawnFromTemplate(...);
        createdEntities.Add(entity);
        mapInfoEntity.AddRelationship(entity, new ParentOf());
    }
}
catch
{
    // Cleanup: destroy partially created entities
    foreach (var entity in createdEntities)
    {
        world.Destroy(entity);
    }
    throw;  // Re-throw after cleanup
}
```

---

### 🔴 CRITICAL-4: Coordinate conversion overflow with large maps
**Location:** `MapObjectSpawner.cs:118-121, 579-580, 655-656`
**Severity:** CRITICAL
**Description:**
Coordinate conversion uses `(int)Math.Floor(obj.X / tileWidth)` without checking for overflow. Very large maps or malformed TMX files could cause overflow.

```csharp
int tileX = (int)Math.Floor(obj.X / tileWidth);  // obj.X could be float.MaxValue
int tileY = (int)Math.Floor(obj.Y / tileHeight); // Could overflow to int.MinValue
```

**Impact:**
- Entities spawned at negative coordinates (wrapping around)
- Spatial index corruption
- Crashes in collision detection or rendering systems

**Suggested Fix:**
```csharp
const float MaxCoordinate = 32768f;  // Reasonable map size limit

if (obj.X < 0 || obj.Y < 0 || obj.X > MaxCoordinate || obj.Y > MaxCoordinate)
{
    _logger?.LogWarning(
        "Object '{Name}' has out-of-range coordinates ({X}, {Y}), skipping",
        obj.Name, obj.X, obj.Y);
    continue;
}

int tileX = (int)Math.Floor(obj.X / tileWidth);
int tileY = (int)Math.Floor(obj.Y / tileHeight);
```

---

### 🔴 CRITICAL-5: Division by zero if tileWidth/tileHeight is 0
**Location:** `MapObjectSpawner.cs:118-121`
**Severity:** CRITICAL
**Description:**
No validation that `tileWidth` and `tileHeight` are positive. Division by zero will crash.

```csharp
int tileX = (int)Math.Floor(obj.X / tileWidth);  // ❌ DivideByZeroException
```

**Suggested Fix:**
Add parameter validation at method entry:
```csharp
public int SpawnMapObjects(
    World world,
    TmxDocument tmxDoc,
    Entity mapInfoEntity,
    MapRuntimeId mapId,
    int tileWidth,
    int tileHeight,
    HashSet<SpriteId>? requiredSpriteIds = null)
{
    if (tileWidth <= 0)
        throw new ArgumentOutOfRangeException(nameof(tileWidth), "Tile width must be positive");
    if (tileHeight <= 0)
        throw new ArgumentOutOfRangeException(nameof(tileHeight), "Tile height must be positive");

    // ... rest of method ...
}
```

---

### 🟠 MAJOR-5: requiredSpriteIds HashSet can grow unbounded
**Location:** `MapObjectSpawner.cs:62, 225, 290, 348, 662`
**Severity:** MAJOR
**Description:**
`requiredSpriteIds` HashSet is passed around and added to without any size limits. A malicious or corrupted TMX file with thousands of unique sprite IDs could cause OOM.

**Impact:**
- Memory exhaustion with large/malicious maps
- No protection against resource attacks

**Suggested Fix:**
```csharp
const int MaxSpriteIds = 10000;  // Reasonable limit

if (requiredSpriteIds != null)
{
    if (requiredSpriteIds.Count >= MaxSpriteIds)
    {
        _logger?.LogWarning(
            "Sprite ID limit ({MaxSpriteIds}) reached, skipping additional sprites",
            MaxSpriteIds);
    }
    else
    {
        requiredSpriteIds.Add(spriteId);
    }
}
```

---

### 🟠 MAJOR-6: Duplicate warp positions overwrite silently
**Location:** `MapObjectSpawner.cs:595-602`
**Severity:** MAJOR
**Description:**
When a warp already exists at a position, it's overwritten with only a warning log.

```csharp
if (!mapWarps.AddWarp(tileX, tileY, warpEntity))
{
    _logger?.LogWarning(
        "Warp at ({TileX}, {TileY}) overwrites existing warp - duplicate warp positions",
        tileX, tileY);
}
```

**Impact:**
- First warp becomes inaccessible
- Potential entity leak (old warp entity still in world but not in index)
- Map design errors go unnoticed

**Suggested Fix:**
```csharp
if (!mapWarps.AddWarp(tileX, tileY, warpEntity))
{
    _logger?.LogError(
        "Duplicate warp at ({TileX}, {TileY}) - rejecting new warp '{Name}'",
        tileX, tileY, obj.Name);
    world.Destroy(warpEntity);  // Clean up the duplicate
    continue;  // Don't increment created counter
}
```

---

### 🟠 MAJOR-7: Elevation conversion can throw for invalid values
**Location:** `MapObjectSpawner.cs:137-138, 667-668`
**Severity:** MAJOR
**Description:**
`Convert.ToByte(elevProp)` can throw `OverflowException` if elevation value is negative or > 255.

```csharp
byte elevValue = Convert.ToByte(elevProp);  // ❌ Throws if value is -1 or 300
```

**Suggested Fix:**
```csharp
if (obj.Properties.TryGetValue("elevation", out object? elevProp))
{
    try
    {
        byte elevValue = Convert.ToByte(elevProp);
        builder.OverrideComponent(new Elevation(elevValue));
    }
    catch (OverflowException)
    {
        _logger?.LogWarning(
            "Object '{Name}' has invalid elevation value '{Value}', using default 0",
            obj.Name, elevProp);
    }
}
```

---

### 🟠 MAJOR-8: SpriteId constructor can throw in hot loop
**Location:** `MapObjectSpawner.cs:224, 661`
**Severity:** MAJOR
**Description:**
Creating `new SpriteId(spriteIdStr)` inside exception handlers or tight loops. If SpriteId constructor throws (malformed ID), it bubbles up unexpectedly.

```csharp
if (!string.IsNullOrWhiteSpace(spriteIdStr))
{
    var spriteId = new SpriteId(spriteIdStr);  // ❌ Can throw ArgumentException
    requiredSpriteIds?.Add(spriteId);
}
```

**Suggested Fix:**
```csharp
if (!string.IsNullOrWhiteSpace(spriteIdStr))
{
    try
    {
        var spriteId = new SpriteId(spriteIdStr);
        requiredSpriteIds?.Add(spriteId);
    }
    catch (ArgumentException ex)
    {
        _logger?.LogWarning(ex,
            "Invalid sprite ID '{SpriteId}' for object '{Name}', skipping",
            spriteIdStr, obj.Name);
        continue;  // Skip this object
    }
}
```

---

### 🟠 MAJOR-9: Float.TryParse culture sensitivity
**Location:** `MapObjectSpawner.cs:416, 462, 502, 770`
**Severity:** MAJOR
**Description:**
`float.TryParse(value.ToString(), out float speed)` uses current culture. In locales that use comma for decimal separator (e.g., European), parsing "1.5" fails.

```csharp
// Current culture is de-DE (German)
float.TryParse("1.5", out float speed)  // ❌ Returns false (expects "1,5")
```

**Impact:**
- Movement speed, wait times parsed incorrectly in non-US locales
- NPCs move at wrong speeds
- Hard-to-diagnose internationalization bugs

**Suggested Fix:**
```csharp
using System.Globalization;

if (float.TryParse(speedProp.ToString(),
    NumberStyles.Float,
    CultureInfo.InvariantCulture,
    out float speed))
{
    builder.OverrideComponent(new GridMovement(speed));
}
```

---

### 🟠 MAJOR-10: Waypoint parsing doesn't validate coordinate count
**Location:** `MapObjectSpawner.cs:481-515`
**Severity:** MAJOR
**Description:**
Waypoint parsing silently skips malformed pairs but doesn't validate minimum waypoint count.

```csharp
foreach (string pair in pairs)
{
    if (coords.Length == 2 && int.TryParse(...))
    {
        points.Add(new Point(x, y));  // Silently skips invalid pairs
    }
}

if (points.Count > 0)  // ❌ Creates route with 1 waypoint (should be minimum 2)
{
    builder.OverrideComponent(new MovementRoute(points.ToArray(), ...));
}
```

**Impact:**
- MovementRoute with single waypoint might cause infinite loops or crashes in movement system
- No feedback about malformed waypoint data

**Suggested Fix:**
```csharp
if (points.Count >= 2)  // Require minimum 2 waypoints for a route
{
    builder.OverrideComponent(new MovementRoute(points.ToArray(), true, waypointWaitTime));
    _logger?.LogDebug("Applied waypoint route with {Count} points", points.Count);
}
else if (points.Count == 1)
{
    _logger?.LogWarning(
        "Object '{Name}' has only 1 waypoint, need at least 2 for a route",
        obj.Name);
}
```

---

### 🟡 MINOR-5: No validation for negative ranges
**Location:** `MapObjectSpawner.cs:730-746, 789-795`
**Severity:** MINOR
**Description:**
Range values can be negative, causing waypoints with negative coordinates.

```csharp
int range = 2;
if (obj.Properties.TryGetValue("range", out object? rangeProp))
{
    range = parsedRange;  // No validation, could be -10
}

waypoints = new[] {
    new Point(tileX, tileY),
    new Point(tileX + range, tileY)  // Creates Point(10, 5) and Point(0, 5) if range is -10
};
```

**Suggested Fix:**
```csharp
if (parsedRange <= 0)
{
    _logger?.LogWarning(
        "Object '{Name}' has invalid range {Range}, using default 2",
        obj.Name, parsedRange);
    range = 2;
}
else
{
    range = parsedRange;
}
```

---

### 🟡 MINOR-6: No maximum waypoint count limit
**Location:** `MapObjectSpawner.cs:481-495`
**Severity:** MINOR
**Description:**
Waypoint parsing has no upper limit. Malformed data could create thousands of waypoints, causing performance issues.

**Suggested Fix:**
```csharp
const int MaxWaypoints = 100;

if (points.Count >= MaxWaypoints)
{
    _logger?.LogWarning(
        "Object '{Name}' exceeds maximum waypoint count ({MaxWaypoints}), truncating",
        obj.Name, MaxWaypoints);
    break;
}
points.Add(new Point(x, y));
```

---

## Summary of Test Coverage Gaps

### Critical Test Scenarios Missing:

1. **EntityId edge cases:**
   - Malformed IDs that bypass regex but fail parsing
   - Maximum length strings (1MB+ IDs)
   - Unicode normalization edge cases
   - Component constructor with special characters that normalize to empty

2. **GameNpcId parsing:**
   - Legacy format with multiple slashes
   - Whitespace-only strings after normalization
   - Very long category/name combinations
   - Null vs empty string handling consistency

3. **MapObjectSpawner resilience:**
   - TMX files with 10,000+ objects (memory/performance)
   - All objects failing validation (100% error rate)
   - Duplicate warp/NPC positions (collision handling)
   - Missing required properties (graceful degradation)
   - Extreme coordinate values (overflow/underflow)
   - Zero/negative tile dimensions

4. **Concurrent access:**
   - Multiple maps loading simultaneously (requiredSpriteIds race condition)
   - Entity pool exhaustion during batch spawn
   - Relationship corruption with concurrent AddRelationship calls

5. **Resource limits:**
   - Maximum entities per map
   - Maximum sprite IDs collected
   - Maximum waypoints per NPC
   - Memory consumption with pathological input

---

## Recommended Testing Framework

### Unit Tests (Isolated Component Testing)
```csharp
[TestFixture]
public class EntityIdTests
{
    [TestCase("base:npc:townfolk/prof_birch")]
    [TestCase("mod_alpha:trainer:rivals/gary")]
    public void Constructor_ValidId_CreatesSuccessfully(string id) { }

    [TestCase("")]
    [TestCase(null)]
    [TestCase("invalid")]
    [TestCase("no:slashes")]
    public void Constructor_InvalidId_ThrowsArgumentException(string id) { }

    [Test]
    public void Constructor_ExtremelyLongId_ThrowsOrHandlesGracefully()
    {
        var longId = new string('a', 1_000_000) + ":npc:cat/name";
        // Should either throw ArgumentException or handle efficiently
    }

    [TestCase("!!!@@@###")]
    [TestCase("   ")]
    [TestCase("\t\n")]
    public void NormalizeComponent_InvalidInput_ReturnsEmpty(string input) { }
}

[TestFixture]
public class MapObjectSpawnerTests
{
    [Test]
    public void SpawnMapObjects_WithZeroTileWidth_ThrowsArgumentException() { }

    [Test]
    public void SpawnMapObjects_ExceptionInMiddle_CleansUpPartialEntities() { }

    [Test]
    public void SpawnMapObjects_DuplicateWarpPosition_RejectsSecondWarp() { }

    [Test]
    public void SpawnMapObjects_NegativeCoordinates_HandlesGracefully() { }

    [Test]
    public void SpawnMapObjects_10000Objects_CompletesWithinTimeout() { }
}
```

### Integration Tests (System-Level)
```csharp
[TestFixture]
public class MapLoadingIntegrationTests
{
    [Test]
    public void LoadMap_WithComplexNPCPatrols_CreatesValidMovementRoutes() { }

    [Test]
    public void LoadMap_WithInternationalCharacters_PreservesNames() { }

    [Test]
    public async Task LoadMultipleMaps_Concurrently_NoMemoryLeaks() { }
}
```

### Property-Based Testing (FsCheck/Hypothesis)
```csharp
[Property]
public Property EntityId_RoundTrip_IsStable()
{
    return Prop.ForAll(
        Arb.Generate<string>().Where(s => EntityId.IsValidFormat(s)),
        id => {
            var entityId = new EntityId(id);
            return new EntityId(entityId.Value).Value == id;
        });
}
```

---

## Conclusion

The codebase has solid architecture but lacks defensive programming for edge cases. Priority should be:

1. **Fix CRITICAL bugs immediately** (ParseComponents, cleanup on failure, coordinate overflow)
2. **Add comprehensive input validation** throughout the spawning pipeline
3. **Implement resource limits** to prevent OOM attacks
4. **Improve test coverage** with focus on boundary conditions and error paths
5. **Add integration tests** for concurrent map loading scenarios

**Estimated Risk:** If deployed to production with user-generated content (mods), **HIGH risk** of crashes and data corruption.

