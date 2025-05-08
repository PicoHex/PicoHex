namespace PicoHex.Protocols.MQTT;

public class Subscription
{
    public string TopicFilter { get; set; }
    public byte QoS { get; set; }
}
