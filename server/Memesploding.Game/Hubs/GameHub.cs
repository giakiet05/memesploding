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
            "Connected",
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
            await Clients.Caller.SendAsync("ReceiveMessage", WsServerEvent<WsErrorDto>.Create("Error", new WsErrorDto("Unauthorized connection context")));
            return;
        }

        if (string.Equals(command.Event, "RequestServerTime", StringComparison.OrdinalIgnoreCase))
        {
            await SendServerTimeAsync(command, Context.ConnectionAborted);
            return;
        }

        var stateVersion = await commandDispatcher.DispatchAsync(connectionContext, command, Context.ConnectionAborted);
        if (stateVersion.HasValue)
        {
            await Clients.Caller.SendAsync("ReceiveMessage", WsServerEvent<WsAckDto>.Create("Ack", new WsAckDto(stateVersion.Value)));
        }

        if (string.Equals(command.Event, "ReconnectMatch", StringComparison.OrdinalIgnoreCase) ||
            string.Equals(command.Event, "AckStateVersion", StringComparison.OrdinalIgnoreCase) ||
            string.Equals(command.Event, "RequestStateSnapshot", StringComparison.OrdinalIgnoreCase))
        {
            await SendStateSnapshotAsync(connectionContext, Context.ConnectionAborted);
        }
    }

    private async Task SendServerTimeAsync(WsClientCommand command, CancellationToken cancellationToken)
    {
        var now = DateTimeOffset.UtcNow;
        var payload = WsServerEvent<WsServerTimeDto>.Create(
            "ServerTime",
            new WsServerTimeDto(
                now.UtcDateTime,
                now.ToUnixTimeMilliseconds(),
                GetClientSentAtMs(command)
            )
        );

        await Clients.Caller.SendAsync("ReceiveMessage", payload, cancellationToken);
    }

    private static long? GetClientSentAtMs(WsClientCommand command)
    {
        if (command.Data.ValueKind != System.Text.Json.JsonValueKind.Object)
        {
            return null;
        }

        if (command.Data.TryGetProperty("clientSentAtMs", out var camelCaseValue) &&
            camelCaseValue.ValueKind == System.Text.Json.JsonValueKind.Number &&
            camelCaseValue.TryGetInt64(out var camelCaseTimestamp))
        {
            return camelCaseTimestamp;
        }

        if (command.Data.TryGetProperty("ClientSentAtMs", out var pascalCaseValue) &&
            pascalCaseValue.ValueKind == System.Text.Json.JsonValueKind.Number &&
            pascalCaseValue.TryGetInt64(out var pascalCaseTimestamp))
        {
            return pascalCaseTimestamp;
        }

        return null;
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
                snapshot.StartedAt,
                DateTime.UtcNow,
                snapshot.TurnIndex,
                snapshot.TurnCounter,
                snapshot.TurnTimerSeconds,
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
                self?.Hand?.ToList() ?? [],
                snapshot.DrawPile.Count,
                snapshot.DiscardPile.ToList(),
                snapshot.PendingDefuseUserId,
                snapshot.PendingBombOwnerUserId,
                snapshot.PendingBombCardCode,
                snapshot.DefuseWindowEndsAt,
                snapshot.BombReinsertWindowEndsAt,
                snapshot.PendingReactionUserId,
                snapshot.PendingReactionAction,
                snapshot.ReactionWindowEndsAt,
                snapshot.PendingNopeCount,
                snapshot.PendingFavorRequesterId,
                snapshot.PendingFavorTargetId,
                snapshot.FavorWindowEndsAt
            )
        );

        await Clients.Caller.SendAsync("ReceiveMessage", payload, cancellationToken);
    }

    private static string GetRoomGroup(string roomCode) => $"room:{roomCode.ToUpperInvariant()}";
}
