# PokeSharp Modpack System Architecture

**Version:** 1.0.0
**Author:** PokeSharp Hive Mind - Architect Agent
**Date:** 2025-11-29
**Status:** Design Complete

---

## 1. Executive Summary

This document specifies the architecture for PokeSharp's modpack system, designed to load complete game implementations (like Pokemon Emerald) as self-contained modpacks. The system extends the existing JSON Patch-based modding infrastructure with event hooks, asset management, and scripting capabilities.

### Key Design Goals

1. **Self-Contained**: Each modpack is a complete, isolated package
2. **Event-Driven**: Deep integration with ECS event system
3. **Asset-Aware**: Comprehensive asset loading pipeline
4. **Script-Enabled**: Roslyn C# scripting (.csx) for behaviors
5. **Backwards Compatible**: Works with existing mod infrastructure

---

## 2. Architecture Overview

### 2.1 System Components

```
┌─────────────────────────────────────────────────────────────────┐
│                     PokeSharp Engine Core                        │
│                                                                   │
│  ┌──────────────┐   ┌──────────────┐   ┌──────────────┐        │
│  │   ModPack    │──▶│  EventHook   │──▶│   EventBus   │        │
│  │   Loader     │   │   System     │   │   (ECS)      │        │
│  └──────────────┘   └──────────────┘   └──────────────┘        │
│         │                   │                   │                │
│         ▼                   ▼                   ▼                │
│  ┌──────────────┐   ┌──────────────┐   ┌──────────────┐        │
│  │    Asset     │   │   Script     │   │   Template   │        │
│  │   Manager    │   │   Registry   │   │   System     │        │
│  └──────────────┘   └──────────────┘   └──────────────┘        │
│                                                                   │
└─────────────────────────────────────────────────────────────────┘
         │                        │                      │
         ▼                        ▼                      ▼
┌──────────────┐         ┌──────────────┐      ┌──────────────┐
│   Graphics   │         │     Audio    │      │     Maps     │
│   Pipeline   │         │   Pipeline   │      │   (Tiled)    │
└──────────────┘         └──────────────┘      └──────────────┘
```

### 2.2 Integration Points

| System Component | Integration Method | Data Flow |
|------------------|-------------------|-----------|
| **ModLoader** | Existing mod discovery | Scans `mods/` directory |
| **PatchApplicator** | JSON Patch (RFC 6902) | Patches template JSON |
| **EventBus** | Subscribe/Publish pattern | Event hook registration |
| **TemplateLoader** | JSON deserialization | Loads patched templates |
| **Roslyn Scripting** | .csx script compilation | Behavior scripts |

---

## 3. Modpack Manifest Schema

### 3.1 Complete Schema (modpack.json)

```json
{
  "$schema": "https://pokesharp.dev/schemas/modpack-v1.json",

  "metadata": {
    "modId": "official.pokemon-emerald",
    "name": "Pokemon Emerald",
    "version": "3.0.0",
    "author": "Game Freak / PokeSharp Team",
    "description": "Complete Pokemon Emerald implementation for PokeSharp",
    "homepage": "https://pokesharp.dev/mods/emerald",
    "license": "MIT",
    "tags": ["official", "gen3", "hoenn", "complete-game"],
    "engineVersion": ">=1.0.0",
    "apiVersion": "1.0"
  },

  "dependencies": {
    "required": [],
    "optional": ["community.enhanced-ui"],
    "loadAfter": [],
    "loadBefore": [],
    "incompatible": ["official.pokemon-ruby", "official.pokemon-sapphire"]
  },

  "loadPriority": 50,

  "assets": {
    "graphics": {
      "spritesheets": [
        {
          "id": "pokemon-sprites",
          "path": "assets/graphics/pokemon/sprites.png",
          "tileWidth": 64,
          "tileHeight": 64,
          "metadata": "assets/graphics/pokemon/sprites.json"
        },
        {
          "id": "trainer-sprites",
          "path": "assets/graphics/trainers/*.png",
          "glob": true
        }
      ],
      "tilesets": [
        {
          "id": "hoenn-tileset",
          "path": "assets/graphics/tilesets/hoenn.tsx",
          "format": "tiled"
        }
      ],
      "ui": [
        {
          "id": "battle-ui",
          "path": "assets/graphics/ui/battle/*.png"
        }
      ]
    },
    "audio": {
      "music": [
        {
          "id": "route101",
          "path": "assets/audio/music/route101.ogg",
          "loop": true
        }
      ],
      "sfx": [
        {
          "id": "pokemon-cry",
          "path": "assets/audio/sfx/cries/*.ogg",
          "glob": true
        }
      ]
    },
    "maps": {
      "worlds": [
        {
          "id": "hoenn",
          "path": "assets/maps/hoenn.tmx",
          "format": "tiled",
          "layers": ["terrain", "objects", "triggers"]
        }
      ]
    },
    "data": {
      "templates": "data/templates/**/*.json",
      "scripts": "scripts/**/*.csx",
      "localization": "data/localization/*.json"
    }
  },

  "patches": [
    "patches/pokemon-stats.patch.json",
    "patches/move-data.patch.json",
    "patches/type-chart.patch.json"
  ],

  "scripts": {
    "global": [
      "scripts/global/game-initialization.csx",
      "scripts/global/save-system.csx"
    ],
    "systems": [
      {
        "id": "battle-system",
        "path": "scripts/systems/battle/battle-controller.csx",
        "priority": 100,
        "dependencies": ["damage-calculator.csx"]
      },
      {
        "id": "wild-encounters",
        "path": "scripts/systems/encounters/wild-pokemon.csx",
        "priority": 50
      }
    ],
    "behaviors": [
      {
        "id": "pokemon-behavior",
        "path": "scripts/behaviors/pokemon/*.csx",
        "glob": true
      }
    ]
  },

  "events": {
    "hooks": [
      {
        "eventType": "BattleStartedEvent",
        "handler": "scripts/events/on-battle-start.csx",
        "priority": 100,
        "mode": "before"
      },
      {
        "eventType": "WildEncounterEvent",
        "handler": "scripts/events/on-wild-encounter.csx",
        "priority": 50,
        "mode": "after"
      },
      {
        "eventType": "DialogueRequestedEvent",
        "handler": "scripts/events/on-dialogue.csx",
        "priority": 0,
        "mode": "replace"
      }
    ],
    "customEvents": [
      {
        "name": "PokemonCaughtEvent",
        "namespace": "PokeSharp.Game.Events.Emerald",
        "properties": {
          "pokemonId": "string",
          "level": "int",
          "location": "string",
          "caught": "bool"
        }
      }
    ]
  },

  "dataOverrides": {
    "pokemon": "data/pokemon/*.json",
    "moves": "data/moves/*.json",
    "abilities": "data/abilities/*.json",
    "items": "data/items/*.json",
    "trainers": "data/trainers/*.json"
  },

  "configuration": {
    "features": {
      "physicalSpecialSplit": true,
      "fairyType": false,
      "megaEvolution": false,
      "battleFrontier": true
    },
    "settings": {
      "startingPokemon": ["treecko", "torchic", "mudkip"],
      "startingLocation": "littleroot-town",
      "playerName": "Brendan"
    }
  },

  "templates": {
    "contentFolders": {
      "pokemon": "data/templates/pokemon",
      "trainers": "data/templates/trainers",
      "items": "data/templates/items",
      "npcs": "data/templates/npcs",
      "triggers": "data/templates/triggers"
    },
    "baseTemplates": [
      "data/templates/base/pokemon-base.json",
      "data/templates/base/trainer-base.json"
    ]
  },

  "localization": {
    "defaultLocale": "en-US",
    "supported": ["en-US", "ja-JP", "es-ES", "fr-FR", "de-DE"],
    "textPath": "data/localization/{locale}/text.json"
  }
}
```

