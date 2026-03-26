using System;

namespace Memesploding.Shared.Entities;

public class Profile
{
    public Guid UserId { get; set; }
    public string Nickname { get; set; } = string.Empty;
    public string? AvatarUrl { get; set; }
    public int Level { get; set; } = 1;
    public long Xp { get; set; } = 0;
    public int Score { get; set; } = 1000;
    public int HighestScore { get; set; } = 1000;
    public int TotalMatches { get; set; } = 0;
    public int TotalWins { get; set; } = 0;
    public string? Bio { get; set; }

    // Navigation property
    public User? User { get; set; }
}
