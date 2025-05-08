namespace Pico.Cfg.Abstractions;

public interface IAsyncChangeToken
{
    bool HasChanged { get; }
    ValueTask WaitForChangeAsync(CancellationToken ct = default);
}
