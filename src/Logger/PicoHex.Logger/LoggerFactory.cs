namespace PicoHex.Logger;

public class LoggerFactory : ILoggerFactory
{
    private readonly List<ILoggerProvider> _providers = new();

    public ILogger CreateLogger(string category)
    {
        var loggers = _providers.Select(p => p.CreateLogger(category)).ToList();
        return new InternalLogger(loggers);
    }

    public ILogger<T> CreateLogger<T>() => new GenericTypeLogger<T>(this);

    public void AddProvider(ILoggerProvider provider)
    {
        _providers.Add(provider);
    }
}
