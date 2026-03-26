using System;
using Memesploding.Shared.Enums;

namespace Memesploding.Shared.Entities;

public class Friendship
{
    public Guid UserId1 { get; set; }
    public Guid UserId2 { get; set; }
    public FriendshipStatus Status { get; set; }
    
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;

    // Navigation properties
    public User? User1 { get; set; }
    public User? User2 { get; set; }
}
