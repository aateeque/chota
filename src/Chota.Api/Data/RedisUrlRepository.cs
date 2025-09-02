using Microsoft.Extensions.Caching.Distributed;
using Microsoft.Extensions.Options;
using System.Diagnostics;
using System.Text;
using System.Text.Json;
using Chota.Api.Models;

namespace Chota.Api.Data;

public sealed class RedisUrlRepository(IDistributedCache cache, ILogger<RedisUrlRepository> logger, IOptions<RedisUrlRepositoryOptions>? options = null) : ICacheRepository
{
    private readonly IDistributedCache _cache = cache ?? throw new ArgumentNullException(nameof(cache));
    private readonly ILogger<RedisUrlRepository> _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    private readonly RedisUrlRepositoryOptions _options = options?.Value ?? new RedisUrlRepositoryOptions();
    private readonly ActivitySource _activitySource = new("Chota.Cache");

    // Cache statistics
    private long _cacheHits;
    private long _cacheMisses;
    private long _cacheErrors;

    // Cache key constants for consistency and performance
    private const string CACHE_NAMESPACE = "urlshort";
    private const string CACHE_VERSION = "v1";
    private const string SHORT_CODE_PREFIX = "sc";
    private const string LONG_URL_PREFIX = "lu";

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

        using var activity = _activitySource.StartActivity("RedisUrlRepository.GetByShortCode");
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
                _logger.LogDebug("Cache miss for short code: {ShortCode} (took {ElapsedMs}ms)",
                    shortCode, stopwatch.ElapsedMilliseconds);
                return null;
            }

            var shortUrl = JsonSerializer.Deserialize<ShortUrl>(json, JsonOptions);
            Interlocked.Increment(ref _cacheHits);
            activity?.SetTag("cache.hit", true);

            _logger.LogDebug("Cache hit for short code: {ShortCode} (took {ElapsedMs}ms)",
                shortCode, stopwatch.ElapsedMilliseconds);

            return shortUrl;
        }
        catch (Exception ex)
        {
            Interlocked.Increment(ref _cacheErrors);
            activity?.SetTag("cache.error", true);
            activity?.SetTag("cache.error.message", ex.Message);

            _logger.LogWarning(ex, "Failed to get URL by short code from cache: {ShortCode} (took {ElapsedMs}ms)",
                shortCode, stopwatch.ElapsedMilliseconds);
            return null;
        }
    }

    public async Task<ShortUrl?> GetByLongUrl(string longUrl)
    {
        if (string.IsNullOrWhiteSpace(longUrl))
        {
            _logger.LogWarning("GetByLongUrl called with null or empty longUrl");
            return null;
        }

        using var activity = _activitySource.StartActivity("RedisUrlRepository.GetByLongUrl");
        activity?.SetTag("cache.operation", "get");
        activity?.SetTag("cache.key_type", "long_url");

        var stopwatch = Stopwatch.StartNew();

        try
        {
            var key = GetLongUrlKey(longUrl);
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

            _logger.LogWarning(ex, "Failed to get URL by long URL from cache (took {ElapsedMs}ms)",
                stopwatch.ElapsedMilliseconds);
            return null;
        }
    }

    public async Task Set(ShortUrl shortUrl, TimeSpan? expiration = null)
    {
        if (shortUrl is null)
        {
            _logger.LogWarning("Set called with null shortUrl");
            return;
        }

        if (string.IsNullOrWhiteSpace(shortUrl.ShortCode) || string.IsNullOrWhiteSpace(shortUrl.LongUrl))
        {
            _logger.LogWarning("Set called with invalid shortUrl - missing ShortCode or LongUrl");
            return;
        }

        using var activity = _activitySource.StartActivity("RedisUrlRepository.Set");
        activity?.SetTag("cache.operation", "set");

        var stopwatch = Stopwatch.StartNew();

        try
        {
            var json = JsonSerializer.Serialize(shortUrl, JsonOptions);
            var options = GetCacheOptions(expiration);

            var shortCodeKey = GetShortCodeKey(shortUrl.ShortCode);
            var longUrlKey = GetLongUrlKey(shortUrl.LongUrl);

            // Use parallel execution for better performance
            var setTasks = new[]
            {
                _cache.SetStringAsync(shortCodeKey, json, options),
                _cache.SetStringAsync(longUrlKey, json, options)
            };

            await Task.WhenAll(setTasks);
            stopwatch.Stop();

            _logger.LogDebug("Cached URL with short code: {ShortCode} (took {ElapsedMs}ms)",
                shortUrl.ShortCode, stopwatch.ElapsedMilliseconds);
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

    public async Task Remove(string key)
    {
        if (string.IsNullOrWhiteSpace(key))
        {
            _logger.LogWarning("Remove called with null or empty key");
            return;
        }

        using var activity = _activitySource.StartActivity("RedisUrlRepository.Remove");
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

        using var activity = _activitySource.StartActivity("RedisUrlRepository.Exists");
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

    // Enhanced cache invalidation methods
    public async Task RemoveByShortCode(string shortCode)
    {
        if (string.IsNullOrWhiteSpace(shortCode))
        {
            return;
        }

        var key = GetShortCodeKey(shortCode);
        await Remove(key);
    }

    public async Task RemoveByLongUrl(string longUrl)
    {
        if (string.IsNullOrWhiteSpace(longUrl))
        {
            return;
        }

        var key = GetLongUrlKey(longUrl);
        await Remove(key);
    }

    public async Task RemoveAll(ShortUrl shortUrl)
    {
        if (shortUrl is null || string.IsNullOrWhiteSpace(shortUrl.ShortCode) || string.IsNullOrWhiteSpace(shortUrl.LongUrl))
        {
            return;
        }

        var tasks = new[]
        {
            RemoveByShortCode(shortUrl.ShortCode),
            RemoveByLongUrl(shortUrl.LongUrl)
        };

        await Task.WhenAll(tasks);
    }

    // Cache statistics for monitoring
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

    // Private helper methods
    private static string GetShortCodeKey(string shortCode)
        => $"{CACHE_NAMESPACE}:{CACHE_VERSION}:{SHORT_CODE_PREFIX}:{shortCode}";

    private static string GetLongUrlKey(string longUrl)
        => $"{CACHE_NAMESPACE}:{CACHE_VERSION}:{LONG_URL_PREFIX}:{GetUrlHash(longUrl)}";

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

    /// <summary>
    /// Fast, deterministic hash function using FNV-1a algorithm.
    /// Much better than GetHashCode() which is non-deterministic across app restarts.
    /// </summary>
    private static string GetUrlHash(string url)
    {
        if (string.IsNullOrEmpty(url))
            return "0";

        // Normalize URL for consistent hashing (lowercase, trim)
        var normalizedUrl = url.Trim().ToLowerInvariant();
        var bytes = Encoding.UTF8.GetBytes(normalizedUrl);

        // FNV-1a hash algorithm - fast and deterministic
        const ulong FnvOffsetBasis = 14695981039346656037UL;
        const ulong FnvPrime = 1099511628211UL;

        var hash = FnvOffsetBasis;
        foreach (var b in bytes)
        {
            hash ^= b;
            hash *= FnvPrime;
        }

        return hash.ToString("X16"); // 16-character hex representation
    }
}