using Microsoft.EntityFrameworkCore;
using Memesploding.Shared.Infrastructure.Cache;
using Memesploding.Shared.Auth;

namespace Memesploding.Api.Services;

using System;
using System.Linq;
using System.Security.Cryptography;
using System.Threading.Tasks;
using Google.Apis.Auth;
using Memesploding.Api.Data;
using Memesploding.Api.DTOs;
using Memesploding.Api.Exceptions;
using Memesploding.Shared.Entities;
using Memesploding.Shared.Enums;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.Configuration;
using Npgsql;

public class AuthService(ApplicationDbContext db, ITokenService tokenService, ICacheStore cache, IConfiguration config)
    : IAuthService
{
    private const int MinimumPasswordLength = 6;
    private static readonly PasswordHasher<User> PasswordHasher = new();

    public async Task<AuthResponseDto> RegisterGuestAsync(RegisterGuestRequestDto request)
    {
        var existingUser = await db.Users
            .FirstOrDefaultAsync(u => u.ProviderId == request.DeviceId && u.Provider == AuthProvider.Guest);

        if (existingUser != null)
        {
            var accessToken = tokenService.GenerateAccessToken(existingUser, existingUser.Username);
            var refreshToken = tokenService.GenerateRefreshToken();
            await cache.SetAsync($"rt:{refreshToken}", existingUser.Id.ToString(), TimeSpan.FromDays(30));
            return new AuthResponseDto(MeDto.FromEntity(existingUser), accessToken, refreshToken, IsNewUser: false);
        }

        User? user = null;
        for (var attempt = 0; attempt < 5; attempt++)
        {
            user = new User
            {
                Provider = AuthProvider.Guest,
                ProviderId = request.DeviceId,
                Username = await GenerateUniqueUsernameAsync("Guest", request.DeviceId),
                AvatarUrl = "default-avatar.png",
                CreatedAt = DateTime.UtcNow
            };

            db.Users.Add(user);
            try
            {
                await db.SaveChangesAsync();
                break;
            }
            catch (DbUpdateException ex) when (IsUsernameUniqueViolation(ex))
            {
                db.Entry(user).State = EntityState.Detached;
                user = null;
            }
        }

        if (user == null)
        {
            throw AppException.BadRequest(ErrorCode.ValidationFailed, "Unable to create guest account. Please retry.");
        }

        var newAccessToken = tokenService.GenerateAccessToken(user, user.Username);
        var newRefreshToken = tokenService.GenerateRefreshToken();
        await cache.SetAsync($"rt:{newRefreshToken}", user.Id.ToString(), TimeSpan.FromDays(30));

        return new AuthResponseDto(MeDto.FromEntity(user), newAccessToken, newRefreshToken, IsNewUser: true);
    }

    public async Task<AuthResponseDto> LoginGoogleAsync(LoginGoogleRequestDto request)
    {
        GoogleJsonWebSignature.Payload payload;
        try
        {
            var settings = new GoogleJsonWebSignature.ValidationSettings
            {
                Audience = new[] { config["Google:ClientId"] }
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

        var user = await db.Users
            .FirstOrDefaultAsync(u => u.ProviderId == googleId && u.Provider == AuthProvider.Google);

        bool isNewUser = false;
        if (user == null)
        {
            isNewUser = true;
            user = new User
            {
                Provider = AuthProvider.Google,
                ProviderId = googleId,
                Email = googleEmail,
                Username = await GenerateUniqueUsernameAsync("User", googleId),
                AvatarUrl = googleAvatar
            };
            db.Users.Add(user);
            try
            {
                await db.SaveChangesAsync();
            }
            catch (DbUpdateException ex) when (IsEmailUniqueViolation(ex))
            {
                db.Entry(user).State = EntityState.Detached;
                throw AppException.BadRequest(ErrorCode.ValidationFailed, "Email is already registered with another sign-in method");
            }
            catch (DbUpdateException ex) when (IsUsernameUniqueViolation(ex))
            {
                db.Entry(user).State = EntityState.Detached;
                throw AppException.BadRequest(ErrorCode.ValidationFailed, "Unable to create Google account. Please retry.");
            }
        }

        var accessToken = tokenService.GenerateAccessToken(user, user.Username);
        var refreshToken = tokenService.GenerateRefreshToken();
        await cache.SetAsync($"rt:{refreshToken}", user.Id.ToString(), TimeSpan.FromDays(30));

        return new AuthResponseDto(MeDto.FromEntity(user), accessToken, refreshToken, IsNewUser: isNewUser);
    }

    public async Task<AuthResponseDto> RegisterEmailAsync(RegisterEmailRequestDto request)
    {
        var email = NormalizeEmail(request.Email);
        ValidatePassword(request.Password);

        if (await db.Users.AnyAsync(u => u.Email == email))
        {
            throw AppException.BadRequest(ErrorCode.ValidationFailed, "Email is already registered");
        }

        User? user = null;
        for (var attempt = 0; attempt < 5; attempt++)
        {
            user = new User
            {
                Provider = AuthProvider.Email,
                ProviderId = email,
                Email = email,
                Username = await GenerateUniqueUsernameAsync("User", email),
                AvatarUrl = "default-avatar.png",
                CreatedAt = DateTime.UtcNow,
                UpdatedAt = DateTime.UtcNow
            };
            user.PasswordHash = PasswordHasher.HashPassword(user, request.Password);

            db.Users.Add(user);
            try
            {
                await db.SaveChangesAsync();
                break;
            }
            catch (DbUpdateException ex) when (IsEmailUniqueViolation(ex))
            {
                db.Entry(user).State = EntityState.Detached;
                throw AppException.BadRequest(ErrorCode.ValidationFailed, "Email is already registered");
            }
            catch (DbUpdateException ex) when (IsUsernameUniqueViolation(ex))
            {
                db.Entry(user).State = EntityState.Detached;
                user = null;
            }
        }

        if (user == null)
        {
            throw AppException.BadRequest(ErrorCode.ValidationFailed, "Unable to create account. Please retry.");
        }

        return await CreateAuthResponseAsync(user, isNewUser: true);
    }

    public async Task<AuthResponseDto> LoginEmailAsync(LoginEmailRequestDto request)
    {
        var email = NormalizeEmail(request.Email);
        var user = await db.Users.FirstOrDefaultAsync(u => u.Email == email && u.Provider == AuthProvider.Email);
        if (user == null || string.IsNullOrWhiteSpace(user.PasswordHash))
        {
            throw AppException.Unauthorized("Invalid email or password");
        }

        var result = PasswordHasher.VerifyHashedPassword(user, user.PasswordHash, request.Password);
        if (result == PasswordVerificationResult.Failed)
        {
            throw AppException.Unauthorized("Invalid email or password");
        }

        if (result == PasswordVerificationResult.SuccessRehashNeeded)
        {
            user.PasswordHash = PasswordHasher.HashPassword(user, request.Password);
            user.UpdatedAt = DateTime.UtcNow;
            await db.SaveChangesAsync();
        }

        return await CreateAuthResponseAsync(user, isNewUser: false);
    }

    public async Task SendForgotPasswordOtpAsync(ForgotPasswordSendOtpRequestDto request)
    {
        var email = NormalizeEmail(request.Email);
        var userExists = await db.Users.AnyAsync(u => u.Email == email && u.Provider == AuthProvider.Email);
        if (!userExists)
        {
            return;
        }

        var otp = RandomNumberGenerator.GetInt32(0, 1_000_000).ToString("D6");
        await cache.SetAsync(GetForgotPasswordOtpKey(email), otp, TimeSpan.FromMinutes(10));
        // TODO: Send OTP to the user's email address through an email provider.
    }

    public async Task VerifyForgotPasswordOtpAsync(ForgotPasswordVerifyOtpRequestDto request)
    {
        var email = NormalizeEmail(request.Email);
        var otp = await cache.GetAsync<string>(GetForgotPasswordOtpKey(email));
        if (string.IsNullOrWhiteSpace(otp) || otp != request.Otp.Trim())
        {
            throw AppException.BadRequest(ErrorCode.ValidationFailed, "Invalid or expired OTP");
        }
    }

    public async Task ResetForgotPasswordAsync(ForgotPasswordResetRequestDto request)
    {
        var email = NormalizeEmail(request.Email);
        ValidatePassword(request.NewPassword);

        var otp = await cache.GetAsync<string>(GetForgotPasswordOtpKey(email));
        if (string.IsNullOrWhiteSpace(otp) || otp != request.Otp.Trim())
        {
            throw AppException.BadRequest(ErrorCode.ValidationFailed, "Invalid or expired OTP");
        }

        var user = await db.Users.FirstOrDefaultAsync(u => u.Email == email && u.Provider == AuthProvider.Email);
        if (user == null)
        {
            throw AppException.NotFound("User not found");
        }

        user.PasswordHash = PasswordHasher.HashPassword(user, request.NewPassword);
        user.UpdatedAt = DateTime.UtcNow;
        await db.SaveChangesAsync();
        await cache.RemoveAsync(GetForgotPasswordOtpKey(email));
    }

    public async Task<AuthResponseDto> RefreshAsync(RefreshTokenRequestDto request)
    {
        var userIdStr = await cache.GetAsync<string>($"rt:{request.RefreshToken}");
        if (string.IsNullOrEmpty(userIdStr))
            throw AppException.Unauthorized("Invalid or expired refresh token");

        var userId = Guid.Parse(userIdStr);
        var user = await db.Users.FindAsync(userId);
        if (user == null) throw AppException.NotFound("User not found");

        // Rotation: Xóa token cũ ngay khi sử dụng
        await cache.RemoveAsync($"rt:{request.RefreshToken}");

        var newAccessToken = tokenService.GenerateAccessToken(user, user.Username);
        var newRefreshToken = tokenService.GenerateRefreshToken();
        await cache.SetAsync($"rt:{newRefreshToken}", user.Id.ToString(), TimeSpan.FromDays(30));

        return new AuthResponseDto(MeDto.FromEntity(user), newAccessToken, newRefreshToken, IsNewUser: false);
    }

    public async Task LogoutAsync(string accessToken, string refreshToken)
    {
        // 1. Chỉ xóa Refresh Token mà client gửi lên (Chỉ logout máy hiện tại)
        if (!string.IsNullOrEmpty(refreshToken))
        {
            await cache.RemoveAsync($"rt:{refreshToken}");
        }

        // 2. Blacklist Access Token hiện tại
        var remainingTtl = tokenService.GetRemainingTime(accessToken);
        if (remainingTtl.HasValue)
        {
            await cache.SetAsync($"bl:{accessToken[^20..]}", "revoked", remainingTtl.Value);
        }
    }

    private async Task<AuthResponseDto> CreateAuthResponseAsync(User user, bool isNewUser)
    {
        var accessToken = tokenService.GenerateAccessToken(user, user.Username);
        var refreshToken = tokenService.GenerateRefreshToken();
        await cache.SetAsync($"rt:{refreshToken}", user.Id.ToString(), TimeSpan.FromDays(30));
        return new AuthResponseDto(MeDto.FromEntity(user), accessToken, refreshToken, isNewUser);
    }

    private static string NormalizeEmail(string email)
    {
        if (string.IsNullOrWhiteSpace(email) || !email.Contains('@') || !email.Contains('.'))
        {
            throw AppException.BadRequest(ErrorCode.ValidationFailed, "A valid email is required");
        }

        return email.Trim().ToLowerInvariant();
    }

    private static void ValidatePassword(string password)
    {
        if (string.IsNullOrWhiteSpace(password) || password.Length < MinimumPasswordLength)
        {
            throw AppException.BadRequest(ErrorCode.ValidationFailed, $"Password must be at least {MinimumPasswordLength} characters");
        }
    }

    private static string GetForgotPasswordOtpKey(string email) => $"auth:forgot-password:{email}";

    private async Task<string> GenerateUniqueUsernameAsync(string prefix, string seed)
    {
        var normalizedSeed = NormalizeSeed(seed);
        for (var attempt = 0; attempt < 20; attempt++)
        {
            var suffix = Guid.NewGuid().ToString("N")[..6];
            var candidate = $"{prefix}_{normalizedSeed}_{suffix}";
            if (candidate.Length > 50)
            {
                candidate = candidate[..50];
            }

            var exists = await db.Users.AnyAsync(u => u.Username == candidate);
            if (!exists)
            {
                return candidate;
            }
        }

        throw AppException.BadRequest(ErrorCode.ValidationFailed, "Unable to generate unique username");
    }

    private static string NormalizeSeed(string seed)
    {
        var alnum = new string(seed.Where(char.IsLetterOrDigit).ToArray()).ToLowerInvariant();
        if (string.IsNullOrWhiteSpace(alnum))
        {
            return "user";
        }

        return alnum.Length <= 8 ? alnum : alnum[..8];
    }

    private static bool IsUsernameUniqueViolation(DbUpdateException ex)
    {
        return ex.InnerException is PostgresException pg
               && pg.SqlState == PostgresErrorCodes.UniqueViolation
               && pg.ConstraintName == "IX_users_Username";
    }

    private static bool IsEmailUniqueViolation(DbUpdateException ex)
    {
        return ex.InnerException is PostgresException pg
               && pg.SqlState == PostgresErrorCodes.UniqueViolation
               && pg.ConstraintName == "IX_users_Email";
    }
}
