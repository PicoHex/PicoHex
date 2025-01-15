namespace PicoHex.HttpServer;

public class HttpRequest
{
    public string Method { get; set; }
    public string Path { get; set; }
    public string ProtocolVersion { get; set; }
    public Dictionary<string, string> Headers { get; set; } = new Dictionary<string, string>();
    public byte[] Body { get; set; }
}