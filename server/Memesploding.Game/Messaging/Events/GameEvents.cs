namespace Memesploding.Game.Messaging.Events;

public record RuntimeCommandAcceptedEvent(
    Guid MatchId,
    long StateVersion
);
