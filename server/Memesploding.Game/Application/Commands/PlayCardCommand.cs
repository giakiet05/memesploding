namespace Memesploding.Game.Application.Commands;

public record PlayCardCommand(
    Guid UserId,
    string CardCode
);
