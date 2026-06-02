namespace PicoLog.Tests;

public sealed class SeqSinkTests
{
    private sealed class FakeHttpHandler(Func<HttpRequestMessage, HttpResponseMessage> handler)
        : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request,
            CancellationToken ct
        ) => Task.FromResult(handler(request));
    }

    [Test]
    public async Task WriteBatchAsync_SendsCompactJson_ToCorrectEndpoint()
    {
        string? capturedBody = null;
        var handler = new FakeHttpHandler(req =>
        {
            capturedBody = req.Content!.ReadAsStringAsync().GetAwaiter().GetResult();
            return new HttpResponseMessage(HttpStatusCode.Created);
        });
        var httpClient = new HttpClient(handler) { BaseAddress = new Uri("http://localhost:5341") };
        var sink = new SeqSink(httpClient);

        var batch = new List<LogEntry>
        {
            new LogEntry
            {
                Timestamp = new DateTimeOffset(2026, 1, 1, 0, 0, 0, TimeSpan.Zero),
                Level = LogLevel.Info,
                Message = "Hello Seq"
            }
        };

        await sink.WriteBatchAsync(batch);
        // Force flush to send
        await sink.FlushAsync();

        await Assert.That(capturedBody).IsNotNull();
        await Assert.That(capturedBody!).Contains("\"@t\"");
        await Assert.That(capturedBody!).Contains("Hello Seq");
    }

    [Test]
    public async Task WriteBatchAsync_SendsApiKeyHeader_WhenConfigured()
    {
        string? capturedApiKey = null;
        var handler = new FakeHttpHandler(req =>
        {
            capturedApiKey = req.Headers.GetValues("X-Seq-ApiKey").FirstOrDefault();
            return new HttpResponseMessage(HttpStatusCode.Created);
        });
        var httpClient = new HttpClient(handler) { BaseAddress = new Uri("http://localhost:5341") };
        var sink = new SeqSink(httpClient, apiKey: "test-key");

        await sink.WriteBatchAsync([new LogEntry { Message = "test" }]);
        await sink.FlushAsync();

        await Assert.That(capturedApiKey).IsEqualTo("test-key");
    }

    [Test]
    public async Task FlushAsync_SendsBufferedEntries()
    {
        string? body = null;
        var handler = new FakeHttpHandler(req =>
        {
            body = req.Content!.ReadAsStringAsync().GetAwaiter().GetResult();
            return new HttpResponseMessage(HttpStatusCode.Created);
        });
        var httpClient = new HttpClient(handler) { BaseAddress = new Uri("http://localhost:5341") };
        var sink = new SeqSink(httpClient);

        await sink.WriteAsync(new LogEntry { Message = "one" });
        await sink.WriteAsync(new LogEntry { Message = "two" });
        await sink.FlushAsync();

        await Assert.That(body).IsNotNull();
        await Assert.That(body!).Contains("one");
        await Assert.That(body!).Contains("two");
    }

    [Test]
    public async Task HttpFailure_IncrementsFailureCount_AndFallsBack()
    {
        var handler = new FakeHttpHandler(
            _ => new HttpResponseMessage(HttpStatusCode.ServiceUnavailable)
        );
        var httpClient = new HttpClient(handler) { BaseAddress = new Uri("http://localhost:5341") };
        var sink = new SeqSink(httpClient, enableConsoleFallback: true);

        await sink.WriteBatchAsync([new LogEntry { Message = "fail" }]);
        await sink.FlushAsync();

        await Assert.That(sink.FailureCount).IsGreaterThan(0);
        await Assert.That(sink.LastFailureTime).IsNotNull();
    }

    [Test]
    public async Task PeriodicFlush_SendsEntriesBelowBatchThreshold()
    {
        string? body = null;
        var signal = new TaskCompletionSource();
        var handler = new FakeHttpHandler(req =>
        {
            body = req.Content!.ReadAsStringAsync().GetAwaiter().GetResult();
            signal.TrySetResult();
            return new HttpResponseMessage(HttpStatusCode.Created);
        });
        var httpClient = new HttpClient(handler) { BaseAddress = new Uri("http://localhost:5341") };
        var sink = new SeqSink(httpClient, flushInterval: TimeSpan.FromMilliseconds(100));

        // Write a single entry (below batch threshold)
        await sink.WriteAsync(new LogEntry { Message = "periodic-flush" });

        // Wait for the periodic timer to trigger (up to 5 seconds)
        var completed = await Task.WhenAny(signal.Task, Task.Delay(TimeSpan.FromSeconds(5)));
        await Assert.That(completed).IsEqualTo(signal.Task);
        await Assert.That(body).IsNotNull();
        await Assert.That(body!).Contains("periodic-flush");

        await sink.DisposeAsync();
    }

    [Test]
    public async Task DisposeAsync_FlushesRemainingEntries()
    {
        string? body = null;
        var handler = new FakeHttpHandler(req =>
        {
            body = req.Content!.ReadAsStringAsync().GetAwaiter().GetResult();
            return new HttpResponseMessage(HttpStatusCode.Created);
        });
        var httpClient = new HttpClient(handler) { BaseAddress = new Uri("http://localhost:5341") };
        var sink = new SeqSink(httpClient);

        await sink.WriteAsync(new LogEntry { Message = "dispose-test" });
        await sink.DisposeAsync();

        await Assert.That(body).IsNotNull();
        await Assert.That(body!).Contains("dispose-test");
    }
}
