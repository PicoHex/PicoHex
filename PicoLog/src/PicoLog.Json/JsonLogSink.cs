namespace PicoLog.Json;

/// <summary>
/// Writes log entries as newline-delimited JSON (NDJSON) to a file.
/// Uses PicoJetson for AOT-compatible zero-reflection JSON serialization.
/// </summary>
public sealed class JsonLogSink : ILogSink, IFlushableLogSink
{
    private readonly string _filePath;
    private readonly Lock _writeLock = new();
    private FileStream? _stream;
    private StreamWriter? _writer;

    public JsonLogSink(string filePath)
    {
        _filePath = filePath ?? throw new ArgumentNullException(nameof(filePath));
    }

    private void EnsureWriter()
    {
        if (_writer is not null)
            return;

        var dir = Path.GetDirectoryName(_filePath);
        if (dir is not null && !Directory.Exists(dir))
            Directory.CreateDirectory(dir);

        _stream = new FileStream(_filePath, FileMode.Append, FileAccess.Write, FileShare.Read);
        _writer = new StreamWriter(_stream, Encoding.UTF8) { AutoFlush = false };
    }

    public Task WriteAsync(LogEntry entry, CancellationToken ct = default)
    {
        ct.ThrowIfCancellationRequested();
        lock (_writeLock)
            EnsureWriter();
        WriteEntry(entry);
        return Task.CompletedTask;
    }

    public ValueTask FlushAsync(CancellationToken ct = default)
    {
        ct.ThrowIfCancellationRequested();
        lock (_writeLock)
        {
            _writer?.Flush();
            _stream?.Flush();
            _stream?.Flush(true); // flush to disk
        }
        return ValueTask.CompletedTask;
    }

    private void WriteEntry(LogEntry entry)
    {
        lock (_writeLock)
        {
            var writer = SerializerExtensions.RentWriter();
            try
            {
                WriteJsonEntry(writer, entry);
                var bytes = writer.WrittenSpan;

                _writer!.Write(Encoding.UTF8.GetString(bytes));
                _writer.WriteLine();
            }
            finally
            {
                writer.Clear();
            }
        }
    }

    private static void WriteJsonEntry(IBufferWriter<byte> writer, LogEntry entry)
    {
        var jw = new JsonWriter(writer);
        jw.WriteStartObject();

        WriteProperty(jw, "Timestamp", entry.Timestamp.ToString("O"));
        WriteProperty(jw, "Level", entry.Level.ToString());
        if (entry.Category is not null)
            WriteProperty(jw, "Category", entry.Category);
        if (entry.Message is not null)
            WriteProperty(jw, "Message", entry.Message);
        if (entry.MessageTemplate is not null)
            WriteProperty(jw, "MessageTemplate", entry.MessageTemplate);
        if (entry.EventId != default && entry.EventId.Name is not null)
            WriteProperty(jw, "EventId", entry.EventId.Name);

        if (entry.Exception is not null)
        {
            WriteProperty(jw, "ExceptionType", entry.Exception.GetType().FullName!);
            WriteProperty(jw, "Exception", entry.Exception.Message);
            if (entry.Exception.StackTrace is not null)
                WriteProperty(jw, "StackTrace", entry.Exception.StackTrace);
        }

        if (entry.Properties is { Count: > 0 })
        {
            jw.WritePropertyName("Properties");
            jw.WriteStartObject();
            foreach (var prop in entry.Properties)
                WriteProperty(jw, prop.Key, prop.Value?.ToString());
            jw.WriteEndObject();
        }

        if (entry.ScopeProperties is { Count: > 0 })
        {
            jw.WritePropertyName("ScopeProperties");
            jw.WriteStartObject();
            foreach (var prop in entry.ScopeProperties)
                WriteProperty(jw, prop.Key, prop.Value?.ToString());
            jw.WriteEndObject();
        }

        jw.WriteEndObject();
    }

    private static void WriteProperty(JsonWriter writer, string name, string? value)
    {
        writer.WritePropertyName(name);
        if (value is null)
            writer.WriteNull();
        else
            writer.WriteString(value);
    }

    public async ValueTask DisposeAsync()
    {
        lock (_writeLock)
        {
            _writer?.Dispose();
            _stream?.Dispose();
            _writer = null;
            _stream = null;
        }
        await Task.CompletedTask;
    }
}
