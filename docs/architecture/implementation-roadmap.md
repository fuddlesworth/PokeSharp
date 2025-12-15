# Implementation Roadmap: Base Game as Mod

## Document Overview

This roadmap defines the step-by-step implementation plan for converting PokeSharp's base game content into a mod-based architecture. It includes task breakdown, milestones, dependencies, and success criteria.

**Status**: Planning Phase
**Estimated Duration**: 4 weeks
**Team Size**: 1-2 developers

---

## Phase 1: Foundation (Week 1)

### Milestone: Core Infrastructure Ready

**Objective**: Implement ContentProvider and establish mod-aware content resolution.

### Tasks

#### 1.1 Define IContentProvider Interface
**Estimated Time**: 2 hours
**Priority**: Critical

```csharp
// File: Engine/Content/IContentProvider.cs

public interface IContentProvider
{
    string? ResolveContentPath(string contentType, string relativePath);
    IEnumerable<string> GetAllContentPaths(string contentType, string pattern = "*.json");
    bool ContentExists(string contentType, string relativePath);
    string? GetContentSource(string contentType, string relativePath);
    void InvalidateCache(string? contentType = null);
}
```

**Acceptance Criteria**:
- Interface compiles without errors
- XML documentation complete
- Follows project naming conventions

**Dependencies**: None

---

#### 1.2 Implement ContentProvider Class
**Estimated Time**: 8 hours
**Priority**: Critical

**Files to Create**:
- `Engine/Content/ContentProvider.cs`
- `Engine/Content/ContentProviderOptions.cs`
- `Engine/Content/ContentResolution.cs`

**Implementation Checklist**:
- [ ] Constructor with dependency injection
- [ ] ResolveContentPath with priority search
- [ ] LRU cache implementation
- [ ] GetModsByPriority helper
- [ ] TryResolveInMod helper
- [ ] TryResolveInBaseGame helper
- [ ] SanitizeRelativePath for security
- [ ] GetAllContentPaths implementation
- [ ] ContentExists implementation
- [ ] GetContentSource implementation
- [ ] InvalidateCache implementation
- [ ] Comprehensive logging

**Acceptance Criteria**:
- All methods implemented and documented
- Security checks for path traversal
- Cache hit rate >90% in tests
- No breaking changes to existing code

**Dependencies**: 1.1 (Interface definition)

---

#### 1.3 Create Base Game Manifest
**Estimated Time**: 2 hours
**Priority**: Critical

**File to Create**: `MonoBallFramework.Game/Assets/mod.json`

```json
{
  "id": "base:pokesharp-core",
  "name": "PokeSharp Core Content",
  "author": "PokeSharp Team",
  "version": "1.0.0",
  "description": "Base game content for PokeSharp - Pokemon-style game framework",
  "priority": 1000,
  "contentFolders": {
    "Definitions": "Definitions",
    "Graphics": "Graphics",
    "Audio": "Audio",
    "Scripts": "Scripts",
    "Fonts": "Fonts",
    "Tiled": "Tiled",
    "Tilesets": "Tilesets"
  },
  "scripts": [],
  "patches": [],
  "dependencies": []
}
```

**Acceptance Criteria**:
- Valid JSON syntax
- All content folders mapped
- Version follows semantic versioning
- Validates with ModManifest.Validate()

**Dependencies**: None

---

#### 1.4 Unit Tests for ContentProvider
**Estimated Time**: 6 hours
**Priority**: High

**Test Files**:
- `Tests/Engine/Content/ContentProviderTests.cs`
- `Tests/Engine/Content/ContentProviderCacheTests.cs`
- `Tests/Engine/Content/ContentProviderSecurityTests.cs`

**Test Coverage**:
- [ ] ResolveContentPath with base game only
- [ ] ResolveContentPath with mod override
- [ ] ResolveContentPath with multiple mods (priority)
- [ ] ResolveContentPath with cache hit
- [ ] ResolveContentPath with cache miss
- [ ] GetAllContentPaths across mods
- [ ] ContentExists checks
- [ ] GetContentSource verification
- [ ] InvalidateCache functionality
- [ ] Path traversal prevention
- [ ] Invalid content type handling
- [ ] Null/empty parameter handling

