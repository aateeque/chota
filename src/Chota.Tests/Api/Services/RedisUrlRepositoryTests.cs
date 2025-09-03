using Microsoft.Extensions.Caching.Distributed;
using Microsoft.Extensions.Logging;
using NSubstitute;
using System.Text;
using Chota.Api.Data;
using Chota.Api.Models;

namespace Chota.Tests.Api.Services;

public sealed class RedisUrlRepositoryTests
{
    private readonly IDistributedCache _mockCache;
    private readonly ILogger<RedisUrlRepository> _mockLogger;
    private readonly RedisUrlRepository _repository;

    public RedisUrlRepositoryTests()
    {
        _mockCache = Substitute.For<IDistributedCache>();
        _mockLogger = Substitute.For<ILogger<RedisUrlRepository>>();

        // Setup default mock behavior to prevent ArgumentNullException
        _mockCache.SetStringAsync(Arg.Any<string>(), Arg.Any<string>(), Arg.Any<DistributedCacheEntryOptions>(), Arg.Any<CancellationToken>())
                  .Returns(Task.CompletedTask);
        _mockCache.RemoveAsync(Arg.Any<string>(), Arg.Any<CancellationToken>())
                  .Returns(Task.CompletedTask);

        _repository = new RedisUrlRepository(_mockCache, _mockLogger);
    }

    [Test]
    public async Task GetByShortCode_WhenCacheHit_ReturnsShortUrl()
    {
        // Arrange
        var shortCode = "abc123";
        var shortUrl = new ShortUrl(1L, "https://example.com", shortCode, DateTime.UtcNow);
        var json = """{"id":1,"longUrl":"https://example.com","shortCode":"abc123","createdAt":"2024-01-01T00:00:00.000Z","clickCount":0}""";

        // Mock the namespaced key that our improved implementation uses
        _mockCache.GetStringAsync($"urlshort:v1:sc:{shortCode}", Arg.Any<CancellationToken>())
                  .Returns(Task.FromResult<string?>(json));

        // Act
        var result = await _repository.GetByShortCode(shortCode);

        // Assert
        await Assert.That(result).IsNotNull();
        await Assert.That(result!.ShortCode).IsEqualTo(shortCode);
        await Assert.That(result.LongUrl).IsEqualTo("https://example.com");
    }

    [Test]
    public async Task GetByShortCode_WhenCacheMiss_ReturnsNull()
    {
        // Arrange
        var shortCode = "notfound";
        _mockCache.GetStringAsync($"urlshort:v1:sc:{shortCode}", Arg.Any<CancellationToken>())
                  .Returns(Task.FromResult((string?)null));

        // Act
        var result = await _repository.GetByShortCode(shortCode);

        // Assert
        await Assert.That(result).IsNull();
    }

    [Test]
    public async Task GetByLongUrl_WhenCacheHit_ReturnsShortUrl()
    {
        // Arrange
        var longUrl = "https://example.com";
        var shortUrl = new ShortUrl(1L, longUrl, "abc123", DateTime.UtcNow);
        var json = """{"id":1,"longUrl":"https://example.com","shortCode":"abc123","createdAt":"2024-01-01T00:00:00.000Z","clickCount":0}""";

        // Use the FNV-1a hash that our improved implementation uses
        var expectedHash = GetFnvHash(longUrl.Trim().ToLowerInvariant());
        var expectedKey = $"urlshort:v1:lu:{expectedHash}";

        _mockCache.GetStringAsync(expectedKey, Arg.Any<CancellationToken>())
                  .Returns(Task.FromResult<string?>(json));

        // Act
        var result = await _repository.GetByLongUrlHash(longUrl);

        // Assert
        await Assert.That(result).IsNotNull();
        await Assert.That(result!.LongUrl).IsEqualTo(longUrl);
        await Assert.That(result.ShortCode).IsEqualTo("abc123");
    }

    [Test]
    public async Task GetByLongUrl_WhenCacheMiss_ReturnsNull()
    {
        // Arrange
        var longUrl = "https://notfound.com";
        var expectedHash = GetFnvHash(longUrl.Trim().ToLowerInvariant());
        var expectedKey = $"urlshort:v1:lu:{expectedHash}";

        _mockCache.GetStringAsync(expectedKey, Arg.Any<CancellationToken>())
                  .Returns(Task.FromResult((string?)null));

        // Act
        var result = await _repository.GetByLongUrlHash(longUrl);

        // Assert
        await Assert.That(result).IsNull();
    }

    [Test]
    public async Task Set_StoresBothShortCodeAndLongUrlKeys()
    {
        // Arrange
        var shortUrl = new ShortUrl(1L, "https://example.com", "abc123", DateTime.UtcNow);
        var expectedShortCodeKey = "urlshort:v1:sc:abc123";
        var expectedLongUrlKey = $"urlshort:v1:lu:{GetFnvHash("https://example.com")}";


        // Act
        await _repository.Set(shortUrl);

        // Assert - Verify the correct keys were used
        await _mockCache.Received(1).SetStringAsync(
            expectedShortCodeKey,
            Arg.Is<string>(json => !string.IsNullOrEmpty(json)),
            Arg.Any<DistributedCacheEntryOptions>(),
            Arg.Any<CancellationToken>());

        await _mockCache.Received(1).SetStringAsync(
            expectedLongUrlKey,
            Arg.Is<string>(json => !string.IsNullOrEmpty(json)),
            Arg.Any<DistributedCacheEntryOptions>(),
            Arg.Any<CancellationToken>());
    }

