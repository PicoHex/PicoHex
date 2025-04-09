// Step 1: Create the IoC container

var svcRegistry = Bootstrap.CreateContainer();

// Registering logging
svcRegistry.RegisterLogger();

// Registering Handlers
svcRegistry.RegisterSingle<ITcpHandler, MyStreamHandler>();
svcRegistry.RegisterSingle<IUdpHandler, MyBytesHandler>();

var logger = svcRegistry.CreateLogger<Program>();

// Registering servers
svcRegistry.RegisterSingle<Func<ITcpHandler>>(sp => () => sp.Resolve<ITcpHandler>()!);
svcRegistry.RegisterSingle<Func<IUdpHandler>>(sp => () => sp.Resolve<IUdpHandler>()!);
const int tcpPort = 12345;
const int udpPort = 12346;
svcRegistry.RegisterSingle<TcpServer>(
    sp =>
        new TcpServer(
            IPAddress.Any,
            tcpPort,
            sp.Resolve<Func<ITcpHandler>>(),
            sp.Resolve<ILogger<TcpServer>>()
        )
);
svcRegistry.RegisterSingle<UdpServer>(
    sp =>
        new UdpServer(
            IPAddress.Any,
            udpPort,
            sp.Resolve<Func<IUdpHandler>>(),
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
    var tcpServer = serviceProvider.Resolve<TcpServer>()!;
    var udpServer = serviceProvider.Resolve<UdpServer>()!;

    using var cancellationTokenSource = new CancellationTokenSource();

    // Run servers
    var tcpServerTask = tcpServer.StartAsync(cancellationTokenSource.Token);
    var udpServerTask = udpServer.StartAsync(cancellationTokenSource.Token);

    // Wait for servers to complete
    await Task.WhenAny(tcpServerTask, udpServerTask).GetAwaiter().GetResult();
}
catch (Exception ex)
{
    logger.Error($"An unhandled exception occurred during application startup: {ex}");
}
