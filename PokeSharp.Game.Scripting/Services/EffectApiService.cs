using Arch.Core;
using Microsoft.Extensions.Logging;
using Microsoft.Xna.Framework;
using PokeSharp.Engine.Common.Logging;
using PokeSharp.Engine.Core.Events.ECS;
using PokeSharp.Game.Scripting.Api;
using PokeSharp.Game.Systems.Services;

namespace PokeSharp.Game.Scripting.Services;

/// <summary>
///     Visual effect management service implementation.
///     Publishes effect events to the event bus for rendering systems to handle.
/// </summary>
public class EffectApiService(
    World world,
    IEcsEventBus eventBus,
    ILogger<EffectApiService> logger,
    IGameTimeService gameTime
) : IEffectApi
{
    private readonly IEcsEventBus _eventBus =
        eventBus ?? throw new ArgumentNullException(nameof(eventBus));

    private readonly IGameTimeService _gameTime =
        gameTime ?? throw new ArgumentNullException(nameof(gameTime));

    private readonly ILogger<EffectApiService> _logger =
        logger ?? throw new ArgumentNullException(nameof(logger));

    private readonly World _world = world ?? throw new ArgumentNullException(nameof(world));

    /// <inheritdoc />
    public void SpawnEffect(
        string effectId,
        Point position,
        float duration = 0.0f,
        float scale = 1.0f,
        Color? tint = null
    )
    {
        if (string.IsNullOrWhiteSpace(effectId))
        {
            _logger.LogOperationSkipped("Effects.Spawn", "effectId was null or whitespace");
            return;
        }

        var effectEvent = new EffectRequestedEvent
        {
            EffectId = effectId,
            Position = position,
            Duration = duration,
            Scale = scale,
            Tint = tint,
            Timestamp = _gameTime.TotalSeconds,
            Priority = EventPriority.Normal,
        };

        _eventBus.Publish(effectEvent);

        _logger.LogDebug(
            "Spawned effect: {EffectId} at ({X}, {Y}) with duration {Duration}s, scale {Scale}",
            effectId,
            position.X,
            position.Y,
            duration,
            scale
        );
    }

    /// <inheritdoc />
    public void ClearEffects()
    {
        _logger.LogDebug("Cleared all active effects");
        // Publish a clear effects event if needed
    }

    /// <inheritdoc />
    public bool HasEffect(string effectId)
    {
        // This would typically check a registry or asset manager
        // For now, we'll assume all effects exist
        _logger.LogDebug("Checking if effect exists: {EffectId}", effectId);
        return !string.IsNullOrWhiteSpace(effectId);
    }
}
