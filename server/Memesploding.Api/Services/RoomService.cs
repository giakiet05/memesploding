using Memesploding.Api.DTOs;
using Memesploding.Api.Exceptions;
using Memesploding.Shared.Enums;
using StackExchange.Redis;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Memesploding.Api.Data;

namespace Memesploding.Api.Services;

public class RoomService : IRoomService
{
    private readonly IDatabase _redis;
    private readonly ApplicationDbContext _db;
    private static readonly Random _random = new();
    private const string RoomKeyPrefix = "room:";
    private const string PublicRoomsSetKey = "public_rooms";
    private const string UserRoomKeyPrefix = "user:";
    private const int RoomCodeLength = 6;
    private const string RoomCodeChars = "ABCDEFGHJKLMNPQRSTUVWXYZ23456789"; // No 0, O, 1, I

    public RoomService(IConnectionMultiplexer redis, ApplicationDbContext db)
    {
        _redis = redis.GetDatabase();
        _db = db;
    }

    public async Task<RoomDetailDto> CreateRoomAsync(Guid hostId, CreateRoomDto dto)
    {
        // Validate card sets exist and are active
        var cardSetIds = dto.CardSetIds.Distinct().ToList();
        var cardSets = await _db.CardSets
            .Where(cs => cardSetIds.Contains(cs.Id) && cs.IsActive)
            .Select(cs => new { cs.Id, cs.Name })
            .ToListAsync();

        if (cardSets.Count != cardSetIds.Count)
        {
            throw AppException.BadRequest(ErrorCode.ValidationFailed, "One or more card sets are invalid or inactive");
        }

        // Get host info
        var host = await _db.Users
            .Where(u => u.Id == hostId)
            .Select(u => new { u.Id, u.Username, u.AvatarUrl })
            .FirstOrDefaultAsync();

        if (host == null)
        {
            throw AppException.NotFound("User not found");
        }

        // Check if user already in a room
        var existingRoomCode = await _redis.StringGetAsync($"{UserRoomKeyPrefix}{hostId}:room");
        if (!existingRoomCode.IsNullOrEmpty)
        {
            throw AppException.BadRequest(ErrorCode.ValidationFailed, "You are already in a room");
        }

        // Generate unique room code
        var roomCode = await GenerateUniqueRoomCodeAsync();

        // Create room data
        var roomKey = $"{RoomKeyPrefix}{roomCode}";
        var participantsKey = $"{roomKey}:participants";
        var cardSetsKey = $"{roomKey}:card_sets";

        var roomData = new
        {
            host_id = hostId.ToString(),
            status = "waiting",
            is_public = dto.IsPublic,
            max_players = dto.MaxPlayers,
            turn_timer = 15, // Default
            created_at = DateTime.UtcNow
        };

        var participantData = new
        {
            user_id = hostId.ToString(),
            nickname = host.Username,
            avatar_url = host.AvatarUrl ?? "",
            role = "player",
            is_ready = true // Host auto-ready
        };

        // Transaction: Create room
        var transaction = _redis.CreateTransaction();
        
        // Set room metadata
        _ = transaction.HashSetAsync(roomKey, new HashEntry[]
        {
            new("host_id", roomData.host_id),
            new("status", roomData.status),
            new("is_public", roomData.is_public.ToString()),
            new("max_players", roomData.max_players.ToString()),
            new("turn_timer", roomData.turn_timer.ToString()),
            new("created_at", roomData.created_at.ToString("O"))
        });

        // Add host as participant
        _ = transaction.HashSetAsync(participantsKey, hostId.ToString(), JsonSerializer.Serialize(participantData));

        // Add card sets
        foreach (var csId in cardSetIds)
        {
            _ = transaction.SetAddAsync(cardSetsKey, csId.ToString());
        }

        // Add to public rooms set if public
        if (dto.IsPublic)
        {
            _ = transaction.SetAddAsync(PublicRoomsSetKey, roomCode);
        }

        // Mark user as in room
        _ = transaction.StringSetAsync($"{UserRoomKeyPrefix}{hostId}:room", roomCode);

        await transaction.ExecuteAsync();

        // Build response
        return new RoomDetailDto(
            Code: roomCode,
            HostId: hostId,
            Status: "waiting",
            IsPublic: dto.IsPublic,
            Settings: new RoomSettingsDto(dto.MaxPlayers, 15),
            CardSets: cardSets.Select(cs => new CardSetInfoDto(cs.Id, cs.Name)).ToList(),
            CurrentParticipants: new List<RoomParticipantDto>
            {
                new(hostId, host.Username, host.AvatarUrl ?? "", "player", true)
            },
            Connection: new RoomConnectionDto(
                WsUrl: "wss://game.memesploding.com/ws", // TODO: Get from config
                WsAccessToken: "placeholder" // TODO: Generate JWT for WS
            )
        );
    }

