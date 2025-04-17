namespace PicoHex.Protocols.CoAP;

public class CoapOption
{
    public ushort Number { get; set; }    // 选项编号（如 Uri-Path=11）
    public byte[] Value { get; set; }     // 选项值（二进制格式）
}