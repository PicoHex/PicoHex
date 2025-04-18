namespace PicoHex.Protocols.HTTP;

public static class HttpParser
{
    private const int BufferSize = 4096;
    private static readonly byte[] HeaderEndMarker = "\r\n\r\n"u8.ToArray();

    public static HttpRequest ParseRequest(Stream stream)
    {
        var request = new HttpRequest();
        var headerBuffer = new MemoryStream();
        var headerEndFound = false;

        // 流式读取直到找到头结束标记
        while (!headerEndFound)
        {
            var buffer = new byte[BufferSize];
            var bytesRead = stream.Read(buffer, 0, BufferSize);
            if (bytesRead == 0)
                break;

            headerBuffer.Write(buffer, 0, bytesRead);
            headerEndFound = FindHeaderEnd(headerBuffer);
        }

        // 处理头信息
        ProcessHeaders(headerBuffer, request);

        // 处理正文
        ProcessBody(stream, headerBuffer, request);
        return request;
    }

    private static bool FindHeaderEnd(MemoryStream buffer)
    {
        var data = buffer.GetBuffer();
        for (var i = 0; i <= buffer.Length - HeaderEndMarker.Length; i++)
        {
            if (!HeaderEndMarker.SequenceEqual(data.Skip(i).Take(HeaderEndMarker.Length)))
                continue;
            buffer.SetLength(i + HeaderEndMarker.Length); // 截断到头部结束
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

            // 读取请求行
            var requestLine = reader.ReadLine();
            if (!string.IsNullOrEmpty(requestLine))
            {
                var requestParts = requestLine.Split(new[] { ' ' }, 3);
                if (requestParts.Length >= 3)
                {
                    request.Method = requestParts[0];
                    request.Url = requestParts[1];
                    request.ProtocolVersion = requestParts[2];
                }
            }

            // 读取头字段
            string? headerLine;
            while (!string.IsNullOrEmpty(headerLine = reader.ReadLine()))
            {
                var colonIndex = headerLine.IndexOf(':');
                if (colonIndex > 0)
                {
                    var key = headerLine.Substring(0, colonIndex).Trim();
                    var value = headerLine.Substring(colonIndex + 1).Trim();
                    request.Headers[key] = value;
                }
            }
        }
        finally
        {
            headerBuffer.Position = 0; // 重置流位置
        }
    }

    private static void ProcessBody(
        Stream sourceStream,
        MemoryStream headerBuffer,
        HttpRequest request
    )
    {
        // 已读取的正文部分
        var bodyStart = headerBuffer.Length;
        var initialBodyLength = headerBuffer.Capacity - (int)bodyStart; // 安全转换

        if (initialBodyLength > 0)
        {
            request.BodyStream = new MemoryStream(
                headerBuffer.GetBuffer(),
                (int)bodyStart,
                initialBodyLength
            );
        }

        // 读取剩余正文（修正类型转换）
        if (!request.Headers.TryGetValue("Content-Length", out var contentLengthStr))
            return;

        var contentLength = long.Parse(contentLengthStr); // 使用long类型
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
            var readSize = (int)Math.Min(remaining, BufferSize); // 安全转换为int
            var bytesRead = sourceStream.Read(buffer, 0, readSize);
            if (bytesRead == 0)
                break;

            bodyStream.Write(buffer, 0, bytesRead);
            remaining -= bytesRead;
        }

        bodyStream.Position = 0;
        request.BodyStream = bodyStream;
    }
}
