namespace Pico.Node.Http;

public class HttpRequest
{
    public string Method { get; set; } = "GET";
    public string Path { get; set; } = "/";
    public string Version { get; set; } = "HTTP/1.1";
    public Dictionary<string, string> Headers { get; } =
        new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
    public string Body { get; set; } = string.Empty;
}
