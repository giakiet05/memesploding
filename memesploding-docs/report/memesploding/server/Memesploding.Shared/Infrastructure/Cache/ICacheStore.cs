using StackExchange.Redis;

namespace Memesploding.Shared.Infrastructure.Cache;

public interface ICacheStore
{
    Task<string?> StringGetAsync(string key);
    Task<bool> StringSetAsync(string key, string value, TimeSpan? expiry = null);
    
    Task<long> SetAddAsync(string key, string value);
    Task<long> SetRemoveAsync(string key, string value);
    Task<string[]> SetMembersAsync(string key);
    Task<long> SetLengthAsync(string key);
    
    Task<bool> HashSetAsync(string key, string field, string value);
    Task<bool> HashSetAsync(string key, HashEntry[] entries);
    Task<RedisValue> HashGetAsync(string key, string field); // Thêm mới
    Task<RedisValue[]> HashKeysAsync(string key); // Thêm mới
    Task<HashEntry[]> HashGetAllAsync(string key);
    Task<long> HashLengthAsync(string key);
    
    Task<bool> KeyExistsAsync(string key);
    Task<bool> KeyDeleteAsync(string key);
    Task<bool> KeyExpireAsync(string key, TimeSpan expiry);
    
    ITransaction CreateTransaction();
    
    Task SetAsync<T>(string key, T data, TimeSpan? expiry = null);
    Task<T?> GetAsync<T>(string key);
    Task RemoveAsync(string key);
}
