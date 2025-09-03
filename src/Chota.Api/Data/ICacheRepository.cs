using Chota.Api.Models;

namespace Chota.Api.Data;

public interface ICacheRepository
{
    Task<ShortUrl?> GetByShortCode(string shortCode);
    Task<ShortUrl?> GetByLongUrlHash(string longUrlHash);
    Task Set(ShortUrl shortUrl, TimeSpan? expiration = null);

    // Cache statistics for monitoring
    CacheStatistics GetStatistics();
}