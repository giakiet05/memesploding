using Memesploding.Api.Data;
using Memesploding.Api.DTOs;
using Memesploding.Api.Exceptions;
using Memesploding.Shared.Enums;
using Memesploding.Api.Infrastructure.Cache;
using Microsoft.EntityFrameworkCore;
using Memesploding.Shared.Infrastructure.Cache;

namespace Memesploding.Api.Services;

public class MatchmakingService(ICacheStore cache, ApplicationDbContext db, IRoomService roomService) : IMatchmakingService
{
    public async Task<RoomDetailDto> QuickPlayAsync(Guid userId)
    {
        // 1. Check if user already in a room
        var existingRoomCode = await cache.StringGetAsync(CacheKeys.UserInRoom(userId));
        if (!string.IsNullOrEmpty(existingRoomCode))
        {
            throw AppException.BadRequest(ErrorCode.PlayerAlreadyInRoom, "You are already in a room");
        }

        // 2. Find available public rooms
        var availableRooms = await FindAvailableRoomsAsync();

        // 3. Pick best room (most full)
        var bestRoom = availableRooms.OrderByDescending(r => r.CurrentPlayers).FirstOrDefault();

        if (bestRoom != null)
        {
            try
            {
                return await roomService.JoinRoomAsync(userId, bestRoom.Code);
            }
            catch (AppException ex) when (ex.ErrorCode == ErrorCode.ValidationFailed || ex.ErrorCode == ErrorCode.RoomIsFull)
            {
                // Room might be full now, try next or create
                return await CreateNewRoomAsync(userId);
            }
        }

        return await CreateNewRoomAsync(userId);
    }

    private async Task<List<AvailableRoomInfo>> FindAvailableRoomsAsync()
    {
        var publicRoomCodes = await cache.SetMembersAsync(CacheKeys.PublicRooms());
        var availableRooms = new List<AvailableRoomInfo>();

        foreach (var code in publicRoomCodes)
        {
            var roomInfo = await cache.HashGetAllAsync(CacheKeys.RoomInfo(code));
            if (roomInfo.Length == 0) continue;

            var roomDict = roomInfo.ToDictionary(x => x.Name.ToString(), x => x.Value.ToString());
            if (roomDict.GetValueOrDefault("status") != "waiting") continue;

            var currentPlayers = await cache.HashLengthAsync(CacheKeys.RoomParticipants(code));
            var maxPlayers = int.Parse(roomDict.GetValueOrDefault("max_players", "6"));

            if (currentPlayers < maxPlayers)
            {
                availableRooms.Add(new AvailableRoomInfo { Code = code, CurrentPlayers = (int)currentPlayers, MaxPlayers = maxPlayers });
            }
        }

        return availableRooms;
    }

    private async Task<RoomDetailDto> CreateNewRoomAsync(Guid userId)
    {
        var originalCardSet = await db.CardSets
            .Where(cs => cs.Name == "Original" && cs.IsActive)
            .Select(cs => cs.Id)
            .FirstOrDefaultAsync();

        if (originalCardSet == Guid.Empty)
            throw AppException.BadRequest(ErrorCode.InternalError, "Default card set not found");

        return await roomService.CreateRoomAsync(userId, new CreateRoomDto(6, true, [originalCardSet]));
    }

    private class AvailableRoomInfo
    {
        public string Code { get; set; } = "";
        public int CurrentPlayers { get; set; }
        public int MaxPlayers { get; set; }
    }
}
