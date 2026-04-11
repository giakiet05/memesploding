using System.Text.Json;
using StackExchange.Redis;

namespace Memesploding.Shared.Infrastructure.Cache;

public class RedisStore : ICacheStore
{
    private readonly IDatabase _db;

    public RedisStore(IConnectionMultiplexer redis)
    {
        _db = redis.GetDatabase();
    }

    public async Task<string?> StringGetAsync(string key)
    {
        var value = await _db.StringGetAsync(key);
        return value.IsNullOrEmpty ? null : value.ToString();
    }

    public async Task<bool> StringSetAsync(string key, string value, TimeSpan? expiry = null)
    {
        if (expiry.HasValue)
        {
            return await _db.StringSetAsync(key, value, expiry.Value);
        }
        return await _db.StringSetAsync(key, value);
    }

    public async Task<long> SetAddAsync(string key, string value)
    {
        return await _db.SetAddAsync(key, value) ? 1 : 0;
    }

    public async Task<long> SetRemoveAsync(string key, string value)
    {
        return await _db.SetRemoveAsync(key, value) ? 1 : 0;
    }

    public async Task<string[]> SetMembersAsync(string key)
    {
        var members = await _db.SetMembersAsync(key);
        return members.Select(m => m.ToString()).ToArray();
    }

    public async Task<long> SetLengthAsync(string key)
    {
        return await _db.SetLengthAsync(key);
    }

    public async Task<bool> HashSetAsync(string key, string field, string value)
    {
        return await _db.HashSetAsync(key, field, value);
    }

    public async Task<bool> HashSetAsync(string key, HashEntry[] entries)
    {
        await _db.HashSetAsync(key, entries);
        return true;
    }

    public async Task<RedisValue> HashGetAsync(string key, string field)
    {
        return await _db.HashGetAsync(key, field);
    }

    public async Task<RedisValue[]> HashKeysAsync(string key)
    {
        return await _db.HashKeysAsync(key);
    }

    public async Task<HashEntry[]> HashGetAllAsync(string key)
    {
        return await _db.HashGetAllAsync(key);
    }

    public async Task<long> HashLengthAsync(string key)
    {
        return await _db.HashLengthAsync(key);
    }

    public async Task<bool> KeyExistsAsync(string key)
    {
        return await _db.KeyExistsAsync(key);
    }

    public async Task<bool> KeyDeleteAsync(string key)
    {
        return await _db.KeyDeleteAsync(key);
    }

    public async Task<bool> KeyExpireAsync(string key, TimeSpan expiry)
    {
        return await _db.KeyExpireAsync(key, expiry);
    }

    public ITransaction CreateTransaction()
    {
        return _db.CreateTransaction();
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
