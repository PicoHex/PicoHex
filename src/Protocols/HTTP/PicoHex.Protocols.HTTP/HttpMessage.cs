namespace PicoHex.Protocols.HTTP;

public abstract class HttpMessage : IDisposable, IAsyncDisposable
{
    private bool _disposed;
    public string ProtocolVersion { get; set; } = "HTTP/1.1";
    public Dictionary<string, string> Headers { get; } = new();
    private Stream? _bodyStream;

    public Stream? BodyStream
    {
        get => _bodyStream;
        set
        {
            _bodyStream?.Dispose();
            _bodyStream = value;
        }
    }

    public byte[]? GetBodyBytes()
    {
        if (_bodyStream is null)
            return null;
        if (_bodyStream.CanSeek)
            _bodyStream.Position = 0;
        using var ms = new MemoryStream();
        _bodyStream.CopyTo(ms);
        return ms.ToArray();
    }

    public virtual void Dispose()
    {
        if (_disposed)
            return;
        _disposed = true;
        _bodyStream?.Dispose();
        GC.SuppressFinalize(this);
    }

    public async ValueTask DisposeAsync()
    {
        if (_disposed)
            return;
        _disposed = true;
        if (_bodyStream != null)
            await _bodyStream.DisposeAsync();
        GC.SuppressFinalize(this);
    }
}
