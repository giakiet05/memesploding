using System.Collections.Generic;
using System.Threading.Tasks;
using Network.API.Models;

namespace Network.API.Services
{
    public class NotificationService
    {
        private static NotificationService _instance;
        public static NotificationService Instance => _instance ??= new NotificationService();

        private readonly string _baseUrl = $"{Config.Api.baseUrl}/api/v1";

        private NotificationService() { }

        public Task<ApiResponse<ListResponseData<NotificationDto>>> GetNotificationsAsync(PaginationQueryDto pagination, string accessToken)
        {
            var url = ApiQueryBuilder.Build($"{_baseUrl}/me/notifications", BuildPagination(pagination));
            return ApiClient.Instance.GetAsync<ListResponseData<NotificationDto>>(url, accessToken);
        }

        public Task<ApiResponse<UnreadCountDto>> GetUnreadCountAsync(string accessToken)
        {
            return ApiClient.Instance.GetAsync<UnreadCountDto>($"{_baseUrl}/me/notifications/unread-count", accessToken);
        }

        public Task<ApiResponse<NotificationDto>> MarkAsReadAsync(string notificationId, string accessToken)
        {
            return ApiClient.Instance.PatchAsync<NotificationDto>($"{_baseUrl}/notifications/{notificationId}", new object(), accessToken);
        }

        public Task<ApiResponse<object>> DeleteNotificationAsync(string notificationId, string accessToken)
        {
            return ApiClient.Instance.DeleteAsync<object>($"{_baseUrl}/notifications/{notificationId}", accessToken);
        }

        public Task<ApiResponse<object>> MarkAllAsReadAsync(string accessToken)
        {
            return ApiClient.Instance.PatchAsync<object>($"{_baseUrl}/notifications/mark-all-read", new object(), accessToken);
        }

        public Task<ApiResponse<object>> ClearAllAsync(string accessToken)
        {
            return ApiClient.Instance.DeleteAsync<object>($"{_baseUrl}/notifications/clear-all", accessToken);
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
