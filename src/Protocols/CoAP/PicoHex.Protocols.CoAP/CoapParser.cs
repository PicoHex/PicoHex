namespace PicoHex.Protocols.CoAP;

public class CoapParser
{
    public static CoapMessage ParseCoapMessage(byte[] data)
    {
        if (data is null || data.Length < 4)
            throw new ArgumentException("Invalid CoAP message: too short");

        var message = new CoapMessage();

        // --- 解析头部（前4字节） ---
        var firstByte = data[0];
        message.Version = (byte)((firstByte >> 6) & 0x03); // 版本（第7-8位）
        if (message.Version != 1)
            throw new NotSupportedException($"CoAP version {message.Version} not supported");

        message.Type = (byte)((firstByte >> 4) & 0x03); // 类型（第5-6位）
        message.TokenLength = (byte)(firstByte & 0x0F); // Token长度（第1-4位）
        message.Code = data[1]; // 方法/状态码（第2字节）
        message.MessageId = (ushort)((data[2] << 8) | data[3]); // 消息ID（第3-4字节）

        var offset = 4; // 当前解析位置

        // --- 解析 Token ---
        if (message.TokenLength > 0)
        {
            if (offset + message.TokenLength > data.Length)
                throw new ArgumentException("Token length exceeds message length");

            message.Token = new byte[message.TokenLength];
            Array.Copy(data, offset, message.Token, 0, message.TokenLength);
            offset += message.TokenLength;
        }

        // --- 解析 Options ---
        ushort previousOptionNumber = 0; // 用于计算 Delta
        while (offset < data.Length && data[offset] != 0xFF)
        {
            var option = new CoapOption();
            var optionHeader = data[offset++];

            // 解析 Delta 和 Length
            var delta = (byte)((optionHeader >> 4) & 0x0F);
            var length = (byte)(optionHeader & 0x0F);

            switch (delta)
            {
                // 处理扩展的 Delta 和 Length（RFC7252 3.1节）
                case 13:
                    delta = (byte)(data[offset++] + 13);
                    break;
                case 14:
                    delta = (byte)(((data[offset] << 8) | data[offset + 1]) + 269);
                    offset += 2;
                    break;
                case 15:
                    throw new ArgumentException("Invalid option delta 15");
            }

            switch (length)
            {
                case 13:
                    length = (byte)(data[offset++] + 13);
                    break;
                case 14:
                    length = (byte)(((data[offset] << 8) | data[offset + 1]) + 269);
                    offset += 2;
                    break;
                case 15:
                    throw new ArgumentException("Invalid option length 15");
            }

            // 计算实际 Option Number
            option.Number = (ushort)(previousOptionNumber + delta);
            previousOptionNumber = option.Number;

            // 读取 Option Value
            if (offset + length > data.Length)
                throw new ArgumentException("Option value exceeds message length");

            option.Value = new byte[length];
            Array.Copy(data, offset, option.Value, 0, length);
            offset += length;

            message.Options.Add(option);
        }

        // --- 解析 Payload（如果有 0xFF 标记） ---
        if (offset >= data.Length || data[offset] != 0xFF)
            return message;
        offset++;
        var payloadLength = data.Length - offset;
        if (payloadLength <= 0)
            return message;
        message.Payload = new byte[payloadLength];
        Array.Copy(data, offset, message.Payload, 0, payloadLength);

        return message;
    }
}