### 3.2 Schema Validation Rules

```csharp
// ModpackManifest.cs validation rules
public class ModpackManifest : ModManifest
{
    // Core metadata (extends ModManifest)
    public ModpackMetadata Metadata { get; set; }

    // Asset definitions
    public AssetManifest Assets { get; set; }

    // Script registrations
    public ScriptManifest Scripts { get; set; }

    // Event hook definitions
    public EventManifest Events { get; set; }

    // Data overrides
    public Dictionary<string, string> DataOverrides { get; set; }

    // Configuration
    public ModpackConfiguration Configuration { get; set; }

    // Template system integration
    public TemplateManifest Templates { get; set; }

    public override void Validate()
    {
        base.Validate(); // ModManifest validation

        // Validate metadata
        if (string.IsNullOrWhiteSpace(Metadata.ModId))
            throw new ValidationException("Metadata.ModId is required");

        // Validate version format (semantic versioning)
        if (!SemanticVersion.TryParse(Metadata.Version, out _))
            throw new ValidationException($"Invalid version format: {Metadata.Version}");

        // Validate engine compatibility
        if (!VersionRange.Parse(Metadata.EngineVersion).Includes(EngineVersion.Current))
            throw new ValidationException($"Incompatible engine version: {Metadata.EngineVersion}");

        // Validate asset paths exist
        ValidateAssetPaths();

        // Validate script files exist
        ValidateScriptPaths();

        // Validate event hook targets
        ValidateEventHooks();
    }
}
```

---

## 4. Directory Structure

### 4.1 Complete Modpack Layout

```
mods/pokemon-emerald/
├── modpack.json                      # Main manifest
├── README.md                         # Documentation
├── LICENSE                           # License file
├── CHANGELOG.md                      # Version history
│
├── assets/                           # All game assets
│   ├── graphics/
│   │   ├── pokemon/
│   │   │   ├── sprites.png           # Pokemon sprite sheet
│   │   │   ├── sprites.json          # Sprite metadata
│   │   │   └── icons.png             # Menu icons
│   │   ├── trainers/
│   │   │   ├── brendan.png
│   │   │   ├── may.png
│   │   │   └── gym-leaders/
│   │   ├── tilesets/
│   │   │   ├── hoenn.tsx             # Tiled tileset
│   │   │   ├── hoenn.png             # Tileset image
│   │   │   └── indoor.tsx
│   │   └── ui/
│   │       ├── battle/
│   │       │   ├── hud.png
│   │       │   └── menu.png
│   │       └── menus/
│   ├── audio/
│   │   ├── music/
│   │   │   ├── route101.ogg
│   │   │   ├── battle-wild.ogg
│   │   │   └── littleroot-town.ogg
│   │   └── sfx/
│   │       ├── cries/
│   │       │   ├── treecko.ogg
│   │       │   └── ...
│   │       └── effects/
│   │           ├── menu-select.ogg
│   │           └── pokeball-throw.ogg
│   └── maps/
│       ├── hoenn.tmx                 # Main world map
│       ├── routes/
│       │   ├── route101.tmx
│       │   └── ...
│       └── towns/
│           ├── littleroot-town.tmx
│           └── ...
│
├── data/                             # Game data
│   ├── templates/                    # ECS entity templates
│   │   ├── base/
│   │   │   ├── pokemon-base.json     # Base pokemon template
│   │   │   └── trainer-base.json     # Base trainer template
│   │   ├── pokemon/
│   │   │   ├── treecko.json
│   │   │   ├── torchic.json
│   │   │   └── ...
│   │   ├── trainers/
│   │   │   ├── professor-birch.json
│   │   │   ├── rival.json
│   │   │   └── gym-leaders/
│   │   ├── items/
│   │   │   ├── pokeball.json
│   │   │   └── ...
│   │   ├── npcs/
│   │   │   ├── pokemart-clerk.json
│   │   │   └── ...
│   │   └── triggers/
│   │       ├── wild-encounter-zone.json
│   │       └── story-event-trigger.json
│   ├── pokemon/                      # Pokemon data (non-template)
│   │   ├── species.json
│   │   ├── evolution.json
│   │   └── egg-groups.json
│   ├── moves/
│   │   ├── move-list.json
│   │   └── move-effects.json
│   ├── abilities/
│   │   └── ability-list.json
│   ├── items/
│   │   └── item-list.json
│   ├── trainers/
│   │   └── trainer-parties.json
│   └── localization/
│       ├── en-US/
│       │   ├── text.json
│       │   ├── pokemon-names.json
│       │   └── dialogue.json
│       └── ja-JP/
│
├── scripts/                          # C# scripts (.csx)
│   ├── global/
│   │   ├── game-initialization.csx   # Game startup
│   │   ├── save-system.csx           # Save/load
│   │   └── settings.csx              # Game settings
│   ├── systems/                      # ECS systems
│   │   ├── battle/
│   │   │   ├── battle-controller.csx
│   │   │   ├── damage-calculator.csx
│   │   │   ├── ai-controller.csx
│   │   │   └── status-effects.csx
│   │   ├── encounters/
│   │   │   ├── wild-pokemon.csx
│   │   │   └── encounter-table.csx
│   │   ├── movement/
│   │   │   ├── player-movement.csx
│   │   │   └── npc-movement.csx
│   │   └── ui/
│   │       ├── menu-controller.csx
│   │       └── dialogue-system.csx
│   ├── behaviors/                    # Entity behaviors
│   │   ├── pokemon/
│   │   │   ├── pokemon-behavior.csx
│   │   │   └── evolution-handler.csx
│   │   ├── trainers/
│   │   │   └── trainer-ai.csx
│   │   └── items/
│   │       └── item-effects.csx
│   └── events/                       # Event handlers
│       ├── on-battle-start.csx
│       ├── on-wild-encounter.csx
│       ├── on-dialogue.csx
│       └── on-pokemon-caught.csx
│
├── patches/                          # JSON Patches
│   ├── pokemon-stats.patch.json      # Modify base stats
│   ├── move-data.patch.json          # Modify move power/accuracy
│   ├── type-chart.patch.json         # Modify type effectiveness
│   └── evolution.patch.json          # Modify evolution methods
│
└── config/                           # Runtime configuration
    ├── feature-flags.json            # Toggle features
    ├── balance.json                  # Game balance tweaks
    └── debug.json                    # Debug settings
```

