using Memesploding.Game.Domain.MatchRuntime;

namespace Memesploding.Game.Infrastructure.StateStore;

public interface IGameSnapshotStore
{
    Task SaveSnapshotAsync(MatchRuntimeState state);
    Task<MatchRuntimeState?> GetSnapshotByRoomCodeAsync(string roomCode);
}
