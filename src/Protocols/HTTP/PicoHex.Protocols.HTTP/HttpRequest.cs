namespace PicoHex.Protocols.HTTP;

public class HttpRequest : HttpMessage
{
    public string Method { get; set; } = string.Empty; // GET, POST 等
    public string Uri { get; set; } = string.Empty; // 请求路径（如 "/index.html"）

    public override void Parse(byte[] rawData)
    {
        var text = Encoding.UTF8.GetString(rawData);
        var parts = text.Split(["\r\n\r\n"], 2, StringSplitOptions.None);
        var lines = parts[0].Split(["\r\n"], StringSplitOptions.None);

        // 解析请求行（如 "GET / HTTP/1.1"）
        var requestLine = lines[0].Split(' ');
        Method = requestLine[0];
        Uri = requestLine[1];
        Version = requestLine[2];

        // 解析头部
        for (var i = 1; i < lines.Length; i++)
        {
            var header = lines[i].Split([": "], 2, StringSplitOptions.None);
            if (header.Length == 2)
                Headers[header[0]] = header[1];
        }

        // 解析正文
        if (parts.Length > 1)
            Body = Encoding.UTF8.GetBytes(parts[1]);
    }

    public override byte[] Serialize()
    {
        var sb = new StringBuilder();
        sb.AppendLine($"{Method} {Uri} {Version}");

        foreach (var header in Headers)
            sb.AppendLine($"{header.Key}: {header.Value}");

        sb.AppendLine(); // 空行分隔头部和正文
        var headerBytes = Encoding.UTF8.GetBytes(sb.ToString());

        return Body != null ? CombineBytes(headerBytes, Body) : headerBytes;
    }

    internal static byte[] CombineBytes(byte[] a, byte[] b)
    {
        var combined = new byte[a.Length + b.Length];
        Buffer.BlockCopy(a, 0, combined, 0, a.Length);
        Buffer.BlockCopy(b, 0, combined, a.Length, b.Length);
        return combined;
    }
}
