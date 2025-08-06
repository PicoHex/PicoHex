namespace Pico.Node.Http;

internal class HttpResponse
{
    public int StatusCode { get; set; } = 200;
    public string StatusText { get; set; } = "OK";
    public string Body { get; set; } = string.Empty;
    public string ContentType { get; set; } = "text/plain";
}
