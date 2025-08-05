namespace Pico.Proto.MQTT;

public class MqttConnectMessage : MqttMessage
{
    public string ProtocolName { get; set; }
    public byte ProtocolLevel { get; set; }
    public bool CleanSession { get; set; }
    public ushort KeepAlive { get; set; }
    public string ClientId { get; set; }
}
