namespace PicoHex.Protocols.MQTT;

public class MqttPublishMessage : MqttMessage
{
    public string TopicName { get; set; }
    public ushort PacketId { get; set; }
    public byte[] Payload { get; set; }
    public byte QoS { get; set; }
}
