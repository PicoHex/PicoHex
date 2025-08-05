namespace Pico.Proto.MQTT;

public class MqttConnackMessage : MqttMessage
{
    public byte ReturnCode { get; set; }
}
