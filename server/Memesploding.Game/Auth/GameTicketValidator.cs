using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;
using Microsoft.IdentityModel.Tokens;

namespace Memesploding.Game.Auth;

public class GameTicketValidator(IConfiguration configuration) : IGameTicketValidator
{
    public bool TryValidate(string token, out GameConnectionContext? context)
    {
        context = null;
        var jwtKey = configuration["Jwt:Key"];
        if (string.IsNullOrWhiteSpace(jwtKey))
        {
            return false;
        }

        var validationParameters = new TokenValidationParameters
        {
            ValidateIssuerSigningKey = true,
            IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(jwtKey)),
            ValidateIssuer = true,
            ValidIssuer = configuration["GameTicket:Issuer"] ?? configuration["Jwt:Issuer"],
            ValidateAudience = true,
            ValidAudience = configuration["GameTicket:Audience"] ?? configuration["Jwt:Audience"],
            ValidateLifetime = true,
            ClockSkew = TimeSpan.Zero
        };

        var handler = new JwtSecurityTokenHandler();
        ClaimsPrincipal principal;
        try
        {
            principal = handler.ValidateToken(token, validationParameters, out _);
        }
        catch
        {
            return false;
        }

        var scope = principal.Claims.FirstOrDefault(c => c.Type == "scope")?.Value;
        var subject = principal.Claims.FirstOrDefault(c => c.Type == JwtRegisteredClaimNames.Sub)?.Value;
        var roomCode = principal.Claims.FirstOrDefault(c => c.Type == "room_code")?.Value;
        var matchIdClaim = principal.Claims.FirstOrDefault(c => c.Type == "match_id")?.Value;

        if (scope != "game_ws" || string.IsNullOrWhiteSpace(subject) || string.IsNullOrWhiteSpace(roomCode))
        {
            return false;
        }

        if (!Guid.TryParse(subject, out var userId))
        {
            return false;
        }

        Guid? matchId = null;
        if (!string.IsNullOrWhiteSpace(matchIdClaim) && Guid.TryParse(matchIdClaim, out var parsed))
        {
            matchId = parsed;
        }

        context = new GameConnectionContext(userId, roomCode.ToUpperInvariant(), matchId);
        return true;
    }
}
