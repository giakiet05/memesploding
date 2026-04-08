using Memesploding.Api.Data;
using Memesploding.Api.DTOs;
using Microsoft.AspNetCore.SignalR;
using Microsoft.EntityFrameworkCore;
using Memesploding.Shared.Infrastructure.Cache;
using System.Text.Json;

namespace Memesploding.Api.Services;

public class InvitationService(
    ICacheStore cache,
    ApplicationDbContext db,
    IHubContext<Hubs.PresenceHub> hubContext,
    IPresenceService presenceService,
    ILogger<InvitationService> logger) : IInvitationService
{
    private const int InvitationTtlMinutes = 5;

    public async Task<(bool Success, string? ErrorCode, string? ErrorMessage)> InviteToRoomAsync(
        Guid inviterId,
        string roomCode,
        Guid friendUserId)
    {
        // 1. Check if inviter is in the room
        var actualRoomCode = await cache.StringGetAsync(CacheKeys.UserInRoom(inviterId));

        if (string.IsNullOrEmpty(actualRoomCode) || actualRoomCode != roomCode)
        {
            return (false, "NOT_IN_ROOM", "You are not in this room");
        }

        // 2. Check if they are friends
        var areFriends = await db.Friendships
            .AnyAsync(f =>
                (f.UserId1 == inviterId && f.UserId2 == friendUserId ||
                 f.UserId1 == friendUserId && f.UserId2 == inviterId) &&
                f.Status == Shared.Enums.FriendshipStatus.Accepted
            );

        if (!areFriends)
        {
            return (false, "NOT_FRIENDS", "Target user is not your friend");
        }

        // 3. Check room full
        var roomInfoKey = CacheKeys.RoomInfo(roomCode);
        var maxPlayersVal = await cache.HashGetAsync(roomInfoKey, "max_players");
        var currentPlayersCount = await cache.HashLengthAsync(CacheKeys.RoomParticipants(roomCode));

        if (!maxPlayersVal.IsNull)
        {
            var maxPlayers = int.Parse(maxPlayersVal.ToString());
            if (currentPlayersCount >= maxPlayers)
            {
                return (false, "ROOM_FULL", "Room is full");
            }
        }

        // 4. Rate limit check
        var rateLimitKey = CacheKeys.InviteRateLimit(inviterId, friendUserId);
        var rateLimitExists = await cache.StringGetAsync(rateLimitKey);

        if (!string.IsNullOrEmpty(rateLimitExists))
        {
            return (false, "RATE_LIMITED", "Please wait before sending another invitation");
        }

        await cache.StringSetAsync(rateLimitKey, "1", TimeSpan.FromSeconds(5));

        // 5. Get inviter info
        var inviter = await db.Users.FindAsync(inviterId);
        if (inviter == null)
        {
            return (false, "USER_NOT_FOUND", "Inviter not found");
        }

        // 6. Get basic room info
        var isPublicVal = await cache.HashGetAsync(roomInfoKey, "is_public");
        var isPublic = isPublicVal.IsNull || bool.Parse(isPublicVal.ToString());

        // 7. Create invitation
        var invitationId = Guid.NewGuid().ToString();
        var expiresAt = DateTime.UtcNow.AddMinutes(InvitationTtlMinutes);

        // Dùng Record chuẩn để Serialize/Deserialize đồng nhất PascalCase
        var invitation = new InternalInvitationData(invitationId, roomCode, inviterId, friendUserId, DateTime.UtcNow, expiresAt);

        await cache.StringSetAsync(
            CacheKeys.Invitation(invitationId),
            JsonSerializer.Serialize(invitation),
            TimeSpan.FromMinutes(InvitationTtlMinutes)
        );

        await cache.SetAddAsync(CacheKeys.UserInvitationIndex(friendUserId), invitationId);
        await cache.KeyExpireAsync(CacheKeys.UserInvitationIndex(friendUserId), TimeSpan.FromMinutes(InvitationTtlMinutes));

        // 9. Send invitation event
        var friendConnections = await presenceService.GetUserConnectionsAsync(friendUserId);

        if (friendConnections.Count > 0)
        {
            var invitationDto = new WsRoomInvitationDto(
                invitationId,
                roomCode,
                inviterId,
                inviter.Username,
                inviter.AvatarUrl ?? "",
                isPublic,
                (int)currentPlayersCount,
                maxPlayersVal.IsNull ? 6 : int.Parse(maxPlayersVal.ToString()),
                expiresAt
            );
            var wsMessage = WsMessage<WsRoomInvitationDto>.Create(WsEventType.RoomInvitationReceived, invitationDto);

            await hubContext.Clients.Clients(friendConnections).SendAsync("ReceiveMessage", wsMessage);

            logger.LogInformation("Sent invitation {InvitationId} to {Friend}", invitationId, friendUserId);
        }

        return (true, null, null);
    }

    public async Task<(bool Success, string? ErrorCode, string? ErrorMessage)> RespondInvitationAsync(
        Guid userId,
        string invitationId,
        bool accepted)
    {
        var json = await cache.StringGetAsync(CacheKeys.Invitation(invitationId));
        
        if (string.IsNullOrEmpty(json))
        {
            return (false, "INVITATION_NOT_FOUND", "Invitation not found or expired");
        }

        var invitation = JsonSerializer.Deserialize<InternalInvitationData>(json);
        
        // Bây giờ invitation.InviteeId sẽ được map đúng từ JSON!
        if (invitation == null || invitation.InviteeId != userId)
        {
            return (false, "INVITATION_NOT_FOUND", "Invitation not found or expired");
        }

        var inviterConnections = await presenceService.GetUserConnectionsAsync(invitation.InviterId);
        var invitee = await db.Users.FindAsync(userId);

        if (inviterConnections.Count > 0 && invitee != null)
        {
            var responseDto = new WsRoomInvitationResponseDto(invitationId, accepted, userId, invitee.Username);
            var wsMessage = WsMessage<WsRoomInvitationResponseDto>.Create(WsEventType.RoomInvitationResponse, responseDto);
            await hubContext.Clients.Clients(inviterConnections).SendAsync("ReceiveMessage", wsMessage);
        }

        await cache.KeyDeleteAsync(CacheKeys.Invitation(invitationId));
        await cache.SetRemoveAsync(CacheKeys.UserInvitationIndex(userId), invitationId);

        return (true, null, null);
    }

    public async Task<(bool Success, string? ErrorCode, string? ErrorMessage)> RequestJoinRoomAsync(
        Guid requesterId,
        string roomCode)
    {
        var actualRoomCode = await cache.StringGetAsync(CacheKeys.UserInRoom(requesterId));
        if (!string.IsNullOrEmpty(actualRoomCode) && actualRoomCode == roomCode)
        {
            return (false, "ALREADY_IN_ROOM", "You are already in this room");
        }

        var roomKey = CacheKeys.RoomInfo(roomCode);
        var hostIdVal = await cache.HashGetAsync(roomKey, "host_id");

        if (hostIdVal.IsNull) return (false, "ROOM_NOT_FOUND", "Room not found");

        var isPublicVal = await cache.HashGetAsync(roomKey, "is_public");
        var isPublic = !isPublicVal.IsNull && bool.Parse(isPublicVal.ToString());

        if (isPublic) return (false, "ROOM_PUBLIC", "Room is public, join via REST");

        var currentPlayersCount = await cache.HashLengthAsync(CacheKeys.RoomParticipants(roomCode));
        var maxPlayersVal = await cache.HashGetAsync(roomKey, "max_players");

        if (!maxPlayersVal.IsNull && currentPlayersCount >= int.Parse(maxPlayersVal.ToString()))
        {
            return (false, "ROOM_FULL", "Room is full");
        }

        var rateLimitKey = CacheKeys.JoinRequestRateLimit(requesterId, roomCode);
        if (!string.IsNullOrEmpty(await cache.StringGetAsync(rateLimitKey)))
        {
            return (false, "RATE_LIMITED", "Please wait");
        }
        await cache.StringSetAsync(rateLimitKey, "1", TimeSpan.FromSeconds(10));

        var requester = await db.Users.FindAsync(requesterId);
        if (requester == null) return (false, "USER_NOT_FOUND", "User not found");

        var participantKeys = await cache.HashKeysAsync(CacheKeys.RoomParticipants(roomCode));
        var requestId = Guid.NewGuid().ToString();
        var expiresAt = DateTime.UtcNow.AddMinutes(InvitationTtlMinutes);

        var requestData = new { request_id = requestId, room_code = roomCode, requester_id = requesterId, expires_at = expiresAt };

        await cache.StringSetAsync(CacheKeys.JoinRequest(requestId), JsonSerializer.Serialize(requestData), TimeSpan.FromMinutes(InvitationTtlMinutes));

        var memberConnectionIds = new List<string>();
        foreach (var pIdStr in participantKeys)
        {
            var connections = await presenceService.GetUserConnectionsAsync(Guid.Parse(pIdStr.ToString()));
            memberConnectionIds.AddRange(connections);
        }

        if (memberConnectionIds.Count > 0)
        {
            var requestDto = new WsJoinRoomRequestDto(requestId, roomCode, requesterId, requester.Username, requester.AvatarUrl ?? "", expiresAt);
            var wsMessage = WsMessage<WsJoinRoomRequestDto>.Create(WsEventType.JoinRoomRequested, requestDto);
            await hubContext.Clients.Clients(memberConnectionIds).SendAsync("ReceiveMessage", wsMessage);
        }

        return (true, null, null);
    }

    public async Task<(bool Success, string? ErrorCode, string? ErrorMessage)> RespondJoinRequestAsync(
        Guid userId,
        string requestId,
        bool accepted)
    {
        var requestJson = await cache.StringGetAsync(CacheKeys.JoinRequest(requestId));
        if (string.IsNullOrEmpty(requestJson)) return (false, "REQUEST_NOT_FOUND", "Expired");

        var joinRequest = JsonSerializer.Deserialize<JoinRequestData>(requestJson);
        if (joinRequest == null) return (false, "REQUEST_NOT_FOUND", "Invalid");

        var actualRoomCode = await cache.StringGetAsync(CacheKeys.UserInRoom(userId));
        if (string.IsNullOrEmpty(actualRoomCode) || actualRoomCode != joinRequest.RoomCode)
        {
            return (false, "NOT_AUTHORIZED", "Not in room");
        }

        if (accepted)
        {
            var currentPlayersCount = await cache.HashLengthAsync(CacheKeys.RoomParticipants(joinRequest.RoomCode));
            var maxPlayersVal = await cache.HashGetAsync(CacheKeys.RoomInfo(joinRequest.RoomCode), "max_players");
            if (!maxPlayersVal.IsNull && currentPlayersCount >= int.Parse(maxPlayersVal.ToString()))
                return (false, "ROOM_FULL", "Room full");
        }

        var requesterConnections = await presenceService.GetUserConnectionsAsync(joinRequest.RequesterId);
        var responder = await db.Users.FindAsync(userId);

        if (requesterConnections.Count > 0)
        {
            var responseDto = new WsJoinRoomResponseDto(requestId, joinRequest.RoomCode, accepted, userId, responder?.Username);
            var wsMessage = WsMessage<WsJoinRoomResponseDto>.Create(WsEventType.JoinRoomResponse, responseDto);
            await hubContext.Clients.Clients(requesterConnections).SendAsync("ReceiveMessage", wsMessage);
        }

        await cache.KeyDeleteAsync(CacheKeys.JoinRequest(requestId));
        return (true, null, null);
    }

    // DTO nội bộ dùng Record cho đồng nhất PascalCase
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
