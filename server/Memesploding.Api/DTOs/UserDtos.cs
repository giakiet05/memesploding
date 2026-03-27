using Memesploding.Shared.Entities;
using Memesploding.Shared.Enums;

namespace Memesploding.Api.DTOs;

public record UserDto(
    Guid Id,
    string Username,
    string Email,
    AuthProvider Provider,
    string AvatarUrl,
    string Bio,
    int HighestScore,
    int Score,
    int TotalWins,
    int TotalMatches,
    long Xp,
    DateTime CreatedAt,
    DateTime UpdatedAt
)
{
    // Factory method: Chuyển User Entity → UserDto (Không lộ thông tin nội bộ ra ngoài)
    public static UserDto FromEntity(User user) => new(
        user.Id,
        user.Username,
        user.Email ?? string.Empty,
        user.Provider,
        user.AvatarUrl ?? string.Empty,
        user.Bio ?? string.Empty,
        user.HighestScore,
        user.Score,
        user.TotalWins,
        user.TotalMatches,
        user.Xp,
        user.CreatedAt,
        user.UpdatedAt
    );
}