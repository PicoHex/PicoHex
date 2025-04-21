using System.Buffers;
using System.IO.Pipelines;
using System.Text;

namespace PicoHex.Transport.HTTP;

public class HttpOverTcpAdapter : IDisposable
{
    private const int DefaultMaxRequestBodySize = 10 * 1024 * 1024; // 10MB
    private readonly ITcpTransport _tcpTransport;
    private readonly ILogger<HttpOverTcpAdapter>? _logger;
    private readonly Dictionary<string, Func<HttpContext, Task>> _routeHandlers = new();

    public HttpOverTcpAdapter(
        ITcpTransport tcpTransport,
        ILogger<HttpOverTcpAdapter>? logger = null
    )
    {
        _tcpTransport = tcpTransport;
        _logger = logger;
        _tcpTransport.OnNewConnection += HandleNewConnection;
    }

    public void MapGet(string path, Func<HttpContext, Task> handler) =>
        _routeHandlers[$"GET{path}"] = handler;

    public void MapPost(string path, Func<HttpContext, Task> handler) =>
        _routeHandlers[$"POST{path}"] = handler;

    private async void HandleNewConnection(IConnection connection)
    {
        try
        {
            using (connection)
            {
                var reader = PipeReader.Create(connection.ReceiveAsync());
                var writer = PipeWriter.Create(connection);

                while (true)
                {
                    var context = await ParseRequestAsync(reader);
                    if (context == null)
                        break;

                    await ProcessRequestAsync(context, writer);

                    if (!context.KeepAlive)
                        break;
                }
            }
        }
        catch (Exception ex)
        {
            _logger?.LogError(ex, "Connection handling failed");
        }
    }

    private async Task<HttpContext?> ParseRequestAsync(PipeReader reader)
    {
        var context = new HttpContext();

        // 解析请求行
        var line = await ReadLineAsync(reader);
        if (line == null)
            return null;

        var requestLine = line.Split(' ');
        if (requestLine.Length != 3)
            throw new HttpParseException("Invalid request line");

        context.Method = requestLine[0];
        context.Path = requestLine[1];
        context.Version = requestLine[2];

        // 解析请求头
        while (true)
        {
            line = await ReadLineAsync(reader);
            if (string.IsNullOrEmpty(line))
                break;

            var header = line.Split(new[] { ':' }, 2);
            if (header.Length != 2)
                continue;

            context.Headers[header[0].Trim()] = header[1].Trim();
        }

        // 解析请求体
        if (
            context.Headers.TryGetValue("Content-Length", out var contentLengthStr)
            && int.TryParse(contentLengthStr, out var contentLength)
        )
        {
            context.Body = await ReadBodyAsync(reader, contentLength);
        }
        else if (
            context.Headers.ContainsKey("Transfer-Encoding")
            && context.Headers["Transfer-Encoding"] == "chunked"
        )
        {
            context.Body = await ReadChunkedBodyAsync(reader);
        }

        context.KeepAlive =
            context.Version == "HTTP/1.1"
            && (
                !context.Headers.TryGetValue("Connection", out var conn)
                || conn.Equals("keep-alive", StringComparison.OrdinalIgnoreCase)
            );

        return context;
    }

    private static async Task<byte[]?> ReadBodyAsync(PipeReader reader, int contentLength)
    {
        var bodyBuffer = new byte[contentLength];
        var bytesRead = 0;

        while (bytesRead < contentLength)
        {
            var result = await reader.ReadAsync();
            var buffer = result.Buffer;

            foreach (var segment in buffer)
            {
                var copyLength = Math.Min(segment.Length, contentLength - bytesRead);
                segment.Slice(0, copyLength).CopyTo(bodyBuffer.AsSpan(bytesRead));
                bytesRead += copyLength;

                reader.AdvanceTo(segment.GetPosition(copyLength));
                if (bytesRead >= contentLength)
                    break;
            }
        }

        return bodyBuffer;
    }

    private async Task<byte[]?> ReadChunkedBodyAsync(PipeReader reader)
    {
        using var ms = new MemoryStream();

        while (true)
        {
            var line = await ReadLineAsync(reader);
            if (string.IsNullOrEmpty(line))
                break;

            var chunkSize = Convert.ToInt32(line.Trim(), 16);
            if (chunkSize == 0)
                break;

            var chunkData = await ReadBodyAsync(reader, chunkSize);
            ms.Write(chunkData);

            // 跳过 chunk 后的 CRLF
            await ReadLineAsync(reader);
        }

        return ms.ToArray();
    }

    private static async Task<string?> ReadLineAsync(PipeReader reader)
    {
        var lineBuffer = new ArrayBufferWriter<byte>();

        while (true)
        {
            var result = await reader.ReadAsync();
            var buffer = result.Buffer;

            var position = buffer.PositionOf((byte)'\n');
            if (position == null)
                continue;

            var line = buffer.Slice(0, position.Value);
            foreach (var segment in line)
            {
                lineBuffer.Write(segment.Span);
            }

            reader.AdvanceTo(buffer.GetPosition(1, position.Value));
            return Encoding.UTF8.GetString(lineBuffer.WrittenSpan.TrimEnd('\r'));
        }
    }

    private async Task ProcessRequestAsync(HttpContext context, PipeWriter writer)
    {
        try
        {
            var routeKey = $"{context.Method}{context.Path}";
            if (_routeHandlers.TryGetValue(routeKey, out var handler))
            {
                await handler(context);
            }
            else
            {
                context.Response.StatusCode = 404;
                context.Response.Body = Encoding.UTF8.GetBytes("Not Found");
            }
        }
        catch (Exception ex)
        {
            context.Response.StatusCode = 500;
            context.Response.Body = Encoding.UTF8.GetBytes("Internal Server Error");
            _logger?.LogError(ex, "Request processing failed");
        }

        await WriteResponseAsync(context, writer);
    }

    private async Task WriteResponseAsync(HttpContext context, PipeWriter writer)
    {
        var response = context.Response;
        var headers = new StringBuilder()
            .Append(
                $"HTTP/1.1 {response.StatusCode} {GetStatusCodePhrase(response.StatusCode)}\r\n"
            )
            .Append($"Content-Length: {response.Body?.Length ?? 0}\r\n");

        if (context.KeepAlive)
            headers.Append("Connection: keep-alive\r\n");

        foreach (var header in response.Headers)
            headers.Append($"{header.Key}: {header.Value}\r\n");

        headers.Append("\r\n");

        var headerBytes = Encoding.UTF8.GetBytes(headers.ToString());
        await writer.WriteAsync(headerBytes);

        if (response.Body != null)
            await writer.WriteAsync(response.Body);
    }

    private static string GetStatusCodePhrase(int statusCode) =>
        statusCode switch
        {
            200 => "OK",
            404 => "Not Found",
            500 => "Internal Server Error",
            _ => "Unknown"
        };

    public void Dispose() => _tcpTransport.Dispose();

    // HTTP 上下文模型
    public class HttpContext
    {
        public string Method { get; set; } = "";
        public string Path { get; set; } = "";
        public string Version { get; set; } = "";
        public Dictionary<string, string> Headers { get; } = new();
        public byte[]? Body { get; set; }
        public bool KeepAlive { get; set; }

        public HttpResponse Response { get; } = new();
    }

    public class HttpResponse
    {
        public int StatusCode { get; set; } = 200;
        public Dictionary<string, string> Headers { get; } = new();
        public byte[]? Body { get; set; }
    }

    public class HttpParseException : Exception
    {
        public HttpParseException(string message)
            : base(message) { }
    }
}
