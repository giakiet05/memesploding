using System.Collections.Generic;
using System.Threading.Tasks;
using Network.API.Models;

namespace Network.API.Services
{
    public class MatchService
    {
        private static MatchService _instance;
        public static MatchService Instance => _instance ??= new MatchService();

        private readonly string _baseUrl = $"{Config.Api.baseUrl}/api/v1";

        private MatchService() { }

        public Task<ApiResponse<ListResponseData<MatchSummaryDto>>> GetMyMatchHistoryAsync(PaginationQueryDto pagination, string accessToken)
        {
            var url = ApiQueryBuilder.Build($"{_baseUrl}/me/match-history", BuildPagination(pagination));
            return ApiClient.Instance.GetAsync<ListResponseData<MatchSummaryDto>>(url, accessToken);
        }

        public Task<ApiResponse<ListResponseData<MatchSummaryDto>>> GetUserMatchHistoryAsync(string userId, PaginationQueryDto pagination, string accessToken)
        {
            var url = ApiQueryBuilder.Build($"{_baseUrl}/users/{userId}/match-history", BuildPagination(pagination));
            return ApiClient.Instance.GetAsync<ListResponseData<MatchSummaryDto>>(url, accessToken);
        }

        public Task<ApiResponse<MatchDetailDto>> GetMatchDetailAsync(string matchId, string accessToken)
        {
            return ApiClient.Instance.GetAsync<MatchDetailDto>($"{_baseUrl}/matches/{matchId}", accessToken);
        }

        private static Dictionary<string, string> BuildPagination(PaginationQueryDto pagination)
        {
            return new Dictionary<string, string>
            {
                ["Page"] = (pagination?.Page ?? 1).ToString(),
                ["PageSize"] = (pagination?.PageSize ?? 20).ToString()
            };
        }
    }
}
