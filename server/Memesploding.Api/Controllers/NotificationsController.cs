using Memesploding.Api.DTOs;
using Memesploding.Api.Extensions;
using Memesploding.Api.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Memesploding.Api.Controllers;

[ApiController]
[Route("api/v1")]
[Authorize]
public class NotificationsController : ControllerBase
{
    private readonly INotificationService _notificationService;

    public NotificationsController(INotificationService notificationService)
    {
        _notificationService = notificationService;
    }

    [HttpGet("me/notifications")]
    public async Task<IActionResult> GetNotifications([FromQuery] PaginationQueryDto query)
    {
        var userId = User.GetUserId();
        var notifications = await _notificationService.GetNotificationsAsync(userId, query);
        return Ok(new ApiResponse<ListResponseData<NotificationDto>>("Successfully!", notifications));
    }

    [HttpGet("me/notifications/unread-count")]
    public async Task<IActionResult> GetUnreadCount()
    {
        var userId = User.GetUserId();
        var count = await _notificationService.GetUnreadCountAsync(userId);
        return Ok(new ApiResponse<UnreadCountDto>("Successfully!", new UnreadCountDto(count)));
    }

    [HttpPatch("notifications/{id}")]
    public async Task<IActionResult> MarkAsRead(Guid id)
    {
        var userId = User.GetUserId();
        var notification = await _notificationService.MarkAsReadAsync(id, userId);
        return Ok(new ApiResponse<NotificationDto>("Notification marked as read", notification));
    }

    [HttpDelete("notifications/{id}")]
    public async Task<IActionResult> DeleteNotification(Guid id)
    {
        var userId = User.GetUserId();
        await _notificationService.DeleteNotificationAsync(id, userId);
        return Ok(new ApiResponse<object>("Notification deleted successfully", new { }));
    }

    [HttpPatch("notifications/mark-all-read")]
    public async Task<IActionResult> MarkAllAsRead()
    {
        var userId = User.GetUserId();
        await _notificationService.MarkAllAsReadAsync(userId);
        return Ok(new ApiResponse<object>("All notifications marked as read", new { }));
    }

    [HttpDelete("notifications/clear-all")]
    public async Task<IActionResult> ClearAllNotifications()
    {
        var userId = User.GetUserId();
        await _notificationService.ClearAllNotificationsAsync(userId);
        return Ok(new ApiResponse<object>("All notifications cleared successfully", new { }));
    }
}
