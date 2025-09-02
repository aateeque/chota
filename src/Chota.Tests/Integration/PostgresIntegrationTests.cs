using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Testcontainers.PostgreSql;
using Chota.Api.Data;
using Chota.Api.Models;

namespace Chota.Tests.Integration;

public sealed class PostgresIntegrationTests : IAsyncDisposable
{
    private readonly PostgreSqlContainer _postgres = new PostgreSqlBuilder()
        .WithImage("postgres:16-alpine")
        .WithDatabase("Chota")
        .WithUsername("postgres")
        .WithPassword("postgres")
        .Build();

    private IServiceProvider? _serviceProvider;
    private bool _initialized = false;

    private async Task EnsureInitialized()
    {
        if (_initialized) return;

        await _postgres.StartAsync();

        var connectionString = _postgres.GetConnectionString();

        var services = new ServiceCollection();
        services.AddDbContext<UrlDbContext>(options =>
            options.UseNpgsql(connectionString)
                   .EnableSensitiveDataLogging()
                   .EnableDetailedErrors());
        services.AddLogging(builder => builder.AddConsole().SetMinimumLevel(LogLevel.Debug));
        services.AddTransient<PostgresUrlRepository>();

        _serviceProvider = services.BuildServiceProvider();

        // Apply migrations
        using var scope = _serviceProvider.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<UrlDbContext>();

        // Ensure database exists
        await context.Database.EnsureCreatedAsync();

        // Apply migrations explicitly
        await context.Database.MigrateAsync();

        _initialized = true;
    }

    public async ValueTask DisposeAsync()
    {
        await _postgres.DisposeAsync();
        _serviceProvider?.GetService<UrlDbContext>()?.Dispose();
    }

    [Test]
    public async Task PostgresUrlRepository_Save_StoresUrlSuccessfully()
    {
        // Arrange
        await EnsureInitialized();
        using var scope = _serviceProvider!.CreateScope();
        var repository = scope.ServiceProvider.GetRequiredService<PostgresUrlRepository>();

        var shortUrl = new ShortUrl(
            1L,
            "https://example.com",
            "abc123",
            DateTime.UtcNow
        );

        // Act
        var savedId = await repository.Save(shortUrl);

        // Assert
        await Assert.That(savedId).IsEqualTo(1L);

        var retrieved = await repository.GetByShortCode("abc123");
        await Assert.That(retrieved).IsNotNull();
        await Assert.That(retrieved!.LongUrl).IsEqualTo("https://example.com");
        await Assert.That(retrieved.ShortCode).IsEqualTo("abc123");
    }

    [Test]
    public async Task PostgresUrlRepository_GetByShortCode_ReturnsNullForNonexistent()
    {
        // Arrange
        await EnsureInitialized();
        using var scope = _serviceProvider!.CreateScope();
        var repository = scope.ServiceProvider.GetRequiredService<PostgresUrlRepository>();

        // Act
        var result = await repository.GetByShortCode("nonexistent");

        // Assert
        await Assert.That(result).IsNull();
    }

    [Test]
    public async Task PostgresUrlRepository_GetByLongUrl_FindsExistingUrl()
    {
        // Arrange
        await EnsureInitialized();
        using var scope = _serviceProvider!.CreateScope();
        var repository = scope.ServiceProvider.GetRequiredService<PostgresUrlRepository>();

        var shortUrl = new ShortUrl(
            2L,
            "https://longurl.test.com",
            "xyz789",
            DateTime.UtcNow
        );

        await repository.Save(shortUrl);

        // Act
        var result = await repository.GetByLongUrl("https://longurl.test.com");

        // Assert
        await Assert.That(result).IsNotNull();
        await Assert.That(result!.ShortCode).IsEqualTo("xyz789");
        await Assert.That(result.LongUrl).IsEqualTo("https://longurl.test.com");
    }

    [Test]
    public async Task PostgresUrlRepository_Save_HandlesLongUrlDuplicates()
    {
        // Arrange
        await EnsureInitialized();
        using var scope = _serviceProvider!.CreateScope();
        var repository = scope.ServiceProvider.GetRequiredService<PostgresUrlRepository>();

        var shortUrl1 = new ShortUrl(
            3L,
            "https://duplicate.test.com",
            "dup123",
            DateTime.UtcNow
        );

        var shortUrl2 = new ShortUrl(
            4L,
            "https://duplicate.test.com", // Same long URL
            "dup456",
            DateTime.UtcNow
        );

        // Act
        var savedId1 = await repository.Save(shortUrl1);
        var savedId2 = await repository.Save(shortUrl2); // Should return existing

        // Assert
        await Assert.That(savedId1).IsEqualTo(3L);
        await Assert.That(savedId2).IsEqualTo(3L); // Should return the existing ID

        var count = await repository.GetUrlCount();
        await Assert.That(count).IsEqualTo(1); // Only one URL should be saved
    }

    [Test]
    public async Task PostgresUrlRepository_GetUrlCount_ReturnsCorrectCount()
    {
        // Arrange
        await EnsureInitialized();
        using var scope = _serviceProvider!.CreateScope();
        var repository = scope.ServiceProvider.GetRequiredService<PostgresUrlRepository>();

        var urls = new[]
        {
            new ShortUrl(5L, "https://count1.test.com", "cnt1", DateTime.UtcNow),
            new ShortUrl(6L, "https://count2.test.com", "cnt2", DateTime.UtcNow),
            new ShortUrl(7L, "https://count3.test.com", "cnt3", DateTime.UtcNow)
        };

        // Act
        foreach (var url in urls)
        {
            await repository.Save(url);
        }

        var count = await repository.GetUrlCount();

        // Assert
        await Assert.That(count).IsEqualTo(3);
    }

    [Test]
    public async Task PostgresUrlRepository_GetRecentUrls_ReturnsInDescendingOrder()
    {
        // Arrange
        await EnsureInitialized();
        using var scope = _serviceProvider!.CreateScope();
        var repository = scope.ServiceProvider.GetRequiredService<PostgresUrlRepository>();

        var now = DateTime.UtcNow;
        var urls = new[]
        {
            new ShortUrl(8L, "https://recent1.test.com", "rec1", now.AddMinutes(-10)),
            new ShortUrl(9L, "https://recent2.test.com", "rec2", now.AddMinutes(-5)),
            new ShortUrl(10L, "https://recent3.test.com", "rec3", now)
        };

        // Act
        foreach (var url in urls)
        {
            await repository.Save(url);
        }

        var recent = await repository.GetRecentUrls(5);
        var recentList = recent.ToList();

        // Assert
        await Assert.That(recentList.Count).IsEqualTo(3);
        await Assert.That(recentList[0].ShortCode).IsEqualTo("rec3"); // Most recent first
        await Assert.That(recentList[1].ShortCode).IsEqualTo("rec2");
        await Assert.That(recentList[2].ShortCode).IsEqualTo("rec1");
    }

    [Test]
    public async Task PostgresUrlRepository_ExistsById_ReturnsTrueForExistingId()
    {
        // Arrange
        await EnsureInitialized();
        using var scope = _serviceProvider!.CreateScope();
        var repository = scope.ServiceProvider.GetRequiredService<PostgresUrlRepository>();

        var shortUrl = new ShortUrl(
            11L,
            "https://exists.test.com",
            "exists1",
            DateTime.UtcNow
        );

        await repository.Save(shortUrl);

        // Act
        var exists = await repository.ExistsById(11L);
        var notExists = await repository.ExistsById(999L);

        // Assert
        await Assert.That(exists).IsTrue();
        await Assert.That(notExists).IsFalse();
    }
}