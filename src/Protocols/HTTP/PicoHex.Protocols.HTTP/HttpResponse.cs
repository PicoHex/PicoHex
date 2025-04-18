namespace PicoHex.Protocols.HTTP;

public class HttpResponse
{
    public string ProtocolVersion { get; set; } = "HTTP/1.1";
    public int StatusCode { get; set; } = 200;
    public string StatusDescription { get; set; } = "OK";
    public Dictionary<string, string> Headers { get; } = new();
    public byte[]? Body { get; set; }

    public byte[] BuildResponseBytes()
    {
        var headerBuilder = new StringBuilder();
        headerBuilder.Append($"HTTP/1.1 {StatusCode} {StatusDescription}\r\n");

        // 自动设置 Content-Length
        if (Body != null && !Headers.ContainsKey("Content-Length"))
        {
            Headers["Content-Length"] = Body.Length.ToString();
        }

        foreach (var header in Headers)
        {
            headerBuilder.Append($"{header.Key}: {header.Value}\r\n");
        }
        headerBuilder.Append("\r\n");

        var headerBytes = Encoding.ASCII.GetBytes(headerBuilder.ToString());
        var responseBytes = new byte[headerBytes.Length + (Body?.Length ?? 0)];
        Buffer.BlockCopy(headerBytes, 0, responseBytes, 0, headerBytes.Length);
        if (Body != null)
        {
            Buffer.BlockCopy(Body, 0, responseBytes, headerBytes.Length, Body.Length);
        }
        return responseBytes;
    }
}
