using Memesploding.Api.DTOs;
using Memesploding.Api.Hubs;
using Memesploding.Shared.Events;
using Memesploding.Shared.Infrastructure.EventBus;
using Microsoft.AspNetCore.SignalR;

namespace Memesploding.Api.Workers;

public class FriendshipEventWorker : BackgroundService
{
    private readonly IServiceProvider _serviceProvider;
    private readonly IEventBus _eventBus;
    private readonly ILogger<FriendshipEventWorker> _logger;

    public FriendshipEventWorker(
        IServiceProvider serviceProvider, 
        IEventBus eventBus, 
        ILogger<FriendshipEventWorker> logger)
    {
        _serviceProvider = serviceProvider;
        _eventBus = eventBus;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation("FriendshipEventWorker is starting.");

        await _eventBus.SubscribeAsync<FriendRequestSentEvent>(EventChannels.FriendRequestSent, HandleFriendRequestSentAsync);
        await _eventBus.SubscribeAsync<FriendRequestRespondedEvent>(EventChannels.FriendRequestResponded, HandleFriendRequestRespondedAsync);
    }

    private async Task HandleFriendRequestSentAsync(FriendRequestSentEvent @event)
    {
        using var scope = _serviceProvider.CreateScope();
        var hubContext = scope.ServiceProvider.GetRequiredService<IHubContext<AppHub>>();

        var wsPayload = WsMessage<WsFriendRequestDto>.Create(
            WsEventType.FriendRequestReceived, 
            new WsFriendRequestDto(@event.SenderId, @event.SenderUsername, @event.SenderAvatarUrl, $"User {@event.SenderUsername} sent you a friend request.")
        );
        
        await hubContext.Clients.User(@event.ReceiverId.ToString()).SendAsync("ReceiveMessage", wsPayload);
        _logger.LogInformation("Handled FriendRequestSentEvent from {SenderId} to {ReceiverId}", @event.SenderId, @event.ReceiverId);
    }

    private async Task HandleFriendRequestRespondedAsync(FriendRequestRespondedEvent @event)
    {
        using var scope = _serviceProvider.CreateScope();
        var hubContext = scope.ServiceProvider.GetRequiredService<IHubContext<AppHub>>();

        var wsPayload = WsMessage<WsFriendRequestDto>.Create(
            WsEventType.FriendRequestAccepted, 
            new WsFriendRequestDto(@event.ResponderId, @event.ResponderUsername, "", $"{@event.ResponderUsername} accepted your friend request.")
        );
        
        await hubContext.Clients.User(@event.RequesterId.ToString()).SendAsync("ReceiveMessage", wsPayload);
        _logger.LogInformation("Handled FriendRequestRespondedEvent for {RequesterId}", @event.RequesterId);
    }
}
