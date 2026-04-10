using Memesploding.Api.Data;
using Memesploding.Api.DTOs;
using Memesploding.Shared.Events;
using Memesploding.Shared.Enums;
using Memesploding.Shared.Infrastructure.EventBus;
using Memesploding.Shared.Infrastructure.Cache;
using Microsoft.EntityFrameworkCore;
using System.Text.Json;
using Memesploding.Api.Exceptions;

namespace Memesploding.Api.Services;

public class InvitationService(
    ICacheStore cache,
    ApplicationDbContext db,
    IEventBus eventBus,
    IRoomService roomService,
    ILogger<InvitationService> logger) : IInvitationService
{
    private const int InvitationTtlMinutes = 5;

    public async Task<ServiceResult> InviteToRoomAsync(
        Guid inviterId,
        string roomCode,
        Guid friendUserId)
    {
        var actualRoomCode = await cache.StringGetAsync(CacheKeys.UserInRoom(inviterId));

        if (string.IsNullOrEmpty(actualRoomCode) || actualRoomCode != roomCode)
        {
            return ServiceResult.Fail(ErrorCode.NotInRoom, "You are not in this room");
        }

        var friendRoomCode = await cache.StringGetAsync(CacheKeys.UserInRoom(friendUserId));
        if (!string.IsNullOrEmpty(friendRoomCode) && friendRoomCode == roomCode)
        {
            return ServiceResult.Fail(ErrorCode.PlayerAlreadyInRoom, "Friend is already in this room");
        }

        var areFriends = await db.Friendships
            .AnyAsync(f =>
                (f.UserId1 == inviterId && f.UserId2 == friendUserId ||
                 f.UserId1 == friendUserId && f.UserId2 == inviterId) &&
                f.Status == Shared.Enums.FriendshipStatus.Accepted
            );

        if (!areFriends)
        {
            return ServiceResult.Fail(ErrorCode.NotFriends, "Target user is not your friend");
        }

        var roomInfoKey = CacheKeys.RoomInfo(roomCode);
        var maxPlayersVal = await cache.HashGetAsync(roomInfoKey, "max_players");
        var currentPlayersCount = await cache.HashLengthAsync(CacheKeys.RoomParticipants(roomCode));

        if (!maxPlayersVal.IsNull)
        {
            var maxPlayers = int.Parse(maxPlayersVal.ToString());
            if (currentPlayersCount >= maxPlayers)
            {
                return ServiceResult.Fail(ErrorCode.RoomIsFull, "Room is full");
            }
        }

        var rateLimitKey = CacheKeys.InviteRateLimit(inviterId, friendUserId);
        var rateLimitExists = await cache.StringGetAsync(rateLimitKey);

        if (!string.IsNullOrEmpty(rateLimitExists))
        {
            return ServiceResult.Fail(ErrorCode.RateLimited, "Please wait before sending another invitation");
        }

        await cache.StringSetAsync(rateLimitKey, "1", TimeSpan.FromSeconds(5));

        var inviter = await db.Users.FindAsync(inviterId);
        if (inviter == null)
        {
            return ServiceResult.Fail(ErrorCode.NotFound, "Inviter not found");
        }

        var isPublicVal = await cache.HashGetAsync(roomInfoKey, "is_public");
        var isPublic = isPublicVal.IsNull || bool.Parse(isPublicVal.ToString());

        var invitationId = Guid.NewGuid().ToString();
        var expiresAt = DateTime.UtcNow.AddMinutes(InvitationTtlMinutes);

        var invitation = new InternalInvitationData(invitationId, roomCode, inviterId, friendUserId, DateTime.UtcNow, expiresAt);

        await cache.StringSetAsync(
            CacheKeys.Invitation(invitationId),
            JsonSerializer.Serialize(invitation),
            TimeSpan.FromMinutes(InvitationTtlMinutes)
        );

        // Bắn event ra Redis thay vì gọi SignalR trực tiếp
        var @event = new RoomInvitationSentEvent(
            invitationId,
            roomCode,
            inviterId,
            friendUserId,
            inviter.Username,
            inviter.AvatarUrl ?? "",
            isPublic,
            (int)currentPlayersCount,
            maxPlayersVal.IsNull ? 6 : int.Parse(maxPlayersVal.ToString()),
            expiresAt
        );

        await eventBus.PublishAsync(EventChannels.RoomInvitationSent, @event);
        logger.LogInformation("Published RoomInvitationSentEvent {InvitationId} to {Friend}", invitationId, friendUserId);

        return ServiceResult.Ok();
    }

    public async Task<ServiceResult> RespondInvitationAsync(
        Guid userId,
        string invitationId,
        bool accepted)
    {
        var json = await cache.StringGetAsync(CacheKeys.Invitation(invitationId));
        
        if (string.IsNullOrEmpty(json))
        {
            return ServiceResult.Fail(ErrorCode.InvitationNotFound, "Invitation not found or expired");
        }

        var invitation = JsonSerializer.Deserialize<InternalInvitationData>(json);
        
        if (invitation == null || invitation.InviteeId != userId)
        {
            return ServiceResult.Fail(ErrorCode.InvitationNotFound, "Invitation not found or expired");
        }

        if (accepted)
        {
            try
            {
                await roomService.JoinRoomAsync(userId, invitation.RoomCode);
            }
            catch (AppException ex)
            {
                return ServiceResult.Fail(ex.ErrorCode, ex.Message);
            }
        }

        var invitee = await db.Users.FindAsync(userId);
        if (invitee != null)
        {
            var @event = new RoomInvitationRespondedEvent(
                invitationId,
                invitation.InviterId,
                userId,
                invitee.Username,
                accepted
            );

            await eventBus.PublishAsync(EventChannels.RoomInvitationResponded, @event);
        }

        await cache.KeyDeleteAsync(CacheKeys.Invitation(invitationId));


        return ServiceResult.Ok();
    }

    public async Task<ServiceResult> RequestJoinRoomAsync(
        Guid requesterId,
        string roomCode)
    {
        var actualRoomCode = await cache.StringGetAsync(CacheKeys.UserInRoom(requesterId));
        if (!string.IsNullOrEmpty(actualRoomCode) && actualRoomCode == roomCode)
        {
            return ServiceResult.Fail(ErrorCode.PlayerAlreadyInRoom, "You are already in this room");
        }

        var roomKey = CacheKeys.RoomInfo(roomCode);
        var hostIdVal = await cache.HashGetAsync(roomKey, "host_id");

        if (hostIdVal.IsNull) return ServiceResult.Fail(ErrorCode.NotFound, "Room not found");

        var isPublicVal = await cache.HashGetAsync(roomKey, "is_public");
        var isPublic = !isPublicVal.IsNull && bool.Parse(isPublicVal.ToString());

        if (isPublic) return ServiceResult.Fail(ErrorCode.RoomPublic, "Room is public, join via REST");

        var currentPlayersCount = await cache.HashLengthAsync(CacheKeys.RoomParticipants(roomCode));
        var maxPlayersVal = await cache.HashGetAsync(roomKey, "max_players");

        if (!maxPlayersVal.IsNull && currentPlayersCount >= int.Parse(maxPlayersVal.ToString()))
        {
            return ServiceResult.Fail(ErrorCode.RoomIsFull, "Room is full");
        }

        var rateLimitKey = CacheKeys.JoinRequestRateLimit(requesterId, roomCode);
        if (!string.IsNullOrEmpty(await cache.StringGetAsync(rateLimitKey)))
        {
            return ServiceResult.Fail(ErrorCode.RateLimited, "Please wait");
        }
        await cache.StringSetAsync(rateLimitKey, "1", TimeSpan.FromSeconds(10));

        var requester = await db.Users.FindAsync(requesterId);
        if (requester == null) return ServiceResult.Fail(ErrorCode.NotFound, "User not found");

        var participantKeys = await cache.HashKeysAsync(CacheKeys.RoomParticipants(roomCode));
        var requestId = Guid.NewGuid().ToString();
        var expiresAt = DateTime.UtcNow.AddMinutes(InvitationTtlMinutes);

        var requestData = new { request_id = requestId, room_code = roomCode, requester_id = requesterId, expires_at = expiresAt };

        await cache.StringSetAsync(CacheKeys.JoinRequest(requestId), JsonSerializer.Serialize(requestData), TimeSpan.FromMinutes(InvitationTtlMinutes));

        var memberIds = participantKeys.Select(k => Guid.Parse(k.ToString())).ToList();

        if (memberIds.Count > 0)
        {
            var @event = new RoomJoinRequestSentEvent(
                requestId,
                roomCode,
                requesterId,
                requester.Username,
                requester.AvatarUrl ?? "",
                expiresAt,
                memberIds
            );

            await eventBus.PublishAsync(EventChannels.RoomJoinRequestSent, @event);
        }

        return ServiceResult.Ok();
    }

    public async Task<ServiceResult> RespondJoinRequestAsync(
        Guid userId,
        string requestId,
        bool accepted)
    {
        var requestJson = await cache.StringGetAsync(CacheKeys.JoinRequest(requestId));
        if (string.IsNullOrEmpty(requestJson)) return ServiceResult.Fail(ErrorCode.RequestNotFound, "Expired");

        var joinRequest = JsonSerializer.Deserialize<JoinRequestData>(requestJson);
        if (joinRequest == null) return ServiceResult.Fail(ErrorCode.RequestNotFound, "Invalid");

        var actualRoomCode = await cache.StringGetAsync(CacheKeys.UserInRoom(userId));
        if (string.IsNullOrEmpty(actualRoomCode) || actualRoomCode != joinRequest.RoomCode)
        {
            return ServiceResult.Fail(ErrorCode.Forbidden, "Not in room");
        }

        if (accepted)
        {
            try
            {
                await roomService.JoinRoomAsync(joinRequest.RequesterId, joinRequest.RoomCode);
            }
            catch (AppException ex)
            {
                return ServiceResult.Fail(ex.ErrorCode, ex.Message);
            }
        }

        var responder = await db.Users.FindAsync(userId);
        
        var @event = new RoomJoinRequestRespondedEvent(
            requestId,
            joinRequest.RoomCode,
            joinRequest.RequesterId,
            userId,
            responder?.Username ?? "Unknown",
            accepted
        );

        await eventBus.PublishAsync(EventChannels.RoomJoinRequestResponded, @event);

        await cache.KeyDeleteAsync(CacheKeys.JoinRequest(requestId));
        return ServiceResult.Ok();
    }

    private record InternalInvitationData(string InvitationId, string RoomCode, Guid InviterId, Guid InviteeId, DateTime CreatedAt, DateTime ExpiresAt);

    private class JoinRequestData
    {
        [System.Text.Json.Serialization.JsonPropertyName("request_id")]
        public string RequestId { get; set; } = "";
        [System.Text.Json.Serialization.JsonPropertyName("room_code")]
        public string RoomCode { get; set; } = "";
        [System.Text.Json.Serialization.JsonPropertyName("requester_id")]
        public Guid RequesterId { get; set; }
        [System.Text.Json.Serialization.JsonPropertyName("expires_at")]
        public DateTime ExpiresAt { get; set; }
    }
}
