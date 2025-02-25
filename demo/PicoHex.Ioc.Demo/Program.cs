// Example usage

using PicoHex.IoC;

var services = new ServiceCollection();
services.AddSingleton<ILogger, ConsoleLogger>();
services.AddTransient<UserService, UserService>();

// Build container
using var container = new ServiceContainer(services);

// Resolve service and use it
var userService = container.GetService<UserService>();
userService.DoWork();

public interface ILogger
{
    void Log(string message);
}

public class ConsoleLogger : ILogger
{
    public void Log(string message)
    {
        Console.WriteLine(message);
    }
}

public class UserService
{
    private readonly ILogger _logger;

    // Constructor injection
    public UserService(ILogger logger)
    {
        _logger = logger;
    }

    public void DoWork()
    {
        _logger.Log("Work done by UserService");
    }
}

// Configuration
