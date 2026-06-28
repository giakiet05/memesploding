using Memesploding.Api.DTOs;

namespace Memesploding.Api.Services;

public interface IRoomService
{
    Task<RoomDetailDto> CreateRoomAsync(Guid hostId, CreateRoomDto dto);
    Task<RoomDetailDto> UpdateRoomSettingsAsync(Guid hostId, string roomCode, UpdateRoomDto dto);
    Task<ListResponseData<RoomSummaryDto>> GetPublicRoomsAsync(RoomQueryDto query);
    Task<RoomDetailDto> GetRoomByCodeAsync(string code, Guid? requesterUserId = null);
    Task<RoomDetailDto> JoinRoomAsync(Guid userId, string roomCode);
    Task LeaveRoomAsync(Guid userId, string roomCode);
    Task<RoomDetailDto> UpdateReadyStatusAsync(Guid userId, string roomCode, bool isReady);
    Task KickPlayerAsync(Guid hostId, string roomCode, Guid targetUserId);
    Task<RoomDetailDto> StartMatchAsync(Guid hostId, string roomCode);
}
