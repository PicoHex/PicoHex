// See https://aka.ms/new-console-template for more information

var serviceProvider = new ServiceCollection()
    .AddLogging(builder => builder.AddConsole())
    .AddTransient<IStreamHandler, HttpHandler>()
    .BuildServiceProvider();

var logger = serviceProvider.GetRequiredService<ILogger<TcpServer>>();
IStreamHandler HandlerFactory() => serviceProvider.GetRequiredService<IStreamHandler>();

var tcpServer = new TcpServer(IPAddress.Loopback, 8080, HandlerFactory, logger);

Console.WriteLine("Starting TCP server on http://localhost:8080...");
var cts = new CancellationTokenSource();

AppDomain.CurrentDomain.ProcessExit += (_, __) => cts.Cancel();
Console.CancelKeyPress += (_, __) => cts.Cancel();

await tcpServer.StartAsync(cts.Token);
