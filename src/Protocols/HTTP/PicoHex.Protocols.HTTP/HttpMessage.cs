namespace PicoHex.Protocols.HTTP;

public abstract class HttpMessage
{
    public string Version { get; set; } = "HTTP/1.1";
    public Dictionary<string, string> Headers { get; } = new(StringComparer.OrdinalIgnoreCase);
    public byte[]? Body { get; set; }

    // 解析或生成报文
    public abstract void Parse(byte[] rawData);
    public abstract byte[] Serialize();
}
