namespace PicoHex.Protocols.HTTP;

public class HttpRequest : HttpMessage
{
    public string Method { get; set; }  // GET, POST 等
    public string Uri { get; set; }     // 请求路径（如 "/index.html"）

    public override void Parse(byte[] rawData)
    {
        string text = Encoding.UTF8.GetString(rawData);
        string[] parts = text.Split(new[] { "\r\n\r\n" }, 2, StringSplitOptions.None);
        string[] lines = parts[0].Split(new[] { "\r\n" }, StringSplitOptions.None);

        // 解析请求行（如 "GET / HTTP/1.1"）
        string[] requestLine = lines[0].Split(' ');
        Method = requestLine[0];
        Uri = requestLine[1];
        Version = requestLine[2];

        // 解析头部
        for (int i = 1; i < lines.Length; i++)
        {
            string[] header = lines[i].Split(new[] { ": " }, 2, StringSplitOptions.None);
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
        byte[] headerBytes = Encoding.UTF8.GetBytes(sb.ToString());
        
        if (Body != null)
            return CombineBytes(headerBytes, Body);
        else
            return headerBytes;
    }

    internal static byte[] CombineBytes(byte[] a, byte[] b)
    {
        byte[] combined = new byte[a.Length + b.Length];
        Buffer.BlockCopy(a, 0, combined, 0, a.Length);
        Buffer.BlockCopy(b, 0, combined, a.Length, b.Length);
        return combined;
    }
}
