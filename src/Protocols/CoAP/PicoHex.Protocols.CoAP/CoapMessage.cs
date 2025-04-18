namespace PicoHex.Protocols.CoAP;

public abstract class CoapMessage
{
    public byte Version { get; set; } = 0x01; // 版本固定1
    public byte Type { get; set; } // 0:CON, 1:NON, 2:ACK, 3:RST
    public byte TokenLength { get; set; } // Token长度 (0-8 bytes)
    public byte[] Token { get; set; } // 请求标识
    public List<CoapOption> Options { get; set; } = new();
    public byte[] Payload { get; set; }
}
