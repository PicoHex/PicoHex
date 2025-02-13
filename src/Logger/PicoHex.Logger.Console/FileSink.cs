namespace PicoHex.Logger.Console;

public class FileSink(ILogFormatter formatter, string filePath) : ILogSink
{
    public LogLevel MinimumLevel { get; set; } = LogLevel.Information;

    public void Emit(LogEntry entry)
    {
        File.AppendAllText(filePath, formatter.Format(entry) + Environment.NewLine);
    }

    public async ValueTask EmitAsync(LogEntry entry, CancellationToken cancellationToken = default)
    {
        await File.AppendAllTextAsync(
            filePath,
            formatter.Format(entry) + Environment.NewLine,
            cancellationToken
        );
    }
}
