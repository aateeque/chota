using System.Collections.Concurrent;
using Chota.Api.Models;
using Chota.Api.Services;

namespace Chota.Api.Data;

public sealed class InMemoryUrlRepository : IUrlRepository
{
    private readonly ConcurrentDictionary<string, ShortUrl> _urlsByShortCode = new();
    private readonly ConcurrentDictionary<string, ShortUrl> _urlsByLongUrl = new();

    public Task<ShortUrl?> GetByShortCode(string shortCode)
    {
        _urlsByShortCode.TryGetValue(shortCode, out var shortUrl);
        return Task.FromResult(shortUrl);
    }

    public Task<ShortUrl?> GetByLongUrl(string longUrl)
    {
        _urlsByLongUrl.TryGetValue(longUrl, out var shortUrl);
        return Task.FromResult(shortUrl);
    }

    public Task Save(ShortUrl shortUrl)
    {
        _ = _urlsByLongUrl.TryAdd(shortUrl.LongUrl, shortUrl) && _urlsByShortCode.TryAdd(shortUrl.ShortCode, shortUrl);
        return Task.CompletedTask;

    }

    public Task<string> GetNextId()
    {
        throw new NotImplementedException();
    }
}