**Acceptance Criteria**:
- Test coverage >90%
- All edge cases covered
- Performance tests included
- Security tests passing

**Dependencies**: 1.2 (ContentProvider implementation)

---

#### 1.5 Documentation
**Estimated Time**: 4 hours
**Priority**: Medium

**Documents to Create/Update**:
- `docs/content-provider-guide.md` - Developer guide
- `docs/api/IContentProvider.md` - API reference
- XML documentation in code

**Content**:
- Usage examples
- Integration patterns
- Best practices
- Migration guide for existing code

**Acceptance Criteria**:
- All public APIs documented
- Code examples compile and run
- Clear migration instructions

**Dependencies**: 1.2, 1.4

---

### Week 1 Deliverables

- [ ] IContentProvider interface complete
- [ ] ContentProvider implementation complete
- [ ] Base game manifest created
- [ ] Unit tests passing (>90% coverage)
- [ ] Documentation published

**Exit Criteria**:
- All unit tests passing
- Code review approved
- Documentation complete
- No critical bugs

---

## Phase 2: Integration (Week 2)

### Milestone: Content System Integrated

**Objective**: Integrate ContentProvider into AssetManager and GameDataLoader.

### Tasks

#### 2.1 Update ModLoader for Base Game
**Estimated Time**: 4 hours
**Priority**: Critical

**Changes to `Engine/Core/Modding/ModLoader.cs`**:

```csharp
/// <summary>
/// Loads the base game as a special mod.
/// Called before loading user mods.
/// </summary>
public async Task LoadBaseGameModAsync(string baseGameRoot)
{
    string manifestPath = Path.Combine(baseGameRoot, "mod.json");

    if (!File.Exists(manifestPath))
    {
        _logger.LogWarning("Base game mod.json not found at {Path}", manifestPath);
        await CreateDefaultBaseManifestAsync(manifestPath);
    }

    ModManifest? manifest = ParseManifest(manifestPath, baseGameRoot);
    if (manifest != null)
    {
        _loadedMods[manifest.Id] = manifest;
        _logger.LogInformation("Loaded base game mod: {Id} v{Version}",
            manifest.Id, manifest.Version);
    }
}

private async Task CreateDefaultBaseManifestAsync(string path)
{
    // Create default manifest if missing
}
```

**Acceptance Criteria**:
- LoadBaseGameModAsync works correctly
- Base game mod appears in LoadedMods
- Priority system respects base game
- Backward compatible

**Dependencies**: Phase 1 complete

---

#### 2.2 Integrate ContentProvider into AssetManager
**Estimated Time**: 6 hours
**Priority**: Critical

**Changes to `Engine/Rendering/Assets/AssetManager.cs`**:

```csharp
private readonly IContentProvider _contentProvider;

public AssetManager(
    GraphicsDevice graphicsDevice,
    IContentProvider contentProvider,
    ILogger<AssetManager>? logger = null)
{
    _graphicsDevice = graphicsDevice;
    _contentProvider = contentProvider;
    _logger = logger;
}

public void LoadTexture(string id, string relativePath)
{
    if (HasTexture(id)) return;

    // Resolve path through mod system
    string? fullPath = _contentProvider.ResolveContentPath("Graphics", relativePath);

    // Fallback for backward compatibility
    if (fullPath == null)
    {
        fullPath = Path.Combine(AssetRoot, relativePath);
        _logger?.LogWarning(
            "Content not found in mods, using legacy path: {Path}",
            relativePath);
    }

    if (!File.Exists(fullPath))
    {
        throw new FileNotFoundException($"Texture not found: {relativePath}");
    }

    // Existing loading logic...
}
```

**Acceptance Criteria**:
- All texture loading uses ContentProvider
- Backward compatibility maintained
- Performance not degraded
- Cache working correctly

**Dependencies**: 2.1

