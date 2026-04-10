using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.SignalR;
using Memesploding.Api.Extensions;
using Memesploding.Api.Services;
using Memesploding.Api.DTOs;
using Memesploding.Shared.Enums;
using Memesploding.Shared.Events;
using Memesploding.Shared.Infrastructure.EventBus;

namespace Memesploding.Api.Hubs;

[Authorize]
public partial class AppHub : Hub
{
    private readonly IPresenceService _presenceService;
    private readonly IInvitationService _invitationService;
    private readonly IEventBus _eventBus;
    private readonly ILogger<AppHub> _logger;

    public AppHub(
        IPresenceService presenceService, 
        IInvitationService invitationService,
        IEventBus eventBus,
        ILogger<AppHub> logger)
    {
        _presenceService = presenceService;
        _invitationService = invitationService;
        _eventBus = eventBus;
        _logger = logger;
    }

    public override async Task OnConnectedAsync()
    {
        var userId = Context.User?.GetUserId();
        if (userId == null)
        {
            _logger.LogWarning("Connection attempt without valid user ID");
            Context.Abort();
            return;
        }

        var connectionId = Context.ConnectionId;
        _logger.LogInformation("User {UserId} connected with connection {ConnectionId}", userId, connectionId);

        try
        {
            await _eventBus.PublishAsync(EventChannels.UserConnected, new UserConnectedEvent(userId.Value, connectionId));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error publishing user connection event for {UserId}", userId);
            throw;
        }

        await base.OnConnectedAsync();
    }

    public override async Task OnDisconnectedAsync(Exception? exception)
    {
        var userId = Context.User?.GetUserId();
        if (userId == null)
        {
            await base.OnDisconnectedAsync(exception);
            return;
        }

        var connectionId = Context.ConnectionId;
        _logger.LogInformation("User {UserId} disconnected, connection {ConnectionId}", userId, connectionId);

        try
        {
            await _eventBus.PublishAsync(EventChannels.UserDisconnected, new UserDisconnectedEvent(userId.Value, connectionId));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error publishing user disconnection event for {UserId}", userId);
        }

        await base.OnDisconnectedAsync(exception);
    }
}
