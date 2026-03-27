namespace Memesploding.Api.Services;

using System.Threading.Tasks;
using Memesploding.Api.DTOs;

public interface IAuthService
{
    Task<AuthResponseDto> RegisterGuestAsync(RegisterGuestRequestDto request);
    Task<AuthResponseDto> LoginGoogleAsync(LoginGoogleRequestDto request);
    Task<AuthResponseDto> RefreshAsync(RefreshTokenRequestDto request);
    Task LogoutAsync(string accessToken, string refreshToken);
}
