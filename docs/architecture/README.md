# Architecture Documentation

This directory contains architectural decisions, design documents, and technical specifications for the PokeSharp MonoGame project.

---

## Quick Links

### Architecture Decision Records (ADRs)
- [ADR-001: MapPopupService Naming Strategy](./ADR-001-map-popup-service-naming.md) - Resolving naming collision between two MapPopupService classes

### Design Documents
- [Map Popup Service Refactoring Plan](./map-popup-service-refactoring-plan.md) - Detailed implementation plan for renaming services
- [Map Popup Service Architecture](./diagrams/map-popup-service-architecture.md) - Visual diagrams and component interaction flows
- [Base Game as Mod Architecture](./base-game-as-mod-architecture.md) - Comprehensive design for converting base game content into mod-based system
- [Content Provider C4 Diagrams](./diagrams/content-provider-c4-context.md) - C4 model diagrams for ContentProvider architecture
- [Content Resolution Sequences](./diagrams/content-resolution-sequence.md) - Sequence diagrams for content loading scenarios
- [Technology Evaluation Matrix](./technology-evaluation-matrix.md) - Comparative analysis of architectural approaches
- [Implementation Roadmap](./implementation-roadmap.md) - 4-week implementation plan with detailed tasks

---

## Active Issues

### 🔴 High Priority

#### MapPopupService Naming Collision
**Status:** PROPOSED - Awaiting Implementation
**ADR:** [ADR-001](./ADR-001-map-popup-service-naming.md)
**Implementation Plan:** [Refactoring Plan](./map-popup-service-refactoring-plan.md)

**Problem:**
Two classes named `MapPopupService` exist in different namespaces, causing:
- Ambiguity and confusion
- Requirement for fully qualified names
- Poor developer experience

**Solution:**
Rename services to clearly indicate responsibilities:
- `GameData.Services.MapPopupService` → `MapPopupDataService` (data access)
- `Engine.Scenes.Services.MapPopupService` → `MapPopupOrchestrator` (event orchestration)

**Impact:**
- 9 files to modify
- ~4-6 hours effort
- Low risk (compiler catches all references)

---

### 🟡 Medium Priority

#### Base Game as Mod Architecture
**Status:** DESIGN PHASE - Architecture Complete
**Documentation:** [Base Game as Mod Architecture](./base-game-as-mod-architecture.md)
**Implementation Plan:** [4-Week Roadmap](./implementation-roadmap.md)

**Problem:**
Base game content is loaded from hardcoded paths, preventing:
- Mod overrides of base content
- Unified content resolution
- Hot-reload support for all content
- Consistent content discovery

**Solution:**
Treat base game as special mod (`base:pokesharp-core`) with unified ContentProvider:
- Priority-based content resolution
- LRU caching for performance
- Backward compatible API
- Security-hardened path resolution

**Impact:**
- 4-week implementation timeline
- New `IContentProvider` interface
- Integration with AssetManager and GameDataLoader
- Test coverage >90% required
- Full architecture documentation complete

---

## Document Index

### By Category

#### Decision Records
| Document | Status | Date | Topic |
|----------|--------|------|-------|
| [ADR-001](./ADR-001-map-popup-service-naming.md) | PROPOSED | 2025-12-05 | Service naming strategy |

#### Design Documents
| Document | Type | Date | Topic |
|----------|------|------|-------|
| [Map Popup Service Refactoring Plan](./map-popup-service-refactoring-plan.md) | Implementation Plan | 2025-12-05 | Step-by-step refactoring guide |
| [Map Popup Service Architecture](./diagrams/map-popup-service-architecture.md) | Architecture Diagram | 2025-12-05 | Visual architecture reference |

#### Diagrams
| Document | Type | Coverage |
|----------|------|----------|
| [Map Popup Service Architecture](./diagrams/map-popup-service-architecture.md) | Component Diagram | Data flow, dependencies, lifecycle |

---

## ADR Process

### Template
All ADRs should follow this structure:
1. **Status** - PROPOSED, ACCEPTED, REJECTED, SUPERSEDED, DEPRECATED
2. **Context** - What is the issue we're addressing?
3. **Decision** - What are we doing about it?
4. **Consequences** - What are the results of this decision?
5. **Alternatives Considered** - What other options were evaluated?

### Workflow
1. **Proposal** - Create ADR document with PROPOSED status
2. **Review** - Architecture team reviews and provides feedback
3. **Decision** - Team approves (ACCEPTED) or rejects (REJECTED)
4. **Implementation** - If accepted, execute implementation plan
5. **Completion** - Update ADR with actual results

---

## Architecture Principles

### Core Principles

1. **Single Responsibility Principle (SRP)**
   - Each class/service has one clear purpose
   - Example: MapPopupDataService (data access) vs MapPopupOrchestrator (coordination)

2. **Interface Segregation Principle (ISP)**
   - Define clear contracts through interfaces
   - Avoid "fat" interfaces with unused methods

3. **Dependency Inversion Principle (DIP)**
   - Depend on abstractions (interfaces), not concretions
   - Use dependency injection throughout

4. **Open/Closed Principle (OCP)**
   - Open for extension, closed for modification
   - Use composition and interfaces for extensibility

5. **Naming Clarity**
   - Names should be descriptive and unambiguous
   - Avoid generic suffixes like "Manager" or "Helper"
   - Use role-based naming (Repository, Service, Orchestrator, etc.)

