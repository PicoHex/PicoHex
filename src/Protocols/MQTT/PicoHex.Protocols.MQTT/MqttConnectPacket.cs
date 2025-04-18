namespace PicoHex.Protocols.MQTT;

public class MqttConnectPacket : MqttPacket
{
    public string ProtocolName { get; set; } = "MQTT";
    public byte ProtocolLevel { get; set; } = 0x04; // MQTT 3.1.1
    public bool CleanSession { get; set; }
    public ushort KeepAlive { get; set; }
    public string ClientId { get; set; }

    public override void Parse(byte[] data)
    {
        var offset = 0;

        // 解析固定头
        PacketType = (MqttPacketType)(data[0] >> 4);
        Flags = (byte)(data[0] & 0x0F);
        offset++;

        // 解析剩余长度
        var (remainingLength, bytesRead) = MqttUtils.DecodeRemainingLength(data, offset);
        offset += bytesRead;

        // 解析协议名（UTF-8字符串，长度前缀为2字节）
        var protocolNameLength = (ushort)((data[offset] << 8) | data[offset + 1]);
        offset += 2;
        ProtocolName = Encoding.UTF8.GetString(data, offset, protocolNameLength);
        offset += protocolNameLength;

        // 协议级别和标志位
        ProtocolLevel = data[offset++];
        var connectFlags = data[offset++];
        CleanSession = (connectFlags & 0x02) != 0;

        // 心跳间隔（2字节）
        KeepAlive = (ushort)((data[offset] << 8) | data[offset + 1]);
        offset += 2;

        // 客户端ID（UTF-8字符串）
        var clientIdLength = (ushort)((data[offset] << 8) | data[offset + 1]);
        offset += 2;
        ClientId = Encoding.UTF8.GetString(data, offset, clientIdLength);
    }

    public override byte[] Serialize()
    {
        var stream = new MemoryStream();

        // 固定头（报文类型 + 标志位）
        var fixedHeader = (byte)((byte)MqttPacketType.CONNECT << 4);
        stream.WriteByte(fixedHeader);

        // 可变头 + 载荷的二进制数据
        var variableHeader = new List<byte>();

        // 协议名（MQTT）
        var protocolNameBytes = Encoding.UTF8.GetBytes(ProtocolName);
        variableHeader.AddRange([0x00, (byte)protocolNameBytes.Length]);
        variableHeader.AddRange(protocolNameBytes);

        // 协议级别和标志位
        variableHeader.Add(ProtocolLevel);
        byte connectFlags = 0x02; // CleanSession=1
        variableHeader.Add(connectFlags);

        // 心跳间隔
        variableHeader.Add((byte)(KeepAlive >> 8));
        variableHeader.Add((byte)KeepAlive);

        // 客户端ID
        var clientIdBytes = Encoding.UTF8.GetBytes(ClientId);
        variableHeader.AddRange([(byte)(clientIdBytes.Length >> 8), (byte)clientIdBytes.Length]);
        variableHeader.AddRange(clientIdBytes);

        // 编码剩余长度并写入
        var (remainingLengthBytes, _) = MqttUtils.EncodeRemainingLength(variableHeader.Count);
        stream.Write(remainingLengthBytes, 0, remainingLengthBytes.Length);
        stream.Write(variableHeader.ToArray(), 0, variableHeader.Count);

        return stream.ToArray();
    }
}
