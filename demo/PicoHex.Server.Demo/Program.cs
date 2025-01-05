namespace PicoHex.Server.Demo;

internal class Program
{
    static async Task Main(string[] args)
    {
        // Step 1: Create the IoC container
        var serviceCollection = new ServiceCollection();

        // Registering logging
        serviceCollection.AddLogging(builder => builder.AddConsole());

        // Registering Handlers
        serviceCollection.AddSingleton<IStreamHandler, MyStreamHandler>();
        serviceCollection.AddSingleton<IBytesHandler, MyBytesHandler>();

        // Registering servers
        serviceCollection.AddSingleton<Func<IStreamHandler>>(
            sp => sp.GetRequiredService<IStreamHandler>
        );
        serviceCollection.AddSingleton<Func<IBytesHandler>>(
            sp => sp.GetRequiredService<IBytesHandler>
        );
        serviceCollection.AddSingleton<TcpServer>();
        serviceCollection.AddSingleton<UdpServer>();

        serviceCollection.BuildServiceProvider();

        var serviceProvider = serviceCollection.BuildServiceProvider();

        // Step 2: Try to get a logger from the IoC container
        var logger = serviceProvider.GetService<ILogger<Program>>();

        // Step 3: If no logger is registered, set up a default console logger
        if (logger == null)
        {
            logger = CreateDefaultLogger<Program>();
            Console.WriteLine("No logger registered, using default console logger.");
        }

        // Step 4: Set up the rest of your services
        try
        {
            // Proceed with starting the servers (TcpServer and UdpServer)
            var tcpServer = serviceProvider.GetRequiredService<TcpServer>();
            var udpServer = serviceProvider.GetRequiredService<UdpServer>();

            var cancellationTokenSource = new CancellationTokenSource();

            // Run servers
            var tcpServerTask = tcpServer.StartAsync(cancellationTokenSource.Token);
            var udpServerTask = udpServer.StartAsync(cancellationTokenSource.Token);

            // Wait for servers to complete
            await Task.WhenAny(tcpServerTask, udpServerTask).GetAwaiter().GetResult();
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "An unhandled exception occurred during application startup");
        }
        finally
        {
            await Log.CloseAndFlushAsync();
        }
    }

    // Creates a default logger that logs to the console
    private static ILogger<T> CreateDefaultLogger<T>()
    {
        var factory = LoggerFactory.Create(builder =>
        {
            builder.AddConsole(); // This will log to the console by default
        });
        return factory.CreateLogger<T>();
    }
}
