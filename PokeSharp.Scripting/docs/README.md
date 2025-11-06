# PokeSharp.Scripting Documentation

> Comprehensive documentation for the PokeSharp.Scripting hot-reloadable C# scripting system.

---

## 📚 Documentation Index

### Architecture Documentation (NEW)

| Document | Description | Pages | Audience |
|----------|-------------|-------|----------|
| **[ARCHITECTURE.md](ARCHITECTURE.md)** | Comprehensive architecture documentation with 9 detailed sections covering system design, components, threading, and quality attributes | 1,755 lines | Architects, Senior Developers |
| **[ARCHITECTURE_DIAGRAMS.md](ARCHITECTURE_DIAGRAMS.md)** | Visual architecture representations including C4 model, sequence diagrams, state diagrams, class diagrams, and data flow diagrams | 854 lines | All Technical Staff |
| **[ARCHITECTURE_SUMMARY.md](ARCHITECTURE_SUMMARY.md)** | Quick reference guide with key patterns, performance metrics, troubleshooting, and code examples | 533 lines | Developers, DevOps |

### Integration & Tutorials

| Document | Description | Audience |
|----------|-------------|----------|
| **[MONOGAME-INTEGRATION.md](MONOGAME-INTEGRATION.md)** | MonoGame integration guide with performance optimization | Game Developers |
| **[tutorials/](tutorials/)** | Step-by-step tutorials for common scenarios | New Users |

---

## 🚀 Quick Start

### For New Developers
1. Start with **[ARCHITECTURE_SUMMARY.md](ARCHITECTURE_SUMMARY.md)** - Get familiar with key concepts in 15 minutes
2. Review **[ARCHITECTURE_DIAGRAMS.md](ARCHITECTURE_DIAGRAMS.md)** - Understand system visually
3. Try examples in **[ARCHITECTURE_SUMMARY.md](ARCHITECTURE_SUMMARY.md)** - Hands-on learning

