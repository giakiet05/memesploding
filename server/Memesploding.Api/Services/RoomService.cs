using Memesploding.Api.Data;
using Memesploding.Api.DTOs;
using Memesploding.Api.Exceptions;
using Memesploding.Api.Messaging.Events;
using Memesploding.Shared.Enums;
using Memesploding.Api.Infrastructure.Cache;
using Memesploding.Api.Messaging.Channels;
using Memesploding.Shared.Auth;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using StackExchange.Redis;
using System.Text.Json;
using Memesploding.Shared.Infrastructure.Cache;
using Memesploding.Shared.Messaging.EventBus;
using Memesploding.Shared.Messaging.Integration.Channels;
using Memesploding.Shared.Messaging.Integration.Events;

namespace Memesploding.Api.Services;

public class RoomService(
    ICacheStore cache,
    ApplicationDbContext db,
    IConfiguration config,
    IEventBus eventBus,
    ITokenService tokenService
) : IRoomService
{
    private static readonly Random _random = new();
    private const int RoomCodeLength = 6;
    private const string RoomCodeChars = "ABCDEFGHJKLMNPQRSTUVWXYZ23456789";
    private static readonly JsonSerializerOptions _jsonOptions = new() { PropertyNamingPolicy = JsonNamingPolicy.CamelCase };
    private static readonly JsonSerializerOptions _deserializeOptions = new() { PropertyNameCaseInsensitive = true };

    public async Task<RoomDetailDto> CreateRoomAsync(Guid hostId, CreateRoomDto dto)
    {
        var cardSetIds = dto.CardSetIds.Distinct().ToList();
        var cardSets = await db.CardSets
            .Where(cs => cardSetIds.Contains(cs.Id) && cs.IsActive)
            .Select(cs => new { cs.Id, cs.Name })
            .ToListAsync();

        if (cardSets.Count != cardSetIds.Count)
            throw AppException.BadRequest(ErrorCode.ValidationFailed, "One or more card sets are invalid or inactive");

        var host = await db.Users.FindAsync(hostId);
        if (host == null) throw AppException.NotFound("User not found");

        var existingRoomCode = await cache.StringGetAsync(CacheKeys.UserInRoom(hostId));
        if (!string.IsNullOrEmpty(existingRoomCode))
            throw AppException.BadRequest(ErrorCode.ValidationFailed, "You are already in a room");

        var roomCode = await GenerateUniqueRoomCodeAsync();

        var roomInfoKey = CacheKeys.RoomInfo(roomCode);
        var participantsKey = CacheKeys.RoomParticipants(roomCode);
        var cardSetsKey = CacheKeys.RoomCardSets(roomCode);

        var participantData = new
        {
            userId = hostId,
            nickname = host.Username,
            avatarUrl = host.AvatarUrl ?? "",
            role = "host",
            isReady = true
        };

        var transaction = cache.CreateTransaction();
        
        _ = transaction.HashSetAsync(roomInfoKey, new HashEntry[]
        {
            new("host_id", hostId.ToString()),
            new("status", "waiting"),
            new("is_public", dto.IsPublic.ToString().ToLower()),
            new("max_players", dto.MaxPlayers.ToString()),
            new("created_at", DateTime.UtcNow.ToString("O"))
        });

        _ = transaction.HashSetAsync(participantsKey, hostId.ToString(), JsonSerializer.Serialize(participantData, _jsonOptions));

        foreach (var csId in cardSetIds)
            _ = transaction.SetAddAsync(cardSetsKey, csId.ToString());

        if (dto.IsPublic)
            _ = transaction.SetAddAsync(CacheKeys.PublicRooms(), roomCode);

        _ = transaction.StringSetAsync(CacheKeys.UserInRoom(hostId), roomCode);

        await transaction.ExecuteAsync();

        return await GetRoomByCodeAsync(roomCode, hostId);
    }

    public async Task<RoomDetailDto> JoinRoomAsync(Guid userId, string roomCode)
    {
        roomCode = roomCode.ToUpper();

        var existingRoom = await cache.StringGetAsync(CacheKeys.UserInRoom(userId));
        if (!string.IsNullOrEmpty(existingRoom))
        {
            if (existingRoom == roomCode) return await GetRoomByCodeAsync(roomCode, userId);
            throw AppException.BadRequest(ErrorCode.ValidationFailed, "You are already in another room");
        }

        var roomInfoKey = CacheKeys.RoomInfo(roomCode);
        var roomData = await cache.HashGetAllAsync(roomInfoKey);
        if (roomData.Length == 0) throw AppException.NotFound("Room not found");

        var roomDict = roomData.ToDictionary(x => x.Name.ToString(), x => x.Value.ToString());
        if (roomDict.GetValueOrDefault("status") != "waiting")
            throw AppException.BadRequest(ErrorCode.MatchAlreadyStarted, "Cannot join room after match started");

        var participantsKey = CacheKeys.RoomParticipants(roomCode);
        var currentCount = await cache.HashLengthAsync(participantsKey);
        var maxPlayers = int.Parse(roomDict["max_players"]);

        if (currentCount >= maxPlayers) throw AppException.BadRequest(ErrorCode.ValidationFailed, "Room is full");

        var user = await db.Users.FindAsync(userId);
        if (user == null) throw AppException.NotFound("User not found");

        var participantData = new
        {
            userId = userId,
            nickname = user.Username,
            avatarUrl = user.AvatarUrl ?? "",
            role = "player",
            isReady = false
        };

        var transaction = cache.CreateTransaction();
        _ = transaction.HashSetAsync(participantsKey, userId.ToString(), JsonSerializer.Serialize(participantData, _jsonOptions));
        _ = transaction.StringSetAsync(CacheKeys.UserInRoom(userId), roomCode);
        
        await transaction.ExecuteAsync();

        var recipients = await GetParticipantIdsAsync(roomCode);
        await eventBus.PublishAsync(
            EventChannels.RoomMemberJoined,
            new RoomMemberJoinedEvent(
                roomCode,
                userId,
                user.Username,
                user.AvatarUrl ?? "",
                "player",
                false,
                recipients
            )
        );

        return await GetRoomByCodeAsync(roomCode, userId);
    }

    public async Task LeaveRoomAsync(Guid userId, string roomCode)
    {
        roomCode = roomCode.ToUpper();
        await EnsureUserInRoomAsync(userId, roomCode);

        var participantsKey = CacheKeys.RoomParticipants(roomCode);
        var roomInfo = await GetRoomInfoDictAsync(roomCode);
        var hostId = Guid.Parse(roomInfo["host_id"]);

        if (hostId == userId)
        {
            await RemoveParticipantAsync(roomCode, userId, participantsKey);

            var remainingEntries = await cache.HashGetAllAsync(participantsKey);
            if (remainingEntries.Length == 0)
            {
                await DissolveRoomInternalAsync(roomCode, hostId);
                return;
            }

            var remainingIds = ParseParticipantIds(remainingEntries);
            var newHostId = remainingIds[_random.Next(remainingIds.Count)];
            await PromoteHostAsync(roomCode, participantsKey, newHostId);

            var leftRecipients = remainingIds.Append(userId).Distinct().ToList();
            await eventBus.PublishAsync(
                EventChannels.RoomMemberLeft,
                new RoomMemberLeftEvent(roomCode, userId, leftRecipients)
            );

            await eventBus.PublishAsync(
                EventChannels.RoomHostChanged,
                new RoomHostChangedEvent(roomCode, userId, newHostId, remainingIds)
            );
            return;
        }

        await RemoveParticipantAsync(roomCode, userId, participantsKey);

        var remainingParticipants = await cache.HashLengthAsync(participantsKey);
        var recipients = await GetParticipantIdsAsync(roomCode);
        recipients.Add(userId);
        await eventBus.PublishAsync(
            EventChannels.RoomMemberLeft,
            new RoomMemberLeftEvent(roomCode, userId, recipients.Distinct().ToList())
        );

        if (remainingParticipants == 0)
        {
            await DissolveRoomInternalAsync(roomCode, hostId);
        }
    }

    public async Task<RoomDetailDto> UpdateReadyStatusAsync(Guid userId, string roomCode, bool isReady)
    {
        roomCode = roomCode.ToUpper();
        await EnsureUserInRoomAsync(userId, roomCode);

        var roomInfo = await GetRoomInfoDictAsync(roomCode);
        if (roomInfo.GetValueOrDefault("status") != "waiting")
            throw AppException.BadRequest(ErrorCode.MatchAlreadyStarted, "Cannot change ready status while match is running");

        var participantsKey = CacheKeys.RoomParticipants(roomCode);
        var participantValue = await cache.HashGetAsync(participantsKey, userId.ToString());
        if (participantValue.IsNullOrEmpty)
            throw AppException.BadRequest(ErrorCode.NotInRoom, "You are not in this room");

        var participant = JsonSerializer.Deserialize<InternalParticipant>(participantValue.ToString(), _deserializeOptions);
        if (participant == null)
            throw AppException.BadRequest(ErrorCode.ValidationFailed, "Invalid participant data");

        var updatedParticipant = participant with { IsReady = isReady };
        await cache.HashSetAsync(participantsKey, userId.ToString(), JsonSerializer.Serialize(updatedParticipant, _jsonOptions));
        var recipients = await GetParticipantIdsAsync(roomCode);
        await eventBus.PublishAsync(
            EventChannels.RoomReadyStatusChanged,
            new RoomReadyStatusChangedEvent(roomCode, userId, isReady, recipients)
        );

        return await GetRoomByCodeAsync(roomCode, userId);
    }

    public async Task KickPlayerAsync(Guid hostId, string roomCode, Guid targetUserId)
    {
        roomCode = roomCode.ToUpper();
        await EnsureUserInRoomAsync(hostId, roomCode);

        if (hostId == targetUserId)
            throw AppException.BadRequest(ErrorCode.ValidationFailed, "Host cannot kick yourself");

        var roomInfo = await GetRoomInfoDictAsync(roomCode);
        var expectedHostId = Guid.Parse(roomInfo["host_id"]);
        if (expectedHostId != hostId)
            throw AppException.BadRequest(ErrorCode.NotRoomHost, "Only host can kick players");

        if (roomInfo.GetValueOrDefault("status") != "waiting")
            throw AppException.BadRequest(ErrorCode.MatchAlreadyStarted, "Cannot kick players while match is running");

        var participantsKey = CacheKeys.RoomParticipants(roomCode);
        var targetParticipant = await cache.HashGetAsync(participantsKey, targetUserId.ToString());
        if (targetParticipant.IsNullOrEmpty)
            throw AppException.BadRequest(ErrorCode.NotInRoom, "Target player is not in room");

        var transaction = cache.CreateTransaction();
        _ = transaction.HashDeleteAsync(participantsKey, targetUserId.ToString());
        _ = transaction.KeyDeleteAsync(CacheKeys.UserInRoom(targetUserId));
        await transaction.ExecuteAsync();

        var recipients = await GetParticipantIdsAsync(roomCode);
        recipients.Add(targetUserId);
        await eventBus.PublishAsync(
            EventChannels.RoomMemberKicked,
            new RoomMemberKickedEvent(roomCode, targetUserId, hostId, recipients.Distinct().ToList())
        );
    }

    public async Task<RoomDetailDto> StartMatchAsync(Guid hostId, string roomCode)
    {
        roomCode = roomCode.ToUpper();
        await EnsureUserInRoomAsync(hostId, roomCode);

        var roomInfo = await GetRoomInfoDictAsync(roomCode);
        var expectedHostId = Guid.Parse(roomInfo["host_id"]);
        if (expectedHostId != hostId)
            throw AppException.BadRequest(ErrorCode.NotRoomHost, "Only host can start match");

        var status = roomInfo.GetValueOrDefault("status", "waiting");
        if (status != "waiting")
            throw AppException.BadRequest(ErrorCode.MatchAlreadyStarted, "Match already started");

        var participantsData = await cache.HashGetAllAsync(CacheKeys.RoomParticipants(roomCode));
        if (participantsData.Length < 2)
            throw AppException.BadRequest(ErrorCode.ValidationFailed, "Need at least 2 players to start match");

        var allReady = participantsData.All(entry =>
        {
            var participant = JsonSerializer.Deserialize<InternalParticipant>(entry.Value.ToString(), _deserializeOptions);
            return participant?.IsReady == true;
        });
        if (!allReady)
            throw AppException.BadRequest(ErrorCode.ValidationFailed, "All players must be ready before starting");

        await cache.HashSetAsync(CacheKeys.RoomInfo(roomCode), "status", "playing");
        var recipients = await GetParticipantIdsAsync(roomCode);

        var matchId = Guid.NewGuid();
        var turnTimer = 0; // Let Game Server handle the default turn timer
        var cardSetIds = (await cache.SetMembersAsync(CacheKeys.RoomCardSets(roomCode)))
            .Select(x => Guid.TryParse(x, out var id) ? id : Guid.Empty)
            .Where(id => id != Guid.Empty)
            .ToList();

        var playerInfos = new List<IntegrationPlayerInfo>();
        foreach (var entry in participantsData)
        {
            var participant = JsonSerializer.Deserialize<InternalParticipant>(entry.Value.ToString(), _deserializeOptions);
            if (participant != null)
            {
                playerInfos.Add(new IntegrationPlayerInfo(
                    participant.UserId,
                    participant.Nickname,
                    participant.AvatarUrl ?? "",
                    participant.UserId == expectedHostId ? "host" : "player"
                ));
            }
        }

        await eventBus.PublishAsync(
            GameIntegrationChannels.StartMatchRequested,
            new StartMatchRequestedEvent(
                matchId,
                roomCode,
                hostId,
                turnTimer,
                cardSetIds,
                playerInfos,
                DateTime.UtcNow
            )
        );

        await eventBus.PublishAsync(
            EventChannels.RoomMatchStarting,
            new RoomMatchStartingEvent(roomCode, hostId, recipients)
        );
        return await GetRoomByCodeAsync(roomCode, hostId);
    }

    public async Task<ListResponseData<RoomSummaryDto>> GetPublicRoomsAsync(RoomQueryDto query)
    {
        var publicRoomCodes = await cache.SetMembersAsync(CacheKeys.PublicRooms());
        if (publicRoomCodes.Length == 0)
            return new ListResponseData<RoomSummaryDto>(new List<RoomSummaryDto>(), new PaginationMeta(query.Page, query.PageSize, 0, false));

        var rooms = new List<RoomSummaryDto>();
        foreach (var code in publicRoomCodes)
        {
            var roomInfoKey = CacheKeys.RoomInfo(code);
            var roomData = await cache.HashGetAllAsync(roomInfoKey);
            if (roomData.Length == 0) continue;

            var roomDict = roomData.ToDictionary(x => x.Name.ToString(), x => x.Value.ToString());
            if (roomDict.GetValueOrDefault("status") != "waiting") continue;

            var participantCount = await cache.HashLengthAsync(CacheKeys.RoomParticipants(code));
            var maxPlayers = int.Parse(roomDict.GetValueOrDefault("max_players", "6"));
            if (query.MaxPlayers?.Count > 0 && !query.MaxPlayers.Contains(maxPlayers)) continue;

            var cardSetIds = await cache.SetMembersAsync(CacheKeys.RoomCardSets(code));
            var cardSetGuids = cardSetIds.Select(x => Guid.Parse(x)).ToList();
            if (query.CardSetIds?.Count > 0 && !query.CardSetIds.Any(id => cardSetGuids.Contains(id))) continue;

            var cardSets = await db.CardSets.Where(cs => cardSetGuids.Contains(cs.Id)).Select(cs => new CardSetInfoDto(cs.Id, cs.Name)).ToListAsync();
            var hostId = Guid.Parse(roomDict["host_id"]);
            var hostNickname = await db.Users.Where(u => u.Id == hostId).Select(u => u.Username).FirstOrDefaultAsync() ?? "Unknown";

            rooms.Add(new RoomSummaryDto(code, hostId, hostNickname, maxPlayers, (int)participantCount, roomDict["status"], cardSets, bool.Parse(roomDict.GetValueOrDefault("is_public", "false")), DateTime.Parse(roomDict.GetValueOrDefault("created_at", DateTime.UtcNow.ToString("O")))));
        }

        var totalCount = rooms.Count;
        var paginatedRooms = rooms.OrderByDescending(r => r.CreatedAt).Skip((query.Page - 1) * query.PageSize).Take(query.PageSize).ToList();
        return new ListResponseData<RoomSummaryDto>(paginatedRooms, new PaginationMeta(query.Page, query.PageSize, totalCount, totalCount > query.Page * query.PageSize));
    }

    public async Task<RoomDetailDto> GetRoomByCodeAsync(string code, Guid? requesterUserId = null)
    {
        code = code.ToUpper();
        var roomInfoKey = CacheKeys.RoomInfo(code);
        var roomData = await cache.HashGetAllAsync(roomInfoKey);
        if (roomData.Length == 0) throw AppException.NotFound("Room not found");

        var roomDict = roomData.ToDictionary(x => x.Name.ToString(), x => x.Value.ToString());
        var participantsData = await cache.HashGetAllAsync(CacheKeys.RoomParticipants(code));
        
        var participants = participantsData.Select(entry => {
            var p = JsonSerializer.Deserialize<JsonElement>(entry.Value.ToString());
            return new RoomParticipantDto(
                UserId: Guid.Parse(GetJsonProp(p, "userId", "user_id", "UserId")),
                Nickname: GetJsonProp(p, "nickname", null, "Nickname"),
                AvatarUrl: GetJsonProp(p, "avatarUrl", "avatar_url", "AvatarUrl"),
                Role: GetJsonProp(p, "role", null, "Role"),
                IsReady: bool.Parse(GetJsonProp(p, "isReady", "is_ready", "IsReady"))
            );
        }).ToList();

        var cardSetIds = await cache.SetMembersAsync(CacheKeys.RoomCardSets(code));
        var cardSetGuids = cardSetIds.Select(x => Guid.Parse(x)).ToList();
        var cardSets = await db.CardSets.Where(cs => cardSetGuids.Contains(cs.Id)).Select(cs => new CardSetInfoDto(cs.Id, cs.Name)).ToListAsync();

        return new RoomDetailDto(
            code,
            Guid.Parse(roomDict["host_id"]),
            roomDict["status"],
            bool.Parse(roomDict["is_public"]),
            new RoomSettingsDto(int.Parse(roomDict["max_players"]), int.Parse(roomDict.GetValueOrDefault("turn_timer", "0"))),
            cardSets,
            participants,
            await BuildRoomConnectionAsync(code, roomDict["status"], requesterUserId)
        );
    }

    private async Task<RoomConnectionDto> BuildRoomConnectionAsync(string roomCode, string roomStatus, Guid? requesterUserId)
    {
        var wsUrl = config["Realtime:GameWsUrl"] ?? "ws://localhost:5217/ws";

        if (requesterUserId == null || !string.Equals(roomStatus, "playing", StringComparison.OrdinalIgnoreCase))
            return new RoomConnectionDto(wsUrl, string.Empty);

        var currentRoom = await cache.StringGetAsync(CacheKeys.UserInRoom(requesterUserId.Value));
        if (!string.Equals(currentRoom, roomCode, StringComparison.OrdinalIgnoreCase))
            return new RoomConnectionDto(wsUrl, string.Empty);

        var gameTicket = tokenService.GenerateGameTicket(requesterUserId.Value, roomCode);
        return new RoomConnectionDto(wsUrl, gameTicket);
    }

    private async Task EnsureUserInRoomAsync(Guid userId, string roomCode)
    {
        var currentRoom = await cache.StringGetAsync(CacheKeys.UserInRoom(userId));
        if (string.IsNullOrEmpty(currentRoom) || !string.Equals(currentRoom, roomCode, StringComparison.OrdinalIgnoreCase))
            throw AppException.BadRequest(ErrorCode.NotInRoom, "You are not in this room");
    }

    private async Task<Dictionary<string, string>> GetRoomInfoDictAsync(string roomCode)
    {
        var roomInfoData = await cache.HashGetAllAsync(CacheKeys.RoomInfo(roomCode));
        if (roomInfoData.Length == 0)
            throw AppException.NotFound("Room not found");

        return roomInfoData.ToDictionary(x => x.Name.ToString(), x => x.Value.ToString());
    }

    private async Task DissolveRoomInternalAsync(string roomCode, Guid dissolvedByUserId)
    {
        var participantsKey = CacheKeys.RoomParticipants(roomCode);
        var participants = await cache.HashGetAllAsync(participantsKey);
        var recipientIds = participants
            .Select(p => Guid.TryParse(p.Name.ToString(), out var userId) ? userId : Guid.Empty)
            .Where(id => id != Guid.Empty)
            .ToList();

        var transaction = cache.CreateTransaction();
        foreach (var participant in participants)
        {
            if (Guid.TryParse(participant.Name.ToString(), out var userId))
            {
                _ = transaction.KeyDeleteAsync(CacheKeys.UserInRoom(userId));
            }
        }

        _ = transaction.KeyDeleteAsync(CacheKeys.RoomInfo(roomCode));
        _ = transaction.KeyDeleteAsync(participantsKey);
        _ = transaction.KeyDeleteAsync(CacheKeys.RoomCardSets(roomCode));
        _ = transaction.SetRemoveAsync(CacheKeys.PublicRooms(), roomCode);
        await transaction.ExecuteAsync();

        if (recipientIds.Count > 0)
        {
            await eventBus.PublishAsync(
                EventChannels.RoomDissolved,
                new RoomDissolvedEvent(roomCode, dissolvedByUserId, recipientIds)
            );
        }
    }

    private async Task RemoveParticipantAsync(string roomCode, Guid userId, string participantsKey)
    {
        var transaction = cache.CreateTransaction();
        _ = transaction.HashDeleteAsync(participantsKey, userId.ToString());
        _ = transaction.KeyDeleteAsync(CacheKeys.UserInRoom(userId));
        _ = transaction.KeyDeleteAsync(CacheKeys.RoomReconnectGrace(userId));
        _ = transaction.SetRemoveAsync(CacheKeys.RoomReconnectGraceUsers(), userId.ToString());
        await transaction.ExecuteAsync();
    }

    private async Task PromoteHostAsync(string roomCode, string participantsKey, Guid newHostId)
    {
        var participantValue = await cache.HashGetAsync(participantsKey, newHostId.ToString());
        if (participantValue.IsNullOrEmpty)
            throw AppException.NotFound("New host participant not found");

        var participant = JsonSerializer.Deserialize<InternalParticipant>(participantValue.ToString(), _deserializeOptions);
        if (participant == null)
            throw AppException.BadRequest(ErrorCode.ValidationFailed, "Invalid participant data");

        var updated = participant with { Role = "host" };
        var transaction = cache.CreateTransaction();
        _ = transaction.HashSetAsync(CacheKeys.RoomInfo(roomCode), "host_id", newHostId.ToString());
        _ = transaction.HashSetAsync(participantsKey, newHostId.ToString(), JsonSerializer.Serialize(updated, _jsonOptions));
        await transaction.ExecuteAsync();
    }

    private async Task<List<Guid>> GetParticipantIdsAsync(string roomCode)
    {
        var keys = await cache.HashKeysAsync(CacheKeys.RoomParticipants(roomCode));
        return keys
            .Select(k => Guid.TryParse(k.ToString(), out var userId) ? userId : Guid.Empty)
            .Where(id => id != Guid.Empty)
            .ToList();
    }

    private static List<Guid> ParseParticipantIds(IEnumerable<HashEntry> participants)
    {
        return participants
            .Select(p => Guid.TryParse(p.Name.ToString(), out var userId) ? userId : Guid.Empty)
            .Where(id => id != Guid.Empty)
            .ToList();
    }

    private string GetJsonProp(JsonElement el, string camel, string? snake = null, string? pascal = null)
    {
        if (el.TryGetProperty(camel, out var val)) return val.ToString();
        if (pascal != null && el.TryGetProperty(pascal, out var val3)) return val3.ToString();
        if (snake != null && el.TryGetProperty(snake, out var val2)) return val2.ToString();
        return "";
    }

    private async Task<string> GenerateUniqueRoomCodeAsync()
    {
        for (int attempt = 0; attempt < 10; attempt++)
        {
            var code = GenerateRoomCode();
            if (!await cache.KeyExistsAsync(CacheKeys.RoomInfo(code))) return code;
        }
        throw new InvalidOperationException("Failed to generate unique room code after 10 attempts");
    }

    private static string GenerateRoomCode()
    {
        var chars = new char[RoomCodeLength];
        for (int i = 0; i < RoomCodeLength; i++) chars[i] = RoomCodeChars[_random.Next(RoomCodeChars.Length)];
        return new string(chars);
    }

    private record InternalParticipant(
        Guid UserId,
        string Nickname,
        string AvatarUrl,
        string Role,
        bool IsReady
    );
}