### 4.2 Asset Path Resolution

```csharp
public class ModpackAssetResolver
{
    private readonly LoadedModpack _modpack;

    public string ResolveAssetPath(string assetId, AssetType type)
    {
        // 1. Check modpack asset manifest
        if (_modpack.Manifest.Assets.TryGetAsset(assetId, type, out string? path))
        {
            return _modpack.ResolvePath(path);
        }

        // 2. Check content folders (templates)
        if (_modpack.Manifest.Templates.ContentFolders.TryGetValue(type.ToString(), out string? folder))
        {
            string potentialPath = Path.Combine(folder, $"{assetId}.json");
            if (File.Exists(_modpack.ResolvePath(potentialPath)))
            {
                return _modpack.ResolvePath(potentialPath);
            }
        }

        // 3. Check data overrides
        if (_modpack.Manifest.DataOverrides.TryGetValue(type.ToString(), out string? dataPath))
        {
            // Handle glob patterns
            if (dataPath.Contains("*"))
            {
                var matches = Directory.GetFiles(
                    _modpack.RootPath,
                    dataPath,
                    SearchOption.AllDirectories
                );
                return matches.FirstOrDefault(m => Path.GetFileNameWithoutExtension(m) == assetId);
            }
        }

        throw new AssetNotFoundException($"Asset not found: {assetId} ({type})");
    }
}
```

---

## 5. Event Hook System

### 5.1 Event Hook Architecture

```
┌─────────────────────────────────────────────────────────────┐
│                    EventBus (ECS Core)                       │
│                                                               │
│  ┌──────────────┐   ┌──────────────┐   ┌──────────────┐    │
│  │   Publish    │──▶│   Subscribe  │──▶│   Handler    │    │
│  │   <TEvent>   │   │   <TEvent>   │   │   Execute    │    │
│  └──────────────┘   └──────────────┘   └──────────────┘    │
└─────────────────────────────────────────────────────────────┘
         │                        │
         ▼                        ▼
┌──────────────────┐    ┌──────────────────┐
│  EventHookMode   │    │  ScriptedHandler │
│  - Before        │    │  - Roslyn .csx   │
│  - After         │    │  - Hot Reload    │
│  - Replace       │    │  - Error Isolate │
│  - Intercept     │    │  - Async Support │
└──────────────────┘    └──────────────────┘
```

### 5.2 Event Hook Registration

```csharp
public class EventHookRegistry
{
    private readonly IEventBus _eventBus;
    private readonly ScriptExecutor _scriptExecutor;
    private readonly ILogger<EventHookRegistry> _logger;

    // Track all hook subscriptions for cleanup
    private readonly List<IDisposable> _subscriptions = new();

    public void RegisterHooks(LoadedModpack modpack)
    {
        foreach (var hookDef in modpack.Manifest.Events.Hooks)
        {
            try
            {
                RegisterEventHook(modpack, hookDef);
                _logger.LogInformation(
                    "Registered event hook: {EventType} -> {Handler} ({Mode})",
                    hookDef.EventType,
                    hookDef.Handler,
                    hookDef.Mode
                );
            }
            catch (Exception ex)
            {
                _logger.LogError(
                    ex,
                    "Failed to register event hook: {EventType} in modpack {ModId}",
                    hookDef.EventType,
                    modpack.Manifest.Metadata.ModId
                );
                throw;
            }
        }
    }

    private void RegisterEventHook(LoadedModpack modpack, EventHookDefinition hookDef)
    {
        // Load and compile script
        string scriptPath = modpack.ResolvePath(hookDef.Handler);
        var scriptHandler = _scriptExecutor.LoadScript(scriptPath);

        // Get event type via reflection
        Type eventType = ResolveEventType(hookDef.EventType);

        // Create subscription based on mode
        IDisposable subscription = hookDef.Mode switch
        {
            EventHookMode.Before => SubscribeBefore(eventType, scriptHandler, hookDef.Priority),
            EventHookMode.After => SubscribeAfter(eventType, scriptHandler, hookDef.Priority),
            EventHookMode.Replace => SubscribeReplace(eventType, scriptHandler),
            EventHookMode.Intercept => SubscribeIntercept(eventType, scriptHandler),
            _ => throw new NotSupportedException($"Hook mode not supported: {hookDef.Mode}")
        };

        _subscriptions.Add(subscription);
    }

    private IDisposable SubscribeBefore<TEvent>(
        Type eventType,
        CompiledScript handler,
        int priority
    ) where TEvent : TypeEventBase
    {
        return _eventBus.Subscribe<TEvent>(evt =>
        {
            try
            {
                // Execute script before original handlers
                var context = new EventContext<TEvent>
                {
                    Event = evt,
                    Priority = priority,
                    Mode = EventHookMode.Before
                };

                handler.Execute(context);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error in 'before' event hook for {EventType}", eventType.Name);
                // Don't propagate - isolate errors
            }
        });
    }

    // Similar implementations for After, Replace, Intercept...
}
```

