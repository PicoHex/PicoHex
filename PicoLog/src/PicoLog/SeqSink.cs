namespace PicoLog;

public sealed class SeqSink : IBatchingLogSink, IFlushableLogSink
{
    private const int MaxBatchEntries = 100;
    private const int MaxBatchBytes = 256 * 1024;
    private static readonly TimeSpan DefaultFlushInterval = TimeSpan.FromSeconds(2);
    private const int DefaultMaxRetries = 3;
    private const int DefaultRetryBaseDelayMs = 100;

    private readonly HttpClient _httpClient;
    private readonly string _apiKey;
    private readonly Lock _bufferLock = new();
    private readonly List<LogEntry> _buffer = [];
    private readonly TimeSpan _flushInterval;
    private readonly int _maxRetries;
    private readonly int _retryBaseDelayMs;
    private readonly CancellationTokenSource _timerCts = new();
    private readonly Task _timerTask;
    private long _bufferBytes;
    private long _failureCount;
    private long _lastFailureTicks;
    private readonly bool _enableConsoleFallback;
    private int _disposed;

    public long FailureCount => Interlocked.Read(ref _failureCount);
    public DateTimeOffset? LastFailureTime =>
        Interlocked.Read(ref _lastFailureTicks) == 0
            ? null
            : new DateTimeOffset(Interlocked.Read(ref _lastFailureTicks), TimeSpan.Zero);

    public SeqSink(
        HttpClient httpClient,
        string? apiKey = null,
        TimeSpan? flushInterval = null,
        bool enableConsoleFallback = false,
        int maxRetries = DefaultMaxRetries,
        int retryBaseDelayMs = DefaultRetryBaseDelayMs
    )
    {
        _httpClient = httpClient ?? throw new ArgumentNullException(nameof(httpClient));
        _apiKey = apiKey ?? string.Empty;
        _flushInterval = flushInterval ?? DefaultFlushInterval;
        _enableConsoleFallback = enableConsoleFallback;
        _maxRetries = maxRetries > 0 ? maxRetries : DefaultMaxRetries;
        _retryBaseDelayMs = retryBaseDelayMs > 0 ? retryBaseDelayMs : DefaultRetryBaseDelayMs;
        _timerTask = RunPeriodicFlushAsync(_timerCts.Token);
    }

    public Task WriteAsync(LogEntry entry, CancellationToken ct = default)
    {
        BufferEntries([entry]);
        return Task.CompletedTask;
    }

    public ValueTask WriteBatchAsync(IReadOnlyList<LogEntry> batch, CancellationToken ct = default)
    {
        BufferEntries(batch);
        return ValueTask.CompletedTask;
    }

    public async ValueTask FlushAsync(CancellationToken ct = default)
    {
        IReadOnlyList<LogEntry> batch;
        lock (_bufferLock)
        {
            if (_buffer.Count == 0)
                return;
            batch = DrainBuffer();
        }
        await SendBatchAsync(batch, ct).ConfigureAwait(false);
    }

    private async Task RunPeriodicFlushAsync(CancellationToken ct)
    {
        while (!ct.IsCancellationRequested)
        {
            try
            {
                await Task.Delay(_flushInterval, ct).ConfigureAwait(false);
            }
            catch (OperationCanceledException)
            {
                break;
            }

            try
            {
                await DrainAndSendAsync().ConfigureAwait(false);
            }
            catch
            {
                // Best-effort: errors are logged inside DrainAndSendAsync
            }
        }
    }

    public async ValueTask DisposeAsync()
    {
        if (Interlocked.Exchange(ref _disposed, 1) != 0)
            return;
        _timerCts.Cancel();
        try
        {
            await _timerTask.ConfigureAwait(false);
        }
        catch (OperationCanceledException) { }
        await FlushAsync().ConfigureAwait(false);
        _timerCts.Dispose();
        _httpClient.Dispose();
    }

    private void BufferEntries(IReadOnlyList<LogEntry> entries)
    {
        lock (_bufferLock)
        {
            foreach (var entry in entries)
            {
                _buffer.Add(entry);
                _bufferBytes += EstimateEntryBytes(entry);
            }

            if (_buffer.Count >= MaxBatchEntries || _bufferBytes >= MaxBatchBytes)
                _ = SafeDrainAsync();
        }
    }

