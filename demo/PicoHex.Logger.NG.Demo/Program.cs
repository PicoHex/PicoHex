// See https://aka.ms/new-console-template for more information

// Configure logger factory

using PicoHex.Logger;
using PicoHex.Logger.Console;

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
logger.Info("Starting application");
logger.Warning("Low memory warning");
logger.Error("Database connection failed", new Exception("Connection timeout"));

// Usage examples
await logger.InfoAsync("Starting application");
await logger.WarningAsync("Low memory warning");
await logger.ErrorAsync("Database connection failed", new Exception("Connection timeout"));
