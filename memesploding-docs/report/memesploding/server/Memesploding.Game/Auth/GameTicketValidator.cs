using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;
using Microsoft.IdentityModel.Tokens;

namespace Memesploding.Game.Auth;

public class GameTicketValidator(IConfiguration configuration, ILogger<GameTicketValidator> logger) : IGameTicketValidator
{
    public bool TryValidate(string token, out GameConnectionContext? context)
    {
        context = null;
        var jwtKey = configuration["Jwt:Key"];
        if (string.IsNullOrWhiteSpace(jwtKey))
        {
            logger.LogWarning("GameTicketValidator: Jwt:Key is missing or empty in configuration");
            return false;
        }

        var validIssuer = configuration["GameTicket:Issuer"] ?? configuration["Jwt:Issuer"];
        var validAudience = configuration["GameTicket:Audience"] ?? configuration["Jwt:Audience"];

        logger.LogDebug("GameTicketValidator: Validating with Issuer={Issuer}, Audience={Audience}", validIssuer, validAudience);

        var validationParameters = new TokenValidationParameters
        {
            ValidateIssuerSigningKey = true,
            IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(jwtKey)),
            ValidateIssuer = true,
            ValidIssuer = validIssuer,
            ValidateAudience = true,
            ValidAudience = validAudience,
            ValidateLifetime = true,
            ClockSkew = TimeSpan.Zero
        };

        var handler = new JwtSecurityTokenHandler();
        ClaimsPrincipal principal;
        try
        {
            principal = handler.ValidateToken(token, validationParameters, out _);
        }
        catch (Exception ex)
        {
            logger.LogWarning("GameTicketValidator: Token validation failed: {Message}", ex.Message);
            return false;
        }

        var scope = principal.Claims.FirstOrDefault(c => c.Type == "scope")?.Value;
        var subject = principal.Claims.FirstOrDefault(c => c.Type == JwtRegisteredClaimNames.Sub || c.Type == ClaimTypes.NameIdentifier)?.Value;
        var roomCode = principal.Claims.FirstOrDefault(c => c.Type == "room_code")?.Value;
        var matchIdClaim = principal.Claims.FirstOrDefault(c => c.Type == "match_id")?.Value;

        logger.LogWarning("GameTicketValidator: Claims - scope={Scope}, sub={Sub}, room_code={RoomCode}", scope, subject, roomCode);

        if (scope != "game_ws" || string.IsNullOrWhiteSpace(subject) || string.IsNullOrWhiteSpace(roomCode))
        {
            logger.LogWarning("GameTicketValidator: Invalid claims - scope={Scope}, sub={Sub}, room_code={RoomCode}", scope, subject, roomCode);
            return false;
        }

        if (!Guid.TryParse(subject, out var userId))
        {
            logger.LogWarning("GameTicketValidator: Cannot parse userId from sub={Sub}", subject);
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