### 5.3 Event Hook Definition

```csharp
public record EventHookDefinition
{
    /// <summary>
    /// Event type name (e.g., "BattleStartedEvent", "WildEncounterEvent")
    /// </summary>
    public required string EventType { get; init; }

    /// <summary>
    /// Path to script handler (relative to modpack root)
    /// </summary>
    public required string Handler { get; init; }

    /// <summary>
    /// Hook execution priority (higher = earlier)
    /// </summary>
    public int Priority { get; init; } = 0;

    /// <summary>
    /// Hook execution mode
    /// </summary>
    public EventHookMode Mode { get; init; } = EventHookMode.After;

    /// <summary>
    /// Whether this hook can be disabled by configuration
    /// </summary>
    public bool Optional { get; init; } = false;
}

public enum EventHookMode
{
    /// <summary>
    /// Execute before original handlers
    /// </summary>
    Before,

    /// <summary>
    /// Execute after original handlers
    /// </summary>
    After,

    /// <summary>
    /// Replace all original handlers (dangerous)
    /// </summary>
    Replace,

    /// <summary>
    /// Intercept event and optionally cancel propagation
    /// </summary>
    Intercept
}
```

### 5.4 Custom Event Definition

```csharp
public class CustomEventRegistry
{
    private readonly Dictionary<string, Type> _customEvents = new();

    public void RegisterCustomEvents(LoadedModpack modpack)
    {
        foreach (var eventDef in modpack.Manifest.Events.CustomEvents)
        {
            // Generate event class at runtime using Roslyn
            Type eventType = GenerateEventType(eventDef);
            _customEvents[eventDef.Name] = eventType;

            _logger.LogInformation(
                "Registered custom event: {EventName} in namespace {Namespace}",
                eventDef.Name,
                eventDef.Namespace
            );
        }
    }

    private Type GenerateEventType(CustomEventDefinition eventDef)
    {
        // Generate C# code for event class
        string code = $$"""
        using PokeSharp.Engine.Core.Types.Events;

        namespace {{eventDef.Namespace}};

        public record {{eventDef.Name}} : TypeEventBase
        {
            {{GenerateProperties(eventDef.Properties)}}
        }
        """;

        // Compile using Roslyn
        return RoslynCompiler.CompileType(code, eventDef.Name);
    }
}

public record CustomEventDefinition
{
    public required string Name { get; init; }
    public required string Namespace { get; init; }
    public Dictionary<string, string> Properties { get; init; } = new();
}
```

### 5.5 Event Hook Script Example

```csharp
// scripts/events/on-wild-encounter.csx
// Context is provided by the EventHookRegistry

using PokeSharp.Engine.Core.Types.Events;
using PokeSharp.Game.Events;

// This script runs AFTER WildEncounterEvent is published
var encounterEvent = (WildEncounterEvent)Context.Event;

// Log the encounter
Logger.LogInformation(
    "Wild {Pokemon} (Level {Level}) appeared at {Location}!",
    encounterEvent.PokemonId,
    encounterEvent.Level,
    encounterEvent.Location
);

// Modify encounter based on conditions
if (encounterEvent.Location.Contains("tall-grass"))
{
    // Increase level slightly in tall grass
    encounterEvent.Level += 2;
}

// Check for shiny (1/8192 chance in Emerald)
if (Random.Shared.Next(8192) == 0)
{
    encounterEvent.IsShiny = true;
    Logger.LogInformation("✨ SHINY POKEMON!");
}

// Store encounter in player data
PlayerData.AddEncounter(new EncounterRecord
{
    PokemonId = encounterEvent.PokemonId,
    Level = encounterEvent.Level,
    Location = encounterEvent.Location,
    Timestamp = DateTime.UtcNow
});
```

---

## 6. Asset Loading Pipeline

### 6.1 Asset Loading Flow

```
Modpack Load
     │
     ├─▶ Graphics Assets
     │   ├─▶ Spritesheets → Texture Atlas
     │   ├─▶ Tilesets → Tiled Integration
     │   └─▶ UI Elements → FNA Resources
     │
     ├─▶ Audio Assets
     │   ├─▶ Music → Streaming Buffer
     │   └─▶ SFX → Memory Buffer
     │
     ├─▶ Map Assets
     │   └─▶ Tiled Maps → ECS Entities
     │
     └─▶ Data Assets
         ├─▶ Templates → EntityTemplate Cache
         ├─▶ Scripts → Roslyn Compilation
         └─▶ Localization → Resource Dictionary
```

### 6.2 Asset Manager Implementation

```csharp
public class ModpackAssetManager
{
    private readonly Dictionary<string, Texture2D> _textures = new();
    private readonly Dictionary<string, SoundEffect> _sounds = new();
    private readonly Dictionary<string, EntityTemplate> _templates = new();
    private readonly ILogger<ModpackAssetManager> _logger;

    public async Task LoadAssetsAsync(LoadedModpack modpack, CancellationToken ct = default)
    {
        var manifest = modpack.Manifest.Assets;

        // Load graphics in parallel
        var graphicsTasks = new List<Task>
        {
            LoadSpritesheetsAsync(modpack, manifest.Graphics.Spritesheets, ct),
            LoadTilesetsAsync(modpack, manifest.Graphics.Tilesets, ct),
            LoadUIAssetsAsync(modpack, manifest.Graphics.UI, ct)
        };

        // Load audio in parallel
        var audioTasks = new List<Task>
        {
            LoadMusicAsync(modpack, manifest.Audio.Music, ct),
            LoadSFXAsync(modpack, manifest.Audio.SFX, ct)
        };

        // Load data assets
        var dataTasks = new List<Task>
        {
            LoadTemplatesAsync(modpack, manifest.Data.Templates, ct),
            LoadScriptsAsync(modpack, manifest.Data.Scripts, ct),
            LoadLocalizationAsync(modpack, manifest.Data.Localization, ct)
        };

        // Wait for all asset loading
        await Task.WhenAll(graphicsTasks.Concat(audioTasks).Concat(dataTasks));

        _logger.LogInformation(
            "Loaded modpack assets: {TextureCount} textures, {SoundCount} sounds, {TemplateCount} templates",
            _textures.Count,
            _sounds.Count,
            _templates.Count
        );
    }

    private async Task LoadSpritesheetsAsync(
        LoadedModpack modpack,
        List<SpritesheetDefinition> spritesheets,
        CancellationToken ct
    )
    {
        foreach (var sheet in spritesheets)
        {
            string fullPath = modpack.ResolvePath(sheet.Path);

            if (sheet.Glob)
            {
                // Load multiple files matching pattern
                var files = Directory.GetFiles(
                    Path.GetDirectoryName(fullPath)!,
                    Path.GetFileName(fullPath)
                );

                foreach (var file in files)
                {
                    await LoadTextureAsync(file, ct);
                }
            }
            else
            {
                await LoadTextureAsync(fullPath, ct);
            }
        }
    }

    private async Task LoadTextureAsync(string path, CancellationToken ct)
    {
        // Load using FNA GraphicsDevice
        using var stream = File.OpenRead(path);
        var texture = Texture2D.FromStream(GraphicsDevice, stream);

        string id = Path.GetFileNameWithoutExtension(path);
        _textures[id] = texture;

        _logger.LogDebug("Loaded texture: {Id} from {Path}", id, path);
    }
}
```

