namespace PicoHex.Protocols.MQTT;

public class MqttSubackMessage : MqttMessage
{
    public ushort PacketId { get; set; }
    public List<byte> ReturnCodes { get; } = new List<byte>();
}
