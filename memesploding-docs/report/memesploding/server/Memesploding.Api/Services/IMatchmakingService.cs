using Memesploding.Api.DTOs;

namespace Memesploding.Api.Services;

public interface IMatchmakingService
{
    Task<RoomDetailDto> QuickPlayAsync(Guid userId);
}
