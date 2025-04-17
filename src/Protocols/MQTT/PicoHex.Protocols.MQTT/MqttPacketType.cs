namespace PicoHex.Protocols.MQTT;

public enum MqttPacketType : byte
{
    CONNECT = 1,
    CONNACK = 2,
    PUBLISH = 3,
    SUBSCRIBE = 8,
    SUBACK = 9
}