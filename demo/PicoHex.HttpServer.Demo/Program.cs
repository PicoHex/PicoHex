// See https://aka.ms/new-console-template for more information

using PicoHex.Server.Abstractions;

var serviceProvider = new ServiceCollection()
    .AddLogging(builder => builder.AddConsole())
    .AddTransient<ITcpHandler, HttpHandler>()
    .BuildServiceProvider();

var logger = serviceProvider.GetRequiredService<ILogger<TcpServer>>();
ITcpHandler HandlerFactory() => serviceProvider.GetRequiredService<ITcpHandler>();

var tcpServer = new TcpServer(IPAddress.Loopback, 8080, HandlerFactory, logger);

Console.WriteLine("Starting TCP server on http://localhost:8080...");
var cts = new CancellationTokenSource();

AppDomain.CurrentDomain.ProcessExit += (_, __) => cts.Cancel();
Console.CancelKeyPress += (_, __) => cts.Cancel();

await tcpServer.StartAsync(cts.Token);
