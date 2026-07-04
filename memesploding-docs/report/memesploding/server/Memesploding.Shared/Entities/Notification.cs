using System;
using Memesploding.Shared.Enums;

namespace Memesploding.Shared.Entities;

public class Notification
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid ReceiverId { get; set; }
    public Guid? SenderId { get; set; }
    public NotificationType Type { get; set; }
    public string? Payload { get; set; } // Stored as JSON string
    public bool IsRead { get; set; } = false;
    
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    // Navigation properties
    public User? Receiver { get; set; }
    public User? Sender { get; set; }
}
