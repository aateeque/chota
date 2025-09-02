using Chota.Api.Models;

namespace Chota.Api.Data;

public interface IPostgresUrlRepository
{
    Task<ShortUrl?> GetByShortCode(string shortCode);
    Task<ShortUrl?> GetByLongUrl(string longUrl);
    Task<long> Save(ShortUrl shortUrl);
    Task<bool> ExistsById(long id);
    Task<int> GetUrlCount();
    Task<IEnumerable<ShortUrl>> GetRecentUrls(int count = 10);
}