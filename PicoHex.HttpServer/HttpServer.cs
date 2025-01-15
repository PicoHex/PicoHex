namespace PicoHex.HttpServer;

public class HttpServer(string ip, int port)
{
    private readonly TcpListener _listener = new(IPAddress.Parse(ip), port);

    public async Task StartAsync(CancellationToken cancellationToken)
    {
        _listener.Start();
        while (!cancellationToken.IsCancellationRequested)
        {
            var client = await _listener.AcceptTcpClientAsync(cancellationToken);
            _ = ProcessRequestAsync(client);
        }
        _listener.Stop();
    }

    private async Task ProcessRequestAsync(TcpClient client)
    {
        var stream = client.GetStream();
        try
        {
            var request = await ReadRequestAsync(stream);
            var response = ProcessRequest(request);
            await WriteResponseAsync(stream, response);
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Error processing request: {ex.Message}");
        }
        finally
        {
            client.Close();
        }
    }

    private async Task<HttpRequest> ReadRequestAsync(NetworkStream stream)
    {
        var request = new HttpRequest
        {
            Headers = new Dictionary<string, string>(),
            Body = []
        };

        using (var reader = new StreamReader(stream, Encoding.UTF8, leaveOpen: true))
        {
            var requestLine = await reader.ReadLineAsync();
            if (string.IsNullOrEmpty(requestLine))
                throw new InvalidOperationException("Invalid request line.");

            var parts = requestLine.Split(' ', StringSplitOptions.RemoveEmptyEntries);
            if (parts.Length != 3)
                throw new InvalidOperationException("Invalid request line format.");

            request.Method = parts[0];
            request.Path = parts[1];
            request.ProtocolVersion = parts[2];

            while (await reader.ReadLineAsync() is { } line && line != "")
            {
                var colonIndex = line.IndexOf(':');
                if (colonIndex == -1)
                    throw new InvalidOperationException("Invalid header format.");

                var headerName = line.Substring(0, colonIndex).Trim();
                var headerValue = line.Substring(colonIndex + 1).Trim();
                request.Headers[headerName] = headerValue;
            }

            if (request.Headers.TryGetValue("Content-Length", out var contentLengthString))
            {
                if (int.TryParse(contentLengthString, out var contentLength))
                {
                    var bodyBuffer = new byte[contentLength];
                    var bytesRead = await reader.BaseStream.ReadAsync(bodyBuffer.AsMemory(0, contentLength));
                    if (bytesRead != contentLength)
                        throw new InvalidOperationException("Incomplete request body.");

                    request.Body = bodyBuffer;
                }
                else
                    throw new InvalidOperationException("Invalid Content-Length.");
            }
        }

        return request;
    }

    private HttpResponse ProcessRequest(HttpRequest request)
    {
        if (request.Method == "GET")
        {
            var body = "<html><body>Hello from custom HttpListener!</body></html>";
            var bodyBytes = Encoding.UTF8.GetBytes(body);
            return new HttpResponse
            {
                StatusCode = 200,
                StatusDescription = "OK",
                Headers =
                {
                    { "Content-Type", "text/html" },
                    { "Content-Length", bodyBytes.Length.ToString() }
                },
                Body = bodyBytes
            };
        }
        else
        {
            var errorBody = "Method Not Allowed";
            var errorBodyBytes = Encoding.UTF8.GetBytes(errorBody);
            return new HttpResponse
            {
                StatusCode = 405,
                StatusDescription = "Method Not Allowed",
                Headers =
                {
                    { "Content-Type", "text/plain" },
                    { "Content-Length", errorBodyBytes.Length.ToString() }
                },
                Body = errorBodyBytes
            };
        }
    }

    private async Task WriteResponseAsync(NetworkStream stream, HttpResponse response)
    {
        var responseBuilder = new StringBuilder();
        responseBuilder.AppendLine($"{response.ProtocolVersion} {response.StatusCode} {response.StatusDescription}");
        foreach (var header in response.Headers)
            responseBuilder.AppendLine($"{header.Key}: {header.Value}");
        responseBuilder.AppendLine();

        var headerString = responseBuilder.ToString();
        var headerBytes = Encoding.UTF8.GetBytes(headerString);
        await stream.WriteAsync(headerBytes);

        if (response.Body is { Length: > 0 })
            await stream.WriteAsync(response.Body.AsMemory(0, response.Body.Length));
    }
}