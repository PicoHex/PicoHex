namespace PicoHex.Log.Abstractions;

public interface ILogSink : IDisposable
{
    Task WriteAsync(LogEntry entry);
}
