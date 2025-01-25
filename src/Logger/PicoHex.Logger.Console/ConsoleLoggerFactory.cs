namespace PicoHex.Logger.Console;

public class ConsoleLoggerFactory(ILogFormatter? formatter = null, ILogSink? sink = null)
    : ILoggerFactory
{
    private readonly ILogFormatter _formatter = formatter ?? new ConsoleLogFormatter();
    private readonly ILogSink _sink = sink ?? new ConsoleLogSink();

    public ILogger CreateLogger(string categoryName) =>
        new ConsoleLogger(categoryName, _formatter, _sink);

    public void AddProvider(ILoggerProvider provider)
    {
        throw new NotImplementedException();
    }

    public ILogger<T> CreateLogger<T>() => new ConsoleLogger<T>(_formatter, _sink);

    public void Dispose() => _sink.DisposeAsync().GetAwaiter().GetResult();

    public async ValueTask DisposeAsync()
    {
        await _sink.DisposeAsync();
    }
}
