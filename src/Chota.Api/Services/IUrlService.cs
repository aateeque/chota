using Chota.Api.Common;
using Chota.Api.Models;

namespace Chota.Api.Services;

public interface IUrlService
{
    Task<Result<ShortUrl>> Shorten(string longUrl);

    Task<Result<ShortUrl>> GetByShortCode(string shortCode);
}
