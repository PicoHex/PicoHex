﻿// See https://aka.ms/new-console-template for more information

// Step 1: Create the IoC container

var svcRegistry = Bootstrap.CreateContainer();

// var tcpOption = new TcpNodeOptions { IpAddress = IPAddress.Any, Port = 8080 };
// svcRegistry
//     // .AddLogging(builder => builder.AddConsole())
//     .RegisterTransient<ITcpHandler, HttpHandler>()
//     .RegisterTransient<Func<ITcpHandler>>(sp => sp.Resolve<ITcpHandler>)
//     .RegisterSingle<TcpNode>(sp => new TcpNode(
//         tcpOption,
//         sp.Resolve<Func<ITcpHandler>>(),
//         sp.Resolve<ILogger<TcpNode>>()
//     ));

var tcpOptionV6 = new TcpNodeOptions { IpAddress = IPAddress.Any, Port = 8081 };
svcRegistry
    // .AddLogging(builder => builder.AddConsole())
    .RegisterTransient<IPipelineHandler, HttpHandlerV2>()
    .RegisterTransient<Func<IPipelineHandler>>(sp => sp.Resolve<IPipelineHandler>)
    .RegisterSingle<TcpNode>(sp => new TcpNode(
        tcpOptionV6,
        sp.Resolve<Func<IPipelineHandler>>(),
        sp.Resolve<ILogger<TcpNode>>()
    ));

svcRegistry.RegisterLogger();

var svcProvider = svcRegistry.GetProvider();

var tcpServerV6 = svcProvider.Resolve<TcpNode>();

var cts = new CancellationTokenSource();

AppDomain.CurrentDomain.ProcessExit += (_, _) => cts.Cancel();
Console.CancelKeyPress += (_, _) => cts.Cancel();

await tcpServerV6.StartAsync(cts.Token);

Console.ReadLine();

await tcpServerV6.StopAsync(cts.Token);
await tcpServerV6.DisposeAsync();
