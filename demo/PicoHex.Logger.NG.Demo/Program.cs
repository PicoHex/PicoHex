// See https://aka.ms/new-console-template for more information

// Configure logger factory

var factory = new LoggerFactory();

// Console sink with simple formatter
var consoleFormatter = new SimpleFormatter();
var consoleSink = new ConsoleSink(consoleFormatter) { MinimumLevel = LogLevel.Information };
factory.AddProvider(new LoggerProvider(consoleSink));

// File sink with JSON formatter
var jsonFormatter = new JsonFormatter();
var fileSink = new FileSink(jsonFormatter, "log.json") { MinimumLevel = LogLevel.Warning };
factory.AddProvider(new LoggerProvider(fileSink));

// Create logger
var logger = factory.CreateLogger<Program>();

// Usage examples
logger.Log(LogLevel.Information, "Starting application");
logger.Log(LogLevel.Warning, "Low memory warning");
logger.Log(LogLevel.Error, "Database connection failed", new Exception("Connection timeout"));

// Usage examples
await logger.LogAsync(LogLevel.Information, "Starting application");
await logger.LogAsync(LogLevel.Warning, "Low memory warning");
await logger.LogAsync(
    LogLevel.Error,
    "Database connection failed",
    new Exception("Connection timeout")
);
