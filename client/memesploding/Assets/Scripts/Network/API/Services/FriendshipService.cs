using System.Collections.Generic;
using System.Threading.Tasks;
using Network.API.Models;

namespace Network.API.Services
{
    public class FriendshipService
    {
        private static FriendshipService _instance;
        public static FriendshipService Instance => _instance ??= new FriendshipService();

        private readonly string _baseUrl = $"{Config.Api.baseUrl}/api/v1/me/friends";

        private FriendshipService() { }

        public Task<ApiResponse<ListResponseData<UserProfileDto>>> GetFriendsAsync(FriendshipQueryDto query, string accessToken)
        {
            var url = ApiQueryBuilder.Build(_baseUrl, BuildFriendshipQuery(query));
            return ApiClient.Instance.GetAsync<ListResponseData<UserProfileDto>>(url, accessToken);
        }

        public Task<ApiResponse<UserProfileDto>> SendInvitationAsync(FriendshipRequestDto request, string accessToken)
        {
            return ApiClient.Instance.PostAsync<UserProfileDto>($"{_baseUrl}/invitations", request, accessToken);
        }

        public Task<ApiResponse<UserProfileDto>> RespondInvitationAsync(string requesterId, ProcessFriendRequestDto request, string accessToken)
        {
            return ApiClient.Instance.PatchAsync<UserProfileDto>($"{_baseUrl}/invitations/{requesterId}", request, accessToken);
        }

        public Task<ApiResponse<object>> RemoveFriendAsync(string friendId, string accessToken)
        {
            return ApiClient.Instance.DeleteAsync<object>($"{_baseUrl}/{friendId}", accessToken);
        }

        private static Dictionary<string, string> BuildFriendshipQuery(FriendshipQueryDto query)
        {
            var values = new Dictionary<string, string>();
            if (query == null)
                return values;

            if (!string.IsNullOrWhiteSpace(query.Status))
                values["Status"] = query.Status;

            if (query.Pagination != null)
            {
                values["Pagination.Page"] = query.Pagination.Page.ToString();
                values["Pagination.PageSize"] = query.Pagination.PageSize.ToString();
            }

            return values;
        }
    }
}
