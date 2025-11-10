# System Lifecycle - Quick Reference Card

## 🚀 Registration Methods

### Update Systems (Game Logic)
```csharp
// Use for systems that modify game state
_systemManager.RegisterUpdateSystem<MovementSystem>();
_systemManager.RegisterUpdateSystem(new InputSystem(...));

// Properties:
public int UpdatePriority => 100;  // Lower = runs first (0-1000)
public void Update(World world, float deltaTime) { }
```

### Render Systems (Graphics)
```csharp
// Use for systems that draw to screen
_systemManager.RegisterRenderSystem<ZOrderRenderSystem>();
_systemManager.RegisterRenderSystem(new UIRenderer(...));

// Properties:
public int RenderOrder => 1;  // 0=background, 1=world, 2=UI, 3=debug
public void Render(World world) { }
```

### Legacy Systems (Deprecated)
```csharp
// ⚠️ Avoid - use specialized methods above
_systemManager.RegisterSystem<MySystem>();
```

---

## 📊 System Lifecycle Phases

```
1. REGISTRATION
   ↓
2. INITIALIZATION (once)
   ↓
3. GAME LOOP (every frame)
   ├─ Update Phase (logic)
   └─ Render Phase (graphics)
```

---

## 🎯 Priority Guidelines

### Update Systems (UpdatePriority: 0-1000)
```
0-100    : Input and early game logic
100-300  : Movement and physics
300-500  : Collision and interaction
500-700  : AI and gameplay systems
700-900  : Animation and effects
900-1000 : Late update and cleanup
```

### Render Systems (RenderOrder: 0-3)
```
0 : Background layers
1 : World/gameplay objects
2 : UI elements
3 : Debug overlays
```

---

## ✅ Best Practices

### DO ✅
```csharp
// Use specialized registration
_systemManager.RegisterUpdateSystem<MySystem>();
_systemManager.RegisterRenderSystem<MyRenderSystem>();

// Keep initialization simple
public override void Initialize(World world)
{
    base.Initialize(world);
    // Just store references, no complex logic
}

// Put complex logic in Update
public void Update(World world, float deltaTime)
{
    // Complex initialization can happen here
    // Dependencies are guaranteed to be ready
}
```

### DON'T ❌
```csharp
// Don't use legacy registration
_systemManager.RegisterSystem<MySystem>();  // ❌

// Don't depend on init order of other systems
public override void Initialize(World world)
{
    base.Initialize(world);
    var otherSystem = GetSystem<OtherSystem>(); // ❌ May not be init yet
}

// Don't mix priority properties inconsistently
public int Priority => 500;
public int UpdatePriority => 10;  // ❌ Confusing initialization order
```

---

## 🔍 Common Patterns

### Update System Example
```csharp
public class MovementSystem : IUpdateSystem
{
    public int Priority => 100;
    public int UpdatePriority => 100;  // ✅ Same value
    public bool Enabled { get; set; } = true;

    public void Initialize(World world)
    {
        // Simple initialization only
    }

    public void Update(World world, float deltaTime)
    {
        // Main logic here
    }
}
```

### Render System Example
```csharp
public class SpriteRenderer : IRenderSystem
{
    public int Priority => 900;
    public int RenderOrder => 1;  // World layer
    public bool Enabled { get; set; } = true;

    public void Initialize(World world)
    {
        // Simple initialization only
    }

    public void Update(World world, float deltaTime)
    {
        // Can still update (IRenderSystem : ISystem)
    }

    public void Render(World world)
    {
        // Rendering logic here
    }
}
```

---

## 🐛 Troubleshooting

### System Not Initializing?
```
✅ Check: Did you use RegisterUpdateSystem() or RegisterRenderSystem()?
✅ Check: Does your system implement IUpdateSystem or IRenderSystem?
✅ Check: Was Initialize() called before Update()?
```

### Null Reference During Init?
```
⚠️ Cause: Depending on another system's initialization
✅ Fix: Move complex logic to Update(), not Initialize()
✅ Fix: Use lazy initialization in Update() phase
```

### Wrong Execution Order?
```
⚠️ Cause: Priority mismatch (Priority ≠ UpdatePriority)
✅ Fix: Make Priority and UpdatePriority the same value
✅ Fix: Check system registration order doesn't matter
```

---

## 📞 Need More Info?

- **Full Analysis:** `system-registration-lifecycle-analysis.md`
- **Flow Diagrams:** `system-lifecycle-flow-diagram.md`
- **Executive Summary:** `SYSTEM_LIFECYCLE_EXECUTIVE_SUMMARY.md`
- **Source Code:** `PokeSharp.Core/Systems/SystemManager.cs`

---

**Quick Tip:** When in doubt, keep initialization simple and put complex logic in Update()!
