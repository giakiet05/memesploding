namespace Memesploding.Game.Auth;

public record GameConnectionContext(
    Guid UserId,
    string RoomCode,
    Guid? MatchId
);
