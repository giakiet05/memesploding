using Memesploding.Api.DTOs;
using Memesploding.Api.Hubs;
using Memesploding.Api.Services;
using Memesploding.Api.Messaging.Events;
using Memesploding.Api.Infrastructure.Cache;
using Memesploding.Api.Messaging.Channels;
using Memesploding.Api.Data;
using Memesploding.Shared.Entities;
using Memesploding.Shared.Auth;
using Microsoft.Extensions.Configuration;
using Microsoft.AspNetCore.SignalR;
using Microsoft.EntityFrameworkCore;
using StackExchange.Redis;
using Memesploding.Shared.Infrastructure.Cache;
using Memesploding.Shared.Messaging.EventBus;
using Memesploding.Shared.Messaging.Integration.Channels;
using Memesploding.Shared.Messaging.Integration.Events;
using System.Text.Json;

namespace Memesploding.Api.Workers;

public class RoomEventWorker : BackgroundService
{
    private readonly IServiceProvider _serviceProvider;
    private readonly IEventBus _eventBus;
    private readonly ICacheStore _cache;
    private readonly ILogger<RoomEventWorker> _logger;
    private readonly string _gameWsUrl;

    public RoomEventWorker(
        IServiceProvider serviceProvider, 
        IEventBus eventBus,
        ICacheStore cache,
        IConfiguration configuration,
        ILogger<RoomEventWorker> logger)
    {
        _serviceProvider = serviceProvider;
        _eventBus = eventBus;
        _cache = cache;
        _logger = logger;
        _gameWsUrl = configuration["Realtime:GameWsUrl"]
                     ?? throw new InvalidOperationException("Missing Realtime:GameWsUrl in configuration");
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation("RoomEventWorker is starting.");

        // Subscribe to Room/Invitation events (SignalR)
        await _eventBus.SubscribeAsync<RoomInvitationSentEvent>(EventChannels.RoomInvitationSent, HandleRoomInvitationSentAsync);
        await _eventBus.SubscribeAsync<RoomInvitationRespondedEvent>(EventChannels.RoomInvitationResponded, HandleRoomInvitationRespondedAsync);
        await _eventBus.SubscribeAsync<RoomJoinRequestSentEvent>(EventChannels.RoomJoinRequestSent, HandleRoomJoinRequestSentAsync);
        await _eventBus.SubscribeAsync<RoomJoinRequestRespondedEvent>(EventChannels.RoomJoinRequestResponded, HandleRoomJoinRequestRespondedAsync);
        await _eventBus.SubscribeAsync<RoomMemberJoinedEvent>(EventChannels.RoomMemberJoined, HandleRoomMemberJoinedAsync);
        await _eventBus.SubscribeAsync<RoomMemberLeftEvent>(EventChannels.RoomMemberLeft, HandleRoomMemberLeftAsync);
        await _eventBus.SubscribeAsync<RoomMemberKickedEvent>(EventChannels.RoomMemberKicked, HandleRoomMemberKickedAsync);
        await _eventBus.SubscribeAsync<RoomReadyStatusChangedEvent>(EventChannels.RoomReadyStatusChanged, HandleRoomReadyStatusChangedAsync);
        await _eventBus.SubscribeAsync<RoomMatchStartingEvent>(EventChannels.RoomMatchStarting, HandleRoomMatchStartingAsync);
        await _eventBus.SubscribeAsync<RoomHostChangedEvent>(EventChannels.RoomHostChanged, HandleRoomHostChangedAsync);
        await _eventBus.SubscribeAsync<RoomDissolvedEvent>(EventChannels.RoomDissolved, HandleRoomDissolvedAsync);
        
        // Subscribe to Room updates from Game Server
        await _eventBus.SubscribeAsync<RoomUpdatedIntegrationEvent>(GameIntegrationChannels.RoomUpdated, HandleRoomUpdateAsync);
        await _eventBus.SubscribeAsync<MatchEndedIntegrationEvent>(GameIntegrationChannels.MatchEnded, HandleMatchEndedAsync);
    }

