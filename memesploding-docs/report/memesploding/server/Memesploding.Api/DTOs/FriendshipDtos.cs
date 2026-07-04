using Memesploding.Shared.Enums;

namespace Memesploding.Api.DTOs;

public record FriendshipQueryDto
{
    public FriendshipStatus? Status { get; init; } = null;
    public PaginationQueryDto Pagination { get; init; } = new();
}

public record FriendshipRequestDto(
    Guid UserId
);

public record ProcessFriendRequestDto(
    bool Accept // true = chấp nhận, false = từ chối
);
