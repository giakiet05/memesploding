using Memesploding.Api.DTOs;
using Memesploding.Api.Hubs;
using Memesploding.Api.Services;
using Memesploding.Shared.Events;
using Memesploding.Shared.Infrastructure.EventBus;
using Microsoft.AspNetCore.SignalR;
using StackExchange.Redis;
using System.Text.Json;

namespace Memesploding.Api.Workers;

public class RoomEventWorker : BackgroundService
{
    private readonly IServiceProvider _serviceProvider;
    private readonly IEventBus _eventBus;
    private readonly IConnectionMultiplexer _redis;
    private readonly ILogger<RoomEventWorker> _logger;

    public RoomEventWorker(
        IServiceProvider serviceProvider, 
        IEventBus eventBus,
        IConnectionMultiplexer redis,
        ILogger<RoomEventWorker> logger)
    {
        _serviceProvider = serviceProvider;
        _eventBus = eventBus;
        _redis = redis;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation("RoomEventWorker is starting.");

        // Subscribe to Room/Invitation events (SignalR)
        await _eventBus.SubscribeAsync<RoomInvitationSentEvent>(EventChannels.RoomInvitationSent, HandleRoomInvitationSentAsync);
        await _eventBus.SubscribeAsync<RoomInvitationRespondedEvent>(EventChannels.RoomInvitationResponded, HandleRoomInvitationRespondedAsync);
        await _eventBus.SubscribeAsync<RoomJoinRequestSentEvent>(EventChannels.RoomJoinRequestSent, HandleRoomJoinRequestSentAsync);
        await _eventBus.SubscribeAsync<RoomJoinRequestRespondedEvent>(EventChannels.RoomJoinRequestResponded, HandleRoomJoinRequestRespondedAsync);
        
        // Subscribe to Room updates from Game Server
        await _eventBus.SubscribeAsync<RoomUpdatedEvent>(EventChannels.RoomUpdates, HandleRoomUpdateAsync);
    }

    private async Task HandleRoomUpdateAsync(RoomUpdatedEvent update)
    {
        var db = _redis.GetDatabase();

        // 1. Update room:{code}:info cache
        if (!string.IsNullOrEmpty(update.RoomCode))
        {
            var roomInfoKey = $"room:{update.RoomCode}:info";
            var roomInfo = new
            {
                is_public = update.IsPublic,
                current_players = update.CurrentPlayers,
                max_players = update.MaxPlayers
            };

            await db.StringSetAsync(
                roomInfoKey,
                JsonSerializer.Serialize(roomInfo),
                TimeSpan.FromHours(1) // Cache for 1 hour
            );

            _logger.LogDebug("Updated room info cache: {RoomCode}", update.RoomCode);
        }

        // 2. Update presence for all players in room
        if (update.PlayerIds != null && update.PlayerIds.Count > 0)
        {
            using var scope = _serviceProvider.CreateScope();
            var presenceService = scope.ServiceProvider.GetRequiredService<IPresenceService>();

            var updateTasks = update.PlayerIds.Select(playerId => 
                presenceService.UpdateUserActivityAsync(playerId)
            );

            await Task.WhenAll(updateTasks);

            _logger.LogInformation(
                "Updated presence for {Count} players in room {RoomCode}",
                update.PlayerIds.Count,
                update.RoomCode
            );
        }
    }

    private async Task HandleRoomInvitationSentAsync(RoomInvitationSentEvent @event)
    {
        using var scope = _serviceProvider.CreateScope();
        var hubContext = scope.ServiceProvider.GetRequiredService<IHubContext<AppHub>>();
        var presenceService = scope.ServiceProvider.GetRequiredService<IPresenceService>();

        var friendConnections = await presenceService.GetUserConnectionsAsync(@event.InviteeId);
        if (friendConnections.Count > 0)
        {
            var invitationDto = new WsRoomInvitationDto(
                @event.InvitationId,
                @event.RoomCode,
                @event.InviterId,
                @event.InviterUsername,
                @event.InviterAvatarUrl,
                @event.IsPublic,
                @event.CurrentPlayers,
                @event.MaxPlayers,
                @event.ExpiresAt
            );
            var wsMessage = WsMessage<WsRoomInvitationDto>.Create(WsEventType.RoomInvitationReceived, invitationDto);

            await hubContext.Clients.Clients(friendConnections).SendAsync("ReceiveMessage", wsMessage);
            _logger.LogInformation("Handled RoomInvitationSentEvent to {InviteeId}", @event.InviteeId);
        }
    }

