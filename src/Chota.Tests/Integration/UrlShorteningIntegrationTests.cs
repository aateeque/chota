//using Microsoft.Extensions.DependencyInjection;
//using Microsoft.Extensions.Hosting;
//using Testcontainers.PostgreSql;
//using Testcontainers.Redis;
//using TUnit.Assertions;
//using TUnit.Core;
//using Chota.Api.Services;
//using Microsoft.Extensions.Configuration;

//namespace Chota.Tests.Integration;

//public class UrlShorteningIntegrationTests : IAsyncDisposable
//{
//    private readonly PostgreSqlContainer _postgresContainer;
//    private readonly RedisContainer _redisContainer;
//    private IHost? _host;
//    private IServiceScope? _scope;

//    public UrlShorteningIntegrationTests()
//    {
//        _postgresContainer = new PostgreSqlBuilder()
//            .WithImage("postgres:15-alpine")
//            .WithDatabase("url_shortener_test")
//            .WithUsername("test_user")
//            .WithPassword("test_password")
//            .WithCleanUp(true)
//            .Build();

//        _redisContainer = new RedisBuilder()
//            .WithImage("redis:7-alpine")
//            .WithCleanUp(true)
//            .Build();
//    }

//    [Before(Test)]
//    public async Task Setup()
//    {
//        await _postgresContainer.StartAsync();
//        await _redisContainer.StartAsync();

//        var configuration = new ConfigurationBuilder()
//            .AddInMemoryCollection(new Dictionary<string, string?>
//            {
//                ["ConnectionStrings:DefaultConnection"] = _postgresContainer.GetConnectionString(),
//                ["ConnectionStrings:Redis"] = _redisContainer.GetConnectionString(),
//            })
//            .Build();

//        _host = Host.CreateDefaultBuilder()
//            .ConfigureServices(services =>
//            {
//                // Add your actual services here - this is a simplified example
//                services.AddSingleton<IConfiguration>(configuration);
//                services.AddScoped<IUrlValidator, UrlValidator>();
//                services.AddScoped<IUrlEncoder, Base62UrlEncoder>();
//                services.AddScoped<IUrlService, UrlService>();
//                // Add repository implementations that use the test containers
//            })
//            .Build();

//        _scope = _host.Services.CreateScope();
//    }

//    [After(Test)]
//    public async Task Cleanup()
//    {
//        _scope?.Dispose();
//        if (_host != null)
//        {
//            await _host.StopAsync();
//            _host.Dispose();
//        }
//    }

//    [Test]
//    public async Task ShortenUrl_WithValidUrl_CreatesShortUrlAndStoresInDatabase()
//    {
//        // Arrange
//        var urlService = _scope!.ServiceProvider.GetRequiredService<IUrlService>();
//        var originalUrl = "https://www.example.com/very/long/path/to/resource?param=value";

//        // Act
//        var result = await urlService.Shorten(originalUrl);

//        // Assert
//        await Assert.That(result.IsSuccess).IsTrue();
//        await Assert.That(result.Value).IsNotNull();
//        await Assert.That(result.Value).HasLength().GreaterThan(0);

//        // Verify we can retrieve the original URL
//        var retrieveResult = await urlService.GetByShortCode(result.Value);
//        await Assert.That(retrieveResult.IsSuccess).IsTrue();
//        await Assert.That(retrieveResult.Value.LongUrl).IsEqualTo(originalUrl);
//    }

//    [Test]
//    public async Task ShortenUrl_WithDuplicateUrl_ReturnsSameShortCode()
//    {
//        // Arrange
//        var urlService = _scope!.ServiceProvider.GetRequiredService<IUrlService>();
//        var originalUrl = "https://www.example.com/duplicate-test";

//        // Act
//        var result1 = await urlService.Shorten(originalUrl);
//        var result2 = await urlService.Shorten(originalUrl);

//        // Assert
//        await Assert.That(result1.IsSuccess).IsTrue();
//        await Assert.That(result2.IsSuccess).IsTrue();
//        await Assert.That(result1.Value).IsEqualTo(result2.Value);
//    }

//    [Test]
//    public async Task ShortenUrl_WithInvalidUrl_ReturnsValidationError()
//    {
//        // Arrange
//        var urlService = _scope!.ServiceProvider.GetRequiredService<IUrlService>();
//        var invalidUrl = "not-a-valid-url";

//        // Act
//        var result = await urlService.Shorten(invalidUrl);

//        // Assert
//        await Assert.That(result.IsFailure).IsTrue();
//        await Assert.That(result.Error.Message).Contains("Invalid URL format");
//    }

//    [Test]
//    public async Task GetByShortCode_WithNonExistentCode_ReturnsNotFound()
//    {
//        // Arrange
//        var urlService = _scope!.ServiceProvider.GetRequiredService<IUrlService>();
//        var nonExistentCode = "nonexistent123";

//        // Act
//        var result = await urlService.GetByShortCode(nonExistentCode);

//        // Assert
//        await Assert.That(result.IsFailure).IsTrue();
//        await Assert.That(result.Error.Message).Contains("not found");
//    }