    /// <summary>
    /// Fire-and-forget drain that observes exceptions via await.
    /// Unlike a raw <c>_ = DrainAndSendAsync()</c>, exceptions are caught and
    /// logged here rather than becoming unobserved task exceptions.
    /// </summary>
    private async Task SafeDrainAsync()
    {
        try
        {
            await DrainAndSendAsync().ConfigureAwait(false);
        }
        catch (Exception ex)
        {
            Trace.WriteLine($"[SeqSink] SafeDrain error: {ex}");
        }
    }

    private static long EstimateEntryBytes(LogEntry entry) =>
        (entry.Message?.Length ?? 0) * 2 + 128;

    private async Task DrainAndSendAsync()
    {
        IReadOnlyList<LogEntry> batch;
        lock (_bufferLock)
        {
            if (_buffer.Count == 0)
                return;
            batch = DrainBuffer();
        }
        await SendBatchAsync(batch, CancellationToken.None).ConfigureAwait(false);
    }

    private IReadOnlyList<LogEntry> DrainBuffer()
    {
        var result = _buffer.ToArray();
        _buffer.Clear();
        _bufferBytes = 0;
        return result;
    }

    private async Task SendBatchAsync(IReadOnlyList<LogEntry> batch, CancellationToken ct)
    {
        // Serialize once — the JSON is immutable across retries.
        var json = SerializeBatch(batch);

        for (var attempt = 0; attempt < _maxRetries; attempt++)
        {
            // HttpRequestMessage and StringContent are single-use;
            // recreate them for every attempt.
            var content = new StringContent(json, Encoding.UTF8, "application/json");
            using var request = new HttpRequestMessage(HttpMethod.Post, "/api/events/raw")
            {
                Content = content,
            };

            if (_apiKey.Length > 0)
                request.Headers.Add("X-Seq-ApiKey", _apiKey);

            try
            {
                var response = await _httpClient.SendAsync(request, ct).ConfigureAwait(false);
                response.EnsureSuccessStatusCode();
                Interlocked.Exchange(ref _failureCount, 0);
                return;
            }
            catch (Exception ex) when (!ct.IsCancellationRequested)
            {
                if (attempt == _maxRetries - 1)
                {
                    Interlocked.Increment(ref _failureCount);
                    Interlocked.Exchange(ref _lastFailureTicks, DateTimeOffset.UtcNow.Ticks);
                    FallbackToConsole(batch, ex);
                    return;
                }
                await Task.Delay(_retryBaseDelayMs * (int)Math.Pow(2, attempt), ct)
                    .ConfigureAwait(false);
            }
        }
    }

    private void FallbackToConsole(IReadOnlyList<LogEntry> batch, Exception ex)
    {
        if (!_enableConsoleFallback)
            return;

        try
        {
            foreach (var entry in batch)
                Console.WriteLine(
                    $"[SeqSink fallback] {entry.Timestamp:O} [{entry.Level}] {entry.Message}"
                );
        }
        catch
        {
            /* best-effort fallback */
        }
        _ = ex; // suppress unused warning
    }

    private static string SerializeBatch(IReadOnlyList<LogEntry> batch)
    {
        var sb = new StringBuilder();
        sb.Append("{\"Events\":[");

        for (var i = 0; i < batch.Count; i++)
        {
            if (i > 0)
                sb.Append(',');
            sb.Append(SerializeEntry(batch[i]));
        }

        sb.Append("]}");
        return sb.ToString();
    }

    private static string SerializeEntry(LogEntry entry)
    {
        var sb = new StringBuilder();
        sb.Append("{\"@t\":\"");
        sb.Append(entry.Timestamp.ToString("O"));
        sb.Append("\",\"@mt\":\"");
        sb.Append(EscapeJson(entry.Message ?? string.Empty));
        sb.Append("\",\"@l\":\"");
        sb.Append(entry.Level.ToString());
        sb.Append('"');

        if (entry.Properties is { Count: > 0 })
        {
            foreach (var prop in entry.Properties)
            {
                sb.Append(",\"");
                sb.Append(EscapeJson(prop.Key));
                sb.Append("\":\"");
                sb.Append(EscapeJson(prop.Value?.ToString() ?? "null"));
                sb.Append('"');
            }
        }

        if (entry.Exception is not null)
        {
            sb.Append(",\"@x\":\"");
            sb.Append(EscapeJson(entry.Exception.ToString()));
            sb.Append('"');
        }

        sb.Append('}');
        return sb.ToString();
    }

    private static string EscapeJson(string value)
    {
        return value
            .Replace("\\", "\\\\")
            .Replace("\"", "\\\"")
            .Replace("\n", "\\n")
            .Replace("\r", "\\r")
            .Replace("\t", "\\t");
    }
}
