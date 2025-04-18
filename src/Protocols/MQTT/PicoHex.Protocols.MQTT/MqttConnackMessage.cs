namespace PicoHex.Protocols.MQTT;

public class MqttConnackMessage : MqttMessage
{
    public byte ReturnCode { get; set; }
}
