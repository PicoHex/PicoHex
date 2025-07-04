namespace Pico.SVR.Http;

public class HttpResponse
{
    public string ProtocolVersion { get; set; } = "HTTP/1.1";
    public int StatusCode { get; set; }
    public string StatusDescription { get; set; }
    public Dictionary<string, string> Headers { get; set; } = new();
    public byte[] Body { get; set; }
}
