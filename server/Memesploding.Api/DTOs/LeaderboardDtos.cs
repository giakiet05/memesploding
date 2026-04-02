namespace Memesploding.Api.DTOs;

public record LeaderboardEntryDto(
    int Rank,
    Guid UserId,
    string Nickname,
    string? AvatarUrl,
    int Level,
    int Score,
    int TotalWins
);
