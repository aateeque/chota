using Chota.Api.Models;

namespace Chota.Api.Services;

public interface IUrlRepository
{
    ValueTask<ShortUrl?> GetByShortCode(string shortCode);

    ValueTask<ShortUrl?> GetByLongUrl(string longUrl);

    ValueTask Save(ShortUrl shortUrl);

    ValueTask Update(ShortUrl shortUrl);
}
