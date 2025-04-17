namespace PicoHex.Protocols.MQTT;

public static class MqttUtils
{
    // 编码剩余长度（返回字节数组和写入的字节数）
    public static (byte[], int) EncodeRemainingLength(int length)
    {
        var buffer = new List<byte>();
        do
        {
            byte digit = (byte)(length % 128);
            length /= 128;
            if (length > 0) digit |= 0x80;
            buffer.Add(digit);
        } while (length > 0);
        return (buffer.ToArray(), buffer.Count);
    }

    // 解码剩余长度（返回剩余长度和消耗的字节数）
    public static (int, int) DecodeRemainingLength(byte[] data, int offset)
    {
        int multiplier = 1;
        int value = 0;
        int bytesRead = 0;
        do
        {
            value += (data[offset + bytesRead] & 0x7F) * multiplier;
            multiplier *= 128;
            bytesRead++;
        } while ((data[offset + bytesRead - 1] & 0x80) != 0);
        return (value, bytesRead);
    }
}
