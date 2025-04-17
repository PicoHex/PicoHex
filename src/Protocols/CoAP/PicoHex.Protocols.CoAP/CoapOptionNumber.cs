namespace PicoHex.Protocols.CoAP;

public enum CoapOptionNumber : ushort
{
    UriPath = 11,        // URI路径（分段）
    ContentFormat = 12,  // 内容格式（如 0=text/plain, 40=application/json）
    UriQuery = 15,       // URI查询参数（如 "temperature=25"）
    Observe = 6          // 观察模式（注册/取消）
}