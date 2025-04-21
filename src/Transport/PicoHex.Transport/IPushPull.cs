namespace PicoHex.Transport;

public interface IPushPull
{
    // 端点绑定
    Task BindEndpointAsync(EndpointType endpointType, Uri address);

    // 数据操作
    Task PushAsync(byte[] message, CancellationToken ct = default);
    Task<byte[]> PullAsync(CancellationToken ct = default);
}

public enum EndpointType
{
    Push,
    Pull
}
