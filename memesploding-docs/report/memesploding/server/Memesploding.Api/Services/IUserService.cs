using Memesploding.Api.DTOs;

namespace Memesploding.Api.Services;

public interface IUserService
{
    Task<MeDto> GetMeAsync(Guid userId);
    Task<MeDto> UpdateUserAsync(Guid userId, UpdateUserRequestDto request);
    Task<ListResponseData<UserProfileDto>> GetUsersAsync(Guid currentUserId, UserQueryDto query, Guid? excludeUserId = null);
    Task<UserProfileDto> GetUserProfileAsync(Guid currentUserId, Guid targetUserId);
    Task<UserStatsDto> GetUserStatsAsync(Guid userId);
}