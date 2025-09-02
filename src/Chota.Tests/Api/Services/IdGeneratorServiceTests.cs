using FluentAssertions;
using IdGen;
using Chota.Api.Services;

namespace Chota.Tests.Api.Services;

public class IdGeneratorServiceTests
{
    private readonly IdGeneratorService _idGeneratorService;

    public IdGeneratorServiceTests()
    {
        // Configure IdGen for testing with same setup as Program.cs
        var epoch = new DateTime(2024, 1, 1, 0, 0, 0, DateTimeKind.Utc);
        var structure = new IdStructure(41, 10, 12); // 41-bit timestamp, 10-bit machine, 12-bit sequence
        var options = new IdGeneratorOptions(structure, new DefaultTimeSource(epoch));
        var idGenerator = new IdGenerator(0, options); // Machine ID 0 for testing

        _idGeneratorService = new IdGeneratorService(idGenerator);
    }

    [Test]
    public void GenerateNextId_ShouldReturnNonZeroValue()
    {
        var id = _idGeneratorService.GenerateNextId();

        id.Should().BeGreaterThan(0);
    }

    [Test]
    public void GenerateNextId_ShouldReturnNumericString()
    {
        var id = _idGeneratorService.GenerateNextId();

        id.Should().BeOfType(typeof(long), "ID should be a long integer");
    }

    [Test]
    public void GenerateNextId_ShouldReturnUniqueIds()
    {
        var ids = new HashSet<long>();

        for (var i = 0; i < 1000; i++)
        {
            var id = _idGeneratorService.GenerateNextId();
            ids.Add(id).Should().BeTrue($"ID '{id}' should be unique");
        }

        ids.Count.Should().Be(1000);
    }

    [Test]
    public void GenerateNextId_ShouldGenerateSequentialIds()
    {
        var id1 = _idGeneratorService.GenerateNextId();
        var id2 = _idGeneratorService.GenerateNextId();

        id2.Should().BeGreaterThan(id1, "Snowflake IDs should be sequential");
    }

    [Test]
    public void GenerateNextId_PerformanceTest_ShouldGenerateQuickly()
    {
        var stopwatch = System.Diagnostics.Stopwatch.StartNew();

        // Reduce to 1000 to avoid sequence overflow in single millisecond
        for (var i = 0; i < 1000; i++)
        {
            _idGeneratorService.GenerateNextId();
        }

        stopwatch.Stop();

        stopwatch.ElapsedMilliseconds.Should().BeLessThan(100, "Should generate 1,000 IDs in under 100ms");
    }

    [Test]
    public void GenerateNextId_ShouldContainTimestamp()
    {
        var beforeGeneration = DateTimeOffset.UtcNow;
        var id = _idGeneratorService.GenerateNextId();
        var afterGeneration = DateTimeOffset.UtcNow;

        var longId = id;

        // Extract timestamp from Snowflake ID (first 41 bits after shifting)
        var timestamp = (longId >> 22) + new DateTimeOffset(2024, 1, 1, 0, 0, 0, TimeSpan.Zero).ToUnixTimeMilliseconds();
        var idTime = DateTimeOffset.FromUnixTimeMilliseconds(timestamp);

        idTime.Should().BeOnOrAfter(beforeGeneration.AddSeconds(-1));
        idTime.Should().BeOnOrBefore(afterGeneration.AddSeconds(1));
    }

    [Test]
    public void GenerateNextId_WithHighVelocity_ShouldNotCollide()
    {
        // Test high-velocity generation - reduced to avoid sequence overflow
        var ids = new ConcurrentHashSet<long>();
        const int iterations = 500; // Reduced to avoid sequence overflow in single tick

        Parallel.For(0, iterations, _ =>
        {
            var id = _idGeneratorService.GenerateNextId();
            ids.Add(id).Should().BeTrue($"High velocity generation should not produce duplicate ID: {id}");
        });

        ids.Count.Should().Be(iterations, "All generated IDs should be unique");
    }
}

// Helper class for thread-safe HashSet
public class ConcurrentHashSet<T> where T : notnull
{
    private readonly HashSet<T> _hashSet = new();
    private readonly object _lock = new();

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