namespace PicoHex.Log.Extensions;

public static class ContainerExtensions
{
    public static ISvcContainer AddConsoleLogger<T>(this ISvcContainer container)
    {
        container.RegisterScoped<ILogFormatter, ConsoleLogFormatter>();
        container.RegisterScoped<ILoggerFactory, LoggerFactory>();
        container.RegisterScoped<ILogSink, ConsoleLogSink>();
        container.RegisterScoped<ILogger<T>, Logger<T>>();
        return container;
    }
}
