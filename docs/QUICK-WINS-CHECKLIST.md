# ECS Optimization Quick Wins Checklist

## 🎯 Top 3 Immediate Actions (2-3 Hours, 15-20% Gain)

### 1. Replace Dynamic List Allocations (30 min)

**Problem:** Allocating new lists every frame/call
```csharp
// ❌ BAD - Allocates every call
public override List<Type> GetReadComponents()
{
    return new List<Type> { typeof(Position), typeof(GridMovement) };
}
```

**Fix:** Use static readonly
```csharp
// ✅ GOOD - Zero allocation
private static readonly List<Type> _readComponents = new()
{
    typeof(Position), typeof(GridMovement)
};

public override List<Type> GetReadComponents() => _readComponents;
```

**Files to Fix:**
- [ ] `MovementSystem.cs` (Lines 458, 472)
- [ ] `PathfindingSystem.cs` (Line 211)
- [ ] `SystemManager.cs` (Lines 204, 290)

**Expected Gain:** 8-12% GC reduction

---

### 2. Implement CommandBuffer (1-2 hours)

**Problem:** Deferred structural changes require intermediate lists
```csharp
// ❌ BAD - Allocates list, two loops
var entitiesToFix = new List<Entity>();
world.Query(query, entity => entitiesToFix.Add(entity));
foreach (var entity in entitiesToFix)
    entity.Remove<Component>();
```

**Fix:** Use CommandBuffer
```csharp
// ✅ GOOD - Zero allocation, batched
var buffer = new CommandBuffer();
world.Query(query, entity => buffer.Remove<Component>(entity));
buffer.Execute(); // All changes at once
```

**Implementation:**
```csharp
public class CommandBuffer
{
    private readonly List<Action> _commands = new();

    public void Remove<T>(Entity entity) where T : struct
    {
        _commands.Add(() => entity.Remove<T>());
    }

    public void Add<T>(Entity entity, T component) where T : struct
    {
        _commands.Add(() => entity.Add(component));
    }

    public void Set<T>(Entity entity, T component) where T : struct
    {
        _commands.Add(() => entity.Set(component));
    }

    public void Execute()
    {
        foreach (var command in _commands)
            command();
        _commands.Clear();
    }
}
```

**Files to Refactor:**
- [ ] `RelationshipSystem.cs` (Lines 108, 170, 197)
- [ ] `PathfindingSystem.cs` (MovementRequest modifications)
- [ ] `MovementSystem.cs` (Line 236 - MovementRequest cleanup)

**Expected Gain:** 5-10% performance improvement

---

### 3. Add Struct Packing (30 min)

**Problem:** Components have unnecessary padding
```csharp
// ❌ BAD - 20 bytes + 4 padding = 24 bytes
public struct Position
{
    public int X;        // 4 bytes
    public int Y;        // 4 bytes
    public float PixelX; // 4 bytes
    public float PixelY; // 4 bytes
    public int MapId;    // 4 bytes
    // Padding: 4 bytes on 64-bit
}
```

**Fix:** Explicit packing
```csharp
// ✅ GOOD - 20 bytes, no padding
[StructLayout(LayoutKind.Sequential, Pack = 4)]
public struct Position
{
    public int X;        // 4 bytes
    public int Y;        // 4 bytes
    public int MapId;    // 4 bytes (group ints together)
    public float PixelX; // 4 bytes
    public float PixelY; // 4 bytes
}
```

**Components to Pack:**
- [ ] `Position` (Core/Components/Movement/)
- [ ] `GridMovement` (Core/Components/Movement/)
- [ ] `TilePosition` (Core/Components/Tiles/)
- [ ] `MovementRoute` (Core/Components/NPCs/)

**Expected Gain:** 2-4% query performance

---

## 🔧 Implementation Steps

