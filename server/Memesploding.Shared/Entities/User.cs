using System;
using System.Collections.Generic;
using Memesploding.Shared.Enums;

namespace Memesploding.Shared.Entities;

public class User
{
    public Guid Id { get; set; } = Guid.NewGuid();

    // Thông tin xác thực (Auth)
    public AuthProvider Provider { get; set; }
    public string? ProviderId { get; set; }
    public string? Email { get; set; }      // Dùng cho Google login, null nếu là Guest

    // Thông tin hiển thị trong Game (gộp từ Profile)
    public string Username { get; set; } = string.Empty;    // Tên hiển thị trong Game
    public string? AvatarUrl { get; set; }
    public string? Bio { get; set; }

    // Thống kê Game (gộp từ Profile)
    public int Level { get; set; } = 1;
    public long Xp { get; set; } = 0;
    public int Score { get; set; } = 1000;
    public int HighestScore { get; set; } = 1000;
    public int TotalMatches { get; set; } = 0;
    public int TotalWins { get; set; } = 0;

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;

    // Navigation properties
    public ICollection<Friendship> InitiatedFriendships { get; set; } = new List<Friendship>();
    public ICollection<Friendship> ReceivedFriendships { get; set; } = new List<Friendship>();
    public ICollection<MatchParticipant> MatchParticipants { get; set; } = new List<MatchParticipant>();
    public ICollection<Notification> ReceivedNotifications { get; set; } = new List<Notification>();
    public ICollection<Notification> SentNotifications { get; set; } = new List<Notification>();
}
