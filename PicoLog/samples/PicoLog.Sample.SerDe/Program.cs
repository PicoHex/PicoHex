// PicoLog + PicoSerDe — Structured JSON Logging Demo
// Demonstrates JSON log sink producing newline-delimited JSON (NDJSON) output.
// Each log entry is a single JSON line — easy to ingest with log platforms.

Console.WriteLine("=== PicoLog.SerDe — Structured JSON Logging ===");
Console.WriteLine();

var tempFile = Path.Combine(
    Path.GetTempPath(),
    $"PicoLog_SerDe_Demo_{DateTime.UtcNow:yyyyMMddHHmmss}.json"
);
Console.WriteLine($"Output file: {tempFile}");
Console.WriteLine();

var sink = new JsonLogSink(tempFile);

// ── Basic logging ──

await sink.WriteAsync(
    new LogEntry
    {
        Timestamp = DateTimeOffset.UtcNow,
        Level = LogLevel.Info,
        Category = "Demo",
        Message = "Application started"
    }
);

await sink.WriteAsync(
    new LogEntry
    {
        Timestamp = DateTimeOffset.UtcNow,
        Level = LogLevel.Warning,
        Category = "Demo",
        Message = "Configuration file not found, using defaults"
    }
);

// ── Structured properties ──

await sink.WriteAsync(
    new LogEntry
    {
        Timestamp = DateTimeOffset.UtcNow,
        Level = LogLevel.Info,
        Category = "Database",
        Message = "Connection opened",
        Properties = new List<KeyValuePair<string, object?>>
        {
            new("Server", "db.example.com"),
            new("Port", 5432),
            new("PoolSize", 20)
        }
    }
);

// ── Exception logging ──

try
{
    _ = int.Parse("not a number");
}
catch (Exception ex)
{
    await sink.WriteAsync(
        new LogEntry
        {
            Timestamp = DateTimeOffset.UtcNow,
            Level = LogLevel.Error,
            Category = "Demo",
            Message = "Failed to parse configuration value",
            Exception = ex
        }
    );
}

// ── Scope properties ──

await sink.WriteAsync(
    new LogEntry
    {
        Timestamp = DateTimeOffset.UtcNow,
        Level = LogLevel.Info,
        Category = "Request",
        Message = "Request completed",
        ScopeProperties = new List<KeyValuePair<string, object?>>
        {
            new("RequestId", Guid.NewGuid()),
            new("DurationMs", 42)
        }
    }
);

// ── Flush and dispose ──

await sink.FlushAsync();
await sink.DisposeAsync();

// ── Verify output ──

var lines = File.ReadAllLines(tempFile);
Console.WriteLine($"Wrote {lines.Length} log entries:");
Console.WriteLine();

foreach (var line in lines)
{
    // Print compact preview (first 120 chars)
    var preview = line.Length > 120 ? line[..117] + "..." : line;
    Console.WriteLine($"  {preview}");
}

Console.WriteLine();

var testResults = 0;
var failed = 0;

void Check(string name, bool condition)
{
    testResults++;
    if (condition)
        Console.WriteLine($"  PASS: {name}");
    else
    {
        Console.WriteLine($"  FAIL: {name}");
        failed++;
    }
}

Check("Contains Info entry", lines.Any(l => l.Contains("\"Info\"")));
Check("Contains Warning entry", lines.Any(l => l.Contains("\"Warning\"")));
Check("Contains Error entry", lines.Any(l => l.Contains("\"Error\"")));
Check("Contains structured Properties", lines.Any(l => l.Contains("\"Properties\"")));
Check("Contains ExceptionType", lines.Any(l => l.Contains("\"ExceptionType\"")));
Check("Contains ScopeProperties", lines.Any(l => l.Contains("\"ScopeProperties\"")));
Check(
    "All lines are valid JSON",
    lines.All(l => l.Trim().StartsWith('{') && l.Trim().EndsWith('}'))
);

Console.WriteLine();
Console.WriteLine($"Total: {testResults}, Passed: {testResults - failed}, Failed: {failed}");

// Clean up
File.Delete(tempFile);

return failed == 0 ? 0 : 1;
