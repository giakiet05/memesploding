using System.Collections.Concurrent;
using Memesploding.Game.Application;
using Memesploding.Game.Auth;
using Memesploding.Game.DTOs;
using Memesploding.Game.Infrastructure.Runtime;
using Memesploding.Game.Infrastructure.StateStore;
using Microsoft.AspNetCore.SignalR;

namespace Memesploding.Game.Hubs;

public class GameHub(
    IGameTicketValidator ticketValidator,
    IGameCommandDispatcher commandDispatcher,
    IMatchRuntimeManager runtimeManager,
    IGameSnapshotStore snapshotStore,
    ILogger<GameHub> logger
) : Hub
{
    private static readonly ConcurrentDictionary<string, GameConnectionContext> Connections = new();

    public override async Task OnConnectedAsync()
    {
        var token = Context.GetHttpContext()?.Request.Query["access_token"].ToString();
        if (string.IsNullOrWhiteSpace(token) || !ticketValidator.TryValidate(token, out var connectionContext) || connectionContext == null)
        {
            logger.LogWarning("Game WS connection rejected. Invalid game ticket.");
            Context.Abort();
            return;
        }

        Connections[Context.ConnectionId] = connectionContext;
        runtimeManager.MarkPlayerConnection(connectionContext.RoomCode, connectionContext.UserId, true);
        await Groups.AddToGroupAsync(Context.ConnectionId, GetRoomGroup(connectionContext.RoomCode));

        var payload = WsServerEvent<WsConnectedDto>.Create(
            "connected",
            new WsConnectedDto(connectionContext.RoomCode, connectionContext.MatchId)
        );
        await Clients.Caller.SendAsync("ReceiveMessage", payload);
        await SendStateSnapshotAsync(connectionContext, Context.ConnectionAborted);

        await base.OnConnectedAsync();
    }

    public override async Task OnDisconnectedAsync(Exception? exception)
    {
        if (Connections.TryRemove(Context.ConnectionId, out var connectionContext))
        {
            runtimeManager.MarkPlayerConnection(connectionContext.RoomCode, connectionContext.UserId, false);
            await Groups.RemoveFromGroupAsync(Context.ConnectionId, GetRoomGroup(connectionContext.RoomCode));
        }

        await base.OnDisconnectedAsync(exception);
    }

    public async Task SendCommand(WsClientCommand command)
    {
        if (!Connections.TryGetValue(Context.ConnectionId, out var connectionContext))
        {
            await Clients.Caller.SendAsync("ReceiveMessage", WsServerEvent<WsErrorDto>.Create("error", new WsErrorDto("Unauthorized connection context")));
            return;
        }

        var stateVersion = await commandDispatcher.DispatchAsync(connectionContext, command, Context.ConnectionAborted);
        if (stateVersion.HasValue)
        {
            await Clients.Caller.SendAsync("ReceiveMessage", WsServerEvent<WsAckDto>.Create("ack", new WsAckDto(stateVersion.Value)));
        }

        if (string.Equals(command.Event, "reconnectmatch", StringComparison.OrdinalIgnoreCase) ||
            string.Equals(command.Event, "ackstateversion", StringComparison.OrdinalIgnoreCase) ||
            string.Equals(command.Event, "requeststatesnapshot", StringComparison.OrdinalIgnoreCase))
        {
            await SendStateSnapshotAsync(connectionContext, Context.ConnectionAborted);
        }
    }

    private async Task SendStateSnapshotAsync(GameConnectionContext connectionContext, CancellationToken cancellationToken)
    {
        Domain.MatchRuntime.MatchRuntimeState? snapshot = null;
        if (runtimeManager.TryGetByRoomCode(connectionContext.RoomCode, out var runtime) && runtime != null)
        {
            snapshot = runtime.State;
        }
        else
        {
            snapshot = await snapshotStore.GetSnapshotByRoomCodeAsync(connectionContext.RoomCode);
        }

        if (snapshot == null)
        {
            return;
        }

        var self = snapshot.Players.FirstOrDefault(p => p.UserId == connectionContext.UserId);
        var payload = WsServerEvent<WsStateSnapshotDto>.Create(
            "state_snapshot",
            new WsStateSnapshotDto(
                snapshot.MatchId,
                snapshot.RoomCode,
                snapshot.StateVersion,
                snapshot.Phase.ToString(),
                snapshot.TurnIndex,
                snapshot.TurnCounter,
                snapshot.TurnEndsAt,
                snapshot.Players
                    .Select(player => new WsPlayerPublicStateDto(
                        player.UserId,
                        player.Nickname,
                        player.Connected,
                        player.LifeState.ToString(),
                        player.Hand?.Count ?? 0,
                        player.PendingDrawCount,
                        player.PendingReconnectUntil
                    ))
                    .ToList(),
                self?.Hand?.ToList() ?? []
            )
        );

        await Clients.Caller.SendAsync("ReceiveMessage", payload, cancellationToken);
    }

    private static string GetRoomGroup(string roomCode) => $"room:{roomCode.ToUpperInvariant()}";
}
