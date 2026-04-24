using System.Collections.Concurrent;
using Memesploding.Game.Domain.MatchRuntime;
using Memesploding.Game.Infrastructure.Configuration;
using Memesploding.Shared.Messaging.Integration.Events;
using Microsoft.Extensions.Options;

namespace Memesploding.Game.Infrastructure.Runtime;

public class MatchRuntimeManager(
    IOptions<GameplayTimingOptions> gameplayTimingOptions,
    IDeckComposer deckComposer
) : IMatchRuntimeManager
{
    private readonly ConcurrentDictionary<Guid, MatchRuntime> _matchRuntimes = new();
    private readonly ConcurrentDictionary<string, Guid> _roomIndex = new(StringComparer.OrdinalIgnoreCase);
    private readonly GameplayTimingOptions _timing = gameplayTimingOptions.Value;

    public MatchRuntime CreateOrReplace(StartMatchRequestedEvent @event)
    {
        var runtimeState = new MatchRuntimeState
        {
            MatchId = @event.MatchId,
            RoomCode = @event.RoomCode.ToUpperInvariant(),
            TurnTimerSeconds = @event.TurnTimerSeconds > 0 ? @event.TurnTimerSeconds : _timing.DefaultTurnSeconds,
            NopeWindowSeconds = _timing.NopeWindowSeconds,
            DefuseDecisionSeconds = _timing.DefuseDecisionSeconds,
            BombReinsertSeconds = _timing.BombReinsertSeconds,
            ReconnectGraceSeconds = _timing.ReconnectGraceSeconds,
            EffectResolutionDelayMs = _timing.EffectResolutionDelayMs,
            Players = @event.Players
                .Select(p => new MatchRuntimePlayerState(
                    p.UserId,
                    p.Nickname,
                    p.AvatarUrl,
                    p.Role,
                    Connected: true,
                    LifeState: PlayerLifeState.Alive,
                    PendingDrawCount: 1,
                    IsBlind: false,
                    HasTowerMask: false,
                    IsMarked: false,
                    PendingReconnectUntil: null,
                    Hand: []
                ))
                .ToList()
        };

        deckComposer.InitializeHandsAndDeck(runtimeState, @event.CardSetIds);

        var runtime = new MatchRuntime(runtimeState);
        runtime.MarkStarted();

        _matchRuntimes[@event.MatchId] = runtime;
        _roomIndex[@event.RoomCode.ToUpperInvariant()] = @event.MatchId;

        return runtime;
    }

    public bool TryGetByMatchId(Guid matchId, out MatchRuntime? runtime)
    {
        var found = _matchRuntimes.TryGetValue(matchId, out var innerRuntime);
        runtime = innerRuntime;
        return found;
    }

    public bool TryGetByRoomCode(string roomCode, out MatchRuntime? runtime)
    {
        runtime = null;
        if (!_roomIndex.TryGetValue(roomCode.ToUpperInvariant(), out var matchId))
        {
            return false;
        }

        return TryGetByMatchId(matchId, out runtime);
    }

    public async Task EnqueueCommandAsync(Guid matchId, RuntimeCommand command, CancellationToken cancellationToken = default)
    {
        if (_matchRuntimes.TryGetValue(matchId, out var runtime))
        {
            await runtime.EnqueueAsync(command, cancellationToken);
        }
    }

    public async Task TickAllAsync(CancellationToken cancellationToken = default)
    {
        foreach (var runtime in _matchRuntimes.Values)
        {
            await runtime.TickAsync(cancellationToken);
        }
    }

    public Task ProcessReconnectTimeoutsAsync(CancellationToken cancellationToken = default)
    {
        foreach (var runtime in _matchRuntimes.Values)
        {
            cancellationToken.ThrowIfCancellationRequested();
            runtime.ProcessReconnectTimeouts();
        }

        return Task.CompletedTask;
    }

    public void MarkPlayerConnection(string roomCode, Guid userId, bool connected)
    {
        if (!TryGetByRoomCode(roomCode, out var runtime) || runtime == null)
        {
            return;
        }

        var index = runtime.State.Players.FindIndex(p => p.UserId == userId);
        if (index < 0)
        {
            return;
        }

        runtime.State.Players[index] = runtime.State.Players[index] with
        {
            Connected = connected,
            PendingReconnectUntil = connected ? null : DateTime.UtcNow.AddSeconds(runtime.State.ReconnectGraceSeconds)
        };
        runtime.State.StateVersion++;
    }

    public IReadOnlyList<MatchRuntimeEvent> DrainEventLog(Guid matchId)
    {
        if (!_matchRuntimes.TryGetValue(matchId, out var runtime))
        {
            return [];
        }

        var events = runtime.State.EventLog.ToList();
        runtime.State.EventLog.Clear();
        return events;
    }

    public IReadOnlyList<MatchRuntime> GetFinishedRuntimes()
    {
        return _matchRuntimes.Values
            .Where(r => r.State.Phase == Domain.StateMachine.MatchPhase.Finished)
            .ToList();
    }

    public IReadOnlyList<MatchRuntime> GetAllRuntimes()
    {
        return _matchRuntimes.Values.ToList();
    }
}
