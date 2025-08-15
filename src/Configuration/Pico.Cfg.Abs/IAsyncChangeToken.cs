namespace Pico.Cfg.Abs;

public interface IAsyncChangeToken
{
    bool HasChanged { get; }
    ValueTask WaitForChangeAsync(CancellationToken ct = default);
}
