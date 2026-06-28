namespace Memesploding.Api.Messaging.Events;

public record UserConnectedEvent(Guid UserId, string ConnectionId);
public record UserDisconnectedEvent(Guid UserId, string ConnectionId);

public record PresenceRoomBrief(
    string Code,
    string Status,
    bool IsPublic = true,
    int CurrentPlayers = 0,
    int MaxPlayers = 0
);

public record PresenceUserActivity(
    string Type,
    PresenceRoomBrief? Room = null
);

public record FriendStatusChangedEvent(
    Guid UserId,
    string Username,
    string AvatarUrl,
    bool Online,
    DateTime? LastSeen,
    PresenceUserActivity Activity,
    List<Guid> FriendIdsToNotify
);
