namespace PicoHex.Logger.NG;

public class ConsoleSink(ILogFormatter formatter) : ILogSink
{
    public LogLevel MinimumLevel { get; set; } = LogLevel.Debug;

    public void Emit(LogEntry entry)
    {
        Console.WriteLine(formatter.Format(entry));
    }
}
