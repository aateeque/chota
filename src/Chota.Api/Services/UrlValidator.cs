using System.Text.RegularExpressions;

namespace Chota.Api.Services
{
    public partial class UrlValidator : IUrlValidator
    {
        private static readonly Regex UrlRegex = MyRegex();

        public bool IsValid(string url)
        {
            if (string.IsNullOrWhiteSpace(url))
                return false;

            if (Uri.TryCreate(url, UriKind.Absolute, out var uriResult))
            {
                // Accept only http, https, ftp
                if (uriResult.Scheme == Uri.UriSchemeHttp ||
                    uriResult.Scheme == Uri.UriSchemeHttps ||
                    uriResult.Scheme == Uri.UriSchemeFtp)
                {
                    return true;
                }
            }

            // Fallback to regex
            return UrlRegex.IsMatch(url);
        }

        [GeneratedRegex(@"^(https?|ftp)://[^\s/$.?#].[^\s]*$", RegexOptions.IgnoreCase | RegexOptions.Compiled, "en-GB")]
        private static partial Regex MyRegex();
    }
}
