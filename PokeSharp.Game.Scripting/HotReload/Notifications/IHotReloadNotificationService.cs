namespace PokeSharp.Game.Scripting.HotReload.Notifications;

using PokeSharp.Game.Systems.Services;

/// <summary>
///     Service for managing in-game hot-reload notifications.
/// </summary>
public interface IHotReloadNotificationService
{
    /// <summary>
    ///     Show a notification to the player.
    /// </summary>
    void ShowNotification(HotReloadNotification notification);

    /// <summary>
    ///     Clear all current notifications.
    /// </summary>
    void ClearNotifications();
}
