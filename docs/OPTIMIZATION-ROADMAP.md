# PokeSharp ECS Optimization Roadmap

## Quick Reference Guide

### 📊 Optimization Impact Matrix

```
┌──────────────────────────────────────────────────────────────┐
│  Impact vs. Effort Analysis                                  │
├──────────────────────────────────────────────────────────────┤
│                                                               │
│  High Impact │  🎯 CommandBuffer       🎯 Entity Pooling     │
│              │     (10%, 3hrs)            (15%, 6hrs)        │
│              │                                                │
│              │  🎯 Eliminate Lists                            │
│              │     (12%, 2hrs)                                │
│  ─────────────────────────────────────────────────────────── │
│              │                                                │
│  Medium      │  📦 ArrayPool           📦 Struct Packing     │
│  Impact      │     (8%, 4hrs)             (4%, 2hrs)         │
│              │                                                │
│              │  📦 Archetype Reserve                          │
│              │     (5%, 2hrs)                                 │
│  ─────────────────────────────────────────────────────────── │
│              │                                                │
│  Low Impact  │  ⚙️ SIMD Vectorization                        │
│              │     (10%, 12hrs)                               │
│              │                                                │
│              │                                                │
│              └────────────────┬───────────────────────────────│
│                 Low Effort    │     High Effort               │
└──────────────────────────────────────────────────────────────┘
```

---

## 🚀 3-Week Implementation Plan

### Week 1: Quick Wins (15-26% gain, 6-9 hours)

**Day 1-2: Allocation Elimination (2-3 hours)**
```csharp
// Before
public override List<Type> GetReadComponents()
    => new List<Type> { typeof(Position), ... };

// After
private static readonly List<Type> _readComponents = new() { ... };
public override List<Type> GetReadComponents() => _readComponents;
```
**Target Files:**
- MovementSystem.cs (Lines 458, 472)
- PathfindingSystem.cs (Line 211)
- SystemManager.cs (Lines 204, 290)

**Expected Gain:** 8-12% GC reduction

---

**Day 3-4: CommandBuffer Implementation (3-4 hours)**
```csharp
// New CommandBuffer class
public class CommandBuffer
{
    public void Remove<T>(Entity entity) where T : struct;
    public void Add<T>(Entity entity, T component) where T : struct;
    public void Execute();
}
```
**Target Systems:**
- RelationshipSystem.cs (Lines 108, 170, 197)
- PathfindingSystem.cs (entity modifications)
- MovementSystem.cs (MovementRequest cleanup)

**Expected Gain:** 5-10% performance improvement

---

**Day 5: Struct Packing (1-2 hours)**
```csharp
[StructLayout(LayoutKind.Sequential, Pack = 4)]
public struct Position { ... }
```
**Target Components:**
- Position, GridMovement, TilePosition, MovementRoute

**Expected Gain:** 2-4% query performance

---

### Week 2: Medium Wins (15-23% gain, 7-10 hours)

**Day 1-3: Entity Pooling (4-6 hours)**
```csharp
public class EntityPool<TComponents> where TComponents : IArchetype
{
    public Entity Get() { ... }
    public void Return(Entity entity) { ... }
}
```
**Target Use Cases:**
- MovementRequest entities (high churn)
- Temporary collision detection entities
- Pathfinding intermediate entities

**Expected Gain:** 10-15% movement performance

---

**Day 4-5: ArrayPool Integration (3-4 hours)**
```csharp
// ParallelQueryExecutor optimization
var entities = ArrayPool<Entity>.Shared.Rent(estimatedSize);
try { /* process */ }
finally { ArrayPool<Entity>.Shared.Return(entities); }
```
**Target File:**
- ParallelQueryExecutor.cs (Lines 50, 78, 105, 135)

**Expected Gain:** 5-8% GC reduction

---

### Week 3: Advanced Optimizations (8-15% gain, 10-15 hours)

**Day 1-2: Archetype Pre-allocation (2-3 hours)**
```csharp
public void InitializeArchetypes(World world)
{
    world.Reserve<Position, GridMovement, Animation>(100);
    world.Reserve<TilePosition, Collision>(1000);
    // ... other common archetypes
}
```
**Expected Gain:** 3-5% entity creation speed

---

**Day 3-5: SIMD Vectorization (8-12 hours)**
```csharp
// Vectorized lerp for 8 entities at once
using System.Numerics;
public void ProcessMovementSIMD(Span<Position> positions) { ... }
```
**Complexity:** High (requires SoA data layout)
**Expected Gain:** 5-10% for 100+ entities

---

## 📈 Benchmarking Checklist

### Pre-Optimization Baseline
```bash
# Run these tests BEFORE any changes
- [ ] 100 NPCs pathfinding stress test
- [ ] 50 simultaneous entity movements
- [ ] 1000 tiles + 50 dynamic collision test
- [ ] 200 parent-child relationships
- [ ] Record GC Gen0/Gen1 collection counts
- [ ] Record per-frame allocation bytes
- [ ] Record average frame time (60fps = 16.67ms target)
```

### Post-Optimization Validation
```bash
# Run after EACH phase
- [ ] Same test scenarios as baseline
- [ ] Compare frame times (10%+ improvement?)
- [ ] Compare GC collections (20%+ reduction?)
- [ ] Verify no functional regressions
- [ ] Check 1% low frame times (stutter reduction)
```

