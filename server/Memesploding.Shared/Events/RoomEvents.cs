namespace Memesploding.Shared.Events;

public record RoomInvitationSentEvent(
    string InvitationId,
    string RoomCode,
    Guid InviterId,
    Guid InviteeId,
    string InviterUsername,
    string InviterAvatarUrl,
    bool IsPublic,
    int CurrentPlayers,
    int MaxPlayers,
    DateTime ExpiresAt
);

public record RoomInvitationRespondedEvent(
    string InvitationId,
    Guid InviterId,
    Guid InviteeId,
    string InviteeUsername,
    bool Accepted
);

public record RoomJoinRequestSentEvent(
    string RequestId,
    string RoomCode,
    Guid RequesterId,
    string RequesterUsername,
    string RequesterAvatarUrl,
    DateTime ExpiresAt,
    List<Guid> RoomMemberIds
);

public record RoomJoinRequestRespondedEvent(
    string RequestId,
    string RoomCode,
    Guid RequesterId,
    Guid ResponderId,
    string ResponderUsername,
    bool Accepted
);

public record RoomUpdatedEvent(
    string Type,
    string RoomCode,
    string Status,
    bool IsPublic,
    int CurrentPlayers,
    int MaxPlayers,
    List<Guid> PlayerIds
);
