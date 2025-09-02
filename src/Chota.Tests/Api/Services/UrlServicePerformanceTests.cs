//using System.Diagnostics;
//using NSubstitute;
//using Chota.Api.Common;
//using Chota.Api.Models;
//using Chota.Api.Services;

//namespace Chota.Tests.Api.Services;

//public class UrlServicePerformanceTests
//{
//    private readonly IUrlRepository _mockRepository = Substitute.For<IUrlRepository>();
//    private readonly IIdGeneratorService _mockIdGenerator = Substitute.For<IIdGeneratorService>();
//    private readonly IUrlEncoder _mockEncoder = Substitute.For<IUrlEncoder>();
//    private readonly IUrlValidator _mockValidator = Substitute.For<IUrlValidator>();
//    private readonly UrlService _service;

//    public UrlServicePerformanceTests()
//    {
//        _service = new UrlService(_mockRepository, _mockIdGenerator, _mockEncoder, _mockValidator);

//        // Setup default mock behaviors for performance testing
//        _mockValidator.IsValid(Arg.Any<string>()).Returns(true);
//        _mockRepository.GetByLongUrl(Arg.Any<string>()).Returns((ShortUrl?)null);
//        _mockIdGenerator.GenerateNextId().Returns(12345L);
//        _mockEncoder.Encode(Arg.Any<long>()).Returns("abc123");
//        _mockRepository.Save(Arg.Any<ShortUrl>()).Returns(Task.CompletedTask);
//    }

//    [Test]
//    public async Task Shorten_SingleOperation_CompletesUnder25Milliseconds()
//    {
//        var stopwatch = Stopwatch.StartNew();

//        await _service.Shorten("https://example.com");

//        stopwatch.Stop();
//        await Assert.That(stopwatch.ElapsedMilliseconds).IsLessThan(25);
//    }

//    [Test]
//    public async Task Shorten_100ConcurrentOperations_CompletesUnder500Milliseconds()
//    {
//        const int operationCount = 100;
//        var tasks = new List<Task<Result<string>>>();
//        var stopwatch = Stopwatch.StartNew();

//        for (var i = 0; i < operationCount; i++)
//        {
//            var url = $"https://example{i}.com";
//            tasks.Add(_service.Shorten(url));
//        }

//        await Task.WhenAll(tasks);
//        stopwatch.Stop();

//        await Assert.That(stopwatch.ElapsedMilliseconds).IsLessThan(500);
//        await Assert.That(tasks.All(t => t.Result.IsSuccess)).IsTrue();
//    }

//    [Test]
//    public async Task Shorten_1000Operations_AverageLatencyUnder10Milliseconds()
//    {
//        const int operationCount = 1000;
//        var latencies = new List<long>();

//        for (var i = 0; i < operationCount; i++)
//        {
//            var stopwatch = Stopwatch.StartNew();
//            await _service.Shorten($"https://example{i}.com");
//            stopwatch.Stop();
//            latencies.Add(stopwatch.ElapsedMilliseconds);
//        }

//        var averageLatency = latencies.Average();
//        await Assert.That(averageLatency).IsLessThan(10);
//    }

//    [Test]
//    public async Task GetByShortCode_SingleOperation_CompletesUnder5Milliseconds()
//    {
//        var shortUrl = new ShortUrl(1L, "https://example.com", "abc123", DateTime.UtcNow);
//        _mockRepository.GetByShortCode(Arg.Any<string>()).Returns(shortUrl);

//        var stopwatch = Stopwatch.StartNew();

//        await _service.GetByShortCode("abc123");

//        stopwatch.Stop();
//        await Assert.That(stopwatch.ElapsedMilliseconds).IsLessThan(5);
//    }

//    [Test]
//    public async Task GetByShortCode_1000ConcurrentOperations_CompletesUnder200Milliseconds()
//    {
//        const int operationCount = 1000;
//        var shortUrl = new ShortUrl(1L, "https://example.com", "abc123", DateTime.UtcNow);
//        _mockRepository.GetByShortCode(Arg.Any<string>()).Returns(shortUrl);

//        var tasks = new List<Task<Result<ShortUrl>>>();
//        var stopwatch = Stopwatch.StartNew();

//        for (var i = 0; i < operationCount; i++)
//        {
//            tasks.Add(_service.GetByShortCode("abc123"));
//        }

//        await Task.WhenAll(tasks);
//        stopwatch.Stop();

//        await Assert.That(stopwatch.ElapsedMilliseconds).IsLessThan(200);
//        await Assert.That(tasks.All(t => t.Result.IsSuccess)).IsTrue();
//    }

//    [Test]
//    public async Task Shorten_MemoryUsage_StaysUnder100MB()
//    {
//        const int operationCount = 10000;
//        var initialMemory = GC.GetTotalMemory(true);

//        var tasks = new List<Task<Result<string>>>();
//        for (var i = 0; i < operationCount; i++)
//        {
//            tasks.Add(_service.Shorten($"https://example{i}.com"));
//        }

//        await Task.WhenAll(tasks);

//        var finalMemory = GC.GetTotalMemory(true);
//        var memoryUsedMB = (finalMemory - initialMemory) / (1024.0 * 1024.0);

//        await Assert.That(memoryUsedMB).IsLessThan(100);
//    }

//    [Test]
//    public async Task Shorten_ThroughputTest_Achieves1160OperationsPerSecond()
//    {
//        const int testDurationSeconds = 5;
//        const int targetThroughput = 1160; // From NFRs: 1160 write operations per second
//        var operationCount = 0;
//        var stopwatch = Stopwatch.StartNew();

//        while (stopwatch.Elapsed.TotalSeconds < testDurationSeconds)
//        {
//            await _service.Shorten($"https://example{operationCount}.com");
//            operationCount++;
//        }

//        stopwatch.Stop();
//        var actualThroughput = operationCount / stopwatch.Elapsed.TotalSeconds;

//        await Assert.That(actualThroughput).IsGreaterThanOrEqualTo(targetThroughput);
//    }

//    [Test]
//    public async Task GetByShortCode_ThroughputTest_Achieves11600OperationsPerSecond()
//    {
//        const int testDurationSeconds = 5;
//        const int targetThroughput = 11600; // From NFRs: 11600 read operations per second

//        var shortUrl = new ShortUrl(1L, "https://example.com", "abc123", DateTime.UtcNow);
//        _mockRepository.GetByShortCode(Arg.Any<string>()).Returns(shortUrl);

//        var operationCount = 0;
//        var stopwatch = Stopwatch.StartNew();

//        while (stopwatch.Elapsed.TotalSeconds < testDurationSeconds)
//        {
//            await _service.GetByShortCode("abc123");
//            operationCount++;
//        }

//        stopwatch.Stop();
//        var actualThroughput = operationCount / stopwatch.Elapsed.TotalSeconds;

//        await Assert.That(actualThroughput).IsGreaterThanOrEqualTo(targetThroughput);
//    }

//    [Test]
//    public async Task Shorten_P99Latency_Under50Milliseconds()
//    {
//        const int operationCount = 1000;
//        var latencies = new List<long>();

//        for (var i = 0; i < operationCount; i++)
//        {
//            var stopwatch = Stopwatch.StartNew();
//            await _service.Shorten($"https://example{i}.com");
//            stopwatch.Stop();
//            latencies.Add(stopwatch.ElapsedMilliseconds);
//        }

//        latencies.Sort();
//        var p99Index = (int)(operationCount * 0.99);
//        var p99Latency = latencies[p99Index];

//        await Assert.That(p99Latency).IsLessThan(50);
//    }
//}