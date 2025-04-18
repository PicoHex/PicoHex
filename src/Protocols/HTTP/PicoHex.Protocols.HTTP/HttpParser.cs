namespace PicoHex.Protocols.HTTP;

public static class HttpParser
{
    private const int BufferSize = 4096;
    private static readonly byte[] HeaderEndMarker = { 0x0D, 0x0A, 0x0D, 0x0A }; // \r\n\r\n

    public static HttpRequest ParseRequest(Stream stream)
    {
        var request = new HttpRequest();
        var buffer = new byte[BufferSize];
        var headerBytes = new List<byte>();
        var headerEndIndex = -1;

        // 查找头部结束标记
        while (headerEndIndex == -1)
        {
            int bytesRead = stream.Read(buffer, 0, BufferSize);
            if (bytesRead == 0)
                break;

            headerBytes.AddRange(buffer.AsSpan(0, bytesRead).ToArray());
            headerEndIndex = FindHeaderEndIndex(headerBytes.ToArray());
        }

        // 解析头部
        if (headerEndIndex == -1)
            return request;
        var headerText = Encoding.ASCII.GetString(headerBytes.ToArray(), 0, headerEndIndex);
        ParseRequestHeaders(request, headerText);

        // 处理正文
        var bodyStart = headerEndIndex + HeaderEndMarker.Length;
        var bodyLengthInBuffer = headerBytes.Count - bodyStart;

        if (bodyLengthInBuffer > 0)
        {
            request.Body = new byte[bodyLengthInBuffer];
            Array.Copy(headerBytes.ToArray(), bodyStart, request.Body, 0, bodyLengthInBuffer);
        }

        // 读取剩余正文
        if (!request.Headers.TryGetValue("Content-Length", out string contentLengthStr))
            return request;
        var contentLength = int.Parse(contentLengthStr);
        ReadRemainingBody(stream, request, contentLength - bodyLengthInBuffer);

        return request;
    }

    private static int FindHeaderEndIndex(byte[] data)
    {
        for (var i = 0; i <= data.Length - HeaderEndMarker.Length; i++)
        {
            var match = !HeaderEndMarker.Where((t, j) => data[i + j] != t).Any();
            if (match)
                return i;
        }
        return -1;
    }

    private static void ParseRequestHeaders(HttpRequest request, string headerText)
    {
        var lines = headerText.Split(["\r\n"], StringSplitOptions.RemoveEmptyEntries);
        if (lines.Length == 0)
            return;

        // 解析请求行
        var requestLine = lines[0].Split(' ');
        if (requestLine.Length >= 3)
        {
            request.Method = requestLine[0];
            request.Url = requestLine[1];
            request.ProtocolVersion = requestLine[2];
        }

        // 解析头部字段
        for (var i = 1; i < lines.Length; i++)
        {
            var colonIndex = lines[i].IndexOf(':');
            if (colonIndex <= 0)
                continue;
            var key = lines[i][..colonIndex].Trim();
            var value = lines[i][(colonIndex + 1)..].Trim();
            request.Headers[key] = value;
        }
    }

    private static void ReadRemainingBody(Stream stream, HttpRequest request, int remainingBytes)
    {
        if (remainingBytes <= 0)
            return;

        var body = new MemoryStream();
        if (request.Body != null)
        {
            body.Write(request.Body, 0, request.Body.Length);
        }

        var buffer = new byte[BufferSize];
        while (remainingBytes > 0)
        {
            var bytesToRead = Math.Min(remainingBytes, BufferSize);
            var bytesRead = stream.Read(buffer, 0, bytesToRead);
            if (bytesRead == 0)
                break;

            body.Write(buffer, 0, bytesRead);
            remainingBytes -= bytesRead;
        }

        request.Body = body.ToArray();
    }
}
