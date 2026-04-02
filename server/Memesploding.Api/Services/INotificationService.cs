using Memesploding.Api.DTOs;

namespace Memesploding.Api.Services;

public interface INotificationService
{
    Task<ListResponseData<NotificationDto>> GetNotificationsAsync(Guid userId, PaginationQueryDto query);
    Task<int> GetUnreadCountAsync(Guid userId);
    Task<NotificationDto> MarkAsReadAsync(Guid notificationId, Guid userId);
    Task DeleteNotificationAsync(Guid notificationId, Guid userId);
    Task MarkAllAsReadAsync(Guid userId);
    Task ClearAllNotificationsAsync(Guid userId);
}
