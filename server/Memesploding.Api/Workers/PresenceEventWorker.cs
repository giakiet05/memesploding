using Memesploding.Api.DTOs;
using Memesploding.Api.Hubs;
using Memesploding.Api.Services;
using Memesploding.Shared.Events;
using Memesploding.Shared.Infrastructure.EventBus;
using Microsoft.AspNetCore.SignalR;

namespace Memesploding.Api.Workers;

public class PresenceEventWorker : BackgroundService
{
    private readonly IServiceProvider _serviceProvider;
    private readonly IEventBus _eventBus;
    private readonly ILogger<PresenceEventWorker> _logger;

    public PresenceEventWorker(
        IServiceProvider serviceProvider, 
        IEventBus eventBus, 
        ILogger<PresenceEventWorker> logger)
    {
        _serviceProvider = serviceProvider;
        _eventBus = eventBus;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation("PresenceEventWorker is starting.");

        await _eventBus.SubscribeAsync<UserConnectedEvent>(EventChannels.UserConnected, HandleUserConnectedAsync);
        await _eventBus.SubscribeAsync<UserDisconnectedEvent>(EventChannels.UserDisconnected, HandleUserDisconnectedAsync);
        await _eventBus.SubscribeAsync<FriendStatusChangedEvent>(EventChannels.FriendStatusChanged, HandleFriendStatusChangedAsync);
    }

    private async Task HandleUserConnectedAsync(UserConnectedEvent @event)
    {
        using var scope = _serviceProvider.CreateScope();
        var presenceService = scope.ServiceProvider.GetRequiredService<IPresenceService>();

        await presenceService.UserConnectedAsync(@event.UserId, @event.ConnectionId);
        _logger.LogInformation("Handled UserConnectedEvent for {UserId}", @event.UserId);
    }

    private async Task HandleUserDisconnectedAsync(UserDisconnectedEvent @event)
    {
        using var scope = _serviceProvider.CreateScope();
        var presenceService = scope.ServiceProvider.GetRequiredService<IPresenceService>();

        await presenceService.UserDisconnectedAsync(@event.UserId, @event.ConnectionId);
        _logger.LogInformation("Handled UserDisconnectedEvent for {UserId}", @event.UserId);
    }

    private async Task HandleFriendStatusChangedAsync(FriendStatusChangedEvent @event)
    {
        using var scope = _serviceProvider.CreateScope();
        var hubContext = scope.ServiceProvider.GetRequiredService<IHubContext<AppHub>>();
        var presenceService = scope.ServiceProvider.GetRequiredService<IPresenceService>();

        var statusDto = new WsFriendStatusDto(
            @event.UserId,
            @event.Username,
            @event.AvatarUrl,
            @event.Online,
            @event.LastSeen,
            MapToWsUserActivity(@event.Activity)
        );

        var wsMessage = WsMessage<WsFriendStatusDto>.Create(WsEventType.FriendStatusChanged, statusDto);

        foreach (var friendId in @event.FriendIdsToNotify)
        {
            var friendConnections = await presenceService.GetUserConnectionsAsync(friendId);
            if (friendConnections.Any())
            {
                await hubContext.Clients.Clients(friendConnections).SendAsync("ReceiveMessage", wsMessage);
            }
        }
        
        _logger.LogInformation("Broadcasted status change of {UserId} to its friends", @event.UserId);
    }

    private static WsUserActivityDto MapToWsUserActivity(PresenceUserActivity activity)
    {
        var room = activity.Room is null
            ? null
            : new WsRoomBriefDto(
                activity.Room.Code,
                activity.Room.Status,
                activity.Room.IsPublic,
                activity.Room.CurrentPlayers,
                activity.Room.MaxPlayers
            );

        return new WsUserActivityDto(activity.Type, room);
    }
}
