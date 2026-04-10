using Memesploding.Api.Data;
using Memesploding.Api.DTOs;
using Memesploding.Api.Exceptions;
using Memesploding.Shared.Enums;
using Memesploding.Shared.Infrastructure.Cache;
using Microsoft.EntityFrameworkCore;
using StackExchange.Redis;
using System.Text.Json;

namespace Memesploding.Api.Services;

public class RoomService(ICacheStore cache, ApplicationDbContext db) : IRoomService
{
    private static readonly Random _random = new();
    private const int RoomCodeLength = 6;
    private const string RoomCodeChars = "ABCDEFGHJKLMNPQRSTUVWXYZ23456789";

    public async Task<RoomDetailDto> CreateRoomAsync(Guid hostId, CreateRoomDto dto)
    {
        var cardSetIds = dto.CardSetIds.Distinct().ToList();
        var cardSets = await db.CardSets
            .Where(cs => cardSetIds.Contains(cs.Id) && cs.IsActive)
            .Select(cs => new { cs.Id, cs.Name })
            .ToListAsync();

        if (cardSets.Count != cardSetIds.Count)
            throw AppException.BadRequest(ErrorCode.ValidationFailed, "One or more card sets are invalid or inactive");

        var host = await db.Users.FindAsync(hostId);
        if (host == null) throw AppException.NotFound("User not found");

        var existingRoomCode = await cache.StringGetAsync(CacheKeys.UserInRoom(hostId));
        if (!string.IsNullOrEmpty(existingRoomCode))
            throw AppException.BadRequest(ErrorCode.ValidationFailed, "You are already in a room");

        var roomCode = await GenerateUniqueRoomCodeAsync();

        var roomInfoKey = CacheKeys.RoomInfo(roomCode);
        var participantsKey = CacheKeys.RoomParticipants(roomCode);
        var cardSetsKey = CacheKeys.RoomCardSets(roomCode);

        var participantData = new
        {
            userId = hostId,
            nickname = host.Username,
            avatarUrl = host.AvatarUrl ?? "",
            role = "host", // Host role
            isReady = true
        };

        var transaction = cache.CreateTransaction();
        
        _ = transaction.HashSetAsync(roomInfoKey, new HashEntry[]
        {
            new("host_id", hostId.ToString()),
            new("status", "waiting"),
            new("is_public", dto.IsPublic.ToString().ToLower()),
            new("max_players", dto.MaxPlayers.ToString()),
            new("turn_timer", "15"),
            new("created_at", DateTime.UtcNow.ToString("O"))
        });

        _ = transaction.HashSetAsync(participantsKey, hostId.ToString(), JsonSerializer.Serialize(participantData));

        foreach (var csId in cardSetIds)
            _ = transaction.SetAddAsync(cardSetsKey, csId.ToString());

        if (dto.IsPublic)
            _ = transaction.SetAddAsync(CacheKeys.PublicRooms(), roomCode);

        _ = transaction.StringSetAsync(CacheKeys.UserInRoom(hostId), roomCode);

        await transaction.ExecuteAsync();

        return await GetRoomByCodeAsync(roomCode);
    }

    public async Task<RoomDetailDto> JoinRoomAsync(Guid userId, string roomCode)
    {
        roomCode = roomCode.ToUpper();
        
        // 1. Check if user already in a room
        var existingRoom = await cache.StringGetAsync(CacheKeys.UserInRoom(userId));
        if (!string.IsNullOrEmpty(existingRoom))
        {
            if (existingRoom == roomCode) return await GetRoomByCodeAsync(roomCode);
            throw AppException.BadRequest(ErrorCode.ValidationFailed, "You are already in another room");
        }

        // 2. Check room existence and capacity
        var roomInfoKey = CacheKeys.RoomInfo(roomCode);
        var roomData = await cache.HashGetAllAsync(roomInfoKey);
        if (roomData.Length == 0) throw AppException.NotFound("Room not found");

        var roomDict = roomData.ToDictionary(x => x.Name.ToString(), x => x.Value.ToString());
        var participantsKey = CacheKeys.RoomParticipants(roomCode);
        var currentCount = await cache.HashLengthAsync(participantsKey);
        var maxPlayers = int.Parse(roomDict["max_players"]);

        if (currentCount >= maxPlayers) throw AppException.BadRequest(ErrorCode.ValidationFailed, "Room is full");

        // 3. Get user info
        var user = await db.Users.FindAsync(userId);
        if (user == null) throw AppException.NotFound("User not found");

        // 4. Add to participants
        var participantData = new
        {
            userId = userId,
            nickname = user.Username,
            avatarUrl = user.AvatarUrl ?? "",
            role = "player",
            isReady = false
        };

        var transaction = cache.CreateTransaction();
        _ = transaction.HashSetAsync(participantsKey, userId.ToString(), JsonSerializer.Serialize(participantData));
        _ = transaction.StringSetAsync(CacheKeys.UserInRoom(userId), roomCode);
        
        await transaction.ExecuteAsync();

        return await GetRoomByCodeAsync(roomCode);
    }

