# PokeSharp.Scripting - Architecture Diagrams

This document contains detailed diagrams and visual representations of the PokeSharp.Scripting architecture.

## Table of Contents
1. [C4 Model Diagrams](#c4-model-diagrams)
2. [Sequence Diagrams](#sequence-diagrams)
3. [State Diagrams](#state-diagrams)
4. [Class Diagrams](#class-diagrams)
5. [Data Flow Diagrams](#data-flow-diagrams)

---

## 1. C4 Model Diagrams

### Level 1: System Context

```
┌─────────────────────────────────────────────────────────────┐
│                     System Context                          │
└─────────────────────────────────────────────────────────────┘

                      ┌──────────────┐
                      │  Developer   │
                      │   (Person)   │
                      └───────┬──────┘
                              │
                              │ Edits .cs files
                              │ Views notifications
                              ▼
              ┌───────────────────────────────┐
              │   PokeSharp.Scripting         │
              │   (Software System)           │
              │                               │
              │  - Compiles C# scripts        │
              │  - Hot-reloads on changes     │
              │  - Manages script lifecycle   │
              └───────────┬───────────────────┘
                          │
                          │ Provides scripts
                          │ Executes behaviors
                          ▼
              ┌───────────────────────────────┐
              │   PokeSharp Game Engine       │
              │   (Software System)           │
              │                               │
              │  - Runs ECS world             │
              │  - Ticks entities             │
              │  - Renders game               │
              └───────────────────────────────┘
```

### Level 2: Container Diagram

```
┌─────────────────────────────────────────────────────────────┐
│              PokeSharp.Scripting Containers                 │
└─────────────────────────────────────────────────────────────┘

┌────────────┐
│ Developer  │
└─────┬──────┘
      │ Edits
      │
      ▼
┌──────────────────────────────────────────────────────────┐
│  File System (.cs files)                                 │
│  [File Storage]                                          │
└─────────┬────────────────────────────────────────────────┘
          │ Watches
          │
          ▼
┌──────────────────────────────────────────────────────────┐
│  ScriptHotReloadService                                  │
│  [.NET Service]                                          │
│                                                          │
│  - Watches for file changes                              │
│  - Orchestrates hot-reload workflow                      │
│  - Manages rollback on failure                           │
└─────────┬────────────────────────────────────────────────┘
          │
          │ Uses
          ▼
┌──────────────────────────────────────────────────────────┐
│  Roslyn Compiler Services                                │
│  [External Library]                                      │
│                                                          │
│  - Compiles C# code at runtime                           │
│  - Provides syntax analysis                              │
│  - Generates IL and assemblies                           │
└─────────┬────────────────────────────────────────────────┘
          │
          │ Returns Types
          ▼
┌──────────────────────────────────────────────────────────┐
│  VersionedScriptCache                                    │
│  [In-Memory Cache]                                       │
│                                                          │
│  - Stores compiled script types                          │
│  - Maintains version history                             │
│  - Provides instant rollback                             │
└─────────┬────────────────────────────────────────────────┘
          │
          │ Provides Instances
          ▼
┌──────────────────────────────────────────────────────────┐
│  Game Engine (ECS World)                                 │
│  [External System]                                       │
│                                                          │
│  - Executes script behaviors                             │
│  - Manages entity lifecycle                              │
│  - Renders game state                                    │
└──────────────────────────────────────────────────────────┘
```

### Level 3: Component Diagram

```
┌───────────────────────────────────────────────────────────────────┐
│           ScriptHotReloadService (Component Breakdown)            │
└───────────────────────────────────────────────────────────────────┘

                    ┌────────────────────┐
                    │  IScriptWatcher    │
                    │  [Interface]       │
                    └─────────┬──────────┘
                              │
                    ┌─────────▼──────────┐
                    │  WatcherFactory    │
                    │  [Factory]         │
                    └─────────┬──────────┘
                              │ Creates
                ┌─────────────┴─────────────┐
                │                           │
      ┌─────────▼──────────┐      ┌────────▼──────────┐
      │ FileSystemWatcher  │      │  PollingWatcher   │
      │ Adapter            │      │                   │
      │ [Concrete]         │      │  [Concrete]       │
      └─────────┬──────────┘      └────────┬──────────┘
                │                           │
                └─────────────┬─────────────┘
                              │ Fires Changed Event
                              ▼
                ┌──────────────────────────────┐
                │ ScriptHotReloadService       │
                │ [Orchestrator]               │
                │                              │
                │ - ProcessScriptChangeAsync() │
                │ - Debouncing logic           │
                │ - SemaphoreSlim lock         │
                └─────────┬────────────────────┘
                          │
        ┌─────────────────┼─────────────────┐
        │                 │                 │
        ▼                 ▼                 ▼
┌───────────────┐ ┌──────────────┐ ┌───────────────┐
│ScriptBackup   │ │IScriptCompiler│ │VersionedScript│
│Manager        │ │[Interface]    │ │Cache          │
│[Backup Store] │ │               │ │[Version Store]│
└───────────────┘ └──────────────┘ └───────────────┘
```

---

## 2. Sequence Diagrams

### Hot-Reload Success Flow

```
Developer   FileSystem   IScriptWatcher   HotReloadService   IScriptCompiler   VersionedCache   Entity
    │            │              │                │                  │                │           │
    │ Edit       │              │                │                  │                │           │
    │ Pikachu.cs │              │                │                  │                │           │
    ├───────────►│              │                │                  │                │           │
    │            │              │                │                  │                │           │
    │            │ File Changed │                │                  │                │           │
    │            ├─────────────►│                │                  │                │           │
    │            │              │                │                  │                │           │
    │            │              │ Changed Event  │                  │                │           │
    │            │              ├───────────────►│                  │                │           │
    │            │              │                │                  │                │           │
    │            │              │                │ Cancel Old       │                │           │
    │            │              │                │ Debouncer (CTS)  │                │           │
    │            │              │                │ ─ ─ ─ ─ ─ ─ ─ ─ ─│                │           │
    │            │              │                │                  │                │           │
    │            │              │                │ Wait 300ms       │                │           │
    │            │              │                │ (Debounce)       │                │           │
    │            │              │                │ ─ ─ ─ ─ ─ ─ ─ ─ ─│                │           │
    │            │              │                │                  │                │           │
    │            │              │                │ Acquire Semaphore│                │           │
    │            │              │                │ (Async Lock)     │                │           │
    │            │              │                │ ─ ─ ─ ─ ─ ─ ─ ─ ─│                │           │
    │            │              │                │                  │                │           │
    │            │              │                │ CreateBackup()   │                │           │
    │            │              │                │ ──────────────────────────────────►│           │
    │            │              │                │                  │                │           │
    │            │              │                │ CompileScriptAsync()              │           │
    │            │              │                ├─────────────────►│                │           │
    │            │              │                │                  │                │           │
    │            │              │                │                  │ Read File      │           │
    │            │              │                │                  │ Roslyn Compile │           │
    │            │              │                │                  │ ─ ─ ─ ─ ─ ─ ─ ─│           │
    │            │              │                │                  │                │           │
    │            │              │                │◄─────────────────┤                │           │
    │            │              │                │ CompilationResult│                │           │
    │            │              │                │ (Success, Type)  │                │           │
    │            │              │                │                  │                │           │
    │            │              │                │ UpdateVersion(typeId, newType)    │           │
    │            │              │                ├──────────────────────────────────►│           │
    │            │              │                │                  │                │           │
    │            │              │                │                  │    version++   │           │
    │            │              │                │                  │    Link prev   │           │
    │            │              │                │                  │    ─ ─ ─ ─ ─ ─ │           │
    │            │              │                │                  │                │           │
    │            │              │                │◄──────────────────────────────────┤           │
    │            │              │                │     New version = 2               │           │
    │            │              │                │                  │                │           │
    │            │              │                │ ShowNotification()                │           │
    │            │              │                │ (Success, 150ms) │                │           │
    │            │              │                │ ─ ─ ─ ─ ─ ─ ─ ─ ─│                │           │
    │            │              │                │                  │                │           │
    │            │              │                │ Release Semaphore│                │           │
    │            │              │                │ ─ ─ ─ ─ ─ ─ ─ ─ ─│                │           │
    │            │              │                │                  │                │           │
    │            │              │                │                  │                │ OnTick()  │
    │            │              │                │                  │                │◄──────────│
    │            │              │                │                  │                │           │
    │            │              │                │ GetOrCreateInstance("Pikachu")    │           │
    │            │              │                │◄──────────────────────────────────┤           │
    │            │              │                │                  │                │           │
    │            │              │                │                  │  Lazy Init     │           │
    │            │              │                │                  │  new Type()    │           │
    │            │              │                │                  │  ─ ─ ─ ─ ─ ─ ─ │           │
    │            │              │                │                  │                │           │
    │            │              │                │  New Instance ──────────────────►│           │
    │            │              │                │                  │                │           │
    │            │              │                │                  │                │ Execute   │
    │            │              │                │                  │                │ New Code! │
```

### Hot-Reload Failure with Rollback

```
Developer   FileSystem   HotReloadService   IScriptCompiler   VersionedCache   BackupManager
    │            │              │                  │                │               │
    │ Save Bad   │              │                  │                │               │
    │ Code       │              │                  │                │               │
    ├───────────►│              │                  │                │               │
    │            │              │                  │                │               │
    │            │ Changed      │                  │                │               │
    │            ├─────────────►│                  │                │               │
    │            │              │                  │                │               │
    │            │              │ Acquire Semaphore│                │               │
    │            │              │ ─ ─ ─ ─ ─ ─ ─ ─ ─│                │               │
    │            │              │                  │                │               │
    │            │              │ CreateBackup(typeId, oldType, v1) │               │
    │            │              ├───────────────────────────────────────────────────►│
    │            │              │                  │                │               │
    │            │              │ CompileScriptAsync()              │               │
    │            │              ├─────────────────►│                │               │
    │            │              │                  │                │               │
    │            │              │                  │ Syntax Error!  │               │
    │            │              │                  │ ─ ─ ─ ─ ─ ─ ─ ─│               │
    │            │              │                  │                │               │
    │            │              │◄─────────────────┤                │               │
    │            │              │ CompilationResult│                │               │
    │            │              │ (Success=false)  │                │               │
    │            │              │                  │                │               │
    │            │              │ Rollback(typeId) │                │               │
    │            │              ├──────────────────────────────────►│               │
    │            │              │                  │                │               │
    │            │              │                  │  current.PreviousVersion       │
    │            │              │                  │  Swap pointer  │               │
    │            │              │                  │  ─ ─ ─ ─ ─ ─ ─ │               │
    │            │              │                  │                │               │
    │            │              │◄──────────────────────────────────┤               │
    │            │              │     true (success)                │               │
    │            │              │                  │                │               │
    │            │              │ ShowNotification()                │               │
    │            │              │ (Warning, rolled back to v1)      │               │
    │            │              │ ─ ─ ─ ─ ─ ─ ─ ─ ─│                │               │
    │            │              │                  │                │               │
    │◄──────────────────────────┤                  │                │               │
    │ "⚠ Reload failed,         │                  │                │               │
    │  rolled back to v1"       │                  │                │               │
```

### Lazy Instantiation Flow

```
Entity      VersionedCache      ScriptCacheEntry
  │               │                    │
  │ GetOrCreate   │                    │
  │ Instance      │                    │
  │ ("Pikachu")   │                    │
  ├──────────────►│                    │
  │               │                    │
  │               │ TryGetValue        │
  │               │ ("Pikachu")        │
  │               │ ─ ─ ─ ─ ─ ─ ─ ─ ─ ─│
  │               │                    │
  │               │    Entry Found     │
  │               │◄ ─ ─ ─ ─ ─ ─ ─ ─ ─ │
  │               │                    │
  │               │ GetOrCreateInstance()
  │               ├───────────────────►│
  │               │                    │
  │               │                    │ lock(_instanceLock)
  │               │                    │ if (instance == null)
  │               │                    │   instance = new()
  │               │                    │ ─ ─ ─ ─ ─ ─ ─ ─ ─ ─
  │               │                    │
  │               │◄───────────────────┤
  │               │   instance         │
  │               │                    │
  │◄──────────────┤                    │
  │  instance     │                    │
  │               │                    │
  │ Execute       │                    │
  │ OnTick()      │                    │
  │ ─ ─ ─ ─ ─ ─ ─ │                    │
```

---

## 3. State Diagrams

### Script Lifecycle States

```
                    ┌────────────┐
                    │  Not Loaded│
                    │  (Initial) │
                    └──────┬─────┘
                           │
                           │ LoadScriptAsync()
                           │
                           ▼
                    ┌────────────┐
                    │  Compiled  │
                    │ (In Cache) │
                    └──────┬─────┘
                           │
                           │ GetOrCreateInstance()
                           │
                           ▼
                    ┌────────────┐
               ┌────┤Instantiated│◄────┐
               │    │  (Active)  │     │
               │    └──────┬─────┘     │
               │           │           │
               │           │ OnTick()  │
               │           │ (Repeat)  │
               │           │           │
               │           └───────────┘
               │
               │ Hot-Reload Success
               │
               ▼
        ┌────────────┐
        │  Compiled  │
        │ (New Ver.) │
        └──────┬─────┘
               │
               │ Next Tick
               │
               ▼
        ┌────────────┐
        │Instantiated│
        │ (New Ver.) │
        └────────────┘


        Hot-Reload Failure:

        ┌────────────┐
        │ Compile    │
        │  Failed    │
        └──────┬─────┘
               │
               │ Rollback()
               │
               ▼
        ┌────────────┐
        │  Compiled  │
        │ (Old Ver.) │ ◄── Previous version restored
        └──────┬─────┘
               │
               │ Continue execution
               │
               ▼
        ┌────────────┐
        │Instantiated│
        │ (Old Ver.) │ ◄── No disruption!
        └────────────┘
```

### Watcher State Machine

```
        ┌─────────┐
        │ Stopped │
        │(Initial)│
        └────┬────┘
             │
             │ StartAsync()
             │
             ▼
        ┌─────────┐
        │Starting │
        │         │
        └────┬────┘
             │
             │ Initialization complete
             │
             ▼
        ┌─────────┐          ┌───────┐
        │ Running │◄─────────┤ Error │
        │         │  Recover │(Temp) │
        └────┬────┘          └───┬───┘
             │                   │
             │ StopAsync()       │ Critical Error
             │                   │
             ▼                   ▼
        ┌─────────┐          ┌───────┐
        │Stopping │          │ Error │
        │         │          │(Fatal)│
        └────┬────┘          └───────┘
             │
             │ Cleanup complete
             │
             ▼
        ┌─────────┐
        │ Stopped │
        └─────────┘
```

---

## 4. Class Diagrams

### Core Components Class Diagram

```
┌──────────────────────────────────────────────────────────────┐
│                      TypeScriptBase                          │
├──────────────────────────────────────────────────────────────┤
│ # OnInitialize(ctx: ScriptContext): void                     │
│ + OnActivated(ctx: ScriptContext): void                      │
│ + OnTick(ctx: ScriptContext, deltaTime: float): void         │
│ + OnDeactivated(ctx: ScriptContext): void                    │
│ # GetDirectionTo(from: Point, to: Point): Direction          │
│ # ShowMessage(ctx: ScriptContext, message: string): void     │
│ # Random(): float                                            │
└──────────────────────────────────────────────────────────────┘
                            △
                            │ inherits
                            │
        ┌───────────────────┴───────────────────┐
        │                                       │
┌───────────────────┐               ┌───────────────────┐
│ WanderBehavior    │               │ ChaseBehavior     │
│ (User Script)     │               │ (User Script)     │
├───────────────────┤               ├───────────────────┤
│ + OnTick()        │               │ + OnTick()        │
└───────────────────┘               └───────────────────┘


┌──────────────────────────────────────────────────────────────┐
│                      ScriptContext                           │
├──────────────────────────────────────────────────────────────┤
│ + World: World                                               │
│ + Entity: Entity?                                            │
│ + Logger: ILogger                                            │
│ + IsEntityScript: bool                                       │
│ + IsGlobalScript: bool                                       │
├──────────────────────────────────────────────────────────────┤
│ + GetState<T>(): ref T                                       │
│ + TryGetState<T>(out state: T): bool                         │
│ + GetOrAddState<T>(): ref T                                  │
│ + HasState<T>(): bool                                        │
│ + RemoveState<T>(): bool                                     │
│ + Position: ref Position                                     │
│ + HasPosition: bool                                          │
└──────────────────────────────────────────────────────────────┘


┌──────────────────────────────────────────────────────────────┐
│                      ScriptService                           │
├──────────────────────────────────────────────────────────────┤
│ - _scriptCache: ConcurrentDictionary<string, (Script, Type)> │
│ - _scriptInstances: ConcurrentDictionary<string, object>     │
│ - _defaultOptions: ScriptOptions                             │
│ - _logger: ILogger                                           │
├──────────────────────────────────────────────────────────────┤
│ + LoadScriptAsync(scriptPath: string): Task<object?>         │
│ + ReloadScriptAsync(scriptPath: string): Task<object?>       │
│ + GetScriptInstance(scriptPath: string): object?             │
│ + InitializeScript(instance, world, entity, logger): void    │
│ + IsScriptLoaded(scriptPath: string): bool                   │
│ + ClearCache(): void                                         │
└──────────────────────────────────────────────────────────────┘
```

### Hot-Reload Subsystem Class Diagram

```
┌──────────────────────────────────────────────────────────────┐
│                 ScriptHotReloadService                       │
├──────────────────────────────────────────────────────────────┤
│ - _watcher: IScriptWatcher?                                  │
│ - _compiler: IScriptCompiler                                 │
│ - _cache: VersionedScriptCache                               │
│ - _backupManager: ScriptBackupManager                        │
│ - _notificationService: IHotReloadNotificationService        │
│ - _reloadSemaphore: SemaphoreSlim                            │
│ - _debouncers: ConcurrentDictionary<string, CTS>             │
│ - _statistics: HotReloadStatistics                           │
├──────────────────────────────────────────────────────────────┤
│ + StartAsync(directory, cancellationToken): Task             │
│ + StopAsync(): Task                                          │
│ + GetStatistics(): HotReloadStatistics                       │
│ - OnScriptChanged(sender, e): void                           │
│ - ProcessScriptChangeAsync(e): Task                          │
│ - ExtractTypeId(filePath): string                            │
└──────────────────────────────────────────────────────────────┘
                    │
                    │ uses
                    ▼
┌──────────────────────────────────────────────────────────────┐
│                  VersionedScriptCache                        │
├──────────────────────────────────────────────────────────────┤
│ - _cache: ConcurrentDictionary<string, ScriptCacheEntry>     │
│ - _currentVersion: int                                       │
│ - _versionLock: object                                       │
├──────────────────────────────────────────────────────────────┤
│ + CurrentVersion: int                                        │
│ + CachedScriptCount: int                                     │
├──────────────────────────────────────────────────────────────┤
│ + UpdateVersion(typeId, type): int                           │
│ + GetOrCreateInstance(typeId): object                        │
│ + GetInstance(typeId): (int, object?)                        │
│ + GetVersion(typeId): int                                    │
│ + Rollback(typeId): bool                                     │
│ + Contains(typeId): bool                                     │
│ + ClearInstance(typeId): bool                                │
│ + Remove(typeId): bool                                       │
│ + GetVersionHistoryDepth(typeId): int                        │
└──────────────────────────────────────────────────────────────┘
                    │
                    │ contains
                    ▼
┌──────────────────────────────────────────────────────────────┐
│                   ScriptCacheEntry                           │
├──────────────────────────────────────────────────────────────┤
│ - _instanceLock: object                                      │
│ - _instance: object?                                         │
├──────────────────────────────────────────────────────────────┤
│ + Version: int                                               │
│ + ScriptType: Type                                           │
│ + Instance: object?                                          │
│ + LastUpdated: DateTime                                      │
│ + PreviousVersion: ScriptCacheEntry?                         │
│ + IsInstantiated: bool                                       │
├──────────────────────────────────────────────────────────────┤
│ + GetOrCreateInstance(): object                              │
│ + ClearInstance(): void                                      │
│ + Clone(newVersion): ScriptCacheEntry                        │
└──────────────────────────────────────────────────────────────┘


┌──────────────────────────────────────────────────────────────┐
│                  <<interface>>                               │
│                  IScriptWatcher                              │
├──────────────────────────────────────────────────────────────┤
│ + Status: WatcherStatus                                      │
│ + CpuOverheadPercent: double                                 │
│ + ReliabilityScore: int                                      │
├──────────────────────────────────────────────────────────────┤
│ + Changed: event EventHandler<ScriptChangedEventArgs>        │
│ + Error: event EventHandler<ScriptWatcherErrorEventArgs>     │
│ + StartAsync(directory, filter, ct): Task                    │
│ + StopAsync(): Task                                          │
└──────────────────────────────────────────────────────────────┘
                    △
                    │ implements
        ┌───────────┴────────────┐
        │                        │
┌───────────────────┐    ┌───────────────────┐
│FileSystemWatcher  │    │  PollingWatcher   │
│    Adapter        │    │                   │
├───────────────────┤    ├───────────────────┤
│ - _fsw: FileSystem│    │ - _pollInterval   │
│   Watcher         │    │ - _lastModified   │
└───────────────────┘    └───────────────────┘
```

---

## 5. Data Flow Diagrams

### Compilation Data Flow

```
┌─────────────────────────────────────────────────────────────────┐
│                    Compilation Pipeline                         │
└─────────────────────────────────────────────────────────────────┘

.cs File                 Source Code              CompilationResult
(Disk)                   (Memory)                 (Object)
   │                         │                          │
   │                         │                          │
   ▼                         ▼                          ▼
┌──────┐   Read File   ┌──────────┐   Compile   ┌─────────────┐
│ I/O  ├──────────────►│  Roslyn  ├────────────►│   Result    │
│Layer │               │ Compiler │             │   Object    │
└──────┘               └──────────┘             └──────┬──────┘
                                                       │
                                                       │
                                    ┌──────────────────┴──────────────────┐
                                    │                                     │
                                    ▼                                     ▼
                            ┌───────────────┐                    ┌───────────────┐
                            │   Success     │                    │    Failure    │
                            │   Path        │                    │    Path       │
                            └───────┬───────┘                    └───────┬───────┘
                                    │                                    │
                                    │ Type                               │ Errors
                                    │                                    │
                                    ▼                                    ▼
                        ┌───────────────────┐              ┌────────────────────┐
                        │VersionedScriptCache│            │   Rollback         │
                        │  UpdateVersion()   │            │   Strategy         │
                        └───────────────────┘              └────────────────────┘
```

### Entity-Script Data Flow

```
┌─────────────────────────────────────────────────────────────────┐
│              Entity Tick → Script Execution                     │
└─────────────────────────────────────────────────────────────────┘

Game Loop           Type System           Cache Layer         Script Instance
    │                    │                      │                    │
    │                    │                      │                    │
    │ Tick Entity        │                      │                    │
    │ with TypeId        │                      │                    │
    ├────────────────────┼─────────────────────►│                    │
    │                    │                      │                    │
    │                    │                      │ GetOrCreate        │
    │                    │                      │ Instance(typeId)   │
    │                    │                      ├───────────────────►│
    │                    │                      │                    │
    │                    │                      │                    │ Lazy init
    │                    │                      │                    │ if needed
    │                    │                      │                    │ ─ ─ ─ ─ ─
    │                    │                      │                    │
    │                    │                      │◄───────────────────┤
    │                    │                      │   Instance         │
    │                    │                      │                    │
    │◄───────────────────┼──────────────────────┤                    │
    │    Instance        │                      │                    │
    │                    │                      │                    │
    │                    │ Build ScriptContext  │                    │
    │                    │ (World, Entity, Log) │                    │
    │                    │ ─ ─ ─ ─ ─ ─ ─ ─ ─ ─ ─│                    │
    │                    │                      │                    │
    │ OnTick(ctx, dt)    │                      │                    │
    ├────────────────────┼──────────────────────┼───────────────────►│
    │                    │                      │                    │
    │                    │                      │                    │ Execute
    │                    │                      │                    │ User Code
    │                    │                      │                    │ ─ ─ ─ ─ ─
    │                    │                      │                    │
    │                    │ ctx.GetState<T>()    │                    │
    │◄───────────────────┼──────────────────────┼────────────────────┤
    │                    │                      │                    │
    │ Component Access   │                      │                    │
    │ (ref Health, etc.) │                      │                    │
    │ ─ ─ ─ ─ ─ ─ ─ ─ ─ ─│                      │                    │
    │                    │                      │                    │
    │ Return to game loop│                      │                    │
    │◄───────────────────┼──────────────────────┼────────────────────┤
    │                    │                      │                    │
```

### Version History Chain

```
┌─────────────────────────────────────────────────────────────────┐
│                    Version Chain Structure                      │
└─────────────────────────────────────────────────────────────────┘

Cache Dictionary:
  "Pikachu" ──────────────────────────┐
                                      │
                                      ▼
                            ┌──────────────────┐
                            │ ScriptCacheEntry │
                            ├──────────────────┤
                            │ Version: 3       │
                            │ Type: v3_Type    │
                            │ Instance: null   │  ◄── Current (not yet used)
                            │ PreviousVersion ─┼──┐
                            └──────────────────┘  │
                                                  │
                                                  ▼
                                        ┌──────────────────┐
                                        │ ScriptCacheEntry │
                                        ├──────────────────┤
                                        │ Version: 2       │
                                        │ Type: v2_Type    │
                                        │ Instance: obj_v2 │ ◄── Last good (active)
                                        │ PreviousVersion ─┼──┐
                                        └──────────────────┘  │
                                                              │
                                                              ▼
                                                    ┌──────────────────┐
                                                    │ ScriptCacheEntry │
                                                    ├──────────────────┤
                                                    │ Version: 1       │
                                                    │ Type: v1_Type    │
                                                    │ Instance: null   │ ◄── Old (GC soon)
                                                    │ PreviousVersion: │
                                                    │        null      │
                                                    └──────────────────┘

On Rollback():
  "Pikachu" ──────────────────────────┐
                                      │
                                      ▼
                            ┌──────────────────┐
                            │ ScriptCacheEntry │
                            ├──────────────────┤
                            │ Version: 2       │
                            │ Type: v2_Type    │
                            │ Instance: obj_v2 │ ◄── Restored (instant swap)
                            │ PreviousVersion ─┼──┐
                            └──────────────────┘  │
                                                  │
                                                  ▼
                                        ┌──────────────────┐
                                        │ ScriptCacheEntry │
                                        ├──────────────────┤
                                        │ Version: 1       │
                                        │ Type: v1_Type    │
                                        │ Instance: null   │
                                        │ PreviousVersion: │
                                        │        null      │
                                        └──────────────────┘

v3_Type → Eligible for GC (no references)
```

### Concurrency Model

```
┌─────────────────────────────────────────────────────────────────┐
│                    Thread Interaction Model                     │
└─────────────────────────────────────────────────────────────────┘

    Game Thread          I/O Thread         Roslyn Thread
         │                    │                    │
         │                    │ File Changed       │
         │                    │ Event              │
         │                    ├────────┐           │
         │                    │        │           │
         │                    │    ┌───▼────────┐  │
         │                    │    │ Debounce   │  │
         │                    │    │ 300ms      │  │
         │                    │    └───┬────────┘  │
         │                    │        │           │
         │                    │        │           │
         │                    │    ┌───▼────────┐  │
         │                    │    │ Semaphore  │  │
         │                    │    │  .Wait()   │  │ ◄── Blocks if
         │                    │    └───┬────────┘      already
         │                    │        │               compiling
         │                    │        │           │
         │                    │        │  Compile  │
         │                    │        │  Request  │
         │                    │        ├──────────►│
         │                    │        │           │
         │                    │        │      ┌────▼────┐
         │                    │        │      │ Roslyn  │
         │                    │        │      │ Compile │
         │                    │        │      │(50-300ms│
         │                    │        │      └────┬────┘
         │                    │        │           │
         │                    │        │◄──────────┤
         │                    │        │   Type    │
         │                    │        │           │
         │                    │    ┌───▼────────┐  │
         │                    │    │ Update     │  │
         │                    │    │ Cache      │  │
         │                    │    └───┬────────┘  │
         │                    │        │           │
         │                    │    ┌───▼────────┐  │
         │                    │    │ Release    │  │
         │                    │    │ Semaphore  │  │
         │                    │    └────────────┘  │
         │                    │                    │
         │ OnTick()           │                    │
         ├────────┐           │                    │
         │        │           │                    │
         │    ┌───▼──────────────────┐             │
         │    │ GetOrCreateInstance  │             │
         │    │  (Lock-Free Read)    │             │
         │    └───┬──────────────────┘             │
         │        │                                │
         │◄───────┤                                │
         │  Instance                               │
         │                                         │

Note: Game thread never blocks on compilation!
      Cache reads are lock-free (ConcurrentDictionary)
      Only Instance creation uses fine-grained lock
```

---

## Summary

These diagrams provide comprehensive visual documentation of the PokeSharp.Scripting architecture:

- **C4 Model**: Multi-level system architecture (Context → Container → Component)
- **Sequence Diagrams**: Detailed interaction flows for hot-reload scenarios
- **State Diagrams**: Lifecycle and state machine representations
- **Class Diagrams**: Static structure and relationships
- **Data Flow Diagrams**: Information flow through the system

### Key Visual Insights

1. **Clean Separation**: Core components clearly separated by responsibility
2. **Dual Rollback**: Visual representation of cache → backup fallback
3. **Lazy Instantiation**: Shows deferred work pattern clearly
4. **Thread Safety**: Concurrency model with SemaphoreSlim and lock-free reads
5. **Version Chain**: Linked list structure for instant rollback

### Diagram Notation

- `─────►` Synchronous call/flow
- `─ ─ ─►` Asynchronous or internal operation
- `◄─────` Return value
- `│` Sequence progression
- `△` Inheritance relationship
- `◄►` Bidirectional association

---

**Document Version**: 1.0
**Last Updated**: 2025-01-06
**Maintained By**: Architecture Team