### 6.3 Asset Definitions

```csharp
public record AssetManifest
{
    public GraphicsAssets Graphics { get; init; } = new();
    public AudioAssets Audio { get; init; } = new();
    public MapAssets Maps { get; init; } = new();
    public DataAssets Data { get; init; } = new();
}

public record GraphicsAssets
{
    public List<SpritesheetDefinition> Spritesheets { get; init; } = new();
    public List<TilesetDefinition> Tilesets { get; init; } = new();
    public List<UIAssetDefinition> UI { get; init; } = new();
}

public record SpritesheetDefinition
{
    public required string Id { get; init; }
    public required string Path { get; init; }
    public int TileWidth { get; init; } = 16;
    public int TileHeight { get; init; } = 16;
    public string? Metadata { get; init; }
    public bool Glob { get; init; } = false;
}

public record AudioAssets
{
    public List<MusicDefinition> Music { get; init; } = new();
    public List<SFXDefinition> SFX { get; init; } = new();
}

public record MusicDefinition
{
    public required string Id { get; init; }
    public required string Path { get; init; }
    public bool Loop { get; init; } = true;
    public float Volume { get; init; } = 1.0f;
}
```

---

## 7. Script Registry System

### 7.1 Script Compilation Pipeline

```
.csx Files
    │
    ├─▶ Roslyn Compiler
    │   ├─▶ Parse CSX
    │   ├─▶ Resolve References
    │   ├─▶ Compile Assembly
    │   └─▶ Cache Compiled Script
    │
    └─▶ Script Executor
        ├─▶ Create Execution Context
        ├─▶ Inject Dependencies
        ├─▶ Execute Script
        └─▶ Handle Errors
```

### 7.2 Script Registry Implementation

```csharp
public class ModpackScriptRegistry
{
    private readonly Dictionary<string, CompiledScript> _compiledScripts = new();
    private readonly ScriptExecutor _executor;
    private readonly ILogger<ModpackScriptRegistry> _logger;

    public async Task LoadScriptsAsync(LoadedModpack modpack, CancellationToken ct = default)
    {
        var manifest = modpack.Manifest.Scripts;

        // Load global scripts first
        foreach (var scriptPath in manifest.Global)
        {
            await CompileAndRegisterAsync(modpack, scriptPath, ScriptType.Global, ct);
        }

        // Load system scripts with dependencies
        var systemsGraph = BuildDependencyGraph(manifest.Systems);
        foreach (var system in systemsGraph.TopologicalSort())
        {
            await CompileAndRegisterAsync(modpack, system.Path, ScriptType.System, ct);
        }

        // Load behavior scripts
        foreach (var behavior in manifest.Behaviors)
        {
            if (behavior.Glob)
            {
                await LoadGlobScriptsAsync(modpack, behavior.Path, ScriptType.Behavior, ct);
            }
            else
            {
                await CompileAndRegisterAsync(modpack, behavior.Path, ScriptType.Behavior, ct);
            }
        }
    }

    private async Task<CompiledScript> CompileAndRegisterAsync(
        LoadedModpack modpack,
        string scriptPath,
        ScriptType type,
        CancellationToken ct
    )
    {
        string fullPath = modpack.ResolvePath(scriptPath);
        string scriptId = GetScriptId(fullPath);

        // Check cache
        if (_compiledScripts.TryGetValue(scriptId, out var cached))
        {
            return cached;
        }

        // Read script source
        string source = await File.ReadAllTextAsync(fullPath, ct);

        // Compile using Roslyn
        var compiled = await _executor.CompileAsync(source, new ScriptCompileOptions
        {
            ScriptId = scriptId,
            SourcePath = fullPath,
            Type = type,
            References = GetScriptReferences(modpack),
            Imports = GetScriptImports()
        }, ct);

        // Cache compiled script
        _compiledScripts[scriptId] = compiled;

        _logger.LogInformation(
            "Compiled script: {ScriptId} ({Type}) from {Path}",
            scriptId,
            type,
            scriptPath
        );

        return compiled;
    }

    private string[] GetScriptReferences(LoadedModpack modpack)
    {
        return new[]
        {
            "PokeSharp.Engine.Core.dll",
            "PokeSharp.Game.dll",
            "Arch.dll",
            "Microsoft.Extensions.Logging.dll",
            // Add modpack-specific references
            ...modpack.Manifest.Scripts.References
        };
    }

    private string[] GetScriptImports()
    {
        return new[]
        {
            "System",
            "System.Collections.Generic",
            "System.Linq",
            "PokeSharp.Engine.Core",
            "PokeSharp.Engine.Core.Events",
            "PokeSharp.Game",
            "Arch.Core",
            "Microsoft.Extensions.Logging"
        };
    }
}
```

### 7.3 Script Execution Context

