using PicoHex.Server.Abstractions;

namespace PicoHex.Server.Test;

public class UdpServerTests
{
    [Fact]
    public async Task UdpServer_Should_Handle_Received_Datagram()
    {
        // Arrange
        var ipAddress = IPAddress.Loopback;
        ushort port = 5001;
        var mockHandler = new Mock<IUdpHandler>();
        var mockLogger = new Mock<ILogger<UdpServer>?>();

        mockHandler
            .Setup(
                h =>
                    h.HandleAsync(
                        It.IsAny<byte[]>(),
                        It.IsAny<EndPoint>(),
                        It.IsAny<CancellationToken>()
                    )
            )
            .Returns(ValueTask.CompletedTask);

        var server = new UdpServer(ipAddress, port, () => mockHandler.Object, mockLogger.Object);

        var cts = new CancellationTokenSource();
        var serverTask = server.StartAsync(cts.Token);

        // Act
        using var udpClient = new UdpClient();
        udpClient.Connect(ipAddress, port);
        var message = Encoding.UTF8.GetBytes("Hello UDP Server");
        await udpClient.SendAsync(message, message.Length);

        // Allow some time for the server to process
        await Task.Delay(500, cts.Token);

        // Assert
        mockHandler.Verify(
            h =>
                h.HandleAsync(
                    It.IsAny<byte[]>(),
                    It.IsAny<EndPoint>(),
                    It.IsAny<CancellationToken>()
                ),
            Times.Once
        );

        // Cleanup
        await cts.CancelAsync();
        await serverTask;
    }
}
