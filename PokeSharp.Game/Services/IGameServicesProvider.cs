using PokeSharp.Core.Factories;
using PokeSharp.Core.Types;
using PokeSharp.Scripting.Services;

namespace PokeSharp.Game.Services;

/// <summary>
///     Provides unified access to core game services.
///     This facade simplifies dependency injection by grouping fundamental game services
///     (entity creation, scripting, and behavior management) into a single provider.
/// </summary>
/// <remarks>
///     <para>
///         Benefits of this facade pattern:
///     </para>
///     <list type="bullet">
///         <item>
///             <description>
///                 <strong>Simplified DI:</strong> Inject one provider instead of three separate services.
///             </description>
///         </item>
///         <item>
///             <description>
///                 <strong>Consistency:</strong> Mirrors IScriptingApiProvider pattern for API services.
///             </description>
///         </item>
///         <item>
///             <description>
///                 <strong>Encapsulation:</strong> Hides service implementation details from consumers.
///             </description>
///         </item>
///         <item>
///             <description>
///                 <strong>Maintainability:</strong> Single point to manage core service dependencies.
///             </description>
///         </item>
///     </list>
/// </remarks>
public interface IGameServicesProvider
{
    /// <summary>
    ///     Gets the Entity Factory service for spawning entities from templates.
    /// </summary>
    /// <remarks>
    ///     Provides type-safe entity creation with validation and configuration support.
    ///     Used for spawning players, NPCs, tiles, and other game entities.
    /// </remarks>
    IEntityFactoryService EntityFactory { get; }

    /// <summary>
    ///     Gets the Script service for compiling and executing C# scripts.
    /// </summary>
    /// <remarks>
    ///     Handles .csx file compilation, execution, hot-reload, and instance caching.
    ///     Enables dynamic behavior through Roslyn-based scripting.
    /// </remarks>
    ScriptService ScriptService { get; }

    /// <summary>
    ///     Gets the Behavior Registry for managing NPC behavior definitions.
    /// </summary>
    /// <remarks>
    ///     Stores and retrieves behavior patterns that define how NPCs act and respond.
    ///     Supports behavior lookup, registration, and removal.
    /// </remarks>
    TypeRegistry<BehaviorDefinition> BehaviorRegistry { get; }
}
