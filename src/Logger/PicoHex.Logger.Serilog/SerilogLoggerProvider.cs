namespace PicoHex.Logger.Serilog;

public class SerilogLoggerProvider(Serilog.ILogger logger) : ILoggerProvider
{
    private readonly Serilog.ILogger _serilogLogger = logger;

    public ILogger CreateLogger(string categoryName) =>
        new SerilogLogger(_serilogLogger.ForContext("Category", categoryName));

    public void Dispose() { }
}
