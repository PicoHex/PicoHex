namespace PicoHex.Logger.NG;

public class FileSink(ILogFormatter formatter, string filePath) : ILogSink
{
    public LogLevel MinimumLevel { get; set; } = LogLevel.Information;

    public void Emit(LogEntry entry)
    {
        File.AppendAllText(filePath, formatter.Format(entry) + Environment.NewLine);
    }
}
