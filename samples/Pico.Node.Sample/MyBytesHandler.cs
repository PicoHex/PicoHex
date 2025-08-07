using System.Buffers;
using System.Net;
using System.Text;

namespace Pico.Node.Handlers;

/// <summary>
/// 多功能UDP处理器实现
/// 1. 基础Echo功能
/// 2. 请求统计
/// 3. 自定义命令处理
/// 4. 大文件分片传输演示
/// </summary>
public class AdvancedUdpHandler : IUdpHandler, IDisposable
{
    private readonly ILogger? _logger;
    private long _totalRequests;
    private readonly Timer _statsTimer;
    private readonly ConcurrentDictionary<EndPoint, ClientSession> _sessions = new();
    private readonly MemoryPool<byte> _memoryPool = MemoryPool<byte>.Shared;

    public AdvancedUdpHandler(ILogger? logger = null)
    {
        _logger = logger;
        _statsTimer = new Timer(
            LogStatistics,
            null,
            TimeSpan.FromMinutes(1),
            TimeSpan.FromMinutes(1)
        );
    }

    public async ValueTask HandleAsync(
        ReadOnlyMemory<byte> data,
        EndPoint remoteEndPoint,
        Func<ReadOnlyMemory<byte>, ValueTask> sendResponse,
        CancellationToken cancellationToken = default
    )
    {
        Interlocked.Increment(ref _totalRequests);

        try
        {
            // 获取或创建客户端会话
            var session = _sessions.GetOrAdd(remoteEndPoint, ep => new ClientSession(ep));

            // 检测心跳包
            if (data.Length == 4 && data.Span.SequenceEqual(new byte[] { 0x00, 0x00, 0x00, 0x00 }))
            {
                session.UpdateLastActivity();
                await sendResponse(new byte[] { 0xFF }); // 心跳响应
                return;
            }

            // 命令处理
            if (data.Span[0] == 0x2F) // '/' 字符
            {
                await HandleCommand(data, remoteEndPoint, sendResponse, session);
                return;
            }

            // 文件传输处理
            if (data.Span[0] == 0x01) // 文件传输标识
            {
                await HandleFileTransfer(data, remoteEndPoint, sendResponse, session);
                return;
            }

            // 默认处理逻辑：Echo + 额外信息
            await HandleEcho(data, remoteEndPoint, sendResponse, session);
        }
        catch (Exception ex)
        {
            _logger?.LogError(ex, "处理UDP请求时出错");
            await SendErrorResponse(sendResponse, "处理错误");
        }
    }

    private async ValueTask HandleEcho(
        ReadOnlyMemory<byte> data,
        EndPoint remoteEndPoint,
        Func<ReadOnlyMemory<byte>, ValueTask> sendResponse,
        ClientSession session
    )
    {
        session.UpdateLastActivity();

        // 解码消息
        var message = Encoding.UTF8.GetString(data.Span);
        session.MessageCount++;

        _logger?.LogInformation($"收到来自 {remoteEndPoint} 的消息: {message}");

        // 构建响应
        var responseMessage = $"ECHO [{session.MessageCount}] {message}";
        var responseData = Encoding.UTF8.GetBytes(responseMessage);

        // 发送响应
        await sendResponse(responseData);

        // 如果消息包含"double"，发送额外响应
        if (message.Contains("double", StringComparison.OrdinalIgnoreCase))
        {
            var extraResponse = Encoding.UTF8.GetBytes($"额外响应: {DateTime.UtcNow:HH:mm:ss}");
            await sendResponse(extraResponse);
        }
    }

    private async ValueTask HandleCommand(
        ReadOnlyMemory<byte> data,
        EndPoint remoteEndPoint,
        Func<ReadOnlyMemory<byte>, ValueTask> sendResponse,
        ClientSession session
    )
    {
        var commandText = Encoding.UTF8.GetString(data.Span[1..]);
        _logger?.LogInformation($"收到命令: {commandText}");

        switch (commandText.ToLower())
        {
            case "stats":
                var stats = $"请求总数: {_totalRequests}, 会话数: {_sessions.Count}";
                await sendResponse(Encoding.UTF8.GetBytes(stats));
                break;

            case "time":
                await sendResponse(Encoding.UTF8.GetBytes(DateTime.UtcNow.ToString("O")));
                break;

            case "reset":
                session.Reset();
                await sendResponse(Encoding.UTF8.GetBytes("会话已重置"));
                break;

            default:
                await sendResponse(Encoding.UTF8.GetBytes($"未知命令: {commandText}"));
                break;
        }
    }

