using Memesploding.Shared.Enums;
using Memesploding.Shared.Entities;

namespace Memesploding.Api.DTOs;

public record NotificationDto(
    Guid Id,
    NotificationType Type,
    string? Payload,
    bool IsRead,
    DateTime CreatedAt,
    NotificationSenderDto? Sender
);

public record NotificationSenderDto(
    Guid Id,
    string Username,
    string? AvatarUrl
);

public record UnreadCountDto(
    int Count
);

public static class NotificationDtoExtensions
{
    public static NotificationDto ToDto(this Notification notification)
    {
        return new NotificationDto(
            Id: notification.Id,
            Type: notification.Type,
            Payload: notification.Payload,
            IsRead: notification.IsRead,
            CreatedAt: notification.CreatedAt,
            Sender: notification.Sender != null
                ? new NotificationSenderDto(
                    notification.Sender.Id,
                    notification.Sender.Username,
                    notification.Sender.AvatarUrl
                  )
                : null
        );
    }
}
