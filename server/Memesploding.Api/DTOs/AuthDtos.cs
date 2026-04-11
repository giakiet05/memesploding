namespace Memesploding.Api.DTOs;

public record RegisterGuestRequestDto(
    string DeviceId     // Client tự sinh 1 lần, lưu vào máy mãi mãi
);

public record AuthResponseDto(
    MeDto User,
    string AccessToken,
    string RefreshToken,
    bool IsNewUser  // True nếu lần đầu đăng nhập → Client hiện màn hình chọn Username
);

public record LoginGoogleRequestDto(
    string IdToken
);

public record RefreshTokenRequestDto(
    string RefreshToken
);
