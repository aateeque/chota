using System.Collections.Concurrent;
using Chota.Api.Models;
using Chota.Api.Services;

namespace Chota.Api.Data;

public sealed class InMemoryUrlRepository : IUrlRepository
{
    private readonly ConcurrentDictionary<string, ShortUrl> _urlsByShortCode = new();
    private readonly ConcurrentDictionary<string, ShortUrl> _urlsByLongUrl = new();

    public ValueTask<ShortUrl?> GetByShortCode(string shortCode)
    {
        _urlsByShortCode.TryGetValue(shortCode, out var shortUrl);
        return ValueTask.FromResult(shortUrl);
    }

    public ValueTask<ShortUrl?> GetByLongUrl(string longUrl)
    {
        _urlsByLongUrl.TryGetValue(longUrl, out var shortUrl);
        return ValueTask.FromResult(shortUrl);
    }

    public ValueTask Save(ShortUrl shortUrl)
    {
        _ = _urlsByLongUrl.TryAdd(shortUrl.LongUrl, shortUrl) && _urlsByShortCode.TryAdd(shortUrl.ShortCode, shortUrl);
        return ValueTask.CompletedTask;

    }

    public ValueTask Update(ShortUrl shortUrl)
    {
        return ValueTask.CompletedTask;
    }
}
