using ECommerce.Application.Abstractions;
using StackExchange.Redis;

namespace ECommerce.Infrastructure.Cache;

public class RedisCacheService(IConnectionMultiplexer mux) : ICacheService
{
    private readonly IDatabase _db = mux.GetDatabase();

    public async Task<string?> GetStringAsync(string key)
    {
        var v = await _db.StringGetAsync(key);
        return v.HasValue ? v.ToString() : null;
    }

    public Task SetStringAsync(string key, string value, TimeSpan ttl)
        => _db.StringSetAsync(key, value, ttl);

    public Task RemoveAsync(string key)
        => _db.KeyDeleteAsync(key);
}