### For Architects
1. Read **[ARCHITECTURE.md](ARCHITECTURE.md)** - Deep dive into design decisions
2. Study **[Architectural Decision Records](ARCHITECTURE.md#9-architectural-decision-records)** - Understand rationale
3. Review **[Extension Points](ARCHITECTURE.md#6-extension-points)** - Plan customizations

### For DevOps / Operations
1. Check **[Performance Characteristics](ARCHITECTURE.md#7-performance-characteristics)** - Understand metrics
2. Review **[Threading Model](ARCHITECTURE.md#5-threading-model)** - Concurrency patterns
3. Use **[Troubleshooting Guide](ARCHITECTURE_SUMMARY.md#-troubleshooting)** - Common issues

---

## 📖 Architecture Documentation Deep Dive

### ARCHITECTURE.md - Table of Contents

1. **System Overview**
   - High-level architecture diagram
   - Component relationships
   - Data flow

2. **Core Components**
   - ScriptService (orchestrator)
   - IScriptCompiler (compilation)
   - TypeScriptBase (user scripts)
   - ScriptContext (runtime API)

3. **Hot-Reload Subsystem**
   - VersionedScriptCache (versioning)
   - ScriptHotReloadService (orchestration)
   - File watchers (change detection)
   - ScriptBackupManager (rollback)
   - Notification system

4. **Design Patterns**
   - Template Method (TypeScriptBase)
   - Strategy (IScriptWatcher)
   - Chain of Responsibility (version history)
   - Facade (ScriptService)
   - Memento (backup/restore)

5. **Threading Model**
   - Concurrency patterns
   - Lock strategies (SemaphoreSlim, ConcurrentDictionary)
   - Async/await usage

6. **Extension Points**
   - Custom compilers
   - Custom watchers
   - Script capabilities
   - Notification handlers

7. **Performance Characteristics**
   - Hot-reload: 100-500ms target
   - Rollback: <1ms
   - Memory: O(1) per script
   - CPU: <1% overhead

8. **Quality Attributes**
   - Reliability (99%+ success rate)
   - Maintainability (8.5/10)
   - Performance (7.5/10)
   - Security (sandbox considerations)

9. **Architectural Decision Records**
   - ADR-001: Lazy Instantiation
   - ADR-002: Dual Rollback Strategy
   - ADR-003: SemaphoreSlim for Async
   - ADR-004: Per-File Debouncing
   - ADR-005: Strategy Pattern for Watchers
   - ADR-006: Stateless Script Design
   - ADR-007: ConcurrentDictionary Cache

---

## 🎨 Diagrams Reference

### ARCHITECTURE_DIAGRAMS.md - Available Diagrams

#### 1. C4 Model Diagrams
- **Level 1: System Context** - External users and systems
- **Level 2: Container Diagram** - Major subsystems and dependencies
- **Level 3: Component Diagram** - Internal component breakdown

#### 2. Sequence Diagrams
- **Hot-Reload Success Flow** - Complete workflow from edit to execution
- **Hot-Reload Failure with Rollback** - Automatic recovery process
- **Lazy Instantiation Flow** - Deferred instance creation

#### 3. State Diagrams
- **Script Lifecycle States** - From compilation to execution
- **Watcher State Machine** - File watching lifecycle

#### 4. Class Diagrams
- **Core Components** - TypeScriptBase, ScriptContext, ScriptService
- **Hot-Reload Subsystem** - Cache, backup, watchers

#### 5. Data Flow Diagrams
- **Compilation Pipeline** - From .cs file to executable Type
- **Entity-Script Data Flow** - Game loop to script execution
- **Version History Chain** - Rollback structure
- **Concurrency Model** - Thread interaction patterns

---

## 📊 Key Metrics & Performance

### Performance Targets (from ARCHITECTURE_SUMMARY.md)

| Metric | Target | Actual | Status |
|--------|--------|--------|--------|
| Hot-reload time | 100-500ms | 381-457ms | ✅ Met |
| Rollback time | <1ms | <1ms | ✅ Met |
| Success rate | 99%+ | 99.04% | ✅ Met |
| Frame spike | 0.1-0.5ms | <0.5ms | ✅ Met |
| Debounce efficiency | N/A | 60-80% | 📊 Data |

### Architecture Quality Scores

| Attribute | Score | Key Strengths |
|-----------|-------|---------------|
| **Reliability** | 9.5/10 | Automatic rollback, thread-safe |
| **Maintainability** | 8.5/10 | Clear patterns, 95% doc coverage |
| **Performance** | 7.5/10 | Lazy init, lock-free reads |
| **Testability** | 9/10 | Interfaces, DI-friendly |
| **Security** | ⚠️ | No sandboxing (future work) |

---

## 🔑 Key Architectural Decisions

### Why These Patterns?

1. **Lazy Instantiation** - Faster hot-reload, minimal frame impact
2. **Dual Rollback** - 100% recovery success rate
3. **Stateless Scripts** - Singleton pattern works correctly
4. **SemaphoreSlim** - Async-safe locking (no deadlocks)
5. **ConcurrentDictionary** - Lock-free reads (99% operations)
6. **Strategy Pattern** - Platform-optimized file watching
7. **Debouncing** - 60-80% reduction in wasted compilation

Full rationale in [ARCHITECTURE.md - ADRs](ARCHITECTURE.md#9-architectural-decision-records)

---

## 🛠️ Common Use Cases

### 1. Understanding the System
- **New developer onboarding**: Start with [ARCHITECTURE_SUMMARY.md](ARCHITECTURE_SUMMARY.md)
- **System design review**: Read [ARCHITECTURE.md](ARCHITECTURE.md) sections 1-4
- **Debugging hot-reload issues**: Check [Troubleshooting](ARCHITECTURE_SUMMARY.md#-troubleshooting)

### 2. Extending the System
- **Adding custom compiler**: See [Extension Points - Custom Compilers](ARCHITECTURE.md#61-custom-compilers)
- **Implementing new watcher**: See [Extension Points - Custom Watchers](ARCHITECTURE.md#62-custom-watchers)
- **Creating script capabilities**: See [Extension Points - Script Capabilities](ARCHITECTURE.md#63-script-capabilities-custom-typescriptbase-extensions)

### 3. Performance Optimization
- **Analyzing bottlenecks**: Review [Performance Characteristics](ARCHITECTURE.md#7-performance-characteristics)
- **Understanding memory usage**: Check [Memory Characteristics](ARCHITECTURE.md#72-memory-characteristics)
- **Improving compile times**: See [Bottleneck Analysis](ARCHITECTURE.md#71-hot-reload-performance)

### 4. Code Review
- **Checking design patterns**: Refer to [Design Patterns](ARCHITECTURE.md#4-design-patterns)
- **Validating thread safety**: Review [Threading Model](ARCHITECTURE.md#5-threading-model)
- **Assessing quality**: Check [Quality Attributes](ARCHITECTURE.md#8-quality-attributes)

---

## 📝 Writing Your Own Scripts

### Quick Reference

```csharp
// ✅ CORRECT: Stateless script with ScriptContext state
public class MyBehavior : TypeScriptBase
{
    protected override void OnTick(ScriptContext ctx, float deltaTime)
    {
        // Use ScriptContext for persistent state
        var timer = ctx.GetOrAddState<Timer>();
        timer.Elapsed += deltaTime;

        // Access ECS components
        if (ctx.TryGetState<Health>(out var health))
        {
            health.Current -= 1;
        }

        // Use helper methods
        var direction = GetDirectionTo(currentPos, targetPos);
        ShowMessage(ctx, "Moving!");
    }
}

// Return instance at end
new MyBehavior()
```

### Anti-Patterns

```csharp
// ❌ WRONG: Instance state (breaks with multiple entities)
public class BadBehavior : TypeScriptBase
{
    private int counter;  // DON'T DO THIS!

    protected override void OnTick(ScriptContext ctx, float deltaTime)
    {
        counter++;  // This will be shared across ALL entities!
    }
}
```

Full examples in [ARCHITECTURE_SUMMARY.md - Common Patterns](ARCHITECTURE_SUMMARY.md#-common-patterns)

---

## 🔍 Troubleshooting Quick Links

| Issue | Solution Guide |
|-------|----------------|
| Hot-reload not working | [Troubleshooting - Hot-Reload](ARCHITECTURE_SUMMARY.md#hot-reload-not-working) |
| Compilation errors | [Troubleshooting - Compilation](ARCHITECTURE_SUMMARY.md#compilation-errors) |
| Performance slow | [Troubleshooting - Performance](ARCHITECTURE_SUMMARY.md#performance-issues) |
| Memory leaks | [Troubleshooting - Memory](ARCHITECTURE_SUMMARY.md#memory-leaks) |

---

## 🎓 Learning Path

### Beginner (0-2 hours)
1. Read [ARCHITECTURE_SUMMARY.md](ARCHITECTURE_SUMMARY.md) (30 min)
2. Review visual diagrams in [ARCHITECTURE_DIAGRAMS.md](ARCHITECTURE_DIAGRAMS.md) (30 min)
3. Try Quick Start examples (1 hour)

### Intermediate (2-6 hours)
1. Study [Core Components](ARCHITECTURE.md#2-core-components) (1 hour)
2. Understand [Hot-Reload Subsystem](ARCHITECTURE.md#3-hot-reload-subsystem) (2 hours)
3. Learn [Design Patterns](ARCHITECTURE.md#4-design-patterns) (1 hour)
4. Review [Threading Model](ARCHITECTURE.md#5-threading-model) (2 hours)

### Advanced (6+ hours)
1. Deep dive into [ADRs](ARCHITECTURE.md#9-architectural-decision-records) (2 hours)
2. Explore [Extension Points](ARCHITECTURE.md#6-extension-points) (2 hours)
3. Study [Performance Analysis](ARCHITECTURE.md#7-performance-characteristics) (2 hours)
4. Implement custom compiler or watcher (project-based)

---

## 📚 External Resources

### Recommended Reading
- [Roslyn Scripting API](https://github.com/dotnet/roslyn/wiki/Scripting-API-Samples) - Official Roslyn docs
- [C4 Model](https://c4model.com/) - Architecture diagram notation
- [ECS Architecture](https://github.com/SanderMertens/ecs-faq) - Entity Component System patterns
- [Async/Await Best Practices](https://docs.microsoft.com/en-us/archive/msdn-magazine/2013/march/async-await-best-practices-in-asynchronous-programming) - Threading guidance

### Related Projects
- [PokeSharp.Core](../../Core/) - ECS components and systems
- [PokeSharp.Game](../../Game/) - Main game engine
- [Arch ECS](https://github.com/genaray/Arch) - High-performance ECS framework

---

## 📞 Support & Contribution

### Getting Help
1. Check [Troubleshooting Guide](ARCHITECTURE_SUMMARY.md#-troubleshooting)
2. Review [Common Patterns](ARCHITECTURE_SUMMARY.md#-common-patterns)
3. Search [Architecture Documentation](ARCHITECTURE.md)
4. Open an issue on GitHub

### Contributing
- Follow architectural patterns documented here
- Add ADRs for significant design decisions
- Update diagrams when changing architecture
- Maintain 95%+ documentation coverage

---

## 📄 Document Metadata

| Document | Lines | Size | Last Updated | Audience |
|----------|-------|------|--------------|----------|
| ARCHITECTURE.md | 1,755 | 56 KB | 2025-01-06 | Architects, Senior Devs |
| ARCHITECTURE_DIAGRAMS.md | 854 | 60 KB | 2025-01-06 | All Technical Staff |
| ARCHITECTURE_SUMMARY.md | 533 | 17 KB | 2025-01-06 | Developers, DevOps |
| MONOGAME-INTEGRATION.md | 880 | 24 KB | 2024-12-XX | Game Developers |

**Total Documentation**: 4,022 lines, 157 KB

---

## ✨ What's New

### January 2025 - Complete Architecture Documentation

**NEW**: Comprehensive architecture documentation suite:
- ✅ Full system architecture (ARCHITECTURE.md)
- ✅ Visual diagrams (ARCHITECTURE_DIAGRAMS.md)
- ✅ Quick reference guide (ARCHITECTURE_SUMMARY.md)
- ✅ 7 Architectural Decision Records (ADRs)
- ✅ Performance metrics and quality scores
- ✅ Troubleshooting guide
- ✅ Extension points documentation

**Key Additions**:
- C4 model diagrams (3 levels)
- 3 sequence diagrams (success, failure, lazy init)
- 2 state diagrams (script lifecycle, watcher states)
- 2 class diagrams (core, hot-reload)
- 5 data flow diagrams
- 50+ code examples
- 100+ pages of technical documentation

---

**Documentation Maintained By**: Architecture Team
**Last Major Update**: 2025-01-06
**Version**: 1.0
