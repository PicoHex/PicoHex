namespace PicoHex.Protocols.HTTP;

public abstract class HttpMessage : IDisposable
{
    public string ProtocolVersion { get; set; } = "HTTP/1.1";
    public Dictionary<string, string> Headers { get; } = new();
    private Stream? _bodyStream;

    public Stream? BodyStream
    {
        get => _bodyStream;
        set
        {
            _bodyStream?.Dispose(); // 释放旧流
            _bodyStream = value;
        }
    }

    // 快捷访问方法（可选）
    public byte[]? GetBodyBytes()
    {
        if (_bodyStream == null)
            return null;
        if (_bodyStream.CanSeek)
            _bodyStream.Position = 0;
        using var ms = new MemoryStream();
        _bodyStream.CopyTo(ms);
        return ms.ToArray();
    }

    public virtual void Dispose()
    {
        _bodyStream?.Dispose();
        GC.SuppressFinalize(this);
    }
}