---

#### 2.3 Integrate ContentProvider into GameDataLoader
**Estimated Time**: 6 hours
**Priority**: Critical

**Changes to `GameData/Loading/GameDataLoader.cs`**:

```csharp
private readonly IContentProvider _contentProvider;

public GameDataLoader(
    GameDataContext context,
    IContentProvider contentProvider,
    ILogger<GameDataLoader> logger)
{
    _context = context;
    _contentProvider = contentProvider;
    _logger = logger;
}

public async Task LoadAllAsync(string dataPath, CancellationToken ct)
{
    _logger.LogGameDataLoadingStarted(dataPath);

    // Load from all mods in priority order, then base game
    var allMapFiles = _contentProvider.GetAllContentPaths(
        "Definitions",
        "Maps/Regions/*.json");

    foreach (string file in allMapFiles)
    {
        await LoadMapEntityAsync(file, ct);
    }

    // Similar for other definition types...
}
```

**Acceptance Criteria**:
- All definition loading uses ContentProvider
- Mods can override base definitions
- EF Core correctly handles overwrites
- No duplicate data in database

**Dependencies**: 2.1

---

#### 2.4 Update Initialization Pipeline
**Estimated Time**: 4 hours
**Priority**: Critical

**Changes to `Initialization/Pipeline/GameInitializer.cs`**:

```csharp
public async Task InitializeAsync()
{
    // 1. Load base game as mod
    await _modLoader.LoadBaseGameModAsync(_gameRoot + "/Assets");

    // 2. Load user mods
    await _modLoader.LoadModsAsync();

    // 3. Create ContentProvider
    var contentProvider = new ContentProvider(
        _modLoader,
        _logger,
        _options.Value);

    _services.AddSingleton<IContentProvider>(contentProvider);

    // 4. Initialize systems (now using ContentProvider)
    await _gameDataLoader.LoadAllAsync(/* ... */);

    // ...
}
```

**Acceptance Criteria**:
- Initialization sequence correct
- Base game loads before user mods
- ContentProvider available to all systems
- No breaking changes to startup

**Dependencies**: 2.1, 2.2, 2.3

---

#### 2.5 Integration Tests
**Estimated Time**: 8 hours
**Priority**: High

**Test Files**:
- `Tests/Integration/ContentProviderIntegrationTests.cs`
- `Tests/Integration/ModOverrideIntegrationTests.cs`
- `Tests/Integration/AssetManagerIntegrationTests.cs`

**Test Scenarios**:
- [ ] Load base game content successfully
- [ ] Load user mod content successfully
- [ ] Mod overrides base texture
- [ ] Mod overrides base definition
- [ ] Mod adds new content
- [ ] Multiple mods with priority ordering
- [ ] Hot reload scenario
- [ ] Cache invalidation on mod reload
- [ ] Performance benchmarks

**Acceptance Criteria**:
- All integration tests passing
- Performance within 10% of baseline
- No memory leaks
- Resource cleanup verified

**Dependencies**: 2.1, 2.2, 2.3, 2.4

---

### Week 2 Deliverables

- [ ] ModLoader supports base game
- [ ] AssetManager integrated with ContentProvider
- [ ] GameDataLoader integrated with ContentProvider
- [ ] Initialization pipeline updated
- [ ] Integration tests passing

**Exit Criteria**:
- All integration tests passing
- Manual testing successful
- Performance benchmarks met
- Code review approved

---

## Phase 3: Testing & Refinement (Week 3)

### Milestone: Production-Ready System

**Objective**: Comprehensive testing, performance optimization, and bug fixes.

### Tasks

#### 3.1 Scenario Testing
**Estimated Time**: 8 hours
**Priority**: Critical

**Test Scenarios**:

1. **Base Game Only**
   - No mods installed
   - All content loads from Assets/
   - Game functions normally

2. **Single Mod Override**
   - Mod overrides player sprite
   - Verify correct texture loads
   - Base game sprite not used

