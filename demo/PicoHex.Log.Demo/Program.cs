var container = Bootstrap.CreateContainer();
container.RegisterConsoleLogger<Program>();
var logger = container.CreateLogger<Program>();

await logger.InfoAsync("Hello, World!");

// 输出所有定义的日志级别示例
logger.Trace("Verbose diagnostic tracing"); // 最详细的跟踪信息
logger.Debug("Database query executed in 12ms"); // 调试细节
logger.Info("Application initialized successfully"); // 常规运行信息
logger.Notice("User 'admin' logged in from 192.168.1.1"); // 需要注意但非错误的事件
logger.Warning("High CPU usage detected (90%)"); // 潜在问题警告
logger.Error("Failed to save user profile", new InvalidOperationException("File locked")); // 可恢复错误
logger.Critical("Payment gateway unreachable"); // 关键业务功能中断
logger.Alert("Security firewall breached"); // 需要立即人工干预
logger.Emergency("System storage full - service halted"); // 整个系统不可用

// 异步日志示例
await logger.TraceAsync("Async trace: Cache refresh started");
await logger.DebugAsync("Async debug: Deserializing payload");
await logger.NoticeAsync("Async notice: New user registration");
await logger.AlertAsync("Async alert: Brute force attack detected");

// 带作用域的日志
using (logger.BeginScope("OrderProcessing"))
{
    logger.Info("Starting order processing workflow");

    try
    {
        logger.Debug("Validating order items");
        logger.Notice("Processing payment for order");
        throw new DivideByZeroException();
    }
    catch (Exception ex)
    {
        logger.Error("Payment processing failed", ex);
        logger.Critical("Order workflow cannot continue");
    }
}

logger.Info("Press any key to exit...");
Console.ReadKey();
