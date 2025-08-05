namespace Pico.Proto.HTTP;

public static class HttpParser
{
    private const int BufferSize = 4096;
    private static readonly byte[] HeaderEndMarker = Encoding.ASCII.GetBytes("\r\n\r\n");

    public static HttpRequest ParseRequest(Stream stream)
    {
        var request = new HttpRequest();
        var headerBuffer = new MemoryStream();
        var headerEndFound = false;

        while (!headerEndFound)
        {
            var buffer = new byte[BufferSize];
            var bytesRead = stream.Read(buffer, 0, BufferSize);
            if (bytesRead is 0)
                break;

            headerBuffer.Write(buffer, 0, bytesRead);
            headerEndFound = FindHeaderEnd(headerBuffer);
        }

        ProcessHeaders(headerBuffer, request);
        ProcessBody(stream, headerBuffer, request);

        return request;
    }

    public static HttpResponse ParseResponse(Stream stream)
    {
        var response = new HttpResponse();
        var headerBuffer = new MemoryStream();
        var headerEndFound = false;

        while (!headerEndFound)
        {
            var buffer = new byte[BufferSize];
            var bytesRead = stream.Read(buffer, 0, BufferSize);
            if (bytesRead is 0)
                break;

            headerBuffer.Write(buffer, 0, bytesRead);
            headerEndFound = FindHeaderEnd(headerBuffer);
        }

        ProcessResponseHeaders(headerBuffer, response);
        ProcessResponseBody(stream, headerBuffer, response);

        return response;
    }

    #region Private Methods

    private static bool FindHeaderEnd(MemoryStream buffer)
    {
        var data = buffer.GetBuffer();
        for (var i = 0; i <= buffer.Length - HeaderEndMarker.Length; i++)
        {
            if (!HeaderEndMarker.SequenceEqual(data.Skip(i).Take(HeaderEndMarker.Length)))
                continue;
            buffer.SetLength(i + HeaderEndMarker.Length);
            return true;
        }
        return false;
    }

    private static void ProcessHeaders(MemoryStream headerBuffer, HttpRequest request)
    {
        try
        {
            headerBuffer.Position = 0;
            using var reader = new StreamReader(
                headerBuffer,
                Encoding.ASCII,
                detectEncodingFromByteOrderMarks: false
            );

            var requestLine = reader.ReadLine();
            if (!string.IsNullOrEmpty(requestLine))
            {
                var requestParts = requestLine.Split([' '], 3);
                if (requestParts.Length >= 3)
                {
                    request.Method = requestParts[0];
                    request.Url = requestParts[1];
                    request.ProtocolVersion = requestParts[2];
                }
            }

            string? headerLine;
            while (!string.IsNullOrEmpty(headerLine = reader.ReadLine()))
            {
                var colonIndex = headerLine.IndexOf(':');
                if (colonIndex <= 0)
                    continue;
                var key = headerLine[..colonIndex].Trim();
                var value = headerLine[(colonIndex + 1)..].Trim();
                request.Headers[key] = value;
            }
        }
        finally
        {
            headerBuffer.Position = 0;
        }
    }

    private static void ProcessResponseHeaders(MemoryStream headerBuffer, HttpResponse response)
    {
        headerBuffer.Position = 0;
        using var reader = new StreamReader(headerBuffer, Encoding.ASCII);

        // 解析状态行
        var statusLine = reader.ReadLine();
        if (!string.IsNullOrEmpty(statusLine))
        {
            var parts = statusLine.Split([' '], 3);
            if (parts.Length >= 2)
            {
                response.ProtocolVersion = parts[0];
                response.StatusCode = int.Parse(parts[1]);
                response.StatusDescription = parts.Length > 2 ? parts[2] : string.Empty;
            }
        }

        // 解析头部字段
        string? headerLine;
        while (!string.IsNullOrEmpty(headerLine = reader.ReadLine()))
        {
            var colonIndex = headerLine.IndexOf(':');
            if (colonIndex <= 0)
                continue;
            var key = headerLine[..colonIndex].Trim();
            var value = headerLine[(colonIndex + 1)..].Trim();
            response.Headers[key] = value;
        }
    }

    private static void ProcessResponseBody(
        Stream sourceStream,
        MemoryStream headerBuffer,
        HttpResponse response
    )
    {
        // 实现与请求正文类似的流处理逻辑
    }

    private static void ProcessBody(
        Stream sourceStream,
        MemoryStream headerBuffer,
        HttpRequest request
    )
    {
        var bodyStart = headerBuffer.Length;
        var initialBodyLength = headerBuffer.Capacity - (int)bodyStart;

        if (initialBodyLength > 0)
        {
            request.BodyStream = new MemoryStream(
                headerBuffer.GetBuffer(),
                (int)bodyStart,
                initialBodyLength
            );
        }

        if (!request.Headers.TryGetValue("Content-Length", out var contentLengthStr))
            return;

        var contentLength = long.Parse(contentLengthStr);
        var remaining = contentLength - (headerBuffer.Length - bodyStart);
        ReadRemainingBody(sourceStream, request, remaining);
    }

    private static void ReadRemainingBody(Stream sourceStream, HttpRequest request, long remaining)
    {
        if (remaining <= 0)
            return;

        var bodyStream = request.BodyStream ?? new MemoryStream();
        var buffer = new byte[BufferSize];

        while (remaining > 0)
        {
            var readSize = (int)Math.Min(remaining, BufferSize);
            var bytesRead = sourceStream.Read(buffer, 0, readSize);
            if (bytesRead == 0)
                break;

            bodyStream.Write(buffer, 0, bytesRead);
            remaining -= bytesRead;
        }

        bodyStream.Position = 0;
        request.BodyStream = bodyStream;
    }

    #endregion
}
