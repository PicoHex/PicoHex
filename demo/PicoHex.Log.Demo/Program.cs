// See https://aka.ms/new-console-template for more information

Console.WriteLine("Hello, World!");

var container = Bootstrap.CreateContainer();
container.RegisterConsoleLogger<Program>();

// Create logger
var logger = container.CreateLogger<Program>();

// Usage examples
logger.Info("Starting application");
logger.Warning("Low memory warning");
logger.Error("Database connection failed", new Exception("Connection timeout"));

// Usage examples
await logger.InfoAsync("Starting application async");
await logger.WarningAsync("Low memory warning async");
await logger.ErrorAsync("Database connection failed async", new Exception("Connection timeout"));

// Usage examples
using (logger.BeginScope("Transaction-123"))
{
    using (logger.BeginScope("transaction-456"))
    {
        logger.Log(LogLevel.Info, "Processing started");
        try
        {
            throw new DivideByZeroException();
        }
        catch (Exception ex)
        {
            logger.Log(LogLevel.Error, "Processing failed", ex);
        }
    }
}

Console.WriteLine("Press any key to exit...");
Console.ReadKey();
