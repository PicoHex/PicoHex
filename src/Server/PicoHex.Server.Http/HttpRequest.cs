namespace PicoHex.Server.Http;

public class HttpRequest
{
    public string Method { get; set; }
    public string Path { get; set; }
    public string ProtocolVersion { get; set; }
    public Dictionary<string, string> Headers { get; set; } = new();
    public byte[] Body { get; set; }
}
