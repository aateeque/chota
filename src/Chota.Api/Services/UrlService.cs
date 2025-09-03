using Chota.Api.Common;
using Chota.Api.Models;

namespace Chota.Api.Services;

public sealed class UrlService(IUrlRepository urlRepository, IIdGeneratorService idGeneratorService, IUrlEncoder urlEncoder, IUrlValidator urlValidator) : IUrlService
{
    public async Task<Result<ShortUrl>> Shorten(string longUrl)
    {
        if (string.IsNullOrWhiteSpace(longUrl))
            return Error.Validation("Long URL cannot be null or empty.");

        if (!urlValidator.IsValid(longUrl))
            return Error.Validation("Invalid URL format.");

        var hash = idGeneratorService.HashLongUrl(longUrl);

        var existingUrl = await urlRepository.GetByLongUrl(hash);
        if (existingUrl is not null)
            return existingUrl;

        var id = idGeneratorService.GenerateNextId();
        var shortCode = urlEncoder.Encode(id);

        var shortUrl = new ShortUrl(id, longUrl, shortCode, hash, DateTime.UtcNow);

        await urlRepository.Save(shortUrl);

        return shortUrl;
    }

    public async Task<Result<ShortUrl>> GetByShortCode(string shortCode)
    {
        if (string.IsNullOrWhiteSpace(shortCode))
            return Error.Validation("Short code cannot be null or empty.");

        var shortUrl = await urlRepository.GetByShortCode(shortCode);
        if (shortUrl is null)
            return Error.NotFound("Short URL not found.");

        return shortUrl;
    }
}
