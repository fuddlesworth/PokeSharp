using Arch.Core;
using Microsoft.Extensions.Logging;
using PokeSharp.Engine.Common.Logging;
using PokeSharp.Engine.Core.Events.ECS;
using PokeSharp.Game.Scripting.Api;
using PokeSharp.Game.Systems.Services;

namespace PokeSharp.Game.Scripting.Services;

/// <summary>
///     Dialogue management service implementation.
///     Publishes dialogue events to the event bus for UI systems to handle.
/// </summary>
public class DialogueApiService(
    World world,
    IEcsEventBus eventBus,
    ILogger<DialogueApiService> logger,
    IGameTimeService gameTime
) : IDialogueApi
{
    private readonly IEcsEventBus _eventBus =
        eventBus ?? throw new ArgumentNullException(nameof(eventBus));

    private readonly IGameTimeService _gameTime =
        gameTime ?? throw new ArgumentNullException(nameof(gameTime));

    private readonly ILogger<DialogueApiService> _logger =
        logger ?? throw new ArgumentNullException(nameof(logger));

    private readonly World _world = world ?? throw new ArgumentNullException(nameof(world));

    /// <inheritdoc />
    public bool IsDialogueActive { get; private set; }

    /// <inheritdoc />
    public void ShowMessage(string message, string? speakerName = null, int priority = 0)
    {
        if (string.IsNullOrWhiteSpace(message))
        {
            _logger.LogOperationSkipped("Dialogue.ShowMessage", "message was null or whitespace");
            return;
        }

        var dialogueEvent = new DialogueRequestedEvent
        {
            Message = message,
            SpeakerName = speakerName,
            DisplayPriority = priority,
            Tint = null,
            Timestamp = _gameTime.TotalSeconds,
            Priority = EventPriority.Normal,
        };

        _eventBus.Publish(dialogueEvent);
        IsDialogueActive = true;

        _logger.LogDebug(
            "Published dialogue request: {Message} (Speaker: {Speaker}, Priority: {Priority})",
            message,
            speakerName ?? "None",
            priority
        );
    }

    /// <inheritdoc />
    public void ClearMessages()
    {
        IsDialogueActive = false;
        _logger.LogDebug("Cleared dialogue messages");
    }
}
