namespace Pico.Proto.HTTP;

public class HttpResponse : HttpMessage
{
    public int StatusCode { get; set; } = 200;
    public string StatusDescription { get; set; } = "OK";

    public byte[] BuildResponseBytes()
    {
        using var headerStream = new MemoryStream();
        // 构建响应头
        var statusLine = $"HTTP/1.1 {StatusCode} {StatusDescription}\r\n";
        headerStream.Write(Encoding.ASCII.GetBytes(statusLine));

        // 自动计算 Content-Length
        if (BodyStream != null && !Headers.ContainsKey("Content-Length"))
        {
            if (BodyStream.CanSeek)
            {
                Headers["Content-Length"] = BodyStream.Length.ToString();
            }
            else
            {
                throw new InvalidOperationException(
                    "Cannot determine Content-Length for non-seekable streams"
                );
            }
        }

        // 写入头信息
        foreach (var line in Headers.Select(header => $"{header.Key}: {header.Value}\r\n"))
        {
            headerStream.Write(Encoding.ASCII.GetBytes(line));
        }
        headerStream.Write("\r\n"u8);

        // 合并头与正文
        if (BodyStream == null)
            return headerStream.ToArray();

        if (!BodyStream.CanRead)
            throw new InvalidOperationException("Body stream is not readable");

        // 重置流位置以确保完整读取
        if (BodyStream.CanSeek)
            BodyStream.Position = 0;

        using var responseStream = new MemoryStream();
        headerStream.WriteTo(responseStream);
        BodyStream.CopyTo(responseStream);

        return responseStream.ToArray();
    }
}
