using Memesploding.Api.Data;
using Memesploding.Shared.Events;
using Memesploding.Shared.Infrastructure.EventBus;
using Memesploding.Shared.Infrastructure.Cache;
using Microsoft.EntityFrameworkCore;
using System.Text.Json;
using Microsoft.Extensions.Configuration;

namespace Memesploding.Api.Services;

public class PresenceService : IPresenceService
{
    private readonly ICacheStore _cache;
    private readonly ApplicationDbContext _db;
    private readonly IEventBus _eventBus;
    private readonly ILogger<PresenceService> _logger;
    private readonly int _reconnectGraceSeconds;
    private readonly int _reconnectGraceKeyTtlSeconds;
    
    private const int PresenceTtlMinutes = 10;

    public PresenceService(
        ICacheStore cache, 
        ApplicationDbContext db,
        IEventBus eventBus,
        IConfiguration config,
        ILogger<PresenceService> logger)
    {
        _cache = cache;
        _db = db;
        _eventBus = eventBus;
        _logger = logger;
        _reconnectGraceSeconds = Math.Max(10, config.GetValue<int?>("Realtime:ReconnectGraceSeconds") ?? 120);
        _reconnectGraceKeyTtlSeconds = _reconnectGraceSeconds + 300;
    }

    public async Task UserConnectedAsync(Guid userId, string connectionId)
    {
        var connectionsKey = CacheKeys.UserConnections(userId);
        await _cache.SetAddAsync(connectionsKey, connectionId);
        await _cache.KeyExpireAsync(connectionsKey, TimeSpan.FromMinutes(PresenceTtlMinutes));

        var presenceKey = CacheKeys.UserPresence(userId);
        var existingPresence = await _cache.StringGetAsync(presenceKey);
        bool wasOffline = string.IsNullOrEmpty(existingPresence);

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

        await ClearRoomReconnectGraceAsync(userId);

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
                Activity = new PresenceUserActivity("idle"),
                UpdatedAt = DateTime.UtcNow
            };

            await _cache.StringSetAsync(
                presenceKey,
                JsonSerializer.Serialize(presenceData),
                TimeSpan.FromMinutes(PresenceTtlMinutes)
            );

            await MarkRoomReconnectGraceAsync(userId);

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

        if (!friendIds.Any()) return;

        // Bắn event ra Redis thay vì gọi SignalR trực tiếp
        var @event = new FriendStatusChangedEvent(
            userId,
            user.Username,
            user.AvatarUrl ?? "",
            presence.Online,
            presence.LastSeen,
            presence.Activity,
            friendIds
        );

        await _eventBus.PublishAsync(EventChannels.FriendStatusChanged, @event);
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

    private async Task<PresenceUserActivity> BuildActivityAsync(Guid userId)
    {
        var roomCode = await _cache.StringGetAsync(CacheKeys.UserInRoom(userId));
        if (string.IsNullOrEmpty(roomCode)) return new PresenceUserActivity("idle");

        var roomKey = CacheKeys.RoomInfo(roomCode);
        var roomData = await _cache.HashGetAllAsync(roomKey);
        if (roomData.Length == 0) return new PresenceUserActivity("idle");

        var roomDict = roomData.ToDictionary(x => x.Name.ToString(), x => x.Value.ToString());
        var status = roomDict.GetValueOrDefault("status", "waiting");

        return new PresenceUserActivity(
            Type: status == "playing" ? "in_match" : "in_room",
            Room: new PresenceRoomBrief(
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
        public PresenceUserActivity Activity { get; set; } = new("idle");
        public DateTime UpdatedAt { get; set; }
    }

    private async Task MarkRoomReconnectGraceAsync(Guid userId)
    {
        var roomCode = await _cache.StringGetAsync(CacheKeys.UserInRoom(userId));
        if (string.IsNullOrEmpty(roomCode))
        {
            await ClearRoomReconnectGraceAsync(userId);
            return;
        }

        var data = new RoomReconnectGraceData
        {
            RoomCode = roomCode.ToUpper(),
            DisconnectedAt = DateTime.UtcNow
        };

        await _cache.SetAsync(CacheKeys.RoomReconnectGrace(userId), data, TimeSpan.FromSeconds(_reconnectGraceKeyTtlSeconds));
        await _cache.SetAddAsync(CacheKeys.RoomReconnectGraceUsers(), userId.ToString());
    }

    private async Task ClearRoomReconnectGraceAsync(Guid userId)
    {
        await _cache.KeyDeleteAsync(CacheKeys.RoomReconnectGrace(userId));
        await _cache.SetRemoveAsync(CacheKeys.RoomReconnectGraceUsers(), userId.ToString());
    }

    private class RoomReconnectGraceData
    {
        public string RoomCode { get; set; } = "";
        public DateTime DisconnectedAt { get; set; }
    }
}
