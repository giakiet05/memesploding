namespace Memesploding.Api.Messaging.Events;

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

public record RoomMemberLeftEvent(
    string RoomCode,
    Guid UserId,
    List<Guid> RecipientIds
);

public record RoomMemberJoinedEvent(
    string RoomCode,
    Guid UserId,
    string Nickname,
    string AvatarUrl,
    string Role,
    bool IsReady,
    List<Guid> RecipientIds
);

public record RoomMemberKickedEvent(
    string RoomCode,
    Guid TargetUserId,
    Guid KickedByUserId,
    List<Guid> RecipientIds
);

public record RoomReadyStatusChangedEvent(
    string RoomCode,
    Guid UserId,
    bool IsReady,
    List<Guid> RecipientIds
);

public record RoomMatchStartingEvent(
    string RoomCode,
    Guid StartedByUserId,
    List<Guid> RecipientIds
);

public record RoomHostChangedEvent(
    string RoomCode,
    Guid PreviousHostUserId,
    Guid NewHostUserId,
    List<Guid> RecipientIds
);

public record RoomDissolvedEvent(
    string RoomCode,
    Guid DissolvedByUserId,
    List<Guid> RecipientIds
);
