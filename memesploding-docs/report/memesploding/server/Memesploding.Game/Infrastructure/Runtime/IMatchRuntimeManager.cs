using Memesploding.Game.Domain.MatchRuntime;
using Memesploding.Shared.Messaging.Integration.Events;

namespace Memesploding.Game.Infrastructure.Runtime;

public interface IMatchRuntimeManager
{
    MatchRuntime CreateOrReplace(StartMatchRequestedEvent @event);
    bool TryGetByMatchId(Guid matchId, out MatchRuntime? runtime);
    bool TryGetByRoomCode(string roomCode, out MatchRuntime? runtime);
    Task EnqueueCommandAsync(Guid matchId, RuntimeCommand command, CancellationToken cancellationToken = default);
    Task TickAllAsync(CancellationToken cancellationToken = default);
    Task ProcessReconnectTimeoutsAsync(CancellationToken cancellationToken = default);
    void MarkPlayerConnection(string roomCode, Guid userId, bool connected);
    IReadOnlyList<MatchRuntimeEvent> DrainEventLog(Guid matchId);
    IReadOnlyList<MatchRuntime> GetAllRuntimes();
    IReadOnlyList<MatchRuntime> GetFinishedRuntimes();
}
