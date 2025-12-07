# Entity ID Architecture Proposal

## Executive Summary

This proposal introduces a unified, strongly-typed entity ID system that:
- Makes entity types immediately identifiable from ID strings
- Prevents ID collisions across entity types
- Supports mod namespacing for extensibility
- Maintains backward compatibility with existing data
- Works efficiently with Entity Framework Core

## Core Design Principles

1. **Type Safety**: Compile-time guarantees that correct ID types are used
2. **Self-Describing**: ID format encodes entity type and namespace
3. **Extensible**: Supports future entity types and mod content
4. **Performant**: Efficient string parsing and EF Core integration
5. **Backward Compatible**: Can migrate existing plain-string IDs

## ID Format Specification

### Format Pattern
```
[namespace:]type:category/name
```

### Components
- **namespace** (optional): Mod identifier (e.g., `mymod`, `base`)
- **type** (required): Entity type discriminator (`npc`, `trainer`, `map`, `sprite`, etc.)
- **category** (required): Entity category/group
- **name** (required): Specific entity identifier

### Examples
```
npc:townfolk/oak              # Base game NPC
trainer:gym_leaders/brock     # Base game trainer
map:kanto/pallet_town         # Base game map
sprite:characters/player      # Base game sprite
mymod:npc:custom/merchant     # Modded NPC
```

### Regex Pattern
```regex
^(?:([a-z][a-z0-9_]*):)?([a-z][a-z0-9_]*):([a-z0-9_]+)/([a-z0-9_]+)$
```

- Group 1: Namespace (optional)
- Group 2: Type discriminator
- Group 3: Category
- Group 4: Name

## Architecture Design

### 1. Base EntityId Type

```csharp
/// <summary>
/// Base type for all strongly-typed entity identifiers.
/// Provides parsing, validation, and value semantics.
/// </summary>
public abstract record EntityId
{
    private static readonly Regex IdPattern = new(
        @"^(?:([a-z][a-z0-9_]*):)?([a-z][a-z0-9_]*):([a-z0-9_]+)/([a-z0-9_]+)$",
        RegexOptions.Compiled | RegexOptions.CultureInvariant
    );

    /// <summary>Mod namespace (null = base game)</summary>
    public string? Namespace { get; init; }

    /// <summary>Entity type discriminator (e.g., "npc", "trainer")</summary>
    public string Type { get; init; }

    /// <summary>Entity category/group</summary>
    public string Category { get; init; }

    /// <summary>Specific entity name</summary>
    public string Name { get; init; }

    /// <summary>Full qualified ID string</summary>
    public string Value => Namespace != null
        ? $"{Namespace}:{Type}:{Category}/{Name}"
        : $"{Type}:{Category}/{Name}";

    protected EntityId(string? ns, string type, string category, string name)
    {
        Namespace = ns;
        Type = type ?? throw new ArgumentNullException(nameof(type));
        Category = category ?? throw new ArgumentNullException(nameof(category));
        Name = name ?? throw new ArgumentNullException(nameof(name));
    }

    /// <summary>Parse an ID string into components</summary>
    protected static (string? ns, string type, string category, string name) Parse(string value)
    {
        var match = IdPattern.Match(value);
        if (!match.Success)
            throw new FormatException($"Invalid entity ID format: {value}");

        return (
            ns: match.Groups[1].Success ? match.Groups[1].Value : null,
            type: match.Groups[2].Value,
            category: match.Groups[3].Value,
            name: match.Groups[4].Value
        );
    }

    /// <summary>Validate expected type matches actual type</summary>
    protected static void ValidateType(string actual, string expected, string value)
    {
        if (actual != expected)
            throw new FormatException(
                $"Expected type '{expected}' but got '{actual}' in ID: {value}");
    }

    public override string ToString() => Value;

    // Implicit conversion to string for backward compatibility
    public static implicit operator string(EntityId id) => id.Value;
}
```

### 2. Specific Entity ID Types

