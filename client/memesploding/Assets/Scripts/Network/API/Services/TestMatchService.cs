using System.Threading.Tasks;
using Network.API.Models;

namespace Network.API.Services
{
    public class TestMatchService
    {
        private static TestMatchService _instance;
        public static TestMatchService Instance => _instance ??= new TestMatchService();

        private readonly string _baseUrl = $"{Config.Api.baseUrl}/api/v1";

        private TestMatchService() { }

        public Task<ApiResponse<BotTestMatchDto>> StartBotMatchAsync(string accessToken)
        {
            return ApiClient.Instance.PostAsync<BotTestMatchDto>($"{_baseUrl}/test-matches/bot", new { }, accessToken);
        }
    }
}
