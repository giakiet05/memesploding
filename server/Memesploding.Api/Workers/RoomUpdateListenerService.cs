using StackExchange.Redis;
using System.Text.Json;
using Memesploding.Api.Services;

namespace Memesploding.Api.Workers;

/// <summary>
/// Background service that subscribes to Redis Pub/Sub channel "room:updates"
/// to sync room state changes from Game Server
/// </summary>
public class RoomUpdateListenerService : BackgroundService
{
    private readonly IConnectionMultiplexer _redis;
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly ILogger<RoomUpdateListenerService> _logger;

    public RoomUpdateListenerService(
        IConnectionMultiplexer redis,
        IServiceScopeFactory scopeFactory,
        ILogger<RoomUpdateListenerService> logger)
    {
        _redis = redis;
        _scopeFactory = scopeFactory;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation("RoomUpdateListenerService starting...");

        var subscriber = _redis.GetSubscriber();
        
        await subscriber.SubscribeAsync(RedisChannel.Literal("room:updates"), async (channel, message) =>
        {
            try
            {
                _logger.LogDebug("Received room update: {Message}", message);
                
                var update = JsonSerializer.Deserialize<RoomUpdate>((string)message!);
                if (update == null)
                {
                    _logger.LogWarning("Failed to deserialize room update: {Message}", message);
                    return;
                }

                await HandleRoomUpdateAsync(update);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error handling room update: {Message}", message);
            }
        });

        _logger.LogInformation("Subscribed to Redis channel: room:updates");

        // Keep service running
        await Task.Delay(Timeout.Infinite, stoppingToken);
    }

    private async Task HandleRoomUpdateAsync(RoomUpdate update)
    {
        var db = _redis.GetDatabase();

        // 1. Update room:{code}:info cache
        if (!string.IsNullOrEmpty(update.RoomCode))
        {
            var roomInfoKey = $"room:{update.RoomCode}:info";
            var roomInfo = new
            {
                is_public = update.IsPublic,
                current_players = update.CurrentPlayers,
                max_players = update.MaxPlayers
            };

            await db.StringSetAsync(
                roomInfoKey,
                JsonSerializer.Serialize(roomInfo),
                TimeSpan.FromHours(1) // Cache for 1 hour
            );

            _logger.LogDebug("Updated room info cache: {RoomCode}", update.RoomCode);
        }

        // 2. Update presence for all players in room
        if (update.PlayerIds != null && update.PlayerIds.Count > 0)
        {
            using var scope = _scopeFactory.CreateScope();
            var presenceService = scope.ServiceProvider.GetRequiredService<IPresenceService>();

            var updateTasks = update.PlayerIds.Select(playerId => 
                presenceService.UpdateUserActivityAsync(playerId)
            );

            await Task.WhenAll(updateTasks);

            _logger.LogInformation(
                "Updated presence for {Count} players in room {RoomCode}",
                update.PlayerIds.Count,
                update.RoomCode
            );
        }
    }

    public override async Task StopAsync(CancellationToken cancellationToken)
    {
        _logger.LogInformation("RoomUpdateListenerService stopping...");
        
        var subscriber = _redis.GetSubscriber();
        await subscriber.UnsubscribeAsync(RedisChannel.Literal("room:updates"));
        
        await base.StopAsync(cancellationToken);
    }

    private class RoomUpdate
    {
        public string Type { get; set; } = "";
        public string RoomCode { get; set; } = "";
        public string Status { get; set; } = ""; // "waiting" or "playing"
        public bool IsPublic { get; set; }
        public int CurrentPlayers { get; set; }
        public int MaxPlayers { get; set; }
        public List<Guid> PlayerIds { get; set; } = new();
    }
}