    private async Task HandleRoomUpdateAsync(RoomUpdatedIntegrationEvent update)
    {
        // 1. Update room info cache
        if (!string.IsNullOrEmpty(update.RoomCode))
        {
            var roomInfoKey = CacheKeys.RoomInfo(update.RoomCode);
            await _cache.HashSetAsync(roomInfoKey, new[]
            {
                new HashEntry("status", update.Status),
                new HashEntry("is_public", update.IsPublic ? "true" : "false"),
                new HashEntry("current_players", update.CurrentPlayers.ToString()),
                new HashEntry("max_players", update.MaxPlayers.ToString())
            });
            await _cache.KeyExpireAsync(roomInfoKey, TimeSpan.FromHours(1));

            _logger.LogDebug("Updated room info cache: {RoomCode}", update.RoomCode);
        }

        // 2. Update presence for all players in room
        if (update.PlayerIds != null && update.PlayerIds.Count > 0)
        {
            using var scope = _serviceProvider.CreateScope();
            var presenceService = scope.ServiceProvider.GetRequiredService<IPresenceService>();

            foreach (var playerId in update.PlayerIds)
            {
                await presenceService.UpdateUserActivityAsync(playerId);
            }

            _logger.LogInformation(
                "Updated presence for {Count} players in room {RoomCode}",
                update.PlayerIds.Count,
                update.RoomCode
            );
        }
    }

    private async Task HandleMatchEndedAsync(MatchEndedIntegrationEvent @event)
    {
        using var scope = _serviceProvider.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        var hubContext = scope.ServiceProvider.GetRequiredService<IHubContext<AppHub>>();
        var presenceService = scope.ServiceProvider.GetRequiredService<IPresenceService>();

        var roomCode = @event.RoomCode.ToUpperInvariant();
        var roomInfo = await _cache.HashGetAllAsync(CacheKeys.RoomInfo(roomCode));
        var roomInfoDict = roomInfo.ToDictionary(x => x.Name.ToString(), x => x.Value.ToString());
        var maxPlayers = int.TryParse(roomInfoDict.GetValueOrDefault("max_players"), out var maxPlayersParsed) ? maxPlayersParsed : 0;
        var cardSetIds = (await _cache.SetMembersAsync(CacheKeys.RoomCardSets(roomCode)))
            .Select(raw => Guid.TryParse(raw, out var id) ? id : Guid.Empty)
            .Where(id => id != Guid.Empty)
            .Distinct()
            .ToList();

        var participantIds = await GetRoomParticipantIdsAsync(roomCode);
        var participantResults = (@event.Participants ?? [])
            .GroupBy(x => x.UserId)
            .Select(g => g.First())
            .ToDictionary(x => x.UserId, x => x.FinalRank);

        foreach (var participantId in participantIds)
        {
            if (!participantResults.ContainsKey(participantId))
            {
                participantResults[participantId] = @event.WinnerId == participantId ? 1 : 2;
            }
        }

        var match = await db.Matches
            .Include(m => m.Participants)
            .FirstOrDefaultAsync(m => m.Id == @event.MatchId);

        var settingsJson = JsonSerializer.Serialize(new
        {
            roomCode,
            cardSetIds,
            maxPlayers
        });
        var statsJson = JsonSerializer.Serialize(new
        {
            totalTurns = @event.TotalTurns,
            totalCards = @event.TotalCardsPlayed
        });

        if (match == null)
        {
            match = new Match
            {
                Id = @event.MatchId,
                WinnerId = @event.WinnerId,
                StartedAt = @event.StartedAt ?? @event.EndedAt,
                EndedAt = @event.EndedAt,
                Settings = settingsJson,
                Stats = statsJson
            };

            foreach (var (userId, finalRank) in participantResults.OrderBy(x => x.Value))
            {
                match.Participants.Add(new MatchParticipant
                {
                    MatchId = @event.MatchId,
                    UserId = userId,
                    FinalRank = finalRank,
                    XpEarned = 0,
                    ScoreChange = 0
                });
            }

            await db.Matches.AddAsync(match);
        }
        else
        {
            match.WinnerId = @event.WinnerId;
            match.EndedAt = @event.EndedAt;
            match.StartedAt = @event.StartedAt ?? match.StartedAt;
            match.Settings = settingsJson;
            match.Stats = statsJson;

            db.MatchParticipants.RemoveRange(match.Participants);
            foreach (var (userId, finalRank) in participantResults.OrderBy(x => x.Value))
            {
                await db.MatchParticipants.AddAsync(new MatchParticipant
                {
                    MatchId = @event.MatchId,
                    UserId = userId,
                    FinalRank = finalRank,
                    XpEarned = 0,
                    ScoreChange = 0
                });
            }
        }

        await db.SaveChangesAsync();

        await _cache.HashSetAsync(CacheKeys.RoomInfo(roomCode), "status", "waiting");

        var payload = WsMessage<WsMatchEndedDto>.Create(
            WsEventType.MatchEnded,
            new WsMatchEndedDto(@event.MatchId, roomCode, @event.WinnerId, @event.EndedAt)
        );

        var connections = new List<string>();
        foreach (var userId in participantIds.Distinct())
        {
            var userConnections = await presenceService.GetUserConnectionsAsync(userId);
            connections.AddRange(userConnections);
        }

        if (connections.Count > 0)
        {
            await hubContext.Clients.Clients(connections.Distinct().ToList()).SendAsync("ReceiveMessage", payload);
        }

        _logger.LogInformation("Persisted match {MatchId} result for room {RoomCode}", @event.MatchId, roomCode);
    }

