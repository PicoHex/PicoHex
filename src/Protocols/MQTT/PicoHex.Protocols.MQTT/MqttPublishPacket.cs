namespace PicoHex.Protocols.MQTT;

public class MqttPublishPacket : MqttPacket
{
    public string Topic { get; set; }
    public byte[] Payload { get; set; }
    public ushort PacketIdentifier { get; set; } // QoS > 0 时有效
    public int QoS
    {
        get => (Flags & 0x06) >> 1;
        set => Flags = (byte)((Flags & 0xF9) | (value << 1));
    }

    public override void Parse(byte[] data)
    {
        int offset = 0;

        // 固定头
        PacketType = (MqttPacketType)(data[0] >> 4);
        Flags = (byte)(data[0] & 0x0F);
        offset++;

        // 剩余长度
        var (remainingLength, bytesRead) = MqttUtils.DecodeRemainingLength(data, offset);
        offset += bytesRead;

        // 主题名（UTF-8字符串）
        ushort topicLength = (ushort)((data[offset] << 8) | data[offset + 1]);
        offset += 2;
        Topic = Encoding.UTF8.GetString(data, offset, topicLength);
        offset += topicLength;

        // Packet Identifier（QoS > 0 时存在）
        if ((Flags & 0x06) != 0) // QoS > 0
        {
            PacketIdentifier = (ushort)((data[offset] << 8) | data[offset + 1]);
            offset += 2;
        }

        // 载荷
        Payload = new byte[remainingLength - (offset - bytesRead - 1)];
        Array.Copy(data, offset, Payload, 0, Payload.Length);
    }

    public override byte[] Serialize()
    {
        if (QoS > 0 && PacketIdentifier == 0)
            PacketIdentifier = MqttPacketIdGenerator.GeneratePacketId(); // 需实现生成逻辑
        var stream = new MemoryStream();

        // 固定头（QoS=0）
        byte fixedHeader = (byte)((byte)MqttPacketType.PUBLISH << 4);
        stream.WriteByte(fixedHeader);

        // 可变头 + 载荷
        var variableHeader = new List<byte>();

        // 主题名
        byte[] topicBytes = Encoding.UTF8.GetBytes(Topic);
        variableHeader.AddRange(new byte[] { (byte)(topicBytes.Length >> 8), (byte)topicBytes.Length });
        variableHeader.AddRange(topicBytes);

        // 载荷
        variableHeader.AddRange(Payload);

        // 编码剩余长度并写入
        var (remainingLengthBytes, _) = MqttUtils.EncodeRemainingLength(variableHeader.Count);
        stream.Write(remainingLengthBytes, 0, remainingLengthBytes.Length);
        stream.Write(variableHeader.ToArray(), 0, variableHeader.Count);

        return stream.ToArray();
    }
}
