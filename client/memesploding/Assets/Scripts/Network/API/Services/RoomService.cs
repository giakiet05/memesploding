using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Network.API.Models;

namespace Network.API.Services
{
    public class RoomService
    {
        private static RoomService _instance;
        public static RoomService Instance => _instance ??= new RoomService();

        private readonly string _baseUrl = $"{Config.Api.baseUrl}/api/v1/rooms";

        private RoomService() { }

        public Task<ApiResponse<RoomDetailDto>> CreateRoomAsync(CreateRoomDto request, string accessToken)
        {
            return ApiClient.Instance.PostAsync<RoomDetailDto>(_baseUrl, request, accessToken);
        }

        public Task<ApiResponse<RoomDetailDto>> JoinRoomAsync(string roomCode, string accessToken)
        {
            return ApiClient.Instance.PostAsync<RoomDetailDto>($"{_baseUrl}/{roomCode}/join", new object(), accessToken);
        }

        public Task<ApiResponse<object>> LeaveRoomAsync(string roomCode, string accessToken)
        {
            return ApiClient.Instance.PostAsync<object>($"{_baseUrl}/{roomCode}/leave", new object(), accessToken);
        }

        public Task<ApiResponse<RoomDetailDto>> UpdateRoomSettingsAsync(string roomCode, UpdateRoomDto request, string accessToken)
        {
            return ApiClient.Instance.PatchAsync<RoomDetailDto>($"{_baseUrl}/{roomCode}", request, accessToken);
        }

        public Task<ApiResponse<ListResponseData<RoomSummaryDto>>> GetPublicRoomsAsync(RoomQueryDto query, string accessToken)
        {
            var url = ApiQueryBuilder.Build(_baseUrl, BuildRoomQuery(query));
            return ApiClient.Instance.GetAsync<ListResponseData<RoomSummaryDto>>(url, accessToken);
        }

        public Task<ApiResponse<RoomDetailDto>> GetRoomByCodeAsync(string roomCode, string accessToken)
        {
            return ApiClient.Instance.GetAsync<RoomDetailDto>($"{_baseUrl}/{roomCode}", accessToken);
        }

        private static Dictionary<string, string> BuildRoomQuery(RoomQueryDto query)
        {
            var values = new Dictionary<string, string>
            {
                ["Page"] = (query?.Page ?? 1).ToString(),
                ["PageSize"] = (query?.PageSize ?? 20).ToString()
            };

            if (query?.CardSetIds != null && query.CardSetIds.Count > 0)
                values["CardSetIds"] = string.Join(",", query.CardSetIds.Where(id => !string.IsNullOrWhiteSpace(id)));

            if (query?.MaxPlayers != null && query.MaxPlayers.Count > 0)
                values["MaxPlayers"] = string.Join(",", query.MaxPlayers);

            return values;
        }
    }
}