    private async Task HandleRoomInvitationSentAsync(RoomInvitationSentEvent @event)
    {
        using var scope = _serviceProvider.CreateScope();
        var hubContext = scope.ServiceProvider.GetRequiredService<IHubContext<AppHub>>();
        var presenceService = scope.ServiceProvider.GetRequiredService<IPresenceService>();

        var friendConnections = await presenceService.GetUserConnectionsAsync(@event.InviteeId);
        if (friendConnections.Count > 0)
        {
            var invitationDto = new WsRoomInvitationDto(
                @event.InvitationId,
                @event.RoomCode,
                @event.InviterId,
                @event.InviterUsername,
                @event.InviterAvatarUrl,
                @event.IsPublic,
                @event.CurrentPlayers,
                @event.MaxPlayers,
                @event.ExpiresAt
            );
            var wsMessage = WsMessage<WsRoomInvitationDto>.Create(WsEventType.RoomInvitationReceived, invitationDto);

            await hubContext.Clients.Clients(friendConnections).SendAsync("ReceiveMessage", wsMessage);
            _logger.LogInformation("Handled RoomInvitationSentEvent to {InviteeId}", @event.InviteeId);
        }
    }

    private async Task HandleRoomInvitationRespondedAsync(RoomInvitationRespondedEvent @event)
    {
        using var scope = _serviceProvider.CreateScope();
        var hubContext = scope.ServiceProvider.GetRequiredService<IHubContext<AppHub>>();
        var presenceService = scope.ServiceProvider.GetRequiredService<IPresenceService>();

        var inviterConnections = await presenceService.GetUserConnectionsAsync(@event.InviterId);
        if (inviterConnections.Count > 0)
        {
            var responseDto = new WsRoomInvitationResponseDto(
                @event.InvitationId,
                @event.Accepted,
                @event.InviteeId,
                @event.InviteeUsername
            );
            var wsMessage = WsMessage<WsRoomInvitationResponseDto>.Create(WsEventType.RoomInvitationResponse, responseDto);

            await hubContext.Clients.Clients(inviterConnections).SendAsync("ReceiveMessage", wsMessage);
            _logger.LogInformation("Handled RoomInvitationRespondedEvent for {InviterId}", @event.InviterId);
        }
    }

