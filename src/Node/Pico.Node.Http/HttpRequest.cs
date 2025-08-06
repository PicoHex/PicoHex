namespace Pico.Node.Http;

internal class HttpRequest
{
    public required string Method { get; set; }
    public required string Path { get; set; }
    public required string Protocol { get; set; }
    public required Dictionary<string, string> Headers { get; set; }
    public string Body { get; set; } = string.Empty;
    public required string ClientEndpoint { get; set; }
    public Dictionary<string, string> RouteParameters { get; set; } = new();
}