```csharp
public class ScriptExecutionContext
{
    // Core services
    public ILogger Logger { get; init; }
    public IEventBus EventBus { get; init; }
    public World World { get; init; }

    // Modpack-specific
    public LoadedModpack Modpack { get; init; }
    public ModpackConfiguration Config { get; init; }

    // Script metadata
    public string ScriptId { get; init; }
    public ScriptType Type { get; init; }

    // Runtime data
    public Dictionary<string, object> Variables { get; } = new();

    // Helper methods available to scripts
    public EntityTemplate GetTemplate(string templateId) =>
        Modpack.GetTemplate(templateId);

    public Texture2D GetTexture(string textureId) =>
        Modpack.GetTexture(textureId);

    public SoundEffect GetSound(string soundId) =>
        Modpack.GetSound(soundId);
}
```

### 7.4 Example System Script

```csharp
// scripts/systems/battle/battle-controller.csx
// This script implements the main battle loop

using PokeSharp.Game.Components;
using PokeSharp.Game.Events;

public class BattleController
{
    private readonly ILogger _logger;
    private readonly IEventBus _eventBus;
    private readonly World _world;

    public BattleController(ScriptExecutionContext context)
    {
        _logger = context.Logger;
        _eventBus = context.EventBus;
        _world = context.World;

        // Subscribe to battle events
        _eventBus.Subscribe<BattleStartedEvent>(OnBattleStarted);
        _eventBus.Subscribe<MoveSelectedEvent>(OnMoveSelected);
    }

    private void OnBattleStarted(BattleStartedEvent evt)
    {
        _logger.LogInformation("Battle started: {Player} vs {Opponent}",
            evt.PlayerTrainerId,
            evt.OpponentTrainerId
        );

        // Initialize battle state
        var battleEntity = _world.Create(
            new BattleState
            {
                Turn = 0,
                PlayerTrainer = evt.PlayerTrainerId,
                OpponentTrainer = evt.OpponentTrainerId,
                Phase = BattlePhase.SelectMove
            }
        );

        // Display battle UI
        _eventBus.Publish(new UIRequestedEvent
        {
            UIType = "BattleUI",
            EntityId = battleEntity
        });
    }

    private void OnMoveSelected(MoveSelectedEvent evt)
    {
        // Calculate damage using damage calculator script
        var damageScript = Context.GetScript("damage-calculator");
        int damage = damageScript.CalculateDamage(
            evt.AttackerEntity,
            evt.DefenderEntity,
            evt.MoveId
        );

        // Apply damage
        ref var health = ref _world.Get<Health>(evt.DefenderEntity);
        health.Current -= damage;

        _logger.LogInformation(
            "{Attacker} used {Move}! {Damage} damage dealt.",
            evt.AttackerEntity,
            evt.MoveId,
            damage
        );

        // Check for knockout
        if (health.Current <= 0)
        {
            _eventBus.Publish(new PokemonFaintedEvent
            {
                PokemonEntity = evt.DefenderEntity
            });
        }
    }
}

// Return instance for registration
return new BattleController(Context);
```

---

## 8. Integration with Existing Systems

### 8.1 Template System Integration

```csharp
public class ModpackTemplateLoader
{
    private readonly JsonTemplateLoader _jsonLoader;
    private readonly PatchApplicator _patchApplicator;
    private readonly TemplateCache _cache;

    public async Task LoadModpackTemplatesAsync(
        LoadedModpack modpack,
        CancellationToken ct = default
    )
    {
        // 1. Load all template JSON files into cache
        var templateJson = await _jsonLoader.LoadTemplateJsonAsync(
            modpack.ResolvePath("data/templates"),
            recursive: true,
            ct
        );

        // 2. Apply JSON patches from modpack
        foreach (var patchPath in modpack.Manifest.Patches)
        {
            var patch = await LoadPatchAsync(modpack.ResolvePath(patchPath), ct);
            templateJson = _patchApplicator.ApplyPatchToCache(templateJson, patch);
        }

        // 3. Apply patches from dependencies
        foreach (var dep in modpack.Dependencies)
        {
            foreach (var depPatch in dep.Manifest.Patches)
            {
                var patch = await LoadPatchAsync(dep.ResolvePath(depPatch), ct);
                templateJson = _patchApplicator.ApplyPatchToCache(templateJson, patch);
            }
        }

        // 4. Deserialize patched templates
        foreach (var (path, jsonNode) in templateJson)
        {
            var template = _jsonLoader.DeserializeTemplate(jsonNode, path);
            _cache.Add(template.TemplateId, template);
        }

        // 5. Resolve template inheritance
        ResolveTemplateInheritance();
    }

    private void ResolveTemplateInheritance()
    {
        // Build inheritance graph
        var graph = new Dictionary<string, List<string>>();

        foreach (var template in _cache.GetAll())
        {
            if (!string.IsNullOrEmpty(template.BaseTemplateId))
            {
                if (!graph.ContainsKey(template.BaseTemplateId))
                    graph[template.BaseTemplateId] = new();

                graph[template.BaseTemplateId].Add(template.TemplateId);
            }
        }

        // Apply inheritance (topological sort)
        foreach (var baseId in graph.Keys.TopologicalSort())
        {
            var baseTemplate = _cache.Get(baseId);

            foreach (var childId in graph[baseId])
            {
                var childTemplate = _cache.Get(childId);
                ApplyInheritance(baseTemplate, childTemplate);
            }
        }
    }

    private void ApplyInheritance(EntityTemplate baseTemplate, EntityTemplate childTemplate)
    {
        // Inherit components from base (child overrides)
        foreach (var baseComponent in baseTemplate.Components)
        {
            if (!childTemplate.HasComponent(baseComponent.ComponentType))
            {
                childTemplate.AddComponent(baseComponent);
            }
        }

        // Inherit custom properties
        if (baseTemplate.CustomProperties != null)
        {
            childTemplate.CustomProperties ??= new();

            foreach (var (key, value) in baseTemplate.CustomProperties)
            {
                if (!childTemplate.CustomProperties.ContainsKey(key))
                {
                    childTemplate.CustomProperties[key] = value;
                }
            }
        }
    }
}
```

### 8.2 ECS Event Integration

