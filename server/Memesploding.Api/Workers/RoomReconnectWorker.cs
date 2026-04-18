using Memesploding.Api.Exceptions;
using Memesploding.Api.Services;
using Memesploding.Shared.Enums;
using Memesploding.Shared.Infrastructure.Cache;
using Microsoft.Extensions.Configuration;

namespace Memesploding.Api.Workers;

public class RoomReconnectWorker : BackgroundService
{
    private readonly IServiceProvider _serviceProvider;
    private readonly ICacheStore _cache;
    private readonly ILogger<RoomReconnectWorker> _logger;
    private readonly int _graceSeconds;

    public RoomReconnectWorker(
        IServiceProvider serviceProvider,
        ICacheStore cache,
        IConfiguration config,
        ILogger<RoomReconnectWorker> logger)
    {
        _serviceProvider = serviceProvider;
        _cache = cache;
        _logger = logger;
        _graceSeconds = Math.Max(10, config.GetValue<int?>("Realtime:ReconnectGraceSeconds") ?? 120);
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation("RoomReconnectWorker is starting with grace {GraceSeconds}s", _graceSeconds);

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                await ProcessGraceTimeoutsAsync(stoppingToken);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Unhandled error in RoomReconnectWorker loop");
            }

            try
            {
                await Task.Delay(TimeSpan.FromSeconds(5), stoppingToken);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                break;
            }
        }
    }

    private async Task ProcessGraceTimeoutsAsync(CancellationToken cancellationToken)
    {
        var userIds = await _cache.SetMembersAsync(CacheKeys.RoomReconnectGraceUsers());
        if (userIds.Length == 0)
        {
            return;
        }

        using var scope = _serviceProvider.CreateScope();
        var presenceService = scope.ServiceProvider.GetRequiredService<IPresenceService>();
        var roomService = scope.ServiceProvider.GetRequiredService<IRoomService>();

        foreach (var userIdRaw in userIds)
        {
            cancellationToken.ThrowIfCancellationRequested();

            if (!Guid.TryParse(userIdRaw, out var userId))
            {
                await _cache.SetRemoveAsync(CacheKeys.RoomReconnectGraceUsers(), userIdRaw);
                continue;
            }

            var key = CacheKeys.RoomReconnectGrace(userId);
            var graceData = await _cache.GetAsync<RoomReconnectGraceData>(key);
            if (graceData == null)
            {
                _logger.LogDebug("Grace data missing for user {UserId}, removing from tracking set", userId);
                await _cache.SetRemoveAsync(CacheKeys.RoomReconnectGraceUsers(), userId.ToString());
                continue;
            }

            if (graceData.DisconnectedAt.AddSeconds(_graceSeconds) > DateTime.UtcNow)
            {
                continue;
            }

            var connections = await presenceService.GetUserConnectionsAsync(userId);
            if (connections.Count > 0)
            {
                await ClearGraceAsync(userId);
                continue;
            }

            var currentRoomCode = await _cache.StringGetAsync(CacheKeys.UserInRoom(userId));
            if (string.IsNullOrEmpty(currentRoomCode) || !string.Equals(currentRoomCode, graceData.RoomCode, StringComparison.OrdinalIgnoreCase))
            {
                await ClearGraceAsync(userId);
                continue;
            }

            try
            {
                await roomService.LeaveRoomAsync(userId, graceData.RoomCode);
                _logger.LogInformation("Auto-removed disconnected user {UserId} from room {RoomCode}", userId, graceData.RoomCode);
            }
            catch (AppException ex) when (ex.ErrorCode == ErrorCode.NotInRoom || ex.ErrorCode == ErrorCode.NotFound)
            {
                _logger.LogDebug("Skipping stale grace cleanup for user {UserId}: {ErrorCode}", userId, ex.ErrorCode);
            }
            finally
            {
                await ClearGraceAsync(userId);
            }
        }
    }

    private async Task ClearGraceAsync(Guid userId)
    {
        await _cache.KeyDeleteAsync(CacheKeys.RoomReconnectGrace(userId));
        await _cache.SetRemoveAsync(CacheKeys.RoomReconnectGraceUsers(), userId.ToString());
    }

    private class RoomReconnectGraceData
    {
        public string RoomCode { get; set; } = "";
        public DateTime DisconnectedAt { get; set; }
    }
}
