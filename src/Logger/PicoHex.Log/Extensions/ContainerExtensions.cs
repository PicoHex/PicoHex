namespace PicoHex.Log.Extensions;

public static class ContainerExtensions
{
    public static ISvcContainer RegisterConsoleLogger<T>(this ISvcContainer container) =>
        container
            .RegisterSingle<ILogFormatter, ConsoleLogFormatter>()
            .RegisterSingle<ILoggerFactory, LoggerFactory>()
            .RegisterSingle<ILogSink, ConsoleLogSink>()
            .RegisterSingle<IEnumerable<ILogSink>>(
                sp => new List<ILogSink> { sp.Resolve<ILogSink>()! }
            )
            .RegisterSingle<ILogger<T>, Logger<T>>();

    public static ILogger CreateLogger<T>(this ISvcContainer container) =>
        container.CreateProvider().Resolve<ILogger<T>>()!;
}
