namespace PicoHex.Protocols.CoAP;

public class CoapResponse
{
    // CoAP 响应状态码（参考 RFC7252 12.1.2节）
    public enum StatusCode
    {
        Created = 65,          // 2.01 Created
        Deleted = 66,          // 2.02 Deleted
        Valid = 67,            // 2.03 Valid
        Changed = 68,          // 2.04 Changed
        Content = 69,          // 2.05 Content
        BadRequest = 128,      // 4.00 Bad Request
        NotFound = 132,        // 4.04 Not Found
        InternalError = 160    // 5.00 Internal Server Error
    }

    public StatusCode Code { get; set; }
    public byte[] Payload { get; set; }
    public List<CoapOption> Options { get; set; } = new List<CoapOption>();

    /// <summary>
    /// 从 CoapMessage 解析响应
    /// </summary>
    public static CoapResponse FromCoapMessage(CoapMessage message)
    {
        return new CoapResponse
        {
            Code = (StatusCode)message.Code,
            Payload = message.Payload,
            Options = message.Options
        };
    }

    /// <summary>
    /// 设置 Content-Format 选项（如 text/plain=0）
    /// </summary>
    public void SetContentFormat(ushort format)
    {
        AddOption(CoapOptionNumber.ContentFormat, BitConverter.GetBytes(format));
    }

    public void AddOption(CoapOptionNumber optionNumber, byte[] value)
    {
        Options.Add(new CoapOption { Number = (ushort)optionNumber, Value = value });
    }
}
