// See https://aka.ms/new-console-template for more information

using PicoHex.IoC;
using PicoHex.IoC.Abstractions;
using PicoHex.Log.NG;

var container = Bootstrap.CreateContainer();
container.RegisterSingle<ILogFormatter, ConsoleLogFormatter>();
container.RegisterSingle<ILogSink, ConsoleLogSink>();
container.RegisterSingle<ILoggerFactory>(sp => new LoggerFactory(
    sp.Resolve<ILogSink>()!,
    LogLevel.Debug
));
container.RegisterTransient(typeof(ILogger<Program>), typeof(Logger<Program>));

Console.WriteLine("Hello World!");

// 使用示例
var provider = container.CreateProvider();
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
