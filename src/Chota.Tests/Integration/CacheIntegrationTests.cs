//using Microsoft.Extensions.Caching.Distributed;
//using Microsoft.Extensions.DependencyInjection;
//using Microsoft.Extensions.Hosting;
//using Microsoft.Extensions.Logging;
//using NSubstitute;
//using TUnit.Core;
//using Chota.Api.Data;
//using Chota.Api.Models;
//using Chota.Api.Services;

//namespace Chota.Tests.Integration;

//public sealed class CacheIntegrationTests
//{
//    private readonly TestFixture _fixture = new();

//    [Test]
//    public async Task CompositeRepository_GetByShortCode_FollowsCacheHierarchy()
//    {
//        // Arrange
//        var shortUrl = new ShortUrl("1", "https://example.com", "abc123", DateTime.UtcNow);
//        var repository = _fixture.CreateCompositeRepository();

//        // Act & Assert - Initial save stores in all layers
//        await repository.Save(shortUrl);

//        // Verify L1 (Memory) cache hit
//        var result1 = await repository.GetByShortCode("abc123");
//        await Assert.That(result1).IsNotNull();
//        await Assert.That(result1!.LongUrl).IsEqualTo("https://example.com");

//        // Clear memory cache to test L2 (Redis) cache
//        var memoryRepo = _fixture.CreateMemoryRepository();
//        var cacheRepo = _fixture.CreateCacheRepository();
//        var newCompositeRepo = new CompositeUrlRepository(memoryRepo, cacheRepo, 
//            _fixture.CreatePostgresRepository(),
//            _fixture.CreateSnowflakeIdGenerator(),
//            _fixture.ServiceProvider.GetRequiredService<ILogger<CompositeUrlRepository>>());

//        // Verify L2 (Redis) cache hit - should populate L1
//        var result2 = await newCompositeRepo.GetByShortCode("abc123");
//        await Assert.That(result2).IsNotNull();
//        await Assert.That(result2!.LongUrl).IsEqualTo("https://example.com");

//        // Verify L1 was populated after L2 hit
//        var result3 = await memoryRepo.GetByShortCode("abc123");
//        await Assert.That(result3).IsNotNull();
//        await Assert.That(result3!.LongUrl).IsEqualTo("https://example.com");
//    }

//    [Test]
//    public async Task CompositeRepository_GetByLongUrl_FollowsCacheHierarchy()
//    {
//        // Arrange
//        var longUrl = "https://integration.test.com";
//        var shortUrl = new ShortUrl("2", longUrl, "xyz789", DateTime.UtcNow);
//        var repository = _fixture.CreateCompositeRepository();

//        // Act & Assert
//        await repository.Save(shortUrl);

//        // Test cache hierarchy for long URL lookup
//        var result = await repository.GetByLongUrl(longUrl);
//        await Assert.That(result).IsNotNull();
//        await Assert.That(result!.ShortCode).IsEqualTo("xyz789");
//    }

//    [Test]
//    public async Task CompositeRepository_Save_StoresInAllLayers()
//    {
//        // Arrange
//        var shortUrl = new ShortUrl("3", "https://multilayer.test", "def456", DateTime.UtcNow);
//        var memoryRepo = _fixture.CreateMemoryRepository();
//        var cacheRepo = _fixture.CreateCacheRepository();
//        var repository = new CompositeUrlRepository(memoryRepo, cacheRepo,
//            _fixture.CreatePostgresRepository(),
//            _fixture.CreateSnowflakeIdGenerator(),
//            _fixture.ServiceProvider.GetRequiredService<ILogger<CompositeUrlRepository>>());

//        // Act
//        var savedId = await repository.Save(shortUrl);

//        // Assert
//        await Assert.That(savedId).IsEqualTo("3");

//        // Verify stored in memory
//        var fromMemory = await memoryRepo.GetByShortCode("def456");
//        await Assert.That(fromMemory).IsNotNull();
//        await Assert.That(fromMemory!.LongUrl).IsEqualTo("https://multilayer.test");

//        // Verify stored in Redis
//        var fromCache = await cacheRepo.GetByShortCode("def456");
//        await Assert.That(fromCache).IsNotNull();
//        await Assert.That(fromCache!.LongUrl).IsEqualTo("https://multilayer.test");
//    }

//    [Test]
//    public async Task CompositeRepository_CacheMiss_ReturnsNull()
//    {
//        // Arrange
//        var repository = _fixture.CreateCompositeRepository();

//        // Act
//        var result = await repository.GetByShortCode("nonexistent");

//        // Assert
//        await Assert.That(result).IsNull();
//    }

//    [Test]
//    public async Task CompositeRepository_GetNextId_DelegatesToMemoryRepository()
//    {
//        // Arrange
//        var repository = _fixture.CreateCompositeRepository();

//        // Act
//        var id1 = await repository.GetNextId();
//        var id2 = await repository.GetNextId();

//        // Assert
//        await Assert.That(id2).IsGreaterThan(id1);
//    }

//    public sealed class TestFixture : IDisposable
//    {
//        public IServiceProvider ServiceProvider { get; }
//        private readonly IHost _host;

//        public TestFixture()
//        {
//            var builder = Host.CreateDefaultBuilder()
//                .ConfigureServices(services =>
//                {
//                    services.AddMemoryCache();
//                    services.AddStackExchangeRedisCache(options =>
//                    {
//                        options.Configuration = "localhost:6379";
//                    });
//                    services.AddLogging();
//                });

//            _host = builder.Build();
//            ServiceProvider = _host.Services;
//        }

//        public InMemoryUrlRepository CreateMemoryRepository() => new();

//        public RedisUrlRepository CreateCacheRepository() => new(
//            ServiceProvider.GetRequiredService<IDistributedCache>(),
//            ServiceProvider.GetRequiredService<ILogger<RedisUrlRepository>>());

//        public CompositeUrlRepository CreateCompositeRepository() => new(
//            CreateMemoryRepository(),
//            CreateCacheRepository(),
//            CreatePostgresRepository(),
//            CreateSnowflakeIdGenerator(),
//            ServiceProvider.GetRequiredService<ILogger<CompositeUrlRepository>>());

//        public IPostgresUrlRepository CreatePostgresRepository()
//        {
//            // For testing, return a mock that returns null for all gets
//            var mock = Substitute.For<IPostgresUrlRepository>();
//            return mock;
//        }

//        public IIdGeneratorService CreateSnowflakeIdGenerator() => new IdGeneratorService();

//        public void Dispose()
//        {
//            _host?.Dispose();
//        }
//    }
//}