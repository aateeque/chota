using Chota.Api.Models;
using Chota.Api.Services;

namespace Chota.Api.Data;

public sealed class CompositeUrlRepository(InMemoryUrlRepository memoryRepository,
                                           ICacheRepository cacheRepository,
                                           IPostgresUrlRepository postgresRepository,
                                           IIdGeneratorService idGenerator,
                                           ILogger<CompositeUrlRepository> logger) : IUrlRepository
{
    public async Task<ShortUrl?> GetByShortCode(string shortCode)
    {
        logger.LogDebug("Looking up URL by short code: {ShortCode}", shortCode);

        // L1 Cache: Memory
        var result = await memoryRepository.GetByShortCode(shortCode);
        if (result is not null)
        {
            logger.LogDebug("L1 cache hit for short code: {ShortCode}", shortCode);
            return result;
        }

        // L2 Cache: Redis
        result = await cacheRepository.GetByShortCode(shortCode);
        if (result is not null)
        {
            logger.LogDebug("L2 cache hit for short code: {ShortCode}", shortCode);

            // Store in L1 cache for future requests
            await memoryRepository.Save(result);
            return result;
        }

        // L3 Database: PostgreSQL
        result = await postgresRepository.GetByShortCode(shortCode);
        if (result is not null)
        {
            logger.LogDebug("L3 database hit for short code: {ShortCode}", shortCode);

            // Store in both cache layers for future requests
            await cacheRepository.Set(result);
            await memoryRepository.Save(result);
            return result;
        }

        logger.LogDebug("Complete cache miss for short code: {ShortCode}", shortCode);
        return null;
    }

    public async Task<ShortUrl?> GetByLongUrl(string longUrl)
    {
        // L1 Cache: Memory
        var result = await memoryRepository.GetByLongUrl(longUrl);
        if (result is not null)
        {
            logger.LogDebug("L1 cache hit for long URL: {longUrl}", longUrl);
            return result;
        }

        // L2 Cache: Redis
        result = await cacheRepository.GetByLongUrlHash(longUrl);
        if (result is not null)
        {
            logger.LogDebug("L2 cache hit for long URL: {longUrl}", longUrl);

            // Store in L1 cache for future requests
            await memoryRepository.Save(result);
            return result;
        }

        // L3 Database: PostgreSQL
        result = await postgresRepository.GetByLongUrl(longUrl);
        if (result is not null)
        {
            logger.LogDebug("L3 database hit for long URL: {longUrl}", longUrl);

            // Store in both cache layers for future requests
            await cacheRepository.Set(result);
            await memoryRepository.Save(result);
            return result;
        }

        logger.LogDebug("Complete cache miss for long URL");
        return null;
    }

    public async Task Save(ShortUrl shortUrl)
    {
        logger.LogDebug("Saving URL with short code: {ShortCode}", shortUrl.ShortCode);

        var savedId = await postgresRepository.Save(shortUrl);

        // Update cache layers for future reads
        await cacheRepository.Set(shortUrl);
        await memoryRepository.Save(shortUrl);

        logger.LogDebug("Successfully saved URL to database and all cache layers: {ShortCode}", shortUrl.ShortCode);
    }
}