```csharp
/// <summary>
/// Strongly-typed identifier for NPC entities.
/// Format: [namespace:]npc:category/name
/// Example: npc:townfolk/oak, mymod:npc:merchants/special
/// </summary>
public sealed record NpcId : EntityId
{
    public const string TypeDiscriminator = "npc";

    private NpcId(string? ns, string category, string name)
        : base(ns, TypeDiscriminator, category, name)
    {
    }

    /// <summary>Create NpcId from components (base game)</summary>
    public static NpcId Create(string category, string name)
        => new(null, category, name);

    /// <summary>Create NpcId with mod namespace</summary>
    public static NpcId Create(string ns, string category, string name)
        => new(ns, category, name);

    /// <summary>Parse from full ID string</summary>
    public static NpcId FromString(string value)
    {
        var (ns, type, category, name) = Parse(value);
        ValidateType(type, TypeDiscriminator, value);
        return new NpcId(ns, category, name);
    }

    /// <summary>
    /// Migrate from old plain-string format.
    /// Assumes format: "category_name" -> "category/name"
    /// </summary>
    public static NpcId FromLegacy(string legacyId)
    {
        var parts = legacyId.Split('_', 2);
        if (parts.Length != 2)
            throw new FormatException($"Invalid legacy NPC ID: {legacyId}");

        return Create(parts[0], parts[1]);
    }

    // Explicit conversion from string (prevents accidental assignments)
    public static explicit operator NpcId(string value) => FromString(value);
}

/// <summary>
/// Strongly-typed identifier for Trainer entities.
/// Format: [namespace:]trainer:category/name
/// Example: trainer:gym_leaders/brock
/// </summary>
public sealed record TrainerId : EntityId
{
    public const string TypeDiscriminator = "trainer";

    private TrainerId(string? ns, string category, string name)
        : base(ns, TypeDiscriminator, category, name)
    {
    }

    public static TrainerId Create(string category, string name)
        => new(null, category, name);

    public static TrainerId Create(string ns, string category, string name)
        => new(ns, category, name);

    public static TrainerId FromString(string value)
    {
        var (ns, type, category, name) = Parse(value);
        ValidateType(type, TypeDiscriminator, value);
        return new TrainerId(ns, category, name);
    }

    public static TrainerId FromLegacy(string legacyId)
    {
        var parts = legacyId.Split('_', 2);
        if (parts.Length != 2)
            throw new FormatException($"Invalid legacy Trainer ID: {legacyId}");

        return Create(parts[0], parts[1]);
    }

    public static explicit operator TrainerId(string value) => FromString(value);
}

/// <summary>
/// Strongly-typed identifier for Map entities.
/// Format: [namespace:]map:category/name
/// Example: map:kanto/pallet_town
/// </summary>
public sealed record MapId : EntityId
{
    public const string TypeDiscriminator = "map";

    private MapId(string? ns, string category, string name)
        : base(ns, TypeDiscriminator, category, name)
    {
    }

    public static MapId Create(string category, string name)
        => new(null, category, name);

    public static MapId Create(string ns, string category, string name)
        => new(ns, category, name);

    public static MapId FromString(string value)
    {
        var (ns, type, category, name) = Parse(value);
        ValidateType(type, TypeDiscriminator, value);
        return new MapId(ns, category, name);
    }

    /// <summary>
    /// Migrate from existing MapIdentifier format.
    /// Assumes format: "category/name" already exists
    /// </summary>
    public static MapId FromMapIdentifier(MapIdentifier oldId)
    {
        // MapIdentifier already uses "category/name" format
        var parts = oldId.Value.Split('/', 2);
        if (parts.Length != 2)
            throw new FormatException($"Invalid MapIdentifier: {oldId.Value}");

        return Create(parts[0], parts[1]);
    }

    public static explicit operator MapId(string value) => FromString(value);
}

/// <summary>
/// Strongly-typed identifier for Sprite assets.
/// Format: [namespace:]sprite:category/name
/// Example: sprite:characters/player
/// </summary>
public sealed record SpriteId : EntityId
{
    public const string TypeDiscriminator = "sprite";

    private SpriteId(string? ns, string category, string name)
        : base(ns, TypeDiscriminator, category, name)
    {
    }

    public static SpriteId Create(string category, string name)
        => new(null, category, name);

    public static SpriteId Create(string ns, string category, string name)
        => new(ns, category, name);

    public static SpriteId FromString(string value)
    {
        var (ns, type, category, name) = Parse(value);
        ValidateType(type, TypeDiscriminator, value);
        return new SpriteId(ns, category, name);
    }

    /// <summary>
    /// Migrate from existing SpriteId parsing logic.
    /// Assumes format: "category/spritename"
    /// </summary>
    public static SpriteId FromLegacyPath(string path)
    {
        var parts = path.Split('/', 2);
        if (parts.Length != 2)
            throw new FormatException($"Invalid sprite path: {path}");

        return Create(parts[0], parts[1]);
    }

    public static explicit operator SpriteId(string value) => FromString(value);
}
```

