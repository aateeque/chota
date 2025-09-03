using Microsoft.Extensions.Caching.Distributed;
using Microsoft.Extensions.Options;
using System.Diagnostics;
using System.Text.Json;
using Chota.Api.Models;

namespace Chota.Api.Data;

public sealed class RedisUrlRepository(IDistributedCache cache, ILogger<RedisUrlRepository> logger, IOptions<RedisUrlRepositoryOptions>? options = null) : ICacheRepository
{
    // Cache key constants for consistency and performance
    private const string CacheNamespace = "urlshort";
    private const string ShortCodePrefix = "sc";
    private const string LongUrlPrefix = "lu";

    private readonly IDistributedCache _cache = cache ?? throw new ArgumentNullException(nameof(cache));
    private readonly ILogger<RedisUrlRepository> _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    private readonly RedisUrlRepositoryOptions _options = options?.Value ?? new RedisUrlRepositoryOptions();
    private readonly ActivitySource _activitySource = new("Chota.RedisCache");

    // Cache statistics
    private long _cacheHits;
    private long _cacheMisses;
    private long _cacheErrors;

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        WriteIndented = false,
        DefaultIgnoreCondition = System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull
    };

    public async Task<ShortUrl?> GetByShortCode(string shortCode)
    {
        if (string.IsNullOrWhiteSpace(shortCode))
        {
            _logger.LogWarning("GetByShortCode called with null or empty shortCode");
            return null;
        }

        using var activity = _activitySource.StartActivity();
        activity?.SetTag("cache.operation", "get");
        activity?.SetTag("cache.key_type", "short_code");

        var stopwatch = Stopwatch.StartNew();

        try
        {
            var key = GetShortCodeKey(shortCode);
            var json = await _cache.GetStringAsync(key);

            stopwatch.Stop();

            if (json is null)
            {
                Interlocked.Increment(ref _cacheMisses);
                activity?.SetTag("cache.hit", false);
                _logger.LogDebug("Cache miss for short code: {ShortCode} (took {ElapsedMs}ms)", shortCode, stopwatch.ElapsedMilliseconds);
                return null;
            }

            var shortUrl = JsonSerializer.Deserialize<ShortUrl>(json, JsonOptions);
            Interlocked.Increment(ref _cacheHits);
            activity?.SetTag("cache.hit", true);

            _logger.LogDebug("Cache hit for short code: {ShortCode} (took {ElapsedMs}ms)", shortCode, stopwatch.ElapsedMilliseconds);

            return shortUrl;
        }
        catch (Exception ex)
        {
            Interlocked.Increment(ref _cacheErrors);
            activity?.SetTag("cache.error", true);
            activity?.SetTag("cache.error.message", ex.Message);

            _logger.LogWarning(ex, "Failed to get URL by short code from cache: {ShortCode} (took {ElapsedMs}ms)", shortCode, stopwatch.ElapsedMilliseconds);
            return null;
        }
    }

    public async Task<ShortUrl?> GetByLongUrlHash(string longUrlHash)
    {
        if (string.IsNullOrWhiteSpace(longUrlHash))
        {
            _logger.LogWarning("GetByLongUrlHash called with null or empty longUrlHash");
            return null;
        }

        using var activity = _activitySource.StartActivity(ActivityKind.Client);
        activity?.SetTag("cache.operation", "get");
        activity?.SetTag("cache.key_type", "long_url");

        var stopwatch = Stopwatch.StartNew();

        try
        {
            var key = GetLongUrlKey(longUrlHash);
            var json = await _cache.GetStringAsync(key);

            stopwatch.Stop();

            if (json is null)
            {
                Interlocked.Increment(ref _cacheMisses);
                activity?.SetTag("cache.hit", false);
                _logger.LogDebug("Cache miss for long URL hash (took {ElapsedMs}ms)", stopwatch.ElapsedMilliseconds);
                return null;
            }

            var shortUrl = JsonSerializer.Deserialize<ShortUrl>(json, JsonOptions);
            Interlocked.Increment(ref _cacheHits);
            activity?.SetTag("cache.hit", true);

            _logger.LogDebug("Cache hit for long URL hash (took {ElapsedMs}ms)", stopwatch.ElapsedMilliseconds);
            return shortUrl;
        }
        catch (Exception ex)
        {
            Interlocked.Increment(ref _cacheErrors);
            activity?.SetTag("cache.error", true);
            activity?.SetTag("cache.error.message", ex.Message);

            _logger.LogWarning(ex, "Failed to get URL by long URL from cache (took {ElapsedMs}ms)", stopwatch.ElapsedMilliseconds);
            return null;
        }
    }

    public async Task Set(ShortUrl shortUrl, TimeSpan? expiration = null)
    {
        if (string.IsNullOrWhiteSpace(shortUrl.ShortCode) || string.IsNullOrWhiteSpace(shortUrl.LongUrl))
        {
            _logger.LogWarning("Set called with invalid shortUrl - missing ShortCode or LongUrl");
            return;
        }

        using var activity = _activitySource.StartActivity(ActivityKind.Client);
        activity?.SetTag("cache.operation", "set");

        var stopwatch = Stopwatch.StartNew();

        try
        {
            var json = JsonSerializer.Serialize(shortUrl, JsonOptions);
            var options = GetCacheOptions(expiration);

            var shortCodeKey = GetShortCodeKey(shortUrl.ShortCode);
            var longUrlKey = GetLongUrlKey(shortUrl.LongUrlHash);

            // Use parallel execution for better performance
            var setTasks = new[]
            {
                _cache.SetStringAsync(shortCodeKey, json, options),
                _cache.SetStringAsync(longUrlKey, json, options)
            };

            await Task.WhenAll(setTasks);
            stopwatch.Stop();

            _logger.LogDebug("Cached URL with short code: {ShortCode} (took {ElapsedMs}ms)", shortUrl.ShortCode, stopwatch.ElapsedMilliseconds);
        }
        catch (Exception ex)
        {
            Interlocked.Increment(ref _cacheErrors);
            activity?.SetTag("cache.error", true);
            activity?.SetTag("cache.error.message", ex.Message);

            _logger.LogError(ex, "Failed to cache URL: {ShortCode} (took {ElapsedMs}ms)",
                shortUrl.ShortCode, stopwatch.ElapsedMilliseconds);
        }
    }

    private async Task Remove(string key)
    {
        if (string.IsNullOrWhiteSpace(key))
        {
            _logger.LogWarning("Remove called with null or empty key");
            return;
        }

        using var activity = _activitySource.StartActivity();
        activity?.SetTag("cache.operation", "remove");

        try
        {
            await _cache.RemoveAsync(key);
            _logger.LogDebug("Removed key from cache: {Key}", key);
        }
        catch (Exception ex)
        {
            Interlocked.Increment(ref _cacheErrors);
            activity?.SetTag("cache.error", true);
            activity?.SetTag("cache.error.message", ex.Message);

            _logger.LogWarning(ex, "Failed to remove key from cache: {Key}", key);
        }
    }

    public async Task<bool> Exists(string key)
    {
        if (string.IsNullOrWhiteSpace(key))
        {
            return false;
        }

        using var activity = _activitySource.StartActivity();
        activity?.SetTag("cache.operation", "exists");

        try
        {
            // Optimized: Use GetAsync with minimal data transfer instead of GetStringAsync
            var value = await _cache.GetAsync(key);
            var exists = value is not null;

            activity?.SetTag("cache.exists", exists);
            return exists;
        }
        catch (Exception ex)
        {
            Interlocked.Increment(ref _cacheErrors);
            activity?.SetTag("cache.error", true);
            activity?.SetTag("cache.error.message", ex.Message);

            _logger.LogWarning(ex, "Failed to check key existence in cache: {Key}", key);
            return false;
        }
    }

    public CacheStatistics GetStatistics()
    {
        return new CacheStatistics
        {
            CacheHits = Interlocked.Read(ref _cacheHits),
            CacheMisses = Interlocked.Read(ref _cacheMisses),
            CacheErrors = Interlocked.Read(ref _cacheErrors),
            HitRatio = CalculateHitRatio()
        };
    }

    public void ResetStatistics()
    {
        Interlocked.Exchange(ref _cacheHits, 0);
        Interlocked.Exchange(ref _cacheMisses, 0);
        Interlocked.Exchange(ref _cacheErrors, 0);
    }

    private static string GetShortCodeKey(string shortCode) => $"{CacheNamespace}:{ShortCodePrefix}:{shortCode}";

    private static string GetLongUrlKey(string longUrl) => $"{CacheNamespace}:{LongUrlPrefix}:{longUrl}";
    
    private async Task RemoveByShortCode(string shortCode)
    {
        if (string.IsNullOrWhiteSpace(shortCode))
        {
            return;
        }

        var key = GetShortCodeKey(shortCode);
        await Remove(key);
    }

    private async Task RemoveByLongUrl(string longUrl)
    {
        if (string.IsNullOrWhiteSpace(longUrl))
        {
            return;
        }

        var key = GetLongUrlKey(longUrl);
        await Remove(key);
    }

    private DistributedCacheEntryOptions GetCacheOptions(TimeSpan? expiration)
    {
        if (expiration.HasValue)
        {
            return new DistributedCacheEntryOptions
            {
                AbsoluteExpirationRelativeToNow = expiration
            };
        }

        return new DistributedCacheEntryOptions
        {
            AbsoluteExpirationRelativeToNow = _options.DefaultExpiration,
            SlidingExpiration = _options.SlidingExpiration
        };
    }

    private double CalculateHitRatio()
    {
        var hits = Interlocked.Read(ref _cacheHits);
        var misses = Interlocked.Read(ref _cacheMisses);
        var total = hits + misses;

        return total == 0 ? 0.0 : (double)hits / total * 100.0;
    }
}
