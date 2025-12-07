using System.Diagnostics;

namespace MonoBallFramework.Game.Engine.Core.Types;

/// <summary>
///     Strongly-typed identifier for trainer definitions.
///
///     Format: base:trainer:{class}/{name}
///     Examples:
///     - base:trainer:youngster/joey
///     - base:trainer:gym_leader/roxanne
///     - base:trainer:elite_four/sidney
/// </summary>
[DebuggerDisplay("{Value}")]
public sealed record GameTrainerId : EntityId
{
    private const string TypeName = "trainer";
    private const string DefaultClass = "trainer";

    /// <summary>
    ///     Initializes a new GameTrainerId from a full ID string.
    /// </summary>
    /// <param name="value">The full ID string (e.g., "base:trainer:youngster/joey")</param>
    public GameTrainerId(string value) : base(value)
    {
        if (EntityType != TypeName)
            throw new ArgumentException(
                $"Expected entity type '{TypeName}', got '{EntityType}'",
                nameof(value));
    }

    /// <summary>
    ///     Initializes a new GameTrainerId from components.
    /// </summary>
    /// <param name="trainerClass">The trainer class (e.g., "youngster", "gym_leader")</param>
    /// <param name="name">The trainer name (e.g., "joey", "roxanne")</param>
    /// <param name="ns">Optional namespace (defaults to "base")</param>
    public GameTrainerId(string trainerClass, string name, string? ns = null)
        : base(TypeName, trainerClass, name, ns)
    {
    }

    /// <summary>
    ///     The trainer class (shortcut for Category).
    /// </summary>
    public string TrainerClass => Category;

    /// <summary>
    ///     The trainer name (shortcut for Name).
    /// </summary>
    public string TrainerName => Name;

    /// <summary>
    ///     Creates a GameTrainerId from just a name, using defaults.
    /// </summary>
    /// <param name="trainerName">The trainer name</param>
    /// <param name="trainerClass">Optional class (defaults to "trainer")</param>
    /// <returns>A new GameTrainerId</returns>
    public static GameTrainerId Create(string trainerName, string? trainerClass = null)
    {
        return new GameTrainerId(trainerClass ?? DefaultClass, trainerName);
    }

    /// <summary>
    ///     Tries to create a GameTrainerId from a string, returning null if invalid.
    /// </summary>
    public static GameTrainerId? TryCreate(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
            return null;

        // If it's already in full format, parse it
        if (IsValidFormat(value) && value.Contains($":{TypeName}:"))
        {
            try
            {
                return new GameTrainerId(value);
            }
            catch
            {
                return null;
            }
        }

        // Legacy format
        var normalized = NormalizeComponent(value);
        if (string.IsNullOrEmpty(normalized))
            return null;

        // Check for "class/name" format
        if (normalized.Contains('/'))
        {
            var parts = normalized.Split('/', 2);
            return new GameTrainerId(parts[0], parts[1]);
        }

        return Create(normalized);
    }

    /// <summary>
    ///     Explicit conversion from string. Use TryCreate() for safe parsing.
    /// </summary>
    public static explicit operator GameTrainerId(string value) => new(value);
}