### Design Patterns

**Used in Project:**
- **Repository Pattern** - Data access abstraction (MapPopupDataService)
- **Orchestrator Pattern** - Coordination of multiple services (MapPopupOrchestrator)
- **Event-Driven Architecture** - Decoupled communication via EventBus
- **Dependency Injection** - Constructor injection throughout
- **Cache Pattern** - ConcurrentDictionary for O(1) lookups
- **Service Locator** - IServiceProvider for runtime service resolution

---

## Code Quality Standards

### Naming Conventions

#### Services
- `*DataService` - Data access and caching (e.g., MapPopupDataService)
- `*Orchestrator` - Coordinates multiple services (e.g., MapPopupOrchestrator)
- `*Manager` - Lifecycle management (e.g., SceneManager)
- `*Factory` - Object creation (e.g., PlayerFactory)
- `*Registry` - Collection management (e.g., PopupRegistry)

#### Interfaces
- Prefix with `I` (e.g., IMapPopupDataService)
- Match implementing class name (e.g., IMapPopupDataService → MapPopupDataService)

#### Classes
- PascalCase for public members
- camelCase for private fields with `_` prefix
- Descriptive, specific names (avoid generic terms)

### Documentation Standards

#### XML Documentation
All public APIs must have:
- `<summary>` - Brief description
- `<param>` - Parameter descriptions
- `<returns>` - Return value description
- `<remarks>` - Additional context (optional)
- `<exception>` - Thrown exceptions (if applicable)

#### Code Comments
- Explain **why**, not **what**
- Use for complex logic or non-obvious decisions
- Keep comments up-to-date with code changes

---

## Performance Guidelines

### Hot Paths
Optimize for:
- Map transitions (happens frequently during gameplay)
- Rendering loops (60 FPS requirement)
- Input handling (immediate response needed)

### Caching Strategy
- Use `ConcurrentDictionary` for thread-safe caches
- Preload frequently accessed data
- Provide O(1) lookups for hot paths
- Clear cache when data changes

### Memory Management
- Minimize allocations in hot paths
- Use object pooling for frequently created objects
- Dispose of IDisposable resources properly
- Avoid closures that capture large objects

---

## Testing Strategy

### Test Levels

1. **Unit Tests**
   - Test individual services in isolation
   - Mock dependencies
   - Fast execution (< 1ms per test)

2. **Integration Tests**
   - Test service interactions
   - Use real dependencies where possible
   - Verify data flow through layers

3. **System Tests**
   - Test complete workflows
   - Verify end-to-end functionality
   - Include performance benchmarks

### Coverage Goals
- Critical paths: 100%
- Services: 80%+
- Overall: 70%+

---

## Change Management

### Breaking Changes

**Definition:** Changes that require updates to existing code

**Process:**
1. Document in ADR with BREAKING CHANGE section
2. Create migration guide
3. Update all internal usages
4. Notify external consumers (if any)
5. Provide deprecation period (if feasible)

**Example:** MapPopupService rename requires code changes

### Non-Breaking Changes

**Definition:** Additions or internal refactorings

**Process:**
1. Implement and test
2. Update documentation
3. No migration guide needed

---

## Future Work

### Planned Improvements
- [ ] Add unit tests for all services
- [ ] Create architecture diagrams for all major systems
- [ ] Document ECS component relationships
- [ ] Add performance benchmarks to CI/CD

### Under Consideration
- [ ] Introduce domain events for better decoupling
- [ ] Add feature flags for experimental features
- [ ] Implement health checks for services
- [ ] Add telemetry for performance monitoring

---

## Contributing

### Adding New ADRs
1. Copy template from existing ADR
2. Assign next sequential number (ADR-XXX)
3. Fill in all sections completely
4. Submit for architecture team review
5. Update this README with link

### Updating Architecture Docs
1. Keep diagrams synchronized with code
2. Update version numbers and dates
3. Add migration notes for breaking changes
4. Link related documents

---

## Resources

### Internal
- [Features Documentation](../features/) - Feature specifications
- [Bug Fixes Documentation](../bugfixes/) - Bug fix details
- [Project README](../../README.md) - Project overview

### External
- [C4 Model](https://c4model.com/) - Architecture diagramming
- [ADR Process](https://adr.github.io/) - Architecture Decision Records
- [SOLID Principles](https://en.wikipedia.org/wiki/SOLID) - Object-oriented design
- [.NET Design Guidelines](https://learn.microsoft.com/en-us/dotnet/standard/design-guidelines/) - Microsoft best practices

---

## Glossary

### Terms

**ADR (Architecture Decision Record)**
- Document capturing important architectural decisions and their rationale

**ECS (Entity Component System)**
- Game architecture pattern separating data (components) from behavior (systems)

**Hot Path**
- Code executed frequently during normal gameplay (needs optimization)

**Service Locator**
- Pattern for runtime service resolution (use sparingly, prefer DI)

**FQN (Fully Qualified Name)**
- Complete namespace path to a type (e.g., `MonoBallFramework.Game.GameData.Services.MapPopupService`)

**DI (Dependency Injection)**
- Design pattern for providing dependencies via constructor/property injection

---

**Last Updated:** December 5, 2025
**Maintained By:** Architecture Team
**Contact:** See project README for contact information