    private async Task HandleRoomJoinRequestSentAsync(RoomJoinRequestSentEvent @event)
    {
        using var scope = _serviceProvider.CreateScope();
        var hubContext = scope.ServiceProvider.GetRequiredService<IHubContext<AppHub>>();
        var presenceService = scope.ServiceProvider.GetRequiredService<IPresenceService>();

        var memberConnectionIds = new List<string>();
        foreach (var memberId in @event.RoomMemberIds)
        {
            var connections = await presenceService.GetUserConnectionsAsync(memberId);
            memberConnectionIds.AddRange(connections);
        }

        if (memberConnectionIds.Count > 0)
        {
            var requestDto = new WsJoinRoomRequestDto(
                @event.RequestId,
                @event.RoomCode,
                @event.RequesterId,
                @event.RequesterUsername,
                @event.RequesterAvatarUrl,
                @event.ExpiresAt
            );
            var wsMessage = WsMessage<WsJoinRoomRequestDto>.Create(WsEventType.JoinRoomRequested, requestDto);

            await hubContext.Clients.Clients(memberConnectionIds).SendAsync("ReceiveMessage", wsMessage);
            _logger.LogInformation("Handled RoomJoinRequestSentEvent for room {RoomCode}", @event.RoomCode);
        }
    }

    private async Task HandleRoomJoinRequestRespondedAsync(RoomJoinRequestRespondedEvent @event)
    {
        using var scope = _serviceProvider.CreateScope();
        var hubContext = scope.ServiceProvider.GetRequiredService<IHubContext<AppHub>>();
        var presenceService = scope.ServiceProvider.GetRequiredService<IPresenceService>();

        var requesterConnections = await presenceService.GetUserConnectionsAsync(@event.RequesterId);
        if (requesterConnections.Count > 0)
        {
            var responseDto = new WsJoinRoomResponseDto(
                @event.RequestId,
                @event.RoomCode,
                @event.Accepted,
                @event.ResponderId,
                @event.ResponderUsername
            );
            var wsMessage = WsMessage<WsJoinRoomResponseDto>.Create(WsEventType.JoinRoomResponse, responseDto);

            await hubContext.Clients.Clients(requesterConnections).SendAsync("ReceiveMessage", wsMessage);
            _logger.LogInformation("Handled RoomJoinRequestRespondedEvent for {RequesterId}", @event.RequesterId);
        }
    }

    private async Task HandleRoomMemberJoinedAsync(RoomMemberJoinedEvent @event)
    {
        using var scope = _serviceProvider.CreateScope();
        var hubContext = scope.ServiceProvider.GetRequiredService<IHubContext<AppHub>>();
        var presenceService = scope.ServiceProvider.GetRequiredService<IPresenceService>();
        var payload = WsMessage<WsRoomMemberJoinedDto>.Create(
            WsEventType.RoomMemberJoined,
            new WsRoomMemberJoinedDto(
                @event.RoomCode,
                @event.UserId,
                @event.Nickname,
                @event.AvatarUrl,
                @event.Role,
                @event.IsReady
            )
        );

        await BroadcastToUsersAsync(hubContext, presenceService, @event.RecipientIds, payload);
    }

    private async Task HandleRoomMemberLeftAsync(RoomMemberLeftEvent @event)
    {
        using var scope = _serviceProvider.CreateScope();
        var hubContext = scope.ServiceProvider.GetRequiredService<IHubContext<AppHub>>();
        var presenceService = scope.ServiceProvider.GetRequiredService<IPresenceService>();
        var payload = WsMessage<WsRoomMemberLeftDto>.Create(
            WsEventType.RoomMemberLeft,
            new WsRoomMemberLeftDto(@event.RoomCode, @event.UserId)
        );

        await BroadcastToUsersAsync(hubContext, presenceService, @event.RecipientIds, payload);
    }

    private async Task HandleRoomMemberKickedAsync(RoomMemberKickedEvent @event)
    {
        using var scope = _serviceProvider.CreateScope();
        var hubContext = scope.ServiceProvider.GetRequiredService<IHubContext<AppHub>>();
        var presenceService = scope.ServiceProvider.GetRequiredService<IPresenceService>();
        var payload = WsMessage<WsRoomMemberKickedDto>.Create(
            WsEventType.RoomMemberKicked,
            new WsRoomMemberKickedDto(@event.RoomCode, @event.TargetUserId, @event.KickedByUserId)
        );

        await BroadcastToUsersAsync(hubContext, presenceService, @event.RecipientIds, payload);
    }

