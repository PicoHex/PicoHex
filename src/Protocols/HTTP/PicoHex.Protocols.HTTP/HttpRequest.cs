namespace PicoHex.Protocols.HTTP;

public class HttpRequest
{
    public string Method { get; set; }
    public string Url { get; set; }
    public string ProtocolVersion { get; set; }
    public Dictionary<string, string> Headers { get; } = new Dictionary<string, string>();
    public byte[]? Body { get; set; }
}
