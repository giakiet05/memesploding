namespace Memesploding.Game.Contracts;

public record ReconnectPayload(
    Guid MatchId,
    string RoomCode,
    Guid UserId
);
