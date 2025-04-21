namespace PicoHex.Transport;

public interface ICoapProtocol : IApplicationProtocol, IRequestResponder
{
    Task ObserveResourceAsync(Uri resourceUri, Func<byte[], Task> notificationHandler);
    Task SendMulticastAsync(byte[] message, IPEndPoint group);
}