    private async Task HandleRoomReadyStatusChangedAsync(RoomReadyStatusChangedEvent @event)
    {
        using var scope = _serviceProvider.CreateScope();
        var hubContext = scope.ServiceProvider.GetRequiredService<IHubContext<AppHub>>();
        var presenceService = scope.ServiceProvider.GetRequiredService<IPresenceService>();
        var payload = WsMessage<WsRoomReadyStatusChangedDto>.Create(
            WsEventType.RoomReadyStatusChanged,
            new WsRoomReadyStatusChangedDto(@event.RoomCode, @event.UserId, @event.IsReady)
        );

        await BroadcastToUsersAsync(hubContext, presenceService, @event.RecipientIds, payload);
    }

    private async Task HandleRoomMatchStartingAsync(RoomMatchStartingEvent @event)
    {
        using var scope = _serviceProvider.CreateScope();
        var hubContext = scope.ServiceProvider.GetRequiredService<IHubContext<AppHub>>();
        var presenceService = scope.ServiceProvider.GetRequiredService<IPresenceService>();
        var tokenService = scope.ServiceProvider.GetRequiredService<ITokenService>();
        foreach (var userId in @event.RecipientIds.Distinct())
        {
            var connections = await presenceService.GetUserConnectionsAsync(userId);
            if (connections.Count == 0)
            {
                continue;
            }

            var gameTicket = tokenService.GenerateGameTicket(userId, @event.RoomCode);
            var payload = WsMessage<WsRoomMatchStartingDto>.Create(
                WsEventType.RoomMatchStarting,
                new WsRoomMatchStartingDto(
                    @event.RoomCode,
                    @event.StartedByUserId,
                    new WsRoomConnectionDto(_gameWsUrl, gameTicket)
                )
            );

            await hubContext.Clients.Clients(connections.Distinct().ToList()).SendAsync("ReceiveMessage", payload);
        }
    }

    private async Task HandleRoomDissolvedAsync(RoomDissolvedEvent @event)
    {
        using var scope = _serviceProvider.CreateScope();
        var hubContext = scope.ServiceProvider.GetRequiredService<IHubContext<AppHub>>();
        var presenceService = scope.ServiceProvider.GetRequiredService<IPresenceService>();
        var payload = WsMessage<WsRoomDissolvedDto>.Create(
            WsEventType.RoomDissolved,
            new WsRoomDissolvedDto(@event.RoomCode, @event.DissolvedByUserId)
        );

        await BroadcastToUsersAsync(hubContext, presenceService, @event.RecipientIds, payload);
    }

    private async Task HandleRoomHostChangedAsync(RoomHostChangedEvent @event)
    {
        using var scope = _serviceProvider.CreateScope();
        var hubContext = scope.ServiceProvider.GetRequiredService<IHubContext<AppHub>>();
        var presenceService = scope.ServiceProvider.GetRequiredService<IPresenceService>();
        var payload = WsMessage<WsRoomHostChangedDto>.Create(
            WsEventType.RoomHostChanged,
            new WsRoomHostChangedDto(@event.RoomCode, @event.PreviousHostUserId, @event.NewHostUserId)
        );

        await BroadcastToUsersAsync(hubContext, presenceService, @event.RecipientIds, payload);
    }

    private static async Task BroadcastToUsersAsync<T>(
        IHubContext<AppHub> hubContext,
        IPresenceService presenceService,
        List<Guid> userIds,
        WsMessage<T> payload
    )
    {
        if (userIds.Count == 0) return;

        var allConnections = new List<string>();
        foreach (var userId in userIds.Distinct())
        {
            var connections = await presenceService.GetUserConnectionsAsync(userId);
            allConnections.AddRange(connections);
        }

        if (allConnections.Count > 0)
        {
            await hubContext.Clients.Clients(allConnections.Distinct().ToList()).SendAsync("ReceiveMessage", payload);
        }
    }

    private async Task<List<Guid>> GetRoomParticipantIdsAsync(string roomCode)
    {
        var participantsData = await _cache.HashGetAllAsync(CacheKeys.RoomParticipants(roomCode));
        return participantsData
            .Select(entry => Guid.TryParse(entry.Name.ToString(), out var id) ? id : Guid.Empty)
            .Where(id => id != Guid.Empty)
            .ToList();
    }
}
