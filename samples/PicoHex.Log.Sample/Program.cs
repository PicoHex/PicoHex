// Initialize DI container and configure logging

var container = Bootstrap.CreateContainer();

// Register console logger with application type
container.RegisterLogger();

// Create typed logger instance
var logger = container.GetProvider().Resolve<ILogger<Program>>();

// Demonstrate basic async logging
await logger.InfoAsync("Hello, World!");

// Log all severity levels with contextual examples
logger.Trace("Verbose diagnostic tracing"); // Finest-grained tracing
logger.Debug("Database query executed in 12ms"); // Debug-level details
logger.Info("Application initialized successfully"); // Normal operation
logger.Notice("User 'admin' logged in from 192.168.1.1"); // Significant event
logger.Warning("High CPU usage detected (90%)"); // Potential issues
logger.Error(
    "Failed to save user profile", // Recoverable errors
    new InvalidOperationException("File locked")
);
logger.Critical("Payment gateway unreachable"); // Critical failures
logger.Alert("Security firewall breached"); // Immediate action needed
logger.Emergency("System storage full - service halted"); // System-wide outage

// Demonstrate async logging patterns
await logger.TraceAsync("Async trace: Cache refresh started");
await logger.DebugAsync("Async debug: Deserializing payload");
await logger.NoticeAsync("Async notice: New user registration");
await logger.AlertAsync("Async alert: Brute force attack detected");

// Structured logging with scopes
using (logger.BeginScope("OrderProcessing initiated"))
{
    logger.Debug($"Order ID: {12345}");
    logger.Notice($"User ID: {67890}");
    logger.Warning($"Processing time: {150}ms");

    {
        logger.Info("Starting order processing workflow");

        try
        {
            // Nested scope for payment operations
            using (logger.BeginScope("OrderPayment"))
            {
                logger.Debug("Validating order items");
                logger.Notice("Processing payment for order");

                // Simulate business logic failure
                throw new DivideByZeroException();
            }
        }
        catch (Exception ex)
        {
            // Error logging with exception context
            logger.Error("Payment processing failed", ex);
            logger.Critical("Order workflow cannot continue");
        }
    }
}

// Performance-sensitive logging demonstration
var stopwatch = Stopwatch.StartNew();
logger.Debug("Starting data export...");
await Task.Delay(250); // Simulate work
logger.Debug($"Export completed in {stopwatch.ElapsedMilliseconds}ms");

// Configuration demonstration
// logger.Info($"Current log level: {logger.MinimumLevel}");
// logger.Info($"Log output targets: {logger.Targets}");

// Graceful shutdown example
logger.Notice("Application shutting down...");
logger.Info("Press any key to exit...");
Console.ReadKey();
