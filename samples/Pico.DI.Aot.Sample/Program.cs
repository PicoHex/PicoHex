using Pico.DI;

namespace Pico.DI.Aot.Sample;

public interface IUserService
{
    string GetUserName(int userId);
}

public class UserService : IUserService
{
    public string GetUserName(int userId)
    {
        return $"User_{userId}";
    }
}

public interface IEmailService
{
    void SendEmail(string to, string subject, string body);
}

public class EmailService : IEmailService
{
    public void SendEmail(string to, string subject, string body)
    {
        Console.WriteLine($"Sending email to: {to}");
        Console.WriteLine($"Subject: {subject}");
        Console.WriteLine($"Body: {body}");
    }
}

public class NotificationService
{
    private readonly IUserService _userService;
    private readonly IEmailService _emailService;

    public NotificationService(IUserService userService, IEmailService emailService)
    {
        _userService = userService;
        _emailService = emailService;
    }

    public void SendWelcomeNotification(int userId)
    {
        var userName = _userService.GetUserName(userId);
        var subject = "Welcome to our service!";
        var body = $"Hello {userName}, welcome to our amazing service!";
        
        _emailService.SendEmail($"{userName}@example.com", subject, body);
    }
}

public static class Program
{
    public static void Main()
    {
        Console.WriteLine("=== Pico.DI AOT Sample ===");
        
        // Create AOT-optimized container
        var container = AotBootstrap.CreateAotContainer();
        
        // Register services
        container
            .RegisterTransient<IUserService, UserService>()
            .RegisterTransient<IEmailService, EmailService>()
            .RegisterTransient<NotificationService>();

        var provider = container.GetProvider();

        // Resolve and use services
        var notificationService = provider.Resolve<NotificationService>();
        notificationService.SendWelcomeNotification(123);

        // Demonstrate different lifetimes
        Console.WriteLine("\n=== Lifetime Demonstration ===");
        
        container.RegisterSingle<ISingletonService, SingletonService>();
        container.RegisterScoped<IScopedService, ScopedService>();
        
        var singleton1 = provider.Resolve<ISingletonService>();
        var singleton2 = provider.Resolve<ISingletonService>();
        Console.WriteLine($"Singleton instances are same: {singleton1 == singleton2}");

        using (var scope = provider.CreateScope())
        {
            var scoped1 = scope.Resolve<IScopedService>();
            var scoped2 = scope.Resolve<IScopedService>();
            Console.WriteLine($"Scoped instances in same scope are same: {scoped1 == scoped2}");
        }

        using (var scope2 = provider.CreateScope())
        {
            var scoped3 = scope2.Resolve<IScopedService>();
            Console.WriteLine($"Scoped instance in different scope is different");
        }

        Console.WriteLine("\n=== AOT Features ===");
        Console.WriteLine($"Container Type: {container.GetType().Name}");
        Console.WriteLine($"Provider Type: {provider.GetType().Name}");
        Console.WriteLine("AOT optimization: Compile-time service discovery and factory generation");

        Console.WriteLine("\nPress any key to exit...");
        Console.ReadKey();
    }
}

public interface ISingletonService
{
    Guid InstanceId { get; }
}

public class SingletonService : ISingletonService
{
    public Guid InstanceId { get; } = Guid.NewGuid();
}

public interface IScopedService
{
    Guid InstanceId { get; }
}

public class ScopedService : IScopedService
{
    public Guid InstanceId { get; } = Guid.NewGuid();
}