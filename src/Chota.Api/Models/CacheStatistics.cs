namespace Chota.Api.Models;

// Cache statistics model
public class CacheStatistics
{
    public long CacheHits { get; init; }
    public long CacheMisses { get; init; }
    public long CacheErrors { get; init; }
    public double HitRatio { get; init; }
    public DateTime Timestamp { get; init; } = DateTime.UtcNow;
}