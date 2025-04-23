namespace PicoHex.Transport;

public interface IQuicProtocol : ITransportLayerProtocol
{
    // Stream management
    IReadOnlyDictionary<int, IQuicStream> ActiveStreams { get; }
    IQuicStream CreateStream(StreamType type);
    void CloseStream(int streamId, QuicErrorCode errorCode = QuicErrorCode.NoError);

    // Connection features
    bool IsZeroRTTHandshakeAvailable { get; }
    bool TryMigrateEndpoint(IPEndPoint newLocalEP);

    // Security configuration
    X509Certificate ServerCertificate { get; }
    CipherSuite NegotiatedCipherSuite { get; }
    bool RequireClientAuthentication { get; set; }

    IReadOnlyList<CipherSuite> SupportedCipherSuites { get; }
    void UpdateCipherSuitePreference(IList<CipherSuite> orderedSuites);

    void RequestKeyUpdate(KeyUpdateRequest request);

    // Protocol settings
    int MaxBidirectionalStreams { get; set; }
    int MaxUnidirectionalStreams { get; set; }
    TimeSpan IdleTimeout { get; set; }

    // Error handling
    event Action<QuicErrorCode, string> ConnectionError;
}

public interface IQuicStream : IDisposable
{
    int StreamId { get; }
    StreamType Type { get; }
    StreamState State { get; }

    // Flow control
    long AvailableReadBytes { get; }
    long AvailableWriteBytes { get; }

    // Data operations
    ValueTask WriteAsync(
        ReadOnlyMemory<byte> buffer,
        bool endStream = false,
        CancellationToken ct = default
    );
    ValueTask<ReadOnlyMemory<byte>> ReadAsync(int maxBytes = 4096, CancellationToken ct = default);
    void AbortWrite(QuicErrorCode errorCode);
    void AbortRead(QuicErrorCode errorCode);

    // State notifications
    event Action<StreamState> StateChanged;
}

public enum CipherSuite : ushort
{
    // TLS 1.3 标准套件
    TLS_AES_128_GCM_SHA256 = 0x1301,
    TLS_AES_256_GCM_SHA384 = 0x1302,
    TLS_CHACHA20_POLY1305_SHA256 = 0x1303,
    TLS_AES_128_CCM_SHA256 = 0x1304,
    TLS_AES_128_CCM_8_SHA256 = 0x1305,

    // 传统套件（兼容旧系统）
    TLS_ECDHE_ECDSA_WITH_AES_128_GCM_SHA256 = 0xC02B,
    TLS_ECDHE_RSA_WITH_AES_128_GCM_SHA256 = 0xC02F,
    TLS_ECDHE_ECDSA_WITH_AES_256_GCM_SHA384 = 0xC02C,
    TLS_ECDHE_RSA_WITH_AES_256_GCM_SHA384 = 0xC030,

    // 已废弃的套件
    TLS_RSA_WITH_AES_128_CBC_SHA = 0x002F,
    TLS_RSA_WITH_AES_256_CBC_SHA = 0x0035,

    // 特殊值
    UNKNOWN = 0x0000,
    NULL_CIPHER = 0x00FF
}

// 扩展方法用于获取标准名称
public static class CipherSuiteExtensions
{
    public static string GetStandardName(this CipherSuite suite)
    {
        return suite switch
        {
            CipherSuite.TLS_AES_128_GCM_SHA256 => "TLS_AES_128_GCM_SHA256",
            CipherSuite.TLS_AES_256_GCM_SHA384 => "TLS_AES_256_GCM_SHA384",
            CipherSuite.TLS_CHACHA20_POLY1305_SHA256 => "TLS_CHACHA20_POLY1305_SHA256",
            // 其他模式匹配...
            _ => "UNKNOWN_CIPHER_SUITE"
        };
    }

    public static bool IsModern(this CipherSuite suite)
    {
        return (ushort)suite >= 0x1300 && (ushort)suite <= 0x13FF;
    }
}

public enum KeyUpdateRequest
{
    NotNeeded, // 不需要更新
    UpdateRequested, // 请求密钥更新
    UpdateAndAcknowledge // 要求立即更新并确认
}

public enum QuicErrorCode
{
    NoError,
    ApplicationError,
    StreamLimitReached
}

public enum StreamState
{
    Idle,
    Open,
    Closed,
    Reset
}

public enum StreamType
{
    Unidirectional,
    Bidirectional
}
