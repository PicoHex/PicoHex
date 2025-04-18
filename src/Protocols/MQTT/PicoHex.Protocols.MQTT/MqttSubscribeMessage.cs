namespace PicoHex.Protocols.MQTT;

public class MqttSubscribeMessage : MqttMessage
{
    public ushort PacketId { get; set; }
    public List<Subscription> Subscriptions { get; } = new List<Subscription>();
}
