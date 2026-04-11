using Memesploding.Api.Data;
using Memesploding.Api.DTOs;
using Memesploding.Api.Exceptions;
using Memesploding.Shared.Entities;
using Microsoft.EntityFrameworkCore;

namespace Memesploding.Api.Services;

public class NotificationService : INotificationService
{
    private readonly ApplicationDbContext _db;

    public NotificationService(ApplicationDbContext db)
    {
        _db = db;
    }

    public async Task<ListResponseData<NotificationDto>> GetNotificationsAsync(Guid userId, PaginationQueryDto query)
    {
        var notificationsQuery = _db.Notifications
            .Where(n => n.ReceiverId == userId)
            .OrderByDescending(n => n.CreatedAt);

        var totalCount = await notificationsQuery.CountAsync();

        var notifications = await notificationsQuery
            .Skip((query.Page - 1) * query.PageSize)
            .Take(query.PageSize)
            .Include(n => n.Sender)
            .ToListAsync();

        var dtos = notifications.Select(n => n.ToDto()).ToList();

        return new ListResponseData<NotificationDto>(
            Items: dtos,
            Pagination: new PaginationMeta(
                query.Page,
                query.PageSize,
                totalCount,
                totalCount > query.Page * query.PageSize
            )
        );
    }

    public async Task<int> GetUnreadCountAsync(Guid userId)
    {
        return await _db.Notifications
            .Where(n => n.ReceiverId == userId && !n.IsRead)
            .CountAsync();
    }

    public async Task<NotificationDto> MarkAsReadAsync(Guid notificationId, Guid userId)
    {
        var notification = await _db.Notifications
            .Include(n => n.Sender)
            .FirstOrDefaultAsync(n => n.Id == notificationId);

        if (notification == null)
        {
            throw AppException.NotFound("Notification not found");
        }

        if (notification.ReceiverId != userId)
        {
            throw AppException.Forbidden("You cannot access this notification");
        }

        notification.IsRead = true;
        await _db.SaveChangesAsync();

        return notification.ToDto();
    }

    public async Task DeleteNotificationAsync(Guid notificationId, Guid userId)
    {
        var notification = await _db.Notifications
            .FirstOrDefaultAsync(n => n.Id == notificationId);

        if (notification == null)
        {
            throw AppException.NotFound("Notification not found");
        }

        if (notification.ReceiverId != userId)
        {
            throw AppException.Forbidden("You cannot access this notification");
        }

        _db.Notifications.Remove(notification);
        await _db.SaveChangesAsync();
    }

    public async Task MarkAllAsReadAsync(Guid userId)
    {
        await _db.Notifications
            .Where(n => n.ReceiverId == userId && !n.IsRead)
            .ExecuteUpdateAsync(setters => setters.SetProperty(n => n.IsRead, true));
    }

    public async Task ClearAllNotificationsAsync(Guid userId)
    {
        await _db.Notifications
            .Where(n => n.ReceiverId == userId)
            .ExecuteDeleteAsync();
    }
}
