namespace Memesploding.Shared.Infrastructure.Redis;

public interface ICacheStore
{
    Task SetAsync<T>(string key, T data, TimeSpan? expiry = null);
    Task<T?> GetAsync<T>(string key);
    Task RemoveAsync(string key);
}