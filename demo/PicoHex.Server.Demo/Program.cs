// Step 1: Create the IoC container

var svcRegistry = Bootstrap.CreateContainer();

// Registering logging
// serviceCollection.AddLogging(builder => builder.AddConsole());

// Registering Handlers
svcRegistry.RegisterSingle<ITcpHandler, MyStreamHandler>();
svcRegistry.RegisterSingle<IUdpHandler, MyBytesHandler>();
svcRegistry.RegisterSingle<ILogger<TcpServer>, Logger<TcpServer>>();
svcRegistry.RegisterSingle<ILogger<UdpServer>, Logger<UdpServer>>();
svcRegistry.RegisterSingle<ILogger<MyStreamHandler>, Logger<MyStreamHandler>>();
svcRegistry.RegisterSingle<ILogger<MyBytesHandler>, Logger<MyBytesHandler>>();
svcRegistry.RegisterSingle(_ => LoggerFactory.Create(builder => builder.AddConsole()));

// Registering servers
svcRegistry.RegisterSingle<Func<ITcpHandler>>(sp => sp.Resolve<ITcpHandler>);
svcRegistry.RegisterSingle<Func<IUdpHandler>>(sp => sp.Resolve<IUdpHandler>);
const int tcpPort = 12345;
const int udpPort = 12346;
svcRegistry.RegisterSingle<TcpServer>(sp => new TcpServer(
    IPAddress.Any,
    tcpPort,
    sp.Resolve<Func<ITcpHandler>>(),
    sp.Resolve<ILogger<TcpServer>>()
));
svcRegistry.RegisterSingle<UdpServer>(sp => new UdpServer(
    IPAddress.Any,
    udpPort,
    sp.Resolve<Func<IUdpHandler>>(),
    sp.Resolve<ILogger<UdpServer>>()
));

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

    using var cancellationTokenSource = new CancellationTokenSource();

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

// Creates a default logger that logs to the console
ILogger<T> CreateDefaultLogger<T>()
{
    var factory = LoggerFactory.Create(builder =>
    {
        builder.AddConsole(); // This will log to the console by default
    });
    return factory.CreateLogger<T>();
}