```csharp
public class ModpackEventIntegration
{
    private readonly EventHookRegistry _hookRegistry;
    private readonly CustomEventRegistry _customEventRegistry;
    private readonly IEventBus _eventBus;

    public void IntegrateModpackEvents(LoadedModpack modpack)
    {
        // 1. Register custom events
        _customEventRegistry.RegisterCustomEvents(modpack);

        // 2. Register event hooks
        _hookRegistry.RegisterHooks(modpack);

        // 3. Subscribe to modpack lifecycle events
        _eventBus.Subscribe<ModpackLoadedEvent>(evt =>
        {
            if (evt.ModpackId == modpack.Manifest.Metadata.ModId)
            {
                OnModpackLoaded(modpack);
            }
        });

        _eventBus.Subscribe<ModpackUnloadedEvent>(evt =>
        {
            if (evt.ModpackId == modpack.Manifest.Metadata.ModId)
            {
                OnModpackUnloaded(modpack);
            }
        });
    }

    private void OnModpackLoaded(LoadedModpack modpack)
    {
        // Execute global initialization scripts
        foreach (var scriptId in modpack.Manifest.Scripts.Global)
        {
            var script = modpack.GetCompiledScript(scriptId);
            script.Execute(new ScriptExecutionContext
            {
                Modpack = modpack,
                EventType = "ModpackLoaded"
            });
        }
    }

    private void OnModpackUnloaded(LoadedModpack modpack)
    {
        // Cleanup modpack resources
        _hookRegistry.UnregisterHooks(modpack);
        _customEventRegistry.UnregisterEvents(modpack);

        // Execute cleanup scripts if defined
        if (modpack.Manifest.Scripts.Cleanup != null)
        {
            foreach (var scriptId in modpack.Manifest.Scripts.Cleanup)
            {
                var script = modpack.GetCompiledScript(scriptId);
                script.Execute(new ScriptExecutionContext
                {
                    Modpack = modpack,
                    EventType = "ModpackUnloaded"
                });
            }
        }
    }
}
```

---

## 9. Example: Pokemon Emerald Modpack

### 9.1 Minimal modpack.json

```json
{
  "metadata": {
    "modId": "official.pokemon-emerald",
    "name": "Pokemon Emerald",
    "version": "3.0.0",
    "author": "PokeSharp Team",
    "description": "Complete Pokemon Emerald for PokeSharp",
    "engineVersion": ">=1.0.0"
  },

  "assets": {
    "graphics": {
      "spritesheets": [
        {
          "id": "pokemon-sprites",
          "path": "assets/graphics/pokemon/sprites.png",
          "tileWidth": 64,
          "tileHeight": 64
        }
      ],
      "tilesets": [
        {
          "id": "hoenn-tileset",
          "path": "assets/graphics/tilesets/hoenn.tsx"
        }
      ]
    },
    "audio": {
      "music": [
        {
          "id": "route101",
          "path": "assets/audio/music/route101.ogg"
        }
      ]
    },
    "maps": {
      "worlds": [
        {
          "id": "hoenn",
          "path": "assets/maps/hoenn.tmx"
        }
      ]
    }
  },

  "events": {
    "hooks": [
      {
        "eventType": "BattleStartedEvent",
        "handler": "scripts/events/on-battle-start.csx",
        "mode": "before"
      }
    ]
  },

  "templates": {
    "contentFolders": {
      "pokemon": "data/templates/pokemon",
      "trainers": "data/templates/trainers"
    }
  }
}
```

### 9.2 Example Template (Treecko)

```json
{
  "templateId": "pokemon/treecko",
  "name": "Treecko",
  "tag": "pokemon",
  "baseTemplateId": "pokemon/base",
  "version": "1.0.0",

  "components": [
    {
      "type": "PokemonData",
      "data": {
        "species": "treecko",
        "nationalDex": 252,
        "type1": "Grass",
        "type2": null,
        "baseHP": 40,
        "baseAttack": 45,
        "baseDefense": 35,
        "baseSpAtk": 65,
        "baseSpDef": 55,
        "baseSpeed": 70
      }
    },
    {
      "type": "Sprite",
      "data": {
        "texture": "pokemon-sprites",
        "frameIndex": 252,
        "width": 64,
        "height": 64
      }
    },
    {
      "type": "Moveset",
      "data": {
        "moves": ["pound", "leer", "absorb", "quick-attack"],
        "learnset": {
          "6": "absorb",
          "11": "quick-attack",
          "16": "pursuit"
        }
      },
      "scriptId": "pokemon-moveset"
    }
  ],

  "customProperties": {
    "genderRatio": 0.875,
    "eggGroups": ["Monster", "Dragon"],
    "evolutionLevel": 16,
    "evolutionTarget": "pokemon/grovyle"
  }
}
```

### 9.3 Example Patch (Balance Changes)

```json
{
  "target": "templates/pokemon/treecko.json",
  "description": "Buff Treecko stats for better early game",
  "operations": [
    {
      "op": "replace",
      "path": "/components/0/data/baseAttack",
      "value": 55
    },
    {
      "op": "replace",
      "path": "/components/0/data/baseSpeed",
      "value": 75
    },
    {
      "op": "add",
      "path": "/components/2/data/moves/-",
      "value": "bullet-seed"
    }
  ]
}
```

---

## 10. Implementation Roadmap

### 10.1 Phase 1: Core Infrastructure (Week 1-2)

**Tasks:**
1. Extend `ModManifest` → `ModpackManifest` with new schema
2. Implement `ModpackAssetManager` for asset loading
3. Create `EventHookRegistry` for event system integration
4. Update `ModLoader` to support modpacks

**Deliverables:**
- `ModpackManifest.cs`
- `ModpackAssetManager.cs`
- `EventHookRegistry.cs`
- `LoadedModpack.cs` (extends `LoadedMod`)

### 10.2 Phase 2: Asset Pipeline (Week 3-4)

**Tasks:**
1. Implement graphics asset loading (spritesheets, tilesets, UI)
2. Implement audio asset loading (music, SFX)
3. Implement map asset loading (Tiled integration)
4. Create asset resolver for path resolution

**Deliverables:**
- `GraphicsAssetLoader.cs`
- `AudioAssetLoader.cs`
- `MapAssetLoader.cs`
- `AssetResolver.cs`

### 10.3 Phase 3: Script System (Week 5-6)

**Tasks:**
1. Implement `ModpackScriptRegistry` for script compilation
2. Create `ScriptExecutionContext` for runtime injection
3. Implement hot reload for scripts
4. Add error handling and isolation

**Deliverables:**
- `ModpackScriptRegistry.cs`
- `ScriptExecutionContext.cs`
- `ScriptCompiler.cs`
- `ScriptExecutor.cs`

### 10.4 Phase 4: Event Hooks (Week 7-8)

