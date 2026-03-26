namespace Memesploding.Api.Services;

using System;
using System.Linq;
using System.Threading.Tasks;
using Memesploding.Api.Data;
using Memesploding.Api.DTOs;
using Memesploding.Shared.Entities;
using Memesploding.Shared.Enums;
using Memesploding.Shared.Infrastructure.Redis;
using Memesploding.Shared.Infrastructure.Security;

public class AuthService : IAuthService
{
    private readonly ApplicationDbContext _dbContext;
    private readonly ITokenService _tokenService;
    private readonly ICacheStore _cache;

    public AuthService(ApplicationDbContext dbContext, ITokenService tokenService, ICacheStore cache)
    {
        _dbContext = dbContext;
        _tokenService = tokenService;
        _cache = cache;
    }

    public async Task<AuthResponseDto> RegisterGuestAsync(RegisterGuestRequestDto request)
    {
        var user = new User
        {
            Provider = AuthProvider.Guest,
            ProviderId = null,
            Username = request.Nickname,
            AvatarUrl = "default-avatar.png",
            CreatedAt = DateTime.UtcNow
        };

        _dbContext.Users.Add(user);
        await _dbContext.SaveChangesAsync();

        var accessToken = _tokenService.GenerateAccessToken(user, user.Username);
        var refreshToken = _tokenService.GenerateRefreshToken();

        await _cache.SetAsync($"rt:{refreshToken}", user.Id.ToString(), TimeSpan.FromDays(30));

        return new AuthResponseDto(user.Id, user.Username, accessToken, refreshToken);
    }

    public async Task<AuthResponseDto> LoginGoogleAsync(LoginGoogleRequestDto request)
    {
        // TODO: Gọi Google OAuth2 API thật bằng request.Code
        // Tạm thời dùng dữ liệu giả, sẽ cắm vào sau khi có ClientId/ClientSecret
        var googleEmail = "vip_player@gmail.com";
        var googleId = "1042398420394823";
        var usernameFromGoogle = "GoogleGamer99";

        var user = _dbContext.Users.FirstOrDefault(u => u.ProviderId == googleId && u.Provider == AuthProvider.Google);

        if (user == null)
        {
            user = new User
            {
                Provider = AuthProvider.Google,
                ProviderId = googleId,
                Email = googleEmail,
                Username = usernameFromGoogle,
                AvatarUrl = "default-avatar.png"
            };
            _dbContext.Users.Add(user);
            await _dbContext.SaveChangesAsync();
        }

        var accessToken = _tokenService.GenerateAccessToken(user, user.Username);
        var refreshToken = _tokenService.GenerateRefreshToken();

        await _cache.SetAsync($"rt:{refreshToken}", user.Id.ToString(), TimeSpan.FromDays(30));

        return new AuthResponseDto(user.Id, user.Username, accessToken, refreshToken);
    }

    public async Task<AuthResponseDto> RefreshAsync(RefreshTokenRequestDto request)
    {
        var userIdStr = await _cache.GetAsync<string>($"rt:{request.RefreshToken}");
        if (string.IsNullOrEmpty(userIdStr))
            throw new Exception(ErrorCode.Unauthorized);

        var userId = Guid.Parse(userIdStr);
        var user = await _dbContext.Users.FindAsync(userId);
        if (user == null) throw new Exception(ErrorCode.UserNotFound);

        // Xoay Token: xóa cũ, cấp mới
        await _cache.RemoveAsync($"rt:{request.RefreshToken}");

        var newAccessToken = _tokenService.GenerateAccessToken(user, user.Username);
        var newRefreshToken = _tokenService.GenerateRefreshToken();

        await _cache.SetAsync($"rt:{newRefreshToken}", user.Id.ToString(), TimeSpan.FromDays(30));

        return new AuthResponseDto(user.Id, user.Username, newAccessToken, newRefreshToken);
    }

    public async Task LogoutAsync(string refreshToken)
    {
        await _cache.RemoveAsync($"rt:{refreshToken}");
    }
}