### Step 1: Establish Baseline (15 min)
```bash
# Run benchmark suite
dotnet run --project PokeSharp.Game --configuration Release

# Record metrics:
- Average frame time: ___ ms
- GC Gen0 collections per minute: ___
- MovementSystem average time: ___ ms
- RelationshipSystem average time: ___ ms
```

### Step 2: Fix Allocations (30 min)
1. Replace all `new List<Type>` in GetReadComponents/GetWriteComponents
2. Replace `ToArray()` calls with direct array assignment
3. Use IReadOnlyList/IReadOnlyDictionary for read-only collections

### Step 3: CommandBuffer (90 min)
1. Create `CommandBuffer.cs` in Core/Systems/
2. Add unit tests
3. Refactor RelationshipSystem
4. Refactor PathfindingSystem
5. Refactor MovementSystem

### Step 4: Struct Packing (30 min)
1. Add `using System.Runtime.InteropServices;`
2. Add `[StructLayout(LayoutKind.Sequential, Pack = 4)]` to components
3. Group fields by size (ints together, floats together)

### Step 5: Validate (15 min)
```bash
# Re-run benchmarks
dotnet run --project PokeSharp.Game --configuration Release

# Compare metrics:
- Frame time improvement: ____%
- GC reduction: ____%
- Validate no functional regressions
```

---

## 📊 Quick Reference: Arch ECS Features

### ✅ Currently Using
- `QueryDescription` caching
- Parallel query execution
- Spatial hashing
- Component queries

### ❌ Not Using (Opportunities)
- **CommandBuffer** → Deferred structural changes
- **Entity.Disable()** → Entity pooling
- **Archetypes.Reserve()** → Pre-allocation
- **ArrayPool<T>** → Temporary array reuse
- **Span<T>** → Zero-allocation slicing

---

## 🎯 Code Review Checklist

Before committing optimizations, verify:
- [ ] Baseline benchmarks recorded
- [ ] Post-optimization benchmarks show improvement
- [ ] All existing tests pass
- [ ] No new compiler warnings
- [ ] Code is well-documented
- [ ] No functional regressions in manual testing

---

## 🚨 Common Pitfalls to Avoid

### ❌ Don't Do This
```csharp
// Allocating inside loops
for (int i = 0; i < 1000; i++)
    var list = new List<Entity>(); // ❌

// Structural changes during query
world.Query(query, entity => entity.Remove<T>()); // ❌

// Boxing value types
object boxed = myStruct; // ❌

// LINQ in hot paths
var result = entities.Where(e => e.IsAlive).ToList(); // ❌
```

### ✅ Do This Instead
```csharp
// Reuse collections
var list = new List<Entity>(capacity: 100);
list.Clear(); // ✅

// Use CommandBuffer
var buffer = new CommandBuffer();
world.Query(query, entity => buffer.Remove<T>(entity)); // ✅

// Keep value types as-is
MyStruct value = myStruct; // ✅

// Manual iteration
foreach (var entity in entities)
    if (entity.IsAlive) result.Add(entity); // ✅
```

---

## 📈 Expected Results Summary

| Optimization | Time | Impact | Risk |
|-------------|------|--------|------|
| Static Lists | 30m | 8-12% GC | ✅ Zero |
| CommandBuffer | 2h | 5-10% Perf | ✅ Low |
| Struct Packing | 30m | 2-4% Perf | ✅ Zero |
| **TOTAL** | **3h** | **15-26%** | **✅ Low** |

---

## 🔗 Useful Links

- [Full Research Doc](./PHASE-4-RESEARCH.md)
- [Optimization Roadmap](./OPTIMIZATION-ROADMAP.md)
- [Arch ECS GitHub](https://github.com/genaray/Arch)
- [.NET Performance Tips](https://docs.microsoft.com/en-us/dotnet/core/performance/)

---

**Last Updated:** 2025-11-09
**Estimated Time:** 3 hours
**Estimated Gain:** 15-26% performance improvement
**Risk Level:** Low
