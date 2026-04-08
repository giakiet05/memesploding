using Memesploding.Api.Data;
using Memesploding.Api.DTOs;
using Memesploding.Api.Exceptions;
using Memesploding.Shared.Enums;
using Memesploding.Shared.Infrastructure.Cache;
using Microsoft.EntityFrameworkCore;

namespace Memesploding.Api.Services;

public class MatchmakingService : IMatchmakingService
{
    private readonly ICacheStore _cache;
    private readonly ApplicationDbContext _db;
    private readonly IRoomService _roomService;
    private const string PublicRoomsSetKey = "public_rooms";
    private const string RoomKeyPrefix = "room:";

    public MatchmakingService(ICacheStore cache, ApplicationDbContext db, IRoomService roomService)
    {
        _cache = cache;
        _db = db;
        _roomService = roomService;
    }

    public async Task<RoomDetailDto> QuickPlayAsync(Guid userId)
    {
        // 1. Check if user already in a room
        var userRoomKey = $"user:{userId}:room";
        var existingRoomCode = await _cache.StringGetAsync(userRoomKey);
        if (!string.IsNullOrEmpty(existingRoomCode))
        {
            throw AppException.BadRequest(ErrorCode.ValidationFailed, "You are already in a room");
        }

        // 2. Find any available public room (no filter - truly quick!)
        var availableRooms = await FindAvailableRoomsAsync();

        // 3. Pick best room (closest to full for faster start)
        var bestRoom = availableRooms
            .OrderByDescending(r => r.CurrentPlayers)
            .FirstOrDefault();

        // 4. Join existing room or create new
        if (bestRoom != null)
        {
            // Try to join - might fail if room just filled up (race condition)
            try
            {
                return await JoinExistingRoomAsync(userId, bestRoom.Code);
            }
            catch (AppException ex) when (ex.Message.Contains("full"))
            {
                // Room filled up, try next best or create new
                var nextBest = availableRooms
                    .Where(r => r.Code != bestRoom.Code)
                    .OrderByDescending(r => r.CurrentPlayers)
                    .FirstOrDefault();

                if (nextBest != null)
                {
                    return await JoinExistingRoomAsync(userId, nextBest.Code);
                }
                
                // No other rooms, create new with default settings
                return await CreateNewRoomAsync(userId);
            }
        }
        else
        {
            // No available rooms, create new
            return await CreateNewRoomAsync(userId);
        }
    }

    private async Task<List<AvailableRoomInfo>> FindAvailableRoomsAsync()
    {
        var publicRoomCodes = await _cache.SetMembersAsync(PublicRoomsSetKey);
        var availableRooms = new List<AvailableRoomInfo>();

        foreach (var code in publicRoomCodes)
        {
            var roomCode = code.ToString();
            var roomKey = $"{RoomKeyPrefix}{roomCode}";

            // Get room metadata
            var roomData = await _cache.HashGetAllAsync(roomKey);
            if (roomData.Length == 0) continue;

            var roomDict = roomData.ToDictionary(
                x => x.Name.ToString(),
                x => x.Value.ToString()
            );

            // Only include waiting rooms
            if (roomDict.GetValueOrDefault("status") != "waiting") continue;

            // Check if room is full
            var maxPlayers = int.Parse(roomDict.GetValueOrDefault("max_players", "6"));
            var participantsKey = $"{roomKey}:participants";
            var currentPlayers = await _cache.HashLengthAsync(participantsKey);

            if (currentPlayers >= maxPlayers) continue;

            availableRooms.Add(new AvailableRoomInfo
            {
                Code = roomCode,
                CurrentPlayers = (int)currentPlayers,
                MaxPlayers = maxPlayers
            });
        }

        return availableRooms;
    }

    private async Task<RoomDetailDto> JoinExistingRoomAsync(Guid userId, string roomCode)
    {
        var room = await _roomService.GetRoomByCodeAsync(roomCode);
        
        // Check room not full
        if (room.CurrentParticipants.Count >= room.Settings.MaxPlayers)
        {
            throw AppException.BadRequest(ErrorCode.ValidationFailed, "Room is full");
        }

        // Get user info
        var user = await _db.Users
            .Where(u => u.Id == userId)
            .Select(u => new { u.Id, u.Username, u.AvatarUrl })
            .FirstOrDefaultAsync();

        if (user == null)
        {
            throw AppException.NotFound("User not found");
        }

        // Add participant to Redis
        var roomKey = $"{RoomKeyPrefix}{roomCode}";
        var participantsKey = $"{roomKey}:participants";
        
        var participantData = new
        {
            user_id = userId.ToString(),
            nickname = user.Username,
            avatar_url = user.AvatarUrl ?? "",
            role = "player",
            is_ready = true // Auto-ready for matchmaking
        };

        await _cache.HashSetAsync(participantsKey, userId.ToString(), System.Text.Json.JsonSerializer.Serialize(participantData));
        await _cache.StringSetAsync($"user:{userId}:room", roomCode);

        // Return updated room details
        return await _roomService.GetRoomByCodeAsync(roomCode);
    }

    private async Task<RoomDetailDto> CreateNewRoomAsync(Guid userId)
    {
        // Create with default settings - Original card set only
        var originalCardSet = await _db.CardSets
            .Where(cs => cs.Name == "Original" && cs.IsActive)
            .Select(cs => cs.Id)
            .FirstOrDefaultAsync();

        if (originalCardSet == Guid.Empty)
        {
            throw AppException.BadRequest(ErrorCode.InternalError, "Default card set not found");
        }

        return await _roomService.CreateRoomAsync(userId, new CreateRoomDto(
            MaxPlayers: 6,
            IsPublic: true,
            CardSetIds: [originalCardSet]
        ));
    }

    private class AvailableRoomInfo
    {
        public string Code { get; set; } = "";
        public int CurrentPlayers { get; set; }
        public int MaxPlayers { get; set; }
    }
}
