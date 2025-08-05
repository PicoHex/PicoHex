namespace Pico.Proto.MQTT;

public static class MqttParser
{
    public static MqttMessage Parse(Stream stream)
    {
        byte fixedHeader = ReadByte(stream);
        var packetType = (MqttControlPacketType)(fixedHeader >> 4);
        byte flags = (byte)(fixedHeader & 0x0F);
        int remainingLength = ReadRemainingLength(stream);

        return packetType switch
        {
            MqttControlPacketType.CONNECT => ParseConnect(stream, flags, remainingLength),
            MqttControlPacketType.PUBLISH => ParsePublish(stream, flags, remainingLength),
            MqttControlPacketType.SUBSCRIBE => ParseSubscribe(stream, flags, remainingLength),
            MqttControlPacketType.DISCONNECT => new MqttMessage { PacketType = packetType },
            _ => throw new MqttException($"Unsupported packet type: {packetType}")
        };
    }

    private static MqttConnectMessage ParseConnect(Stream stream, byte flags, int remainingLength)
    {
        var msg = new MqttConnectMessage
        {
            PacketType = MqttControlPacketType.CONNECT,
            Flags = flags,
            RemainingLength = remainingLength
        };

        // 协议名
        msg.ProtocolName = ReadString(stream);
        msg.ProtocolLevel = ReadByte(stream);

        // 连接标志
        byte connectFlags = ReadByte(stream);
        msg.CleanSession = (connectFlags & 0x02) != 0;

        msg.KeepAlive = ReadUInt16(stream);
        msg.ClientId = ReadString(stream);

        return msg;
    }

    private static MqttPublishMessage ParsePublish(Stream stream, byte flags, int remainingLength)
    {
        var msg = new MqttPublishMessage
        {
            PacketType = MqttControlPacketType.PUBLISH,
            Flags = flags,
            RemainingLength = remainingLength,
            QoS = (byte)((flags & 0x06) >> 1)
        };

        msg.TopicName = ReadString(stream);

        if (msg.QoS > 0)
        {
            msg.PacketId = ReadUInt16(stream);
        }

        int payloadLength = remainingLength - (msg.TopicName.Length + 2 + (msg.QoS > 0 ? 2 : 0));
        msg.Payload = ReadBytes(stream, payloadLength);

        return msg;
    }

    private static MqttSubscribeMessage ParseSubscribe(
        Stream stream,
        byte flags,
        int remainingLength
    )
    {
        var msg = new MqttSubscribeMessage
        {
            PacketType = MqttControlPacketType.SUBSCRIBE,
            Flags = flags,
            RemainingLength = remainingLength,
            PacketId = ReadUInt16(stream)
        };

        int bytesRead = 2;
        while (bytesRead < remainingLength)
        {
            var sub = new Subscription { TopicFilter = ReadString(stream), QoS = ReadByte(stream) };
            msg.Subscriptions.Add(sub);
            bytesRead += sub.TopicFilter.Length + 3; // 2字节长度 + 字符串 + 1字节QoS
        }

        return msg;
    }

    #region 基础读写方法
    private static byte ReadByte(Stream stream)
    {
        int value = stream.ReadByte();
        if (value == -1)
            throw new EndOfStreamException();
        return (byte)value;
    }

    private static ushort ReadUInt16(Stream stream)
    {
        byte[] buffer = new byte[2];
        stream.Read(buffer, 0, 2);
        return (ushort)((buffer[0] << 8) | buffer[1]);
    }

    private static string ReadString(Stream stream)
    {
        ushort length = ReadUInt16(stream);
        byte[] buffer = new byte[length];
        stream.Read(buffer, 0, length);
        return System.Text.Encoding.UTF8.GetString(buffer);
    }

    private static byte[] ReadBytes(Stream stream, int length)
    {
        byte[] buffer = new byte[length];
        stream.Read(buffer, 0, length);
        return buffer;
    }

    private static int ReadRemainingLength(Stream stream)
    {
        int multiplier = 1;
        int value = 0;
        byte digit;

        do
        {
            digit = ReadByte(stream);
            value += (digit & 0x7F) * multiplier;
            multiplier *= 128;
        } while ((digit & 0x80) != 0);

        return value;
    }
    #endregion
}
