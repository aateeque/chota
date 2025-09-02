using FluentAssertions;
using Chota.Api.Services;

namespace Chota.Tests.Api.Services;

public class UrlValidatorTests
{
    private readonly UrlValidator _validator = new();

    [Test]
    [Arguments("https://example.com")]
    [Arguments("http://example.com")]
    [Arguments("https://www.example.com")]
    [Arguments("http://subdomain.example.com")]
    [Arguments("https://example.com/path")]
    [Arguments("https://example.com/path/to/resource")]
    [Arguments("https://example.com:8080")]
    [Arguments("https://example.com?query=value")]
    [Arguments("https://example.com#fragment")]
    [Arguments("https://example.com/path?query=value&param=test#fragment")]
    public void IsValid_ValidUrls_ReturnsTrue(string url)
    {
        var result = _validator.IsValid(url);
        result.Should().BeTrue();
    }

    [Test]
    [Arguments("")]
    [Arguments(" ")]
    [Arguments("  ")]
    [Arguments("\t")]
    [Arguments("\n")]
    public async Task IsValid_NullOrWhitespaceUrls_ReturnsFalse(string url)
    {
        var result = _validator.IsValid(url);
        await Assert.That(result).IsFalse();
    }

    [Test]
    [Arguments("javascript:alert('xss')")]
    [Arguments("file:///etc/passwd")]
    [Arguments("data:text/html,<script>alert('xss')</script>")]
    [Arguments("mailto:user@example.com")]
    [Arguments("tel:+1234567890")]
    [Arguments("ssh://user@server.com")]
    [Arguments("ldap://server.com")]
    [Arguments("ftp://example.com")]
    public async Task IsValid_UnsupportedSchemes_ReturnsFalse(string url)
    {
        var result = _validator.IsValid(url);
        await Assert.That(result).IsFalse();
    }

    [Test]
    [Arguments("not-a-url")]
    [Arguments("example.com")]
    [Arguments("www.example.com")]
    [Arguments("//example.com")]
    [Arguments("http://")]
    [Arguments("https://")]
    [Arguments("ftp://")]
    [Arguments("http:// ")]
    [Arguments("https:// example.com")]
    public async Task IsValid_MalformedUrls_ReturnsFalse(string url)
    {
        var result = _validator.IsValid(url);
        await Assert.That(result).IsFalse();
    }

    [Test]
    [Arguments("https://192.168.1.1")]
    [Arguments("http://127.0.0.1:3000")]
    [Arguments("https://[2001:db8::1]")]
    public async Task IsValid_IpAddresses_ReturnsTrue(string url)
    {
        var result = _validator.IsValid(url);
        await Assert.That(result).IsTrue();
    }

    [Test]
    [Arguments("https://example.com/path with spaces")]
    [Arguments("https://example.com/path%20encoded")]
    [Arguments("https://example.com/ñ")]
    [Arguments("https://example.com/测试")]
    [Arguments("https://тест.com")]
    public async Task IsValid_UrlsWithSpecialCharacters_ReturnsTrue(string url)
    {
        var result = _validator.IsValid(url);
        await Assert.That(result).IsTrue();
    }

    [Test]
    [Arguments("HTTPS://EXAMPLE.COM")]
    [Arguments("HTTP://EXAMPLE.COM")]
    [Arguments("HtTpS://ExAmPlE.CoM")]
    public async Task IsValid_CaseInsensitiveSchemes_ReturnsTrue(string url)
    {
        var result = _validator.IsValid(url);
        await Assert.That(result).IsTrue();
    }

    [Test]
    [Arguments("https://localhost")]
    [Arguments("http://localhost:8080")]
    [Arguments("https://dev.local")]
    [Arguments("http://api.staging")]
    public async Task IsValid_LocalDevelopmentUrls_ReturnsTrue(string url)
    {
        var result = _validator.IsValid(url);
        await Assert.That(result).IsTrue();
    }

    [Test]
    public async Task IsValid_NullUrl_ReturnsFalse()
    {
        var result = _validator.IsValid(null);
        await Assert.That(result).IsFalse();
    }

    [Test]
    public async Task IsValid_VeryLongValidUrl_ReturnsTrue()
    {
        var longPath = new string('a', 2000);
        var url = $"https://example.com/{longPath}";

        var result = _validator.IsValid(url);
        await Assert.That(result).IsTrue();
    }

    [Test]
    [Arguments("https://sub1.sub2.sub3.example.com")]
    public async Task IsValid_DeepSubdomainsAndPaths_ReturnsTrue(string url)
    {
        var result = _validator.IsValid(url);
        await Assert.That(result).IsTrue();
    }

    [Test]
    [Arguments("http://example..com", true)]
    [Arguments("https://.example.com", false)]
    [Arguments("https://example.com.", true)]
    [Arguments("https://example-.com", true)]
    [Arguments("https://-example.com", true)]
    public async Task IsValid_EdgeCaseDomains_ReturnsExpectedResult(string url, bool expected)
    {
        var result = _validator.IsValid(url);
        await Assert.That(result).IsEqualTo(expected);
    }
}