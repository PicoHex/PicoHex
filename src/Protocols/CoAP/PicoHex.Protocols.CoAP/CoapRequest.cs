namespace PicoHex.Protocols.CoAP;

public class CoapRequest
{
    // CoAP 方法（Code 字段的十进制值，参考 RFC7252）
    public enum Method
    {
        GET = 1,
        POST = 2,
        PUT = 3,
        DELETE = 4
    }

    public Method RequestMethod { get; set; }
    public string Uri { get; set; }            // 请求的URI（如 "coap://example.com/sensor"）
    public byte[] Payload { get; set; }        // 请求负载（可选）
    public List<CoapOption> Options { get; }   // CoAP 选项（如 Uri-Path、Content-Format）
    public byte[] Token { get; set; }          // Token（用于匹配响应）

    public CoapRequest()
    {
        Options = new List<CoapOption>();
        Token = GenerateRandomToken(4);        // 默认生成4字节随机Token
    }

    /// <summary>
    /// 设置URI路径（自动分解为 Uri-Path 选项）
    /// </summary>
    public void SetUriPath(string uri)
    {
        Uri = uri;
        // 分割路径部分（例如 "/sensor/temp" -> ["sensor", "temp"]）
        var pathSegments = uri.Trim('/').Split('/');
        foreach (var segment in pathSegments)
        {
            AddOption(CoapOptionNumber.UriPath, Encoding.UTF8.GetBytes(segment));
        }
    }

    /// <summary>
    /// 添加选项（如 Content-Format、Uri-Query）
    /// </summary>
    public void AddOption(CoapOptionNumber optionNumber, byte[] value)
    {
        Options.Add(new CoapOption { Number = (ushort)optionNumber, Value = value });
    }

    /// <summary>
    /// 将请求转换为 CoapMessage（用于发送）
    /// </summary>
    public CoapMessage ToCoapMessage()
    {
        var message = new CoapMessage
        {
            Version = 1,
            Type = 0, // CON（需确认的请求）
            TokenLength = (byte)Token.Length,
            Token = Token,
            Code = (byte)RequestMethod,
            MessageId = GenerateMessageId(), // 需实现消息ID生成逻辑
            Options = Options,
            Payload = Payload
        };
        return message;
    }

    // 生成随机Token（简化示例）
    private static byte[] GenerateRandomToken(int length)
    {
        var random = new Random();
        byte[] token = new byte[length];
        random.NextBytes(token);
        return token;
    }

    // 生成消息ID（简化示例：递增计数器）
    private static ushort _messageIdCounter = 0;
    private static ushort GenerateMessageId() => _messageIdCounter++;
}