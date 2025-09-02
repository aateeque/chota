using Microsoft.EntityFrameworkCore;

namespace Chota.Api.Models;

[Index(propertyName: nameof(LongUrl), IsUnique = true)]
public class ShortUrl(long id, string longUrl, string shortCode, DateTime createdAt, int browserClickCount = 0, int apiClickCount = 0)
{
    public long Id { get; init; } = id;

    public string LongUrl { get; init; } = longUrl;

    public string ShortCode { get; init; } = shortCode;

    public DateTime CreatedAt { get; init; } = createdAt;

    public int BrowserClickCount { get; set; } = browserClickCount;

    public int ApiClickCount { get; set; } = apiClickCount;
}
