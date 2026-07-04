namespace Memesploding.Shared.Auth;

using Memesploding.Shared.Entities;

public interface ITokenService
{
    string GenerateAccessToken(User user, string username);
    string GenerateGameTicket(Guid userId, string roomCode, Guid? matchId = null);
    string GenerateRefreshToken();
    TimeSpan? GetRemainingTime(string accessToken); // Calculate remaining TTL of the token
}
