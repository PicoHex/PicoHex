namespace PicoHex.Logger.NG.Abstractions;

public interface ILogSink
{
    LogLevel MinimumLevel { get; set; }
    void Emit(LogEntry entry);
}
