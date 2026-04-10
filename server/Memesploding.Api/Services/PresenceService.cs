using Memesploding.Api.Data;
using Memesploding.Api.DTOs;
using Microsoft.AspNetCore.SignalR;
using Microsoft.EntityFrameworkCore;
using Memesploding.Shared.Infrastructure.Cache;
using System.Text.Json;

namespace Memesploding.Api.Services;

public class PresenceService : IPresenceService
{
    private readonly ICacheStore _cache;
    private readonly ApplicationDbContext _db;
    private readonly IHubContext<Hubs.AppHub> _hubContext;
    private readonly ILogger<PresenceService> _logger;
    
    private const int PresenceTtlMinutes = 10;

    public PresenceService(
        ICacheStore cache, 
        ApplicationDbContext db,
        IHubContext<Hubs.AppHub> hubContext,
        ILogger<PresenceService> logger)
    {
        _cache = cache;
        _db = db;
        _hubContext = hubContext;
        _logger = logger;
    }

    public async Task UserConnectedAsync(Guid userId, string connectionId)
    {
        // Dùng CacheKeys cho đồng bộ
        var connectionsKey = CacheKeys.UserConnections(userId);
        await _cache.SetAddAsync(connectionsKey, connectionId);
        await _cache.KeyExpireAsync(connectionsKey, TimeSpan.FromMinutes(PresenceTtlMinutes));

        var presenceKey = CacheKeys.UserPresence(userId);
        var existingPresence = await _cache.StringGetAsync(presenceKey);
        bool wasOffline = string.IsNullOrEmpty(existingPresence);

        // Build rich activity
        var activity = await BuildActivityAsync(userId);

        var presenceData = new InternalPresenceData
        {
            Online = true,
            Activity = activity,
            UpdatedAt = DateTime.UtcNow
        };

        await _cache.StringSetAsync(
            presenceKey,
            JsonSerializer.Serialize(presenceData),
            TimeSpan.FromMinutes(PresenceTtlMinutes)
        );

        if (wasOffline)
        {
            await BroadcastFriendStatusAsync(userId);
        }
    }

    public async Task UserDisconnectedAsync(Guid userId, string connectionId)
    {
        var connectionsKey = CacheKeys.UserConnections(userId);
        await _cache.SetRemoveAsync(connectionsKey, connectionId);

        var connectionCount = await _cache.SetLengthAsync(connectionsKey);
        
        if (connectionCount == 0)
        {
            var presenceKey = CacheKeys.UserPresence(userId);
            var presenceData = new InternalPresenceData
            {
                Online = false,
                LastSeen = DateTime.UtcNow,
                Activity = new WsUserActivityDto("idle"),
                UpdatedAt = DateTime.UtcNow
            };

            await _cache.StringSetAsync(
                presenceKey,
                JsonSerializer.Serialize(presenceData),
                TimeSpan.FromMinutes(PresenceTtlMinutes)
            );

            await BroadcastFriendStatusAsync(userId);
            await _cache.KeyDeleteAsync(connectionsKey);
        }
    }

    public async Task RefreshPresenceAsync(Guid userId)
    {
        await _cache.KeyExpireAsync(CacheKeys.UserPresence(userId), TimeSpan.FromMinutes(PresenceTtlMinutes));
        await _cache.KeyExpireAsync(CacheKeys.UserConnections(userId), TimeSpan.FromMinutes(PresenceTtlMinutes));
    }

    public async Task<List<string>> GetUserConnectionsAsync(Guid userId)
    {
        var connections = await _cache.SetMembersAsync(CacheKeys.UserConnections(userId));
        return connections.Select(c => c.ToString()).ToList();
    }

    public async Task BroadcastFriendStatusAsync(Guid userId)
    {
        var user = await _db.Users
            .Where(u => u.Id == userId)
            .Select(u => new { u.Username, u.AvatarUrl })
            .FirstOrDefaultAsync();

        if (user == null) return;

        var presenceJson = await _cache.StringGetAsync(CacheKeys.UserPresence(userId));
        if (string.IsNullOrEmpty(presenceJson)) return;

        var presence = JsonSerializer.Deserialize<InternalPresenceData>(presenceJson);
        if (presence == null) return;

        var friendIds = await _db.Friendships
            .Where(f => (f.UserId1 == userId || f.UserId2 == userId) && f.Status == Shared.Enums.FriendshipStatus.Accepted)
            .Select(f => f.UserId1 == userId ? f.UserId2 : f.UserId1)
            .ToListAsync();

        var statusDto = new WsFriendStatusDto(
            userId,
            user.Username,
            user.AvatarUrl ?? "",
            presence.Online,
            presence.LastSeen,
            presence.Activity
        );

        var wsMessage = WsMessage<WsFriendStatusDto>.Create(WsEventType.FriendStatusChanged, statusDto);

        foreach (var friendId in friendIds)
        {
            var friendConnections = await GetUserConnectionsAsync(friendId);
            if (friendConnections.Any())
            {
                await _hubContext.Clients.Clients(friendConnections).SendAsync("ReceiveMessage", wsMessage);
            }
        }
    }

    public async Task UpdateUserActivityAsync(Guid userId)
    {
        var presenceKey = CacheKeys.UserPresence(userId);
        var existingPresence = await _cache.StringGetAsync(presenceKey);
        if (string.IsNullOrEmpty(existingPresence)) return;

        var activity = await BuildActivityAsync(userId);

        var presenceData = new InternalPresenceData
        {
            Online = true,
            Activity = activity,
            UpdatedAt = DateTime.UtcNow
        };

        await _cache.StringSetAsync(
            presenceKey,
            JsonSerializer.Serialize(presenceData),
            TimeSpan.FromMinutes(PresenceTtlMinutes)
        );

        await BroadcastFriendStatusAsync(userId);
    }

    private async Task<WsUserActivityDto> BuildActivityAsync(Guid userId)
    {
        var roomCode = await _cache.StringGetAsync(CacheKeys.UserInRoom(userId));
        if (string.IsNullOrEmpty(roomCode)) return new WsUserActivityDto("idle");

        var roomKey = CacheKeys.RoomInfo(roomCode);
        var roomData = await _cache.HashGetAllAsync(roomKey);
        if (roomData.Length == 0) return new WsUserActivityDto("idle");

        var roomDict = roomData.ToDictionary(x => x.Name.ToString(), x => x.Value.ToString());
        var status = roomDict.GetValueOrDefault("status", "waiting");

        return new WsUserActivityDto(
            Type: status == "playing" ? "in_match" : "in_room",
            Room: new WsRoomBriefDto(
                Code: roomCode,
                Status: status,
                IsPublic: bool.Parse(roomDict.GetValueOrDefault("is_public", "true")),
                CurrentPlayers: (int)await _cache.HashLengthAsync(CacheKeys.RoomParticipants(roomCode)),
                MaxPlayers: int.Parse(roomDict.GetValueOrDefault("max_players", "6"))
            )
        );
    }

    private class InternalPresenceData
    {
        public bool Online { get; set; }
        public DateTime? LastSeen { get; set; }
        public WsUserActivityDto Activity { get; set; } = new("idle");
        public DateTime UpdatedAt { get; set; }
    }
}
