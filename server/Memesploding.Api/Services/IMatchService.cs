using Memesploding.Api.DTOs;

namespace Memesploding.Api.Services;

public interface IMatchService
{
    Task<ListResponseData<MatchSummaryDto>> GetMyMatchHistoryAsync(Guid userId, PaginationQueryDto query);
    Task<ListResponseData<MatchSummaryDto>> GetUserMatchHistoryAsync(Guid targetUserId, PaginationQueryDto query);
    Task<MatchDetailDto> GetMatchDetailAsync(Guid matchId);
}
