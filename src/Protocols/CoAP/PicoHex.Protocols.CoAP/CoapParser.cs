namespace PicoHex.Protocols.CoAP;

public static class CoapParser
{
    private const byte PayloadMarker = 0xFF;

    // 解析请求
    public static CoapRequest ParseRequest(byte[] datagram)
    {
        CoapRequest request = new CoapRequest();
        int index = 0;

        // 解析头部
        byte header = datagram[index++];
        request.Version = (byte)((header & 0xC0) >> 6); // 前2 bits
        request.Type = (byte)((header & 0x30) >> 4); // 中间2 bits
        request.TokenLength = (byte)(header & 0x0F); // 最后4 bits

        // 方法代码（如 GET=0x01）
        request.Method = (CoapMethod)datagram[index++];

        // 消息ID（2字节）
        ushort messageId = (ushort)((datagram[index++] << 8) | datagram[index++]);

        // 解析Token
        if (request.TokenLength > 0)
        {
            request.Token = new byte[request.TokenLength];
            Array.Copy(datagram, index, request.Token, 0, request.TokenLength);
            index += request.TokenLength;
        }

        // 解析选项
        int previousOptionNumber = 0;
        while (index < datagram.Length && datagram[index] != PayloadMarker)
        {
            CoapOption option = ParseOption(datagram, ref index, ref previousOptionNumber);
            request.Options.Add(option);

            // 特殊处理URI路径
            if (option.Number == CoapOptionNumber.UriPath)
            {
                request.Path += (request.Path == null ? "" : "/") + option.StringValue;
            }
        }

        // 解析负载（如果有）
        if (index < datagram.Length && datagram[index] == PayloadMarker)
        {
            index++; // 跳过0xFF
            request.Payload = new byte[datagram.Length - index];
            Array.Copy(datagram, index, request.Payload, 0, request.Payload.Length);
        }

        return request;
    }

    // 构建响应
    public static byte[] BuildResponse(CoapResponse response)
    {
        List<byte> bytes = new List<byte>();

        // 构建头部
        byte header = (byte)((response.Version << 6) | (response.Type << 4) | response.TokenLength);
        bytes.Add(header);
        bytes.Add((byte)response.Code);

        // 消息ID（示例使用固定值）
        bytes.Add(0x12);
        bytes.Add(0x34);

        // Token
        if (response.Token != null)
        {
            bytes.AddRange(response.Token);
        }

        // 选项（需要排序并计算delta）
        int previousOptionNumber = 0;
        foreach (var option in response.Options)
        {
            EncodeOption(option, bytes, ref previousOptionNumber);
        }

        // 负载
        if (response.Payload != null && response.Payload.Length > 0)
        {
            bytes.Add(PayloadMarker);
            bytes.AddRange(response.Payload);
        }

        return bytes.ToArray();
    }

    private static CoapOption ParseOption(byte[] data, ref int index, ref int previousOptionNumber)
    {
        byte deltaLength = data[index++];
        int delta = (deltaLength & 0xF0) >> 4;
        int length = deltaLength & 0x0F;

        // 处理扩展
        delta = ProcessOptionExtension(data, ref index, delta);
        length = ProcessOptionExtension(data, ref index, length);

        int optionNumber = previousOptionNumber + delta;
        previousOptionNumber = optionNumber;

        byte[] value = new byte[length];
        Array.Copy(data, index, value, 0, length);
        index += length;

        return new CoapOption { Number = (CoapOptionNumber)optionNumber, Value = value };
    }

    private static int ProcessOptionExtension(byte[] data, ref int index, int value)
    {
        if (value == 13)
        {
            value = data[index++] + 13;
        }
        else if (value == 14)
        {
            value = ((data[index++] << 8) + data[index++]) + 269;
        }
        else if (value == 15)
        {
            throw new CoapException("Invalid option delta/length");
        }
        return value;
    }

    private static void EncodeOption(
        CoapOption option,
        List<byte> bytes,
        ref int previousOptionNumber
    )
    {
        int delta = (int)option.Number - previousOptionNumber;
        previousOptionNumber = (int)option.Number;

        byte deltaByte = 0;
        byte lengthByte = (byte)(option.Value.Length < 13 ? option.Value.Length : 13);

        // 编码delta
        if (delta < 13)
        {
            deltaByte = (byte)(delta << 4);
        }
        else if (delta < 269)
        {
            deltaByte = 0xD0; // 13 + 0xD0=13<<4
            bytes.Add((byte)(delta - 13));
        }
        else
        {
            deltaByte = 0xE0;
            ushort extended = (ushort)(delta - 269);
            bytes.Add((byte)(extended >> 8));
            bytes.Add((byte)(extended & 0xFF));
        }

        // 编码length
        if (option.Value.Length >= 13)
        {
            if (option.Value.Length < 269)
            {
                lengthByte = 13;
                bytes.Add((byte)(option.Value.Length - 13));
            }
            else
            {
                lengthByte = 14;
                ushort extended = (ushort)(option.Value.Length - 269);
                bytes.Add((byte)(extended >> 8));
                bytes.Add((byte)(extended & 0xFF));
            }
        }

        bytes.Add((byte)(deltaByte | lengthByte));
        bytes.AddRange(option.Value);
    }
}
