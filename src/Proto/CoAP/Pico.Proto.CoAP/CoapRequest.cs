using Pico.Proto.CoAP;

namespace PicoHex.Protocols.CoAP;

public class CoapRequest : CoapMessage
{
    public CoapMethod Method { get; set; } // GET/POST/PUT/DELETE
    public string Path { get; set; } // 解析后的URI路径
}
