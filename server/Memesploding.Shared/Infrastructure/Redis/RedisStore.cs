using System.Text.Json;
using StackExchange.Redis;

namespace Memesploding.Shared.Infrastructure.Redis;

public interface IRedisStore
{
    Task SetAsync<T>(string key, T data, TimeSpan? expiry = null);
    Task<T?> GetAsync<T>(string key);
    Task RemoveAsync(string key);
}

public class RedisStore : IRedisStore
{
    private readonly IDatabase _db;

    public RedisStore(IConnectionMultiplexer redis)
    {
        _db = redis.GetDatabase();
    }

    public async Task SetAsync<T>(string key, T data, TimeSpan? expiry = null)
    {
        var jsonData = JsonSerializer.Serialize(data);
        if (expiry.HasValue)
        {
            await _db.StringSetAsync(key, jsonData, expiry.Value);
        }
        else
        {
            await _db.StringSetAsync(key, jsonData);
        }
    }

    public async Task<T?> GetAsync<T>(string key)
    {
        var jsonData = await _db.StringGetAsync(key);
        if (jsonData.IsNullOrEmpty)
        {
            return default;
        }

        return JsonSerializer.Deserialize<T>(jsonData.ToString());
    }

    public async Task RemoveAsync(string key)
    {
        await _db.KeyDeleteAsync(key);
    }
}
