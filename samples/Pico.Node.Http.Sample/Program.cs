// See https://aka.ms/new-console-template for more information

// Step 1: Create the IoC container

var svcRegistry = Bootstrap.CreateContainer();
const int tcpPort = 8080;
svcRegistry
    // .AddLogging(builder => builder.AddConsole())
    .RegisterTransient<ITcpHandler, HttpHandler>()
    .RegisterTransient<Func<ITcpHandler>>(sp => sp.Resolve<ITcpHandler>)
    .RegisterSingle<TcpNode>(sp => new TcpNode(
        IPAddress.Any,
        tcpPort,
        sp.Resolve<Func<ITcpHandler>>(),
        sp.Resolve<ILogger<TcpNode>>()
    ));

svcRegistry.RegisterLogger();

var svcProvider = svcRegistry.GetProvider();

var tcpServer = svcProvider.Resolve<TcpNode>();

var logger = svcProvider.Resolve<ILogger<Program>>();

await logger.InfoAsync($"Starting TCP server on http://localhost:{tcpPort}...");
var cts = new CancellationTokenSource();

AppDomain.CurrentDomain.ProcessExit += (_, _) => cts.Cancel();
Console.CancelKeyPress += (_, _) => cts.Cancel();

await tcpServer.StartAsync(cts.Token);
