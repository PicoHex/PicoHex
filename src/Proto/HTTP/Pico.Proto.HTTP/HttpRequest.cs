namespace Pico.Proto.HTTP;

public class HttpRequest : HttpMessage
{
    public string Method { get; set; } = "GET";
    public string Url { get; set; } = "/";

    // 添加流式写入方法
    public void WriteBody(Stream sourceStream)
    {
        BodyStream = new MemoryStream();
        sourceStream.CopyTo(BodyStream);
        BodyStream.Position = 0; // 重置读取位置
    }
}
