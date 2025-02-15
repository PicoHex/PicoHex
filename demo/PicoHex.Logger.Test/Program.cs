// See https://aka.ms/new-console-template for more information


// 注册示例（使用Microsoft DI）

using Microsoft.Extensions.DependencyInjection;
using PicoHex.Log.NG;

var svcRegistry = new ServiceCollection();
svcRegistry.AddSingleton<ILogFormatter, ConsoleLogFormatter>();
svcRegistry.AddSingleton<ILogSink, ConsoleLogSink>();
svcRegistry.AddSingleton<ILoggerFactory>(
    sp => new LoggerFactory(sp.GetRequiredService<ILogSink>(), LogLevel.Debug)
);
svcRegistry.AddTransient(typeof(ILogger<>), typeof(Logger<>));

Console.WriteLine("Hello World!");

// 使用示例
var provider = svcRegistry.BuildServiceProvider();
var logger = provider.GetRequiredService<ILogger<Program>>();

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
