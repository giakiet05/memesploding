using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.SignalR;
using Memesploding.Api.Extensions;
using Memesploding.Api.Services;
using Memesploding.Api.DTOs;
using Memesploding.Shared.Enums;

namespace Memesploding.Api.Hubs;

[Authorize]
public class PresenceHub : Hub
{
    private readonly IPresenceService _presenceService;
    private readonly IInvitationService _invitationService;
    private readonly ILogger<PresenceHub> _logger;

    public PresenceHub(
        IPresenceService presenceService, 
        IInvitationService invitationService,
        ILogger<PresenceHub> logger)
    {
        _presenceService = presenceService;
        _invitationService = invitationService;
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
            await _presenceService.UserConnectedAsync(userId.Value, connectionId);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error handling user connection for {UserId}", userId);
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
            await _presenceService.UserDisconnectedAsync(userId.Value, connectionId);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error handling user disconnection for {UserId}", userId);
        }

        await base.OnDisconnectedAsync(exception);
    }

    public async Task Heartbeat()
    {
        var userId = Context.User?.GetUserId();
        if (userId == null) return;

        await _presenceService.RefreshPresenceAsync(userId.Value);
    }

    public async Task InviteToRoom(string roomCode, Guid friendUserId)
    {
        var userId = Context.User?.GetUserId();
        if (userId == null)
        {
            await SendError(ErrorCode.Unauthorized, "User not authenticated");
            return;
        }

        var (success, errorCode, errorMessage) = await _invitationService.InviteToRoomAsync(
            userId.Value,
            roomCode,
            friendUserId
        );

        if (!success)
        {
            // Parse string sang Enum ErrorCode nếu cần, hoặc giả định Service trả về chuẩn
            if (Enum.TryParse<ErrorCode>(errorCode, true, out var code))
            {
                await SendError(code, errorMessage!);
            }
            else
            {
                await SendError(ErrorCode.InternalError, errorMessage!);
            }
        }
    }

    public async Task RespondInvitation(string invitationId, bool accepted)
    {
        var userId = Context.User?.GetUserId();
        if (userId == null)
        {
            await SendError(ErrorCode.Unauthorized, "User not authenticated");
            return;
        }

        var (success, errorCode, errorMessage) = await _invitationService.RespondInvitationAsync(
            userId.Value,
            invitationId,
            accepted
        );

        if (!success)
        {
            if (Enum.TryParse<ErrorCode>(errorCode, true, out var code))
            {
                await SendError(code, errorMessage!);
            }
            else
            {
                await SendError(ErrorCode.InternalError, errorMessage!);
            }
        }
    }

    public async Task RequestJoinRoom(string roomCode)
    {
        var userId = Context.User?.GetUserId();
        if (userId == null)
        {
            await SendError(ErrorCode.Unauthorized, "User not authenticated");
            return;
        }

        var (success, errorCode, errorMessage) = await _invitationService.RequestJoinRoomAsync(
            userId.Value,
            roomCode
        );

        if (!success)
        {
            if (Enum.TryParse<ErrorCode>(errorCode, true, out var code))
            {
                await SendError(code, errorMessage!);
            }
            else
            {
                await SendError(ErrorCode.InternalError, errorMessage!);
            }
        }
    }

    public async Task RespondJoinRequest(string requestId, bool accepted)
    {
        var userId = Context.User?.GetUserId();
        if (userId == null)
        {
            await SendError(ErrorCode.Unauthorized, "User not authenticated");
            return;
        }

        var (success, errorCode, errorMessage) = await _invitationService.RespondJoinRequestAsync(
            userId.Value,
            requestId,
            accepted
        );

        if (!success)
        {
            if (Enum.TryParse<ErrorCode>(errorCode, true, out var code))
            {
                await SendError(code, errorMessage!);
            }
            else
            {
                await SendError(ErrorCode.InternalError, errorMessage!);
            }
        }
    }

    private async Task SendError(ErrorCode code, string message)
    {
        var wsPayload = WsMessage<WsErrorDto>.Create(
            WsEventType.Error,
            new WsErrorDto(code, message)
        );

        await Clients.Caller.SendAsync("ReceiveMessage", wsPayload);
    }
}
