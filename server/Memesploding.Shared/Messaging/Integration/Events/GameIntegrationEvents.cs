namespace Memesploding.Shared.Messaging.Integration.Events;

public record IntegrationPlayerInfo(
    Guid UserId,
    string Nickname,
    string AvatarUrl,
    string Role
);

public record StartMatchRequestedEvent(
    Guid MatchId,
    string RoomCode,
    Guid StartedByUserId,
    List<Guid> CardSetIds,
    List<IntegrationPlayerInfo> Players,
    DateTime RequestedAt
);

public record MatchStartedIntegrationEvent(
    Guid MatchId,
    string RoomCode,
    DateTime StartedAt
);

public record MatchEndedIntegrationEvent(
    Guid MatchId,
    string RoomCode,
    Guid? WinnerId,
    DateTime EndedAt,
    DateTime? StartedAt = null,
    int TotalTurns = 0,
    int TotalCardsPlayed = 0,
    List<MatchEndedParticipantIntegrationResult>? Participants = null
);

public record MatchEndedParticipantIntegrationResult(
    Guid UserId,
    int FinalRank
);

public record RoomUpdatedIntegrationEvent(
    string Type,
    string RoomCode,
    string Status,
    bool IsPublic,
    int CurrentPlayers,
    int MaxPlayers,
    List<Guid> PlayerIds
);
