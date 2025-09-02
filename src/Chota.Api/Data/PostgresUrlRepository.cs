using Microsoft.EntityFrameworkCore;
using Chota.Api.Models;

namespace Chota.Api.Data;

public sealed class PostgresUrlRepository(UrlDbContext context, ILogger<PostgresUrlRepository> logger) : IPostgresUrlRepository
{
    public async Task<ShortUrl?> GetByShortCode(string shortCode)
    {
        try
        {
            logger.LogDebug("Getting URL by short code from PostgreSQL: {ShortCode}", shortCode);

            var result = await context.ShortUrls
                .AsNoTracking()
                .FirstOrDefaultAsync(u => u.ShortCode == shortCode);

            if (result is not null)
            {
                logger.LogDebug("PostgreSQL hit for short code: {ShortCode}", shortCode);
            }

            return result;
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error retrieving URL by short code from PostgreSQL: {ShortCode}", shortCode);
            throw;
        }
    }

    public async Task<ShortUrl?> GetByLongUrl(string longUrl)
    {
        try
        {
            logger.LogDebug("Getting URL by long URL from PostgreSQL");

            var result = await context.ShortUrls.AsNoTracking()
                                                .FirstOrDefaultAsync(u => u.LongUrl == longUrl);

            if (result is not null)
            {
                logger.LogDebug("Found in PostgreSQL long URL: {longUrl}", longUrl);
            }

            return result;
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error retrieving URL by long URL from PostgreSQL");
            throw;
        }
    }

    public async Task<long> Save(ShortUrl shortUrl)
    {
        try
        {
            context.ShortUrls.Add(shortUrl);
            await context.SaveChangesAsync();

            logger.LogDebug("Successfully saved URL to PostgreSQL: {ShortCode}", shortUrl.ShortCode);
            return shortUrl.Id;
        }
        catch (DbUpdateException ex) when (IsUniqueConstraintViolation(ex))
        {
            logger.LogWarning("Unique constraint violation when saving URL: {ShortCode}", shortUrl.ShortCode);

            // Race condition: another process created the same URL
            var existing = await GetByLongUrl(shortUrl.LongUrl) ?? await GetByShortCode(shortUrl.ShortCode);
            if (existing is not null)
            {
                return existing.Id;
            }

            throw;
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error saving URL to PostgreSQL: {ShortCode}", shortUrl.ShortCode);
            throw;
        }
    }

    public async Task<bool> ExistsById(long id)
    {
        try
        {
            return await context.ShortUrls
                .AsNoTracking()
                .AnyAsync(u => u.Id == id);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error checking if URL exists by ID: {Id}", id);
            throw;
        }
    }

    public async Task<int> GetUrlCount()
    {
        try
        {
            return await context.ShortUrls.CountAsync();
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error getting URL count from PostgreSQL");
            throw;
        }
    }

    public async Task<IEnumerable<ShortUrl>> GetRecentUrls(int count = 10)
    {
        try
        {
            return await context.ShortUrls
                .AsNoTracking()
                .OrderByDescending(u => u.CreatedAt)
                .Take(count)
                .ToListAsync();
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error getting recent URLs from PostgreSQL");
            throw;
        }
    }

    private static bool IsUniqueConstraintViolation(DbUpdateException ex)
    {
        // PostgreSQL unique constraint violation error codes
        return ex.InnerException?.Message.Contains("duplicate key value") == true ||
               ex.InnerException?.Message.Contains("23505") == true;
    }
}