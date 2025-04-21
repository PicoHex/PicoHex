namespace PicoHex.Transport;

public interface IHttpProtocol : IApplicationProtocol, IRequestResponder
{
    HttpVersion MaxSupportedVersion { get; }
    Task EnableFeatureAsync(HttpFeature feature);
}

public enum HttpVersion
{
    Http1,
    Http2,
    Http3
}

public enum HttpFeature
{
    Compression,
    ServerPush
}
