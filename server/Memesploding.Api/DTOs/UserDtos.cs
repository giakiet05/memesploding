using System.Text.Json.Serialization;
using Memesploding.Shared.Entities;
using Memesploding.Shared.Enums;

namespace Memesploding.Api.DTOs;

public record UpdateUserRequestDto(string? Username, string? Bio, string? AvatarUrl);

public record UserQueryDto
{
    public string? SearchQuery { get; init; }
    public PaginationQueryDto Pagination { get; init; } = new();
}

public record UserStatsDto(
    long Xp,
    long NextLevelXp,
    int Score,
    int Level,
    int HighestScore,
    int TotalMatches,
    int TotalWins,
    double WinRate,
    int GlobalRank
);

public record MeDto(
    Guid Id,
    string Username,
    string Email,
    AuthProvider Provider,
    string AvatarUrl,
    string Bio,
    int Level,
    int Score,
    DateTime CreatedAt,
    DateTime UpdatedAt
)
{
    // Factory method: Chuyển User Entity → MeDto (Dành cho chính chủ)
    public static MeDto FromEntity(User user) => new(
        user.Id,
        user.Username,
        user.Email ?? string.Empty,
        user.Provider,
        user.AvatarUrl ?? string.Empty,
        user.Bio ?? string.Empty,
        user.Level,
        user.Score,
        user.CreatedAt,
        user.UpdatedAt
    );
}

public record UserProfileDto(
    Guid Id,
    string Username,
    string AvatarUrl,
    string Bio,
    int Level,
    int Score,
    RelationshipType Relationship = RelationshipType.None
)
{
    public static UserProfileDto FromEntity(User user, RelationshipType relationship = RelationshipType.None)
    {
        return new UserProfileDto(
            user.Id,
            user.Username,
            user.AvatarUrl ?? string.Empty,
            user.Bio ?? string.Empty,
            user.Level,
            user.Score,
            relationship
        );
    }

    public static RelationshipType MapRelationship(Friendship? friendship, Guid currentUserId)
    {
        if (friendship == null) return RelationshipType.None;
        if (friendship.Status == FriendshipStatus.Accepted) return RelationshipType.Accepted;
        if (friendship.Status == FriendshipStatus.Pending)
        {
            return friendship.RequesterId == currentUserId 
                ? RelationshipType.PendingSent 
                : RelationshipType.PendingReceived;
        }
        return RelationshipType.None;
    }
}