    private async ValueTask HandleFileTransfer(
        ReadOnlyMemory<byte> data,
        EndPoint remoteEndPoint,
        Func<ReadOnlyMemory<byte>, ValueTask> sendResponse,
        ClientSession session
    )
    {
        // 文件传输协议：
        // 字节0: 0x01 (文件标识)
        // 字节1: 分片索引
        // 字节2-5: 文件大小 (uint)
        // 字节6-: 文件数据

        var sliceIndex = data.Span[1];
        var fileSize = BitConverter.ToUInt32(data.Span[2..6]);

        // 初始化文件缓冲区
        if (session.FileBuffer == null)
        {
            session.FileBuffer = _memoryPool.Rent((int)fileSize);
            session.FileSize = fileSize;
            session.ReceivedSlices = 0;
        }

        // 存储数据
        var fileData = data[6..];
        fileData.CopyTo(session.FileBuffer.Memory.Slice(sliceIndex * 1024, fileData.Length));
        session.ReceivedSlices++;

        // 发送确认
        var ackData = new byte[] { 0x02, sliceIndex }; // 0x02 = ACK
        await sendResponse(ackData);

        // 检查是否完成
        if (session.ReceivedSlices >= (fileSize + 1023) / 1024)
        {
            _logger?.LogInformation($"文件接收完成! 大小: {fileSize} 字节");
            await ProcessCompletedFile(session, sendResponse);
        }
    }

    private async ValueTask ProcessCompletedFile(
        ClientSession session,
        Func<ReadOnlyMemory<byte>, ValueTask> sendResponse
    )
    {
        try
        {
            // 这里可以添加实际的文件处理逻辑
            // 例如：保存到磁盘、分析内容等

            // 演示：计算SHA256哈希
            using var sha256 = System.Security.Cryptography.SHA256.Create();
            var hash = sha256.ComputeHash(session.FileBuffer.Memory.Span[..(int)session.FileSize]);
            var hashString = BitConverter.ToString(hash).Replace("-", "");

            await sendResponse(Encoding.UTF8.GetBytes($"文件接收完成! SHA256: {hashString}"));
        }
        finally
        {
            // 清理资源
            session.FileBuffer?.Dispose();
            session.FileBuffer = null;
            session.FileSize = 0;
        }
    }

    private async ValueTask SendErrorResponse(
        Func<ReadOnlyMemory<byte>, ValueTask> sendResponse,
        string message
    )
    {
        var errorData = Encoding.UTF8.GetBytes($"ERROR: {message}");
        await sendResponse(errorData);
    }

    private void LogStatistics(object? state)
    {
        var inactiveSessions = _sessions.Count(p =>
            DateTime.UtcNow - p.Value.LastActivity > TimeSpan.FromMinutes(5)
        );

        _logger.InfoAsync(
            $"UDP统计: 总请求={_totalRequests}, "
                + $"活动会话={_sessions.Count}, "
                + $"非活动会话={inactiveSessions}"
        );

        // 清理非活动会话
        foreach (var (ep, session) in _sessions)
        {
            if (DateTime.UtcNow - session.LastActivity > TimeSpan.FromMinutes(10))
            {
                _sessions.TryRemove(ep, out _);
                session.Dispose();
            }
        }
    }

    public void Dispose()
    {
        _statsTimer.Dispose();
        foreach (var session in _sessions.Values)
        {
            session.Dispose();
        }
        _sessions.Clear();
    }

    private class ClientSession : IDisposable
    {
        public EndPoint EndPoint { get; }
        public DateTime LastActivity { get; private set; } = DateTime.UtcNow;
        public int MessageCount { get; set; }
        public IMemoryOwner<byte>? FileBuffer { get; set; }
        public uint FileSize { get; set; }
        public int ReceivedSlices { get; set; }

        public ClientSession(EndPoint endPoint)
        {
            EndPoint = endPoint;
        }

        public void UpdateLastActivity() => LastActivity = DateTime.UtcNow;

        public void Reset()
        {
            MessageCount = 0;
            FileBuffer?.Dispose();
            FileBuffer = null;
            FileSize = 0;
            ReceivedSlices = 0;
        }

        public void Dispose()
        {
            FileBuffer?.Dispose();
        }
    }
}
