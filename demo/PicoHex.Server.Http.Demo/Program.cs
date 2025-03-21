// See https://aka.ms/new-console-template for more information

// Step 1: Create the IoC container

using PicoHex.DI.Extensions;

var svcRegistry = Bootstrap.CreateContainer();
const int tcpPort = 8080;
svcRegistry
    // .AddLogging(builder => builder.AddConsole())
    .RegisterTransient<ITcpHandler, RestfulHandler>()
    .RegisterTransient<Func<ITcpHandler>>(sp => () => sp.Resolve<ITcpHandler>()!)
    .RegisterSingle<TcpServer>(sp => new TcpServer(
        IPAddress.Any,
        tcpPort,
        sp.Resolve<Func<ITcpHandler>>(),
        sp.Resolve<ILogger<TcpServer>>()
    ))
    .RegisterSingle<RestfulHandler>();

svcRegistry
    .RegisterSingle<ILogger<TcpServer>, Logger<TcpServer>>()
    .RegisterSingle<ILogger<RestfulHandler>, Logger<RestfulHandler>>()
    .RegisterSingle<ILoggerFactory>(_ => LoggerFactory.Create(builder => builder.AddConsole()));

var svcProvider = svcRegistry.CreateProvider();

var tcpServer = svcProvider.Resolve<TcpServer>()!;

Console.WriteLine($"Starting TCP server on http://localhost:{tcpPort}...");
var cts = new CancellationTokenSource();

AppDomain.CurrentDomain.ProcessExit += (_, __) => cts.Cancel();
Console.CancelKeyPress += (_, __) => cts.Cancel();

await tcpServer.StartAsync(cts.Token);
