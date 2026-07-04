namespace Memesploding.Game.Domain.MatchRuntime;

public record MatchRuntimePlayerState(
    Guid UserId,
    string Nickname,
    string AvatarUrl,
    string Role,
    bool Connected = true,
    PlayerLifeState LifeState = PlayerLifeState.Alive,
    int PendingDrawCount = 1,
    bool IsBlind = false,
    bool HasTowerMask = false,
    bool IsMarked = false,
    DateTime? PendingReconnectUntil = null,
    List<string>? Hand = null
);
