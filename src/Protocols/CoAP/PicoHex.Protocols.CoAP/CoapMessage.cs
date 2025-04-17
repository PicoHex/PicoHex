namespace PicoHex.Protocols.CoAP;

public class CoapMessage
{
    // 头部字段
    public byte Version { get; set; }     // 版本（必须为 1）
    public byte Type { get; set; }        // 消息类型（0=CON, 1=NON, 2=ACK, 3=RST）
    public byte TokenLength { get; set; } // Token 长度（0-8）
    public byte Code { get; set; }        // 方法（如 0x01=GET）或状态码（如 0x45=2.05 Content）
    public ushort MessageId { get; set; } // 消息ID（用于匹配请求/响应）

    // Token 和 Payload
    public byte[] Token { get; set; }     // Token（长度由 TKL 指定）
    public byte[] Payload { get; set; }   // Payload（可选）

    // 选项（Options）
    public List<CoapOption> Options { get; set; } = new();
}