    public async Task<ListResponseData<RoomSummaryDto>> GetPublicRoomsAsync(RoomQueryDto query)
    {
        // Get all public room codes
        var publicRoomCodes = await _redis.SetMembersAsync(PublicRoomsSetKey);
        
        if (publicRoomCodes.Length == 0)
        {
            return new ListResponseData<RoomSummaryDto>(
                Items: new List<RoomSummaryDto>(),
                Pagination: new PaginationMeta(query.Page, query.PageSize, 0, false)
            );
        }

        var rooms = new List<RoomSummaryDto>();

        foreach (var code in publicRoomCodes)
        {
            var roomCode = code.ToString();
            var roomKey = $"{RoomKeyPrefix}{roomCode}";
            
            // Get room metadata
            var roomData = await _redis.HashGetAllAsync(roomKey);
            if (roomData.Length == 0) continue; // Room was deleted

            var roomDict = roomData.ToDictionary(
                x => x.Name.ToString(),
                x => x.Value.ToString()
            );

            // Only include waiting rooms
            if (roomDict.GetValueOrDefault("status") != "waiting") continue;

            // Get participant count
            var participantsKey = $"{roomKey}:participants";
            var participantCount = await _redis.HashLengthAsync(participantsKey);

            // Apply filters
            var maxPlayers = int.Parse(roomDict.GetValueOrDefault("max_players", "6"));
            if (query.MaxPlayers?.Count > 0 && !query.MaxPlayers.Contains(maxPlayers))
            {
                continue;
            }

            // Get card sets
            var cardSetsKey = $"{roomKey}:card_sets";
            var cardSetIds = await _redis.SetMembersAsync(cardSetsKey);
            var cardSetGuids = cardSetIds.Select(x => Guid.Parse(x.ToString())).ToList();

            // Filter by card sets
            if (query.CardSetIds?.Count > 0)
            {
                var hasMatchingCardSet = query.CardSetIds.Any(id => cardSetGuids.Contains(id));
                if (!hasMatchingCardSet) continue;
            }

            // Load card set names from DB
            var cardSets = await _db.CardSets
                .Where(cs => cardSetGuids.Contains(cs.Id))
                .Select(cs => new CardSetInfoDto(cs.Id, cs.Name))
                .ToListAsync();

            // Get host info
            var hostId = Guid.Parse(roomDict["host_id"]);
            var hostNickname = await _db.Users
                .Where(u => u.Id == hostId)
                .Select(u => u.Username)
                .FirstOrDefaultAsync() ?? "Unknown";

            rooms.Add(new RoomSummaryDto(
                Code: roomCode,
                HostId: hostId,
                HostNickname: hostNickname,
                MaxPlayers: maxPlayers,
                CurrentPlayers: (int)participantCount,
                Status: roomDict["status"],
                CardSets: cardSets,
                IsPublic: bool.Parse(roomDict.GetValueOrDefault("is_public", "false")),
                CreatedAt: DateTime.Parse(roomDict.GetValueOrDefault("created_at", DateTime.UtcNow.ToString("O")))
            ));
        }

        // Apply pagination
        var totalCount = rooms.Count;
        var paginatedRooms = rooms
            .OrderByDescending(r => r.CreatedAt)
            .Skip((query.Page - 1) * query.PageSize)
            .Take(query.PageSize)
            .ToList();

        return new ListResponseData<RoomSummaryDto>(
            Items: paginatedRooms,
            Pagination: new PaginationMeta(
                query.Page,
                query.PageSize,
                totalCount,
                totalCount > query.Page * query.PageSize
            )
        );
    }

    public async Task<RoomDetailDto> GetRoomByCodeAsync(string code)
    {
        var roomKey = $"{RoomKeyPrefix}{code}";
        
        // Get room metadata
        var roomData = await _redis.HashGetAllAsync(roomKey);
        if (roomData.Length == 0)
        {
            throw AppException.NotFound("Room not found");
        }

        var roomDict = roomData.ToDictionary(
            x => x.Name.ToString(),
            x => x.Value.ToString()
        );

        var hostId = Guid.Parse(roomDict["host_id"]);
        var maxPlayers = int.Parse(roomDict["max_players"]);
        var turnTimer = int.Parse(roomDict["turn_timer"]);

        // Get participants
        var participantsKey = $"{roomKey}:participants";
        var participantsData = await _redis.HashGetAllAsync(participantsKey);
        
        var participants = participantsData.Select(entry =>
        {
            var p = JsonSerializer.Deserialize<Dictionary<string, object>>(entry.Value.ToString());
            return new RoomParticipantDto(
                UserId: Guid.Parse(p!["user_id"].ToString()!),
                Nickname: p["nickname"].ToString()!,
                AvatarUrl: p["avatar_url"].ToString()!,
                Role: p["role"].ToString()!,
                IsReady: bool.Parse(p["is_ready"].ToString()!)
            );
        }).ToList();

        // Get card sets
        var cardSetsKey = $"{roomKey}:card_sets";
        var cardSetIds = await _redis.SetMembersAsync(cardSetsKey);
        var cardSetGuids = cardSetIds.Select(x => Guid.Parse(x.ToString())).ToList();

        var cardSets = await _db.CardSets
            .Where(cs => cardSetGuids.Contains(cs.Id))
            .Select(cs => new CardSetInfoDto(cs.Id, cs.Name))
            .ToListAsync();

        return new RoomDetailDto(
            Code: code,
            HostId: hostId,
            Status: roomDict["status"],
            IsPublic: bool.Parse(roomDict["is_public"]),
            Settings: new RoomSettingsDto(maxPlayers, turnTimer),
            CardSets: cardSets,
            CurrentParticipants: participants,
            Connection: new RoomConnectionDto(
                WsUrl: "wss://game.memesploding.com/ws",
                WsAccessToken: "placeholder"
            )
        );
    }

    private async Task<string> GenerateUniqueRoomCodeAsync()
    {
        for (int attempt = 0; attempt < 10; attempt++)
        {
            var code = GenerateRoomCode();
            var exists = await _redis.KeyExistsAsync($"{RoomKeyPrefix}{code}");
            if (!exists)
            {
                return code;
            }
        }

        throw new InvalidOperationException("Failed to generate unique room code after 10 attempts");
    }

    private static string GenerateRoomCode()
    {
        var chars = new char[RoomCodeLength];
        for (int i = 0; i < RoomCodeLength; i++)
        {
            chars[i] = RoomCodeChars[_random.Next(RoomCodeChars.Length)];
        }
        return new string(chars);
    }
}
