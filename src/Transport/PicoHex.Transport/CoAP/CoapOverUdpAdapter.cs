namespace PicoHex.Transport.CoAP;

public class CoapOverUdpAdapter : IDisposable
{
    private const int CoapDefaultPort = 5683;
    private readonly IUdpTransport _udpTransport;
    private readonly ILogger<CoapOverUdpAdapter>? _logger;
    private readonly Dictionary<MessageKey, PendingRequest> _pendingRequests = new();
    private readonly Timer _retransmissionTimer;
    private ushort _messageIdCounter;

    // CoAP 协议常量
    private enum CoapMessageType : byte { CON = 0, NON = 1, ACK = 2, RST = 3 }
    private enum CoapCode : byte
    {
        GET = 0x01, POST = 0x02, PUT = 0x03, DELETE = 0x04,
        Created = 0x41, Deleted = 0x42, Valid = 0x43, 
        BadRequest = 0x80, NotFound = 0x84, InternalError = 0xA0
    }

    public CoapOverUdpAdapter(IUdpTransport udpTransport, ILogger<CoapOverUdpAdapter>? logger = null)
    {
        _udpTransport = udpTransport;
        _logger = logger;
        _retransmissionTimer = new Timer(RetransmitExpiredRequests, null, 1000, 1000);
        _ = StartReceiver();
    }

    public event Action<CoapRequest>? OnRequestReceived;
    public event Action<CoapResponse>? OnResponseReceived;

    public async Task<CoapResponse> SendRequestAsync(CoapRequest request, CancellationToken ct = default)
    {
        var messageId = GenerateMessageId();
        var token = GenerateToken();
        var tcs = new TaskCompletionSource<CoapResponse>();
        
        var pendingRequest = new PendingRequest(
            Expires: DateTime.UtcNow.AddSeconds(5),
            RetryCount: 0,
            Tcs: tcs,
            Request: request
        );

        lock (_pendingRequests)
        {
            _pendingRequests[new MessageKey(messageId, token)] = pendingRequest;
        }

        var coapMessage = BuildCoapMessage(request, messageId, token);
        await _udpTransport.SendAsync(request.Destination, coapMessage, ct);

        return await tcs.Task;
    }

    private async Task StartReceiver()
    {
        await foreach (var (from, data) in _udpTransport.ReceiveAsync())
        {
            ProcessIncomingMessage(from, data);
        }
    }

    private void ProcessIncomingMessage(IPEndPoint from, byte[] data)
    {
        try
        {
            var (header, token, options, payload) = ParseCoapMessage(data);
            
            if (header.IsResponse)
            {
                HandleResponse(from, header, token, payload);
            }
            else
            {
                HandleRequest(from, header, token, options, payload);
            }
        }
        catch (Exception ex)
        {
            _logger?.LogError(ex, "Failed to process CoAP message");
        }
    }

    private void HandleRequest(IPEndPoint from, CoapHeader header, byte[] token, 
        List<CoapOption> options, byte[] payload)
    {
        var request = new CoapRequest(
            Code: header.Code,
            Uri: BuildUriFromOptions(options),
            Payload: payload,
            Destination: from,
            Token: token,
            MessageId: header.MessageId
        );

        // 发送 ACK（如果是 CON 消息）
        if (header.Type == CoapMessageType.CON)
        {
            var ackMessage = BuildAckMessage(header.MessageId);
            _udpTransport.SendAsync(from, ackMessage);
        }

        OnRequestReceived?.Invoke(request);
    }

    private void HandleResponse(IPEndPoint from, CoapHeader header, byte[] token, byte[] payload)
    {
        var key = new MessageKey(header.MessageId, token);
        lock (_pendingRequests)
        {
            if (_pendingRequests.TryGetValue(key, out var pending))
            {
                pending.Tcs.TrySetResult(new CoapResponse(
                    Code: header.Code,
                    Payload: payload,
                    Status: header.Type == CoapMessageType.ACK 
                        ? CoapResponseStatus.Acknowledged 
                        : CoapResponseStatus.NonConfirmable
                ));
                _pendingRequests.Remove(key);
            }
        }
    }

