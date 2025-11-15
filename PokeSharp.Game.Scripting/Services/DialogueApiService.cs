using Arch.Core;
using Microsoft.Extensions.Logging;
using PokeSharp.Engine.Common.Logging;
using PokeSharp.Engine.Core.Events;
using PokeSharp.Engine.Core.Systems;
using PokeSharp.Engine.Core.Types.Events;
using PokeSharp.Engine.Systems.Factories;
using PokeSharp.Engine.Systems.Management;
using PokeSharp.Game.Scripting.Api;
using PokeSharp.Game.Systems.Services;

namespace PokeSharp.Game.Scripting.Services;

/// <summary>
///     Dialogue management service implementation.
///     Publishes dialogue events to the event bus for UI systems to handle.
/// </summary>
public class DialogueApiService(
    World world,
    IEventBus eventBus,
    ILogger<DialogueApiService> logger,
    IGameTimeService gameTime
) : IDialogueApi
{
    private readonly IEventBus _eventBus =
        eventBus ?? throw new ArgumentNullException(nameof(eventBus));

    private readonly ILogger<DialogueApiService> _logger =
        logger ?? throw new ArgumentNullException(nameof(logger));

    private readonly World _world = world ?? throw new ArgumentNullException(nameof(world));
    private readonly IGameTimeService _gameTime =
        gameTime ?? throw new ArgumentNullException(nameof(gameTime));
    private bool _isDialogueActive;

    /// <inheritdoc />
    public bool IsDialogueActive => _isDialogueActive;

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
            TypeId = "dialogue-api",
            Timestamp = _gameTime.TotalSeconds,
            Message = message,
            SpeakerName = speakerName,
            Priority = priority,
        };

        _eventBus.Publish(dialogueEvent);
        _isDialogueActive = true;

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
        _isDialogueActive = false;
        _logger.LogDebug("Cleared dialogue messages");
    }
}
