// See https://aka.ms/new-console-template for more information

var cts = new CancellationTokenSource();
var server = new HttpServer("127.0.0.1", 8080);
_ = server.StartAsync(cts.Token);
Console.WriteLine("Server is running. Press any key to exit.");
Console.ReadKey();
cts.Cancel();