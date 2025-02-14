// See https://aka.ms/new-console-template for more information


// 注册示例（使用Microsoft DI）

using PicoHex.DependencyInjection;
using PicoHex.DependencyInjection.Abstractions;
using PicoHex.Log.NG;

var svcRegistry = ContainerBootstrap.CreateRegistry();
svcRegistry.AddSingleton<ILogFormatter, ConsoleLogFormatter>();
svcRegistry.AddSingleton<ILogSink, ConsoleLogSink>();
svcRegistry.AddSingleton<ILoggerFactory>(sp => new LoggerFactory(
    sp.Resolve<ILogSink>(),
    LogLevel.Debug
));
svcRegistry.AddTransient(typeof(ILogger<>), typeof(Logger<>));

Console.WriteLine("Hello World!");

// 使用示例
var provider = svcRegistry.CreateProvider();
var logger = provider.Resolve<ILogger<Program>>();

using (logger.BeginScope("Transaction-123"))
{
    logger.Log(LogLevel.Information, "Processing started");
    try
    {
        throw new DivideByZeroException();
    }
    catch (Exception ex)
    {
        logger.Log(LogLevel.Error, "Processing failed", ex);
    }
}

Console.ReadLine();
