namespace Memesploding.Shared.Infrastructure.Security;

using Memesploding.Shared.Entities;

public interface ITokenService
{
    string GenerateAccessToken(User user, string username);
    string GenerateRefreshToken();
}
