namespace Memesploding.Api.Services;

using System;
using System.Linq;
using System.Threading.Tasks;
using Google.Apis.Auth;
using Memesploding.Api.Data;
using Memesploding.Api.DTOs;
using Memesploding.Api.Exceptions;
using Memesploding.Shared.Entities;
using Memesploding.Shared.Enums;
using Memesploding.Shared.Infrastructure.Cache;
using Memesploding.Shared.Infrastructure.Security;
using Microsoft.Extensions.Configuration;

public class AuthService : IAuthService
{
    private readonly ApplicationDbContext _dbContext;
    private readonly ITokenService _tokenService;
    private readonly ICacheStore _cache;
    private readonly IConfiguration _config;

    public AuthService(ApplicationDbContext dbContext, ITokenService tokenService, ICacheStore cache, IConfiguration config)
    {
        _dbContext = dbContext;
        _tokenService = tokenService;
        _cache = cache;
        _config = config;
    }

    public async Task<AuthResponseDto> RegisterGuestAsync(RegisterGuestRequestDto request)
    {
        var existingUser = _dbContext.Users
            .FirstOrDefault(u => u.ProviderId == request.DeviceId && u.Provider == AuthProvider.Guest);

        if (existingUser != null)
        {
            var accessToken = _tokenService.GenerateAccessToken(existingUser, existingUser.Username);
            var refreshToken = _tokenService.GenerateRefreshToken();
            await _cache.SetAsync($"rt:{refreshToken}", existingUser.Id.ToString(), TimeSpan.FromDays(30));
            return new AuthResponseDto(UserDto.FromEntity(existingUser), accessToken, refreshToken, IsNewUser: false);
        }

        var user = new User
        {
            Provider = AuthProvider.Guest,
            ProviderId = request.DeviceId,
            Username = $"Guest_{request.DeviceId[..8]}",
            AvatarUrl = "default-avatar.png",
            CreatedAt = DateTime.UtcNow
        };

        _dbContext.Users.Add(user);
        await _dbContext.SaveChangesAsync();

        var newAccessToken = _tokenService.GenerateAccessToken(user, user.Username);
        var newRefreshToken = _tokenService.GenerateRefreshToken();
        await _cache.SetAsync($"rt:{newRefreshToken}", user.Id.ToString(), TimeSpan.FromDays(30));

        return new AuthResponseDto(UserDto.FromEntity(user), newAccessToken, newRefreshToken, IsNewUser: true);
    }

    public async Task<AuthResponseDto> LoginGoogleAsync(LoginGoogleRequestDto request)
    {
        GoogleJsonWebSignature.Payload payload;
        try
        {
            var settings = new GoogleJsonWebSignature.ValidationSettings
            {
                Audience = new[] { _config["Google:ClientId"] }
            };
            payload = await GoogleJsonWebSignature.ValidateAsync(request.IdToken, settings);
        }
        catch (InvalidJwtException)
        {
            throw AppException.Unauthorized("Invalid Google IdToken");
        }

        var googleId = payload.Subject;
        var googleEmail = payload.Email;
        var googleAvatar = payload.Picture;

        var user = _dbContext.Users
            .FirstOrDefault(u => u.ProviderId == googleId && u.Provider == AuthProvider.Google);

        bool isNewUser = false;
        if (user == null)
        {
            isNewUser = true;
            user = new User
            {
                Provider = AuthProvider.Google,
                ProviderId = googleId,
                Email = googleEmail,
                Username = $"User_{googleId[..8]}",
                AvatarUrl = googleAvatar
            };
            _dbContext.Users.Add(user);
            await _dbContext.SaveChangesAsync();
        }

        var accessToken = _tokenService.GenerateAccessToken(user, user.Username);
        var refreshToken = _tokenService.GenerateRefreshToken();
        await _cache.SetAsync($"rt:{refreshToken}", user.Id.ToString(), TimeSpan.FromDays(30));

        return new AuthResponseDto(UserDto.FromEntity(user), accessToken, refreshToken, IsNewUser: isNewUser);
    }

    public async Task<AuthResponseDto> RefreshAsync(RefreshTokenRequestDto request)
    {
        var userIdStr = await _cache.GetAsync<string>($"rt:{request.RefreshToken}");
        if (string.IsNullOrEmpty(userIdStr))
            throw AppException.Unauthorized("Invalid or expired refresh token");

        var userId = Guid.Parse(userIdStr);
        var user = await _dbContext.Users.FindAsync(userId);
        if (user == null) throw AppException.NotFound(ErrorCode.UserNotFound, "User not found");

        // Rotation: Xóa token cũ ngay khi sử dụng
        await _cache.RemoveAsync($"rt:{request.RefreshToken}");

        var newAccessToken = _tokenService.GenerateAccessToken(user, user.Username);
        var newRefreshToken = _tokenService.GenerateRefreshToken();
        await _cache.SetAsync($"rt:{newRefreshToken}", user.Id.ToString(), TimeSpan.FromDays(30));

        return new AuthResponseDto(UserDto.FromEntity(user), newAccessToken, newRefreshToken, IsNewUser: false);
    }

    public async Task LogoutAsync(string accessToken, string refreshToken)
    {
        // 1. Chỉ xóa Refresh Token mà client gửi lên (Chỉ logout máy hiện tại)
        if (!string.IsNullOrEmpty(refreshToken))
        {
            await _cache.RemoveAsync($"rt:{refreshToken}");
        }

        // 2. Blacklist Access Token hiện tại
        var remainingTtl = _tokenService.GetRemainingTime(accessToken);
        if (remainingTtl.HasValue)
        {
            await _cache.SetAsync($"bl:{accessToken[^20..]}", "revoked", remainingTtl.Value);
        }
    }
}
