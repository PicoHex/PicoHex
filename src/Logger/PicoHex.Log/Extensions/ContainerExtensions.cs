namespace PicoHex.Log.Extensions;

public static class ContainerExtensions
{
    public static ISvcContainer RegisterConsoleLogger<T>(this ISvcContainer container) =>
        container
            .RegisterScoped<ILogFormatter, ConsoleLogFormatter>()
            .RegisterScoped<ILoggerFactory, LoggerFactory>()
            .RegisterScoped<ILogSink, ConsoleLogSink>()
            .RegisterScoped<ILogger<T>, Logger<T>>();

    public static ILogger CreateLogger<T>(this ISvcContainer container) =>
        container.CreateProvider().Resolve<ILogger<T>>()!;
}
