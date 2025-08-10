namespace Pico.Node.Sample;

public class MyBytesHandler(ILogger<MyBytesHandler> logger) : IUdpHandler
{
    private readonly ILogger<MyBytesHandler> _logger =
        logger ?? throw new ArgumentNullException(nameof(logger));

    public async ValueTask HandleAsync(
        ReadOnlyMemory<byte> data,
        IPEndPoint remoteEndPoint,
        CancellationToken cancellationToken = default
    )
    {
        try
        {
            var remoteIp = remoteEndPoint.ToString();

            await _logger.InfoAsync(
                $"Received {data.Length} bytes from {remoteIp}",
                cancellationToken
            );

            var (hexDump, textDump) = FormatData(data);

            await _logger.DebugAsync(
                $"Data details from {remoteIp}:\n Hex: {hexDump}\n Text: {textDump}",
                cancellationToken: cancellationToken
            );

            ProcessPayload(data.Span);
        }
        catch (Exception ex)
        {
            await _logger.ErrorAsync(
                $"Error processing UDP data from {remoteEndPoint}",
                ex,
                cancellationToken
            );
        }
    }

    private void ProcessPayload(ReadOnlySpan<byte> payload)
    {
        try
        {
            var message = Encoding.UTF8.GetString(payload);
            _logger.Info($"Received message: {message}");
        }
        catch
        {
            _logger.Info(
                $"Binary payload received. First 8 bytes: {BitConverter.ToString(payload.Slice(0, Math.Min(8, payload.Length)).ToArray())}"
            );
        }
    }

    private static (string HexDump, string TextDump) FormatData(ReadOnlyMemory<byte> data)
    {
        const int maxDumpSize = 128;
        var span = data.Span;
        var length = Math.Min(span.Length, maxDumpSize);

        var hexBuilder = new StringBuilder(length * 3);
        for (var i = 0; i < length; i++)
        {
            hexBuilder.Append($"{span[i]:X2} ");
            if ((i + 1) % 16 == 0)
                hexBuilder.AppendLine();
        }

        var textBuilder = new StringBuilder(length);
        foreach (var b in span.Slice(0, length))
        {
            textBuilder.Append(b is >= 32 and <= 126 ? (char)b : '.');
        }

        if (span.Length <= maxDumpSize)
            return (hexBuilder.ToString(), textBuilder.ToString());
        hexBuilder.AppendLine("... [truncated]");
        textBuilder.Append("... [truncated]");

        return (hexBuilder.ToString(), textBuilder.ToString());
    }
}
