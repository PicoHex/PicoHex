namespace PicoHex.Configuration.Abstractions;

public interface ICfgProvider : ICfgNode
{
    ValueTask LoadAsync(CancellationToken ct = default);
}
