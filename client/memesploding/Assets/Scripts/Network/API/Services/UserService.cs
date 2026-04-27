using System.Collections.Generic;
using System.Threading.Tasks;
using Network.API.Models;

namespace Network.API.Services
{
    public class UserService
    {
        private static UserService _instance;
        public static UserService Instance => _instance ??= new UserService();

        private readonly string _baseUrl = $"{Config.Api.baseUrl}/api/v1/users";

        private UserService() { }

        public Task<ApiResponse<MeDto>> GetMeProfileAsync(string accessToken)
        {
            return ApiClient.Instance.GetAsync<MeDto>($"{_baseUrl}/me/profile", accessToken);
        }

        public Task<ApiResponse<MeDto>> UpdateMeProfileAsync(UpdateUserRequestDto request, string accessToken)
        {
            return ApiClient.Instance.PatchAsync<MeDto>($"{_baseUrl}/me/profile", request, accessToken);
        }

        public Task<ApiResponse<UserStatsDto>> GetMyStatsAsync(string accessToken)
        {
            return ApiClient.Instance.GetAsync<UserStatsDto>($"{_baseUrl}/me/stats", accessToken);
        }

        public Task<ApiResponse<UserProfileDto>> GetUserByIdAsync(string userId, string accessToken)
        {
            return ApiClient.Instance.GetAsync<UserProfileDto>($"{_baseUrl}/{userId}", accessToken);
        }

        public Task<ApiResponse<UserStatsDto>> GetUserStatsByIdAsync(string userId, string accessToken)
        {
            return ApiClient.Instance.GetAsync<UserStatsDto>($"{_baseUrl}/{userId}/stats", accessToken);
        }

        public Task<ApiResponse<ListResponseData<UserProfileDto>>> GetUsersAsync(UserQueryDto query, string accessToken)
        {
            var url = ApiQueryBuilder.Build(_baseUrl, BuildUserQuery(query));
            return ApiClient.Instance.GetAsync<ListResponseData<UserProfileDto>>(url, accessToken);
        }

        public Task<ApiResponse<ListResponseData<UserProfileDto>>> GetLeaderboardAsync(PaginationQueryDto pagination, string accessToken)
        {
            var url = ApiQueryBuilder.Build($"{_baseUrl}/leaderboard", BuildPaginationQuery(pagination));
            return ApiClient.Instance.GetAsync<ListResponseData<UserProfileDto>>(url, accessToken);
        }

        private static Dictionary<string, string> BuildUserQuery(UserQueryDto query)
        {
            var values = new Dictionary<string, string>();
            if (query == null)
                return values;

            if (!string.IsNullOrWhiteSpace(query.SearchQuery))
                values["SearchQuery"] = query.SearchQuery;

            if (query.Pagination != null)
            {
                values["Pagination.Page"] = query.Pagination.Page.ToString();
                values["Pagination.PageSize"] = query.Pagination.PageSize.ToString();
            }

            return values;
        }

        private static Dictionary<string, string> BuildPaginationQuery(PaginationQueryDto pagination)
        {
            return new Dictionary<string, string>
            {
                ["Page"] = (pagination?.Page ?? 1).ToString(),
                ["PageSize"] = (pagination?.PageSize ?? 20).ToString()
            };
        }
    }
}
