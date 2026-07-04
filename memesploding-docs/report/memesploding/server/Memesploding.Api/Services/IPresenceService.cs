namespace Memesploding.Api.Services;

public interface IPresenceService
{
    /// <summary>
    /// Track user connection, set presence to online, broadcast to friends
    /// </summary>
    Task UserConnectedAsync(Guid userId, string connectionId);

    /// <summary>
    /// Remove connection, set offline if no more connections, broadcast to friends
    /// </summary>
    Task UserDisconnectedAsync(Guid userId, string connectionId);

    /// <summary>
    /// Refresh presence TTL (called by heartbeat)
    /// </summary>
    Task RefreshPresenceAsync(Guid userId);

    /// <summary>
    /// Get all connection IDs for a user (multi-device support)
    /// </summary>
    Task<List<string>> GetUserConnectionsAsync(Guid userId);

    /// <summary>
    /// Broadcast friend status to all friends of a user
    /// </summary>
    Task BroadcastFriendStatusAsync(Guid userId);

    /// <summary>
    /// Update user's presence activity (when room status changes from Game Server)
    /// </summary>
    Task UpdateUserActivityAsync(Guid userId);
}
