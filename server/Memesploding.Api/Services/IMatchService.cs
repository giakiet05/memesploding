using Memesploding.Api.DTOs;

namespace Memesploding.Api.Services;

public interface IMatchService
{
    Task<ListResponseData<MatchSummaryDto>> GetUserMatchHistoryAsync(Guid userId, PaginationQueryDto query);
    Task<MatchDetailDto> GetMatchDetailAsync(Guid matchId);
}
