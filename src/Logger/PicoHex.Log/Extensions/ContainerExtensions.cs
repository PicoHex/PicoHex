namespace PicoHex.Log.Extensions;

public static class ContainerExtensions
{
    public static ISvcContainer RegisterLogger(this ISvcContainer container) =>
        container
            .RegisterSingle<ILogFormatter, ConsoleLogFormatter>()
            .RegisterSingle<ILoggerFactory, LoggerFactory>()
            .RegisterSingle<ILogSink, ConsoleLogSink>()
            .RegisterSingle(typeof(ILogger<>), typeof(Logger<>));

    public static ILogger CreateLogger<T>(this ISvcContainer container) =>
        container.CreateProvider().Resolve<ILogger<T>>();
}
