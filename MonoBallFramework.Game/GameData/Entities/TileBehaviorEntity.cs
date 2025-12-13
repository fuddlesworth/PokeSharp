using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using MonoBallFramework.Game.Engine.Core.Types;

namespace MonoBallFramework.Game.GameData.Entities;

/// <summary>
///     EF Core entity for tile behavior definitions.
///     Stores tile behavior types with flags for fast lookup.
///     Replaces TypeRegistry&lt;TileBehaviorDefinition&gt;.
/// </summary>
[Table("TileBehaviors")]
public class TileBehaviorEntity
{
    /// <summary>
    ///     Unique identifier for this tile behavior type.
    ///     Example: "base:tile_behavior:movement/jump_south", "base:tile_behavior:movement/ice"
    /// </summary>
    [Key]
    [Column(TypeName = "nvarchar(100)")]
    public GameTileBehaviorId TileBehaviorId { get; set; } = GameTileBehaviorId.CreateMovement("default");

    /// <summary>
    ///     Display name for this tile behavior.
    /// </summary>
    [Required]
    [MaxLength(100)]
    public string DisplayName { get; set; } = string.Empty;

    /// <summary>
    ///     Description of what this tile behavior does.
    /// </summary>
    [MaxLength(1000)]
    public string? Description { get; set; }

    /// <summary>
    ///     Behavior flags for fast lookup without script execution.
    ///     Stored as integer representation of TileBehaviorFlags enum.
    /// </summary>
    public int Flags { get; set; } = 0;

    /// <summary>
    ///     Path to the Roslyn .csx script file that implements this behavior.
    ///     Relative to the Scripts directory.
    ///     Example: "tiles/ice_slide_behavior.csx"
    /// </summary>
    [MaxLength(500)]
    public string? BehaviorScript { get; set; }

    /// <summary>
    ///     Source mod ID (null for base game).
    /// </summary>
    [MaxLength(100)]
    public string? SourceMod { get; set; }

    /// <summary>
    ///     Version for compatibility tracking.
    /// </summary>
    [MaxLength(20)]
    public string Version { get; set; } = "1.0.0";

    // Computed properties

    /// <summary>
    ///     Gets the behavior flags as the enum type.
    /// </summary>
    [NotMapped]
    public TileBehaviorFlags BehaviorFlags
    {
        get => (TileBehaviorFlags)Flags;
        set => Flags = (int)value;
    }

    /// <summary>
    ///     Gets whether this tile can trigger wild Pokemon encounters.
    /// </summary>
    [NotMapped]
    public bool HasEncounters => BehaviorFlags.HasFlag(TileBehaviorFlags.HasEncounters);

    /// <summary>
    ///     Gets whether this tile requires Surf HM to traverse.
    /// </summary>
    [NotMapped]
    public bool IsSurfable => BehaviorFlags.HasFlag(TileBehaviorFlags.Surfable);

    /// <summary>
    ///     Gets whether this tile blocks all movement.
    /// </summary>
    [NotMapped]
    public bool BlocksMovement => BehaviorFlags.HasFlag(TileBehaviorFlags.BlocksMovement);

    /// <summary>
    ///     Gets whether this tile forces automatic movement.
    /// </summary>
    [NotMapped]
    public bool ForcesMovement => BehaviorFlags.HasFlag(TileBehaviorFlags.ForcesMovement);
}
