# PokeSharp Modpack System - Quick Reference Guide

**For:** Developers implementing or using the modpack system
**Last Updated:** 2025-11-29

---

## 🚀 Quick Start

### Creating a Modpack

1. **Create directory structure:**
   ```bash
   mods/your-modpack/
   ├── modpack.json          # Required
   ├── assets/               # Optional
   ├── data/                 # Optional
   ├── scripts/              # Optional
   └── patches/              # Optional
   ```

2. **Create minimal modpack.json:**
   ```json
   {
     "metadata": {
       "modId": "your.modpack-name",
       "name": "Your Modpack",
       "version": "1.0.0",
       "author": "Your Name",
       "engineVersion": ">=1.0.0"
     }
   }
   ```

3. **Place in `mods/` directory** - PokeSharp will auto-discover it

---

## 📋 Modpack Manifest Schema

### Required Fields

```json
{
  "metadata": {
    "modId": "unique.identifier",      // Required: Unique mod ID
    "name": "Display Name",            // Required: Display name
    "version": "1.0.0",                // Required: Semantic version
    "engineVersion": ">=1.0.0"         // Required: Engine compatibility
  }
}
```

### Optional Fields

```json
{
  "metadata": {
    "author": "Author Name",
    "description": "Mod description",
    "homepage": "https://...",
    "license": "MIT",
    "tags": ["tag1", "tag2"]
  },
  "dependencies": {
    "required": ["other.mod.id"],
    "optional": ["optional.mod.id"],
    "incompatible": ["conflicting.mod.id"]
  },
  "loadPriority": 50,                 // Lower = earlier (default: 100)
  "assets": { /* ... */ },
  "scripts": { /* ... */ },
  "events": { /* ... */ },
  "patches": [ /* ... */ ],
  "templates": { /* ... */ },
  "configuration": { /* ... */ }
}
```

---

## 🎨 Asset Loading

### Graphics Assets

```json
{
  "assets": {
    "graphics": {
      "spritesheets": [
        {
          "id": "my-sprites",
          "path": "assets/graphics/sprites.png",
          "tileWidth": 64,
          "tileHeight": 64
        }
      ],
      "tilesets": [
        {
          "id": "my-tileset",
          "path": "assets/graphics/tileset.tsx",
          "format": "tiled"
        }
      ]
    }
  }
}
```

### Audio Assets

```json
{
  "assets": {
    "audio": {
      "music": [
        {
          "id": "background-music",
          "path": "assets/audio/music.ogg",
          "loop": true,
          "volume": 0.8
        }
      ],
      "sfx": [
        {
          "id": "sound-effect",
          "path": "assets/audio/sfx.ogg",
          "volume": 1.0
        }
      ]
    }
  }
}
```

### Map Assets

```json
{
  "assets": {
    "maps": {
      "worlds": [
        {
          "id": "main-world",
          "path": "assets/maps/world.tmx",
          "format": "tiled"
        }
      ]
    }
  }
}
```

---

## 📝 Entity Templates

### Basic Template Structure

```json
{
  "templateId": "category/entity-name",
  "name": "Display Name",
  "tag": "entity-type",
  "components": [
    {
      "type": "ComponentName",
      "data": {
        "property1": "value1",
        "property2": 123
      }
    }
  ]
}
```

### Template Inheritance

```json
{
  "templateId": "pokemon/pikachu",
  "baseTemplateId": "pokemon/base",  // Inherit from base
  "components": [
    // Only specify overrides/additions
  ]
}
```

---

## 🔧 Event Hooks

### Registering Event Hooks

```json
{
  "events": {
    "hooks": [
      {
        "eventType": "BattleStartedEvent",
        "handler": "scripts/events/on-battle-start.csx",
        "priority": 100,
        "mode": "before"
      }
    ]
  }
}
```

### Event Hook Modes

- **`before`** - Execute before original handlers
- **`after`** - Execute after original handlers (default)
- **`replace`** - Replace all original handlers
- **`intercept`** - Intercept and optionally cancel

### Writing Event Hook Scripts

