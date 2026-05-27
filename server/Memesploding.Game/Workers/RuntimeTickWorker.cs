using Memesploding.Game.DTOs;
using Memesploding.Game.Hubs;
using Memesploding.Game.Infrastructure.Runtime;
using Memesploding.Game.Infrastructure.StateStore;
using Memesploding.Shared.Messaging.EventBus;
using Memesploding.Shared.Messaging.Integration.Channels;
using Memesploding.Shared.Messaging.Integration.Events;
using Microsoft.AspNetCore.SignalR;

namespace Memesploding.Game.Workers;

public class RuntimeTickWorker(
    IMatchRuntimeManager runtimeManager,
    IGameSnapshotStore snapshotStore,
    IHubContext<GameHub> hubContext,
    IEventBus eventBus,
    ILogger<RuntimeTickWorker> logger
) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        logger.LogInformation("RuntimeTickWorker is starting.");

        while (!stoppingToken.IsCancellationRequested)
        {
            await runtimeManager.TickAllAsync(stoppingToken);
            await PublishRuntimeEventsAsync(stoppingToken);
            await PublishFinishedMatchesAsync(stoppingToken);

            try
            {
                await Task.Delay(TimeSpan.FromMilliseconds(100), stoppingToken);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                break;
            }
        }
    }

    private async Task PublishRuntimeEventsAsync(CancellationToken cancellationToken)
    {
        foreach (var runtime in runtimeManager.GetAllRuntimes())
        {
            cancellationToken.ThrowIfCancellationRequested();
            var events = runtimeManager.DrainEventLog(runtime.State.MatchId);
            if (events.Count == 0)
            {
                continue;
            }

            await snapshotStore.SaveSnapshotAsync(runtime.State);

            foreach (var item in events)
            {
                var payload = WsServerEvent<WsGameplayEventDto>.Create(
                    "GameplayEvent",
                    new WsGameplayEventDto(item.EventType, item.Payload, item.StateVersion)
                );
                await hubContext.Clients.Group(GetRoomGroup(runtime.State.RoomCode)).SendAsync("ReceiveMessage", payload, cancellationToken);
            }
        }
    }

    private async Task PublishFinishedMatchesAsync(CancellationToken cancellationToken)
    {
        foreach (var runtime in runtimeManager.GetFinishedRuntimes())
        {
            cancellationToken.ThrowIfCancellationRequested();
            if (runtime.State.MatchEndedPublished)
            {
                continue;
            }

            runtime.State.MatchEndedPublished = true;
            var participantResults = BuildParticipantResults(runtime.State);
            if (!runtime.State.IsTestMatch)
            {
                await eventBus.PublishAsync(
                    GameIntegrationChannels.MatchEnded,
                    new MatchEndedIntegrationEvent(
                        runtime.State.MatchId,
                        runtime.State.RoomCode,
                        runtime.State.WinnerUserId,
                        DateTime.UtcNow,
                        runtime.State.StartedAt,
                        runtime.State.TurnCounter,
                        runtime.State.DiscardPile.Count,
                        participantResults
                    )
                );

                await eventBus.PublishAsync(
                    GameIntegrationChannels.RoomUpdated,
                    new RoomUpdatedIntegrationEvent(
                        Type: "match_ended",
                        RoomCode: runtime.State.RoomCode,
                        Status: "waiting",
                        IsPublic: true,
                        CurrentPlayers: runtime.State.Players.Count,
                        MaxPlayers: runtime.State.Players.Count,
                        PlayerIds: runtime.State.Players.Select(p => p.UserId).ToList()
                    )
                );
            }

            var payload = WsServerEvent<WsStateSnapshotDto>.Create(
                "match_ended",
                new WsStateSnapshotDto(
                    runtime.State.MatchId,
                    runtime.State.RoomCode,
                    runtime.State.StateVersion,
                    runtime.State.Phase.ToString(),
                    runtime.State.StartedAt,
                    DateTime.UtcNow,
                    runtime.State.TurnIndex,
                    runtime.State.TurnCounter,
                    runtime.State.TurnTimerSeconds,
                    runtime.State.TurnEndsAt,
                    BuildPublicPlayers(runtime.State),
                    [],
                    runtime.State.DrawPile.Count,
                    runtime.State.DiscardPile.ToList(),
                    runtime.State.PendingDefuseUserId,
                    runtime.State.PendingBombOwnerUserId,
                    runtime.State.PendingBombCardCode,
                    runtime.State.DefuseWindowEndsAt,
                    runtime.State.BombReinsertWindowEndsAt,
                    runtime.State.PendingReactionUserId,
                    runtime.State.PendingReactionAction,
                    runtime.State.ReactionWindowEndsAt,
                    runtime.State.PendingNopeCount,
                    runtime.State.PendingFavorRequesterId,
                    runtime.State.PendingFavorTargetId,
                    runtime.State.FavorWindowEndsAt
                )
            );

            await hubContext.Clients.Group(GetRoomGroup(runtime.State.RoomCode)).SendAsync("ReceiveMessage", payload, cancellationToken);
            logger.LogInformation("Published match ended for {MatchId}", runtime.State.MatchId);
        }
    }

    private static List<MatchEndedParticipantIntegrationResult> BuildParticipantResults(Domain.MatchRuntime.MatchRuntimeState state)
    {
        var results = new List<MatchEndedParticipantIntegrationResult>();
        var rankByUserId = new Dictionary<Guid, int>();
        var rank = state.Players.Count;

        foreach (var eliminatedUserId in state.EliminationOrder)
        {
            rankByUserId[eliminatedUserId] = rank--;
        }

        if (state.WinnerUserId.HasValue)
        {
            rankByUserId[state.WinnerUserId.Value] = 1;
        }

        foreach (var player in state.Players)
        {
            if (!rankByUserId.TryGetValue(player.UserId, out var finalRank))
            {
                finalRank = Math.Max(1, rank);
                rank--;
            }

            results.Add(new MatchEndedParticipantIntegrationResult(player.UserId, finalRank));
        }

        return results;
    }

    private static IReadOnlyList<WsPlayerPublicStateDto> BuildPublicPlayers(Domain.MatchRuntime.MatchRuntimeState state)
    {
        return state.Players
            .Select(player => new WsPlayerPublicStateDto(
                player.UserId,
                player.Nickname,
                player.Connected,
                player.LifeState.ToString(),
                player.Hand?.Count ?? 0,
                player.PendingDrawCount,
                player.PendingReconnectUntil
            ))
            .ToList();
    }

    private static string GetRoomGroup(string roomCode) => $"room:{roomCode.ToUpperInvariant()}";
}