---

## 🎯 Success Criteria

### Phase 1 Targets
- ✅ **Frame Time:** 10% reduction in MovementSystem/RelationshipSystem
- ✅ **GC Collections:** 20% reduction in Gen0 collections
- ✅ **Code Quality:** Zero functional regressions

### Phase 2 Targets
- ✅ **Movement Performance:** 15% faster with 50+ moving entities
- ✅ **Memory:** 30% reduction in per-frame allocations
- ✅ **Throughput:** 20% more entities processable at 60fps

### Phase 3 Targets
- ✅ **Entity Creation:** 5% faster archetype allocation
- ✅ **Large Batches:** 10% improvement with 100+ entities
- ✅ **Stability:** No increase in worst-case frame times

---

## 🔧 Implementation Order (Strict Priority)

1. **Eliminate List Allocations** → Immediate, safe, high ROI
2. **CommandBuffer** → Foundation for future optimizations
3. **Struct Packing** → One-time effort, permanent benefit
4. **Entity Pooling** → High impact, requires careful testing
5. **ArrayPool** → Complements parallel execution
6. **Archetype Reserve** → Low-hanging fruit for init time
7. **SIMD** → Only if other gains don't meet targets

---

## ⚠️ Known Risks and Mitigations

### Entity Pooling Risks
**Risk:** Stale component data in pooled entities
**Mitigation:**
```csharp
public void Return(Entity entity)
{
    entity.Set(default(Position));     // Clear components
    entity.Set(default(GridMovement)); // Prevent data leaks
    entity.Disable();
    _pool.Push(entity);
}
```

### CommandBuffer Risks
**Risk:** Forgetting to call Execute()
**Mitigation:** Use using pattern or automatic flush

### ArrayPool Risks
**Risk:** Forgetting to return arrays to pool
**Mitigation:** Always use try-finally blocks

---

## 📚 Monitoring and Profiling

### Tools to Use
1. **Visual Studio Profiler** - CPU sampling, allocation tracking
2. **dotTrace** - Advanced .NET profiling
3. **BenchmarkDotNet** - Microbenchmarking individual methods
4. **In-Game Metrics** - FPS counter, frame time graph

### Key Metrics to Track
```csharp
public class PerformanceMetrics
{
    public double AverageFrameMs;
    public double P99FrameMs;        // 99th percentile (worst 1%)
    public long GCGen0Collections;
    public long AllocatedBytes;
    public int EntitiesProcessed;
    public double MovementSystemMs;
    public double PathfindingSystemMs;
}
```

---

## 🎓 Learning Resources

### Arch-Specific
- [Arch GitHub](https://github.com/genaray/Arch)
- [Arch Performance Wiki](https://github.com/genaray/Arch/wiki/Performance)

### .NET Performance
- [Writing High-Performance Code](https://docs.microsoft.com/en-us/dotnet/core/performance/)
- [Span<T> Deep Dive](https://docs.microsoft.com/en-us/archive/msdn-magazine/2018/january/csharp-all-about-span-exploring-a-new-net-mainstay)

### ECS Patterns
- [Data-Oriented Design](https://www.dataorienteddesign.com/dodbook/)
- [Entities and Components](https://github.com/SanderMertens/ecs-faq)

---

## 📝 Progress Tracking Template

```markdown
## Week 1 Progress

### Day 1: Allocation Elimination
- [x] Fixed MovementSystem.GetReadComponents
- [x] Fixed PathfindingSystem.ToArray() calls
- [ ] Fixed SystemManager defensive copies
- **Benchmark:** GC Gen0 reduced from X to Y (-Z%)

### Day 3: CommandBuffer
- [ ] Implemented CommandBuffer class
- [ ] Refactored RelationshipSystem
- [ ] Added unit tests
- **Benchmark:** Frame time reduced from X to Y (-Z%)

### Week 1 Summary
- **Total Time:** X hours
- **Performance Gain:** Y%
- **GC Reduction:** Z%
- **Issues Found:** None / [List issues]
- **Next Steps:** Begin Week 2 optimizations
```

---

## 🚦 Go/No-Go Decision Points

### After Phase 1
**Continue to Phase 2 if:**
- ✅ 10%+ performance improvement measured
- ✅ No functional regressions
- ✅ Code quality maintained

**Reassess if:**
- ❌ Less than 5% improvement
- ❌ Instability introduced
- ❌ Implementation took 2x expected time

### After Phase 2
**Continue to Phase 3 if:**
- ✅ Cumulative 20%+ improvement
- ✅ Team comfortable with complexity
- ✅ Still have performance budget for SIMD work

**Stop and consolidate if:**
- ❌ Already hit performance targets
- ❌ Diminishing returns observed
- ❌ Other priorities emerged

---

## 🎉 Expected Final Results

### Conservative Estimate (All Phases)
- **Frame Time:** 25% reduction
- **GC Pressure:** 40% reduction
- **Entity Throughput:** 30% increase
- **Memory Footprint:** 15% reduction

### Aggressive Estimate (With SIMD)
- **Frame Time:** 35% reduction
- **GC Pressure:** 50% reduction
- **Entity Throughput:** 50% increase
- **Memory Footprint:** 20% reduction

---

**Last Updated:** 2025-11-09
**Status:** Ready for Implementation
**Owner:** Development Team
