using Microsoft.EntityFrameworkCore;
using Chota.Api.Models;

namespace Chota.Api.Data;

public sealed class UrlDbContext(DbContextOptions<UrlDbContext> options) : DbContext(options)
{
    public DbSet<ShortUrl> ShortUrls => Set<ShortUrl>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);
    }

    protected override void OnConfiguring(DbContextOptionsBuilder optionsBuilder)
    {
        base.OnConfiguring(optionsBuilder);

        // Enable sensitive data logging in development
        if (!optionsBuilder.IsConfigured)
        {
            optionsBuilder.EnableSensitiveDataLogging(false);
            optionsBuilder.EnableDetailedErrors(false);
        }
    }
}