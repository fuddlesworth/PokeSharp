using System.Diagnostics;

namespace MonoBallFramework.Game.Engine.Core.Types;

/// <summary>
///     Strongly-typed identifier for NPC definitions.
///
///     Format: base:npc:{category}/{name}
///     Examples:
///     - base:npc:townfolk/prof_birch
///     - base:npc:shopkeeper/pokemart_clerk
///     - base:npc:story/rival_may
/// </summary>
[DebuggerDisplay("{Value}")]
public sealed record GameNpcId : EntityId
{
    private const string TypeName = "npc";
    private const string DefaultCategory = "generic";

    /// <summary>
    ///     Initializes a new GameNpcId from a full ID string.
    /// </summary>
    /// <param name="value">The full ID string (e.g., "base:npc:townfolk/prof_birch")</param>
    public GameNpcId(string value) : base(value)
    {
        if (EntityType != TypeName)
            throw new ArgumentException(
                $"Expected entity type '{TypeName}', got '{EntityType}'",
                nameof(value));
    }

    /// <summary>
    ///     Initializes a new GameNpcId from components.
    /// </summary>
    /// <param name="category">The NPC category (e.g., "townfolk", "shopkeeper")</param>
    /// <param name="name">The NPC name (e.g., "prof_birch")</param>
    /// <param name="ns">Optional namespace (defaults to "base")</param>
    public GameNpcId(string category, string name, string? ns = null)
        : base(TypeName, category, name, ns)
    {
    }

    /// <summary>
    ///     The NPC category (shortcut for Category).
    /// </summary>
    public string NpcCategory => Category;

    /// <summary>
    ///     The NPC name (shortcut for Name).
    /// </summary>
    public string NpcName => Name;

    /// <summary>
    ///     Creates a GameNpcId from just a name, using defaults.
    /// </summary>
    /// <param name="npcName">The NPC name</param>
    /// <param name="category">Optional category (defaults to "generic")</param>
    /// <returns>A new GameNpcId</returns>
    public static GameNpcId Create(string npcName, string? category = null)
    {
        return new GameNpcId(category ?? DefaultCategory, npcName);
    }

    /// <summary>
    ///     Tries to create a GameNpcId from a string, returning null if invalid.
    /// </summary>
    public static GameNpcId? TryCreate(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
            return null;

        // If it's already in full format, parse it
        if (IsValidFormat(value) && value.Contains($":{TypeName}:"))
        {
            try
            {
                return new GameNpcId(value);
            }
            catch
            {
                return null;
            }
        }

        // Legacy format: could be "npc/name" or just "name"
        var normalized = NormalizeComponent(value);
        if (string.IsNullOrEmpty(normalized))
            return null;

        // Check for legacy "category/name" format
        if (normalized.Contains('/'))
        {
            var parts = normalized.Split('/', 2);
            return new GameNpcId(parts[0], parts[1]);
        }

        return Create(normalized);
    }

    /// <summary>
    ///     Explicit conversion from string. Use TryCreate() for safe parsing.
    /// </summary>
    public static explicit operator GameNpcId(string value) => new(value);
}