//    [Test]
//    public async Task UrlShortening_EndToEndWorkflow_WorksCorrectly()
//    {
//        // Arrange
//        var urlService = _scope!.ServiceProvider.GetRequiredService<IUrlService>();
//        var testUrls = new[]
//        {
//            "https://www.google.com",
//            "https://www.github.com/user/repo",
//            "https://api.example.com/v1/users?limit=10&offset=0",
//            "https://subdomain.example.org/path/to/resource.html#section1"
//        };

//        // Act & Assert
//        var shortCodes = new List<string>();

//        // Create short URLs
//        foreach (var url in testUrls)
//        {
//            var shortenResult = await urlService.Shorten(url);
//            await Assert.That(shortenResult.IsSuccess).IsTrue();
//            shortCodes.Add(shortenResult.Value);
//        }

//        // Verify all short codes are unique
//        await Assert.That(shortCodes.Distinct().Count()).IsEqualTo(testUrls.Length);

//        // Verify retrieval works for all URLs
//        for (var i = 0; i < testUrls.Length; i++)
//        {
//            var retrieveResult = await urlService.GetByShortCode(shortCodes[i]);
//            await Assert.That(retrieveResult.IsSuccess).IsTrue();
//            await Assert.That(retrieveResult.Value.LongUrl).IsEqualTo(testUrls[i]);
//        }
//    }

//    [Test]
//    public async Task UrlShortening_ConcurrentOperations_HandlesRaceConditions()
//    {
//        // Arrange
//        var urlService = _scope!.ServiceProvider.GetRequiredService<IUrlService>();
//        const int concurrentOperations = 100;
//        var baseUrl = "https://concurrent-test-";

//        // Act
//        var tasks = Enumerable.Range(1, concurrentOperations)
//            .Select(i => urlService.Shorten($"{baseUrl}{i}.com"))
//            .ToArray();

//        var results = await Task.WhenAll(tasks);

//        // Assert
//        await Assert.That(results.All(r => r.IsSuccess)).IsTrue();

//        var shortCodes = results.Select(r => r.Value).ToList();
//        await Assert.That(shortCodes.Distinct().Count()).IsEqualTo(concurrentOperations);
//    }

//    [Test]
//    public async Task UrlShortening_DatabasePersistence_SurvivesServiceRestart()
//    {
//        // Arrange
//        var originalUrl = "https://persistence-test.com";
//        string shortCode;

//        // Create short URL with first service instance
//        {
//            var urlService = _scope!.ServiceProvider.GetRequiredService<IUrlService>();
//            var result = await urlService.Shorten(originalUrl);
//            await Assert.That(result.IsSuccess).IsTrue();
//            shortCode = result.Value;
//        }

//        // Simulate service restart by creating new scope
//        _scope?.Dispose();
//        _scope = _host!.Services.CreateScope();

//        // Act - Try to retrieve with new service instance
//        var newUrlService = _scope!.ServiceProvider.GetRequiredService<IUrlService>();
//        var retrieveResult = await newUrlService.GetByShortCode(shortCode);

//        // Assert
//        await Assert.That(retrieveResult.IsSuccess).IsTrue();
//        await Assert.That(retrieveResult.Value.LongUrl).IsEqualTo(originalUrl);
//    }

//    [Test]
//    public async Task UrlShortening_LargeDataset_MaintainsPerformance()
//    {
//        // Arrange
//        var urlService = _scope!.ServiceProvider.GetRequiredService<IUrlService>();
//        const int urlCount = 1000;

//        // Act - Create large number of URLs
//        var tasks = Enumerable.Range(1, urlCount)
//            .Select(i => urlService.Shorten($"https://performance-test-{i}.com"))
//            .ToArray();

//        var startTime = DateTime.UtcNow;
//        var results = await Task.WhenAll(tasks);
//        var endTime = DateTime.UtcNow;

//        // Assert
//        await Assert.That(results.All(r => r.IsSuccess)).IsTrue();

//        var duration = endTime - startTime;
//        var operationsPerSecond = urlCount / duration.TotalSeconds;

//        // Should maintain reasonable performance even with larger dataset
//        await Assert.That(operationsPerSecond).IsGreaterThan(100);

//        // Verify random sampling of URLs can be retrieved
//        var sampleSize = Math.Min(100, urlCount);
//        var random = new Random();
//        var sampleTasks = Enumerable.Range(0, sampleSize)
//            .Select(_ =>
//            {
//                var index = random.Next(urlCount);
//                return urlService.GetByShortCode(results[index].Value);
//            })
//            .ToArray();

//        var sampleResults = await Task.WhenAll(sampleTasks);
//        await Assert.That(sampleResults.All(r => r.IsSuccess)).IsTrue();
//    }

//    public async ValueTask DisposeAsync()
//    {
//        await Cleanup();
//        await _postgresContainer.StopAsync();
//        await _redisContainer.StopAsync();
//        await _postgresContainer.DisposeAsync();
//        await _redisContainer.DisposeAsync();
//    }
//}
