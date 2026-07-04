using StackExchange.Redis;
using System.Text.Json;
using Microsoft.Extensions.Logging;

namespace Memesploding.Shared.Messaging.EventBus;

public class RedisEventBus : IEventBus
{
    private readonly ISubscriber _subscriber;
    private readonly ILogger<RedisEventBus> _logger;

    public RedisEventBus(IConnectionMultiplexer redis, ILogger<RedisEventBus> logger)
    {
        _subscriber = redis.GetSubscriber();
        _logger = logger;
    }

    public async Task PublishAsync<T>(string channel, T @event) where T : class
    {
        var message = JsonSerializer.Serialize(@event);
        // Dùng RedisChannel.Literal để tránh cảnh báo Obsolete
        await _subscriber.PublishAsync(RedisChannel.Literal(channel), message);
    }

    public async Task SubscribeAsync<T>(string channel, Func<T, Task> handler) where T : class
    {
        // Dùng RedisChannel.Literal để tránh cảnh báo Obsolete
        await _subscriber.SubscribeAsync(RedisChannel.Literal(channel), async (redisChannel, message) =>
        {
            try
            {
                if (message.IsNullOrEmpty) return;

                // Ép kiểu message về string tường minh để tránh lỗi Ambiguous call
                var @event = JsonSerializer.Deserialize<T>(message.ToString());
                if (@event != null)
                {
                    await handler(@event);
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Lỗi nghiêm trọng khi chạy handler cho event {Channel}. Lỗi: {Message}", channel, ex.Message);
            }
        });
    }
}
