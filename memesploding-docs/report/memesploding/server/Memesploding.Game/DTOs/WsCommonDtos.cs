namespace Memesploding.Game.DTOs;

public record WsConnectedDto(
    string RoomCode,
    Guid? MatchId
);

public record WsAckDto(
    long StateVersion
);

public record WsErrorDto(
    string Message
);

public record WsServerTimeDto(
    DateTime ServerTimeUtc,
    long ServerUnixTimeMs,
    long? ClientSentAtMs
);

public record WsGameplayEventDto(
    string Type,
    string Payload,
    long StateVersion
);

public record WsStateSnapshotDto(
    Guid MatchId,
    string RoomCode,
    long StateVersion,
    string Phase,
    DateTime? StartedAt,
    DateTime ServerTimeUtc,
    int TurnIndex,
    int TurnCounter,
    int TurnTimerSeconds,
    DateTime? TurnEndsAt,
    IReadOnlyList<WsPlayerPublicStateDto> Players,
    IReadOnlyList<string> SelfHand,
    int DrawPileCount,
    IReadOnlyList<string> DiscardPile,
    Guid? PendingDefuseUserId,
    Guid? PendingBombOwnerUserId,
    string? PendingBombCardCode,
    DateTime? DefuseWindowEndsAt,
    DateTime? BombReinsertWindowEndsAt,
    Guid? PendingReactionUserId,
    string? PendingReactionAction,
    DateTime? ReactionWindowEndsAt,
    int PendingNopeCount,
    IReadOnlyList<Guid> PendingReactionTargetUserIds,
    string PendingReactionEffectScope,
    Guid? PendingFavorRequesterId,
    Guid? PendingFavorTargetId,
    DateTime? FavorWindowEndsAt
);

public record WsPlayerPublicStateDto(
    Guid UserId,
    string Nickname,
    bool Connected,
    string LifeState,
    int HandCount,
    int PendingDrawCount,
    DateTime? PendingReconnectUntil
);
