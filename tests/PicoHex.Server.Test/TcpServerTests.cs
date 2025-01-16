namespace PicoHex.Server.Test;

public class TcpServerTests
{
    [Fact]
    public async Task TcpServer_Should_Handle_Client_Connection()
    {
        // Arrange
        var ipAddress = IPAddress.Loopback;
        var port = 5000;
        var mockHandler = new Mock<ITcpHandler>();
        var mockLogger = new Mock<ILogger<TcpServer>>();

        mockHandler
            .Setup(h => h.HandleAsync(It.IsAny<NetworkStream>(), It.IsAny<CancellationToken>()))
            .Returns(ValueTask.CompletedTask);

        var server = new TcpServer(
            ipAddress,
            port,
            () => mockHandler.Object,
            mockLogger.Object,
            maxConcurrentConnections: 1
        );

        var cts = new CancellationTokenSource();
        var serverTask = server.StartAsync(cts.Token);

        // Act
        using var client = new TcpClient();
        await client.ConnectAsync(ipAddress, port, cts.Token);
        var stream = client.GetStream();
        var message = Encoding.UTF8.GetBytes("Hello Server");
        await stream.WriteAsync(message, 0, message.Length, cts.Token);

        // Allow some time for the server to process
        await Task.Delay(500, cts.Token);

        // Assert
        mockHandler.Verify(
            h => h.HandleAsync(It.IsAny<NetworkStream>(), It.IsAny<CancellationToken>()),
            Times.Once
        );

        // Cleanup
        await cts.CancelAsync();
        await serverTask;
    }
}