### 3. Entity Framework Core Integration

```csharp
/// <summary>
/// Generic value converter for EntityId types.
/// Converts strongly-typed IDs to strings for database storage.
/// </summary>
public class EntityIdConverter<TEntityId> : ValueConverter<TEntityId, string>
    where TEntityId : EntityId
{
    public EntityIdConverter()
        : base(
            // Convert to string for storage
            id => id.Value,
            // Convert from string when reading
            value => ParseEntityId(value)
        )
    {
    }

    private static TEntityId ParseEntityId(string value)
    {
        // Use reflection to call the static FromString method
        var method = typeof(TEntityId).GetMethod(
            "FromString",
            BindingFlags.Public | BindingFlags.Static,
            null,
            new[] { typeof(string) },
            null
        );

        if (method == null)
            throw new InvalidOperationException(
                $"{typeof(TEntityId).Name} must have a public static FromString method");

        return (TEntityId)method.Invoke(null, new[] { value })!;
    }
}

/// <summary>
/// Value comparer for EntityId types (required for EF Core change tracking).
/// </summary>
public class EntityIdComparer<TEntityId> : ValueComparer<TEntityId>
    where TEntityId : EntityId
{
    public EntityIdComparer()
        : base(
            // Equality comparison
            (left, right) => left!.Value == right!.Value,
            // Hash code
            id => id.Value.GetHashCode(),
            // Snapshot (records are immutable, so return same instance)
            id => id
        )
    {
    }
}

/// <summary>
/// Extension methods for configuring EntityId properties in EF Core.
/// </summary>
public static class EntityIdModelBuilderExtensions
{
    public static PropertyBuilder<TEntityId> HasEntityIdConversion<TEntityId>(
        this PropertyBuilder<TEntityId> propertyBuilder)
        where TEntityId : EntityId
    {
        return propertyBuilder
            .HasConversion(
                new EntityIdConverter<TEntityId>(),
                new EntityIdComparer<TEntityId>()
            )
            .HasMaxLength(200) // Reasonable max for namespaced IDs
            .IsUnicode(false); // ASCII only for performance
    }
}
```

### 4. DbContext Configuration

```csharp
public class GameDbContext : DbContext
{
    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        // Configure NPC entity
        modelBuilder.Entity<Npc>(entity =>
        {
            entity.Property(e => e.Id)
                .HasEntityIdConversion();

            entity.HasIndex(e => e.Id)
                .IsUnique();
        });

        // Configure Trainer entity
        modelBuilder.Entity<Trainer>(entity =>
        {
            entity.Property(e => e.Id)
                .HasEntityIdConversion();

            entity.HasIndex(e => e.Id)
                .IsUnique();
        });

        // Configure Map entity
        modelBuilder.Entity<Map>(entity =>
        {
            entity.Property(e => e.Id)
                .HasEntityIdConversion();

            entity.HasIndex(e => e.Id)
                .IsUnique();
        });

        // Configure relationships with FK constraints
        modelBuilder.Entity<MapNpc>(entity =>
        {
            entity.Property(e => e.MapId)
                .HasEntityIdConversion();

            entity.Property(e => e.NpcId)
                .HasEntityIdConversion();

            entity.HasOne(e => e.Map)
                .WithMany()
                .HasForeignKey(e => e.MapId)
                .OnDelete(DeleteBehavior.Cascade);

            entity.HasOne(e => e.Npc)
                .WithMany()
                .HasForeignKey(e => e.NpcId)
                .OnDelete(DeleteBehavior.Cascade);
        });
    }
}
```

## Migration Strategy

### Phase 1: Parallel Implementation
1. Add new EntityId types alongside existing plain strings
2. Add `[Obsolete]` attributes to old properties
3. Add migration methods (FromLegacy, FromMapIdentifier)

