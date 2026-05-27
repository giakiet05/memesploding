using Memesploding.Game.Infrastructure.Runtime;
using Memesploding.Game.Infrastructure.StateStore;
using Memesploding.Shared.Messaging.EventBus;
using Memesploding.Shared.Messaging.Integration.Channels;
using Memesploding.Shared.Messaging.Integration.Events;

namespace Memesploding.Game.Infrastructure.Integration;

public class StartMatchConsumer(
    IEventBus eventBus,
    IMatchRuntimeManager runtimeManager,
    IGameSnapshotStore snapshotStore,
    ILogger<StartMatchConsumer> logger
) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        logger.LogInformation("StartMatchConsumer is starting.");

        await eventBus.SubscribeAsync<StartMatchRequestedEvent>(
            GameIntegrationChannels.StartMatchRequested,
            HandleStartMatchRequestedAsync
        );
    }

    private async Task HandleStartMatchRequestedAsync(StartMatchRequestedEvent @event)
    {
        var runtime = runtimeManager.CreateOrReplace(@event);
        await snapshotStore.SaveSnapshotAsync(runtime.State);

        if (!runtime.State.IsTestMatch)
        {
            await eventBus.PublishAsync(
                GameIntegrationChannels.MatchStarted,
                new MatchStartedIntegrationEvent(runtime.State.MatchId, runtime.State.RoomCode, runtime.State.StartedAt ?? DateTime.UtcNow)
            );

            await eventBus.PublishAsync(
                GameIntegrationChannels.RoomUpdated,
                new RoomUpdatedIntegrationEvent(
                    Type: "match_started",
                    RoomCode: runtime.State.RoomCode,
                    Status: "playing",
                    IsPublic: true,
                    CurrentPlayers: runtime.State.Players.Count,
                    MaxPlayers: runtime.State.Players.Count,
                    PlayerIds: runtime.State.Players.Select(p => p.UserId).ToList()
                )
            );
        }

        logger.LogInformation("Started match runtime {MatchId} for room {RoomCode}", runtime.State.MatchId, runtime.State.RoomCode);
    }
}
