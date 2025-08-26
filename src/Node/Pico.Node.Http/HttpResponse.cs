namespace Pico.Node.Http;

public class HttpResponse
{
    public int StatusCode { get; set; } = 200;
    public string StatusText { get; set; } = "OK";
    public Dictionary<string, string> Headers { get; } =
        new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
    public string Body { get; set; } = string.Empty;
}
