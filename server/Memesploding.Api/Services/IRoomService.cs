using Memesploding.Api.DTOs;

namespace Memesploding.Api.Services;

public interface IRoomService
{
    Task<RoomDetailDto> CreateRoomAsync(Guid hostId, CreateRoomDto dto);
    Task<ListResponseData<RoomSummaryDto>> GetPublicRoomsAsync(RoomQueryDto query);
    Task<RoomDetailDto> GetRoomByCodeAsync(string code);
    Task<RoomDetailDto> JoinRoomAsync(Guid userId, string roomCode); // Thêm mới
}
