using System.Threading.Tasks;
using Network.API.Models;

namespace Network.API.Services
{
    public class MatchmakingService
    {
        private static MatchmakingService _instance;
        public static MatchmakingService Instance => _instance ??= new MatchmakingService();

        private readonly string _baseUrl = $"{Config.Api.baseUrl}/api/v1/matchmaking";

        private MatchmakingService() { }

        public Task<ApiResponse<RoomDetailDto>> QuickPlayAsync(string accessToken)
        {
            return ApiClient.Instance.PostAsync<RoomDetailDto>($"{_baseUrl}/quick-play", new object(), accessToken);
        }
    }
}
