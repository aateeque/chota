using System.Diagnostics;
using Microsoft.EntityFrameworkCore;
using Chota.Api.Data;
using Chota.Api.Models;

namespace Chota.MigrationService;

public class Worker(IServiceProvider serviceProvider, IHostApplicationLifetime hostApplicationLifetime, ILogger<Worker> logger) : BackgroundService
{
    public const string ActivitySourceName = "Migrations";
    private static readonly ActivitySource ActivitySource = new(ActivitySourceName);

    protected override async Task ExecuteAsync(CancellationToken cancellationToken)
    {
        using var activity = ActivitySource.StartActivity(ActivityKind.Internal);

        try
        {
            using var scope = serviceProvider.CreateScope();
            var dbContext = scope.ServiceProvider.GetRequiredService<UrlDbContext>();

            logger.LogInformation("Starting database migration process...");

            await EnsureDatabaseExistsAsync(dbContext, cancellationToken);
            await RunMigrationAsync(dbContext, cancellationToken);
            await SeedDataAsync(dbContext, cancellationToken);

            logger.LogInformation("Database migration process completed successfully");
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Database migration failed: {ErrorMessage}", ex.Message);
            activity?.AddException(ex);
            throw;
        }
        finally
        {
            logger.LogInformation("Migration service shutting down...");
            hostApplicationLifetime.StopApplication();
        }
    }

    private async Task EnsureDatabaseExistsAsync(UrlDbContext dbContext, CancellationToken cancellationToken)
    {
        const int maxRetries = 5;
        const int delayMs = 2000;

        for (int attempt = 1; attempt <= maxRetries; attempt++)
        {
            try
            {
                logger.LogInformation("Attempting to ensure database exists (attempt {Attempt}/{MaxRetries})...", attempt, maxRetries);

                // Test the connection first
                await dbContext.Database.CanConnectAsync(cancellationToken);

                logger.LogInformation("Database connection verified successfully");
                return;
            }
            catch (Exception ex) when (attempt < maxRetries)
            {
                logger.LogWarning(ex, "Database connection attempt {Attempt} failed, retrying in {Delay}ms...", attempt, delayMs);
                await Task.Delay(delayMs, cancellationToken);
            }
        }

        logger.LogError("Failed to establish database connection after {MaxRetries} attempts", maxRetries);
        throw new InvalidOperationException($"Could not establish database connection after {maxRetries} attempts");
    }

    private async Task RunMigrationAsync(UrlDbContext dbContext, CancellationToken cancellationToken)
    {
        logger.LogInformation("Starting database migration...");

        var strategy = dbContext.Database.CreateExecutionStrategy();
        await strategy.ExecuteAsync(async () =>
        {
            var pendingMigrations = await dbContext.Database.GetPendingMigrationsAsync(cancellationToken);

            if (pendingMigrations.Any())
            {
                logger.LogInformation("Applying {Count} pending migrations: {Migrations}",
                    pendingMigrations.Count(), string.Join(", ", pendingMigrations));

                await dbContext.Database.MigrateAsync(cancellationToken);

                logger.LogInformation("Database migration completed successfully");
            }
            else
            {
                logger.LogInformation("No pending migrations found. Database is up to date");
            }
        });
    }

    private async Task SeedDataAsync(UrlDbContext dbContext, CancellationToken cancellationToken)
    {
        logger.LogInformation("Starting database seeding...");

        try
        {
            // Check if any data already exists
            var existingCount = await dbContext.ShortUrls.CountAsync(cancellationToken);

            if (existingCount > 0)
            {
                logger.LogInformation("Database already contains {Count} records. Skipping seeding", existingCount);
                return;
            }

            ShortUrl firstShortUrl = new(0L, string.Empty, string.Empty, string.Empty, DateTime.UnixEpoch);

            var strategy = dbContext.Database.CreateExecutionStrategy();
            await strategy.ExecuteAsync(async () =>
            {
                try
                {
                    await using var transaction = await dbContext.Database.BeginTransactionAsync(cancellationToken);
                    await dbContext.ShortUrls.AddAsync(firstShortUrl, cancellationToken);
                    await dbContext.SaveChangesAsync(cancellationToken);
                    await transaction.CommitAsync(cancellationToken);

                    logger.LogInformation("Database seeding completed successfully");
                }
                catch (Exception ex)
                {
                    logger.LogError(ex, "Database seeding failed: {ErrorMessage}", ex.Message);
                    throw;
                }
            });
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error during database seeding process: {ErrorMessage}", ex.Message);
            throw;
        }
    }
}
