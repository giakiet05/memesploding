namespace Memesploding.Api.DTOs;

public record RegisterGuestRequestDto(
    string Nickname
);

public record AuthResponseDto(
    Guid UserId,
    string Nickname,
    string AccessToken,
    string RefreshToken
);
