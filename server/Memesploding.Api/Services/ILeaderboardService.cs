using Memesploding.Api.DTOs;

namespace Memesploding.Api.Services;

public interface ILeaderboardService
{
    Task<ListResponseData<LeaderboardEntryDto>> GetGlobalLeaderboardAsync(PaginationQueryDto query);
}
