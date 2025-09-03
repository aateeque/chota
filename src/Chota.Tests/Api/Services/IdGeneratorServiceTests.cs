using FluentAssertions;
using IdGen;
using Chota.Api.Services;

namespace Chota.Tests.Api.Services;

public class IdGeneratorServiceTests
{
    public IdGeneratorService IdGeneratorService { get; set; }

    public IdGeneratorServiceTests()
    {
        // Configure IdGen for testing with same setup as Program.cs
        var epoch = new DateTime(2024, 1, 1, 0, 0, 0, DateTimeKind.Utc);
        var structure = new IdStructure(41, 10, 12); // 41-bit timestamp, 10-bit machine, 12-bit sequence
        var options = new IdGeneratorOptions(structure, new DefaultTimeSource(epoch));
        var idGenerator = new IdGenerator(0, options); // Machine ID 0 for testing

        IdGeneratorService = new IdGeneratorService(idGenerator);
    }

    public class TheGenerateNextIdMethod : IdGeneratorServiceTests
    {

        [Test]
        public void ShouldReturnNonZeroValue()
        {
            // Arrange & Act
            var id = IdGeneratorService.GenerateNextId();

            // Assert
            id.Should().BeGreaterThan(0);
        }

        [Test]
        public void ShouldReturnNumericString()
        {
            // Arrange & Act
            var id = IdGeneratorService.GenerateNextId();

            // Assert
            id.Should().BeOfType(typeof(long), "ID should be a long integer");
        }

        [Test]
        public void ShouldReturnUniqueIds()
        {
            // Arrange
            var ids = new HashSet<long>();

            // Act & Assert
            for (var i = 0; i < 1000; i++)
            {
                var id = IdGeneratorService.GenerateNextId();
                ids.Add(id).Should().BeTrue($"ID '{id}' should be unique");
            }

            ids.Count.Should().Be(1000);
        }

        [Test]
        public void ShouldGenerateSequentialIds()
        {
            // Arrange & Act
            var id1 = IdGeneratorService.GenerateNextId();
            var id2 = IdGeneratorService.GenerateNextId();

            // Assert
            id2.Should().BeGreaterThan(id1, "Snowflake IDs should be sequential");
        }

        [Test]
        public void PerformanceTest_ShouldGenerateQuickly()
        {
            // Arrange
            var stopwatch = System.Diagnostics.Stopwatch.StartNew();

            // Act
            for (var i = 0; i < 1000; i++)
            {
                IdGeneratorService.GenerateNextId();
            }

            stopwatch.Stop();

            // Assert
            stopwatch.ElapsedMilliseconds.Should().BeLessThan(100, "Should generate 1,000 IDs in under 100ms");
        }

        [Test]
        public void ShouldContainTimestamp()
        {
            // Arrange
            var beforeGeneration = DateTimeOffset.UtcNow;

            // Act
            var id = IdGeneratorService.GenerateNextId();

            var afterGeneration = DateTimeOffset.UtcNow;
            var longId = id;
            // Extract timestamp from Snowflake ID (first 41 bits after shifting)
            var timestamp = (longId >> 22) +
                            new DateTimeOffset(2024, 1, 1, 0, 0, 0, TimeSpan.Zero).ToUnixTimeMilliseconds();
            var idTime = DateTimeOffset.FromUnixTimeMilliseconds(timestamp);

            // Assert
            idTime.Should().BeOnOrAfter(beforeGeneration.AddSeconds(-1));
            idTime.Should().BeOnOrBefore(afterGeneration.AddSeconds(1));
        }

        [Test]
        public void WithHighVelocity_ShouldNotCollide()
        {
            // Arrange
            // Test high-velocity generation - reduced to avoid sequence overflow
            var ids = new ConcurrentHashSet<long>();
            const int iterations = 500;

            // Act & Assert
            Parallel.For(0, iterations, _ =>
            {
                var id = IdGeneratorService.GenerateNextId();
                ids.Add(id).Should().BeTrue($"High velocity generation should not produce duplicate ID: {id}");
            });

            ids.Count.Should().Be(iterations, "All generated IDs should be unique");
        }
    }


    public class TheHashLongUrlMethod : IdGeneratorServiceTests
    {
        [Test]
        public void ShouldReturnHash()
        {

            // Arrange
            const string url = "https://www.google.com/search?q=geta+long+url+string+from+google&oq=geta++long+url+string+from+google+&gs_lcrp=EgRlZGdlKgYIABBFGDkyBggAEEUYOdIBCDUwNzFqMGoxqAIAsAIA&sourceid=chrome&ie=UTF-8&safe=active&ssui=on";

            // Act
            var hash = IdGeneratorService.HashLongUrl(url);

            // Assert
            hash.Should().Be("c8319664e795238913cb35e305a133b9");
        }
    }

    // Helper class for thread-safe HashSet
    private class ConcurrentHashSet<T> where T : notnull
    {
        private readonly HashSet<T> _hashSet = [];
        private readonly Lock _lock = new();

        public bool Add(T item)
        {
            lock (_lock)
            {
                return _hashSet.Add(item);
            }
        }

        public int Count
        {
            get
            {
                lock (_lock)
                {
                    return _hashSet.Count;
                }
            }
        }
    }
}