3. **Multiple Mods**
   - 3+ mods with different priorities
   - Verify priority order respected
   - Highest priority mod wins

4. **Content Addition**
   - Mod adds new region
   - New content accessible
   - No conflicts with base

5. **Partial Override**
   - Mod overrides some tiles, not all
   - Mix of mod and base content works
   - No missing textures

6. **Hot Reload**
   - Edit mod file while game running
   - Reload mod
   - Changes reflected immediately

7. **Error Recovery**
   - Invalid mod manifest
   - Missing mod files
   - Circular dependencies
   - Game continues with valid mods

**Acceptance Criteria**:
- All scenarios documented
- Test results recorded
- Issues logged and tracked
- Pass rate >95%

**Dependencies**: Phase 2 complete

---

#### 3.2 Performance Profiling
**Estimated Time**: 8 hours
**Priority**: High

**Metrics to Measure**:
- ContentProvider.ResolveContentPath (cold)
- ContentProvider.ResolveContentPath (cached)
- ModLoader.LoadModsAsync startup time
- Memory usage with 0/5/10/20 mods
- Cache hit rate over 1000 resolutions
- Texture loading time (mod vs base)

**Tools**:
- BenchmarkDotNet for microbenchmarks
- dotMemory for memory profiling
- Visual Studio Profiler for CPU profiling

**Target Metrics**:
- Cold resolution: <10ms per call
- Cached resolution: <0.1ms per call
- Cache hit rate: >90%
- Startup overhead: <100ms for 10 mods
- Memory overhead: <10MB for ContentProvider

**Acceptance Criteria**:
- All targets met
- Bottlenecks identified and optimized
- Profiling report documented

**Dependencies**: 3.1

---

#### 3.3 Security Audit
**Estimated Time**: 4 hours
**Priority**: High

**Security Checks**:
- [ ] Path traversal prevention (../)
- [ ] Rooted path rejection (C:/)
- [ ] Symlink handling
- [ ] Case sensitivity handling
- [ ] Null byte injection
- [ ] Unicode normalization attacks
- [ ] Resource exhaustion (huge files)
- [ ] Mod sandboxing boundaries

**Tools**:
- Static analysis (Roslyn analyzers)
- Security scanner (OWASP dependency check)
- Penetration testing

**Acceptance Criteria**:
- No critical vulnerabilities
- All paths sanitized
- Logging of suspicious activity
- Security test suite passing

**Dependencies**: 3.1

---

#### 3.4 Bug Fixes & Optimization
**Estimated Time**: 12 hours
**Priority**: High

**Focus Areas**:
- Fix issues found in 3.1 testing
- Optimize slow paths identified in 3.2
- Address security issues from 3.3
- Code cleanup and refactoring
- Edge case handling

**Acceptance Criteria**:
- All P0/P1 bugs fixed
- All P2 bugs triaged
- Performance targets met
- Code review approved

**Dependencies**: 3.1, 3.2, 3.3

---

#### 3.5 Documentation Updates
**Estimated Time**: 6 hours
**Priority**: Medium

**Documents to Update**:
- `docs/architecture/base-game-as-mod-architecture.md` - Final architecture
- `docs/modding-guide.md` - Mod developer guide
- `docs/content-provider-guide.md` - Usage guide
- `CHANGELOG.md` - Release notes

**Content**:
- Lessons learned
- Known limitations
- Migration guide updates
- Troubleshooting section
- FAQ

**Acceptance Criteria**:
- All documents up-to-date
- Examples tested and verified
- Clear migration instructions
- Troubleshooting guide complete

**Dependencies**: 3.4

---

### Week 3 Deliverables

- [ ] All scenario tests passing
- [ ] Performance benchmarks met
- [ ] Security audit complete
- [ ] All critical bugs fixed
- [ ] Documentation updated

**Exit Criteria**:
- Zero critical bugs
- Performance targets met
- Security vulnerabilities addressed
- Documentation complete
- Stakeholder approval

---

## Phase 4: Deprecation & Migration (Week 4)

### Milestone: Legacy Code Removed

