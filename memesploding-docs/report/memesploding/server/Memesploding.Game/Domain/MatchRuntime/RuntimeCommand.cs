namespace Memesploding.Game.Domain.MatchRuntime;

public record RuntimeCommand(
    string Name,
    Guid UserId,
    string Payload
);
