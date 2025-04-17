namespace PicoHex.Protocols.MQTT;

public abstract class MqttPacket
{
    public MqttPacketType PacketType { get; protected set; }
    public byte Flags { get; protected set; } // 报文标志位

    // 解析和序列化方法
    public abstract void Parse(byte[] data);
    public abstract byte[] Serialize();
}