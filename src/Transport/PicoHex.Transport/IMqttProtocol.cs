namespace PicoHex.Transport;

public interface IMqttProtocol : IApplicationProtocol, IPublisherSubscriber
{
    Task ConnectToBrokerAsync(byte[] connectPacket);
    Task DisconnectFromBrokerAsync(byte[] disconnectPacket);
    Task SetQoSLevel(QoSLevel level);
}

public enum QoSLevel
{
    AtMostOnce,
    AtLeastOnce,
    ExactlyOnce
}
