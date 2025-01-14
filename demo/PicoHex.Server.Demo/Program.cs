namespace PicoHex.Server.Demo;

internal class Program
{
    static async Task Main(string[] args)
    {
        // Step 1: Create the IoC container
        var svcRegistry = ContainerBootstrap.CreateRegistry();

        // Registering logging
        // serviceCollection.AddLogging(builder => builder.AddConsole());

        // Registering Handlers
        svcRegistry.AddSingleton<IStreamHandler, MyStreamHandler>();
        svcRegistry.AddSingleton<IBytesHandler, MyBytesHandler>();
        svcRegistry.AddSingleton<ILogger<TcpServer>, Logger<TcpServer>>();
        svcRegistry.AddSingleton<ILogger<UdpServer>, Logger<UdpServer>>();
        svcRegistry.AddSingleton<ILogger<MyStreamHandler>, Logger<MyStreamHandler>>();
        svcRegistry.AddSingleton<ILogger<MyBytesHandler>, Logger<MyBytesHandler>>();
        svcRegistry.AddSingleton<ILoggerFactory>(
            _ => LoggerFactory.Create(builder => builder.AddConsole())
        );

        // Registering servers
        svcRegistry.AddSingleton<Func<IStreamHandler>>(sp => sp.Resolve<IStreamHandler>);
        svcRegistry.AddSingleton<Func<IBytesHandler>>(sp => sp.Resolve<IBytesHandler>);
        svcRegistry.AddSingleton<TcpServer>(
            sp =>
                new TcpServer(
                    IPAddress.Any,
                    12345,
                    sp.Resolve<Func<IStreamHandler>>(),
                    sp.Resolve<ILogger<TcpServer>>()
                )
        );
        svcRegistry.AddSingleton<UdpServer>(
            sp =>
                new UdpServer(
                    IPAddress.Any,
                    12345,
                    sp.Resolve<Func<IBytesHandler>>(),
                    sp.Resolve<ILogger<UdpServer>>()
                )
        );

        var serviceProvider = svcRegistry.CreateProvider();

        // Step 2: Try to get a logger from the IoC container
        // var logger = serviceProvider.GetService<ILogger<Program>>();

        // Step 3: If no logger is registered, set up a default console logger
        // if (logger == null)
        // {
        //     logger = CreateDefaultLogger<Program>();
        //     Console.WriteLine("No logger registered, using default console logger.");
        // }

        // Step 4: Set up the rest of your services
        try
        {
            // Proceed with starting the servers (TcpServer and UdpServer)
            var tcpServer = serviceProvider.Resolve<TcpServer>();
            var udpServer = serviceProvider.Resolve<UdpServer>();

            var cancellationTokenSource = new CancellationTokenSource();

            // Run servers
            var tcpServerTask = tcpServer?.StartAsync(cancellationTokenSource.Token);
            var udpServerTask = udpServer?.StartAsync(cancellationTokenSource.Token);

            // Wait for servers to complete
            await Task.WhenAny(tcpServerTask, udpServerTask).GetAwaiter().GetResult();
        }
        catch (Exception ex)
        {
            Console.WriteLine($"An unhandled exception occurred during application startup: {ex}");
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
