using Chota.Api.Models;

namespace Chota.Api.Services;

public interface IUrlRepository
{
    Task<ShortUrl?> GetByShortCode(string shortCode);

    Task<ShortUrl?> GetByLongUrl(string longUrl);

    Task Save(ShortUrl shortUrl);
}
