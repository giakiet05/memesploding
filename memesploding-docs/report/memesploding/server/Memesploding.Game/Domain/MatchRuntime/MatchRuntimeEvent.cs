namespace Memesploding.Game.Domain.MatchRuntime;

public record MatchRuntimeEvent(
    string EventType,
    string Payload,
    long StateVersion,
    DateTime OccurredAt
);
