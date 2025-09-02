using FluentAssertions;
using Chota.Api.Services;

namespace Chota.Tests.Api.Services;

public class Base62UrlEncoderTests
{
    private readonly Base62Encoder _encoder = new();

    [Test]
    public void Encode_WithZero_ShouldReturnZero()
    {
        var result = _encoder.Encode(0);

        result.Should().Be("0");
    }

    [Test]
    public void Encode_WithPositiveNumber_ShouldReturnValidBase62String()
    {
        var result = _encoder.Encode(123456789);

        result.Should().NotBeEmpty();
        result.Should().MatchRegex(@"^[a-zA-Z0-9]+$");
    }

    [Test]
    public void Decode_WithEmptyString_ShouldReturnZero()
    {
        var result = _encoder.Decode("");

        result.Should().Be(0);
    }

    [Test]
    public void Decode_WithNullString_ShouldReturnZero()
    {
        var result = _encoder.Decode(null!);

        result.Should().Be(0);
    }

    [Test]
    public void Decode_WithInvalidCharacter_ShouldThrowArgumentException()
    {
        var invalidEncoded = "abc123+";

        var action = () => _encoder.Decode(invalidEncoded);

        action.Should().Throw<ArgumentException>()
            .WithMessage("*Invalid Base62 character*");
    }

    [Test]
    public void EncodeAndDecode_ShouldRoundTripCorrectly()
    {
        var testValues = new long[] { 1, 62, 123, 3844, 123456789, long.MaxValue };

        foreach (var value in testValues)
        {
            var encoded = _encoder.Encode(value);
            var decoded = _encoder.Decode(encoded);

            decoded.Should().Be(value, $"Failed for value {value}");
        }
    }

    [Test]
    public void Encode_WithSameInput_ShouldReturnSameOutput()
    {
        const long input = 123456789;

        var result1 = _encoder.Encode(input);
        var result2 = _encoder.Encode(input);

        result1.Should().Be(result2);
    }

    [Test]
    public void Encode_WithDifferentInputs_ShouldReturnDifferentOutputs()
    {
        var encoded1 = _encoder.Encode(123);
        var encoded2 = _encoder.Encode(456);

        encoded1.Should().NotBe(encoded2);
    }

    [Test]
    public void Encode_WithSequentialIds_ShouldProduceValidCodes()
    {
        for (long i = 1; i <= 1000; i++)
        {
            var encoded = _encoder.Encode(i);

            encoded.Should().NotBeEmpty();
            encoded.Should().MatchRegex(@"^[a-zA-Z0-9]+$");

            var decoded = _encoder.Decode(encoded);
            decoded.Should().Be(i);
        }
    }

    [Test]
    public void Performance_EncodeAndDecode_ShouldCompleteQuickly()
    {
        var stopwatch = System.Diagnostics.Stopwatch.StartNew();

        for (var i = 1; i <= 10000; i++)
        {
            var encoded = _encoder.Encode(i);
            var decoded = _encoder.Decode(encoded);

            decoded.Should().Be(i);
        }

        stopwatch.Stop();

        stopwatch.ElapsedMilliseconds.Should().BeLessThan(1000);
    }

    [Test]
    public void Encode_WithSnowflakeTypicalId_ShouldProduceShortCode()
    {
        // Typical Snowflake ID from 2024
        const long snowflakeId = 1852378944123456789L;

        var encoded = _encoder.Encode(snowflakeId);

        encoded.Length.Should().BeLessThan(12, "Snowflake IDs should produce short codes");
        encoded.Should().MatchRegex(@"^[a-zA-Z0-9]+$");
    }

    [Test]
    public void Performance_SnowflakeEncoding_ShouldBeFast()
    {
        const int iterations = 10000;
        var snowflakeId = 1852378944123456789L;

        var stopwatch = System.Diagnostics.Stopwatch.StartNew();
        for (var i = 0; i < iterations; i++)
        {
            _encoder.Encode(snowflakeId + i);
        }
        stopwatch.Stop();

        stopwatch.ElapsedMilliseconds.Should().BeLessThan(100, "Should encode 10,000 Snowflake IDs in under 100ms");
    }

    [Test]
    public void Encode_WithMaxValue_ShouldWork()
    {
        var encoded = _encoder.Encode(long.MaxValue);
        var decoded = _encoder.Decode(encoded);

        decoded.Should().Be(long.MaxValue);
    }

    [Test]
    public void Encode_ProducesExpectedLength()
    {
        // Test that encoded values have reasonable lengths
        _encoder.Encode(1).Length.Should().Be(1);
        _encoder.Encode(61).Length.Should().Be(1);
        _encoder.Encode(62).Length.Should().Be(2);
        _encoder.Encode(3843).Length.Should().Be(2);
        _encoder.Encode(3844).Length.Should().Be(3);
    }
}