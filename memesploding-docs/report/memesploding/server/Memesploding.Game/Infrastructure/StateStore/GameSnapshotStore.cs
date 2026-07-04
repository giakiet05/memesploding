using Memesploding.Game.Domain.MatchRuntime;
using Memesploding.Shared.Infrastructure.Cache;

namespace Memesploding.Game.Infrastructure.StateStore;

public class GameSnapshotStore(ICacheStore cacheStore) : IGameSnapshotStore
{
    private static string BuildKey(string roomCode) => $"game:match:{roomCode.ToUpperInvariant()}:state";

    public Task SaveSnapshotAsync(MatchRuntimeState state)
    {
        var key = BuildKey(state.RoomCode);
        return cacheStore.SetAsync(key, state, TimeSpan.FromMinutes(30));
    }

    public Task<MatchRuntimeState?> GetSnapshotByRoomCodeAsync(string roomCode)
    {
        return cacheStore.GetAsync<MatchRuntimeState>(BuildKey(roomCode));
    }
}
