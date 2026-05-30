namespace PicoLog;

public sealed class SeqSink : IBatchingLogSink, IFlushableLogSink
{
    private const int MaxBatchEntries = 100;
    private const int MaxBatchBytes = 256 * 1024;
    private static readonly TimeSpan FlushInterval = TimeSpan.FromSeconds(2);

    private readonly HttpClient _httpClient;
    private readonly string _apiKey;
    private readonly Lock _bufferLock = new();
    private readonly List<LogEntry> _buffer = [];
    private long _bufferBytes;
    private long _failureCount;
    private long _lastFailureTicks;
    private int _disposed;

    public long FailureCount => Interlocked.Read(ref _failureCount);
    public DateTimeOffset? LastFailureTime =>
        Interlocked.Read(ref _lastFailureTicks) == 0
            ? null
            : new DateTimeOffset(Interlocked.Read(ref _lastFailureTicks), TimeSpan.Zero);

    public SeqSink(HttpClient httpClient, string? apiKey = null)
    {
        _httpClient = httpClient ?? throw new ArgumentNullException(nameof(httpClient));
        _apiKey = apiKey ?? string.Empty;
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

    public async ValueTask DisposeAsync()
    {
        if (Interlocked.Exchange(ref _disposed, 1) != 0)
            return;
        await FlushAsync().ConfigureAwait(false);
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
                _ = DrainAndSendAsync();
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
        var json = SerializeBatch(batch);
        var content = new StringContent(json, Encoding.UTF8, "application/json");

        using var request = new HttpRequestMessage(HttpMethod.Post, "/api/events/raw")
        {
            Content = content
        };

        if (_apiKey.Length > 0)
            request.Headers.Add("X-Seq-ApiKey", _apiKey);

        for (var attempt = 0; attempt < 3; attempt++)
        {
            try
            {
                var response = await _httpClient.SendAsync(request, ct).ConfigureAwait(false);
                response.EnsureSuccessStatusCode();
                Interlocked.Exchange(ref _failureCount, 0);
                return;
            }
            catch (Exception ex)
            {
                if (attempt == 2)
                {
                    Interlocked.Increment(ref _failureCount);
                    Interlocked.Exchange(ref _lastFailureTicks, DateTimeOffset.UtcNow.Ticks);
                    FallbackToConsole(batch, ex);
                    return;
                }
                await Task.Delay((int)Math.Pow(2, attempt) * 100, ct).ConfigureAwait(false);
            }
        }
    }

    private static void FallbackToConsole(IReadOnlyList<LogEntry> batch, Exception ex)
    {
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
