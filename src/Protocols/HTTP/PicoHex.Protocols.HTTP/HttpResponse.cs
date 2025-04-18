namespace PicoHex.Protocols.HTTP;

public class HttpResponse : HttpMessage
{
    public int StatusCode { get; set; } // 如 200
    public string StatusText { get; set; } = string.Empty; // 如 "OK"

    public override void Parse(byte[] rawData)
    {
        var text = Encoding.UTF8.GetString(rawData);
        var parts = text.Split(["\r\n\r\n"], 2, StringSplitOptions.None);
        var lines = parts[0].Split(["\r\n"], StringSplitOptions.None);

        // 解析状态行（如 "HTTP/1.1 200 OK"）
        var statusLine = lines[0].Split([' '], 3);
        Version = statusLine[0];
        StatusCode = int.Parse(statusLine[1]);
        StatusText = statusLine[2];

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
        sb.AppendLine($"{Version} {StatusCode} {StatusText}");

        foreach (var header in Headers)
            sb.AppendLine($"{header.Key}: {header.Value}");

        sb.AppendLine();
        var headerBytes = Encoding.UTF8.GetBytes(sb.ToString());

        return Body != null ? CombineBytes(headerBytes, Body) : headerBytes;
    }

    private static byte[] CombineBytes(byte[] a, byte[] b) => HttpRequest.CombineBytes(a, b); // 复用请求类的合并方法
}
