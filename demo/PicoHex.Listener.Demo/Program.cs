// See https://aka.ms/new-console-template for more information

using System.Net;
using PicoHex.Tcp.Server;

var tcpListener = new TcpListener(new IPEndPoint(IPAddress.Any, 8080));

tcpListener.ClientConnected += clientId => Console.WriteLine($"Client connected: {clientId}");

tcpListener.ClientDisconnected += clientId => Console.WriteLine($"Client disconnected: {clientId}");

tcpListener.ErrorOccurred += ex => Console.WriteLine($"Error occurred: {ex}");

tcpListener.Start();

// 保持运行
await Task.Delay(-1);

await using var udpListener = new UdpListener(new IPEndPoint(IPAddress.Any, 514));
udpListener.DataReceived += (data, ep) =>
    Console.WriteLine($"Received {data.Length} bytes from {ep}");
udpListener.ErrorOccurred += ex => Console.WriteLine($"Error: {ex.Message}");

udpListener.Start();

// 保持运行
await Task.Delay(Timeout.InfiniteTimeSpan);
