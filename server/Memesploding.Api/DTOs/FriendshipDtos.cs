using Memesploding.Shared.Enums;

namespace Memesploding.Api.DTOs;

public record FriendshipQueryDto(
    FriendshipStatus? Status = null,
    PaginationQueryDto? Pagination = null
);

public record FriendshipRequestDto(
    Guid UserId
);

public record ProcessFriendRequestDto(
    bool Accept // true = chấp nhận, false = từ chối
);
