namespace PicoHex.Protocols.MQTT;

public class MqttConnackPacket : MqttPacket
{
    public byte ReturnCode { get; set; } // 0=连接成功

    public override void Parse(byte[] data)
    {
        PacketType = (MqttPacketType)(data[0] >> 4);
        Flags = (byte)(data[0] & 0x0F);
        ReturnCode = data[3]; // 简化解析逻辑
    }

    public override byte[] Serialize()
    {
        var stream = new MemoryStream();
        stream.WriteByte((byte)((byte)MqttPacketType.CONNACK << 4));
        stream.WriteByte(0x02); // 剩余长度=2
        stream.WriteByte(0x00); // 确认标志
        stream.WriteByte(ReturnCode);
        return stream.ToArray();
    }
}