    [Test]
    public async Task Set_WithCustomExpiration_UsesProvidedExpiration()
    {
        // Arrange
        var shortUrl = new ShortUrl(1L, "https://example.com", "abc123", DateTime.UtcNow);
        var customExpiration = TimeSpan.FromMinutes(30);


        // Act
        await _repository.Set(shortUrl, customExpiration);

        // Assert
        await _mockCache.Received(2).SetStringAsync(
            Arg.Any<string>(),
            Arg.Is<string>(json => !string.IsNullOrEmpty(json)),
            Arg.Is<DistributedCacheEntryOptions>(opts =>
                opts.AbsoluteExpirationRelativeToNow == customExpiration),
            Arg.Any<CancellationToken>());
    }

    [Test]
    public async Task Set_WithNullShortUrl_DoesNotThrow()
    {
        // Act & Assert
        await _repository.Set(null!);

        // Should not throw and should not call cache
        await _mockCache.DidNotReceive().SetStringAsync(
            Arg.Any<string>(),
            Arg.Any<string>(),
            Arg.Any<DistributedCacheEntryOptions>(),
            Arg.Any<CancellationToken>());
    }

    [Test]
    public async Task Set_WithInvalidShortUrl_DoesNotThrow()
    {
        // Arrange
        var invalidShortUrl = new ShortUrl(1L, "", "abc123", DateTime.UtcNow);

        // Act & Assert
        await _repository.Set(invalidShortUrl);

        // Should not throw and should not call cache
        await _mockCache.DidNotReceive().SetStringAsync(
            Arg.Any<string>(),
            Arg.Any<string>(),
            Arg.Any<DistributedCacheEntryOptions>(),
            Arg.Any<CancellationToken>());
    }

    [Test]
    public async Task Exists_WhenKeyExists_ReturnsTrue()
    {
        // Arrange
        var key = "test-key";
        _mockCache.GetAsync(key, Arg.Any<CancellationToken>())
                  .Returns(Task.FromResult(Encoding.UTF8.GetBytes("some-data")));

        // Act
        var result = await _repository.Exists(key);

        // Assert
        await Assert.That(result).IsTrue();
    }

    [Test]
    public async Task Exists_WhenKeyDoesNotExist_ReturnsFalse()
    {
        // Arrange
        var key = "nonexistent-key";
        _mockCache.GetAsync(key, Arg.Any<CancellationToken>())
                  .Returns(Task.FromResult((byte[]?)null));

        // Act
        var result = await _repository.Exists(key);

        // Assert
        await Assert.That(result).IsFalse();
    }

    [Test]
    public async Task Remove_CallsCacheRemove()
    {
        // Arrange
        var key = "test-key";

        // Act
        await _repository.Remove(key);

        // Assert
        await _mockCache.Received(1).RemoveAsync(key, Arg.Any<CancellationToken>());
    }

    [Test]
    public async Task GetStatistics_ReturnsCorrectInitialValues()
    {
        // Act
        var stats = _repository.GetStatistics();

        // Assert
        await Assert.That(stats.CacheHits).IsEqualTo(0);
        await Assert.That(stats.CacheMisses).IsEqualTo(0);
        await Assert.That(stats.CacheErrors).IsEqualTo(0);
        await Assert.That(stats.HitRatio).IsEqualTo(0.0);
    }

    [Test]
    public async Task ResetStatistics_ClearsAllCounters()
    {
        // Arrange - Generate some cache activity first
        _mockCache.GetStringAsync(Arg.Any<string>(), Arg.Any<CancellationToken>())
                  .Returns(Task.FromResult("some-json"));
        await _repository.GetByShortCode("test");

        // Act
        _repository.ResetStatistics();

        // Assert
        var stats = _repository.GetStatistics();
        await Assert.That(stats.CacheHits).IsEqualTo(0);
        await Assert.That(stats.CacheMisses).IsEqualTo(0);
        await Assert.That(stats.CacheErrors).IsEqualTo(0);
    }

    [Test]
    public async Task GetByShortCode_WithNullOrEmpty_ReturnsNull()
    {
        // Act & Assert - These should not throw and should return null
        var result1 = await _repository.GetByShortCode(null!);
        var result2 = await _repository.GetByShortCode("");
        var result3 = await _repository.GetByShortCode("   ");

        await Assert.That(result1).IsNull();
        await Assert.That(result2).IsNull();
        await Assert.That(result3).IsNull();
    }

    [Test]
    public async Task GetByLongUrl_WithNullOrEmpty_ReturnsNull()
    {
        // Act & Assert - These should not throw and should return null
        var result1 = await _repository.GetByLongUrlHash(null!);
        var result2 = await _repository.GetByLongUrlHash("");
        var result3 = await _repository.GetByLongUrlHash("   ");

        await Assert.That(result1).IsNull();
        await Assert.That(result2).IsNull();
        await Assert.That(result3).IsNull();
    }

    /// <summary>
    /// FNV-1a hash function matching the implementation in RedisUrlRepository.
    /// This ensures our tests use the same hashing algorithm.
    /// </summary>
    private static string GetFnvHash(string url)
    {
        if (string.IsNullOrEmpty(url))
            return "0";

        var normalizedUrl = url.Trim().ToLowerInvariant();
        var bytes = Encoding.UTF8.GetBytes(normalizedUrl);

        const ulong FnvOffsetBasis = 14695981039346656037UL;
        const ulong FnvPrime = 1099511628211UL;

        var hash = FnvOffsetBasis;
        foreach (var b in bytes)
        {
            hash ^= b;
            hash *= FnvPrime;
        }

        return hash.ToString("X16");
    }
}