using Memesploding.Api.Data;
using Memesploding.Api.DTOs;
using Memesploding.Api.Exceptions;
using Memesploding.Shared.Auth;
using Memesploding.Shared.Messaging.EventBus;
using Memesploding.Shared.Messaging.Integration.Channels;
using Memesploding.Shared.Messaging.Integration.Events;

namespace Memesploding.Api.Services;

public class BotTestMatchService(
    ApplicationDbContext db,
    IConfiguration config,
    IEventBus eventBus,
    ITokenService tokenService
) : IBotTestMatchService
{
    private const int BotCount = 3;
    private const string RoomCodeChars = "ABCDEFGHJKLMNPQRSTUVWXYZ23456789";

    public async Task<BotTestMatchDto> StartAsync(Guid userId)
    {
        var user = await db.Users.FindAsync(userId);
        if (user == null)
        {
            throw AppException.NotFound("User not found");
        }

        var matchId = Guid.NewGuid();
        var roomCode = GenerateRoomCode();
        var players = new List<IntegrationPlayerInfo>
        {
            new(user.Id, user.Username, user.AvatarUrl ?? "", "player")
        };

        for (var index = 1; index <= BotCount; index++)
        {
            players.Add(new IntegrationPlayerInfo(
                Guid.NewGuid(),
                $"Bot {index}",
                "",
                "bot"
            ));
        }

        await eventBus.PublishAsync(
            GameIntegrationChannels.StartMatchRequested,
            new StartMatchRequestedEvent(
                matchId,
                roomCode,
                user.Id,
                [],
                players,
                DateTime.UtcNow,
                IsTestMatch: true
            )
        );

        var connection = new RoomConnectionDto(
            config["Realtime:GameWsUrl"] ?? "ws://localhost:5217/ws",
            tokenService.GenerateGameTicket(user.Id, roomCode, matchId)
        );

        var participants = players
            .Select(player => new RoomParticipantDto(
                player.UserId,
                player.Nickname,
                player.AvatarUrl,
                player.Role,
                IsReady: true
            ))
            .ToList();

        return new BotTestMatchDto(matchId, roomCode, connection, participants);
    }

    private static string GenerateRoomCode()
    {
        Span<char> chars = stackalloc char[6];
        chars[0] = 'T';

        for (var index = 1; index < chars.Length; index++)
        {
            chars[index] = RoomCodeChars[Random.Shared.Next(RoomCodeChars.Length)];
        }

        return new string(chars);
    }
}
