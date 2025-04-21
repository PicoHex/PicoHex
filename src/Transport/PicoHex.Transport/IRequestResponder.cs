namespace PicoHex.Transport;

public interface IRequestResponder
{
    // 端点路由管理
    Task RegisterRouteAsync(string routePattern);
    Task UnregisterRouteAsync(string routePattern);

    // 请求响应
    Task<byte[]> SendRequestAsync(string route, byte[] request, CancellationToken ct = default);
    Task RegisterHandlerAsync(string routePattern, Func<byte[], Task<byte[]>> handler);
}
