using Memesploding.Api.Extensions;
using Memesploding.Api.Exceptions;
using Memesploding.Shared.Enums;

namespace Memesploding.Api.Hubs;

public partial class AppHub
{
    public async Task LeaveRoom(string roomCode)
    {
        var userId = Context.User?.GetUserId();
        if (userId == null)
        {
            await SendError(ErrorCode.Unauthorized, "User not authenticated");
            return;
        }

        try
        {
            await _roomService.LeaveRoomAsync(userId.Value, roomCode);
        }
        catch (AppException ex)
        {
            await SendError(ex.ErrorCode, ex.Message);
        }
    }

    public async Task KickRoomMember(string roomCode, Guid targetUserId)
    {
        var userId = Context.User?.GetUserId();
        if (userId == null)
        {
            await SendError(ErrorCode.Unauthorized, "User not authenticated");
            return;
        }

        try
        {
            await _roomService.KickPlayerAsync(userId.Value, roomCode, targetUserId);
        }
        catch (AppException ex)
        {
            await SendError(ex.ErrorCode, ex.Message);
        }
    }

    public async Task SetReadyStatus(string roomCode, bool isReady)
    {
        var userId = Context.User?.GetUserId();
        if (userId == null)
        {
            await SendError(ErrorCode.Unauthorized, "User not authenticated");
            return;
        }

        try
        {
            await _roomService.UpdateReadyStatusAsync(userId.Value, roomCode, isReady);
        }
        catch (AppException ex)
        {
            await SendError(ex.ErrorCode, ex.Message);
        }
    }

    public async Task StartRoomMatch(string roomCode)
    {
        var userId = Context.User?.GetUserId();
        if (userId == null)
        {
            await SendError(ErrorCode.Unauthorized, "User not authenticated");
            return;
        }

        try
        {
            await _roomService.StartMatchAsync(userId.Value, roomCode);
        }
        catch (AppException ex)
        {
            await SendError(ex.ErrorCode, ex.Message);
        }
    }

}