**Objective**: Deprecate old hardcoded path methods and complete migration.

### Tasks

#### 4.1 Mark Legacy APIs as Obsolete
**Estimated Time**: 4 hours
**Priority**: Medium

**APIs to Deprecate**:

```csharp
[Obsolete("Use ContentProvider.ResolveContentPath instead. Will be removed in v2.0.")]
public void LoadTextureLegacy(string id, string hardcodedPath)
{
    // Legacy implementation
}
```

**Files to Update**:
- AssetManager.cs
- GameDataLoader.cs
- TypeRegistry.cs

**Acceptance Criteria**:
- All legacy methods marked [Obsolete]
- Deprecation warnings in logs
- Migration guide provided
- Deprecation timeline documented

**Dependencies**: Phase 3 complete

---

#### 4.2 Migrate Internal Code
**Estimated Time**: 8 hours
**Priority**: High

**Code to Migrate**:
- All internal AssetManager calls
- All internal GameDataLoader calls
- Map loading systems
- Audio loading systems
- Script loading systems

**Checklist**:
- [ ] Search for hardcoded "Assets/" paths
- [ ] Replace with ContentProvider calls
- [ ] Remove fallback logic
- [ ] Update tests
- [ ] Verify no regressions

**Acceptance Criteria**:
- No internal code uses deprecated APIs
- All tests passing
- No hardcoded paths in codebase
- Code review approved

**Dependencies**: 4.1

---

#### 4.3 Community Migration Support
**Estimated Time**: 6 hours
**Priority**: Medium

**Deliverables**:
- Migration guide for mod developers
- Example mods updated
- Video tutorial (optional)
- Forum/Discord announcements

**Migration Guide Content**:
- What changed and why
- Step-by-step migration instructions
- Code before/after examples
- Common issues and solutions
- Support contact information

**Acceptance Criteria**:
- Migration guide published
- Example mods updated
- Community notified
- Support channels ready

**Dependencies**: 4.1, 4.2

---

#### 4.4 Remove Deprecated Code
**Estimated Time**: 4 hours
**Priority**: Low

**Timeline**: After 3 months deprecation period

**Removal Checklist**:
- [ ] Remove [Obsolete] methods
- [ ] Remove legacy fallback paths
- [ ] Remove compatibility shims
- [ ] Update tests
- [ ] Update documentation

**Acceptance Criteria**:
- All deprecated code removed
- Tests still passing
- Documentation updated
- No breaking changes for migrated code

**Dependencies**: 4.1, 4.2, 4.3 + 3 months

---

#### 4.5 Final Release
**Estimated Time**: 4 hours
**Priority**: High

**Release Checklist**:
- [ ] Version bump (1.0.0 -> 2.0.0)
- [ ] CHANGELOG updated
- [ ] Release notes drafted
- [ ] Git tags created
- [ ] NuGet packages published (if applicable)
- [ ] GitHub release created

**Release Notes Content**:
- New features
- Breaking changes
- Migration guide link
- Performance improvements
- Bug fixes
- Known issues

**Acceptance Criteria**:
- Release published
- Documentation live
- Community notified
- Support ready

**Dependencies**: 4.1, 4.2, 4.3

---

### Week 4 Deliverables

- [ ] Legacy APIs deprecated
- [ ] Internal code migrated
- [ ] Migration guide published
- [ ] Deprecated code removed (after period)
- [ ] Final release published

**Exit Criteria**:
- Clean codebase (no deprecated code)
- Community successfully migrated
- Release stable
- Support issues resolved

---

## Risk Management

### High-Risk Items

| Risk | Probability | Impact | Mitigation |
|------|-------------|--------|------------|
| Performance regression | Medium | High | Comprehensive benchmarking, LRU cache |
| Breaking changes | Medium | High | Backward compatibility layer, gradual migration |
| Security vulnerabilities | Low | Critical | Security audit, path sanitization |
| Mod compatibility issues | Medium | Medium | Extensive testing, clear migration guide |
| Cache invalidation bugs | Low | Medium | Conservative invalidation, comprehensive tests |

