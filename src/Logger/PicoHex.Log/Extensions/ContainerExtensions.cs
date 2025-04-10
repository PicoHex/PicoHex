namespace PicoHex.Log.Extensions;

public static class ContainerExtensions
{
    public static ISvcContainer RegisterLogger(this ISvcContainer container) =>
        container
            .RegisterSingle<ILogSink, ConsoleLogSink>()
            .RegisterSingle<ILogFormatter, ConsoleLogFormatter>()
            .RegisterSingle<ILoggerFactory, LoggerFactory>()
            .RegisterSingle(typeof(ILogger<>), typeof(Logger<>));
}
