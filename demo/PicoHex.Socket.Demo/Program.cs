// See https://aka.ms/new-console-template for more information

using System.Net;
using System.Text;
using PicoHex.Socket;

var tcpListener = new TcpListener(new IPEndPoint(IPAddress.Any, 8080));

tcpListener.ClientConnected += clientId => Console.WriteLine($"Client connected: {clientId}");

tcpListener.ClientDisconnected += clientId => Console.WriteLine($"Client disconnected: {clientId}");

tcpListener.ErrorOccurred += ex => Console.WriteLine($"Error occurred: {ex}");

tcpListener.Start();

// 保持运行
await Task.Delay(-1);

// 服务端代码（使用之前的UdpListener）
var udpListener = new UdpListener(new IPEndPoint(IPAddress.Any, 514));
udpListener.Start();

udpListener.DataReceived += (data, ep) =>
{
    Console.WriteLine($"Server received: {Encoding.UTF8.GetString(data)}");
    _ = udpListener.SendAsync("Response"u8.ToArray(), ep);
};

// 客户端代码
_ = Task.Run(async () =>
{
    await using var client = new UdpClient();
    var response = await client.RequestAsync(
        Encoding.UTF8.GetBytes("Hello"),
        new IPEndPoint(IPAddress.Loopback, 514),
        TimeSpan.FromSeconds(2)
    );
    Console.WriteLine($"Client got: {Encoding.UTF8.GetString(response)}");
});
