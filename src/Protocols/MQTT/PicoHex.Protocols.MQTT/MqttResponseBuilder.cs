namespace PicoHex.Protocols.MQTT;

// MQTT 响应生成器
public static class MqttResponseBuilder
{
    public static byte[] BuildConnack(byte returnCode)
    {
        using var ms = new MemoryStream();
        // 固定报头
        ms.WriteByte((byte)((byte)MqttControlPacketType.CONNACK << 4));
        ms.WriteByte(0x02); // 剩余长度
        // 可变报头
        ms.WriteByte(0x00); // 保留
        ms.WriteByte(returnCode);
        return ms.ToArray();
    }

    public static byte[] BuildSuback(ushort packetId, List<byte> returnCodes)
    {
        using var ms = new MemoryStream();
        // 固定报头
        ms.WriteByte((byte)((byte)MqttControlPacketType.SUBACK << 4));
        byte[] variableHeader = new byte[2 + returnCodes.Count];
        variableHeader[0] = (byte)(packetId >> 8);
        variableHeader[1] = (byte)packetId;
        returnCodes.CopyTo(variableHeader, 2);

        // 写入剩余长度
        WriteRemainingLength(ms, variableHeader.Length);
        ms.Write(variableHeader, 0, variableHeader.Length);
        return ms.ToArray();
    }

    private static void WriteRemainingLength(MemoryStream ms, int length)
    {
        do
        {
            byte digit = (byte)(length % 128);
            length /= 128;
            if (length > 0)
                digit |= 0x80;
            ms.WriteByte(digit);
        } while (length > 0);
    }
}
