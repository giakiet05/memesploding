using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.SignalR;
using Memesploding.Api.Extensions;
using Memesploding.Api.Services;
using Memesploding.Api.DTOs;
using Memesploding.Shared.Enums;

namespace Memesploding.Api.Hubs;

public partial class AppHub
{
    public async Task InviteToRoom(string roomCode, Guid friendUserId)
    {
        var userId = Context.User?.GetUserId();
        if (userId == null)
        {
            await SendError(ErrorCode.Unauthorized, "User not authenticated");
            return;
        }

        var result = await _invitationService.InviteToRoomAsync(
            userId.Value,
            roomCode,
            friendUserId
        );

        if (!result.Success)
        {
            await SendError(result.ErrorCode ?? ErrorCode.InternalError, result.ErrorMessage!);
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

        var result = await _invitationService.RespondInvitationAsync(
            userId.Value,
            invitationId,
            accepted
        );

        if (!result.Success)
        {
            await SendError(result.ErrorCode ?? ErrorCode.InternalError, result.ErrorMessage!);
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

        var result = await _invitationService.RequestJoinRoomAsync(
            userId.Value,
            roomCode
        );

        if (!result.Success)
        {
            await SendError(result.ErrorCode ?? ErrorCode.InternalError, result.ErrorMessage!);
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

        var result = await _invitationService.RespondJoinRequestAsync(
            userId.Value,
            requestId,
            accepted
        );

        if (!result.Success)
        {
            await SendError(result.ErrorCode ?? ErrorCode.InternalError, result.ErrorMessage!);
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