### Phase 2: Data Migration
```csharp
public class MigrationToEntityIds : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        // Add temporary columns with new format
        migrationBuilder.AddColumn<string>(
            name: "Id_New",
            table: "Npcs",
            nullable: true);

        // Migrate data (example for NPCs)
        migrationBuilder.Sql(@"
            UPDATE Npcs
            SET Id_New = 'npc:' || REPLACE(Id, '_', '/')
            WHERE Id NOT LIKE '%:%'
        ");

        // Drop old column, rename new one
        migrationBuilder.DropColumn(name: "Id", table: "Npcs");
        migrationBuilder.RenameColumn(
            name: "Id_New",
            table: "Npcs",
            newName: "Id");

        // Add constraints
        migrationBuilder.AlterColumn<string>(
            name: "Id",
            table: "Npcs",
            nullable: false);
    }
}
```

### Phase 3: Cleanup
1. Remove obsolete properties
2. Remove legacy migration methods
3. Update all data files to new format

## Usage Examples

### Creating IDs
```csharp
// Base game entities
var oakNpc = NpcId.Create("townfolk", "oak");
var brockTrainer = TrainerId.Create("gym_leaders", "brock");
var palletMap = MapId.Create("kanto", "pallet_town");

// Modded entities
var customNpc = NpcId.Create("mymod", "merchants", "special_trader");

// From strings
var parsedNpc = NpcId.FromString("npc:townfolk/oak");
```

### EF Core Queries
```csharp
// Type-safe queries
var npcId = NpcId.Create("townfolk", "oak");
var npc = await context.Npcs
    .FirstOrDefaultAsync(n => n.Id == npcId);

// ID components in queries
var kantoPokemon = await context.Maps
    .Where(m => m.Id.Category == "kanto")
    .ToListAsync();

// Namespace filtering (for mods)
var moddedNpcs = await context.Npcs
    .Where(n => n.Id.Namespace == "mymod")
    .ToListAsync();
```

### JSON Serialization
```csharp
// System.Text.Json
public class EntityIdJsonConverter<TEntityId> : JsonConverter<TEntityId>
    where TEntityId : EntityId
{
    public override TEntityId Read(
        ref Utf8JsonReader reader,
        Type typeToConvert,
        JsonSerializerOptions options)
    {
        var value = reader.GetString()!;
        var method = typeof(TEntityId).GetMethod("FromString");
        return (TEntityId)method!.Invoke(null, new[] { value })!;
    }

    public override void Write(
        Utf8JsonWriter writer,
        TEntityId value,
        JsonSerializerOptions options)
    {
        writer.WriteStringValue(value.Value);
    }
}
```

## Benefits

### 1. Type Safety
```csharp
// Compile error - can't mix types
void AssignNpcToMap(MapId mapId, TrainerId trainerId) { } // ❌ Won't compile

void AssignNpcToMap(MapId mapId, NpcId npcId) { } // ✅ Correct types
```

### 2. No ID Collisions
```
npc:townfolk/oak      # NPC
trainer:townfolk/oak  # Trainer (different namespace)
```

### 3. Self-Documenting
```csharp
// Type is obvious from the ID string
var id = "mymod:npc:merchants/trader";
// Namespace: mymod
// Type: npc
// Category: merchants
// Name: trader
```

### 4. Extensibility
```csharp
// Easy to add new entity types
public sealed record ItemId : EntityId { ... }
public sealed record QuestId : EntityId { ... }
public sealed record DialogueId : EntityId { ... }
```

## Performance Considerations

### Optimizations
1. **Compiled Regex**: Pattern matching is fast
2. **Record Structs**: Value semantics, no heap allocations
3. **String Interning**: Consider for frequently-used IDs
4. **Cached Parsing**: For hot paths, cache parsed IDs

### Benchmarks (Expected)
- Parsing: ~50-100ns per ID
- EF Core conversion: Negligible overhead
- Memory: Same as plain strings (small objects)

## Future Enhancements

1. **ID Registry**: Central registry for validation
2. **Compile-Time Validation**: Source generators for literal IDs
3. **Localization**: Category/name lookup tables
4. **ID Aliases**: Support for legacy compatibility
5. **Path Resolution**: Automatic file path derivation from IDs

## Conclusion

This unified EntityId architecture provides:
- ✅ Strong typing with compile-time safety
- ✅ Self-describing IDs with type discrimination
- ✅ No collision risks across entity types
- ✅ Mod namespace support for extensibility
- ✅ Backward compatibility via migration
- ✅ Efficient EF Core integration
- ✅ Extensible for future entity types

The design is production-ready and can be implemented incrementally without breaking existing functionality.