    public async Task<ListResponseData<RoomSummaryDto>> GetPublicRoomsAsync(RoomQueryDto query)
    {
        var publicRoomCodes = await cache.SetMembersAsync(CacheKeys.PublicRooms());
        if (publicRoomCodes.Length == 0)
            return new ListResponseData<RoomSummaryDto>(new List<RoomSummaryDto>(), new PaginationMeta(query.Page, query.PageSize, 0, false));

        var rooms = new List<RoomSummaryDto>();
        foreach (var code in publicRoomCodes)
        {
            var roomInfoKey = CacheKeys.RoomInfo(code);
            var roomData = await cache.HashGetAllAsync(roomInfoKey);
            if (roomData.Length == 0) continue;

            var roomDict = roomData.ToDictionary(x => x.Name.ToString(), x => x.Value.ToString());
            if (roomDict.GetValueOrDefault("status") != "waiting") continue;

            var participantCount = await cache.HashLengthAsync(CacheKeys.RoomParticipants(code));
            var maxPlayers = int.Parse(roomDict.GetValueOrDefault("max_players", "6"));
            if (query.MaxPlayers?.Count > 0 && !query.MaxPlayers.Contains(maxPlayers)) continue;

            var cardSetIds = await cache.SetMembersAsync(CacheKeys.RoomCardSets(code));
            var cardSetGuids = cardSetIds.Select(x => Guid.Parse(x)).ToList();
            if (query.CardSetIds?.Count > 0 && !query.CardSetIds.Any(id => cardSetGuids.Contains(id))) continue;

            var cardSets = await db.CardSets.Where(cs => cardSetGuids.Contains(cs.Id)).Select(cs => new CardSetInfoDto(cs.Id, cs.Name)).ToListAsync();
            var hostId = Guid.Parse(roomDict["host_id"]);
            var hostNickname = await db.Users.Where(u => u.Id == hostId).Select(u => u.Username).FirstOrDefaultAsync() ?? "Unknown";

            rooms.Add(new RoomSummaryDto(code, hostId, hostNickname, maxPlayers, (int)participantCount, roomDict["status"], cardSets, bool.Parse(roomDict.GetValueOrDefault("is_public", "false")), DateTime.Parse(roomDict.GetValueOrDefault("created_at", DateTime.UtcNow.ToString("O")))));
        }

        var totalCount = rooms.Count;
        var paginatedRooms = rooms.OrderByDescending(r => r.CreatedAt).Skip((query.Page - 1) * query.PageSize).Take(query.PageSize).ToList();
        return new ListResponseData<RoomSummaryDto>(paginatedRooms, new PaginationMeta(query.Page, query.PageSize, totalCount, totalCount > query.Page * query.PageSize));
    }

    public async Task<RoomDetailDto> GetRoomByCodeAsync(string code)
    {
        var roomInfoKey = CacheKeys.RoomInfo(code);
        var roomData = await cache.HashGetAllAsync(roomInfoKey);
        if (roomData.Length == 0) throw AppException.NotFound("Room not found");

        var roomDict = roomData.ToDictionary(x => x.Name.ToString(), x => x.Value.ToString());
        var participantsData = await cache.HashGetAllAsync(CacheKeys.RoomParticipants(code));
        
        var participants = participantsData.Select(entry => {
            var p = JsonSerializer.Deserialize<JsonElement>(entry.Value.ToString());
            return new RoomParticipantDto(
                UserId: Guid.Parse(GetJsonProp(p, "userId", "user_id")),
                Nickname: GetJsonProp(p, "nickname"),
                AvatarUrl: GetJsonProp(p, "avatarUrl", "avatar_url"),
                Role: GetJsonProp(p, "role"),
                IsReady: bool.Parse(GetJsonProp(p, "isReady", "is_ready"))
            );
        }).ToList();

        var cardSetIds = await cache.SetMembersAsync(CacheKeys.RoomCardSets(code));
        var cardSetGuids = cardSetIds.Select(x => Guid.Parse(x)).ToList();
        var cardSets = await db.CardSets.Where(cs => cardSetGuids.Contains(cs.Id)).Select(cs => new CardSetInfoDto(cs.Id, cs.Name)).ToListAsync();

        return new RoomDetailDto(code, Guid.Parse(roomDict["host_id"]), roomDict["status"], bool.Parse(roomDict["is_public"]), new RoomSettingsDto(int.Parse(roomDict["max_players"]), int.Parse(roomDict["turn_timer"])), cardSets, participants, new RoomConnectionDto("ws://localhost:5217/ws", "placeholder"));
    }

    private string GetJsonProp(JsonElement el, string camel, string? snake = null)
    {
        if (el.TryGetProperty(camel, out var val)) return val.ToString();
        if (snake != null && el.TryGetProperty(snake, out var val2)) return val2.ToString();
        return "";
    }

    private async Task<string> GenerateUniqueRoomCodeAsync()
    {
        for (int attempt = 0; attempt < 10; attempt++)
        {
            var code = GenerateRoomCode();
            if (!await cache.KeyExistsAsync(CacheKeys.RoomInfo(code))) return code;
        }
        throw new InvalidOperationException("Failed to generate unique room code after 10 attempts");
    }

    private static string GenerateRoomCode()
    {
        var chars = new char[RoomCodeLength];
        for (int i = 0; i < RoomCodeLength; i++) chars[i] = RoomCodeChars[_random.Next(RoomCodeChars.Length)];
        return new string(chars);
    }
}
