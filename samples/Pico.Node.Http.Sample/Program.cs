// See https://aka.ms/new-console-template for more information

// Step 1: Create the IoC container

var svcRegistry = Bootstrap.CreateContainer();

var tcpOption = new TcpNodeOptions { IpAddress = IPAddress.Any, Port = 8080 };
svcRegistry
    // .AddLogging(builder => builder.AddConsole())
    .RegisterTransient<ITcpHandler, HttpHandler>()
    .RegisterTransient<Func<ITcpHandler>>(sp => sp.Resolve<ITcpHandler>)
    .RegisterSingle<TcpNode>(sp => new TcpNode(
        tcpOption,
        sp.Resolve<Func<ITcpHandler>>(),
        sp.Resolve<ILogger<TcpNode>>()
    ));

var tcpOptionV2 = new TcpNodeOptionsV2 { IpAddress = IPAddress.Any, Port = 8081 };
svcRegistry
    // .AddLogging(builder => builder.AddConsole())
    .RegisterTransient<IPipelineHandler, HttpHandlerV2>()
    .RegisterTransient<Func<IPipelineHandler>>(sp => sp.Resolve<IPipelineHandler>)
    .RegisterSingle<TcpNodeV2>(sp => new TcpNodeV2(
        tcpOptionV2,
        sp.Resolve<Func<IPipelineHandler>>(),
        sp.Resolve<ILogger<TcpNodeV2>>()
    ));

svcRegistry.RegisterLogger();

var svcProvider = svcRegistry.GetProvider();

var tcpServer = svcProvider.Resolve<TcpNode>();
var tcpServerV2 = svcProvider.Resolve<TcpNodeV2>();

var logger = svcProvider.Resolve<ILogger<Program>>();

await logger.InfoAsync($"Starting TCP server on http://localhost:{tcpOption.Port}...");
var cts = new CancellationTokenSource();

AppDomain.CurrentDomain.ProcessExit += (_, _) => cts.Cancel();
Console.CancelKeyPress += (_, _) => cts.Cancel();

await tcpServer.StartAsync(cts.Token);
await tcpServerV2.StartAsync(cts.Token);

Console.ReadLine();

await tcpServer.StopAsync(cts.Token);
await tcpServer.DisposeAsync();

await tcpServerV2.StopAsync(cts.Token);
await tcpServerV2.DisposeAsync();
