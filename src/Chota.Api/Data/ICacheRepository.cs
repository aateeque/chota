using Chota.Api.Models;

namespace Chota.Api.Data;

public interface ICacheRepository
{
    Task<ShortUrl?> GetByShortCode(string shortCode);
    Task<ShortUrl?> GetByLongUrl(string longUrl);
    Task Set(ShortUrl shortUrl, TimeSpan? expiration = null);
    Task Remove(string key);
    Task<bool> Exists(string key);

    // Enhanced cache invalidation methods
    Task RemoveByShortCode(string shortCode);
    Task RemoveByLongUrl(string longUrl);
    Task RemoveAll(ShortUrl shortUrl);

    // Cache statistics for monitoring
    CacheStatistics GetStatistics();
    void ResetStatistics();
}