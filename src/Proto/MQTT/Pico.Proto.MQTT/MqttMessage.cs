namespace Pico.Proto.MQTT;

public class MqttMessage
{
    public MqttControlPacketType PacketType { get; set; }
    public byte Flags { get; set; }
    public int RemainingLength { get; set; }
}
