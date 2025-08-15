namespace Pico.Node.Core;

public interface INode : IAsyncDisposable
{
    Task StartAsync(CancellationToken cancellationToken = default);
    Task StopAsync(CancellationToken cancellationToken = default);
}