```csharp
// scripts/events/on-battle-start.csx

using PokeSharp.Game.Events;

// Get the event
var battleEvent = (BattleStartedEvent)Context.Event;

// Access services
Context.Logger.LogInformation("Battle started!");
Context.EventBus.Publish(new CustomEvent());

// Query ECS world
var entities = Context.World.Query(
    new QueryDescription().WithAll<Component>()
);

// Access modpack resources
var texture = Context.Modpack.GetTexture("sprite-id");
var template = Context.Modpack.GetTemplate("template-id");
```

### Custom Events

```json
{
  "events": {
    "customEvents": [
      {
        "name": "MyCustomEvent",
        "namespace": "MyModpack.Events",
        "properties": {
          "propertyName": "string",
          "value": "int"
        }
      }
    ]
  }
}
```

---

## 🎯 Scripts

### Script Types

1. **Global Scripts** - Run once on modpack load
2. **System Scripts** - ECS systems (battle, movement, etc.)
3. **Behavior Scripts** - Entity behaviors (AI, interactions)

### Registering Scripts

```json
{
  "scripts": {
    "global": [
      "scripts/global/initialization.csx"
    ],
    "systems": [
      {
        "id": "battle-system",
        "path": "scripts/systems/battle.csx",
        "priority": 100,
        "dependencies": ["damage-calculator.csx"]
      }
    ],
    "behaviors": [
      {
        "id": "pokemon-behavior",
        "path": "scripts/behaviors/pokemon/*.csx",
        "glob": true
      }
    ]
  }
}
```

### Script Context

```csharp
// Available in all scripts:
Context.Logger        // ILogger for logging
Context.EventBus      // IEventBus for events
Context.World         // Arch.Core.World for ECS
Context.Modpack       // LoadedModpack instance
Context.Config        // ModpackConfiguration

// Helper methods:
Context.GetTemplate(id)
Context.GetTexture(id)
Context.GetSound(id)
```

---

## 🔨 JSON Patches

### Patch File Structure

```json
{
  "target": "templates/pokemon/pikachu.json",
  "description": "Buff Pikachu stats",
  "operations": [
    {
      "op": "replace",
      "path": "/components/0/data/baseAttack",
      "value": 65
    },
    {
      "op": "add",
      "path": "/components/-",
      "value": {
        "type": "NewComponent",
        "data": {}
      }
    }
  ]
}
```

### Patch Operations

- **`add`** - Add new property/element
- **`remove`** - Remove property/element
- **`replace`** - Replace existing value
- **`move`** - Move value to new location
- **`copy`** - Copy value to new location
- **`test`** - Test value matches expected

### JSON Pointer Paths

```json
"/components/0/data/baseAttack"     // First component, data property
"/moves/-"                          // Append to array
"/abilities/2"                      // Third element in array
```

---

## ⚙️ Configuration

### Feature Flags

```json
{
  "configuration": {
    "features": {
      "enableNewMechanics": true,
      "debugMode": false
    },
    "settings": {
      "difficulty": "normal",
      "expMultiplier": 1.0
    }
  }
}
```

### Accessing Configuration

```csharp
// In scripts:
if (Context.Config.Features["debugMode"])
{
    // Debug mode active
}

var difficulty = Context.Config.Settings["difficulty"];
```

---

## 🌍 Localization

### Defining Localization

```json
{
  "localization": {
    "defaultLocale": "en-US",
    "supported": ["en-US", "ja-JP", "es-ES"],
    "textPath": "data/localization/{locale}/text.json"
  }
}
```

### Localization Files

```json
// data/localization/en-US/text.json
{
  "battle.start": "Battle started!",
  "pokemon.name.pikachu": "Pikachu"
}
```

### Using Localization

```csharp
var text = Localization.Get("battle.start");
var pokemonName = Localization.Get($"pokemon.name.{species}");
```

---

## 🐛 Debugging

### Enable Debug Logging

```json
{
  "configuration": {
    "debug": {
      "enableVerboseLogging": true,
      "logScriptExecution": true,
      "logEventHooks": true
    }
  }
}
```

### Script Debugging

```csharp
// Use Context.Logger for debugging
Context.Logger.LogDebug("Debug message: {Value}", someValue);
Context.Logger.LogInformation("Info: {Message}", message);
Context.Logger.LogWarning("Warning: {Issue}", issue);
Context.Logger.LogError("Error: {Error}", error);
```

### Common Issues

1. **Modpack not loading**
   - Check `modpack.json` is valid JSON
   - Verify `metadata.modId` is unique
   - Check `metadata.engineVersion` compatibility

