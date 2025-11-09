# Entity Pooling Quick Reference Card

## 🚀 Setup (One-Time)

```csharp
// 1. Create pool manager
var poolManager = new EntityPoolManager(world);

// 2. Register pools
poolManager.RegisterPool(PoolConfiguration.Projectiles);
poolManager.RegisterPool(PoolConfiguration.Enemies);

// 3. Create pooling factory
var factory = new EntityFactoryServicePooling(
    templateCache, logger, poolManager
);

// 4. Register cleanup system
systemManager.RegisterSystem(
    new PoolCleanupSystem(poolManager, logger)
);
```

## ⚡ Spawn Pooled Entity

```csharp
// Spawn with pooling (2-3x faster)
var entity = factory.SpawnFromTemplate(
    "templateId",
    world,
    context: null,
    usePooling: true,      // ← Enable pooling
    poolName: "projectiles"
);

// Use entity normally
entity.Get<Transform>().Position = pos;

// Release when done (DON'T use entity.Destroy()!)
factory.ReleaseEntity(entity);
```

## 🎯 Common Pools

| Pool Name      | Initial | Max   | Use Case              |
|----------------|---------|-------|-----------------------|
| `default`      | 100     | 1000  | General entities      |
| `enemies`      | 50      | 500   | Enemy NPCs            |
| `projectiles`  | 200     | 2000  | Bullets/projectiles   |
| `effects`      | 100     | 1000  | VFX/particles         |
| `ui`           | 50      | 200   | UI elements           |
| `particles`    | 500     | 5000  | Particle systems      |

## 🔥 Typed Pools (Advanced)

```csharp
// Create typed pool
var bulletPool = new TypedEntityPool<Transform, Velocity>(
    world,
    entity => {
        entity.Add(new Transform());
        entity.Add(new Velocity());
    },
    "bullets",
    initialSize: 200,
    maxSize: 2000
);

// Spawn (components already added!)
var bullet = bulletPool.Acquire();
bullet.Get<Transform>().Position = pos;

// Release
bulletPool.Release(bullet);
```

## 📊 Monitor Pools

```csharp
// Get statistics
var stats = poolManager.GetStatistics();
Console.WriteLine($"Active: {stats.TotalActive}");
Console.WriteLine($"Reuse Rate: {stats.OverallReuseRate:P2}");

// Per-pool stats
foreach (var (name, poolStats) in stats.PerPoolStats) {
    Console.WriteLine($"{name}: {poolStats.UsagePercent:P0} full");
}
```

## ✅ Do's and ❌ Don'ts

### ✅ DO
- Call `factory.ReleaseEntity()` when done
- Pre-warm pools during loading
- Monitor pool statistics
- Use typed pools for hot paths

### ❌ DON'T
- Call `entity.Destroy()` on pooled entities
- Forget to release entities (causes pool exhaustion)
- Set pool sizes too small
- Pool long-lived entities (player, world objects)

## 🐛 Troubleshooting

### Pool Exhausted Error
```
InvalidOperationException: Entity pool 'projectiles' exhausted
```
**Fix**: Increase `MaxSize` or reduce spawn rate

### Memory Leak Warning
```
Pool 'enemies' has low release rate (60%)
```
**Fix**: Find missing `ReleaseEntity()` calls

### High Usage Warning
```
Pool 'projectiles' is 95% full
```
**Fix**: Increase pool size or optimize entity lifetime

## 📚 More Info

See `/docs/EntityPoolingGuide.md` for complete documentation.
