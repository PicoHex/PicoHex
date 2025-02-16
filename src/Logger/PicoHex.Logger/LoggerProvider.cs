namespace PicoHex.Logger;

public class LoggerProvider(ILogSink sink) : ILoggerProvider
{
    public ILogger CreateLogger(string category)
    {
        return new CategoryLogger(category, sink);
    }

    public void Dispose() { }
}