### Contingency Plans

1. **Performance Regression**
   - Rollback to previous version
   - Optimize hot paths
   - Increase cache size

2. **Breaking Changes**
   - Extend deprecation period
   - Provide compatibility shims
   - Offer migration assistance

3. **Security Issues**
   - Immediate hotfix
   - Security advisory
   - Third-party audit

---

## Success Metrics

### Quantitative Metrics

- [ ] Test coverage >90%
- [ ] Performance within 10% of baseline
- [ ] Zero critical bugs in production
- [ ] Cache hit rate >90%
- [ ] Startup time <100ms overhead
- [ ] Memory usage <10MB overhead

### Qualitative Metrics

- [ ] Positive community feedback
- [ ] Successful mod migrations
- [ ] Clean, maintainable codebase
- [ ] Comprehensive documentation
- [ ] Team confidence in system

---

## Resource Requirements

### Development Resources

- 1-2 Senior Developers (full-time)
- 1 QA Engineer (part-time, weeks 2-3)
- 1 Technical Writer (part-time, weeks 1 & 3)

### Infrastructure

- Test environments for multiple OS (Windows, Linux, macOS)
- Performance testing hardware
- CI/CD pipeline updates

### Tools

- BenchmarkDotNet
- dotMemory Profiler
- Visual Studio Enterprise
- Static analysis tools

---

## Timeline Summary

```
Week 1: Foundation
├─ Day 1-2: IContentProvider interface & implementation
├─ Day 3-4: Unit tests & base manifest
└─ Day 5: Documentation

Week 2: Integration
├─ Day 1: ModLoader updates
├─ Day 2-3: AssetManager & GameDataLoader integration
├─ Day 4: Initialization pipeline
└─ Day 5: Integration tests

Week 3: Testing & Refinement
├─ Day 1-2: Scenario testing
├─ Day 3: Performance profiling
├─ Day 4: Security audit & bug fixes
└─ Day 5: Documentation updates

Week 4: Deprecation & Migration
├─ Day 1: Mark legacy APIs obsolete
├─ Day 2-3: Migrate internal code
├─ Day 4: Community migration support
└─ Day 5: Final release
```

---

## Approval & Sign-off

| Role | Name | Signature | Date |
|------|------|-----------|------|
| System Architect | TBD | __________ | ____ |
| Lead Developer | TBD | __________ | ____ |
| QA Lead | TBD | __________ | ____ |
| Project Manager | TBD | __________ | ____ |

---

## Appendix: Task Dependencies Graph

```
Phase 1 (Foundation)
┌─────────────┐
│ 1.1 Define  │
│ Interface   │
└──────┬──────┘
       │
       ▼
┌─────────────┐
│ 1.2 Implement│──┐
│ ContentProvider│ │
└──────┬──────┘  │
       │         │
       ▼         │
┌─────────────┐  │
│ 1.4 Unit    │◀─┘
│ Tests       │
└──────┬──────┘
       │
       ▼
┌─────────────┐
│ 1.5 Docs    │
└─────────────┘

Phase 2 (Integration)
┌──────────────┐
│ 2.1 Update   │
│ ModLoader    │
└──────┬───────┘
       │
       ├────────────────┐
       │                │
       ▼                ▼
┌──────────────┐  ┌──────────────┐
│ 2.2 Asset-   │  │ 2.3 GameData-│
│ Manager      │  │ Loader       │
└──────┬───────┘  └──────┬───────┘
       │                 │
       └────────┬────────┘
                │
                ▼
         ┌──────────────┐
         │ 2.4 Init     │
         │ Pipeline     │
         └──────┬───────┘
                │
                ▼
         ┌──────────────┐
         │ 2.5 Integration│
         │ Tests        │
         └──────────────┘

Phase 3 (Testing)
All tasks can run in parallel after Phase 2

Phase 4 (Deprecation)
Sequential: 4.1 → 4.2 → 4.3 → (wait 3 months) → 4.4 → 4.5
```
