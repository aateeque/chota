using AutoFixture;
using AutoFixture.AutoNSubstitute;
using FluentAssertions;
using NSubstitute;
using Chota.Api.Models;
using Chota.Api.Services;

namespace Chota.Tests.Api.Services;

public sealed class UrlServiceTests
{
    private readonly IUrlRepository _urlRepository;
    private readonly IUrlEncoder _urlEncoder;
    private readonly IUrlValidator _urlValidator;
    private readonly IIdGeneratorService _idGeneratorService;
    private readonly Fixture _fixture;

    public UrlService UrlService { get; set; }

    public UrlServiceTests()
    {
        _fixture = new Fixture();
        _fixture.Customize(new AutoNSubstituteCustomization
        {
            ConfigureMembers = true
        });

        _urlRepository = _fixture.Freeze<IUrlRepository>();
        _urlEncoder = _fixture.Freeze<IUrlEncoder>();
        _urlValidator = _fixture.Freeze<IUrlValidator>();
        _idGeneratorService = _fixture.Freeze<IIdGeneratorService>();

        UrlService = _fixture.Create<UrlService>();
    }

    [Test]
    public async Task Shorten_WithValidUrl_ShouldReturnShortCode()
    {
        // Arrange
        const string longUrl = "https://example.com";
        const string expectedCode = "abc123";

        _urlValidator.IsValid(longUrl)
            .Returns(true);

        _urlRepository.GetByLongUrl(Arg.Any<string>())
            .Returns((ShortUrl)null);

        _idGeneratorService.HashLongUrl(Arg.Any<string>())
            .Returns("12345");

        const long randomId = 123456789L;
        _idGeneratorService.GenerateNextId()
            .Returns(randomId);

        _urlEncoder.Encode(Arg.Any<long>())
            .Returns(expectedCode);

        // Act
        var result = await UrlService.Shorten(longUrl);

        // Assert
        _urlValidator.Received(1).IsValid(Arg.Any<string>());
        await _urlRepository.Received(1).GetByLongUrl(Arg.Any<string>());
        _idGeneratorService.Received(1).GenerateNextId();
        _urlEncoder.Received(1).Encode(randomId);

        result.IsSuccess.Should().BeTrue();
        result.Value.ShortCode.Should().Be(expectedCode);
    }
}
