using System;

namespace Network.API.Models
{
    [Serializable]
    public class NotificationDto
    {
        public string Id;
        public string Type;
        public string Payload;
        public bool IsRead;
        public DateTime CreatedAt;
        public NotificationSenderDto Sender;
    }

    [Serializable]
    public class NotificationSenderDto
    {
        public string Id;
        public string Username;
        public string AvatarUrl;
    }

    [Serializable]
    public class UnreadCountDto
    {
        public int Count;
    }
}