**Tasks:**
1. Implement event hook modes (Before, After, Replace, Intercept)
2. Create `CustomEventRegistry` for runtime event generation
3. Add priority-based hook execution
4. Implement hook subscription management

**Deliverables:**
- `EventHookMode.cs`
- `CustomEventRegistry.cs`
- `EventHookDefinition.cs`
- Integration tests

### 10.5 Phase 5: Pokemon Emerald Implementation (Week 9-12)

**Tasks:**
1. Create Pokemon Emerald modpack structure
2. Convert Emerald assets to PokeSharp format
3. Implement core systems (battle, encounters, evolution)
4. Create templates for all 386 Gen 3 Pokemon
5. Implement Hoenn region maps

**Deliverables:**
- `mods/pokemon-emerald/` complete modpack
- 386+ entity templates
- 50+ map files
- Full battle system scripts

---

## 11. Testing Strategy

### 11.1 Unit Tests

```csharp
[Test]
public async Task ModpackManifest_Validates_Successfully()
{
    var manifest = LoadTestManifest("pokemon-emerald");
    Assert.DoesNotThrow(() => manifest.Validate());
}

[Test]
public async Task AssetManager_Loads_All_Assets()
{
    var modpack = LoadTestModpack("pokemon-emerald");
    var assetManager = new ModpackAssetManager();

    await assetManager.LoadAssetsAsync(modpack);

    Assert.That(assetManager.TextureCount, Is.GreaterThan(0));
    Assert.That(assetManager.SoundCount, Is.GreaterThan(0));
}

[Test]
public void EventHookRegistry_Registers_Hooks_Correctly()
{
    var modpack = LoadTestModpack("pokemon-emerald");
    var hookRegistry = new EventHookRegistry();

    hookRegistry.RegisterHooks(modpack);

    Assert.That(hookRegistry.HookCount, Is.EqualTo(modpack.Manifest.Events.Hooks.Count));
}
```

### 11.2 Integration Tests

```csharp
[Test]
public async Task Pokemon_Emerald_Loads_Completely()
{
    // Arrange
    var modLoader = CreateModLoader();
    var modpackLoader = new ModpackLoader(modLoader);

    // Act
    var modpack = await modpackLoader.LoadModpackAsync("pokemon-emerald");

    // Assert
    Assert.That(modpack.Manifest.Metadata.ModId, Is.EqualTo("official.pokemon-emerald"));
    Assert.That(modpack.Templates.Count, Is.GreaterThan(386)); // All Gen 3 Pokemon
    Assert.That(modpack.Scripts.Count, Is.GreaterThan(10)); // Core systems
    Assert.That(modpack.EventHooks.Count, Is.GreaterThan(5)); // Event hooks
}

[Test]
public async Task Battle_System_Executes_Correctly()
{
    // Arrange
    var world = CreateTestWorld();
    var modpack = await LoadModpack("pokemon-emerald");
    var battleController = modpack.GetScript("battle-controller");

    // Act
    var playerPokemon = SpawnPokemon(world, "treecko", level: 5);
    var enemyPokemon = SpawnPokemon(world, "poochyena", level: 3);

    var battleEvent = new BattleStartedEvent
    {
        PlayerPokemon = playerPokemon,
        EnemyPokemon = enemyPokemon
    };

    eventBus.Publish(battleEvent);

    // Assert
    Assert.That(world.Has<BattleState>(playerPokemon), Is.True);
}
```

---

## 12. Performance Considerations

### 12.1 Asset Loading Optimization

- **Parallel Loading**: Load asset categories in parallel (graphics, audio, data)
- **Lazy Loading**: Defer loading of non-critical assets
- **Asset Streaming**: Stream large audio files instead of loading fully
- **Texture Atlasing**: Combine spritesheets into single atlas

### 12.2 Script Compilation Caching

- **Persistent Cache**: Cache compiled scripts across sessions
- **Incremental Compilation**: Only recompile changed scripts
- **Hot Reload**: Support script hot-reload without full restart

### 12.3 Event Hook Performance

- **Priority Queues**: Execute hooks in priority order efficiently
- **Async Handlers**: Support async event handlers for I/O operations
- **Handler Pooling**: Reuse handler instances to reduce allocations

---

## 13. Security Considerations

### 13.1 Script Sandboxing

```csharp
public class ScriptSandbox
{
    // Restrict dangerous APIs
    private static readonly string[] ForbiddenNamespaces = new[]
    {
        "System.IO.File",           // No direct file access
        "System.Diagnostics.Process", // No process spawning
        "System.Reflection.Emit",   // No runtime code generation
        "System.Net.Http"           // No network access (use provided services)
    };

    public void ValidateScript(string source)
    {
        var tree = CSharpSyntaxTree.ParseText(source);
        var usings = tree.GetRoot()
            .DescendantNodes()
            .OfType<UsingDirectiveSyntax>()
            .Select(u => u.Name.ToString());

        foreach (var forbidden in ForbiddenNamespaces)
        {
            if (usings.Any(u => u.StartsWith(forbidden)))
            {
                throw new SecurityException($"Forbidden namespace: {forbidden}");
            }
        }
    }
}
```

### 13.2 Asset Validation

- **File Type Validation**: Only allow whitelisted file types
- **Size Limits**: Enforce maximum file sizes for assets
- **Malware Scanning**: Optional integration with antivirus APIs

---

## 14. Conclusion

This architecture provides a comprehensive, extensible foundation for loading Pokemon Emerald (and future games) as self-contained modpacks in PokeSharp. The system leverages existing infrastructure while adding powerful event hooks, asset management, and scripting capabilities.

### Key Features

✅ **Self-Contained Modpacks**: Complete game implementations as isolated packages
✅ **Event-Driven Architecture**: Deep ECS event system integration
✅ **Asset Pipeline**: Comprehensive graphics, audio, and map loading
✅ **Script System**: Roslyn C# scripting with hot reload
✅ **Template Integration**: Seamless integration with existing template system
✅ **Backwards Compatible**: Works with current mod infrastructure

### Next Steps

1. Review and approve architecture design
2. Begin Phase 1 implementation (Core Infrastructure)
3. Create test modpack for validation
4. Iterate based on feedback

---

**Document Status**: ✅ Complete
**Ready for Implementation**: ✅ Yes
**Estimated Timeline**: 12 weeks for full Pokemon Emerald modpack
