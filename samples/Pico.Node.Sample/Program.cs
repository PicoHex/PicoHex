// Step 1: Create the IoC container

var svcRegistry = Bootstrap.CreateContainer();

// Registering logging
svcRegistry.RegisterLogger();

// Registering Handlers
svcRegistry.RegisterSingle<ITcpHandler, MyStreamHandler>();
svcRegistry.RegisterSingle<IUdpHandler, MyBytesHandler>();

// Registering servers
svcRegistry.RegisterSingle<Func<ITcpHandler>>(sp => sp.Resolve<ITcpHandler>);
svcRegistry.RegisterSingle<Func<IUdpHandler>>(sp => sp.Resolve<IUdpHandler>);
const int tcpPort = 12345;
const int udpPort = 12346;
svcRegistry.RegisterSingle<TcpNode>(sp => new TcpNode(
    IPAddress.Any,
    tcpPort,
    sp.Resolve<Func<ITcpHandler>>(),
    sp.Resolve<ILogger<TcpNode>>()
));
svcRegistry.RegisterSingle<UdpNode>(sp => new UdpNode(
    IPAddress.Any,
    udpPort,
    sp.Resolve<Func<IUdpHandler>>(),
    sp.Resolve<ILogger<UdpNode>>()
));

var serviceProvider = svcRegistry.GetProvider();

// Step 2: Try to get a logger from the IoC container
var logger = serviceProvider.Resolve<ILogger<Program>>();

// Step 3: Set up the rest of your services
try
{
    // Proceed with starting the servers (TcpNode and UdpNode)
    var tcpServer = serviceProvider.Resolve<TcpNode>();
    var udpServer = serviceProvider.Resolve<UdpNode>();

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