2. **Assets not found**
   - Verify file paths in manifest are correct
   - Check files exist in modpack directory
   - Use forward slashes `/` in paths

3. **Scripts failing**
   - Check script syntax (must be valid C#)
   - Verify `using` statements
   - Check forbidden namespace usage

4. **Event hooks not firing**
   - Verify `eventType` matches exactly
   - Check hook `priority` (higher = earlier)
   - Ensure handler script exists

---

## 📊 Performance Tips

### Asset Loading

- Use **glob patterns** for multiple files: `"assets/*.png"`
- Enable **lazy loading** for non-critical assets
- Use **texture atlasing** for many small sprites

### Script Optimization

- Minimize work in event hooks (high frequency)
- Cache frequently accessed data
- Use async operations for I/O

### Event System

- Higher priority = earlier execution (0-1000)
- Avoid expensive operations in `before` hooks
- Use `after` hooks when order doesn't matter

---

## 🔐 Security Guidelines

### Script Restrictions

**Forbidden Namespaces:**
- `System.IO.File` (use provided APIs)
- `System.Diagnostics.Process`
- `System.Reflection.Emit`
- `System.Net.Http` (use provided services)

### Safe Practices

✅ **DO:**
- Use Context services for file access
- Log errors appropriately
- Validate user input
- Handle exceptions gracefully

❌ **DON'T:**
- Access file system directly
- Spawn processes
- Generate code at runtime
- Make network requests

---

## 📚 Common Patterns

### Spawning Entities from Template

```csharp
var template = Context.GetTemplate("pokemon/pikachu");
var entity = template.Instantiate(Context.World);
```

### Publishing Events

```csharp
Context.EventBus.Publish(new BattleStartedEvent
{
    PlayerPokemon = playerEntity,
    EnemyPokemon = enemyEntity
});
```

### Querying ECS

```csharp
var query = Context.World.Query(
    new QueryDescription()
        .WithAll<PokemonData, Health>()
        .WithNone<Fainted>()
);

foreach (var entity in query)
{
    ref var pokemon = ref entity.Get<PokemonData>();
    ref var health = ref entity.Get<Health>();
    // ...
}
```

### Loading Assets

```csharp
var texture = Context.Modpack.GetTexture("sprite-id");
var sound = Context.Modpack.GetSound("sound-id");
var music = Context.Modpack.GetMusic("music-id");
```

---

## 📖 API Reference

### Context API

```csharp
// Logging
Context.Logger.Log{Level}(message, args)

// Events
Context.EventBus.Publish<TEvent>(event)
Context.EventBus.Subscribe<TEvent>(handler)

// ECS
Context.World.Query(description)
Context.World.Create(components...)
Context.World.Destroy(entity)

// Modpack
Context.Modpack.GetTemplate(id)
Context.Modpack.GetTexture(id)
Context.Modpack.GetSound(id)
Context.Modpack.GetMusic(id)

// Configuration
Context.Config.Features[key]
Context.Config.Settings[key]
```

---

## 🎓 Learning Resources

### Example Files

1. **Complete Modpack**: `docs/examples/modpacks/pokemon-emerald-modpack.json`
2. **Entity Template**: `docs/examples/modpacks/treecko-template-example.json`
3. **Event Hook Script**: `docs/examples/modpacks/on-wild-encounter-example.csx`

### Documentation

1. **Full Architecture**: `docs/architecture/modpack-system-design.md`
2. **Summary**: `docs/architecture/modpack-architecture-summary.md`
3. **This Guide**: `docs/architecture/modpack-quick-reference.md`

---

## 🆘 Getting Help

### Error Messages

- **"ModId is required"** - Add `metadata.modId` to manifest
- **"Invalid version format"** - Use semantic versioning (1.0.0)
- **"Asset not found"** - Check file path and existence
- **"Forbidden namespace"** - Remove restricted `using` statements
- **"Circular dependency"** - Check mod `dependencies` for loops

### Community Support

- GitHub Issues: `https://github.com/pokesharp/pokesharp/issues`
- Documentation: `https://pokesharp.dev/docs`
- Discord: `https://discord.gg/pokesharp`

---

**Quick Reference Version:** 1.0.0
**Last Updated:** 2025-11-29
**Maintained by:** PokeSharp Team
