using PokeSharp.Core.Factories;
using PokeSharp.Core.Types;
using PokeSharp.Scripting.Services;

namespace PokeSharp.Game.Services;

/// <summary>
///     Default implementation of IGameServicesProvider that aggregates core game services.
///     This facade simplifies dependency injection by grouping entity creation, scripting,
///     and behavior management into a single provider.
/// </summary>
/// <remarks>
///     <para>
///         This class uses the primary constructor pattern with defensive null checks
///         to ensure all required dependencies are provided at construction time.
///     </para>
///     <para>
///         Properties delegate to private readonly fields, maintaining immutability
///         and preventing accidental service replacement after initialization.
///     </para>
/// </remarks>
public class GameServicesProvider(
    IEntityFactoryService entityFactory,
    ScriptService scriptService,
    TypeRegistry<BehaviorDefinition> behaviorRegistry
) : IGameServicesProvider
{
    private readonly TypeRegistry<BehaviorDefinition> _behaviorRegistry =
        behaviorRegistry ?? throw new ArgumentNullException(nameof(behaviorRegistry));

    private readonly IEntityFactoryService _entityFactory =
        entityFactory ?? throw new ArgumentNullException(nameof(entityFactory));

    private readonly ScriptService _scriptService =
        scriptService ?? throw new ArgumentNullException(nameof(scriptService));

    /// <inheritdoc />
    public IEntityFactoryService EntityFactory => _entityFactory;

    /// <inheritdoc />
    public ScriptService ScriptService => _scriptService;

    /// <inheritdoc />
    public TypeRegistry<BehaviorDefinition> BehaviorRegistry => _behaviorRegistry;
}