    private byte[] BuildCoapMessage(CoapRequest request, ushort messageId, byte[] token)
    {
        using var buffer = new MemoryStream();
        // 构建 CoAP 头
        var header = new CoapHeader(
            Ver: 1,
            Type: request.Qos == CoapQos.Confirmable ? CoapMessageType.CON : CoapMessageType.NON,
            Code: request.Code,
            MessageId: messageId
        );
        buffer.WriteByte((byte)((header.Ver << 6) | ((byte)header.Type << 4) | (token.Length & 0x0F));
        buffer.WriteByte((byte)header.Code);
        buffer.Write(BitConverter.GetBytes(header.MessageId).Reverse().ToArray()); // Big-endian
        buffer.Write(token, 0, token.Length);
        
        // 构建选项（例如 Uri-Path）
        BuildOptions(buffer, request.Uri);
        
        // 添加 payload 标记和内容
        if (request.Payload.Length > 0)
        {
            buffer.WriteByte(0xFF);
            buffer.Write(request.Payload);
        }
        
        return buffer.ToArray();
    }

    private void BuildOptions(MemoryStream buffer, Uri uri)
    {
        var options = new List<CoapOption>
        {
            new CoapOption(OptionNumber.UriPath, uri.AbsolutePath.Split('/')
                .Where(p => !string.IsNullOrEmpty(p))
                .Select(Encoding.UTF8.GetBytes))
        };

        if (uri.Query is { Length: > 1 } query)
        {
            options.Add(new CoapOption(OptionNumber.UriQuery, 
                query.TrimStart('?').Split('&')
                    .Select(Encoding.UTF8.GetBytes)));
        }

        CoapOption prevOption = null;
        foreach (var opt in options.OrderBy(o => o.Number))
        {
            var delta = prevOption == null 
                ? (int)opt.Number 
                : (int)opt.Number - (int)prevOption.Number;
            
            // 写入选项头（delta 和长度）
            // 这里简化处理，实际需要处理多字节扩展
            buffer.WriteByte((byte)((delta << 4) | opt.Value.Length));
            
            foreach (var segment in opt.Values)
            {
                buffer.Write(segment);
            }
            prevOption = opt;
        }
    }

    private void RetransmitExpiredRequests(object? state)
    {
        lock (_pendingRequests)
        {
            var now = DateTime.UtcNow;
            foreach (var kvp in _pendingRequests.ToList())
            {
                if (now > kvp.Value.Expires)
                {
                    if (kvp.Value.RetryCount >= 3)
                    {
                        kvp.Value.Tcs.TrySetException(new TimeoutException("Max retries exceeded"));
                        _pendingRequests.Remove(kvp.Key);
                    }
                    else
                    {
                        // 重传消息
                        _udpTransport.SendAsync(kvp.Value.Request.Destination, 
                            BuildCoapMessage(kvp.Value.Request, kvp.Key.MessageId, kvp.Key.Token));
                        
                        _pendingRequests[kvp.Key] = kvp.Value with 
                        { 
                            RetryCount = kvp.Value.RetryCount + 1,
                            Expires = now.AddSeconds(5 * (kvp.Value.RetryCount + 1))
                        };
                    }
                }
            }
        }
    }

    // 辅助类型定义
    private record struct MessageKey(ushort MessageId, byte[] Token);
    private record PendingRequest(
        DateTime Expires, 
        int RetryCount, 
        TaskCompletionSource<CoapResponse> Tcs,
        CoapRequest Request
    );

    public enum CoapQos { Confirmable, NonConfirmable }
    public record CoapRequest(
        CoapCode Code,
        Uri Uri,
        byte[] Payload,
        IPEndPoint Destination,
        CoapQos Qos = CoapQos.Confirmable,
        byte[]? Token = null,
        ushort MessageId = 0
    );

    public record CoapResponse(
        CoapCode Code,
        byte[] Payload,
        CoapResponseStatus Status
    );

    public enum CoapResponseStatus { Acknowledged, NonConfirmable }
    private enum OptionNumber : int { UriPath = 11, UriQuery = 15 }
    private record CoapOption(OptionNumber Number, IEnumerable<byte[]> Values);
    
    public void Dispose()
    {
        _retransmissionTimer.Dispose();
        _udpTransport.Dispose();
    }

    // 省略部分辅助方法（如 ParseCoapMessage、GenerateToken 等）
}