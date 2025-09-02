namespace Chota.Api.Models;

// Configuration options for Redis cache
public class RedisUrlRepositoryOptions
{
    public TimeSpan DefaultExpiration { get; set; } = TimeSpan.FromHours(24);
    public TimeSpan SlidingExpiration { get; set; } = TimeSpan.FromHours(6);
    public bool EnableTelemetry { get; set; } = true;
}