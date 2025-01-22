namespace PicoHex.Logger;

public class SerilogLoggerProvider : ILoggerProvider
{
    private readonly Serilog.ILogger _serilogLogger;

    public SerilogLoggerProvider(Serilog.ILogger logger) => _serilogLogger = logger;

    public ILogger CreateLogger(string categoryName) =>
        new SerilogLogger(_serilogLogger.ForContext("Category", categoryName));

    public void Dispose() { }
}
