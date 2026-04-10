using Memesploding.Api.Services;
using Memesploding.Shared.Events;
using Memesploding.Shared.Infrastructure.EventBus;

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
}