    private async Task HandleRoomInvitationRespondedAsync(RoomInvitationRespondedEvent @event)
    {
        using var scope = _serviceProvider.CreateScope();
        var hubContext = scope.ServiceProvider.GetRequiredService<IHubContext<AppHub>>();
        var presenceService = scope.ServiceProvider.GetRequiredService<IPresenceService>();

        var inviterConnections = await presenceService.GetUserConnectionsAsync(@event.InviterId);
        if (inviterConnections.Count > 0)
        {
            var responseDto = new WsRoomInvitationResponseDto(
                @event.InvitationId,
                @event.Accepted,
                @event.InviteeId,
                @event.InviteeUsername
            );
            var wsMessage = WsMessage<WsRoomInvitationResponseDto>.Create(WsEventType.RoomInvitationResponse, responseDto);

            await hubContext.Clients.Clients(inviterConnections).SendAsync("ReceiveMessage", wsMessage);
            _logger.LogInformation("Handled RoomInvitationRespondedEvent for {InviterId}", @event.InviterId);
        }
    }

    private async Task HandleRoomJoinRequestSentAsync(RoomJoinRequestSentEvent @event)
    {
        using var scope = _serviceProvider.CreateScope();
        var hubContext = scope.ServiceProvider.GetRequiredService<IHubContext<AppHub>>();
        var presenceService = scope.ServiceProvider.GetRequiredService<IPresenceService>();

        var memberConnectionIds = new List<string>();
        foreach (var memberId in @event.RoomMemberIds)
        {
            var connections = await presenceService.GetUserConnectionsAsync(memberId);
            memberConnectionIds.AddRange(connections);
        }

        if (memberConnectionIds.Count > 0)
        {
            var requestDto = new WsJoinRoomRequestDto(
                @event.RequestId,
                @event.RoomCode,
                @event.RequesterId,
                @event.RequesterUsername,
                @event.RequesterAvatarUrl,
                @event.ExpiresAt
            );
            var wsMessage = WsMessage<WsJoinRoomRequestDto>.Create(WsEventType.JoinRoomRequested, requestDto);

            await hubContext.Clients.Clients(memberConnectionIds).SendAsync("ReceiveMessage", wsMessage);
            _logger.LogInformation("Handled RoomJoinRequestSentEvent for room {RoomCode}", @event.RoomCode);
        }
    }

    private async Task HandleRoomJoinRequestRespondedAsync(RoomJoinRequestRespondedEvent @event)
    {
        using var scope = _serviceProvider.CreateScope();
        var hubContext = scope.ServiceProvider.GetRequiredService<IHubContext<AppHub>>();
        var presenceService = scope.ServiceProvider.GetRequiredService<IPresenceService>();

        var requesterConnections = await presenceService.GetUserConnectionsAsync(@event.RequesterId);
        if (requesterConnections.Count > 0)
        {
            var responseDto = new WsJoinRoomResponseDto(
                @event.RequestId,
                @event.RoomCode,
                @event.Accepted,
                @event.ResponderId,
                @event.ResponderUsername
            );
            var wsMessage = WsMessage<WsJoinRoomResponseDto>.Create(WsEventType.JoinRoomResponse, responseDto);

            await hubContext.Clients.Clients(requesterConnections).SendAsync("ReceiveMessage", wsMessage);
            _logger.LogInformation("Handled RoomJoinRequestRespondedEvent for {RequesterId}", @event.RequesterId);
        }
    }
}
