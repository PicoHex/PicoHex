using System.Net;
using System.Net.Sockets;
using System.Text;

namespace PicoHex.HttpServer;

public class HttpServer
{
    private TcpListener _listener;

    public HttpServer(string ip, int port)
    {
        _listener = new TcpListener(IPAddress.Parse(ip), port);
    }

    public async Task StartAsync(CancellationToken cancellationToken)
    {
        _listener.Start();
        while (!cancellationToken.IsCancellationRequested)
        {
            TcpClient client = await _listener.AcceptTcpClientAsync();
            _ = ProcessRequestAsync(client);
        }
        _listener.Stop();
    }

    private async Task ProcessRequestAsync(TcpClient client)
    {
        NetworkStream stream = client.GetStream();
        try
        {
            HttpRequest request = await ReadRequestAsync(stream);
            HttpResponse response = ProcessRequest(request);
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
        HttpRequest request = new HttpRequest
        {
            Headers = new Dictionary<string, string>(),
            Body = new byte[0]
        };

        using (StreamReader reader = new StreamReader(stream, Encoding.UTF8, leaveOpen: true))
        {
            string requestLine = await reader.ReadLineAsync();
            if (string.IsNullOrEmpty(requestLine))
                throw new InvalidOperationException("Invalid request line.");

            string[] parts = requestLine.Split(' ', StringSplitOptions.RemoveEmptyEntries);
            if (parts.Length != 3)
                throw new InvalidOperationException("Invalid request line format.");

            request.Method = parts[0];
            request.Path = parts[1];
            request.ProtocolVersion = parts[2];

            string line;
            while ((line = await reader.ReadLineAsync()) != null && line != "")
            {
                int colonIndex = line.IndexOf(':');
                if (colonIndex == -1)
                    throw new InvalidOperationException("Invalid header format.");

                string headerName = line.Substring(0, colonIndex).Trim();
                string headerValue = line.Substring(colonIndex + 1).Trim();
                request.Headers[headerName] = headerValue;
            }

            if (request.Headers.TryGetValue("Content-Length", out string contentLengthString))
            {
                if (int.TryParse(contentLengthString, out int contentLength))
                {
                    byte[] bodyBuffer = new byte[contentLength];
                    int bytesRead = await reader.BaseStream.ReadAsync(bodyBuffer, 0, contentLength);
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
            string body = "<html><body>Hello from custom HttpListener!</body></html>";
            byte[] bodyBytes = Encoding.UTF8.GetBytes(body);
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
            string errorBody = "Method Not Allowed";
            byte[] errorBodyBytes = Encoding.UTF8.GetBytes(errorBody);
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
        StringBuilder responseBuilder = new StringBuilder();
        responseBuilder.AppendLine($"{response.ProtocolVersion} {response.StatusCode} {response.StatusDescription}");
        foreach (var header in response.Headers)
            responseBuilder.AppendLine($"{header.Key}: {header.Value}");
        responseBuilder.AppendLine();

        string headerString = responseBuilder.ToString();
        byte[] headerBytes = Encoding.UTF8.GetBytes(headerString);
        await stream.WriteAsync(headerBytes, 0, headerBytes.Length);

        if (response.Body != null && response.Body.Length > 0)
            await stream.WriteAsync(response.Body, 0, response.Body.Length);
    }
}