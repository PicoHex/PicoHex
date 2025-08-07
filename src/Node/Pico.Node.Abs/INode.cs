namespace Pico.Node.Abs;

public interface INode : IDisposable, IAsyncDisposable
{
    Task StartAsync(CancellationToken cancellationToken = default);
    Task StopAsync(TimeSpan? timeout = null);
}
