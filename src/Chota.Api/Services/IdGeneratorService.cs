using IdGen;
using System.Text;

namespace Chota.Api.Services;

public class IdGeneratorService(IIdGenerator<long> idGenerator) : IIdGeneratorService
{
    public long GenerateNextId() => idGenerator.CreateId();

    public string HashLongUrl(string url)
    {
        if (string.IsNullOrEmpty(url))
            return "0";

        // Normalize URL for consistent hashing (lowercase, trim)
        var normalizedUrl = url.Trim().ToLowerInvariant();
        var bytes = Encoding.UTF8.GetBytes(normalizedUrl);

        // FNV-1a hash algorithm - fast and deterministic
        const ulong fnvOffsetBasis = 14695981039346656037UL;
        const ulong fnvPrime = 1099511628211UL;

        var hash = fnvOffsetBasis;
        foreach (var b in bytes)
        {
            hash ^= b;
            hash *= fnvPrime;
        }

        return hash.ToString("X16"); // 16-character hex representation
    }
}
