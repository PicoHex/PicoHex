namespace Pico.DI.Abstractions;

public interface ISvcProvider : ISvcResolver, IDisposable, IAsyncDisposable
{
    ISvcScope CreateScope();
}
